using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Comprehensive test using LFS data to verify vector map generation and produce visual output
    /// </summary>
    public static class VectorLFSTest
    {
        public static void RunLFSDataTest()
        {
            Console.WriteLine("=== Vector LFS Data Test ===");
            
            // Create output directory for test images
            string outputDir = Path.Combine("/tmp", "vector_test_output");
            Directory.CreateDirectory(outputDir);
            
            try
            {
                // Test terrain rendering with LFS data
                TestTerrainWithLFSData(outputDir);
                
                // Test political rendering with LFS data
                TestPoliticalWithLFSData(outputDir);
                
                // Test vector tile cache and performance
                TestVectorPerformance(outputDir);
                
                Console.WriteLine($"Test images saved to: {outputDir}");
                Console.WriteLine("=== LFS Data Test Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LFS data test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestTerrainWithLFSData(string outputDir)
        {
            Console.WriteLine("\n--- Testing Terrain with LFS Data ---");
            
            try
            {
                // Initialize terrain renderer
                var terrainRenderer = new VectorTerrainTileRenderer(1024, 1024);
                
                // Test terrain rendering with LFS data using coordinates from GUI debug log
                var testCoordinates = new[]
                {
                    (10, 3),   // From GUI debug: ViewArea={Left=5175,Top=2004...} -> tile (10,3)
                    (11, 4),   // Adjacent tiles
                    (9, 3),    
                    (10, 4),   
                    (15, 5),   // Further out
                };
                
                int tileIndex = 0;
                foreach (var (tileX, tileY) in testCoordinates)
                {
                    Console.WriteLine($"Generating terrain tile ({tileX}, {tileY})...");
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    // Generate vector tile using actual LFS data
                    var vectorTile = terrainRenderer.GetVectorTileForTesting(tileX, tileY, 1);
                    if (vectorTile == null)
                    {
                        Console.WriteLine($"  WARNING: Vector tile ({tileX}, {tileY}) is null");
                        continue;
                    }
                    
                    // Render to bitmap
                    using var bitmap = new SKBitmap(512, 512);
                    using var canvas = new SKCanvas(bitmap);
                    
                    // Clear with background color
                    canvas.Clear(new SKColor(200, 200, 200)); // Light gray background
                    
                    // Render the vector tile
                    terrainRenderer.RenderVectorTileForTesting(vectorTile, canvas, 512, 512);
                    
                    stopwatch.Stop();
                    
                    // Save the bitmap
                    string filename = Path.Combine(outputDir, $"terrain_tile_{tileX}_{tileY}.png");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = File.OpenWrite(filename);
                    data.SaveTo(stream);
                    
                    // Analyze pixel variation
                    var pixelStats = AnalyzePixelVariation(bitmap);
                    Console.WriteLine($"  Tile ({tileX}, {tileY}): {stopwatch.ElapsedMilliseconds}ms, " +
                                    $"Colors: {pixelStats.UniqueColors}, Variation: {pixelStats.HasVariation}");
                    Console.WriteLine($"  Saved: {filename}");
                    
                    tileIndex++;
                }
                
                terrainRenderer.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in terrain test: {ex.Message}");
            }
        }
        
        private static void TestPoliticalWithLFSData(string outputDir)
        {
            Console.WriteLine("\n--- Testing Political with LFS Data ---");
            
            try
            {
                // Initialize political renderer
                var politicalManager = new PoliticalBorderManager();
                var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 1024, 1024);
                
                // Test multiple tile coordinates for GUI coordinate range
                var testCoordinates = new[]
                {
                    (10, 3),   // From GUI debug: ViewArea={Left=5175,Top=2004...} -> tile (10,3)
                    (11, 4),   // Adjacent tiles
                    (9, 3),    
                    (10, 4),   
                    (15, 5),   // Further out
                };
                
                foreach (var (tileX, tileY) in testCoordinates)
                {
                    Console.WriteLine($"Generating political tile ({tileX}, {tileY})...");
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    // Generate vector tile using actual LFS data
                    var vectorTile = politicalRenderer.GetVectorTileForTesting(tileX, tileY, 1);
                    if (vectorTile == null)
                    {
                        Console.WriteLine($"  WARNING: Political vector tile ({tileX}, {tileY}) is null");
                        continue;
                    }
                    
                    // Render to bitmap
                    using var bitmap = new SKBitmap(512, 512);
                    using var canvas = new SKCanvas(bitmap);
                    
                    // Clear with background color
                    canvas.Clear(new SKColor(240, 240, 240)); // Light gray background
                    
                    // Render the vector tile
                    politicalRenderer.RenderVectorTileForTesting(vectorTile, canvas, 512, 512);
                    
                    stopwatch.Stop();
                    
                    // Save the bitmap
                    string filename = Path.Combine(outputDir, $"political_tile_{tileX}_{tileY}.png");
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var stream = File.OpenWrite(filename);
                    data.SaveTo(stream);
                    
                    // Analyze pixel variation
                    var pixelStats = AnalyzePixelVariation(bitmap);
                    Console.WriteLine($"  Tile ({tileX}, {tileY}): {stopwatch.ElapsedMilliseconds}ms, " +
                                    $"Colors: {pixelStats.UniqueColors}, Variation: {pixelStats.HasVariation}");
                    Console.WriteLine($"  Saved: {filename}");
                }
                
                politicalRenderer.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in political test: {ex.Message}");
            }
        }
        
        private static void TestVectorPerformance(string outputDir)
        {
            Console.WriteLine("\n--- Testing Vector Performance ---");
            
            try
            {
                var terrainRenderer = new VectorTerrainTileRenderer(1024, 1024);
                var politicalManager = new PoliticalBorderManager();
                var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 1024, 1024);
                
                var times = new List<long>();
                
                // Generate multiple tiles to test performance and caching
                for (int i = 0; i < 5; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    // Generate same tile multiple times to test caching - use GUI coordinates
                    var terrainTile = terrainRenderer.GetVectorTileForTesting(10, 3, 1);
                    var politicalTile = politicalRenderer.GetVectorTileForTesting(10, 3, 1);
                    
                    // Render combined view
                    using var bitmap = new SKBitmap(512, 512);
                    using var canvas = new SKCanvas(bitmap);
                    
                    canvas.Clear(SKColors.White);
                    terrainRenderer.RenderVectorTileForTesting(terrainTile, canvas, 512, 512);
                    politicalRenderer.RenderVectorTileForTesting(politicalTile, canvas, 512, 512);
                    
                    stopwatch.Stop();
                    times.Add(stopwatch.ElapsedMilliseconds);
                    
                    if (i == 0) // Save first iteration
                    {
                        string filename = Path.Combine(outputDir, "combined_terrain_political.png");
                        using var image = SKImage.FromBitmap(bitmap);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var stream = File.OpenWrite(filename);
                        data.SaveTo(stream);
                        Console.WriteLine($"  Combined view saved: {filename}");
                    }
                }
                
                Console.WriteLine($"Performance: {times.Average():F1}ms average ({times.Min()}-{times.Max()}ms range)");
                
                terrainRenderer.Dispose();
                politicalRenderer.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in performance test: {ex.Message}");
            }
        }
        
        private static PixelAnalysis AnalyzePixelVariation(SKBitmap bitmap)
        {
            var colors = new HashSet<uint>();
            
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    colors.Add((uint)((pixel.Alpha << 24) | (pixel.Red << 16) | (pixel.Green << 8) | pixel.Blue));
                    
                    // Stop early if we find enough variation
                    if (colors.Count > 10) break;
                }
                if (colors.Count > 10) break;
            }
            
            return new PixelAnalysis
            {
                UniqueColors = colors.Count,
                HasVariation = colors.Count > 3 // More than background + 2 other colors
            };
        }
        
        private struct PixelAnalysis
        {
            public int UniqueColors;
            public bool HasVariation;
        }
    }
}