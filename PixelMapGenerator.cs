using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;



namespace StrategyGame
{
    /// <summary>
    /// Provides helper methods for generating a pixel-art map based on the
    /// ETOPO1 elevation data. The GeoTIFF is downloaded using the existing
    /// Python script when not already present.
    /// </summary>
    public static class PixelMapGenerator
    {
        private static readonly object GdalConfigLock = new object();
        private static bool _gdalConfigured = false;

        // Resolve paths relative to the repository root so the application does
        // not depend on developer specific locations. The executable lives in
        // bin/Debug or bin/Release so we need to traverse three directories up
        // to reach the repo root and then into the data folder.
        private static readonly string RepoRoot =
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));


        // Data files are expected to live in the user's Documents\data folder
        // (e.g. "C:\\Users\\kayla\\Documents\\data").  This path is used directly
        // rather than falling back to the repository so the game always loads
        // external resources from that location.
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");


        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");


        // Data files listed in the text file are resolved relative to the data
        // directory.  This allows the application to load resources that are
        // not part of the repository but exist locally.
        private static readonly string DataFileList =
            Path.Combine(RepoRoot, "DataFileNames");

        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        // System.Drawing fails with "Parameter is not valid" when width or height
        // exceed approximately 32k pixels.  Clamp generated bitmap dimensions to
        // stay below this threshold.
        /// <summary>
        /// Maximum bitmap dimension that can be safely created using
        /// System.Drawing. Larger images must be generated using
        /// ImageSharp to avoid GDI limitations.
        /// </summary>
        public const int MaxBitmapDimension = 30000;

        private static string GetDataFile(string name)
        {

            // first check explicit mappings from DataFileNames
            if (DataFiles.TryGetValue(name, out var mapped) && File.Exists(mapped))
                return mapped;

            // search the user Documents data directory recursively
            string userPath = Path.Combine(DataDir, name);
            if (File.Exists(userPath))
                return userPath;

            if (Directory.Exists(DataDir))
            {
                var matches = Directory.GetFiles(DataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }


            // fall back to the repository data directory if nothing found
            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;

            if (Directory.Exists(RepoDataDir))
            {
                var matches = Directory.GetFiles(RepoDataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }


            // return the path in the Documents folder even if missing so callers know where it was expected
            return userPath;
        }

        private static readonly string TifPath =
            GetDataFile("ETOPO1_Bed_g_geotiff.tif");
        private static readonly string ShpPath =
            GetDataFile("ne_10m_admin_0_countries.shp");
        private static readonly string Admin1Path =
            GetDataFile("ne_10m_admin_1_states_provinces.shp");
        private static readonly string CitiesPath =
            GetDataFile("ne_10m_populated_places.shp");

        // Terrain map used for pixel-art generation.
        private static readonly string TerrainTifPath =
            GetDataFile("NE1_HR_LC.tif");


        /// <summary>
        /// Ensures the GeoTIFF file exists by invoking fetch_etopo1.py if needed.
        /// </summary>
        public static void EnsureElevationData()
        {
            if (File.Exists(TifPath))
                return;

          
        }




        /// <summary>
        /// Generate a single tile of the pixel-art map directly from the terrain
        /// raster. Only the required region is read using GDAL.
        /// </summary>
        private static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>
     GenerateTerrainTileLarge(int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512, int[,] landMask = null)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            using var ds = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
            if (ds == null)
                throw new FileNotFoundException("Missing terrain GeoTIFF", TerrainTifPath);

            int srcW = ds.RasterXSize;
            int srcH = ds.RasterYSize;

            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;

            int pixelX = tileX * tileSizePx;
            int pixelY = tileY * tileSizePx;
            int tileWidth = Math.Min(tileSizePx, mapWidthPx - pixelX);
            int tileHeight = Math.Min(tileSizePx, mapHeightPx - pixelY);
            if (tileWidth <= 0 || tileHeight <= 0)
                return new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);

            landMask ??= CreateCountryMaskTile(mapWidthPx, mapHeightPx, pixelX, pixelY, tileWidth, tileHeight);

            int startCellX = pixelX / cellSize;
            int startCellY = pixelY / cellSize;
            int cellsX = (tileWidth + cellSize - 1) / cellSize;
            int cellsY = (tileHeight + cellSize - 1) / cellSize;

            double scaleX = (double)srcW / mapWidth;
            double scaleY = (double)srcH / mapHeight;

            int srcX = (int)Math.Floor(startCellX * scaleX);
            int srcY = (int)Math.Floor(startCellY * scaleY);
            int readW = (int)Math.Ceiling(cellsX * scaleX);
            int readH = (int)Math.Ceiling(cellsY * scaleY);

            byte[] r = new byte[cellsX * cellsY];
            byte[] g = new byte[cellsX * cellsY];
            byte[] b = new byte[cellsX * cellsY];

            ds.GetRasterBand(1).ReadRaster(srcX, srcY, readW, readH, r, cellsX, cellsY, 0, 0);
            ds.GetRasterBand(2).ReadRaster(srcX, srcY, readW, readH, g, cellsX, cellsY, 0, 0);
            ds.GetRasterBand(3).ReadRaster(srcX, srcY, readW, readH, b, cellsX, cellsY, 0, 0);

            var dest = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(tileWidth, tileHeight);
            int seed = Environment.TickCount;
            var rngLocal = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));
            var waterColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(135, 206, 250, 255); // LightSkyBlue

            Parallel.For(0, cellsY, y =>
            {
                for (int x = 0; x < cellsX; x++)
                {
                    int idx = y * cellsX + x;
                    var baseColor = new Rgba32(r[idx], g[idx], b[idx], 255);
                    var palette = BuildPalette(baseColor);

                    for (int py = 0; py < cellSize; py++)
                    {
                        int destY = y * cellSize + py;
                        if (destY >= tileHeight) continue;

                        var row = dest.DangerousGetPixelRowMemory(destY).Span;

                        for (int px = 0; px < cellSize; px++)
                        {
                            int destX = x * cellSize + px;
                            if (destX >= tileWidth) continue;

                            var isLand = landMask[destY, destX] != 0;
                            if (!isLand)
                            {
                                row[destX] = waterColor;
                            }
                            else
                            {
                                var c = palette[rngLocal.Value.Next(palette.Length)];
                                row[destX] = new SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, c.A);
                            }
                        }
                    }
                }
            });

            return dest;
        }

        /// <summary>
        /// Generate a terrain tile and overlay country borders.
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTileWithCountriesLarge(
     int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            int fullW = mapWidth * cellSize;
            int fullH = mapHeight * cellSize;
            int offsetX = tileX * tileSizePx;
            int offsetY = tileY * tileSizePx;

            int[,] mask = CreateCountryMaskTile(fullW, fullH, offsetX, offsetY, Math.Min(tileSizePx, fullW - offsetX), Math.Min(tileSizePx, fullH - offsetY));

            var img = GenerateTerrainTileLarge(mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx, mask);

            DrawBordersLarge(img, mask);

            return img;
        }

        /// <summary>
        /// Generate a terrain tile and overlay country, state borders and city points.
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTileWithWorldDataLarge(
            int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            var fullW = mapWidth * cellSize;
            var fullH = mapHeight * cellSize;
            int offsetX = tileX * tileSizePx;
            int offsetY = tileY * tileSizePx;

            int[,] mask = CreateCountryMaskTile(fullW, fullH, offsetX, offsetY,
                Math.Min(tileSizePx, fullW - offsetX), Math.Min(tileSizePx, fullH - offsetY));

            var img = GenerateTerrainTileLarge(mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx, mask);

            DrawBordersLarge(img, mask);
            DrawStateBordersLarge(img, mask);
            DrawCitiesLarge(img, widthPx: mask.GetLength(1), heightPx: mask.GetLength(0));

            return img;
        }

        /// <summary>
        /// Determine if a tile contains any land pixels.
        /// </summary>
        public static bool TileContainsLand(int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;

            int pixelX = tileX * tileSizePx;
            int pixelY = tileY * tileSizePx;
            int tileWidth = Math.Min(tileSizePx, mapWidthPx - pixelX);
            int tileHeight = Math.Min(tileSizePx, mapHeightPx - pixelY);
            if (tileWidth <= 0 || tileHeight <= 0)
                return false;

            // Use the rasterized country mask instead of raw terrain colors. The
            // mask precisely marks land areas and avoids false negatives when the
            // terrain imagery uses near-white colors for beaches or deserts.
            int[,] mask = CreateCountryMaskTile(mapWidthPx, mapHeightPx, pixelX, pixelY, tileWidth, tileHeight);

            foreach (var value in mask)
            {
                if (value != 0)
                    return true;
            }

            return false;
        }

        private static int[,] CreateCountryMaskTile(int fullWidth, int fullHeight,
            int offsetX, int offsetY, int width, int height)
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

            OSGeo.GDAL.Driver memDrv = Gdal.GetDriverByName("MEM");
            using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
            maskDs.SetGeoTransform(newGt);
            maskDs.SetProjection(dem.GetProjection());

            using DataSource ds = Ogr.Open(ShpPath, 0);
            Layer layer = ds.GetLayerByIndex(0);

            Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                0, null, new[] { "ATTRIBUTE=ISO_N3" }, null, "");

            Band band = maskDs.GetRasterBand(1);
            int[] flat = new int[width * height];
            band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

            int[,] result = new int[height, width];
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    result[r, c] = flat[r * width + c];
                }
            }

            return result;
        }

 

        /// <summary>
        /// Draw borders directly on an ImageSharp image when the map exceeds
        /// System.Drawing limits.
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>
     DrawBordersLarge(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> baseImage, int[,] landMask)
        {
            if (!System.IO.File.Exists(ShpPath))
                throw new System.IO.FileNotFoundException("Missing shapefile", ShpPath);

            int heightPx = landMask.GetLength(0);
            int widthPx = landMask.GetLength(1);

            var result = baseImage.Clone();

            SixLabors.ImageSharp.PixelFormats.Rgba32 borderColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 255); // black

            var reader = new NetTopologySuite.IO.ShapefileDataReader(
                ShpPath, NetTopologySuite.Geometries.GeometryFactory.Default);

            while (reader.Read())
            {
                var geometry = reader.Geometry;

                if (geometry is NetTopologySuite.Geometries.MultiPolygon multi)
                {
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        var poly = (NetTopologySuite.Geometries.Polygon)multi.GetGeometryN(i);
                        DrawPolygonOutline(result, poly, widthPx, heightPx, borderColor);
                    }
                }
                else if (geometry is NetTopologySuite.Geometries.Polygon poly)
                {
                    DrawPolygonOutline(result, poly, widthPx, heightPx, borderColor);
                }
            }

            return result;
        }

        private static void DrawStateBordersLarge(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image, int[,] landMask)
        {
            if (!System.IO.File.Exists(Admin1Path))
                return;

            int heightPx = landMask.GetLength(0);
            int widthPx = landMask.GetLength(1);

            var borderColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(80, 80, 80, 255);
            var reader = new NetTopologySuite.IO.ShapefileDataReader(Admin1Path, NetTopologySuite.Geometries.GeometryFactory.Default);

            while (reader.Read())
            {
                var geometry = reader.Geometry;
                if (geometry is NetTopologySuite.Geometries.MultiPolygon multi)
                {
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        var poly = (NetTopologySuite.Geometries.Polygon)multi.GetGeometryN(i);
                        DrawPolygonOutline(image, poly, widthPx, heightPx, borderColor);
                    }
                }
                else if (geometry is NetTopologySuite.Geometries.Polygon poly)
                {
                    DrawPolygonOutline(image, poly, widthPx, heightPx, borderColor);
                }
            }
        }

        private static void DrawCitiesLarge(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image, int widthPx, int heightPx)
        {
            if (!System.IO.File.Exists(CitiesPath))
                return;

            var color = new SixLabors.ImageSharp.PixelFormats.Rgba32(200, 20, 20, 255);
            var reader = new NetTopologySuite.IO.ShapefileDataReader(CitiesPath, NetTopologySuite.Geometries.GeometryFactory.Default);

            int scalerankIndex = -1;
            for (int i = 0; i < reader.DbaseHeader.NumFields; i++)
            {
                if (string.Equals(reader.DbaseHeader.Fields[i].Name, "SCALERANK", StringComparison.OrdinalIgnoreCase))
                {
                    scalerankIndex = i;
                    break;
                }
            }

            while (reader.Read())
            {
                if (scalerankIndex >= 0 && reader.GetInt32(scalerankIndex) > 4)
                    continue;

                if (reader.Geometry is NetTopologySuite.Geometries.Point pt)
                {
                    int x = (int)(pt.X * widthPx);
                    int y = (int)((1.0 - pt.Y) * heightPx);
                    FillRect(image, x - 1, y - 1, 3, 3, color);
                }
            }
        }
        private static void DrawPolygonOutline(
    SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image,
    NetTopologySuite.Geometries.Polygon polygon,
    int widthPx,
    int heightPx,
    SixLabors.ImageSharp.PixelFormats.Rgba32 color)
        {
            var exterior = polygon.ExteriorRing.Coordinates;
            for (int i = 0; i < exterior.Length - 1; i++)
            {
                var c0 = exterior[i];
                var c1 = exterior[i + 1];

                int x0 = (int)(c0.X * widthPx);
                int y0 = (int)((1.0 - c0.Y) * heightPx);
                int x1 = (int)(c1.X * widthPx);
                int y1 = (int)((1.0 - c1.Y) * heightPx);

                DrawLine(image, x0, y0, x1, y1, color);
            }
        }
        private static void DrawLine(
    SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image,
    int x0, int y0, int x1, int y1,
    SixLabors.ImageSharp.PixelFormats.Rgba32 color)
        {
            int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;

            while (true)
            {
                if (x0 >= 0 && x0 < image.Width && y0 >= 0 && y0 < image.Height)
                {
                    image[x0, y0] = color;
                }

                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>
        /// Generate a pixel-art terrain map for dimensions larger than System.Drawing supports.
        /// This uses ImageSharp to avoid the 32k bitmap limit.
        /// </summary>

        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>
    GenerateTerrainPixelArtMapLarge(int cellsX, int cellsY, int pixelsPerCell, int[,] landMask = null)
        {
            string path = TerrainTifPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing terrain GeoTIFF", path);

            int widthPx = cellsX * pixelsPerCell;
            int heightPx = cellsY * pixelsPerCell;

            // Load and resize terrain image using ImageSharp only
            using SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> terrainImage =
                SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path);

            terrainImage.Mutate(ctx => ctx.Resize(cellsX, cellsY, SixLabors.ImageSharp.Processing.KnownResamplers.NearestNeighbor));

            landMask ??= CountryMaskGenerator.CreateCountryMask(TerrainTifPath, ShpPath, widthPx, heightPx);

            var dest = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(widthPx, heightPx);
            var waterColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(135, 206, 250, 255); // LightSkyBlue

            int seed = Environment.TickCount;
            var rngLocal = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

            Parallel.For(0, cellsY, y =>
            {
                for (int x = 0; x < cellsX; x++)
                {
                    SixLabors.ImageSharp.PixelFormats.Rgba32 baseColor = terrainImage[x, y];

                    bool isLandCell = false;
                    for (int py = 0; py < pixelsPerCell && !isLandCell; py++)
                    {
                        int destY = y * pixelsPerCell + py;
                        for (int px = 0; px < pixelsPerCell && !isLandCell; px++)
                        {
                            int destX = x * pixelsPerCell + px;
                            if (landMask[destY, destX] != 0)
                                isLandCell = true;
                        }
                    }

                    var palette = isLandCell ? BuildPalette(baseColor) : null;

                    for (int py = 0; py < pixelsPerCell; py++)
                    {
                        int destY = y * pixelsPerCell + py;
                        if (destY >= heightPx) continue;

                        Span<SixLabors.ImageSharp.PixelFormats.Rgba32> row = dest.DangerousGetPixelRowMemory(destY).Span;

                        for (int px = 0; px < pixelsPerCell; px++)
                        {
                            int destX = x * pixelsPerCell + px;
                            if (destX >= widthPx) continue;

                            row[destX] = (landMask[destY, destX] == 0)
                                ? waterColor
                                : palette[rngLocal.Value.Next(palette.Length)];
                        }
                    }
                }
            });

            return dest;
        }

       

       

        // Builds a small palette of colors around the provided base color.  A
        // darker and lighter variant are included to add variety when filling
        // each cell with multiple pixels.


        private static Rgba32[] BuildPalette(Rgba32 baseColor)
        {
            Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
            {
                t = Math.Clamp(t, 0f, 1f);
                byte r = (byte)(a.R + (b.R - a.R) * t);
                byte g = (byte)(a.G + (b.G - a.G) * t);
                byte bVal = (byte)(a.B + (b.B - a.B) * t);
                byte aVal = (byte)(a.A + (b.A - a.A) * t);
                return new Rgba32(r, g, bVal, aVal);
            }

            return new[]
            {
        Lerp(baseColor, new Rgba32(0, 0, 0, baseColor.A), 0.2f),
        baseColor,
        Lerp(baseColor, new Rgba32(255, 255, 255, baseColor.A), 0.2f)
    };
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
