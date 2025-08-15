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
        private readonly StatesBorderManager _statesManager;
        
        private MapViewType _currentViewType = MapViewType.Terrain;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        // Selected country and state tracking for highlighting
        private IndexedCountryFeature? _selectedCountry = null;
        private IndexedStateFeature? _selectedState = null;
        
        // Zoom level threshold for state rendering
        private const int StateRenderingZoomThreshold = 3;
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        public IndexedCountryFeature? SelectedCountry => _selectedCountry;
        public IndexedStateFeature? SelectedState => _selectedState;
        public int StateRenderingThreshold => StateRenderingZoomThreshold;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        public event EventHandler<IndexedCountryFeature?>? SelectedCountryChanged;
        public event EventHandler<IndexedStateFeature?>? SelectedStateChanged;
        
        public HybridMapManager(int baseWidth = 4096, int baseHeight = 2048)
        {
            _terrainManager = new MultiResolutionMapManager(baseWidth, baseHeight);
            _politicalManager = new PoliticalBorderManager();
            _politicalTileManager = new PoliticalTileManager(_politicalManager, baseWidth, baseHeight);
            _statesManager = new StatesBorderManager();
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
                    // Get the base political map - selection highlighting is handled internally by the tile manager
                    result = _politicalTileManager.AssembleView(zoomLevel, viewArea, onTileReady);
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
        /// Gets the state at a specific pixel position (only works if zoom level is high enough)
        /// </summary>
        public IndexedStateFeature? GetStateAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
        {
            // Only check for states if zoom level is high enough
            if (zoomLevel < StateRenderingZoomThreshold)
                return null;

            try
            {
                // Validate inputs
                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1)
                {
                    Debug.WriteLine($"Invalid input to GetStateAtPixel: pixel=({pixelX},{pixelY}), zoom={zoomLevel}");
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
                
                Debug.WriteLine($"State lookup: Screen ({pixelX},{pixelY}) + Offset ({viewOffset.X},{viewOffset.Y}) = Map ({mapPixelX},{mapPixelY}) -> Geo ({longitude:F4},{latitude:F4})");
                
                // Find state at this geographic location - filter by selected country if one is selected
                string? countryFilter = _selectedCountry?.CountryCode;
                return _politicalTileManager.GetStateAtGeographicPoint(longitude, latitude, countryFilter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting state at pixel ({pixelX}, {pixelY}): {ex.Message}");
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
                
                // Clear state selection when country changes
                if (country == null)
                {
                    SelectState(null);
                }
                
                // Notify the political tile manager about the selection change
                _politicalTileManager.SetSelectedCountry(country);
                
                // Notify listeners about the selection change
                SelectedCountryChanged?.Invoke(this, country);
                
                Debug.WriteLine($"Country selection changed: {(country != null ? $"{country.CountryName} ({country.CountryCode})" : "None")}");
            }
        }

        /// <summary>
        /// Selects a state for highlighting (only works if a country is selected and zoom is high enough)
        /// </summary>
        public void SelectState(IndexedStateFeature? state)
        {
            // Only allow state selection if we have a country selected and they match
            if (state != null && _selectedCountry != null && 
                !state.CountryCode.Equals(_selectedCountry.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"State {state.StateName} does not belong to selected country {_selectedCountry.CountryCode}");
                return;
            }

            if (_selectedState != state)
            {
                _selectedState = state;
                
                // Notify the political tile manager about the state selection change
                _politicalTileManager.SetSelectedState(state);
                
                // Notify listeners about the selection change
                SelectedStateChanged?.Invoke(this, state);
                
                Debug.WriteLine($"State selection changed: {(state != null ? $"{state.StateName} in {state.CountryCode}" : "None")}");
            }
        }

        /// <summary>
        /// Gets all states for the currently selected country
        /// </summary>
        public List<IndexedStateFeature> GetStatesForSelectedCountry()
        {
            if (_selectedCountry == null) 
                return new List<IndexedStateFeature>();

            return _politicalTileManager.GetStatesForCountry(_selectedCountry.CountryCode);
        }

        /// <summary>
        /// Determines if states should be rendered at the current zoom level
        /// </summary>
        public bool ShouldRenderStates(int zoomLevel)
        {
            return zoomLevel >= StateRenderingZoomThreshold;
        }

        /// <summary>
        /// Clears the current country selection
        /// </summary>
        public void ClearCountrySelection()
        {
            SelectCountry(null);
        }

        /// <summary>
        /// Clears the current state selection
        /// </summary>
        public void ClearStateSelection()
        {
            SelectState(null);
        }

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}