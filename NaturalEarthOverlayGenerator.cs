using System;
using System.IO;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;

namespace StrategyGame
{
    /// <summary>
    /// Provides overlay rendering for Natural Earth state borders and city icons.
    /// This class does not modify the base tile generation but draws additional
    /// layers on top of generated terrain tiles.
    /// </summary>
    public static class NaturalEarthOverlayGenerator
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalConfigured = false;

        // Paths follow the same resolution logic as PixelMapGenerator
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
            if (Directory.Exists(DataDir))
            {
                var matches = Directory.GetFiles(DataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }

            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;
            if (Directory.Exists(RepoDataDir))
            {
                var matches = Directory.GetFiles(RepoDataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }

            return userPath;
        }

        private static readonly string TerrainTifPath = GetDataFile("NE1_HR_LC.tif");
        private static readonly string Admin1Path = GetDataFile("ne_10m_admin_1_states_provinces.shp");
        private static readonly string CitiesPath = GetDataFile("ne_10m_populated_places.shp");

        /// <summary>
        /// Apply all overlays (state borders and cities) on the provided tile image.
        /// </summary>
        public static void ApplyOverlays(Image<Rgba32> img, int mapWidth, int mapHeight,
                                         int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            lock (GdalLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    Gdal.AllRegister();
                    Ogr.RegisterAll();
                    _gdalConfigured = true;
                }
            }

            DrawStateBorders(img, mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx);
            DrawCities(img, mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx);
        }

        private static void DrawStateBorders(Image<Rgba32> img, int mapWidth, int mapHeight,
                                             int cellSize, int tileX, int tileY, int tileSizePx)
        {
            if (!File.Exists(Admin1Path))
                return;

            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;
            int offsetX = tileX * tileSizePx;
            int offsetY = tileY * tileSizePx;
            int widthPx = Math.Min(tileSizePx, mapWidthPx - offsetX);
            int heightPx = Math.Min(tileSizePx, mapHeightPx - offsetY);

            int[,] mask = CreateMaskTile(Admin1Path, "adm1_code", offsetX, offsetY, widthPx, heightPx, mapWidthPx, mapHeightPx);
            Rgba32 borderColor = new Rgba32(255, 255, 255, 180);
            for (int y = 1; y < heightPx - 1; y++)
            {
                var row = img.DangerousGetPixelRowMemory(y).Span;
                for (int x = 1; x < widthPx - 1; x++)
                {
                    int v = mask[y, x];
                    if (v == 0) continue;
                    if (mask[y - 1, x] != v || mask[y + 1, x] != v || mask[y, x - 1] != v || mask[y, x + 1] != v)
                    {
                        row[x] = borderColor;
                    }
                }
            }
        }

        private static void DrawCities(Image<Rgba32> img, int mapWidth, int mapHeight,
                                       int cellSize, int tileX, int tileY, int tileSizePx)
        {
            if (!File.Exists(CitiesPath))
                return;

            using var dem = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
            double[] gt = new double[6];
            dem.GetGeoTransform(gt);
            int srcCols = dem.RasterXSize;
            int srcRows = dem.RasterYSize;

            double scaleX = (double)srcCols / (mapWidth * cellSize);
            double scaleY = (double)srcRows / (mapHeight * cellSize);

            double originX = gt[0] + tileX * tileSizePx * scaleX * gt[1];
            double originY = gt[3] + tileY * tileSizePx * scaleY * gt[5];
            double pixelW = gt[1] * scaleX;
            double pixelH = gt[5] * scaleY;

            using DataSource ds = Ogr.Open(CitiesPath, 0);
            Layer layer = ds.GetLayerByIndex(0);
            SpatialReference layerSrs = layer.GetSpatialRef();
            SpatialReference demSrs = new SpatialReference(dem.GetProjection());
            using CoordinateTransformation transform = new CoordinateTransformation(layerSrs, demSrs);

            Feature feat;
            layer.ResetReading();
            while ((feat = layer.GetNextFeature()) != null)
            {
                Geometry geom = feat.GetGeometryRef();
                if (geom == null) { feat.Dispose(); continue; }
                if (geom.GetGeometryType() != wkbGeometryType.wkbPoint && geom.GetGeometryType() != wkbGeometryType.wkbPoint25D)
                {
                    feat.Dispose();
                    continue;
                }

                double[] pt = new double[3];
                transform.TransformPoint(pt, geom.GetX(0), geom.GetY(0), 0);
                double lon = pt[0];
                double lat = pt[1];

                int px = (int)Math.Round((lon - originX) / pixelW);
                int py = (int)Math.Round((lat - originY) / pixelH);
                if (px < 0 || py < 0 || px >= img.Width || py >= img.Height)
                {
                    feat.Dispose();
                    continue;
                }

                int pop = feat.GetFieldAsInteger("POP_MAX");
                int size = pop > 500000 ? 4 : pop > 100000 ? 3 : 2;
                Rgba32 color = pop > 1000000 ? new Rgba32(255, 215, 0) : new Rgba32(200, 50, 50);
                DrawSquare(img, px - size / 2, py - size / 2, size, color);
                feat.Dispose();
            }
        }

        private static int[,] CreateMaskTile(string shpPath, string attr, int offsetX, int offsetY, int width, int height, int fullWidth, int fullHeight)
        {
            using var dem = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
            double[] gt = new double[6];
            dem.GetGeoTransform(gt);
            int srcCols = dem.RasterXSize;
            int srcRows = dem.RasterYSize;

            double scaleX = (double)srcCols / fullWidth;
            double scaleY = (double)srcRows / fullHeight;

            double[] newGt = new double[6];
            newGt[0] = gt[0] + offsetX * scaleX * gt[1];
            newGt[1] = gt[1] * scaleX;
            newGt[2] = 0;
            newGt[3] = gt[3] + offsetY * scaleY * gt[5];
            newGt[4] = 0;
            newGt[5] = gt[5] * scaleY;

            Driver memDrv = Gdal.GetDriverByName("MEM");
            using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
            maskDs.SetGeoTransform(newGt);
            maskDs.SetProjection(dem.GetProjection());

            using DataSource ds = Ogr.Open(shpPath, 0);
            Layer layer = ds.GetLayerByIndex(0);

            Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                0, null, new[] { $"ATTRIBUTE={attr}" }, null, "");

            Band band = maskDs.GetRasterBand(1);
            int[] flat = new int[width * height];
            band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

            int[,] result = new int[height, width];
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    result[r, c] = flat[r * width + c];

            return result;
        }

        private static void DrawSquare(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
        {
            for (int yy = 0; yy < size; yy++)
            {
                int py = y + yy;
                if (py < 0 || py >= img.Height) continue;
                var row = img.DangerousGetPixelRowMemory(py).Span;
                for (int xx = 0; xx < size; xx++)
                {
                    int px = x + xx;
                    if (px < 0 || px >= img.Width) continue;
                    row[px] = color;
                }
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
                        dict[trimmed] = userPath;
                    else
                        dict[trimmed] = Path.Combine(RepoDataDir, trimmed);
                }
            }
            return dict;
        }
    }
}

