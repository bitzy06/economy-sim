using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;

namespace StrategyGame
{
    /// <summary>
    /// Manages both terrain and political map layers with memory-efficient rendering
    /// </summary>
    public class HybridMapManager
    {
        private readonly MultiResolutionMapManager _terrainManager;
        private readonly PoliticalBorderManager _politicalManager;
        
        private MapViewType _currentViewType = MapViewType.Terrain;
        private SKBitmap? _cachedPoliticalMap;
        private int[,]? _politicalMask;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        // Cache management
        private readonly Dictionary<string, SKBitmap> _politicalCache = new();
        private const int MaxCacheSize = 5; // Limit cache to prevent memory bloat
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        
        public HybridMapManager(int baseWidth = 4096, int baseHeight = 2048)
        {
            _terrainManager = new MultiResolutionMapManager(baseWidth, baseHeight);
            _politicalManager = new PoliticalBorderManager();
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
                ClearPoliticalCache();
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
                    return AssemblePoliticalView(zoomLevel, viewArea);
                
                default:
                    return null;
            }
        }
        
        private SKBitmap? AssemblePoliticalView(int zoomLevel, SKRectI viewArea)
        {
            try
            {
                // Generate cache key
                string cacheKey = $"political_{zoomLevel}_{viewArea.Left}_{viewArea.Top}_{viewArea.Width}_{viewArea.Height}_{_politicalMapDate:yyyyMMdd}";
                
                // Check cache first
                if (_politicalCache.TryGetValue(cacheKey, out SKBitmap? cachedBitmap))
                {
                    return cachedBitmap.Copy();
                }
                
                // Generate political mask if needed
                if (_politicalMask == null)
                {
                    GeneratePoliticalMask(viewArea.Width, viewArea.Height);
                }
                
                // Render political map for the view area
                var politicalBitmap = RenderPoliticalViewArea(viewArea);
                
                // Cache the result (with size limit)
                if (_politicalCache.Count >= MaxCacheSize)
                {
                    // Remove oldest cache entry
                    var oldestKey = GetOldestCacheKey();
                    if (oldestKey != null && _politicalCache.TryGetValue(oldestKey, out SKBitmap? oldBitmap))
                    {
                        oldBitmap.Dispose();
                        _politicalCache.Remove(oldestKey);
                    }
                }
                
                if (politicalBitmap != null)
                {
                    _politicalCache[cacheKey] = politicalBitmap.Copy();
                    return politicalBitmap;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling political view: {ex.Message}");
                return null;
            }
        }
        
        private void GeneratePoliticalMask(int width, int height)
        {
            try
            {
                // Try to find CShapes file
                string cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    Debug.WriteLine("CShapes-2.0.shp file not found, political view unavailable");
                    return;
                }
                
                Debug.WriteLine($"Generating political mask for date: {_politicalMapDate:yyyy-MM-dd}");
                
                // Generate political mask
                _politicalMask = _politicalManager.CreatePoliticalMask(
                    cshapesPath, 
                    _politicalMapDate, 
                    width, 
                    height);
                
                // Save color mapping
                _politicalManager.SaveColorMapping();
                
                Debug.WriteLine($"Political mask generated: {width}x{height}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating political mask: {ex.Message}");
                _politicalMask = null;
            }
        }
        
        private string? FindCShapesFile()
        {
            // Common locations where CShapes-2.0.shp might be located
            string[] possiblePaths = {
                "data/country_borders/CShapes-2.0.shp",
                "data/CShapes-2.0.shp",
                "../data/country_borders/CShapes-2.0.shp",
                "../data/CShapes-2.0.shp",
                "CShapes-2.0.shp"
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
        }
        
        private SKBitmap? RenderPoliticalViewArea(SKRectI viewArea)
        {
            if (_politicalMask == null)
                return null;
            
            try
            {
                // Extract the view area from the political mask
                int maskHeight = _politicalMask.GetLength(0);
                int maskWidth = _politicalMask.GetLength(1);
                
                // Clamp view area to mask bounds
                int startX = Math.Max(0, Math.Min(viewArea.Left, maskWidth - 1));
                int startY = Math.Max(0, Math.Min(viewArea.Top, maskHeight - 1));
                int endX = Math.Max(startX, Math.Min(viewArea.Right, maskWidth));
                int endY = Math.Max(startY, Math.Min(viewArea.Bottom, maskHeight));
                
                int actualWidth = endX - startX;
                int actualHeight = endY - startY;
                
                if (actualWidth <= 0 || actualHeight <= 0)
                    return null;
                
                // Extract sub-region
                int[,] viewMask = new int[actualHeight, actualWidth];
                for (int y = 0; y < actualHeight; y++)
                {
                    for (int x = 0; x < actualWidth; x++)
                    {
                        viewMask[y, x] = _politicalMask[startY + y, startX + x];
                    }
                }
                
                // Render the view area
                return _politicalManager.RenderPoliticalMap(viewMask, actualWidth, actualHeight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering political view area: {ex.Message}");
                return null;
            }
        }
        
        private string? GetOldestCacheKey()
        {
            // Simple implementation - just return the first key
            // In a more sophisticated implementation, you'd track access times
            foreach (var key in _politicalCache.Keys)
            {
                return key;
            }
            return null;
        }
        
        private void ClearPoliticalCache()
        {
            foreach (var bitmap in _politicalCache.Values)
            {
                bitmap.Dispose();
            }
            _politicalCache.Clear();
            
            _cachedPoliticalMap?.Dispose();
            _cachedPoliticalMap = null;
            _politicalMask = null;
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
            ClearPoliticalCache();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }
    }
}