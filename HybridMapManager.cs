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
                try
                {
                    // Convert screen pixel to map pixel (accounting for view offset)
                    int mapPixelX = mousePosition.X;
                    int mapPixelY = mousePosition.Y;
                    
                    // Get current map dimensions for this zoom level
                    int cellSize = GetCellSizeForZoom(zoomLevel);
                    int mapWidth = BaseWidth * cellSize;
                    int mapHeight = BaseHeight * cellSize;
                    
                    // Check bounds
                    if (mapPixelX < 0 || mapPixelX >= mapWidth || mapPixelY < 0 || mapPixelY >= mapHeight)
                    {
                        Debug.WriteLine($"Point ({mapPixelX}, {mapPixelY}) is outside map bounds ({mapWidth}x{mapHeight})");
                        return;
                    }
                    
                    // Convert map pixel to geographic coordinates
                    var (longitude, latitude) = CoordinateTransform.PixelToGeographic(mapPixelX, mapPixelY, mapWidth, mapHeight);
                    
                    Debug.WriteLine($"Highlight: Map ({mapPixelX},{mapPixelY}) -> Geo ({longitude:F4},{latitude:F4})");
                    
                    // Find country at this geographic location and select it directly
                    // This is better than using visual highlighting with red blocks/crosshairs
                    var country = _politicalTileManager.GetCountryAtGeographicPoint(longitude, latitude);
                    if (country != null)
                    {
                        // We won't call SelectCountry here as that will be done by the caller if needed
                        Debug.WriteLine($"Found country at highlight position: {country.CountryName} ({country.CountryCode})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error highlighting country border: {ex.Message}");
                }
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
                    // Force synchronous rendering for political tiles to ensure they appear immediately
                    result = _politicalTileManager.AssembleView(zoomLevel, viewArea, onTileReady, forceSync: true);
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