using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Generates a population density map from Natural Earth populated places data.
    /// Creates a heat map where red indicates low/no population and bright green indicates high population density.
    /// Country borders are overlaid on top for reference.
    /// </summary>
    public static class PopulationDensityRenderer
    {
        private const int TextureWidth = 4096;
        private const int TextureHeight = 2048;
        
        private static readonly object GdalLock = new();
        private static bool _gdalConfigured = false;

        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");

        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        private static readonly string DataFileList = Path.Combine(RepoRoot, "DataFileNames");
        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        private static Dictionary<string, string> LoadDataFiles()
        {
            var dict = new Dictionary<string, string>();
            if (File.Exists(DataFileList))
            {
                foreach (var line in File.ReadAllLines(DataFileList))
                {
                    if (line.Contains('='))
                    {
                        var parts = line.Split('=', 2);
                        dict[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            return dict;
        }

        private static string GetDataFile(string name)
        {
            if (DataFiles.TryGetValue(name, out var mapped) && File.Exists(mapped))
                return mapped;

            string userPath = Path.Combine(DataDir, name);
            if (File.Exists(userPath))
                return userPath;

            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;

            // Try cities subdirectory
            string citiesPath = Path.Combine(RepoDataDir, "cities", name);
            if (File.Exists(citiesPath))
                return citiesPath;

            return userPath;
        }

        /// <summary>
        /// Generates a population density map as an SKBitmap
        /// </summary>
        public static SKBitmap? GeneratePopulationDensityMap()
        {
            lock (GdalLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            string placesPath = GetDataFile("ne_10m_populated_places.shp");
            string countriesPath = GetDataFile("ne_10m_admin_0_countries.shp");
            
            if (!File.Exists(placesPath))
            {
                Console.WriteLine($"Warning: Could not find populated places file at {placesPath}");
                return null;
            }

            try
            {
                // Create a density grid to accumulate population data
                float[,] densityGrid = new float[TextureHeight, TextureWidth];
                
                // Read populated places and build density map
                using DataSource placesDs = Ogr.Open(placesPath, 0);
                Layer placesLayer = placesDs.GetLayerByIndex(0);
                
                List<float> allPopulations = new List<float>();
                
                // First pass: collect all population values to determine scaling
                placesLayer.ResetReading();
                Feature feat;
                while ((feat = placesLayer.GetNextFeature()) != null)
                {
                    var geom = feat.GetGeometryRef();
                    if (geom == null) continue;
                    
                    double lon = geom.GetX(0);
                    double lat = geom.GetY(0);
                    
                    int pop = 0;
                    if (feat.GetFieldIndex("POP_MAX") != -1)
                        pop = feat.GetFieldAsInteger("POP_MAX");
                    else if (feat.GetFieldIndex("pop_max") != -1)
                        pop = feat.GetFieldAsInteger("pop_max");
                    
                    if (pop > 0)
                        allPopulations.Add(pop);
                }
                
                if (allPopulations.Count == 0)
                {
                    Console.WriteLine("Warning: No population data found in populated places file");
                    return null;
                }
                
                // Calculate logarithmic scaling for better visualization
                float maxPop = allPopulations.Max();
                float minPop = allPopulations.Where(p => p > 0).Min();
                float logMax = (float)Math.Log10(maxPop);
                float logMin = (float)Math.Log10(minPop);
                
                // Second pass: populate density grid
                placesLayer.ResetReading();
                while ((feat = placesLayer.GetNextFeature()) != null)
                {
                    var geom = feat.GetGeometryRef();
                    if (geom == null) continue;
                    
                    double lon = geom.GetX(0);
                    double lat = geom.GetY(0);
                    
                    // Convert to pixel coordinates
                    int px = (int)((lon + 180.0) / 360.0 * TextureWidth);
                    int py = (int)((90.0 - lat) / 180.0 * TextureHeight);
                    
                    if (px < 0 || px >= TextureWidth || py < 0 || py >= TextureHeight)
                        continue;
                    
                    int pop = 0;
                    if (feat.GetFieldIndex("POP_MAX") != -1)
                        pop = feat.GetFieldAsInteger("POP_MAX");
                    else if (feat.GetFieldIndex("pop_max") != -1)
                        pop = feat.GetFieldAsInteger("pop_max");
                    
                    if (pop > 0)
                    {
                        // Use logarithmic scaling for better visualization
                        float logPop = (float)Math.Log10(pop);
                        float normalizedPop = (logPop - logMin) / (logMax - logMin);
                        
                        // Add influence in a radius around the city based on population
                        int radius = Math.Max(1, (int)(normalizedPop * 8) + 1);
                        AddDensityInfluence(densityGrid, px, py, normalizedPop, radius);
                    }
                }
                
                // Convert density grid to SKBitmap
                SKBitmap bitmap = new SKBitmap(TextureWidth, TextureHeight);
                
                for (int y = 0; y < TextureHeight; y++)
                {
                    for (int x = 0; x < TextureWidth; x++)
                    {
                        float density = Math.Min(1.0f, densityGrid[y, x]);
                        SKColor color = GetDensityColor(density);
                        bitmap.SetPixel(x, y, color);
                    }
                }
                
                // Overlay country borders if available
                if (File.Exists(countriesPath))
                {
                    DrawCountryBorders(bitmap, countriesPath);
                }
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating population density map: {ex.Message}");
                return null;
            }
        }

        private static void AddDensityInfluence(float[,] grid, int centerX, int centerY, float intensity, int radius)
        {
            int height = grid.GetLength(0);
            int width = grid.GetLength(1);
            
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (distance <= radius)
                        {
                            // Gaussian-like falloff
                            float falloff = (float)Math.Exp(-(distance * distance) / (2.0 * radius * radius / 4.0));
                            grid[y, x] += intensity * falloff;
                        }
                    }
                }
            }
        }

        private static SKColor GetDensityColor(float density)
        {
            // Create a gradient from red (low density) to bright green (high density)
            if (density <= 0.0f)
                return new SKColor(139, 0, 0); // Dark red for no population
            
            // Interpolate between red and green
            float r, g, b;
            
            if (density < 0.5f)
            {
                // Red to yellow transition
                float t = density * 2.0f;
                r = 1.0f;
                g = t;
                b = 0.0f;
            }
            else
            {
                // Yellow to bright green transition
                float t = (density - 0.5f) * 2.0f;
                r = 1.0f - t;
                g = 1.0f;
                b = 0.0f;
            }
            
            return new SKColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 200);
        }

        private static void DrawCountryBorders(SKBitmap bitmap, string countriesPath)
        {
            try
            {
                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint
                {
                    Color = SKColors.Black,
                    StrokeWidth = 1,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };

                using DataSource ds = Ogr.Open(countriesPath, 0);
                Layer layer = ds.GetLayerByIndex(0);
                
                layer.ResetReading();
                Feature feat;
                while ((feat = layer.GetNextFeature()) != null)
                {
                    var geom = feat.GetGeometryRef();
                    if (geom == null) continue;
                    
                    DrawGeometry(canvas, paint, geom);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not draw country borders: {ex.Message}");
            }
        }

        private static void DrawGeometry(SKCanvas canvas, SKPaint paint, Geometry geom)
        {
            switch (geom.GetGeometryType())
            {
                case wkbGeometryType.wkbPolygon:
                case wkbGeometryType.wkbPolygon25D:
                    DrawPolygon(canvas, paint, geom);
                    break;
                    
                case wkbGeometryType.wkbMultiPolygon:
                case wkbGeometryType.wkbMultiPolygon25D:
                    for (int i = 0; i < geom.GetGeometryCount(); i++)
                    {
                        var subGeom = geom.GetGeometryRef(i);
                        if (subGeom != null)
                            DrawPolygon(canvas, paint, subGeom);
                    }
                    break;
            }
        }

        private static void DrawPolygon(SKCanvas canvas, SKPaint paint, Geometry geom)
        {
            // Draw exterior ring
            var ring = geom.GetGeometryRef(0);
            if (ring != null)
                DrawLineString(canvas, paint, ring);
        }

        private static void DrawLineString(SKCanvas canvas, SKPaint paint, Geometry ring)
        {
            int pointCount = ring.GetPointCount();
            if (pointCount < 2) return;

            using var path = new SKPath();
            bool firstPoint = true;
            
            for (int i = 0; i < pointCount; i++)
            {
                double lon = ring.GetX(i);
                double lat = ring.GetY(i);
                
                // Convert to pixel coordinates
                float px = (float)((lon + 180.0) / 360.0 * TextureWidth);
                float py = (float)((90.0 - lat) / 180.0 * TextureHeight);
                
                if (firstPoint)
                {
                    path.MoveTo(px, py);
                    firstPoint = false;
                }
                else
                {
                    path.LineTo(px, py);
                }
            }
            
            canvas.DrawPath(path, paint);
        }
    }
}