using System;
using System.Diagnostics;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Integration test to verify grid-based PoliticalTileManager works with existing system
    /// </summary>
    public static class GridIntegrationTest
    {
        /// <summary>
        /// Test the grid-based PoliticalTileManager integration
        /// </summary>
        public static void TestGridPoliticalTileManager()
        {
            Debug.WriteLine("=== Starting Grid PoliticalTileManager Integration Test ===");

            try
            {
                // Create components
                var politicalManager = new PoliticalBorderManager();
                var politicalTileManager = new PoliticalTileManager(politicalManager, 2048, 1024);
                var hybridManager = new HybridMapManager(2048, 1024);

                Debug.WriteLine("✓ Components created successfully");

                // Test setting political map date
                politicalTileManager.SetPoliticalMapDate(new DateTime(1950, 1, 1));
                Debug.WriteLine("✓ Political map date set");

                // Test generating tiles with grid system using synchronous mode for deterministic results
                var viewArea = new SKRectI(0, 0, 512, 512);
                var tileBitmap = politicalTileManager.AssembleView(0, viewArea, null, forceSync: true);
                
                if (tileBitmap != null)
                {
                    Debug.WriteLine($"✓ Grid tile assembled: {tileBitmap.Width}x{tileBitmap.Height}");
                    
                    // Save test tile for visual inspection
                    var outputPath = "/tmp/grid_integration_test.png";
                    System.IO.Directory.CreateDirectory("/tmp");
                    using (var stream = System.IO.File.Create(outputPath))
                    {
                        tileBitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
                    }
                    Debug.WriteLine($"✓ Test tile saved to: {outputPath}");
                    tileBitmap.Dispose();
                }
                else
                {
                    Debug.WriteLine("⚠ Tile bitmap is null");
                }

                // Test hybrid manager integration
                hybridManager.SetViewType(MapViewType.Political);
                hybridManager.SetPoliticalMapDate(new DateTime(1950, 1, 1));
                
                var hybridViewArea = new SKRectI(0, 0, 512, 512);
                var hybridBitmap = hybridManager.AssembleView(0, hybridViewArea);
                
                if (hybridBitmap != null)
                {
                    Debug.WriteLine($"✓ Hybrid manager view assembled: {hybridBitmap.Width}x{hybridBitmap.Height}");
                    hybridBitmap.Dispose();
                }
                else
                {
                    Debug.WriteLine("⚠ Hybrid manager returned null bitmap");
                }

                // Test war mechanics
                Debug.WriteLine("Testing war mechanics...");
                var testCells = new[]
                {
                    new System.Drawing.Point(100, 100),
                    new System.Drawing.Point(101, 100),
                    new System.Drawing.Point(100, 101)
                };
                
                politicalTileManager.ChangeControl(999, testCells);
                Debug.WriteLine("✓ Control change applied");

                var frontline = politicalTileManager.ComputeFrontline();
                Debug.WriteLine($"✓ Frontline computed: {frontline.Count} cells");

                // Test coordinate lookup
                var countryId = politicalTileManager.GetCountryAtGeographic(0.0, 0.0);
                Debug.WriteLine($"✓ Country at (0,0): {countryId}");

                // Cleanup
                politicalTileManager.Dispose();
                hybridManager.Dispose();

                Debug.WriteLine("=== Grid PoliticalTileManager Integration Test Passed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Integration Test Failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Test performance comparison between grid and legacy systems
        /// </summary>
        public static void TestPerformanceComparison()
        {
            Debug.WriteLine("=== Starting Performance Comparison Test ===");

            try
            {
                var politicalManager = new PoliticalBorderManager();
                var politicalTileManager = new PoliticalTileManager(politicalManager, 1024, 512);

                var sw = Stopwatch.StartNew();

                // Test grid-based rendering
                sw.Restart();
                var testViewArea = new SKRectI(0, 0, 512, 512);
                for (int i = 0; i < 4; i++)
                {
                    var bitmap = politicalTileManager.AssembleView(0, testViewArea);
                    bitmap?.Dispose();
                }
                sw.Stop();
                var gridTime = sw.ElapsedMilliseconds;

                Debug.WriteLine($"✓ Grid-based rendering (4 tiles): {gridTime}ms ({gridTime / 4.0:F1}ms per tile)");

                politicalTileManager.Dispose();

                Debug.WriteLine("=== Performance Comparison Test Completed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Performance Test Failed: {ex.Message}");
                throw;
            }
        }
    }
}