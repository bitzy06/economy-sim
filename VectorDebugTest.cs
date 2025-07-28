using System;
using System.Diagnostics;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim
{
    public static class VectorDebugTest
    {
        public static void TestTerrainGeneration()
        {
            Console.WriteLine("=== Vector Terrain Debug Test ===");
            Debug.WriteLine("=== Vector Terrain Debug Test ===");
            
            var vectorManager = new VectorHybridMapManager(baseWidth: 4096, baseHeight: 2048);
            
            // Test a single tile generation
            var viewArea = new SKRectI(2048, 1024, 2048 + 512, 1024 + 512); // Center tile
            
            Debug.WriteLine($"Testing terrain generation for center tile at {viewArea}");
            
            var result = vectorManager.AssembleView(1, viewArea);
            
            if (result != null)
            {
                Debug.WriteLine($"Generated bitmap: {result.Width}x{result.Height}");
                
                // Sample pixels to see what colors we get
                var colors = new System.Collections.Generic.HashSet<SKColor>();
                for (int y = 50; y < result.Height - 50; y += 50)
                {
                    for (int x = 50; x < result.Width - 50; x += 50)
                    {
                        var pixel = result.GetPixel(x, y);
                        colors.Add(pixel);
                    }
                }
                
                Debug.WriteLine($"Found {colors.Count} unique colors:");
                foreach (var color in colors)
                {
                    Debug.WriteLine($"  Color: #{color.Red:X2}{color.Green:X2}{color.Blue:X2}{color.Alpha:X2}");
                }
                
                // Test specific coordinates
                TestTerrainClassification();
            }
            else
            {
                Debug.WriteLine("AssembleView returned null bitmap");
            }
        }
        
        private static void TestTerrainClassification()
        {
            Debug.WriteLine("=== Testing terrain classification for specific coordinates ===");
            
            var renderer = new VectorTerrainTileRenderer(4096, 2048);
            
            // Test coordinates around the world
            var testCoords = new[]
            {
                (-74.0, 40.7),    // New York (should be land)
                (2.3, 48.9),      // Paris (should be land)
                (139.7, 35.7),    // Tokyo (should be land)
                (-180.0, 0.0),    // Pacific Ocean (should be water)
                (0.0, 0.0),       // Atlantic Ocean (should be water)
                (120.0, 0.0),     // Pacific Ocean (should be water)
                (-100.0, 40.0),   // Central USA (should be land - plains)
                (20.0, 65.0),     // Northern Europe (should be land - tundra)
                (30.0, 25.0),     // Sahara (should be desert)
                (-122.4, 37.8),   // San Francisco (should be land)
            };
            
            foreach (var (lon, lat) in testCoords)
            {
                try
                {
                    // We need to access the private method, so we'll create a test version
                    var isOcean = TestIsOceanArea(lon, lat);
                    var terrain = TestClassifyTerrain(lon, lat);
                    Debug.WriteLine($"Coord ({lon:F1}, {lat:F1}): Ocean={isOcean}, Terrain={terrain}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error testing coord ({lon:F1}, {lat:F1}): {ex.Message}");
                }
            }
        }
        
        // Duplicate the logic from VectorTerrainTileRenderer to test it directly
        private static bool TestIsOceanArea(double longitude, double latitude)
        {
            // Simplified ocean detection based on major ocean areas
            // Pacific Ocean
            if ((longitude < -120 || longitude > 120) && Math.Abs(latitude) < 65)
                return true;
            
            // Atlantic Ocean (between Americas and Europe/Africa)
            if (longitude > -80 && longitude < -10 && Math.Abs(latitude) < 65)
                return true;
            
            // Indian Ocean
            if (longitude > 30 && longitude < 120 && latitude < 30 && latitude > -50)
                return true;
            
            // Arctic Ocean
            if (Math.Abs(latitude) > 75)
                return true;
                
            return false;
        }
        
        private static string TestClassifyTerrain(double longitude, double latitude)
        {
            // Use absolute latitude for climate zones
            double absLat = Math.Abs(latitude);
            
            // Use longitude and latitude to create noise for variety
            double noise = SimplexNoise(longitude * 0.1, latitude * 0.1);
            double elevation = SimplexNoise(longitude * 0.05, latitude * 0.05);
            
            // Water bodies (simplified)
            if (TestIsOceanArea(longitude, latitude))
            {
                return "Water";
            }
            
            // Ice caps (high latitudes)
            if (absLat > 75 || (absLat > 65 && elevation > 0.6))
            {
                return "Ice";
            }
            
            // Tundra (high latitudes, not ice)
            if (absLat > 60)
            {
                return "Tundra";
            }
            
            // Mountains (high elevation with noise)
            if (elevation > 0.7)
            {
                return "Mountain";
            }
            
            // Desert (specific longitude bands and low latitudes)
            if ((absLat < 35 && (TestIsDesertRegion(longitude, latitude) || elevation < -0.3)))
            {
                return "Desert";
            }
            
            // Forest (temperate and tropical regions with good conditions)
            if (absLat < 60 && noise > 0.2 && elevation > 0.1)
            {
                return "Forest";
            }
            
            // Default to plains
            return "Plains";
        }
        
        private static bool TestIsDesertRegion(double longitude, double latitude)
        {
            // Sahara, Middle East, Central Asia
            if (longitude > -10 && longitude < 60 && latitude > 15 && latitude < 40)
                return true;
            
            // Australian deserts
            if (longitude > 110 && longitude < 155 && latitude > -35 && latitude < -15)
                return true;
            
            // Southwest USA, Northern Mexico
            if (longitude > -125 && longitude < -100 && latitude > 25 && latitude < 40)
                return true;
            
            // Patagonia
            if (longitude > -75 && longitude < -60 && latitude > -50 && latitude < -35)
                return true;
                
            return false;
        }
        
        private static double SimplexNoise(double x, double y)
        {
            // Simple noise function for terrain variation
            double value = 0.0;
            value += Math.Sin(x * 2.1) * Math.Cos(y * 1.7) * 0.5;
            value += Math.Sin(x * 0.8) * Math.Cos(y * 2.3) * 0.3;
            value += Math.Sin(x * 4.2) * Math.Cos(y * 3.9) * 0.2;
            return Math.Clamp(value, -1.0, 1.0);
        }
    }
}