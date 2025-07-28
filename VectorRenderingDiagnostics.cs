using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using StrategyGame;
using Avalonia;

namespace Economy_sim
{
    /// <summary>
    /// Comprehensive diagnostics to identify vector rendering issues
    /// </summary>
    public static class VectorRenderingDiagnostics
    {
        public static void RunFullDiagnostics()
        {
            Console.WriteLine("=== Starting Vector Rendering Diagnostics ===");
            
            // Test 1: Basic SkiaSharp functionality
            TestBasicSkiaSharp();
            
            // Test 2: Vector tile generation
            TestVectorTileGeneration();
            
            // Test 3: Full rendering pipeline
            TestFullRenderingPipeline();
            
            // Test 4: Actual bitmap output
            TestBitmapOutput();
            
            Console.WriteLine("=== Vector Rendering Diagnostics Complete ===");
        }
        
        private static void TestBasicSkiaSharp()
        {
            Console.WriteLine("--- Test 1: Basic SkiaSharp ---");
            
            try
            {
                // Test basic bitmap creation
                using var bitmap = new SKBitmap(256, 256);
                using var canvas = new SKCanvas(bitmap);
                
                // Clear to blue
                canvas.Clear(SKColors.Blue);
                
                // Draw a simple rectangle
                using var paint = new SKPaint { Color = SKColors.Red };
                canvas.DrawRect(50, 50, 156, 156, paint);
                
                // Check if the bitmap has the expected colors
                var bluePixel = bitmap.GetPixel(10, 10);
                var redPixel = bitmap.GetPixel(100, 100);
                
                Console.WriteLine($"  Blue pixel: {bluePixel} (expected blue-ish)");
                Console.WriteLine($"  Red pixel: {redPixel} (expected red-ish)");
                
                bool basicTestPassed = 
                    bluePixel.Blue > 200 && bluePixel.Red < 50 && bluePixel.Green < 50 &&
                    redPixel.Red > 200 && redPixel.Blue < 50 && redPixel.Green < 50;
                
                Console.WriteLine($"  Basic SkiaSharp test: {(basicTestPassed ? "PASSED" : "FAILED")}");
                
                // Save test bitmap
                SaveBitmapToPng(bitmap, "/tmp/basic_skia_test.png");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Basic SkiaSharp test FAILED: {ex.Message}");
            }
        }
        
        private static void TestVectorTileGeneration()
        {
            Debug.WriteLine("--- Test 2: Vector Tile Generation ---");
            
            try
            {
                var terrainRenderer = new VectorTerrainTileRenderer(4096, 2048);
                var politicalRenderer = new VectorPoliticalTileRenderer(new PoliticalBorderManager(), 4096, 2048);
                
                // Test terrain tile generation
                var viewArea = new SKRectI(0, 0, 512, 512);
                var terrainBitmap = terrainRenderer.AssembleView(1, viewArea);
                
                if (terrainBitmap != null)
                {
                    Debug.WriteLine($"  Terrain tile generated: {terrainBitmap.Width}x{terrainBitmap.Height}");
                    
                    // Check if it's not just a solid color
                    var samples = SampleBitmapColors(terrainBitmap, 10);
                    bool hasVariation = CheckColorVariation(samples);
                    Debug.WriteLine($"  Terrain has color variation: {hasVariation}");
                    
                    SaveBitmapToPng(terrainBitmap, "/tmp/terrain_test.png");
                    terrainBitmap.Dispose();
                }
                else
                {
                    Debug.WriteLine("  Terrain tile generation FAILED: null result");
                }
                
                // Test political tile generation
                var politicalBitmap = politicalRenderer.AssembleView(1, viewArea);
                
                if (politicalBitmap != null)
                {
                    Debug.WriteLine($"  Political tile generated: {politicalBitmap.Width}x{politicalBitmap.Height}");
                    
                    var samples = SampleBitmapColors(politicalBitmap, 10);
                    bool hasVariation = CheckColorVariation(samples);
                    Debug.WriteLine($"  Political has color variation: {hasVariation}");
                    
                    SaveBitmapToPng(politicalBitmap, "/tmp/political_test.png");
                    politicalBitmap.Dispose();
                }
                else
                {
                    Debug.WriteLine("  Political tile generation FAILED: null result");
                }
                
                terrainRenderer.Dispose();
                politicalRenderer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Vector tile generation test FAILED: {ex.Message}");
            }
        }
        
