using System;
using System.Diagnostics;
using System.IO;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;

namespace StrategyGame
{
    /// <summary>
    /// Simple debug test to understand vector rendering issues
    /// </summary>
    public static class VectorDebugTest
    {
        public static void RunDebugTest()
        {
            Console.WriteLine("=== Vector Debug Test ===");
            
            try
            {
                // Step 1: Initialize GDAL
                Console.WriteLine("Initializing GDAL...");
                Economy_sim.GdalInit.Ensure();
                Console.WriteLine("GDAL initialized successfully");
                
                // Step 2: Test terrain renderer creation
                Console.WriteLine("Creating terrain renderer...");
                VectorTerrainTileRenderer? terrainRenderer = null;
                try 
                {
                    terrainRenderer = new VectorTerrainTileRenderer(1024, 1024);
                    Console.WriteLine("Terrain renderer created successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create terrain renderer: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return;
                }
                
                // Step 3: Test political renderer creation
                Console.WriteLine("Creating political renderer...");
                VectorPoliticalTileRenderer? politicalRenderer = null;
                try 
                {
                    var politicalManager = new PoliticalBorderManager();
                    politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 1024, 1024);
                    Console.WriteLine("Political renderer created successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create political renderer: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return;
                }
                
                // Step 4: Test vector tile generation with GUI coordinates
                Console.WriteLine("Testing vector tile generation with GUI coordinates...");
                try 
                {
                    // Test with coordinates from the user's debug log
                    // ViewArea={Left=5175,Top=2004,Width=2560,Height=1440}
                    // This should map to tiles around (10, 3)
                    var terrainTile = terrainRenderer.GetVectorTileForTesting(10, 3, 1);
                    Console.WriteLine($"Terrain tile (10,3) result: {(terrainTile != null ? "SUCCESS" : "NULL")}");
                    if (terrainTile != null)
                    {
                        Console.WriteLine($"  Features count: {terrainTile.Features.Count}");
                        Console.WriteLine($"  Tile coordinates: ({terrainTile.TileX}, {terrainTile.TileY})");
                    }
                    
                    var politicalTile = politicalRenderer.GetVectorTileForTesting(10, 3, 1);
                    Console.WriteLine($"Political tile (10,3) result: {(politicalTile != null ? "SUCCESS" : "NULL")}");
                    if (politicalTile != null)
                    {
                        Console.WriteLine($"  Features count: {politicalTile.Features.Count}");
                        Console.WriteLine($"  Tile coordinates: ({politicalTile.TileX}, {politicalTile.TileY})");
                    }
                    
                    // Test another tile from that range
                    var terrainTile2 = terrainRenderer.GetVectorTileForTesting(15, 5, 1);
                    Console.WriteLine($"Terrain tile (15,5) result: {(terrainTile2 != null ? "SUCCESS" : "NULL")}");
                    if (terrainTile2 != null)
                    {
                        Console.WriteLine($"  Features count: {terrainTile2.Features.Count}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Vector tile generation failed: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                
                // Clean up
                terrainRenderer?.Dispose();
                politicalRenderer?.Dispose();
                
                Console.WriteLine("=== Debug Test Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}