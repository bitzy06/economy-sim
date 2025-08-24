using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const uint WhiteBorderColor = 0xFFFFFFFF; // Country selection border
        private const uint BlackBorderColor = 0xFF000000; // Regular country border
        private const uint YellowBorderColor = 0xFFFFFF00; // State border
        private const uint BlueBorderColor = 0xFF0080FF; // State selection border

        // Thread-safe random for visual variation
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(
            () => new Random(Environment.TickCount + System.Threading.Thread.CurrentThread.ManagedThreadId));

        public GridRenderer(GridControlEngine gridEngine, PoliticalDataCache dataCache)
        {
            _gridEngine = gridEngine ?? throw new ArgumentNullException(nameof(gridEngine));
            _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        }

        /// <summary>
        /// Render a tile from the grid to a bitmap (tileSize = output pixels and grid cells span).
        /// </summary>
        public SKBitmap? RenderGridTile(int tileX, int tileY, int tileSize, int selectedCountryId = -1, int lodLevel = 0, 
            string? selectedCountryCode = null, int[,]? stateGrid = null, int selectedStateId = -1)
        {
            try
            {
                Debug.WriteLine($"GridRenderer.RenderGridTile: Starting render for tile ({tileX}, {tileY}) at LOD {lodLevel}");
                
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);

                int startX = tileX * tileSize;
                int startY = tileY * tileSize;
                int endX = Math.Min(startX + tileSize, gridWidth);
                int endY = Math.Min(startY + tileSize, gridHeight);
                
                if (startX >= gridWidth || startY >= gridHeight)
                {
                    return null;
                }
                
                int actualWidth = endX - startX;
                int actualHeight = endY - startY;
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return null;
                }

                var bitmap = new SKBitmap(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.Erase(new SKColor(WaterColor));

                RenderColorsToTile(bitmap, controlGrid, startX, startY, actualWidth, actualHeight, tileSize);
                RenderBordersToTile(bitmap, controlGrid, startX, startY, actualWidth, actualHeight, tileSize, 
                    selectedCountryId, selectedCountryCode, stateGrid, selectedStateId);

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GridRenderer.RenderGridTile: ERROR rendering tile ({tileX}, {tileY}): {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"GridRenderer.RenderGridTile: Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Render a tile at a higher output pixel resolution while sampling the same grid extent.
        /// tileGridSize defines the span in grid cells for this tile (typically 512).
        /// outputTilePixels defines the bitmap size to render (e.g., 512 * pixelsPerCell).
        /// </summary>
        public SKBitmap? RenderGridTileScaled(int tileX, int tileY, int tileGridSize, int outputTilePixels, int selectedCountryId = -1, int lodLevel = 0,
            string? selectedCountryCode = null, int[,]? stateGrid = null, int selectedStateId = -1)
        {
            try
            {
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);

                int startX = tileX * tileGridSize;
                int startY = tileY * tileGridSize;
                int endX = Math.Min(startX + tileGridSize, gridWidth);
                int endY = Math.Min(startY + tileGridSize, gridHeight);

                if (startX >= gridWidth || startY >= gridHeight)
                {
                    return null;
                }

                int actualWidth = endX - startX;
                int actualHeight = endY - startY;
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    return null;
                }

                var bitmap = new SKBitmap(outputTilePixels, outputTilePixels, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.Erase(new SKColor(WaterColor));

                RenderColorsToTile(bitmap, controlGrid, startX, startY, actualWidth, actualHeight, outputTilePixels);
                RenderBordersToTile(bitmap, controlGrid, startX, startY, actualWidth, actualHeight, outputTilePixels, 
                    selectedCountryId, selectedCountryCode, stateGrid, selectedStateId);

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GridRenderer.RenderGridTileScaled: ERROR rendering tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Render colors to a tile bitmap, properly mapping grid to tile coordinates
        /// </summary>
        private void RenderColorsToTile(SKBitmap bitmap, int[,] controlGrid, int gridStartX, int gridStartY, int gridWidth, int gridHeight, int tilePixels)
        {
            unsafe
            {
                var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                int stride = bitmap.RowBytes / 4;

                Parallel.For(0, tilePixels, y =>
                {
                    var rng = ThreadLocalRandom.Value;
                    
                    for (int x = 0; x < tilePixels; x++)
                    {
                        // Map tile pixel to grid coordinate
                        int gridX = gridStartX + (x * gridWidth) / tilePixels;
                        int gridY = gridStartY + (y * gridHeight) / tilePixels;
                        
                        uint color;
                        
                        if (gridX < controlGrid.GetLength(1) && gridY < controlGrid.GetLength(0) && 
                            gridX >= gridStartX && gridY >= gridStartY &&
                            gridX < gridStartX + gridWidth && gridY < gridStartY + gridHeight)
                        {
                            int countryId = controlGrid[gridY, gridX];
                            
                            if (countryId == 0)
                            {
                                color = WaterColor;
                            }
                            else
                            {
                                SKColor baseColor = _dataCache.GetCountryColorByRasterCode(countryId);
                                int variation = rng.Next(-5, 6);
                                byte r = (byte)Math.Clamp(baseColor.Red + variation, 0, 255);
                                byte g = (byte)Math.Clamp(baseColor.Green + variation, 0, 255);
                                byte b = (byte)Math.Clamp(baseColor.Blue + variation, 0, 255);
                                
                                color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);
                            }
                        }
                        else
                        {
                            color = WaterColor;
                        }
                        
                        pixelPtr[y * stride + x] = color;
                    }
                });
            }
        }

        /// <summary>
        /// Render borders to a tile bitmap, properly mapping grid to tile coordinates
        /// </summary>
        private void RenderBordersToTile(SKBitmap bitmap, int[,] controlGrid, int gridStartX, int gridStartY, int gridWidth, int gridHeight, int tilePixels, 
            int selectedCountryId, string? selectedCountryCode = null, int[,]? stateGrid = null, int selectedStateId = -1)
        {
            unsafe
            {
                var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                int stride = bitmap.RowBytes / 4;
                int gridFullWidth = controlGrid.GetLength(1);
                int gridFullHeight = controlGrid.GetLength(0);

                // Get state grid dimensions (should match control grid)
                int stateGridWidth = stateGrid?.GetLength(1) ?? 0;
                int stateGridHeight = stateGrid?.GetLength(0) ?? 0;
                bool hasValidStateGrid = stateGrid != null && 
                    stateGridWidth == gridFullWidth && stateGridHeight == gridFullHeight;

                Parallel.For(0, tilePixels, y =>
                {
                    for (int x = 0; x < tilePixels; x++)
                    {
                        int gridX = gridStartX + (x * gridWidth) / tilePixels;
                        int gridY = gridStartY + (y * gridHeight) / tilePixels;
                        
                        if (gridX >= gridFullWidth || gridY >= gridFullHeight || gridX < 0 || gridY < 0)
                            continue;

                        int currentCountryId = controlGrid[gridY, gridX];
                        if (currentCountryId == 0)
                            continue;

                        // Get current state id if state grid is available
                        int currentStateId = hasValidStateGrid ? stateGrid![gridY, gridX] : 0;

                        bool isCountryBorder = false;
                        bool isStateBorder = false;
                        bool hasSelectedCountryNeighbor = false;
                        bool hasSelectedStateNeighbor = false;

                        var neighbors = new[]
                        {
                            (gridX + 1, gridY),
                            (gridX - 1, gridY),
                            (gridX, gridY + 1),
                            (gridX, gridY - 1)
                        };

                        foreach (var (nx, ny) in neighbors)
                        {
                            int neighborCountryId = 0;
                            int neighborStateId = 0;
                            
                            if (nx >= 0 && nx < gridFullWidth && ny >= 0 && ny < gridFullHeight)
                            {
                                neighborCountryId = controlGrid[ny, nx];
                                if (hasValidStateGrid)
                                    neighborStateId = stateGrid![ny, nx];
                            }

                            // Check for country borders
                            if (neighborCountryId != currentCountryId)
                            {
                                isCountryBorder = true;
                            }

                            // Check for state borders (only within the same country)
                            if (hasValidStateGrid && neighborCountryId == currentCountryId && 
                                neighborStateId != currentStateId && currentStateId != 0 && neighborStateId != 0)
                            {
                                isStateBorder = true;
                            }

                            // Check for selected country neighbors
                            if (selectedCountryId != -1 && 
                                (currentCountryId == selectedCountryId || neighborCountryId == selectedCountryId))
                            {
                                hasSelectedCountryNeighbor = true;
                            }

                            // Check for selected state neighbors
                            if (selectedStateId != -1 && hasValidStateGrid &&
                                (currentStateId == selectedStateId || neighborStateId == selectedStateId))
                            {
                                hasSelectedStateNeighbor = true;
                            }
                        }

                        // Priority order: State selection > Country selection > State border > Country border
                        if (hasSelectedStateNeighbor && isStateBorder)
                        {
                            pixelPtr[y * stride + x] = BlueBorderColor; // Blue for selected state
                        }
                        else if (hasSelectedCountryNeighbor && isCountryBorder)
                        {
                            pixelPtr[y * stride + x] = WhiteBorderColor; // White for selected country
                        }
                        else if (isStateBorder && selectedCountryId != -1 && currentCountryId == selectedCountryId)
                        {
                            pixelPtr[y * stride + x] = YellowBorderColor; // Yellow for state borders in selected country
                        }
                        else if (isCountryBorder)
                        {
                            pixelPtr[y * stride + x] = BlackBorderColor; // Black for regular country borders
                        }
                    }
                });
            }
        }
    }
}