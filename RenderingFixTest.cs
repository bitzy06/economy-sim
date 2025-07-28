using System;
using System.Diagnostics;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim
{
    /// <summary>
    /// Simple test to verify the rendering fixes work
    /// </summary>
    public static class RenderingFixTest
    {
        public static void TestRenderingFix()
        {
            Console.WriteLine("=== Testing Vector Rendering Fix ===");
            
            try
            {
                // Create a vector manager
                var vectorManager = new VectorHybridMapManager(4096, 2048);
                
                // Test terrain rendering with debug info
                Console.WriteLine("Testing terrain rendering...");
                vectorManager.SetViewType(MapViewType.Terrain);
                
                // Use coordinates similar to what GameView would use
                var viewArea = new SKRectI(2048, 1024, 2048 + 1024, 1024 + 768);
                var terrainResult = vectorManager.AssembleView(1, viewArea);
                
                if (terrainResult != null)
                {
                    Console.WriteLine($"✓ Terrain rendered successfully: {terrainResult.Width}x{terrainResult.Height}");
                    
                    // Check if the bitmap has actual content (not just solid color)
                    bool hasContent = CheckBitmapHasContent(terrainResult);
                    Console.WriteLine($"  Bitmap has varied content: {hasContent}");
                    
                    // Save for inspection
                    SaveBitmap(terrainResult, "/tmp/terrain_fix_test.png");
                    terrainResult.Dispose();
                }
                else
                {
                    Console.WriteLine("✗ Terrain rendering failed - null result");
                }
                
                // Test political rendering
                Console.WriteLine("Testing political rendering...");
                vectorManager.SetViewType(MapViewType.Political);
                
                var politicalResult = vectorManager.AssembleView(1, viewArea);
                
                if (politicalResult != null)
                {
                    Console.WriteLine($"✓ Political rendered successfully: {politicalResult.Width}x{politicalResult.Height}");
                    
                    bool hasContent = CheckBitmapHasContent(politicalResult);
                    Console.WriteLine($"  Bitmap has varied content: {hasContent}");
                    
                    SaveBitmap(politicalResult, "/tmp/political_fix_test.png");
                    politicalResult.Dispose();
                }
                else
                {
                    Console.WriteLine("✗ Political rendering failed - null result");
                }
                
                vectorManager.Dispose();
                Console.WriteLine("=== Vector Rendering Fix Test Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test failed with exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static bool CheckBitmapHasContent(SKBitmap bitmap)
        {
            if (bitmap.Width < 10 || bitmap.Height < 10)
                return false;
                
            // Sample several pixels to see if there's variation
            var samples = new SKColor[9];
            samples[0] = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 4);
            samples[1] = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 4);
            samples[2] = bitmap.GetPixel(3 * bitmap.Width / 4, bitmap.Height / 4);
            samples[3] = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 2);
            samples[4] = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
            samples[5] = bitmap.GetPixel(3 * bitmap.Width / 4, bitmap.Height / 2);
            samples[6] = bitmap.GetPixel(bitmap.Width / 4, 3 * bitmap.Height / 4);
            samples[7] = bitmap.GetPixel(bitmap.Width / 2, 3 * bitmap.Height / 4);
            samples[8] = bitmap.GetPixel(3 * bitmap.Width / 4, 3 * bitmap.Height / 4);
            
            // Check if any sample differs significantly from the first
            var first = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                var current = samples[i];
                int rDiff = Math.Abs(current.Red - first.Red);
                int gDiff = Math.Abs(current.Green - first.Green);
                int bDiff = Math.Abs(current.Blue - first.Blue);
                
                if (rDiff > 20 || gDiff > 20 || bDiff > 20)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private static void SaveBitmap(SKBitmap bitmap, string filePath)
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = System.IO.File.OpenWrite(filePath);
                data.AsStream().CopyTo(stream);
                Console.WriteLine($"  Saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Failed to save: {ex.Message}");
            }
        }
    }
}