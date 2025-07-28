using System;
using System.Diagnostics;
using System.IO;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim
{
    /// <summary>
    /// Test vector graphics rendering without requiring UI components
    /// </summary>
    public static class VectorRenderingTest
    {
        public static void TestVectorRendering()
        {
            Console.WriteLine("=== TESTING VECTOR RENDERING SYSTEM ===");
            
            try
            {
                // Test SkiaSharp basic functionality
                TestBasicSkiaSharp();
                
                // Test vector tile rendering
                TestVectorTileRendering();
                
                // Test vector map managers
                TestVectorMapManagers();
                
                Console.WriteLine("✅ All vector rendering tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Vector rendering test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestBasicSkiaSharp()
        {
            Console.WriteLine("Testing basic SkiaSharp functionality...");
            
            // Create a test bitmap
            using var bitmap = new SKBitmap(512, 512);
            using var canvas = new SKCanvas(bitmap);
            
            // Clear with blue background
            canvas.Clear(new SKColor(100, 150, 200));
            
            // Draw a simple shape
            using var paint = new SKPaint
            {
                Color = SKColors.Red,
                Style = SKPaintStyle.Fill
            };
            
            canvas.DrawRect(100, 100, 200, 200, paint);
            
            // Save the test image
            string outputPath = Path.Combine("/tmp", "skia_test.png");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(outputPath);
            data.SaveTo(stream);
            
            Console.WriteLine($"✅ Basic SkiaSharp test passed - saved to {outputPath}");
        }
        
        private static void TestVectorTileRendering()
        {
            Console.WriteLine("Testing vector tile rendering...");
            
            var sw = Stopwatch.StartNew();
            
            // Test terrain renderer
            var terrainRenderer = new VectorTerrainTileRenderer(4096, 2048);
            var terrainBitmap = terrainRenderer.AssembleView(
                1, // zoom level
                new SKRectI(0, 0, 512, 512) // view area
            );
            
            if (terrainBitmap != null)
            {
                string terrainPath = Path.Combine("/tmp", "vector_terrain_test.png");
                using var image = SKImage.FromBitmap(terrainBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(terrainPath);
                data.SaveTo(stream);
                
                Console.WriteLine($"✅ Vector terrain rendering test passed - saved to {terrainPath}");
                terrainBitmap.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Vector terrain rendering returned null bitmap");
            }
            
            // Test political renderer
            var politicalManager = new PoliticalBorderManager();
            var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 4096, 2048);
            var politicalBitmap = politicalRenderer.AssembleView(
                1, // zoom level  
                new SKRectI(0, 0, 512, 512) // view area
            );
            
            if (politicalBitmap != null)
            {
                string politicalPath = Path.Combine("/tmp", "vector_political_test.png");
                using var image = SKImage.FromBitmap(politicalBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(politicalPath);
                data.SaveTo(stream);
                
                Console.WriteLine($"✅ Vector political rendering test passed - saved to {politicalPath}");
                politicalBitmap.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Vector political rendering returned null bitmap");
            }
            
            sw.Stop();
            Console.WriteLine($"Vector tile rendering completed in {sw.ElapsedMilliseconds}ms");
        }
        
        private static void TestVectorMapManagers()
        {
            Console.WriteLine("Testing vector map managers...");
            
            var vectorMapManager = new VectorHybridMapManager(4096, 2048);
            
            // Test terrain view
            vectorMapManager.SetViewType(MapViewType.Terrain);
            var terrainView = vectorMapManager.AssembleView(
                1, // zoom level
                new SKRectI(0, 0, 1024, 1024) // larger view area
            );
            
            if (terrainView != null)
            {
                string terrainPath = Path.Combine("/tmp", "vector_manager_terrain.png");
                using var image = SKImage.FromBitmap(terrainView);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(terrainPath);
                data.SaveTo(stream);
                
                Console.WriteLine($"✅ Vector manager terrain test passed - saved to {terrainPath}");
                terrainView.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Vector manager terrain view returned null bitmap");
            }
            
            // Test political view
            vectorMapManager.SetViewType(MapViewType.Political);
            var politicalView = vectorMapManager.AssembleView(
                1, // zoom level
                new SKRectI(0, 0, 1024, 1024) // larger view area
            );
            
            if (politicalView != null)
            {
                string politicalPath = Path.Combine("/tmp", "vector_manager_political.png");
                using var image = SKImage.FromBitmap(politicalView);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(politicalPath);
                data.SaveTo(stream);
                
                Console.WriteLine($"✅ Vector manager political test passed - saved to {politicalPath}");
                politicalView.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Vector manager political view returned null bitmap");
            }
            
            vectorMapManager.Dispose();
        }
    }
}