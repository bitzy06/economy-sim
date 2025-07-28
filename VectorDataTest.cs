using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Test class to verify that vector graphics system now uses real LFS data
    /// </summary>
    public static class VectorDataTest
    {
        public static void RunDataIntegrationTest()
        {
            Console.WriteLine("=== Vector Data Integration Test ===");
            
            // Test data file access
            TestDataFileAccess();
            
            // Test terrain vector rendering with real data
            TestTerrainVectorGeneration();
            
            // Test political vector rendering with real data
            TestPoliticalVectorGeneration();
            
            Console.WriteLine("=== Test Complete ===");
        }
        
        private static void TestDataFileAccess()
        {
            Console.WriteLine("\n--- Testing Data File Access ---");
            
            string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string dataDir = Path.Combine(repoRoot, "data");
            
            // Check for terrain data
            string terrainFile = Path.Combine(dataDir, "terrain", "NE1_HR_LC.tif");
            Console.WriteLine($"Terrain GeoTIFF: {(File.Exists(terrainFile) ? "FOUND" : "MISSING")} - {terrainFile}");
            if (File.Exists(terrainFile))
            {
                var info = new FileInfo(terrainFile);
                Console.WriteLine($"  Size: {info.Length / (1024 * 1024)} MB");
            }
            
            // Check for country boundaries
            string countryFile = Path.Combine(dataDir, "terrain", "ne_10m_admin_0_countries.shp");
            Console.WriteLine($"Country Shapefile: {(File.Exists(countryFile) ? "FOUND" : "MISSING")} - {countryFile}");
            
            string cshapesFile = Path.Combine(dataDir, "country_borders", "CShapes-2.0.shp");
            Console.WriteLine($"CShapes Shapefile: {(File.Exists(cshapesFile) ? "FOUND" : "MISSING")} - {cshapesFile}");
        }
        
        private static void TestTerrainVectorGeneration()
        {
            Console.WriteLine("\n--- Testing Terrain Vector Generation ---");
            
            try
            {
                var mapManager = new VectorHybridMapManager(4096, 2048);
                mapManager.SetViewType(MapViewType.Terrain);
                
                // Generate a test tile view
                var stopwatch = Stopwatch.StartNew();
                var viewArea = new SKRectI(0, 0, 512, 512);  // Small test area
                var bitmap = mapManager.AssembleView(1, viewArea);
                stopwatch.Stop();
                
                if (bitmap != null)
                {
                    Console.WriteLine($"✓ Terrain view generated successfully in {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"  Bitmap size: {bitmap.Width}x{bitmap.Height}");
                    Console.WriteLine($"  Color info: {bitmap.ColorType}, {bitmap.AlphaType}");
                    
                    // Check if we have pixel variation (indicating real data vs solid color)
                    var hasVariation = CheckPixelVariation(bitmap);
                    Console.WriteLine($"  Has pixel variation: {(hasVariation ? "YES" : "NO")}");
                    
                    bitmap.Dispose();
                }
                else
                {
                    Console.WriteLine("✗ Failed to generate terrain view");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Terrain test failed: {ex.Message}");
            }
        }
        
        private static void TestPoliticalVectorGeneration()
        {
            Console.WriteLine("\n--- Testing Political Vector Generation ---");
            
            try
            {
                var mapManager = new VectorHybridMapManager(4096, 2048);
                mapManager.SetViewType(MapViewType.Political);
                
                // Generate a test tile view
                var stopwatch = Stopwatch.StartNew();
                var viewArea = new SKRectI(0, 0, 512, 512);  // Small test area
                var bitmap = mapManager.AssembleView(1, viewArea);
                stopwatch.Stop();
                
                if (bitmap != null)
                {
                    Console.WriteLine($"✓ Political view generated successfully in {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"  Bitmap size: {bitmap.Width}x{bitmap.Height}");
                    Console.WriteLine($"  Color info: {bitmap.ColorType}, {bitmap.AlphaType}");
                    
                    // Check if we have pixel variation (indicating real data vs solid color)
                    var hasVariation = CheckPixelVariation(bitmap);
                    Console.WriteLine($"  Has pixel variation: {(hasVariation ? "YES" : "NO")}");
                    
                    bitmap.Dispose();
                }
                else
                {
                    Console.WriteLine("✗ Failed to generate political view");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Political test failed: {ex.Message}");
            }
        }
        
        private static bool CheckPixelVariation(SKBitmap bitmap)
        {
            // Check a sample of pixels to see if we have variation
            // This indicates real data rather than solid color fallback
            var colors = new HashSet<uint>();
            int samples = Math.Min(100, bitmap.Width * bitmap.Height);
            
            for (int i = 0; i < samples; i++)
            {
                int x = (i * bitmap.Width / samples) % bitmap.Width;
                int y = (i / bitmap.Width) % bitmap.Height;
                
                var color = bitmap.GetPixel(x, y);
                colors.Add((uint)color);
                
                if (colors.Count > 5) // If we have more than 5 distinct colors, we likely have real data
                    return true;
            }
            
            return colors.Count > 2; // At least some variation
        }
    }
}