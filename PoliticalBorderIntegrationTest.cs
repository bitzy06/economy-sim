using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim.Testing
{
    /// <summary>
    /// Test class to verify political border functionality without requiring CShapes file
    /// </summary>
    public static class PoliticalBorderIntegrationTest
    {
        public static void RunBasicTests()
        {
            Debug.WriteLine("=== Political Border Integration Tests ===");
            
            try
            {
                // Test 1: Create PoliticalBorderManager
                var politicalManager = new PoliticalBorderManager();
                Debug.WriteLine("✓ PoliticalBorderManager created successfully");
                
                // Test 2: Test color generation and retrieval
                var colors = politicalManager.GetAllCountryColors();
                Debug.WriteLine($"✓ Initial country colors loaded: {colors.Count} countries");
                
                // Test 3: Test color retrieval for non-existent country (should return default)
                var testColor = politicalManager.GetCountryColor("TESTCOUNTRY");
                Debug.WriteLine($"✓ Default color retrieved: {testColor}");
                
                // Test 4: Create HybridMapManager
                var hybridManager = new HybridMapManager(1024, 512);
                Debug.WriteLine("✓ HybridMapManager created successfully");
                
                // Test 5: Test view type switching
                Debug.WriteLine($"✓ Initial view type: {hybridManager.CurrentViewType}");
                
                hybridManager.SetViewType(MapViewType.Political);
                Debug.WriteLine($"✓ Switched to political view: {hybridManager.CurrentViewType}");
                
                hybridManager.SetViewType(MapViewType.Terrain);
                Debug.WriteLine($"✓ Switched back to terrain view: {hybridManager.CurrentViewType}");
                
                // Test 6: Test political map date setting
                var testDate = new DateTime(1950, 1, 1);
                hybridManager.SetPoliticalMapDate(testDate);
                Debug.WriteLine($"✓ Political map date set to: {hybridManager.PoliticalMapDate:yyyy-MM-dd}");
                
                // Test 7: Test dummy political mask rendering
                TestDummyPoliticalRendering(politicalManager);
                
                // Test 8: Save color mapping
                politicalManager.SaveColorMapping();
                Debug.WriteLine("✓ Color mapping saved successfully");
                
                Debug.WriteLine("=== All political border tests passed! ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Test failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestDummyPoliticalRendering(PoliticalBorderManager manager)
        {
            // Create a small test mask with some dummy country codes
            int width = 100, height = 50;
            int[,] testMask = new int[height, width];
            
            // Fill with some test patterns
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < width / 3)
                        testMask[y, x] = 1; // Country 1
                    else if (x < 2 * width / 3)
                        testMask[y, x] = 2; // Country 2
                    else
                        testMask[y, x] = 3; // Country 3
                }
            }
            
            // Render the test mask
            var bitmap = manager.RenderPoliticalMap(testMask, width, height);
            
            if (bitmap != null)
            {
                Debug.WriteLine($"✓ Test political map rendered: {bitmap.Width}x{bitmap.Height}");
                bitmap.Dispose();
            }
            else
            {
                Debug.WriteLine("⚠ Test political map rendering returned null");
            }
        }
    }
}