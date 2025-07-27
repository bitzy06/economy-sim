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
        
        private static readonly string cshapesBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data", "country_borders");

        private string? FindCShapesFile()
        {
            // First check the base path for the complete file
            string primaryPath = Path.Combine(cshapesBasePath, "CShapes-2.0.shp");
            if (File.Exists(primaryPath))
            {
                Debug.WriteLine($"Found CShapes file at: {primaryPath}");
                return primaryPath;
            }
            
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
                    Debug.WriteLine($"Found CShapes file at: {path}");
                    return path;
                }
            }
            
            Debug.WriteLine($"CShapes file not found. Searched in:");
            Debug.WriteLine($"  Primary: {primaryPath}");
            foreach (string path in possiblePaths)
            {
                Debug.WriteLine($"  Alternative: {Path.GetFullPath(path)}");
            }
            
            return null;
        }

        private void GeneratePoliticalMask(int width, int height)
        {
            try
            {
                // Try to find CShapes file using the proper method
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    Debug.WriteLine("CShapes-2.0.shp file not found, political view unavailable");
                    Debug.WriteLine("Please ensure CShapes-2.0.shp is placed in the data/country_borders/ directory");
                    return;
                }
                
                Debug.WriteLine($"Using CShapes file: {cshapesPath}");
                Debug.WriteLine($"Generating political mask for date: {_politicalMapDate:yyyy-MM-dd}");
                Debug.WriteLine($"Target dimensions: {width}x{height}");
                
                // Use full map dimensions instead of view area dimensions for the mask
                var mapSize = _terrainManager.GetMapSize(0); // Get base resolution
                int maskWidth = mapSize.Width;
                int maskHeight = mapSize.Height;
                
                Debug.WriteLine($"Generating political mask at full resolution: {maskWidth}x{maskHeight}");
                
                // Generate political mask at full resolution
                _politicalMask = _politicalManager.CreatePoliticalMask(
                    cshapesPath, 
                    _politicalMapDate, 
                    maskWidth, 
                    maskHeight);
                
                if (_politicalMask != null)
                {
                    Debug.WriteLine($"Political mask generated successfully: {_politicalMask.GetLength(1)}x{_politicalMask.GetLength(0)}");
                    
                    // Save color mapping
                    _politicalManager.SaveColorMapping();
                    Debug.WriteLine("Color mapping saved");
                }
                else
                {
                    Debug.WriteLine("Political mask generation returned null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating political mask: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                _politicalMask = null;
            }
        }

        private SKBitmap? AssemblePoliticalView(int zoomLevel, SKRectI viewArea)
        {
            try
            {
                Debug.WriteLine($"Assembling political view - zoom: {zoomLevel}, area: {viewArea}");
                
                // Generate cache key
                string cacheKey = $"political_{zoomLevel}_{viewArea.Left}_{viewArea.Top}_{viewArea.Width}_{viewArea.Height}_{_politicalMapDate:yyyyMMdd}";
                
                // Check cache first
                if (_politicalCache.TryGetValue(cacheKey, out SKBitmap? cachedBitmap))
                {
                    Debug.WriteLine("Returning cached political view");
                    return cachedBitmap.Copy();
                }
                
                // Generate political mask if needed - use full map size, not view area size
                if (_politicalMask == null)
                {
                    var mapSize = _terrainManager.GetMapSize(0);
                    GeneratePoliticalMask(mapSize.Width, mapSize.Height);
                }
                
                // Check if mask generation was successful
                if (_politicalMask == null)
                {
                    Debug.WriteLine("Political mask is null, cannot render political view");
                    // Return a placeholder bitmap indicating political data is unavailable
                    return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
                }
                
                // Render political map for the view area
                var politicalBitmap = RenderPoliticalViewArea(viewArea);
                
                if (politicalBitmap == null)
                {
                    Debug.WriteLine("Political bitmap rendering returned null");
                    return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
                }
                
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
                
                _politicalCache[cacheKey] = politicalBitmap.Copy();
                Debug.WriteLine($"Political view assembled and cached: {politicalBitmap.Width}x{politicalBitmap.Height}");
                return politicalBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling political view: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
            }
        }

        private SKBitmap CreateUnavailablePlaceholder(int width, int height)
        {
            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            
            // Fill with a light gray background
            canvas.Clear(new SKColor(240, 240, 240, 255));
            
            // Draw a message indicating political data is unavailable
            using var paint = new SKPaint
            {
                Color = new SKColor(120, 120, 120, 255),
                TextSize = Math.Min(width, height) / 20f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            
            string message = "Political data unavailable";
            float x = width / 2f;
            float y = height / 2f;
            
            canvas.DrawText(message, x, y, paint);
            
            return bitmap;
        }

        private SKBitmap? RenderPoliticalViewArea(SKRectI viewArea)
        {
            if (_politicalMask == null)
            {
                Debug.WriteLine("Political mask is null in RenderPoliticalViewArea");
                return null;
            }
            
            try
            {
                // Extract the view area from the political mask
                int maskHeight = _politicalMask.GetLength(0);
                int maskWidth = _politicalMask.GetLength(1);
                
                Debug.WriteLine($"Political mask dimensions: {maskWidth}x{maskHeight}");
                Debug.WriteLine($"Requested view area: {viewArea}");
                
                // Calculate scaling factors if needed
                var mapSize = _terrainManager.GetMapSize(0);
                double scaleX = (double)maskWidth / mapSize.Width;
                double scaleY = (double)maskHeight / mapSize.Height;
                
                // Scale view area coordinates to match mask coordinates
                int scaledLeft = (int)(viewArea.Left * scaleX);
                int scaledTop = (int)(viewArea.Top * scaleY);
                int scaledRight = (int)(viewArea.Right * scaleX);
                int scaledBottom = (int)(viewArea.Bottom * scaleY);
                
                // Clamp view area to mask bounds
                int startX = Math.Max(0, Math.Min(scaledLeft, maskWidth - 1));
                int startY = Math.Max(0, Math.Min(scaledTop, maskHeight - 1));
                int endX = Math.Max(startX, Math.Min(scaledRight, maskWidth));
                int endY = Math.Max(startY, Math.Min(scaledBottom, maskHeight));
                
                int actualWidth = endX - startX;
                int actualHeight = endY - startY;
                
                Debug.WriteLine($"Scaled coordinates: ({startX},{startY}) to ({endX},{endY})");
                Debug.WriteLine($"Actual render size: {actualWidth}x{actualHeight}");
                
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    Debug.WriteLine("Invalid render dimensions");
                    return null;
                }
                
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
                var result = _politicalManager.RenderPoliticalMap(viewMask, actualWidth, actualHeight);
                
                if (result != null)
                {
                    Debug.WriteLine($"Political view area rendered: {result.Width}x{result.Height}");
                }
                else
                {
                    Debug.WriteLine("RenderPoliticalMap returned null");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering political view area: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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