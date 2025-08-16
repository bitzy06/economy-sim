using System;
using System.Diagnostics;
using System.Drawing;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace Economy_sim
{
    /// <summary>
    /// Populates the grid control engine from existing shapefile data.
    /// Bridges the polygon-based system with the new grid-based system.
    /// </summary>
    public class GridPopulator
    {
        private static readonly object GdalLock = new object();
        private readonly PoliticalBorderManager _politicalManager;

        public GridPopulator(PoliticalBorderManager politicalManager)
        {
            _politicalManager = politicalManager ?? throw new ArgumentNullException(nameof(politicalManager));
        }

        /// <summary>
        /// Populate grid from CShapes shapefile data using existing PoliticalBorderManager logic
        /// </summary>
        public void PopulateFromShapefile(GridControlEngine gridEngine, string cshapesPath, DateTime targetDate)
        {
            Debug.WriteLine($"Populating grid ({gridEngine.Width}x{gridEngine.Height}) from shapefile for date {targetDate:yyyy-MM-dd}");

            try
            {
                // Use existing PoliticalBorderManager to create a mask at grid resolution
                double[] bounds = { -180.0, -90.0, 180.0, 90.0 }; // Global bounds
                
                var mask = _politicalManager.CreatePoliticalMask(cshapesPath, targetDate, 
                    gridEngine.Width, gridEngine.Height, bounds);

                if (mask == null)
                {
                    throw new ApplicationException("Failed to create political mask from shapefile");
                }

                Debug.WriteLine($"Created political mask with dimensions {mask.GetLength(1)}x{mask.GetLength(0)}");

                // Verify dimensions match
                if (mask.GetLength(0) != gridEngine.Height || mask.GetLength(1) != gridEngine.Width)
                {
                    throw new ApplicationException($"Mask dimensions ({mask.GetLength(1)}x{mask.GetLength(0)}) " +
                        $"don't match grid dimensions ({gridEngine.Width}x{gridEngine.Height})");
                }

                // Initialize the grid with the mask data
                gridEngine.InitializeBaseGrid(mask);

                Debug.WriteLine($"Successfully populated grid with {CountUniqueCountries(mask)} unique countries");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating grid from shapefile: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Populate grid using supersampling for better coastal accuracy
        /// </summary>
        public void PopulateFromShapefileWithSupersampling(GridControlEngine gridEngine, string cshapesPath, 
            DateTime targetDate, int supersampleFactor = 2)
        {
            Debug.WriteLine($"Populating grid with {supersampleFactor}x supersampling");

            try
            {
                // Create high-resolution mask
                int highResWidth = gridEngine.Width * supersampleFactor;
                int highResHeight = gridEngine.Height * supersampleFactor;
                
                double[] bounds = { -180.0, -90.0, 180.0, 90.0 };
                
                var highResMask = _politicalManager.CreatePoliticalMask(cshapesPath, targetDate,
                    highResWidth, highResHeight, bounds);

                if (highResMask == null)
                {
                    throw new ApplicationException("Failed to create high-resolution political mask");
                }

                Debug.WriteLine($"Created high-res mask {highResWidth}x{highResHeight}");

                // Downsample using majority vote per grid cell
                var gridMask = new int[gridEngine.Height, gridEngine.Width];
                
                for (int gridY = 0; gridY < gridEngine.Height; gridY++)
                {
                    for (int gridX = 0; gridX < gridEngine.Width; gridX++)
                    {
                        gridMask[gridY, gridX] = GetMajorityValue(highResMask, 
                            gridX * supersampleFactor, gridY * supersampleFactor, 
                            supersampleFactor, supersampleFactor);
                    }
                }

                // Initialize the grid
                gridEngine.InitializeBaseGrid(gridMask);

                Debug.WriteLine($"Successfully populated grid with supersampling");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating grid with supersampling: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Quick population for testing - creates a simple test pattern
        /// </summary>
        public void PopulateTestPattern(GridControlEngine gridEngine)
        {
            Debug.WriteLine("Populating grid with test pattern");

            var testGrid = new int[gridEngine.Height, gridEngine.Width];

            // Create some test countries
            for (int y = 0; y < gridEngine.Height; y++)
            {
                for (int x = 0; x < gridEngine.Width; x++)
                {
                    // Water in top/bottom 10%
                    if (y < gridEngine.Height * 0.1 || y >= gridEngine.Height * 0.9)
                    {
                        testGrid[y, x] = 0; // Water
                    }
                    // Left/right water edges
                    else if (x < gridEngine.Width * 0.05 || x >= gridEngine.Width * 0.95)
                    {
                        testGrid[y, x] = 0; // Water
                    }
                    // Create horizontal bands for different countries
                    else if (y < gridEngine.Height * 0.3)
                    {
                        testGrid[y, x] = 1; // Country 1
                    }
                    else if (y < gridEngine.Height * 0.5)
                    {
                        testGrid[y, x] = 2; // Country 2
                    }
                    else if (y < gridEngine.Height * 0.7)
                    {
                        testGrid[y, x] = 3; // Country 3
                    }
                    else
                    {
                        testGrid[y, x] = 4; // Country 4
                    }
                }
            }

            gridEngine.InitializeBaseGrid(testGrid);
            Debug.WriteLine("Test pattern populated successfully");
        }

        /// <summary>
        /// Get the majority value in a rectangular region of the mask
        /// </summary>
        private int GetMajorityValue(int[,] mask, int startX, int startY, int width, int height)
        {
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            int maskWidth = mask.GetLength(1);
            int maskHeight = mask.GetLength(0);

            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    int x = startX + dx;
                    int y = startY + dy;

                    if (x >= 0 && x < maskWidth && y >= 0 && y < maskHeight)
                    {
                        int value = mask[y, x];
                        if (counts.TryGetValue(value, out int currentCount))
                        {
                            counts[value] = currentCount + 1;
                        }
                        else
                        {
                            counts[value] = 1;
                        }
                    }
                }
            }

            // Find the value with maximum count
            int majorityValue = 0;
            int maxCount = 0;
            
            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    majorityValue = kvp.Key;
                }
            }

            return majorityValue;
        }

        /// <summary>
        /// Count unique countries in a mask for validation
        /// </summary>
        private int CountUniqueCountries(int[,] mask)
        {
            var unique = new System.Collections.Generic.HashSet<int>();
            
            for (int y = 0; y < mask.GetLength(0); y++)
            {
                for (int x = 0; x < mask.GetLength(1); x++)
                {
                    unique.Add(mask[y, x]);
                }
            }

            return unique.Count;
        }
    }
}