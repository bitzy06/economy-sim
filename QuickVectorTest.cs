using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim
{
    public static class QuickVectorTest
    {
        public static void TestTerrainClassification()
        {
            Console.WriteLine("=== Quick Terrain Classification Test ===");
            
            // Test the terrain classification logic directly
            var testCoords = new[]
            {
                (0.0, 0.0),       // Equator, Atlantic
                (-100.0, 40.0),   // Central USA 
                (2.3, 48.9),      // Paris
                (-74.0, 40.7),    // New York
                (139.7, 35.7),    // Tokyo
                (-180.0, 0.0),    // Pacific
                (180.0, 0.0),     // Pacific
                (30.0, 25.0),     // Sahara
                (0.0, 70.0),      // Arctic
            };
            
            foreach (var (lon, lat) in testCoords)
            {
                var isOcean = TestIsOceanArea(lon, lat);
                var terrain = TestClassifyTerrain(lon, lat);
                Console.WriteLine($"({lon:F1}, {lat:F1}): Ocean={isOcean}, Terrain={terrain}");
            }
            
            Console.WriteLine("\n=== Testing Simple Vector Tile Generation ===");
            
            // Create a simple test bitmap
            try
            {
                var renderer = new VectorTerrainTileRenderer(4096, 2048);
                
                // Test AssembleView for a small area
                var viewArea = new SKRectI(2000, 1000, 2512, 1512); // 512x512 area
                Console.WriteLine($"Testing view area: {viewArea}");
                
                var result = renderer.AssembleView(1, viewArea);
                
                if (result != null)
                {
                    Console.WriteLine($"Generated bitmap: {result.Width}x{result.Height}");
                    
                    // Check some pixel colors
                    var colorCounts = new Dictionary<uint, int>();
                    
                    for (int y = 0; y < result.Height; y += 10)
                    {
                        for (int x = 0; x < result.Width; x += 10)
                        {
                            var pixel = result.GetPixel(x, y);
                            uint colorKey = (uint)((pixel.Red << 16) | (pixel.Green << 8) | pixel.Blue);
                            if (colorCounts.ContainsKey(colorKey))
                                colorCounts[colorKey]++;
                            else
                                colorCounts[colorKey] = 1;
                        }
                    }
                    
                    Console.WriteLine($"Found {colorCounts.Count} unique colors:");
                    var sortedColors = colorCounts.OrderByDescending(kvp => kvp.Value).Take(10);
                    
                    foreach (var kvp in sortedColors)
                    {
                        uint color = kvp.Key;
                        int count = kvp.Value;
                        int r = (int)((color >> 16) & 0xFF);
                        int g = (int)((color >> 8) & 0xFF);
                        int b = (int)(color & 0xFF);
                        Console.WriteLine($"  #{r:X2}{g:X2}{b:X2}: {count} pixels");
                    }
                }
                else
                {
                    Console.WriteLine("AssembleView returned null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n=== Testing Vector Political Tile Generation ===");
            
            try
            {
                var politicalManager = new PoliticalBorderManager();
                var politicalRenderer = new VectorPoliticalTileRenderer(politicalManager, 4096, 2048);
                
                // Test AssembleView for political map
                var politicalArea = new SKRectI(2000, 1000, 2512, 1512); // 512x512 area
                Console.WriteLine($"Testing political view area: {politicalArea}");
                
                var politicalResult = politicalRenderer.AssembleView(1, politicalArea);
                
                if (politicalResult != null)
                {
                    Console.WriteLine($"Generated political bitmap: {politicalResult.Width}x{politicalResult.Height}");
                    
                    // Check political map colors
                    var politicalColorCounts = new Dictionary<uint, int>();
                    
                    for (int y = 0; y < politicalResult.Height; y += 10)
                    {
                        for (int x = 0; x < politicalResult.Width; x += 10)
                        {
                            var pixel = politicalResult.GetPixel(x, y);
                            uint colorKey = (uint)((pixel.Red << 16) | (pixel.Green << 8) | pixel.Blue);
                            if (politicalColorCounts.ContainsKey(colorKey))
                                politicalColorCounts[colorKey]++;
                            else
                                politicalColorCounts[colorKey] = 1;
                        }
                    }
                    
                    Console.WriteLine($"Found {politicalColorCounts.Count} unique political colors:");
                    var sortedPoliticalColors = politicalColorCounts.OrderByDescending(kvp => kvp.Value).Take(10);
                    
                    foreach (var kvp in sortedPoliticalColors)
                    {
                        uint color = kvp.Key;
                        int count = kvp.Value;
                        int r = (int)((color >> 16) & 0xFF);
                        int g = (int)((color >> 8) & 0xFF);
                        int b = (int)(color & 0xFF);
                        Console.WriteLine($"  #{r:X2}{g:X2}{b:X2}: {count} pixels");
                    }
                }
                else
                {
                    Console.WriteLine("Political AssembleView returned null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during political test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static bool TestIsOceanArea(double longitude, double latitude)
        {
            // Copy of the fixed logic
            if (longitude > 160 || longitude < -150)
            {
                if (Math.Abs(latitude) < 50)
                    return true;
            }
            
            if (longitude > -35 && longitude < -15 && Math.Abs(latitude) < 50)
                return true;
            
            if (Math.Abs(latitude) > 85)
                return true;
                
            return false;
        }
        
        private static string TestClassifyTerrain(double longitude, double latitude)
        {
            double absLat = Math.Abs(latitude);
            double noise = Math.Sin(longitude * 0.1) * Math.Cos(latitude * 0.1);
            double elevation = Math.Sin(longitude * 0.05) * Math.Cos(latitude * 0.05);
            
            if (TestIsOceanArea(longitude, latitude))
                return "Water";
            
            if (absLat > 75 || (absLat > 65 && elevation > 0.6))
                return "Ice";
            
            if (absLat > 60)
                return "Tundra";
            
            if (elevation > 0.7)
                return "Mountain";
            
            if (absLat < 35 && elevation < -0.3)
                return "Desert";
            
            if (absLat < 60 && noise > 0.2 && elevation > 0.1)
                return "Forest";
            
            return "Plains";
        }
    }
}