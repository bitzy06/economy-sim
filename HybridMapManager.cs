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

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}