using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace StrategyGame
{
    /// <summary>
    /// Provides helper methods for generating a pixel-art map based on the
    /// ETOPO1 elevation data. The GeoTIFF is downloaded using the existing
    /// Python script when not already present.
    /// </summary>
    public static class PixelMapGenerator
    {
        private static readonly string TifPath =
     @"C:\Users\kayla\source\repos\bitzy06\resources\ETOPO1_Bed_g_geotiff.tif";

        /// <summary>
        /// Ensures the GeoTIFF file exists by invoking fetch_etopo1.py if needed.
        /// </summary>
        public static void EnsureElevationData()
        {
            if (File.Exists(TifPath))
                return;

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "fetch_etopo1.py",
                WorkingDirectory = @"C:\Users\kayla\source\repos\bitzy06\economy-sim",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using (var proc = Process.Start(psi))
            {
                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                DebugLogger.Log(output);
                if (!string.IsNullOrEmpty(err))
                    DebugLogger.Log(err);
            }
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
    }
}
