using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NTSPoint = NetTopologySuite.Geometries.Point;

namespace StrategyGame
{
    /// <summary>
    /// A pre-computation tool to generate a global urban texture layer.
    /// This should be run once by the developer/player to create the data file.
    /// </summary>
    public static class UrbanAreaRenderer
    {
        // Define the output resolution for the global urban texture.
        // 14400px width corresponds to a ~40km ground resolution per pixel at the equator.
        private const int TEXTURE_WIDTH = 14400;
        private const int TEXTURE_HEIGHT = TEXTURE_WIDTH / 2;

        public static async Task GenerateUrbanTextureLayer()
        {
            await Task.Run(() =>
            {
                // Load the required shapefiles
                var urbanPolygons = LoadGeometries(NaturalEarthOverlayGenerator.UrbanAreasShpPath);
                var cities = CityDensityRenderer.LoadCitiesFromNaturalEarth(
                    NaturalEarthOverlayGenerator.CitiesPath,
                    NaturalEarthOverlayGenerator.UrbanAreasShpPath
                );

                Console.WriteLine("[Urban Gen] Creating new urban texture image...");
                using var image = new Image<Rgba32>(TEXTURE_WIDTH, TEXTURE_HEIGHT, new Rgba32(0, 0, 0, 0)); // Start with a fully transparent image

                // --- Step 1: Render Major Urban Area Polygons ---
                Console.WriteLine("[Urban Gen] Rendering major urban polygons...");
                var urbanSprawlColor = new Rgba32(100, 100, 100, 100); // Semi-transparent grey for sprawl
                foreach (var polygon in urbanPolygons)
                {
                    RenderPolygon(image, polygon, urbanSprawlColor);
                }

                // --- Step 2: Render Smaller Towns and Cities ---
                Console.WriteLine("[Urban Gen] Rendering smaller towns...");
                var smallerCities = cities.Where(c => !urbanPolygons.Any(p => p.Contains(new NTSPoint(c.Longitude, c.Latitude)))).ToList();
                foreach (var city in smallerCities)
                {
                    // Draw a soft "blotch" for smaller towns not in a major urban area
                    float radius = (float)Math.Sqrt(city.Population) / 100f; // Radius based on population
                    radius = Math.Clamp(radius, 2, 50); // Clamp size
                    var townBlotchColor = new Rgba32(100, 100, 100, 70); // More transparent for towns

                    int x = (int)(((city.Longitude + 180.0) / 360.0) * TEXTURE_WIDTH);
                    int y = (int)(((90.0 - city.Latitude) / 180.0) * TEXTURE_HEIGHT);

                    // Draw circle using pixel-based approach
                    DrawCircle(image, x, y, (int)radius, townBlotchColor);
                }

                // --- Step 3: Save the Final Texture ---
                string outputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "data", "urban_texture.png" // Save as PNG to preserve transparency
                );
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                image.Save(outputPath);
                Console.WriteLine($"[Urban Gen] Urban texture layer saved successfully to: {outputPath}");
            });
        }

        private static void DrawCircle(Image<Rgba32> image, int centerX, int centerY, int radius, Rgba32 color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int pixelX = centerX + x;
                        int pixelY = centerY + y;
                        
                        if (pixelX >= 0 && pixelX < image.Width && pixelY >= 0 && pixelY < image.Height)
                        {
                            image[pixelX, pixelY] = color;
                        }
                    }
                }
            }
        }

        private static List<Geometry> LoadGeometries(string shpPath)
        {
            var geometries = new List<Geometry>();
            if (!File.Exists(shpPath)) return geometries;
            using var reader = new ShapefileDataReader(shpPath, GeometryFactory.Default);
            while (reader.Read())
            {
                if (reader.Geometry != null && !reader.Geometry.IsEmpty)
                    geometries.Add(reader.Geometry);
            }
            return geometries;
        }
        
        // Helper to render a single polygon onto the large image
        private static void RenderPolygon(Image<Rgba32> image, Geometry geometry, Rgba32 color)
        {
            if (geometry is Polygon p)
            {
                var points = p.ExteriorRing.Coordinates.Select(c => new SixLabors.ImageSharp.PointF(
                    (float)((c.X + 180.0) / 360.0 * image.Width),
                    (float)((90.0 - c.Y) / 180.0 * image.Height)
                )).ToArray();
                image.Mutate(ctx => ctx.Fill(color, new SixLabors.ImageSharp.Drawing.Polygon(points)));
            }
            else if (geometry is MultiPolygon mp)
            {
                foreach (var poly in mp.Geometries.Cast<Polygon>())
                {
                    RenderPolygon(image, poly, color);
                }
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility. Now does nothing since we use pre-generated texture.
        /// </summary>
        public static void DrawUrbanAreasOnTile(Image<Rgba32> tileImage, int cellSize, int tileX, int tileY, int tileSizePx)
        {
            // This method is now deprecated. Urban areas are rendered using the pre-generated texture
            // in the tile generation pipeline in PixelMapGenerator.
        }
    }
}