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
            {
                var dest = new Bitmap(width, height);
                using (var g = Graphics.FromImage(dest))
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(img, 0, 0, width, height);
                }
                return dest;
            }
        }
    }
}
