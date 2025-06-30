using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;

namespace StrategyGame
{
    /// <summary>
    /// Generates a global urban texture layer by rasterizing Natural Earth
    /// urban area polygons and city points. The result is stored as a PNG
    /// for fast overlay when rendering map tiles.
    /// </summary>
    public static class UrbanAreaRenderer
    {
        private const int TextureWidth = 14400;
        private const int TextureHeight = 7200;

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

            return userPath;
        }

        public static void GenerateUrbanTextureLayer()
        {
            lock (GdalLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            string urbanPath = GetDataFile("ne_10m_urban_areas.shp");
            string placesPath = GetDataFile("ne_10m_populated_places.shp");
            if (!File.Exists(urbanPath) || !File.Exists(placesPath))
                throw new FileNotFoundException("Missing urban shapefiles");

            var driver = Gdal.GetDriverByName("MEM");
            using var ds = driver.Create("", TextureWidth, TextureHeight, 1, DataType.GDT_Byte, null);
            double pixelSizeX = 360.0 / TextureWidth;
            double pixelSizeY = 180.0 / TextureHeight;
            double[] gt = { -180.0, pixelSizeX, 0, 90.0, 0, -pixelSizeY };
            ds.SetGeoTransform(gt);
            using var srs = new SpatialReference(string.Empty);
            srs.ImportFromEPSG(4326);
            ds.SetProjection(srs.ExportToWkt());

            using DataSource urbanDs = Ogr.Open(urbanPath, 0);
            Layer urbanLayer = urbanDs.GetLayerByIndex(0);
            // Burn value 255 where urban polygons exist
            Gdal.RasterizeLayer(ds, 1, new[] { 1 }, urbanLayer, IntPtr.Zero, IntPtr.Zero,
                1, new[] { 255.0 }, new[] { "ALL_TOUCHED=TRUE" }, null, "");

            Band maskBand = ds.GetRasterBand(1);
            byte[] mask = new byte[TextureWidth * TextureHeight];
            maskBand.ReadRaster(0, 0, TextureWidth, TextureHeight, mask, TextureWidth, TextureHeight, 0, 0);

            using DataSource placesDs = Ogr.Open(placesPath, 0);
            Layer placesLayer = placesDs.GetLayerByIndex(0);
            var grey = new Rgba32(130, 130, 130, 180);

            using var img = new Image<Rgba32>(TextureWidth, TextureHeight);
            img.Mutate(ctx => ctx.Clear(Color.Transparent));

            // Transfer existing mask to image
            for (int y = 0; y < TextureHeight; y++)
            {
                Span<Rgba32> row = img.DangerousGetPixelRowMemory(y).Span;
                int offset = y * TextureWidth;
                for (int x = 0; x < TextureWidth; x++)
                {
                    if (mask[offset + x] != 0)
                        row[x] = grey;
                }
            }

            // Draw circles for populated places not already covered
            placesLayer.ResetReading();
            Feature feat;
            while ((feat = placesLayer.GetNextFeature()) != null)
            {
                var geom = feat.GetGeometryRef();
                double lon = geom.GetX(0);
                double lat = geom.GetY(0);
                int px = (int)((lon + 180.0) / 360.0 * TextureWidth);
                int py = (int)((90.0 - lat) / 180.0 * TextureHeight);
                if (px < 0 || px >= TextureWidth || py < 0 || py >= TextureHeight)
                    continue;

                int idx = py * TextureWidth + px;
                if (mask[idx] != 0)
                    continue; // already urban

                int pop = 0;
                if (feat.GetFieldIndex("POP_MAX") != -1)
                    pop = feat.GetFieldAsInteger("POP_MAX");
                else if (feat.GetFieldIndex("pop_max") != -1)
                    pop = feat.GetFieldAsInteger("pop_max");

                int radius = pop switch
                {
                    > 10000000 => 6,
                    > 5000000 => 5,
                    > 1000000 => 4,
                    > 500000 => 3,
                    > 100000 => 2,
                    _ => 1
                };
                FillCircle(img, px - radius, py - radius, radius * 2, grey);
            }

            string outPath = Path.Combine(DataDir, "urban_texture.png");
            Directory.CreateDirectory(DataDir);
            img.Save(outPath);
        }

        private static void FillCircle(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
        {
            int radius = size / 2;
            int cx = x + radius;
            int cy = y + radius;
            for (int iy = -radius; iy <= radius; iy++)
            {
                int yy = cy + iy;
                if (yy < 0 || yy >= img.Height) continue;
                int dx = (int)Math.Sqrt(radius * radius - iy * iy);
                int start = cx - dx;
                int end = cx + dx;
                if (start < 0) start = 0;
                if (end >= img.Width) end = img.Width - 1;
                Span<Rgba32> row = img.DangerousGetPixelRowMemory(yy).Span;
                for (int ix = start; ix <= end; ix++)
                    row[ix] = color;
            }
        }

        private static Dictionary<string, string> LoadDataFiles()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(DataFileList))
            {
                foreach (var line in File.ReadAllLines(DataFileList))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("files"))
                        continue;

                    string userPath = Path.Combine(DataDir, trimmed);
                    if (File.Exists(userPath))
                    {
                        dict[trimmed] = userPath;
                    }
                    else
                    {
                        dict[trimmed] = Path.Combine(RepoDataDir, trimmed);
                    }
                }
            }
            return dict;
        }
    }
}
