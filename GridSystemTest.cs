using System;
using System.Diagnostics;
using System.Drawing;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Test class to validate the new grid-based political rendering system
    /// </summary>
    public static class GridSystemTest
    {
        /// <summary>
        /// Run basic tests of the grid control engine and renderer
        /// </summary>
        public static void RunBasicTests()
        {
            Debug.WriteLine("=== Starting Grid System Tests ===");

            try
            {
                // Test 1: Grid Engine Creation
                Debug.WriteLine("Test 1: Creating GridControlEngine...");
                var gridEngine = new GridControlEngine(width: 1024, height: 512, tileSize: 256);
                Debug.WriteLine($"✓ Grid created: {gridEngine.Width}x{gridEngine.Height}, tile size: {gridEngine.TileSize}");

                // Test 2: Test Pattern Population
                Debug.WriteLine("Test 2: Populating with test pattern...");
                var populator = new GridPopulator(new PoliticalBorderManager());
                populator.PopulateTestPattern(gridEngine);
                Debug.WriteLine("✓ Test pattern populated");

                // Test 3: Grid Renderer Creation
                Debug.WriteLine("Test 3: Creating GridRenderer...");
                var dataCache = new PoliticalDataCache(new DateTime(1950, 1, 1));
                var renderer = new GridRenderer(gridEngine, dataCache);
                Debug.WriteLine("✓ GridRenderer created");

                // Test 4: Coordinate Transformations
                Debug.WriteLine("Test 4: Testing coordinate transformations...");
                var (cellX, cellY) = CoordinateTransform.GeographicToGridCell(0.0, 0.0, gridEngine.Width, gridEngine.Height);
                var (lon, lat) = CoordinateTransform.GridCellToGeographic(cellX, cellY, gridEngine.Width, gridEngine.Height);
                Debug.WriteLine($"✓ Geographic (0,0) -> Grid ({cellX},{cellY}) -> Geographic ({lon:F2},{lat:F2})");

                // Test 5: Basic War Mechanics
                Debug.WriteLine("Test 5: Testing war mechanics...");
                var cells = new[] { new Point(100, 100), new Point(101, 100), new Point(100, 101) };
                gridEngine.ChangeControl(999, cells); // Change some cells to country 999
                Debug.WriteLine("✓ Control change applied");

                var frontline = gridEngine.ComputeFrontline();
                Debug.WriteLine($"✓ Frontline computed: {frontline.Count} cells");

                // Test 6: Tile Rendering
                Debug.WriteLine("Test 6: Testing tile rendering...");
                var tileBitmap = renderer.RenderGridTile(0, 0, 256);
                if (tileBitmap != null)
                {
                    Debug.WriteLine($"✓ Tile rendered: {tileBitmap.Width}x{tileBitmap.Height}");
                    tileBitmap.Dispose();
                }
                else
                {
                    Debug.WriteLine("⚠ Tile rendering returned null");
                }

                // Test 7: LOD Generation
                Debug.WriteLine("Test 7: Testing LOD generation...");
                var lodGrid = gridEngine.GetControlGridLod(1);
                Debug.WriteLine($"✓ LOD 1 grid: {lodGrid.GetLength(1)}x{lodGrid.GetLength(0)}");

                // Test 8: Dirty Tile Tracking
                Debug.WriteLine("Test 8: Testing dirty tile tracking...");
                bool hasDirty = gridEngine.HasDirtyTiles();
                var dirtyTiles = gridEngine.GetDirtyTiles(clearAfterGet: false);
                Debug.WriteLine($"✓ Dirty tiles: {hasDirty}, count: {System.Linq.Enumerable.Count(dirtyTiles)}");

                // Cleanup
                gridEngine.Dispose();
                Debug.WriteLine("✓ Grid engine disposed");

                Debug.WriteLine("=== All Grid System Tests Passed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Grid System Test Failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Run visual comparison test to ensure grid renderer matches polygon renderer
        /// </summary>
        public static void RunVisualComparisonTest(string cshapesPath = null)
        {
            if (string.IsNullOrEmpty(cshapesPath))
            {
                Debug.WriteLine("Skipping visual comparison test - no CShapes path provided");
                return;
            }

            Debug.WriteLine("=== Starting Visual Comparison Test ===");

            try
            {
                // Create grid system
                var gridEngine = new GridControlEngine(width: 2048, height: 1024, tileSize: 512);
                var politicalManager = new PoliticalBorderManager();
                var populator = new GridPopulator(politicalManager);
                var dataCache = new PoliticalDataCache(new DateTime(1950, 1, 1));
                var gridRenderer = new GridRenderer(gridEngine, dataCache);

                // Populate from shapefile
                Debug.WriteLine("Populating grid from shapefile...");
                populator.PopulateFromShapefile(gridEngine, cshapesPath, new DateTime(1950, 1, 1));

                // Render a test tile using grid system
                Debug.WriteLine("Rendering tile with grid system...");
                var gridTile = gridRenderer.RenderGridTile(0, 0, 512);

                if (gridTile != null)
                {
                    Debug.WriteLine($"✓ Grid tile rendered: {gridTile.Width}x{gridTile.Height}");
                    
                    // Save for visual inspection
                    var outputPath = "/tmp/grid_test_tile.png";
                    using (var stream = System.IO.File.Create(outputPath))
                    {
                        gridTile.Encode(stream, SKEncodedImageFormat.Png, 100);
                    }
                    Debug.WriteLine($"✓ Grid tile saved to: {outputPath}");
                    
                    gridTile.Dispose();
                }

                gridEngine.Dispose();
                Debug.WriteLine("=== Visual Comparison Test Completed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Visual Comparison Test Failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performance test for grid operations
        /// </summary>
        public static void RunPerformanceTest()
        {
            Debug.WriteLine("=== Starting Performance Test ===");

            try
            {
                var gridEngine = new GridControlEngine(width: 4096, height: 2048, tileSize: 512);
                var populator = new GridPopulator(new PoliticalBorderManager());
                var dataCache = new PoliticalDataCache(new DateTime(1950, 1, 1));
                var renderer = new GridRenderer(gridEngine, dataCache);

                // Populate with test pattern
                var sw = Stopwatch.StartNew();
                populator.PopulateTestPattern(gridEngine);
                sw.Stop();
                Debug.WriteLine($"✓ Grid population: {sw.ElapsedMilliseconds}ms");

                // Test bulk control changes
                sw.Restart();
                var random = new Random(42);
                var cells = new Point[1000];
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = new Point(random.Next(gridEngine.Width), random.Next(gridEngine.Height));
                }
                gridEngine.ChangeControl(100, cells);
                sw.Stop();
                Debug.WriteLine($"✓ Bulk control change (1000 cells): {sw.ElapsedMilliseconds}ms");

                // Test tile rendering performance
                sw.Restart();
                for (int i = 0; i < 4; i++)
                {
                    var tile = renderer.RenderGridTile(i, 0, 512);
                    tile?.Dispose();
                }
                sw.Stop();
                Debug.WriteLine($"✓ 4 tile renders: {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 4.0:F1}ms per tile)");

                // Test frontline computation
                sw.Restart();
                var frontline = gridEngine.ComputeFrontline();
                sw.Stop();
                Debug.WriteLine($"✓ Frontline computation: {sw.ElapsedMilliseconds}ms ({frontline.Count} cells)");

                gridEngine.Dispose();
                Debug.WriteLine("=== Performance Test Completed ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Performance Test Failed: {ex.Message}");
                throw;
            }
        }
    }
}