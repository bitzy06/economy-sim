using System;
using System.Diagnostics;
using StrategyGame;

public class CoordinateValidation
{
    public static void ValidateCoordinateUnification()
    {
        Console.WriteLine("=== Coordinate System Validation ===");
        
        // Test parameters matching the application
        int baseWidth = 4096;
        int baseHeight = 2048;
        int tileSizePx = 512;
        
        Console.WriteLine($"Base dimensions: {baseWidth}x{baseHeight}");
        Console.WriteLine($"Tile size: {tileSizePx}px");
        
        // Test multiple tiles to ensure consistency
        for (int tileX = 0; tileX < 3; tileX++)
        {
            for (int tileY = 0; tileY < 2; tileY++)
            {
                var bounds = CoordinateTransform.GetTileGeographicBounds(
                    tileX, tileY, tileSizePx, baseWidth, baseHeight);
                    
                Console.WriteLine($"Tile ({tileX},{tileY}): {bounds}");
                
                // Validate bounds
                if (!CoordinateTransform.IsValidGeoBounds(bounds))
                {
                    Console.WriteLine($"❌ Invalid bounds for tile ({tileX},{tileY})");
                    return;
                }
                
                // Check that adjacent tiles have adjoining bounds
                if (tileX > 0)
                {
                    var leftBounds = CoordinateTransform.GetTileGeographicBounds(
                        tileX - 1, tileY, tileSizePx, baseWidth, baseHeight);
                    
                    double tolerance = 0.001; // Small tolerance for floating point
                    if (Math.Abs(leftBounds.MaxLon - bounds.MinLon) > tolerance)
                    {
                        Console.WriteLine($"❌ Horizontal gap between tiles ({tileX-1},{tileY}) and ({tileX},{tileY})");
                        Console.WriteLine($"   Left tile MaxLon: {leftBounds.MaxLon:F6}, Current tile MinLon: {bounds.MinLon:F6}");
                        return;
                    }
                }
                
                if (tileY > 0)
                {
                    var topBounds = CoordinateTransform.GetTileGeographicBounds(
                        tileX, tileY - 1, tileSizePx, baseWidth, baseHeight);
                    
                    double tolerance = 0.001;
                    if (Math.Abs(topBounds.MinLat - bounds.MaxLat) > tolerance)
                    {
                        Console.WriteLine($"❌ Vertical gap between tiles ({tileX},{tileY-1}) and ({tileX},{tileY})");
                        Console.WriteLine($"   Top tile MinLat: {topBounds.MinLat:F6}, Current tile MaxLat: {bounds.MaxLat:F6}");
                        return;
                    }
                }
            }
        }
        
        Console.WriteLine("✅ All tile boundaries are consistent");
        
        // Test pixel conversion round-trip
        TestPixelConversion(baseWidth, baseHeight);
        
        Console.WriteLine("✅ Coordinate system validation passed");
    }
    
    static void TestPixelConversion(int baseWidth, int baseHeight)
    {
        // Test some key points
        var testPoints = new[]
        {
            (0, 0, -180.0, 90.0),           // Top-left corner
            (baseWidth, baseHeight, 180.0, -90.0),  // Bottom-right corner
            (baseWidth/2, baseHeight/2, 0.0, 0.0),  // Center
        };
        
        foreach (var (pixelX, pixelY, expectedLon, expectedLat) in testPoints)
        {
            var (actualLon, actualLat) = CoordinateTransform.PixelToGeographic(
                pixelX, pixelY, baseWidth, baseHeight);
            
            double tolerance = 1.0; // 1 degree tolerance for edge cases
            if (Math.Abs(actualLon - expectedLon) > tolerance || 
                Math.Abs(actualLat - expectedLat) > tolerance)
            {
                Console.WriteLine($"❌ Pixel conversion mismatch for ({pixelX},{pixelY}):");
                Console.WriteLine($"   Expected: ({expectedLon:F2},{expectedLat:F2})");
                Console.WriteLine($"   Actual: ({actualLon:F2},{actualLat:F2})");
                return;
            }
        }
        
        Console.WriteLine("✅ Pixel-to-geographic conversion tests passed");
    }
}