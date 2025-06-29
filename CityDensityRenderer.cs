using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Index.Strtree;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace StrategyGame
{
    /// <summary>
    /// Utility to load Natural Earth city data and render urban areas as
    /// pixel-art base colors. This keeps city shapes conforming to the
    /// underlying density polygons.
    /// </summary>
    public static class CityDensityRenderer
    {
        private static readonly Random Random = new Random();
        private const double KM_PER_DEGREE = 111.32; // At equator

        /// <summary>
        /// Load populated places and assign the nearest/containing urban area
        /// polygon to each created city.
        /// </summary>
        public static List<City> LoadCitiesFromNaturalEarth(
            string placesShpPath,
            string urbanAreasShpPath)
        {
            var cities = new List<City>();
            
            if (!File.Exists(placesShpPath))
            {
                Console.WriteLine($"[ERROR] Places shapefile not found: {placesShpPath}");
                return cities;
            }
            
            try
            {
                using var reader = new ShapefileDataReader(placesShpPath, GeometryFactory.Default);

                // Debug: Print available columns
                var header = reader.DbaseHeader;
                Console.WriteLine($"[DEBUG] Available columns in {System.IO.Path.GetFileName(placesShpPath)}:");
                for (int i = 0; i < header.NumFields; i++)
                {
                    var field = header.Fields[i];
                    Console.WriteLine($"  [{i}] {field.Name} ({field.DbaseType})");
                }

                // Try to get column indices with error handling
                int nameIdx = -1;
                int popIdx = -1;
                int scaleRankIdx = -1;
                
                try
                {
                    nameIdx = reader.GetOrdinal("NAME");
                }
                catch (IndexOutOfRangeException)
                {
                    // Try alternative column names
                    try { nameIdx = reader.GetOrdinal("NAME_EN"); }
                    catch { try { nameIdx = reader.GetOrdinal("NAMEASCII"); } catch { } }
                }
                
                try
                {
                    popIdx = reader.GetOrdinal("POP_MAX");
                }
                catch (IndexOutOfRangeException)
                {
                    // Try alternative population column names
                    try { popIdx = reader.GetOrdinal("POP_EST"); }
                    catch { try { popIdx = reader.GetOrdinal("POPULATION"); } catch { } }
                }

                try
                {
                    scaleRankIdx = reader.GetOrdinal("SCALERANK");
                }
                catch (IndexOutOfRangeException)
                {
                    // Try alternative scale rank column names
                    try { scaleRankIdx = reader.GetOrdinal("SCALE_RANK"); }
                    catch { try { scaleRankIdx = reader.GetOrdinal("ScaleRank"); } catch { } }
                }

                if (nameIdx == -1)
                {
                    Console.WriteLine("[ERROR] Could not find name column in shapefile");
                    return cities;
                }

                Console.WriteLine($"[DEBUG] Using name column: {header.Fields[nameIdx].Name}");
                if (popIdx != -1)
                {
                    Console.WriteLine($"[DEBUG] Using population column: {header.Fields[popIdx].Name}");
                }
                else
                {
                    Console.WriteLine("[WARNING] No population column found, using default population");
                }

                if (scaleRankIdx != -1)
                {
                    Console.WriteLine($"[DEBUG] Using scale rank column: {header.Fields[scaleRankIdx].Name}");
                }
                else
                {
                    Console.WriteLine("[WARNING] No scale rank column found, using default scale rank");
                }

                while (reader.Read())
                {
                    if (reader.Geometry is not NetTopologySuite.Geometries.Point pt)
                        continue;

                    string cityName = "Unknown";
                    try
                    {
                        cityName = reader.GetString(nameIdx) ?? "Unknown";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARNING] Failed to read city name: {ex.Message}");
                    }

                    int population = 50000; // Default population
                    if (popIdx != -1)
                    {
                        try
                        {
                            population = reader.GetInt32(popIdx);
                            if (population <= 0) population = 50000; // Use default for invalid values
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Failed to read population for {cityName}: {ex.Message}");
                        }
                    }

                    int scaleRank = 5; // Default scale rank
                    if (scaleRankIdx != -1)
                    {
                        try
                        {
                            scaleRank = reader.GetInt32(scaleRankIdx);
                            if (scaleRank < 0) scaleRank = 5; // Use default for invalid values
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Failed to read scale rank for {cityName}: {ex.Message}");
                        }
                    }

                    var city = new City(cityName)
                    {
                        Population = population,
                        Longitude = pt.X,
                        Latitude = pt.Y,
                        ScaleRank = scaleRank
                    };
                    
                    // Create a central suburb for the city to store its location indirectly
                    var centralSuburb = new Suburb(
                        "City Center", 
                        city.Population, 
                        city.Population * 1.2, // Slightly higher capacity than population
                        0.8); // Initial housing quality
                    
                    // Store coordinates in the suburb's railway length and quality
                    // as a temporary measure (these will be updated later)
                    centralSuburb.RailwayKilometers = pt.X * KM_PER_DEGREE;
                    centralSuburb.HousingQuality = pt.Y;
                    
                    city.Suburbs.Add(centralSuburb);
                    cities.Add(city);
                }

                Console.WriteLine($"[DEBUG] Loaded {cities.Count} cities from shapefile");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load cities from shapefile: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return cities;
            }

            // Assign urban areas to cities
            if (File.Exists(urbanAreasShpPath))
            {
                try
                {
                    AssignUrbanAreas(cities, urbanAreasShpPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to assign urban areas: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[WARNING] Urban areas shapefile not found: {urbanAreasShpPath}");
            }

            return cities;
        }

        /// <summary>
        /// Renders urban areas as pixel art on the provided image
        /// </summary>
        public static void RenderUrbanAreas(
            Image<Rgba32> image, 
            string urbanAreasShpPath,
            int mapWidth,
            int mapHeight)
        {
            Console.WriteLine("[DEBUG] RenderUrbanAreas CALLED!");
            using var reader = new ShapefileDataReader(urbanAreasShpPath, GeometryFactory.Default);
            while (reader.Read())
            {
                if (reader.Geometry is not Geometry geom)
                    continue;

                var baseColor = GenerateUrbanColor();
                var palette = GeneratePixelArtPalette(baseColor);

                if (geom is NetTopologySuite.Geometries.Polygon poly)
                {
                    RenderUrbanPolygon(image, poly, mapWidth, mapHeight, palette);
                }
                else if (geom is MultiPolygon multi)
                {
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        if (multi.GetGeometryN(i) is NetTopologySuite.Geometries.Polygon p)
                        {
                            RenderUrbanPolygon(image, p, mapWidth, mapHeight, palette);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Renders city icons (visible at all zoom levels) on the provided image.
        /// Only cities inside or near urban area polygons are rendered.
        /// Call this after RenderUrbanAreas.
        /// </summary>
        public static void RenderCities(
            Image<Rgba32> image,
            List<City> cities,
            int mapWidth,
            int mapHeight,
            string urbanAreasShpPath,
            double maxDistanceDegrees = 0.2) // ~22km at equator
        {
            if (cities == null || cities.Count == 0) return;
            
            // Load all urban area polygons
            var urbanPolygons = new List<Geometry>();
            using (var reader = new ShapefileDataReader(urbanAreasShpPath, GeometryFactory.Default))
            {
                while (reader.Read())
                {
                    if (reader.Geometry is NetTopologySuite.Geometries.Polygon poly)
                        urbanPolygons.Add(poly);
                    else if (reader.Geometry is MultiPolygon multi)
                    {
                        for (int i = 0; i < multi.NumGeometries; i++)
                        {
                            if (multi.GetGeometryN(i) is NetTopologySuite.Geometries.Polygon p)
                                urbanPolygons.Add(p);
                        }
                    }
                }
            }
            foreach (var city in cities)
            {
                var cityPoint = new NetTopologySuite.Geometries.Point(city.Longitude, city.Latitude) { SRID = 4326 };
                
                // Check if city is inside or near any urban polygon
                bool inUrban = urbanPolygons.Any(poly =>
                    poly.Contains(cityPoint) || poly.IsWithinDistance(cityPoint, maxDistanceDegrees)
                );
                
                if (!inUrban) continue;
                
                int x = (int)((city.Longitude + 180.0) / 360.0 * mapWidth);
                int y = (int)((90.0 - city.Latitude) / 180.0 * mapHeight);
                int size = city.Population > 1000000 ? 10 : city.Population > 100000 ? 7 : 5;
                var color = city.Population > 1000000 ? new Rgba32(255,215,0) : new Rgba32(200,50,50);
                image.Mutate(ctx =>
                {
                    ctx.Fill(color, new SixLabors.ImageSharp.Drawing.EllipsePolygon(x, y, size));
                    ctx.Draw(SixLabors.ImageSharp.Color.Black, 1.5f, new SixLabors.ImageSharp.Drawing.EllipsePolygon(x, y, size));
                });
            }
        }

        private static void AssignUrbanAreas(List<City> cities, string urbanAreasShpPath)
        {
            if (!File.Exists(urbanAreasShpPath))
            {
                Console.WriteLine($"[WARNING] Urban areas shapefile not found: {urbanAreasShpPath}");
                return;
            }

            try
            {
                using var reader = new ShapefileDataReader(urbanAreasShpPath, GeometryFactory.Default);
                var urbanList = new List<(Geometry geom, string name)>();
                
                // Check if NAME column exists
                int nameIdx = -1;
                try
                {
                    nameIdx = reader.GetOrdinal("NAME");
                }
                catch (IndexOutOfRangeException)
                {
                    // Try alternative column names
                    try { nameIdx = reader.GetOrdinal("NAME_EN"); }
                    catch { try { nameIdx = reader.GetOrdinal("URBAN_AREA"); } catch { } }
                }

                while (reader.Read())
                {
                    if (reader.Geometry is not Geometry geom)
                        continue;

                    string areaName = "Unknown Urban Area";
                    if (nameIdx != -1)
                    {
                        try
                        {
                            areaName = reader.GetString(nameIdx) ?? "Unknown Urban Area";
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Failed to read urban area name: {ex.Message}");
                        }
                    }

                    urbanList.Add((geom, areaName));
                }

                Console.WriteLine($"[DEBUG] Loaded {urbanList.Count} urban areas");

                // Build STRtree spatial index for fast urban area queries
                var idx = new STRtree<(Geometry geom, string name)>();
                foreach (var ua in urbanList)
                {
                    idx.Insert(ua.geom.EnvelopeInternal, ua);
                }

                foreach (var city in cities)
                {
                    var pt = new NetTopologySuite.Geometries.Point(city.Longitude, city.Latitude) 
                    { 
                        SRID = 4326 // WGS84
                    };
                    
                    // Query only nearby urban areas using spatial index
                    var candidates = idx.Query(pt.EnvelopeInternal);
                    if (candidates.Count == 0) continue;
                    
                    // Pick the nearest from the candidates
                    var nearest = candidates.OrderBy(x => x.geom.Distance(pt)).First();
                    if (nearest.geom.Distance(pt) < 0.1) // 0.1° ~ 11 km
                    {
                        city.Name = $"{city.Name} ({nearest.name} Urban Area)";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to assign urban areas: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        private static void RenderUrbanPolygon(
            Image<Rgba32> image,
            NetTopologySuite.Geometries.Polygon polygon,
            int mapWidth,
            int mapHeight,
            Rgba32[] palette,
            float density = 0.15f)
        {
            // 1. Get the geo-bounds of this polygon
            var b = polygon.EnvelopeInternal;
            double minLon = b.MinX, maxLon = b.MaxX;
            double minLat = b.MinY, maxLat = b.MaxY;

            // 2. Convert those to pixel extents
            int minX = (int)Math.Max(0, ((minLon + 180.0) / 360.0) * mapWidth);
            int maxX = (int)Math.Min(image.Width, ((maxLon + 180.0) / 360.0) * mapWidth);
            int minY = (int)Math.Max(0, ((90.0 - maxLat) / 180.0) * mapHeight);
            int maxY = (int)Math.Min(image.Height, ((90.0 - minLat) / 180.0) * mapHeight);

            // 3. For each pixel in that box, test the real lon/lat
            image.Mutate(ctx =>
            {
                for (int y = minY; y <= maxY; y++)
                {
                    double lat = 90.0 - (y + 0.5) * 180.0 / mapHeight;
                    for (int x = minX; x <= maxX; x++)
                    {
                        double lon = (x + 0.5) * 360.0 / mapWidth - 180.0;

                        if (polygon.Contains(new NetTopologySuite.Geometries.Point(lon, lat))
                            && Random.NextDouble() < density)
                        {
                            var c = palette[Random.Next(palette.Length)];
                            // 2×2 “building”
                            ctx.Fill(c, new RectangularPolygon(x, y, 2, 2));
                        }
                    }
                }
            });
        }

        private static Rgba32 GenerateUrbanColor()
        {
            // Generate a muted color suitable for urban areas
            return new Rgba32(
                (byte)Random.Next(130, 180),
                (byte)Random.Next(130, 180),
                (byte)Random.Next(130, 180),
                255);
        }

        private static Rgba32[] GeneratePixelArtPalette(Rgba32 baseColor)
        {
            // Create variations of the base color for pixel art effect
            return new[]
            {
                baseColor,
                DarkenColor(baseColor, 0.2f),
                LightenColor(baseColor, 0.2f),
                DarkenColor(baseColor, 0.4f),
                LightenColor(baseColor, 0.4f)
            };
        }

        private static Rgba32 DarkenColor(Rgba32 color, float amount)
        {
            return new Rgba32(
                (byte)(color.R * (1 - amount)),
                (byte)(color.G * (1 - amount)),
                (byte)(color.B * (1 - amount)),
                color.A);
        }

        private static Rgba32 LightenColor(Rgba32 color, float amount)
        {
            return new Rgba32(
                (byte)Math.Min(255, color.R * (1 + amount)),
                (byte)Math.Min(255, color.G * (1 + amount)),
                (byte)Math.Min(255, color.B * (1 + amount)),
                color.A);
        }
    }
}
