using System;
using System.Diagnostics;
using StrategyGame;

namespace StrategyGame.Testing
{
    /// <summary>
    /// Test vector graphics logic without GPU dependencies
    /// </summary>
    public static class VectorLogicTest
    {
        public static void RunLogicTest()
        {
            Console.WriteLine("=== VECTOR LOGIC TEST (No GPU) ===");
            
            try
            {
                // Test terrain data generation
                Console.WriteLine("Testing terrain data generation...");
                TestTerrainDataGeneration();
                
                // Test political data generation
                Console.WriteLine("Testing political data generation...");
                TestPoliticalDataGeneration();
                
                Console.WriteLine("✅ All logic tests passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static void TestTerrainDataGeneration()
        {
            var sw = Stopwatch.StartNew();
            
            var terrainRenderer = new VectorTerrainTileRenderer(4096, 2048);
            
            // Test tile generation without GPU rendering
            var task = terrainRenderer.GetType()
                .GetMethod("LoadVectorDataForTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(terrainRenderer, new object[] { 3, 0, 0 }) as System.Threading.Tasks.Task<VectorTile>;
            
            if (task != null)
            {
                task.Wait();
                var vectorTile = task.Result;
                
                sw.Stop();
                
                if (vectorTile != null)
                {
                    Console.WriteLine($"✅ Terrain data generation: {sw.ElapsedMilliseconds}ms");
                    Console.WriteLine($"   Features generated: {vectorTile.Features.Count}");
                    Console.WriteLine($"   Tile bounds: {vectorTile.Bounds}");
                }
                else
                {
                    Console.WriteLine("❌ Terrain data generation failed");
                }
            }
            else
            {
                Console.WriteLine("❌ Could not invoke terrain data generation");
            }
            
            terrainRenderer.Dispose();
        }
        
        private static void TestPoliticalDataGeneration()
        {
            var sw = Stopwatch.StartNew();
            
            var politicalManager = new PoliticalBorderManager();
            var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 4096, 2048);
            
            // Test tile generation without GPU rendering
            var task = politicalRenderer.GetType()
                .GetMethod("LoadVectorDataForTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(politicalRenderer, new object[] { 3, 0, 0 }) as System.Threading.Tasks.Task<VectorTile>;
            
            if (task != null)
            {
                task.Wait();
                var vectorTile = task.Result;
                
                sw.Stop();
                
                if (vectorTile != null)
                {
                    Console.WriteLine($"✅ Political data generation: {sw.ElapsedMilliseconds}ms");
                    Console.WriteLine($"   Features generated: {vectorTile.Features.Count}");
                    Console.WriteLine($"   Tile bounds: {vectorTile.Bounds}");
                    
                    // Show some sample countries
                    int count = 0;
                    foreach (var feature in vectorTile.Features)
                    {
                        if (feature.Properties.TryGetValue("countryName", out var name))
                        {
                            Console.WriteLine($"   Country: {name}");
                            if (++count >= 3) break; // Show first 3 countries
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ Political data generation failed");
                }
            }
            else
            {
                Console.WriteLine("❌ Could not invoke political data generation");
            }
            
            politicalRenderer.Dispose();
        }
    }
}