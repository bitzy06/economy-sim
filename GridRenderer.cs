using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// CPU-based grid renderer that maintains visual parity with polygon-based system.
    /// Serves as fallback for GPU renderer and reference implementation.
    /// </summary>
    public class GridRenderer
    {
        private readonly GridControlEngine _gridEngine;
        private readonly PoliticalDataCache _dataCache;
        
        // Border rendering settings
        private const uint WaterColor = 0xFF87CEEB; // LightSkyBlue
        private const uint WhiteBorderColor = 0xFFFFFFFF;
        private const uint BlackBorderColor = 0xFF000000;

        // Thread-safe random for visual variation
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(
            () => new Random(Environment.TickCount + System.Threading.Thread.CurrentThread.ManagedThreadId));

        public GridRenderer(GridControlEngine gridEngine, PoliticalDataCache dataCache)
        {
            _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
            _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        }

        /// <summary>
        /// Render a tile from the grid to a bitmap
        /// </summary>
        public SKBitmap? RenderGridTile(int tileX, int tileY, int tileSize, int selectedCountryId = -1, int lodLevel = 0)
        {
            try
            {
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);

                // Calculate tile bounds in grid coordinates
                int startX = tileX * tileSize;
                int startY = tileY * tileSize;
                int endX = Math.Min(startX + tileSize, gridWidth);
                int endY = Math.Min(startY + tileSize, gridHeight);
                
                int actualWidth = endX - startX;
                int actualHeight = endY - startY;

                if (actualWidth <= 0 || actualHeight <= 0)
                    return null;

                // Create bitmap
                var bitmap = new SKBitmap(actualWidth, actualHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);

                // Render in two passes: colors first, then borders
                RenderColors(bitmap, controlGrid, startX, startY, actualWidth, actualHeight);
                RenderBorders(bitmap, controlGrid, startX, startY, actualWidth, actualHeight, selectedCountryId);

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering grid tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// First pass: Fill all pixels with base country colors
        /// </summary>
        private void RenderColors(SKBitmap bitmap, int[,] controlGrid, int startX, int startY, int width, int height)
        {
            unsafe
            {
                var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                int stride = bitmap.RowBytes / 4;

                Parallel.For(0, height, y =>
                {
                    var rng = ThreadLocalRandom.Value;
                    
                    for (int x = 0; x < width; x++)
                    {
                        int gridX = startX + x;
                        int gridY = startY + y;
                        
                        uint color;
                        
                        if (gridX < controlGrid.GetLength(1) && gridY < controlGrid.GetLength(0))
                        {
                            int countryId = controlGrid[gridY, gridX];
                            
                            if (countryId == 0)
                            {
                                // Water
                                color = WaterColor;
                            }
                            else
                            {
                                // Get country color from cache
                                SKColor baseColor = _dataCache.GetCountryColorByRasterCode(countryId);
                                
                                // Add slight random variation for visual interest
                                int variation = rng.Next(-5, 6);
                                byte r = (byte)Math.Clamp(baseColor.Red + variation, 0, 255);
                                byte g = (byte)Math.Clamp(baseColor.Green + variation, 0, 255);
                                byte b = (byte)Math.Clamp(baseColor.Blue + variation, 0, 255);
                                
                                color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);
                            }
                        }
                        else
                        {
                            // Out of bounds - water color
                            color = WaterColor;
                        }
                        
                        pixelPtr[y * stride + x] = color;
                    }
                });
            }
        }

        /// <summary>
        /// Second pass: Draw borders based on country ID changes
        /// </summary>
        private void RenderBorders(SKBitmap bitmap, int[,] controlGrid, int startX, int startY, int width, int height, int selectedCountryId)
        {
            unsafe
            {
                var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                int stride = bitmap.RowBytes / 4;
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);

                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int gridX = startX + x;
                        int gridY = startY + y;
                        
                        if (gridX >= gridWidth || gridY >= gridHeight)
                            continue;

                        int currentId = controlGrid[gridY, gridX];
                        
                        // Skip water for border detection
                        if (currentId == 0)
                            continue;

                        bool isBorder = false;
                        bool hasSelectedNeighbor = false;

                        // Check 4-connected neighbors for border detection
                        var neighbors = new[]
                        {
                            (gridX + 1, gridY),     // Right
                            (gridX - 1, gridY),     // Left  
                            (gridX, gridY + 1),     // Down
                            (gridX, gridY - 1)      // Up
                        };

                        foreach (var (nx, ny) in neighbors)
                        {
                            int neighborId = 0; // Default to water for out-of-bounds
                            
                            if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight)
                            {
                                neighborId = controlGrid[ny, nx];
                            }

                            // Border exists if neighbor has different ID
                            if (neighborId != currentId)
                            {
                                isBorder = true;
                            }

                            // Check for selected country adjacency
                            if (selectedCountryId != -1 && 
                                (currentId == selectedCountryId || neighborId == selectedCountryId))
                            {
                                hasSelectedNeighbor = true;
                            }
                        }

                        // Draw border if detected
                        if (isBorder)
                        {
                            uint borderColor = hasSelectedNeighbor ? WhiteBorderColor : BlackBorderColor;
                            pixelPtr[y * stride + x] = borderColor;
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Render a view area by assembling multiple tiles
        /// </summary>
        public SKBitmap? RenderGridView(Rectangle viewArea, int selectedCountryId = -1, int lodLevel = 0)
        {
            try
            {
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);

                // Clamp view area to grid bounds
                viewArea = Rectangle.Intersect(viewArea, new Rectangle(0, 0, gridWidth, gridHeight));
                
                if (viewArea.Width <= 0 || viewArea.Height <= 0)
                    return null;

                // Create bitmap for entire view
                var bitmap = new SKBitmap(viewArea.Width, viewArea.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);

                // Render colors and borders
                RenderColors(bitmap, controlGrid, viewArea.X, viewArea.Y, viewArea.Width, viewArea.Height);
                RenderBorders(bitmap, controlGrid, viewArea.X, viewArea.Y, viewArea.Width, viewArea.Height, selectedCountryId);

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering grid view: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get country ID at specific grid cell
        /// </summary>
        public int GetCountryAtGridCell(int cellX, int cellY)
        {
            var controlGrid = _gridEngine.ControlGrid;
            
            if (cellX >= 0 && cellX < controlGrid.GetLength(1) && 
                cellY >= 0 && cellY < controlGrid.GetLength(0))
            {
                return controlGrid[cellY, cellX];
            }
            
            return 0; // Water/invalid
        }

        /// <summary>
        /// Get country ID at geographic coordinate
        /// </summary>
        public int GetCountryAtGeographic(double longitude, double latitude)
        {
            var (cellX, cellY) = CoordinateTransform.GeographicToGridCell(longitude, latitude, 
                _gridEngine.Width, _gridEngine.Height);
            return GetCountryAtGridCell(cellX, cellY);
        }
    }
}