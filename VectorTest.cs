using System;
using System.Diagnostics;
using StrategyGame;
using SkiaSharp;

namespace StrategyGame.Testing
{
    /// <summary>
    /// Test vector graphics performance and functionality directly
    /// </summary>
    public static class VectorTest
    {
        public static void RunDirectTest()
        {
            Console.WriteLine("=== DIRECT VECTOR GRAPHICS TEST ===");
            
            try
            {
                // Test terrain renderer
                Console.WriteLine("Testing terrain renderer...");
                TestTerrainRenderer();
                
                // Test political renderer  
                Console.WriteLine("Testing political renderer...");
                TestPoliticalRenderer();
                
                Console.WriteLine("✅ All direct tests passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static void TestTerrainRenderer()
        {
            var sw = Stopwatch.StartNew();
            
            var terrainRenderer = new VectorTerrainTileRenderer(4096, 2048);
            
            // Test view assembly (this should be much faster now)
            var viewArea = new SKRectI(0, 0, 512, 512);
            var bitmap = terrainRenderer.AssembleView(1, viewArea);
            
            sw.Stop();
            
            if (bitmap != null)
            {
                Console.WriteLine($"✅ Terrain rendering: {sw.ElapsedMilliseconds}ms (size: {bitmap.Width}x{bitmap.Height})");
                bitmap.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Terrain rendering failed - no bitmap generated");
            }
            
            terrainRenderer.Dispose();
        }
        
        private static void TestPoliticalRenderer()
        {
            var sw = Stopwatch.StartNew();
            
            var politicalManager = new PoliticalBorderManager();
            var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 4096, 2048);
            
            // Test view assembly
            var viewArea = new SKRectI(0, 0, 512, 512);
            var bitmap = politicalRenderer.AssembleView(1, viewArea);
            
            sw.Stop();
            
            if (bitmap != null)
            {
                Console.WriteLine($"✅ Political rendering: {sw.ElapsedMilliseconds}ms (size: {bitmap.Width}x{bitmap.Height})");
                bitmap.Dispose();
            }
            else
            {
                Console.WriteLine("❌ Political rendering failed - no bitmap generated");
            }
            
            politicalRenderer.Dispose();
        }
    }
}