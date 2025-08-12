using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Drawing;

namespace Economy_sim
{
    /// <summary>
    /// Manages both terrain and political map layers with memory-efficient rendering
    /// </summary>
    public class HybridMapManager
    {
        private readonly MultiResolutionMapManager _terrainManager;
        private readonly PoliticalBorderManager _politicalManager;
        private readonly PoliticalTileManager _politicalTileManager;
        
        private MapViewType _currentViewType = MapViewType.Terrain;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        private Point? _highlightedLocation = null;
        
        // Selected country tracking for white border highlighting
        private IndexedCountryFeature? _selectedCountry = null;
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        public IndexedCountryFeature? SelectedCountry => _selectedCountry;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        public event EventHandler<IndexedCountryFeature?>? SelectedCountryChanged;
        
        public HybridMapManager(int baseWidth = 4096, int baseHeight = 2048)
        {
            _terrainManager = new MultiResolutionMapManager(baseWidth, baseHeight);
            _politicalManager = new PoliticalBorderManager();
            _politicalTileManager = new PoliticalTileManager(_politicalManager, baseWidth, baseHeight);
        }
        
        public void SetViewType(MapViewType viewType)
        {
            if (_currentViewType != viewType)
            {
                _currentViewType = viewType;
                ViewTypeChanged?.Invoke(this, viewType);
                
                // Clear terrain cache when switching to political to save memory
                if (viewType == MapViewType.Political)
                {
                    // Force garbage collection of terrain tiles to free memory
                    GC.Collect();
                }
            }
        }
        
        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;
                _politicalTileManager.SetPoliticalMapDate(date);
                _politicalMapDate = date;
            }
        }
        
        /// <summary>
        /// Highlights a country border at the specified mouse position
        /// </summary>
        /// <param name="mousePosition">The mouse position in map coordinates</param>
        /// <param name="zoomLevel">The current zoom level</param>
        public void HighlightCountryBorder(Point mousePosition, int zoomLevel = 1)
        {
            if (_currentViewType == MapViewType.Political)
            {
                _highlightedLocation = mousePosition;
                Debug.WriteLine($"Country border highlight requested at {mousePosition} with zoom level {zoomLevel}");
                // The actual highlighting will happen during the next AssembleView call
            }
        }
        
        /// <summary>
        /// Assembles the current view based on the selected map type
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            SKBitmap? result = null;
            
            switch (_currentViewType)
            {
                case MapViewType.Terrain:
                    result = _terrainManager.AssembleView(zoomLevel, viewArea, onTileReady);
                    break;
                
                case MapViewType.Political:
                    // Get the base political map
                    result = _politicalTileManager.AssembleView(zoomLevel, viewArea, onTileReady);
                    
                    // Apply country highlight if needed
                    if (result != null && _highlightedLocation.HasValue)
                    {
                        // Get the mask for this view area
                        int cellSize = GetCellSizeForZoom(zoomLevel);
                        int tileWidth = viewArea.Width;
                        int tileHeight = viewArea.Height;
                        
                        try
                        {
                            // Get the political mask for this view (required for border detection)
                            var mask = _politicalTileManager.GetViewMask(cellSize, viewArea.Left, viewArea.Top, tileWidth, tileHeight);
                            if (mask != null)
                            {
                                // Convert map coordinates to local bitmap coordinates
                                Point localPoint = new Point(
                                   _highlightedLocation.Value.X - viewArea.Left,
                                   _highlightedLocation.Value.Y - viewArea.Top
                               );

                                // Ensure the local point is within the actual mask dimensions
                                if (mask != null && localPoint.X >= 0 && localPoint.Y >= 0 &&
                                    localPoint.X < mask.GetLength(1) && localPoint.Y < mask.GetLength(0))
                                {
                                    Debug.WriteLine($"Applying country border highlight at local point: {localPoint}");
                                    Debug.WriteLine($"Mask dimensions: {mask.GetLength(1)}x{mask.GetLength(0)}, Bitmap dimensions: {tileWidth}x{tileHeight}");
                                    Debug.WriteLine($"actual point: map coordinates={_highlightedLocation.Value}, local coordinates={localPoint}");
                                    // Apply the border highlight using the actual mask dimensions
                                    result = _politicalTileManager.CountryBoarderSelectAdd(result, mask, mask.GetLength(1), mask.GetLength(0), localPoint);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error highlighting country border: {ex.Message}");
                        }
                    }
                    break;
                
                default:
                    result = null;
                    break;
            }
            
            return result;
        }
        
        public SKSizeI GetMapSize(int zoomLevel)
        {
            return _terrainManager.GetMapSize(zoomLevel);
        }
        
        public int GetCellSizeForZoom(int zoomLevel)
        {
            return _terrainManager.GetCellSizeForZoom(zoomLevel);
        }
        
        public int BaseWidth => _terrainManager.BaseWidth;
        public int BaseHeight => _terrainManager.BaseHeight;

        /// <summary>
        /// Gets the country at a specific pixel position (accounting for zoom and pan)
        /// </summary>
        /// <param name="pixelX">Screen pixel X coordinate</param>
        /// <param name="pixelY">Screen pixel Y coordinate</param>
        /// <param name="zoomLevel">Current zoom level</param>
        /// <param name="viewOffset">Current view offset</param>
        /// <returns>Country information if found, null otherwise</returns>
        public IndexedCountryFeature? GetCountryAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
        {
            try
            {
                // Validate inputs
                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1)
                {
                    Debug.WriteLine($"Invalid input to GetCountryAtPixel: pixel=({pixelX},{pixelY}), zoom={zoomLevel}");
                    return null;
                }

                // Convert screen pixel to map pixel (accounting for view offset)
                int mapPixelX = pixelX + viewOffset.X;
                int mapPixelY = pixelY + viewOffset.Y;
                
                // Get current map dimensions for this zoom level
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int mapWidth = BaseWidth * cellSize;
                int mapHeight = BaseHeight * cellSize;
                
                // Check bounds
                if (mapPixelX < 0 || mapPixelX >= mapWidth || mapPixelY < 0 || mapPixelY >= mapHeight)
                {
                    Debug.WriteLine($"Pixel ({mapPixelX}, {mapPixelY}) is outside map bounds ({mapWidth}x{mapHeight})");
                    return null;
                }
                
                // Convert map pixel to geographic coordinates
                var (longitude, latitude) = CoordinateTransform.PixelToGeographic(mapPixelX, mapPixelY, mapWidth, mapHeight);
                
                Debug.WriteLine($"Screen ({pixelX},{pixelY}) + Offset ({viewOffset.X},{viewOffset.Y}) = Map ({mapPixelX},{mapPixelY}) -> Geo ({longitude:F4},{latitude:F4})");
                
                // Find country at this geographic location
                return _politicalTileManager.GetCountryAtGeographicPoint(longitude, latitude);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting country at pixel ({pixelX}, {pixelY}): {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Selects a country for highlighting with white borders
        /// </summary>
        public void SelectCountry(IndexedCountryFeature? country)
        {
            if (_selectedCountry != country)
            {
                _selectedCountry = country;
                
                // Notify the political tile manager about the selection change
                _politicalTileManager.SetSelectedCountry(country);
                
                // Notify listeners about the selection change
                SelectedCountryChanged?.Invoke(this, country);
                
                Debug.WriteLine($"Country selection changed: {(country != null ? $"{country.CountryName} ({country.CountryCode})" : "None")}");
            }
        }

        /// <summary>
        /// Clears the current country selection
        /// </summary>
        public void ClearCountrySelection()
        {
            SelectCountry(null);
        }

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}