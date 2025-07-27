using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// High-performance tile-based political map manager inspired by PixelMapGenerator optimizations
    /// </summary>
    public class PoliticalTileManager : IDisposable
    {
        private readonly PoliticalBorderManager _politicalManager;
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        // Performance optimizations inspired by PixelMapGenerator
        private const int TileSizePx = 512;
        private const int MaxCacheSize = 50; // Increased cache size for better performance
        
        // LRU Cache with proper eviction
        private readonly ConcurrentDictionary<string, CacheEntry> _tileCache = new();
        private readonly ConcurrentDictionary<string, int[,]> _maskCache = new();
        private readonly object _cacheLock = new object();
        private long _cacheAccessCounter = 0;
        
        // Async task management
        private readonly ConcurrentDictionary<string, Task<SKBitmap?>> _inFlightTasks = new();
        
        // Thread-safe random for parallel processing
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(
            () => new Random(Environment.TickCount + Thread.CurrentThread.ManagedThreadId));
            
        private class CacheEntry
        {
            public SKBitmap Bitmap { get; set; }
            public long AccessTime { get; set; }
            public DateTime CreatedForDate { get; set; }
        }
        
        public PoliticalTileManager(PoliticalBorderManager politicalManager, int baseWidth, int baseHeight)
        {
            _politicalManager = politicalManager;
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
        }
        
        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;
                ClearCacheForDateChange();
            }
        }
        
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                int cellSize = GetCellSizeForZoom(zoomLevel);
                
                // Calculate tile boundaries for the view area
                int tileStartX = viewArea.Left / TileSizePx;
                int tileStartY = viewArea.Top / TileSizePx;
                int tileEndX = (viewArea.Right + TileSizePx - 1) / TileSizePx;
                int tileEndY = (viewArea.Bottom + TileSizePx - 1) / TileSizePx;
                
                // Create composite bitmap
                var info = new SKImageInfo(viewArea.Width, viewArea.Height);
                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(240, 240, 240, 255)); // Light gray background
                
                // Render tiles in parallel where possible
                var tileRenderTasks = new List<Task>();
                
                for (int ty = tileStartY; ty < tileEndY; ty++)
                {
                    for (int tx = tileStartX; tx < tileEndX; tx++)
                    {
                        int tileX = tx, tileY = ty; // Capture for closure
                        
                        // Calculate tile position in the composite image
                        int destX = tileX * TileSizePx - viewArea.Left;
                        int destY = tileY * TileSizePx - viewArea.Top;
                        
                        // Get tile (async if not cached)
                        var tile = GetTileSync(cellSize, tileX, tileY);
                        if (tile != null)
                        {
                            var destRect = SKRect.Create(destX, destY, tile.Width, tile.Height);
                            canvas.DrawBitmap(tile, destRect);
                        }
                        else
                        {
                            // Trigger async loading for next frame
                            _ = GetTileAsync(cellSize, tileX, tileY, onTileReady);
                        }
                    }
                }
                
                // Create result bitmap
                var result = new SKBitmap(info);
                surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
                
                Debug.WriteLine($"Political view assembled in {sw.ElapsedMilliseconds}ms");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling political view: {ex.Message}");
                return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
            }
        }
        
        private SKBitmap? GetTileSync(int cellSize, int tileX, int tileY)
        {
            string cacheKey = $"{cellSize}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";
            
            if (_tileCache.TryGetValue(cacheKey, out var cached))
            {
                // Update access time for LRU
                cached.AccessTime = Interlocked.Increment(ref _cacheAccessCounter);
                return cached.Bitmap;
            }
            
            return null;
        }
        
        private async Task<SKBitmap?> GetTileAsync(int cellSize, int tileX, int tileY, Action? onComplete = null)
        {
            string cacheKey = $"{cellSize}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";
            
            // Check if already in progress
            if (_inFlightTasks.TryGetValue(cacheKey, out var existingTask))
            {
                return await existingTask;
            }
            
            // Create new async task
            var task = Task.Run(() => GenerateTileAsync(cellSize, tileX, tileY, cacheKey, onComplete));
            _inFlightTasks.TryAdd(cacheKey, task);
            
            try
            {
                return await task;
            }
            finally
            {
                _inFlightTasks.TryRemove(cacheKey, out _);
            }
        }
        
        private SKBitmap? GenerateTileAsync(int cellSize, int tileX, int tileY, string cacheKey, Action? onComplete)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                // Calculate tile bounds in pixel space
                int pixelX = tileX * TileSizePx;
                int pixelY = tileY * TileSizePx;
                
                var mapSize = GetMapSize(0); // Base resolution
                int tileWidth = Math.Min(TileSizePx, mapSize.Width - pixelX);
                int tileHeight = Math.Min(TileSizePx, mapSize.Height - pixelY);
                
                if (tileWidth <= 0 || tileHeight <= 0)
                {
                    return null;
                }
                
                // Get or generate political mask for this tile
                var tileMask = GetTileMask(pixelX, pixelY, tileWidth, tileHeight);
                if (tileMask == null)
                {
                    return CreateUnavailablePlaceholder(tileWidth, tileHeight);
                }
                
                // Render political map using optimized parallel processing
                var bitmap = RenderPoliticalTileOptimized(tileMask, tileWidth, tileHeight);
                
                if (bitmap != null)
                {
                    // Cache the result with LRU management
                    CacheTile(cacheKey, bitmap);
                    onComplete?.Invoke();
                }
                
                Debug.WriteLine($"Political tile ({tileX}, {tileY}) generated in {sw.ElapsedMilliseconds}ms");
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating political tile ({tileX}, {tileY}): {ex.Message}");
                return CreateUnavailablePlaceholder(TileSizePx, TileSizePx);
            }
        }
        
        private int[,]? GetTileMask(int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            string maskKey = $"mask_{pixelX}_{pixelY}_{tileWidth}_{tileHeight}_{_politicalMapDate:yyyyMMdd}";
            
            if (_maskCache.TryGetValue(maskKey, out var cached))
            {
                return cached;
            }
            
            try
            {
                // Find CShapes file
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    Debug.WriteLine("CShapes file not found for tile mask generation");
                    return null;
                }
                
                // Use unified coordinate transformation with base dimensions for consistency
                var bounds = CoordinateTransform.GetTileGeographicBounds(
                    pixelX / TileSizePx, 
                    pixelY / TileSizePx, 
                    TileSizePx, 
                    _baseWidth, 
                    _baseHeight);
                
                // Validate bounds
                if (!CoordinateTransform.IsValidGeoBounds(bounds))
                {
                    Debug.WriteLine($"Invalid geographic bounds calculated: {bounds}");
                    return null;
                }
                
                Debug.WriteLine($"Tile bounds using unified transform: {bounds}");
                
                // Generate mask for just this tile area using unified coordinate bounds
                var mask = _politicalManager.CreatePoliticalMask(
                    cshapesPath, 
                    _politicalMapDate, 
                    tileWidth, 
                    tileHeight, 
                    new double[] { bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat });
                
                // Cache the mask if successful
                if (mask != null && _maskCache.Count < MaxCacheSize * 2)
                {
                    _maskCache.TryAdd(maskKey, mask);
                    Debug.WriteLine($"Cached political mask for tile ({pixelX}, {pixelY})");
                }
                
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating tile mask: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        private SKBitmap? RenderPoliticalTileOptimized(int[,] mask, int width, int height)
        {
            try
            {
                var bitmap = new SKBitmap(width, height);
                
                // Check if mask has any political data
                bool hasData = false;
                int maxCountryCode = 0;
                for (int y = 0; y < mask.GetLength(0) && y < height; y++)
                {
                    for (int x = 0; x < mask.GetLength(1) && x < width; x++)
                    {
                        int code = mask[y, x];
                        if (code > 0)
                        {
                            hasData = true;
                            maxCountryCode = Math.Max(maxCountryCode, code);
                        }
                    }
                }
                
                if (!hasData)
                {
                    Debug.WriteLine("Political mask contains no country data");
                    // Fill with water color
                    bitmap.Erase(new SKColor(135, 206, 235, 255)); // Light blue
                    return bitmap;
                }
                
                Debug.WriteLine($"Political mask contains {maxCountryCode} country codes");
                
                // Use unsafe direct pixel access for maximum performance
                unsafe
                {
                    var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                    int stride = bitmap.RowBytes / 4;
                    
                    // Parallel processing inspired by PixelMapGenerator
                    Parallel.For(0, height, y =>
                    {
                        var rng = ThreadLocalRandom.Value;
                        
                        for (int x = 0; x < width; x++)
                        {
                            uint color;
                            
                            // Check bounds to avoid out-of-range access
                            if (y < mask.GetLength(0) && x < mask.GetLength(1))
                            {
                                int countryId = mask[y, x];
                                
                                if (countryId == 0)
                                {
                                    // Water - light blue
                                    color = 0xFF87CEEB; // LightSkyBlue in ARGB
                                }
                                else
                                {
                                    // Get country color - use the country ID directly as a string
                                    // or create a mapping based on the sequence
                                    var baseColor = _politicalManager.GetCountryColor(countryId.ToString());
                                    
                                    // Add slight random variation for visual interest
                                    int variation = rng.Next(-10, 11);
                                    byte r = (byte)Math.Clamp(baseColor.Red + variation, 0, 255);
                                    byte g = (byte)Math.Clamp(baseColor.Green + variation, 0, 255);
                                    byte b = (byte)Math.Clamp(baseColor.Blue + variation, 0, 255);
                                    
                                    color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);
                                }
                            }
                            else
                            {
                                // Out of bounds - water color
                                color = 0xFF87CEEB;
                            }
                            
                            pixelPtr[y * stride + x] = color;
                        }
                    });
                }
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in optimized political tile rendering: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        private void CacheTile(string cacheKey, SKBitmap bitmap)
        {
            lock (_cacheLock)
            {
                // Implement LRU eviction if cache is full
                if (_tileCache.Count >= MaxCacheSize)
                {
                    EvictOldestCacheEntry();
                }
                
                var entry = new CacheEntry
                {
                    Bitmap = bitmap.Copy(), // Make a copy to avoid disposal issues
                    AccessTime = Interlocked.Increment(ref _cacheAccessCounter),
                    CreatedForDate = _politicalMapDate
                };
                
                _tileCache.TryAdd(cacheKey, entry);
            }
        }
        
        private void EvictOldestCacheEntry()
        {
            string? oldestKey = null;
            long oldestTime = long.MaxValue;
            
            foreach (var kvp in _tileCache)
            {
                if (kvp.Value.AccessTime < oldestTime)
                {
                    oldestTime = kvp.Value.AccessTime;
                    oldestKey = kvp.Key;
                }
            }
            
            if (oldestKey != null && _tileCache.TryRemove(oldestKey, out var removed))
            {
                removed.Bitmap.Dispose();
            }
        }
        
        private void ClearCacheForDateChange()
        {
            lock (_cacheLock)
            {
                foreach (var entry in _tileCache.Values)
                {
                    entry.Bitmap.Dispose();
                }
                _tileCache.Clear();
                _maskCache.Clear();
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
        
        private string? FindCShapesFile()
        {
            // Check user Documents data directory first
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "data", "country_borders");
                
            string primaryPath = Path.Combine(userDataPath, "CShapes-2.0.shp");
            if (File.Exists(primaryPath))
            {
                Debug.WriteLine($"Found CShapes file at: {primaryPath}");
                return primaryPath;
            }
            
            // Also check for ne_10m_admin_0_countries.shp as fallback
            string fallbackPath = Path.Combine(userDataPath, "ne_10m_admin_0_countries.shp");
            if (File.Exists(fallbackPath))
            {
                Debug.WriteLine($"Found fallback shapefile at: {fallbackPath}");
                return fallbackPath;
            }
            
            // Check additional common locations
            string[] possiblePaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "CShapes-2.0.shp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "ne_10m_admin_0_countries.shp"),
                "data/country_borders/CShapes-2.0.shp",
                "data/country_borders/ne_10m_admin_0_countries.shp",
                "data/CShapes-2.0.shp",
                "data/ne_10m_admin_0_countries.shp",
                "../data/country_borders/CShapes-2.0.shp",
                "../data/country_borders/ne_10m_admin_0_countries.shp"
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.WriteLine($"Found shapefile at: {path}");
                    return path;
                }
            }
            
            Debug.WriteLine("No political boundary files found. Checked paths:");
            Debug.WriteLine($"  Primary: {primaryPath}");
            Debug.WriteLine($"  Fallback: {fallbackPath}");
            foreach (string path in possiblePaths)
            {
                Debug.WriteLine($"  Alternative: {path}");
            }
            
            return null;
        }
        
        private SKSizeI GetMapSize(int zoomLevel)
        {
            // Use base dimensions for coordinate calculations (consistent with terrain system)
            return new SKSizeI(_baseWidth, _baseHeight);
        }
        
        private int GetCellSizeForZoom(int zoomLevel)
        {
            // Use simpler scaling approach that was working before
            // This gives reasonable zoom levels: 1, 2, 4, 8, 16, 32...
            return Math.Max(1, 1 << Math.Min(zoomLevel, 6));
        }
        
        public void Dispose()
        {
            ClearCacheForDateChange();
            ThreadLocalRandom.Dispose();
        }
    }
}