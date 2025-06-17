using MaxRev.Gdal.Core;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;

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
        private const int MaxBitmapDimension = 30000;

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
            Bitmap baseMap = GenerateTerrainPixelArtMap(width, height, pixelsPerCell);

            // mask dimensions == pixel dimensions
            int fullW = baseMap.Width;
            int fullH = baseMap.Height;
            int[,] mask = CountryMaskGenerator.CreateCountryMask(
                TerrainTifPath, ShpPath, fullW, fullH);

            DrawBorders(baseMap, mask);

            return baseMap;
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
        /// Generates a pixel-art map using the Natural Earth terrain raster.
        /// Each logical cell is represented by multiple pixels which are
        /// randomly chosen from a small palette derived from the terrain color.
        /// </summary>
        /// <param name="cellsX">Number of cells horizontally.</param>
        /// <param name="cellsY">Number of cells vertically.</param>
        /// <param name="pixelsPerCell">Size of each cell in pixels.</param>

        public static unsafe Bitmap GenerateTerrainPixelArtMap(int cellsX, int cellsY, int pixelsPerCell)
        {
            string path = TerrainTifPath;
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing terrain GeoTIFF", path);

            int widthPx = cellsX * pixelsPerCell;
            int heightPx = cellsY * pixelsPerCell;
            if (widthPx > MaxBitmapDimension || heightPx > MaxBitmapDimension)
                throw new ArgumentOutOfRangeException(nameof(pixelsPerCell),
                    $"Bitmap size {widthPx}x{heightPx} exceeds supported dimensions ({MaxBitmapDimension}).");

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
                                Color chosen = palette[rng.Next(palette.Length)];
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
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTerrainPixelArtMapLarge(int cellsX, int cellsY, int pixelsPerCell)
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

            var dest = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(widthPx, heightPx);
            Random rng = new Random();
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    Color baseColor = scaled.GetPixel(x, y);
                    Color[] palette = BuildPalette(baseColor);
                    for (int py = 0; py < pixelsPerCell; py++)
                    {
                        int destY = y * pixelsPerCell + py;
                        for (int px = 0; px < pixelsPerCell; px++)
                        {
                            Color chosen = palette[rng.Next(palette.Length)];
                            int destX = x * pixelsPerCell + px;
                            dest[destX, destY] = new Rgba32(chosen.R, chosen.G, chosen.B, chosen.A);
                        }
                    }
                }
            }

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
        private static Color[] BuildPalette(Color baseColor)
        {

            // The terrain raster uses near-white values for water. Replace them
            // with a consistent blue tone and avoid random variation so the
            // ocean does not look noisy.
            bool isWater = baseColor.R > 200 && baseColor.G > 200 && baseColor.B > 200 &&
                           Math.Abs(baseColor.R - baseColor.G) < 15 &&
                           Math.Abs(baseColor.R - baseColor.B) < 15;
            if (isWater)
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
