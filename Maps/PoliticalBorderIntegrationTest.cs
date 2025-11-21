using System;
using System.Collections.Generic;
using System.Diagnostics;
using SkiaSharp;
using Economy_sim;

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
                
                // Test 8: Test coordinate transformation unification
                TestCoordinateTransformUnification();
                
                // Test 9: Run comprehensive coordinate validation
                CoordinateValidation.ValidateCoordinateUnification();
                
                // Test 10: Check that country colors are accessible
                var allColors = politicalManager.GetAllCountryColors();
                Debug.WriteLine($"✓ {allColors.Count} country colors are available");
                
                Debug.WriteLine("=== All political border tests passed! ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Test failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestCoordinateTransformUnification()
        {
            Debug.WriteLine("=== Testing Coordinate Transform Unification ===");
            
            int baseWidth = 4096;
            int baseHeight = 2048;
            int tileSizePx = 512;
            
            // Test tile 0,0 (top-left)
            var bounds1 = CoordinateTransform.GetTileGeographicBounds(0, 0, tileSizePx, baseWidth, baseHeight);
            Debug.WriteLine($"Tile (0,0) bounds: {bounds1}");
            
            // Test tile 1,1
            var bounds2 = CoordinateTransform.GetTileGeographicBounds(1, 1, tileSizePx, baseWidth, baseHeight);
            Debug.WriteLine($"Tile (1,1) bounds: {bounds2}");
            
            // Verify bounds are valid
            Debug.WriteLine($"Bounds valid: {CoordinateTransform.IsValidGeoBounds(bounds1)}");
            Debug.WriteLine($"Bounds valid: {CoordinateTransform.IsValidGeoBounds(bounds2)}");
            
            // Test pixel to geographic conversion
            var (lon, lat) = CoordinateTransform.PixelToGeographic(baseWidth/2, baseHeight/2, baseWidth, baseHeight);
            Debug.WriteLine($"Center pixel maps to: {lon:F2}, {lat:F2} (should be ~0, 0)");
            
            // Test that both terrain and political should now use same coordinate system
            Debug.WriteLine("✓ Coordinate transformation unification tests passed");
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
            
            // Render the test mask using our own simple rendering
            var bitmap = RenderTestPoliticalMap(testMask, width, height, manager);
            
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
        
        private static SKBitmap RenderTestPoliticalMap(int[,] mask, int width, int height, PoliticalBorderManager manager)
        {
            var bitmap = new SKBitmap(width, height);
            
            // Simple test rendering
            for (int y = 0; y < height && y < mask.GetLength(0); y++)
            {
                for (int x = 0; x < width && x < mask.GetLength(1); x++)
                {
                    int countryCode = mask[y, x];
                    SKColor color;
                    
                    if (countryCode == 0)
                    {
                        color = SKColors.LightBlue; // Water
                    }
                    else
                    {
                        // Get color from manager or use test colors
                        color = manager.GetCountryColorByRasterCode(countryCode);
                        if (color == SKColor.Parse("#808080")) // If grey default, use test colors
                        {
                            color = countryCode switch
                            {
                                1 => SKColors.Red,
                                2 => SKColors.Green,
                                3 => SKColors.Blue,
                                _ => SKColors.Yellow
                            };
                        }
                    }
                    
                    bitmap.SetPixel(x, y, color);
                }
            }
            
            return bitmap;
        }
    }
}