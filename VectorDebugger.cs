using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Debug class to investigate why vector graphics fall back to procedural patterns
    /// </summary>
    public static class VectorDebugger
    {
        public static void DebugDataIntegration()
        {
            Console.WriteLine("=== Vector Data Integration Debug ===");
            
            // Check GDAL and shapefile access with detailed logging
            TestGDALAccess();
            TestShapefileAccess();
            TestTerrainGeneration();
            TestPoliticalGeneration();
            
            Console.WriteLine("=== Debug Complete ===");
        }
        
        private static void TestGDALAccess()
        {
            Console.WriteLine("\n--- Testing GDAL Access ---");
            
            try
            {
                // Initialize GDAL
                MaxRev.Gdal.Core.GdalBase.ConfigureAll();
                Console.WriteLine("✓ GDAL configured successfully");
                
                // Try to open the terrain file
                string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                string terrainPath = Path.Combine(repoRoot, "data", "terrain", "NE1_HR_LC.tif");
                
                Console.WriteLine($"Attempting to open: {terrainPath}");
                Console.WriteLine($"File exists: {File.Exists(terrainPath)}");
                
                if (File.Exists(terrainPath))
                {
                    using var ds = OSGeo.GDAL.Gdal.Open(terrainPath, OSGeo.GDAL.Access.GA_ReadOnly);
                    if (ds == null)
                    {
                        Console.WriteLine("✗ Failed to open terrain file with GDAL");
                    }
                    else
                    {
                        Console.WriteLine($"✓ GDAL opened terrain file successfully");
                        Console.WriteLine($"  Raster size: {ds.RasterXSize}x{ds.RasterYSize}");
                        Console.WriteLine($"  Bands: {ds.RasterCount}");
                        
                        // Try to read a small sample
                        if (ds.RasterCount >= 3)
                        {
                            byte[] r = new byte[10 * 10];
                            ds.GetRasterBand(1).ReadRaster(0, 0, 10, 10, r, 10, 10, 0, 0);
                            Console.WriteLine($"  Sample pixel values: {r[0]}, {r[1]}, {r[2]}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ GDAL test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestShapefileAccess()
        {
            Console.WriteLine("\n--- Testing Shapefile Access ---");
            
            try
            {
                string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                string shapePath = Path.Combine(repoRoot, "data", "terrain", "ne_10m_admin_0_countries.shp");
                
                Console.WriteLine($"Attempting to open: {shapePath}");
                Console.WriteLine($"File exists: {File.Exists(shapePath)}");
                
                if (File.Exists(shapePath))
                {
                    var factory = new NetTopologySuite.Geometries.GeometryFactory();
                    var reader = new NetTopologySuite.IO.ShapefileDataReader(shapePath, factory);
                    
                    Console.WriteLine("✓ Shapefile opened successfully");
                    Console.WriteLine($"  Header: {reader.DbaseHeader.NumRecords} records, {reader.DbaseHeader.NumFields} fields");
                    
                    // Read first few records
                    int count = 0;
                    while (reader.Read() && count < 3)
                    {
                        var geom = reader.Geometry;
                        Console.WriteLine($"  Country {count + 1}: {geom?.GeometryType}, Area: {geom?.Area}");
                        
                        // Try to get country name
                        for (int i = 0; i < reader.DbaseHeader.NumFields; i++)
                        {
                            var field = reader.DbaseHeader.Fields[i];
                            if (field.Name.Equals("NAME", StringComparison.OrdinalIgnoreCase))
                            {
                                var value = reader.GetValue(i);
                                Console.WriteLine($"    Name: {value}");
                                break;
                            }
                        }
                        count++;
                    }
                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Shapefile test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestTerrainGeneration()
        {
            Console.WriteLine("\n--- Testing Terrain Generation Debug ---");
            
            try
            {
                var terrainRenderer = new VectorTerrainTileRenderer(4096, 2048);
                
                // We need to access the protected method, so let's test the public interface
                var mapManager = new VectorHybridMapManager(4096, 2048);
                mapManager.SetViewType(MapViewType.Terrain);
                
                Console.WriteLine("Testing terrain generation...");
                var viewArea = new SKRectI(1000, 1000, 100, 100);  // Small area over land
                var bitmap = mapManager.AssembleView(1, viewArea);
                
                if (bitmap != null)
                {
                    // Analyze the colors in detail
                    var colorCounts = new Dictionary<uint, int>();
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            var color = (uint)bitmap.GetPixel(x, y);
                            colorCounts[color] = colorCounts.GetValueOrDefault(color, 0) + 1;
                        }
                    }
                    
                    Console.WriteLine($"  Total unique colors: {colorCounts.Count}");
                    var sortedColors = colorCounts.OrderByDescending(kv => kv.Value).Take(5);
                    foreach (var kv in sortedColors)
                    {
                        var color = new SKColor(kv.Key);
                        Console.WriteLine($"    Color: R={color.Red}, G={color.Green}, B={color.Blue}, A={color.Alpha}, Count={kv.Value}");
                    }
                    
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Terrain generation debug failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static void TestPoliticalGeneration()
        {
            Console.WriteLine("\n--- Testing Political Generation Debug ---");
            
            try
            {
                var mapManager = new VectorHybridMapManager(4096, 2048);
                mapManager.SetViewType(MapViewType.Political);
                
                Console.WriteLine("Testing political generation...");
                var viewArea = new SKRectI(1000, 1000, 100, 100);  // Small area over land
                var bitmap = mapManager.AssembleView(1, viewArea);
                
                if (bitmap != null)
                {
                    // Analyze the colors in detail
                    var colorCounts = new Dictionary<uint, int>();
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            var color = (uint)bitmap.GetPixel(x, y);
                            colorCounts[color] = colorCounts.GetValueOrDefault(color, 0) + 1;
                        }
                    }
                    
                    Console.WriteLine($"  Total unique colors: {colorCounts.Count}");
                    var sortedColors = colorCounts.OrderByDescending(kv => kv.Value).Take(5);
                    foreach (var kv in sortedColors)
                    {
                        var color = new SKColor(kv.Key);
                        Console.WriteLine($"    Color: R={color.Red}, G={color.Green}, B={color.Blue}, A={color.Alpha}, Count={kv.Value}");
                    }
                    
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Political generation debug failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}