        private static void TestFullRenderingPipeline()
        {
            Debug.WriteLine("--- Test 3: Full Rendering Pipeline ---");
            
            try
            {
                var vectorManager = new VectorHybridMapManager(4096, 2048);
                
                // Test terrain rendering
                vectorManager.SetViewType(MapViewType.Terrain);
                var viewArea = new SKRectI(2048, 1024, 2048 + 1024, 1024 + 768);
                var terrainResult = vectorManager.AssembleView(1, viewArea);
                
                if (terrainResult != null)
                {
                    Debug.WriteLine($"  Full terrain pipeline: {terrainResult.Width}x{terrainResult.Height}");
                    SaveBitmapToPng(terrainResult, "/tmp/full_terrain_pipeline.png");
                    terrainResult.Dispose();
                }
                else
                {
                    Debug.WriteLine("  Full terrain pipeline FAILED: null result");
                }
                
                // Test political rendering
                vectorManager.SetViewType(MapViewType.Political);
                var politicalResult = vectorManager.AssembleView(1, viewArea);
                
                if (politicalResult != null)
                {
                    Debug.WriteLine($"  Full political pipeline: {politicalResult.Width}x{politicalResult.Height}");
                    SaveBitmapToPng(politicalResult, "/tmp/full_political_pipeline.png");
                    politicalResult.Dispose();
                }
                else
                {
                    Debug.WriteLine("  Full political pipeline FAILED: null result");
                }
                
                vectorManager.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Full rendering pipeline test FAILED: {ex.Message}");
            }
        }
        
        private static void TestBitmapOutput()
        {
            Debug.WriteLine("--- Test 4: Bitmap Output ---");
            
            try
            {
                // Create a test bitmap with known content
                using var testBitmap = new SKBitmap(512, 512);
                using var canvas = new SKCanvas(testBitmap);
                
                // Create a recognizable pattern
                canvas.Clear(SKColors.White);
                
                using var paint = new SKPaint { Color = SKColors.Green };
                canvas.DrawRect(100, 100, 312, 312, paint);
                
                paint.Color = SKColors.Blue;
                canvas.DrawCircle(256, 256, 100, paint);
                
                paint.Color = SKColors.Red;
                canvas.DrawText("TEST", 200, 300, paint);
                
                SaveBitmapToPng(testBitmap, "/tmp/test_pattern.png");
                Debug.WriteLine("  Test pattern saved successfully");
                
                // Now test the actual game's coordinate system
                var gameViewSize = new Size(2560, 1440);
                var mapSize = new SKSizeI(4096, 2048);
                
                // Calculate center offset like the game does
                int centerX = Math.Max(0, (mapSize.Width - 2560) / 2);
                int centerY = Math.Max(0, (mapSize.Height - 1440) / 2);
                
                Debug.WriteLine($"  Game coordinates - Map size: {mapSize}, View size: {gameViewSize}");
                Debug.WriteLine($"  Center offset: ({centerX}, {centerY})");
                
                var testViewArea = new SKRectI(centerX, centerY, centerX + 2560, centerY + 1440);
                Debug.WriteLine($"  Test view area: {testViewArea}");
                
                // Test with the same coordinates as the game uses
                var vectorManager = new VectorHybridMapManager(4096, 2048);
                var gameStyleResult = vectorManager.AssembleView(1, testViewArea);
                
                if (gameStyleResult != null)
                {
                    Debug.WriteLine($"  Game-style coordinates result: {gameStyleResult.Width}x{gameStyleResult.Height}");
                    SaveBitmapToPng(gameStyleResult, "/tmp/game_coordinates_test.png");
                    gameStyleResult.Dispose();
                }
                else
                {
                    Debug.WriteLine("  Game-style coordinates test FAILED: null result");
                }
                
                vectorManager.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Bitmap output test FAILED: {ex.Message}");
            }
        }
        
        private static SKColor[] SampleBitmapColors(SKBitmap bitmap, int sampleCount)
        {
            var samples = new SKColor[sampleCount];
            var random = new Random(42); // Fixed seed for reproducible results
            
            for (int i = 0; i < sampleCount; i++)
            {
                int x = random.Next(0, bitmap.Width);
                int y = random.Next(0, bitmap.Height);
                samples[i] = bitmap.GetPixel(x, y);
            }
            
            return samples;
        }
        
        private static bool CheckColorVariation(SKColor[] colors)
        {
            if (colors.Length < 2) return false;
            
            var first = colors[0];
            for (int i = 1; i < colors.Length; i++)
            {
                var current = colors[i];
                int rDiff = Math.Abs(current.Red - first.Red);
                int gDiff = Math.Abs(current.Green - first.Green);
                int bDiff = Math.Abs(current.Blue - first.Blue);
                
                // If any sample differs by more than 10 in any channel, consider it varied
                if (rDiff > 10 || gDiff > 10 || bDiff > 10)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private static void SaveBitmapToPng(SKBitmap bitmap, string filePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "/tmp");
                
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(filePath);
                data.AsStream().CopyTo(stream);
                
                Debug.WriteLine($"  Saved bitmap to: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Failed to save bitmap to {filePath}: {ex.Message}");
            }
        }
    }
}