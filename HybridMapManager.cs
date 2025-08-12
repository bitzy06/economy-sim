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
                    return _politicalTileManager.AssembleView(zoomLevel, viewArea, onTileReady);
                
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
                // Convert screen pixel to map pixel (accounting for view offset)
                int mapPixelX = pixelX + viewOffset.X;
                int mapPixelY = pixelY + viewOffset.Y;
                
                // Get current map dimensions for this zoom level
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int mapWidth = _terrainManager.BaseWidth * cellSize;
                int mapHeight = _terrainManager.BaseHeight * cellSize;
                
                // Check bounds
                if (mapPixelX < 0 || mapPixelX >= mapWidth || mapPixelY < 0 || mapPixelY >= mapHeight)
                {
                    return null;
                }
                
                // Convert map pixel to geographic coordinates
                var (longitude, latitude) = CoordinateTransform.PixelToGeographic(mapPixelX, mapPixelY, mapWidth, mapHeight);
                
                // Find country at this geographic location
                return _politicalTileManager.GetCountryAtGeographicPoint(longitude, latitude);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting country at pixel ({pixelX}, {pixelY}): {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}