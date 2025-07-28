using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace StrategyGame
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
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        
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
        /// Assembles the current view based on the selected map type
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            switch (_currentViewType)
            {
                case MapViewType.Terrain:
                    return _terrainManager.AssembleView(zoomLevel, viewArea, onTileReady);
                
                case MapViewType.Political:
                    // Pass highlighting information to political manager
                    return _politicalTileManager.AssembleViewWithHighlighting(zoomLevel, viewArea, _highlightedCountry, onTileReady);
                
                default:
                    return null;
            }
        }
        
        public SKSizeI GetMapSize(int zoomLevel)
        {
            return _terrainManager.GetMapSize(zoomLevel);
        }
        
        public int GetCellSizeForZoom(int zoomLevel)
        {
            return _terrainManager.GetCellSizeForZoom(zoomLevel);
        }

        /// <summary>
        /// Gets country information at the specified screen coordinates when in political view
        /// </summary>
        /// <param name="screenX">Screen X coordinate</param>
        /// <param name="screenY">Screen Y coordinate</param>
        /// <param name="zoomLevel">Current zoom level</param>
        /// <param name="viewOffset">Current view offset</param>
        /// <returns>Country information if found, null otherwise</returns>
        public Country? GetCountryAtScreenCoordinate(int screenX, int screenY, int zoomLevel, SKPointI viewOffset)
        {
            Debug.WriteLine($"GetCountryAtScreenCoordinate: Input - Screen({screenX}, {screenY}), Zoom:{zoomLevel}, Offset({viewOffset.X}, {viewOffset.Y})");
            
            if (_currentViewType != MapViewType.Political)
            {
                Debug.WriteLine("GetCountryAtScreenCoordinate: Not in political view, returning null");
                return null;
            }

            // Convert screen coordinates to world coordinates
            int worldX = screenX + viewOffset.X;
            int worldY = screenY + viewOffset.Y;
            
            Debug.WriteLine($"GetCountryAtScreenCoordinate: World coordinates - ({worldX}, {worldY})");
            
            // Convert world coordinates to geographic coordinates
            var mapSize = GetMapSize(zoomLevel);
            Debug.WriteLine($"GetCountryAtScreenCoordinate: Map size - {mapSize.Width}x{mapSize.Height}");
            
            double geoX = (double)worldX / mapSize.Width * 360.0 - 180.0; // Longitude
            double geoY = 90.0 - (double)worldY / mapSize.Height * 180.0; // Latitude
            
            Debug.WriteLine($"GetCountryAtScreenCoordinate: Geographic coordinates - Longitude:{geoX:F2}, Latitude:{geoY:F2}");
            
            // Get country at this geographic coordinate
            var result = GetCountryAtGeoCoordinate(geoX, geoY);
            Debug.WriteLine($"GetCountryAtScreenCoordinate: Result - {result?.Name ?? "null"}");
            
            return result;
        }

        // Country highlighting functionality
        private Country? _highlightedCountry;
        
        // Cache for sample countries
        private static readonly List<Country> _sampleCountries = new List<Country>();
        
        static HybridMapManager()
        {
            // Initialize sample countries once
            var usa = new Country("United States");
            usa.Population = 328000000;
            usa.Budget = 4000000000000; // 4 trillion
            usa.AddResource("Oil", 500000);
            usa.AddResource("Coal", 750000);
            usa.AddResource("Technology", 1000000);
            
            var canada = new Country("Canada");
            canada.Population = 38000000;
            canada.Budget = 600000000000; // 600 billion
            canada.AddResource("Oil", 300000);
            canada.AddResource("Lumber", 800000);
            canada.AddResource("Minerals", 400000);
            
            var mexico = new Country("Mexico");
            mexico.Population = 128000000;
            mexico.Budget = 300000000000; // 300 billion
            mexico.AddResource("Oil", 200000);
            mexico.AddResource("Agriculture", 350000);
            
            var uk = new Country("United Kingdom");
            uk.Population = 67000000;
            uk.Budget = 800000000000; // 800 billion
            uk.AddResource("Financial Services", 500000);
            uk.AddResource("Technology", 300000);
            
            _sampleCountries.AddRange(new[] { usa, canada, mexico, uk });
        }
        
        /// <summary>
        /// Sets the country to highlight with a white border
        /// </summary>
        /// <param name="country">Country to highlight, or null to clear highlighting</param>
        public void SetHighlightedCountry(Country? country)
        {
            if (_highlightedCountry != country)
            {
                _highlightedCountry = country;
                
                // Force refresh of political tiles to show highlighting
                if (_currentViewType == MapViewType.Political)
                {
                    _politicalTileManager.InvalidateCache();
                }
            }
        }
        
        /// <summary>
        /// Gets the currently highlighted country
        /// </summary>
        public Country? HighlightedCountry => _highlightedCountry;
        /// <summary>
        /// Gets country information at the specified geographic coordinates
        /// </summary>
        /// <param name="longitude">Longitude in degrees</param>
        /// <param name="latitude">Latitude in degrees</param>
        /// <returns>Country information if found, null otherwise</returns>
        private Country? GetCountryAtGeoCoordinate(double longitude, double latitude)
        {
            Debug.WriteLine($"GetCountryAtGeoCoordinate: Checking coordinates - Longitude:{longitude:F2}, Latitude:{latitude:F2}");
            
            // Expanded geographic bounds for testing - cover more area
            // North America (expanded bounds)
            if (longitude >= -180 && longitude <= -30 && latitude >= 10 && latitude <= 80)
            {
                Debug.WriteLine("GetCountryAtGeoCoordinate: Position is in North America region (expanded)");
                
                if (latitude >= 46) // Rough Canada border (lowered)
                {
                    var canada = _sampleCountries.Find(c => c.Name == "Canada");
                    Debug.WriteLine($"GetCountryAtGeoCoordinate: Latitude >= 46, returning Canada: {canada?.Name ?? "null"}");
                    return canada;
                }
                else if (latitude >= 22) // Rough US border (lowered)
                {
                    var usa = _sampleCountries.Find(c => c.Name == "United States");
                    Debug.WriteLine($"GetCountryAtGeoCoordinate: Latitude >= 22, returning USA: {usa?.Name ?? "null"}");
                    return usa;
                }
                else
                {
                    var mexico = _sampleCountries.Find(c => c.Name == "Mexico");
                    Debug.WriteLine($"GetCountryAtGeoCoordinate: Latitude < 22, returning Mexico: {mexico?.Name ?? "null"}");
                    return mexico;
                }
            }
            
            // Europe and UK (expanded bounds)
            if (longitude >= -15 && longitude <= 30 && latitude >= 35 && latitude <= 70)
            {
                Debug.WriteLine("GetCountryAtGeoCoordinate: Position is in Europe region (expanded)");
                var uk = _sampleCountries.Find(c => c.Name == "United Kingdom");
                Debug.WriteLine($"GetCountryAtGeoCoordinate: Returning UK: {uk?.Name ?? "null"}");
                return uk;
            }
            
            Debug.WriteLine("GetCountryAtGeoCoordinate: No country found for these coordinates");
            
            // For debugging - if we're anywhere on the map, let's return USA as a fallback
            if (longitude >= -180 && longitude <= 180 && latitude >= -90 && latitude <= 90)
            {
                Debug.WriteLine("GetCountryAtGeoCoordinate: DEBUG - Returning USA as fallback for any valid coordinate");
                return _sampleCountries.Find(c => c.Name == "United States");
            }
            
            return null;
        }

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}