using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SystemDrawing = System.Drawing;



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
        /// Generates a nearest-neighbor scaled Bitmap for pixel-art display.
        /// </summary>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <returns>A scaled Bitmap that should be disposed by the caller.</returns>
        public static Bitmap GeneratePixelArtMap(int width, int height)
        {
            EnsureElevationData();
            if (!File.Exists(TifPath))
                throw new FileNotFoundException("Missing GeoTIFF file", TifPath);

            using (var img = new Bitmap(TifPath))
            using (var scaled = new Bitmap(width, height))
            {
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(img, 0, 0, width, height);
                }

                var dest = new Bitmap(width, height);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color src = scaled.GetPixel(x, y);
                        float b = src.GetBrightness();
                        dest.SetPixel(x, y, GetAltitudeColor(b));
                    }
                }

               return dest;
           }
       }

        /// <summary>
        /// Generates a terrain map and overlays country borders.
        /// </summary>
        public static Bitmap GeneratePixelArtMapWithCountries(int width, int height, int pixelsPerCell = 8)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }
            int widthPx = width * pixelsPerCell;
            int heightPx = height * pixelsPerCell;
            if (widthPx > MaxBitmapDimension || heightPx > MaxBitmapDimension)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerCell),
                    $"Bitmap size {widthPx}x{heightPx} exceeds supported dimensions ({MaxBitmapDimension}). Use GeneratePixelArtMapWithCountriesLarge instead.");

            int fullW = width * pixelsPerCell;
            int fullH = height * pixelsPerCell;
            int[,] mask = CountryMaskGenerator.CreateCountryMask(
                TerrainTifPath, ShpPath, fullW, fullH);

            Bitmap baseMap = GenerateTerrainPixelArtMap(width, height, pixelsPerCell, mask);

            DrawBorders(baseMap, mask);

            return baseMap;
        }

        /// <summary>

        /// Generate a pixel-art map with country borders for very large maps
        /// using ImageSharp to avoid System.Drawing size limits.
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GeneratePixelArtMapWithCountriesLarge(int width, int height, int pixelsPerCell = 8)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            int fullW = width * pixelsPerCell;
            int fullH = height * pixelsPerCell;
            int[,] mask = CountryMaskGenerator.CreateCountryMask(
                TerrainTifPath, ShpPath, fullW, fullH);

            var baseMap = GenerateTerrainPixelArtMapLarge(width, height, pixelsPerCell, mask);

            DrawBordersLarge(baseMap, mask);

            return baseMap;
        }

        /// <summary>
        /// Generate a single tile of the pixel-art map directly from the terrain
        /// raster. Only the required region is read using GDAL.
        /// </summary>
        private static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTerrainTileLarge(
    int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512, int[,] landMask = null)
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

            Parallel.For(0, cellsY, y =>
            {
                for (int py = 0; py < cellSize; py++)
                {
                    int destY = y * cellSize + py;
                    if (destY >= tileHeight) continue;
                    Span<SixLabors.ImageSharp.PixelFormats.Rgba32> row = dest.DangerousGetPixelRowMemory(destY).Span;
                    for (int x = 0; x < cellsX; x++)
                    {
                        int idx = y * cellsX + x;
                        var baseColor = SystemDrawing.Color.FromArgb(r[idx], g[idx], b[idx]);
                        SystemDrawing.Color[] palette = BuildPalette(baseColor);
                        for (int px = 0; px < cellSize; px++)
                        {
                            int destX = x * cellSize + px;
                            if (destX >= tileWidth) break;
                            var chosen = palette[rngLocal.Value.Next(palette.Length)];
                            if (landMask[destY, destX] == 0)
                                chosen = SystemDrawing.Color.LightSkyBlue;
                            row[destX] = new SixLabors.ImageSharp.PixelFormats.Rgba32(chosen.R, chosen.G, chosen.B, chosen.A);
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
        /// Draw one-pixel-wide borders where adjacent mask values differ.
        /// Using direct memory access avoids the overhead of SetPixel.
        /// </summary>
        private static unsafe void DrawBorders(Bitmap bmp, int[,] mask)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            int stride = data.Stride;
            byte* basePtr = (byte*)data.Scan0;
            int width = bmp.Width;
            int height = bmp.Height;

            for (int y = 1; y < height - 1; y++)
            {

                byte* row = basePtr + y * stride;
                for (int x = 1; x < width - 1; x++)
                {
                    int code = mask[y, x];
                    if (code != mask[y - 1, x] || code != mask[y + 1, x] ||
                        code != mask[y, x - 1] || code != mask[y, x + 1])
                    {
                        byte* pixel = row + x * 4;
                        pixel[0] = 0;
                        pixel[1] = 0;
                        pixel[2] = 0;
                        pixel[3] = 255;
                    }
                }
            }

            bmp.UnlockBits(data);

        }

        /// <summary>
        /// Draw borders directly on an ImageSharp image when the map exceeds
        /// System.Drawing limits.
        /// </summary>
        private static void DrawBordersLarge(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img, int[,] mask)
        {
            int width = img.Width;
            int height = img.Height;
            for (int y = 1; y < height - 1; y++)
            {
                var row = img.DangerousGetPixelRowMemory(y).Span;
                for (int x = 1; x < width - 1; x++)
                {
                    int code = mask[y, x];
                    if (code != mask[y - 1, x] || code != mask[y + 1, x] ||
                        code != mask[y, x - 1] || code != mask[y, x + 1])
                    {
                        row[x] = SixLabors.ImageSharp.Color.Black;
                    }
                }
            }

        }

        /// <summary>
        /// Generates a pixel-art map using the Natural Earth terrain raster.
        /// Each logical cell is represented by multiple pixels which are
        /// randomly chosen from a small palette derived from the terrain color.
        /// </summary>
        /// <param name="cellsX">Number of cells horizontally.</param>
        /// <param name="cellsY">Number of cells vertically.</param>
        /// <param name="pixelsPerCell">Size of each cell in pixels.</param>

        public static unsafe Bitmap GenerateTerrainPixelArtMap(int cellsX, int cellsY, int pixelsPerCell, int[,] landMask = null)
        {
            string path = TerrainTifPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing terrain GeoTIFF", path);

            int widthPx = cellsX * pixelsPerCell;
            int heightPx = cellsY * pixelsPerCell;
            if (widthPx > MaxBitmapDimension || heightPx > MaxBitmapDimension)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerCell),
                    $"Bitmap size {widthPx}x{heightPx} exceeds supported dimensions ({MaxBitmapDimension}).");

            landMask ??= CountryMaskGenerator.CreateCountryMask(
                TerrainTifPath, ShpPath, widthPx, heightPx);

            using (var img = new Bitmap(path))
            using (var scaled = new Bitmap(cellsX, cellsY))
            {
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(img, 0, 0, cellsX, cellsY);
                }

                var dest = new Bitmap(widthPx, heightPx, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bmpData = dest.LockBits(new Rectangle(0, 0, dest.Width, dest.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, dest.PixelFormat);
                int stride = bmpData.Stride;
                byte* basePtr = (byte*)bmpData.Scan0;
                Random rng = new Random();
                System.Drawing.Color waterColor = System.Drawing.Color.LightSkyBlue;

                for (int y = 0; y < cellsY; y++)
                {
                    for (int x = 0; x < cellsX; x++)
                    {
                         Color baseColor = scaled.GetPixel(x, y);
                        Color[] palette = BuildPalette(baseColor);
                        for (int py = 0; py < pixelsPerCell; py++)
                        {
                            byte* row = basePtr + ((y * pixelsPerCell + py) * stride) + (x * pixelsPerCell * 4);
                            for (int px = 0; px < pixelsPerCell; px++)
                            {
                                int destX = x * pixelsPerCell + px;
                                int destY = y * pixelsPerCell + py;
                                System.Drawing.Color chosen = palette[rng.Next(palette.Length)];
                                if (landMask[destY, destX] == 0)
                                    chosen = waterColor;
                                int offset = px * 4;
                                row[offset] = chosen.B;
                                row[offset + 1] = chosen.G;
                                row[offset + 2] = chosen.R;
                                row[offset + 3] = chosen.A;
                            }
                        }
                    }
                }

                dest.UnlockBits(bmpData);
                return dest;
            }
        }

        /// <summary>
        /// Generate a pixel-art terrain map for dimensions larger than System.Drawing supports.
        /// This uses ImageSharp to avoid the 32k bitmap limit.
        /// </summary>

        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTerrainPixelArtMapLarge(int cellsX, int cellsY, int pixelsPerCell, int[,] landMask = null)

        {
            string path = TerrainTifPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing terrain GeoTIFF", path);

            int widthPx = cellsX * pixelsPerCell;
            int heightPx = cellsY * pixelsPerCell;

            using var img = new Bitmap(path);
            using var scaled = new Bitmap(cellsX, cellsY);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, 0, 0, cellsX, cellsY);
            }

            landMask ??= CountryMaskGenerator.CreateCountryMask(
                TerrainTifPath, ShpPath, widthPx, heightPx);

            var dest = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(widthPx, heightPx);

            var data = scaled.LockBits(new Rectangle(0, 0, cellsX, cellsY), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            int seed = Environment.TickCount;
            var rngLocal = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

            unsafe
            {
                byte* basePtr = (byte*)data.Scan0;
                SystemDrawing.Color waterColor = SystemDrawing.Color.LightSkyBlue;

                Parallel.For(0, cellsY, y =>
                {
                    byte* rowPtr = basePtr + y * stride;

                    for (int py = 0; py < pixelsPerCell; py++)
                    {
                        int destY = y * pixelsPerCell + py;
                        Span<Rgba32> destRow = dest.DangerousGetPixelRowMemory(destY).Span;

                        for (int x = 0; x < cellsX; x++)
                        {
                            int offset = x * 4;
                            byte b = rowPtr[offset];
                            byte g = rowPtr[offset + 1];
                            byte r = rowPtr[offset + 2];
                            byte a = rowPtr[offset + 3];

                            Color baseColor = Color.FromArgb(a, r, g, b);
                            Color[] palette = BuildPalette(baseColor);

                            for (int px = 0; px < pixelsPerCell; px++)
                            {
                                int destX = x * pixelsPerCell + px;
                                Color chosen = palette[rngLocal.Value.Next(palette.Length)];
                                if (landMask[destY, destX] == 0)
                                    chosen = waterColor;
                                destRow[destX] = new Rgba32(chosen.R, chosen.G, chosen.B, chosen.A);
                            }
                        }
                    }
                });
            }

            scaled.UnlockBits(data);

            return dest;
        }

        private static Color GetAltitudeColor(float value)
        {
            // Piecewise gradient approximating terrain colors
            if (value < 0.30f)
                return Lerp(Color.DarkBlue, Color.Blue, value / 0.30f);
            if (value < 0.50f)
                return Lerp(Color.Blue, Color.LightBlue, (value - 0.30f) / 0.20f);
            if (value < 0.55f)
                return Lerp(Color.LightBlue, Color.SandyBrown, (value - 0.50f) / 0.05f);
            if (value < 0.70f)
                return Lerp(Color.SandyBrown, Color.Green, (value - 0.55f) / 0.15f);
            if (value < 0.85f)
                return Lerp(Color.Green, Color.Sienna, (value - 0.70f) / 0.15f);
            return Lerp(Color.Sienna, Color.White, (value - 0.85f) / 0.15f);
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            int r = (int)(a.R + (b.R - a.R) * t);
            int g = (int)(a.G + (b.G - a.G) * t);
            int bVal = (int)(a.B + (b.B - a.B) * t);
            return Color.FromArgb(r, g, bVal);
        }

        // Builds a small palette of colors around the provided base color.  A
        // darker and lighter variant are included to add variety when filling
        // each cell with multiple pixels.
        private static bool IsWaterColor(Color baseColor)
        {
            return baseColor.R > 200 && baseColor.G > 200 && baseColor.B > 200 &&
                   Math.Abs(baseColor.R - baseColor.G) < 15 &&
                   Math.Abs(baseColor.R - baseColor.B) < 15;
        }

        private static Color[] BuildPalette(Color baseColor)
        {

            // The terrain raster uses near-white values for water. Replace them
            // with a consistent blue tone and avoid random variation so the
            // ocean does not look noisy.
            if (IsWaterColor(baseColor))
            {
                baseColor = Color.LightSkyBlue;

                return new[] { baseColor };
            }

            return new[]
            {
                Lerp(baseColor, Color.Black, 0.2f),
                baseColor,
                Lerp(baseColor, Color.White, 0.2f)
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
