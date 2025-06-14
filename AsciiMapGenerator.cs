using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace StrategyGame
{
    public static class AsciiMapGenerator
    {
        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        private static readonly string DataDir = Path.Combine(RepoRoot, "data");
        private static readonly string TifPath = Path.Combine(DataDir, "ETOPO1_Ice_g_geotiff.tif");

        public static void EnsureElevationData()
        {
            if (File.Exists(TifPath))
                return;

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "fetch_etopo1.py",
                WorkingDirectory = RepoRoot,
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

        public static string GenerateAsciiMap(int width, int height)
        {
            EnsureElevationData();
            if (!File.Exists(TifPath))
                throw new FileNotFoundException("Missing GeoTIFF file", TifPath);

            using (var img = new Bitmap(TifPath))
            using (var scaled = new Bitmap(img, width, height))
            {
                var chars = " .:-=+*#%@";
                var sw = new StringWriter();
                for (int y = 0; y < scaled.Height; y++)
                {
                    for (int x = 0; x < scaled.Width; x++)
                    {
                        Color c = scaled.GetPixel(x, y);
                        double brightness = c.GetBrightness();
                        int idx = (int)(brightness * (chars.Length - 1));
                        sw.Write(chars[idx]);
                    }
                    sw.WriteLine();
                }
                return sw.ToString();
            }
        }
    }
}
