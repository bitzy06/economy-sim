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
    public static class PopulationDensityRenderer
    {
        private const int TextureWidth = 4096;
        private const int TextureHeight = 2048;
        private static readonly object GdalLock = new();
        private static bool _gdalConfigured = false;

        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data");
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
            if (DataFiles.TryGetValue(name, out var mapped) && File.Exists(mapped)) return mapped;
            string userPath = Path.Combine(DataDir, name); if (File.Exists(userPath)) return userPath;
            string repoPath = Path.Combine(RepoDataDir, name); if (File.Exists(repoPath)) return repoPath;
            // Try recursive search (covers nested Natural Earth folder layouts)
            var recursive = FindFileRecursive(DataDir, name) ?? FindFileRecursive(RepoDataDir, name);
            if (recursive != null) return recursive;
            // Try cities subdirectory (legacy)
            string citiesPath = Path.Combine(RepoDataDir, "cities", name); if (File.Exists(citiesPath)) return citiesPath;
            return userPath; // final attempt path (likely missing)
        }

        private static string? FindFileRecursive(string root, string targetName)
        {
            try
            {
                if (!Directory.Exists(root)) return null;
                var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(f => string.Equals(Path.GetFileName(f), targetName, StringComparison.OrdinalIgnoreCase))
                    .Take(1);
                return files.FirstOrDefault();
            }
            catch { return null; }
        }

        public static SKBitmap GeneratePopulationDensityMap()
        {
            try
            {
                lock (GdalLock)
                {
                    if (!_gdalConfigured)
                    {
                        try { GdalBase.ConfigureAll(); _gdalConfigured = true; }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PopulationDensity] GDAL init failed: {ex.Message}"); return GenerateFallback("GDAL init failure"); }
                    }
                }

                string placesPath = GetDataFile("ne_10m_populated_places.shp");
                string countriesPath = GetDataFile("ne_10m_admin_0_countries.shp");

                if (!File.Exists(placesPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[PopulationDensity] Populated places shapefile missing: {placesPath}");
                    // Attempt structured fallback via urban texture
                    var urban = TryGenerateFromUrbanTexture(countriesPath);
                    if (urban != null) return urban;
                    return GenerateFallback("Missing populated places shapefile");
                }

                float[,] densityGrid = new float[TextureHeight, TextureWidth];
                using DataSource placesDs = Ogr.Open(placesPath, 0);
                if (placesDs == null) return GenerateFallback("Open failed");
                Layer placesLayer = placesDs.GetLayerByIndex(0); if (placesLayer == null) return GenerateFallback("Layer missing");

                var allPopulations = new List<float>();
                placesLayer.ResetReading();
                Feature feat;
                while ((feat = placesLayer.GetNextFeature()) != null)
                {
                    try
                    {
                        var geom = feat.GetGeometryRef(); if (geom == null) continue;
                        int pop = ReadPopulation(feat); if (pop > 0) allPopulations.Add(pop);
                    }
                    catch { }
                }
                if (allPopulations.Count == 0) return GenerateFallback("No pop data");
                float maxPop = allPopulations.Max();
                float minPop = allPopulations.Where(p => p > 0).DefaultIfEmpty(1).Min();
                float logMax = (float)Math.Log10(maxPop);
                float logMin = (float)Math.Log10(minPop);

                placesLayer.ResetReading();
                while ((feat = placesLayer.GetNextFeature()) != null)
                {
                    try
                    {
                        var geom = feat.GetGeometryRef(); if (geom == null) continue;
                        double lon = geom.GetX(0); double lat = geom.GetY(0);
                        int px = (int)((lon + 180.0) / 360.0 * TextureWidth);
                        int py = (int)((90.0 - lat) / 180.0 * TextureHeight);
                        if ((uint)px >= TextureWidth || (uint)py >= TextureHeight) continue;
                        int pop = ReadPopulation(feat); if (pop <= 0) continue;
                        float logPop = (float)Math.Log10(pop);
                        float normalizedPop = (logPop - logMin) / Math.Max(0.0001f, (logMax - logMin));
                        int radius = Math.Max(1, (int)(normalizedPop * 8) + 1);
                        AddDensityInfluence(densityGrid, px, py, normalizedPop, radius);
                    }
                    catch { }
                }

                var bitmap = new SKBitmap(TextureWidth, TextureHeight);
                for (int y = 0; y < TextureHeight; y++)
                {
                    for (int x = 0; x < TextureWidth; x++)
                    {
                        bitmap.SetPixel(x, y, GetDensityColor(Math.Min(1f, densityGrid[y, x])));
                    }
                }

                if (File.Exists(countriesPath)) DrawCountryBorders(bitmap, countriesPath);
                else System.Diagnostics.Debug.WriteLine($"[PopulationDensity] Country borders missing: {countriesPath}");
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopulationDensity] Fatal: {ex.Message}");
                return GenerateFallback("Exception");
            }
        }

        private static int ReadPopulation(Feature feat)
        {
            int idx = feat.GetFieldIndex("POP_MAX");
            if (idx != -1) return feat.GetFieldAsInteger(idx);
            idx = feat.GetFieldIndex("pop_max");
            if (idx != -1) return feat.GetFieldAsInteger(idx);
            return 0;
        }

        private static SKBitmap? TryGenerateFromUrbanTexture(string countriesPath)
        {
            try
            {
                string urbanPng = GetDataFile("urban_texture.png");
                if (!File.Exists(urbanPng)) return null;
                using var fs = File.OpenRead(urbanPng);
                using var skImg = SKImage.FromEncodedData(fs);
                if (skImg == null) return null;
                var bmp = new SKBitmap(TextureWidth, TextureHeight);
                using (var canvas = new SKCanvas(bmp))
                {
                    canvas.Clear(SKColors.Transparent);
                    // Stretch / scale
                    canvas.DrawImage(skImg, new SKRect(0, 0, TextureWidth, TextureHeight));
                }
                // Re-color greys as density gradient (brightness -> density)
                for (int y = 0; y < TextureHeight; y++)
                {
                    for (int x = 0; x < TextureWidth; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        if (c.Alpha == 0) continue;
                        // Approx brightness
                        float br = (0.299f * c.Red + 0.587f * c.Green + 0.114f * c.Blue) / 255f;
                        bmp.SetPixel(x, y, GetDensityColor(br));
                    }
                }
                if (File.Exists(countriesPath)) DrawCountryBorders(bmp, countriesPath);
                System.Diagnostics.Debug.WriteLine("[PopulationDensity] Used urban_texture.png as fallback density source.");
                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopulationDensity] Urban texture fallback failed: {ex.Message}");
                return null;
            }
        }

        private static SKBitmap GenerateFallback(string reason)
        {
            var bmp = new SKBitmap(TextureWidth, TextureHeight);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(20, 20, 20));
            using var paint = new SKPaint { IsAntialias = true };
            for (int y = 0; y < TextureHeight; y += 8)
            {
                float t = (float)y / (TextureHeight - 1);
                byte r = (byte)(139 * (1 - t));
                byte g = (byte)(30 + 180 * t);
                paint.Color = new SKColor(r, g, 0, 150);
                canvas.DrawRect(new SKRect(0, y, TextureWidth, y + 8), paint);
            }
            using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 36, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial") };
            canvas.DrawText("Population fallback", 40, 80, textPaint);
            canvas.DrawText(reason, 40, 130, textPaint);
            return bmp;
        }

        private static void AddDensityInfluence(float[,] grid, int centerX, int centerY, float intensity, int radius)
        {
            int h = grid.GetLength(0); int w = grid.GetLength(1);
            for (int dy = -radius; dy <= radius; dy++)
            {
                int y = centerY + dy; if ((uint)y >= h) continue;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx; if ((uint)x >= w) continue;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;
                    float falloff = (float)Math.Exp(-(dist * dist) / (2.0 * radius * radius / 4.0));
                    grid[y, x] += intensity * falloff;
                }
            }
        }

        private static SKColor GetDensityColor(float density)
        {
            if (density <= 0f) return new SKColor(10, 10, 10, 255);
            float r, g, b;
            if (density < 0.5f) { float t = density * 2f; r = 1f; g = t; b = 0f; }
            else { float t = (density - 0.5f) * 2f; r = 1f - t; g = 1f; b = 0f; }
            return new SKColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), 220);
        }

        private static void DrawCountryBorders(SKBitmap bitmap, string countriesPath)
        {
            try
            {
                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
                using DataSource ds = Ogr.Open(countriesPath, 0); if (ds == null) return; Layer layer = ds.GetLayerByIndex(0); if (layer == null) return;
                layer.ResetReading(); Feature feat; while ((feat = layer.GetNextFeature()) != null)
                { var geom = feat.GetGeometryRef(); if (geom == null) continue; DrawGeometry(canvas, paint, geom); }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PopulationDensity] Border draw failed: {ex.Message}"); }
        }

        private static void DrawGeometry(SKCanvas canvas, SKPaint paint, Geometry geom)
        {
            switch (geom.GetGeometryType())
            {
                case wkbGeometryType.wkbPolygon:
                case wkbGeometryType.wkbPolygon25D: DrawPolygon(canvas, paint, geom); break;
                case wkbGeometryType.wkbMultiPolygon:
                case wkbGeometryType.wkbMultiPolygon25D:
                    for (int i = 0; i < geom.GetGeometryCount(); i++)
                    {
                        var sub = geom.GetGeometryRef(i); if (sub != null) DrawPolygon(canvas, paint, sub);
                    }
                    break;
            }
        }
        private static void DrawPolygon(SKCanvas canvas, SKPaint paint, Geometry geom)
        { var ring = geom.GetGeometryRef(0); if (ring != null) DrawLineString(canvas, paint, ring); }
        private static void DrawLineString(SKCanvas canvas, SKPaint paint, Geometry ring)
        {
            int count = ring.GetPointCount(); if (count < 2) return; using var path = new SKPath(); bool first = true;
            for (int i = 0; i < count; i++)
            { double lon = ring.GetX(i); double lat = ring.GetY(i); float px = (float)((lon + 180.0) / 360.0 * TextureWidth); float py = (float)((90.0 - lat) / 180.0 * TextureHeight); if (first) { path.MoveTo(px, py); first = false; } else path.LineTo(px, py); }
            canvas.DrawPath(path, paint);
        }
    }
}