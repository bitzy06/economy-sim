using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using NetTopologySuite.Geometries;

namespace StrategyGame
{
    /// <summary>
    /// Base class for vector-based tile rendering with GPU acceleration support
    /// </summary>
    public abstract class VectorTileRenderer : IDisposable
    {
        protected readonly int _baseWidth;
        protected readonly int _baseHeight;
        protected const int TileSizePx = 512;
        protected const int MaxCacheSize = 100;
        
        // GPU acceleration support
        protected static readonly GRContext? _grContext;
        protected static readonly bool _gpuAvailable;
        
        // Vector tile cache - stores geometric data instead of raster pixels
        protected readonly ConcurrentDictionary<string, VectorTile> _vectorTileCache = new();
        protected readonly ConcurrentDictionary<string, Task<VectorTile?>> _inFlightTasks = new();
        protected readonly object _cacheLock = new object();
        protected long _cacheAccessCounter = 0;
        
        static VectorTileRenderer()
        {
            try
            {
                // Test SkiaSharp basic functionality first
                using var testBitmap = new SKBitmap(1, 1);
                using var testCanvas = new SKCanvas(testBitmap);
                testCanvas.Clear(SKColors.White);
                
                // Only try GPU if basic SkiaSharp works
                _grContext = GRContext.CreateGl();
                _gpuAvailable = _grContext != null;
                Debug.WriteLine($"SkiaSharp initialization successful. GPU acceleration available: {_gpuAvailable}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SkiaSharp/GPU initialization failed: {ex.Message}");
                _gpuAvailable = false;
                _grContext = null;
            }
        }
        
        /// <summary>
        /// Check if GPU acceleration is available
        /// </summary>
        public static bool IsGpuAccelerationAvailable => _gpuAvailable;
        
        protected VectorTileRenderer(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
        }
        
        /// <summary>
        /// Assembles a view from vector tiles with GPU acceleration when available
        /// </summary>
        public virtual SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                int cellSize = GetCellSizeForZoom(zoomLevel);
                
                // Calculate tile boundaries
                int tileStartX = viewArea.Left / TileSizePx;
                int tileStartY = viewArea.Top / TileSizePx;
                int tileEndX = (viewArea.Right + TileSizePx - 1) / TileSizePx;
                int tileEndY = (viewArea.Bottom + TileSizePx - 1) / TileSizePx;
                
                // Create surface with GPU acceleration if available
                SKSurface? surface = null;
                var info = new SKImageInfo(viewArea.Width, viewArea.Height);
                
                try
                {
                    if (_gpuAvailable && _grContext != null)
                    {
                        surface = SKSurface.Create(_grContext, false, info);
                        if (surface != null)
                        {
                            Debug.WriteLine("Using GPU-accelerated surface");
                        }
                    }
                    
                    // Fallback to CPU surface if GPU fails or unavailable
                    if (surface == null)
                    {
                        surface = SKSurface.Create(info);
                        Debug.WriteLine("Using CPU surface");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to create SkiaSharp surface: {ex.Message}");
                    surface = null;
                }
                
                if (surface == null)
                {
                    Debug.WriteLine("Creating fallback error bitmap due to SkiaSharp failure");
                    return CreateSkiaSharpErrorFallback(viewArea.Width, viewArea.Height);
                }
                
                using (surface)
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(GetBackgroundColor());
                    
                    int tilesRendered = 0;
                    int tilesRequested = (tileEndY - tileStartY) * (tileEndX - tileStartX);
                    
                    Debug.WriteLine($"Rendering {tilesRequested} tiles from ({tileStartX},{tileStartY}) to ({tileEndX},{tileEndY})");
                    
                    // Render tiles
                    for (int ty = tileStartY; ty < tileEndY; ty++)
                    {
                        for (int tx = tileStartX; tx < tileEndX; tx++)
                        {
                            int destX = tx * TileSizePx - viewArea.Left;
                            int destY = ty * TileSizePx - viewArea.Top;
                            
                            var vectorTile = GetVectorTileSync(cellSize, tx, ty);
                            if (vectorTile != null)
                            {
                                RenderVectorTile(canvas, vectorTile, destX, destY, cellSize);
                                tilesRendered++;
                            }
                            else
                            {
                                // Draw a placeholder so we can see that the tile area exists
                                using var placeholderPaint = new SKPaint
                                {
                                    Color = new SKColor(255, 200, 200, 100), // Light red transparent
                                    Style = SKPaintStyle.Fill
                                };
                                canvas.DrawRect(destX, destY, TileSizePx, TileSizePx, placeholderPaint);
                                
                                // Trigger async loading for next frame
                                _ = GetVectorTileAsync(cellSize, tx, ty, onTileReady);
                            }
                        }
                    }
                    
                    Debug.WriteLine($"Rendered {tilesRendered}/{tilesRequested} tiles immediately");
                    
                    // Draw debug border around the entire view area
                    using var debugPaint = new SKPaint
                    {
                        Color = SKColors.Yellow,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = 2
                    };
                    canvas.DrawRect(0, 0, viewArea.Width - 1, viewArea.Height - 1, debugPaint);
                    
                    // Create result bitmap
                    var result = new SKBitmap(info);
                    surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
                    
                    Debug.WriteLine($"Vector view assembled in {sw.ElapsedMilliseconds}ms");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling vector view: {ex.Message}");
                return CreateErrorPlaceholder(viewArea.Width, viewArea.Height);
            }
        }
        
        protected VectorTile? GetVectorTileSync(int cellSize, int tileX, int tileY)
        {
            string cacheKey = GetCacheKey(cellSize, tileX, tileY);
            
            if (_vectorTileCache.TryGetValue(cacheKey, out var cached))
            {
                // Update access time for LRU
                cached.AccessTime = Interlocked.Increment(ref _cacheAccessCounter);
                return cached;
            }
            
            // For the center tiles of the view, generate synchronously to avoid blank screen
            // This ensures the user sees something immediately rather than waiting for async loading
            try
            {
                var sw = Stopwatch.StartNew();
                var vectorTile = LoadVectorDataForTile(cellSize, tileX, tileY).GetAwaiter().GetResult();
                
                if (vectorTile != null)
                {
                    CacheVectorTile(cacheKey, vectorTile);
                    Debug.WriteLine($"Synchronously generated tile ({tileX}, {tileY}) in {sw.ElapsedMilliseconds}ms");
                }
                
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error synchronously generating tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        protected async Task<VectorTile?> GetVectorTileAsync(int cellSize, int tileX, int tileY, Action? onComplete = null)
        {
            string cacheKey = GetCacheKey(cellSize, tileX, tileY);
            
            // Check if already in progress
            if (_inFlightTasks.TryGetValue(cacheKey, out var existingTask))
            {
                return await existingTask;
            }
            
            // Create new async task
            var task = Task.Run(() => GenerateVectorTileAsync(cellSize, tileX, tileY, cacheKey, onComplete));
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
        
        protected virtual async Task<VectorTile?> GenerateVectorTileAsync(int cellSize, int tileX, int tileY, string cacheKey, Action? onComplete)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var vectorTile = await LoadVectorDataForTile(cellSize, tileX, tileY);
                
                if (vectorTile != null)
                {
                    CacheVectorTile(cacheKey, vectorTile);
                    onComplete?.Invoke();
                }
                
                Debug.WriteLine($"Vector tile ({tileX}, {tileY}) generated in {sw.ElapsedMilliseconds}ms");
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating vector tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        protected void CacheVectorTile(string cacheKey, VectorTile vectorTile)
        {
            lock (_cacheLock)
            {
                // Implement LRU eviction if cache is full
                if (_vectorTileCache.Count >= MaxCacheSize)
                {
                    EvictOldestCacheEntry();
                }
                
                vectorTile.AccessTime = Interlocked.Increment(ref _cacheAccessCounter);
                _vectorTileCache.TryAdd(cacheKey, vectorTile);
            }
        }
        
        protected void EvictOldestCacheEntry()
        {
            string? oldestKey = null;
            long oldestTime = long.MaxValue;
            
            foreach (var kvp in _vectorTileCache)
            {
                if (kvp.Value.AccessTime < oldestTime)
                {
                    oldestTime = kvp.Value.AccessTime;
                    oldestKey = kvp.Key;
                }
            }
            
            if (oldestKey != null)
            {
                _vectorTileCache.TryRemove(oldestKey, out _);
            }
        }
        
        protected virtual string GetCacheKey(int cellSize, int tileX, int tileY)
        {
            return $"vector_{cellSize}_{tileX}_{tileY}";
        }
        
        protected int GetCellSizeForZoom(int zoomLevel)
        {
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1);
            return MultiResolutionMapManager.PixelsPerCellLevels[index];
        }
        
        protected SKBitmap CreateErrorPlaceholder(int width, int height)
        {
            try
            {
                var bitmap = new SKBitmap(width, height);
                using var canvas = new SKCanvas(bitmap);
                
                canvas.Clear(new SKColor(240, 220, 220, 255)); // Light red background
                
                using var paint = new SKPaint
                {
                    Color = new SKColor(180, 60, 60, 255),
                    TextSize = Math.Min(width, height) / 15f,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                
                string message = "Vector data error";
                float x = width / 2f;
                float y = height / 2f;
                
                canvas.DrawText(message, x, y, paint);
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create error placeholder: {ex.Message}");
                return CreateSimpleFallback(width, height);
            }
        }
        
        protected SKBitmap CreateSkiaSharpErrorFallback(int width, int height)
        {
            try
            {
                // Try creating a basic bitmap to indicate SkiaSharp failure
                var bitmap = new SKBitmap(width, height);
                using var canvas = new SKCanvas(bitmap);
                
                // Use a bright color to indicate the graphics system failure
                canvas.Clear(new SKColor(255, 100, 100, 255)); // Bright red
                
                using var paint = new SKPaint
                {
                    Color = SKColors.White,
                    TextSize = Math.Min(width, height) / 10f,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                
                string message = "SkiaSharp initialization failed";
                float x = width / 2f;
                float y = height / 2f;
                
                canvas.DrawText(message, x, y, paint);
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Complete SkiaSharp failure: {ex.Message}");
                return CreateSimpleFallback(width, height);
            }
        }
        
        protected SKBitmap CreateSimpleFallback(int width, int height)
        {
            try
            {
                // Most basic fallback - just create an empty bitmap
                var bitmap = new SKBitmap(width, height);
                // Don't even try to draw on it if SkiaSharp is broken
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Even simple bitmap creation failed: {ex.Message}");
                // Return null to indicate complete failure
                return null!;
            }
        }
        
        // Abstract methods to be implemented by derived classes
        protected abstract Task<VectorTile?> LoadVectorDataForTile(int cellSize, int tileX, int tileY);
        protected abstract void RenderVectorTile(SKCanvas canvas, VectorTile vectorTile, int destX, int destY, int cellSize);
        protected abstract SKColor GetBackgroundColor();
        
        // Public methods for testing
        public VectorTile? GetVectorTileForTesting(int tileX, int tileY, int cellSize) => GetVectorTileSync(cellSize, tileX, tileY);
        public void RenderVectorTileForTesting(VectorTile vectorTile, SKCanvas canvas, int width, int height) => 
            RenderVectorTile(canvas, vectorTile, 0, 0, 1);
        
        public virtual void Dispose()
        {
            lock (_cacheLock)
            {
                _vectorTileCache.Clear();
            }
        }
    }
    
    /// <summary>
    /// Represents a vector tile containing geometric data instead of raster pixels
    /// </summary>
    public class VectorTile
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int CellSize { get; set; }
        public long AccessTime { get; set; }
        
        // Geometric data for rendering
        public List<VectorFeature> Features { get; set; } = new();
        
        // Bounding box for spatial queries
        public SKRect Bounds { get; set; }
    }
    
    /// <summary>
    /// Represents a vector feature with geometry and styling
    /// </summary>
    public class VectorFeature
    {
        public NetTopologySuite.Geometries.Geometry? Geometry { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public VectorStyle Style { get; set; } = new();
    }
    
    /// <summary>
    /// Customizable styling for vector features
    /// </summary>
    public class VectorStyle
    {
        public SKColor FillColor { get; set; } = SKColors.Transparent;
        public SKColor StrokeColor { get; set; } = SKColors.Black;
        public float StrokeWidth { get; set; } = 1.0f;
        public bool IsVisible { get; set; } = true;
        public float Opacity { get; set; } = 1.0f;
        
        // Advanced styling options
        public SKShader? FillShader { get; set; }
        public SKPathEffect? PathEffect { get; set; }
        public float[] DashArray { get; set; } = Array.Empty<float>();
        
        public SKPaint CreateFillPaint()
        {
            var paint = new SKPaint
            {
                Color = FillColor.WithAlpha((byte)(255 * Opacity)),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            
            if (FillShader != null)
            {
                paint.Shader = FillShader;
            }
            
            return paint;
        }
        
        public SKPaint CreateStrokePaint()
        {
            var paint = new SKPaint
            {
                Color = StrokeColor.WithAlpha((byte)(255 * Opacity)),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = StrokeWidth,
                IsAntialias = true
            };
            
            if (PathEffect != null)
            {
                paint.PathEffect = PathEffect;
            }
            else if (DashArray.Length > 0)
            {
                paint.PathEffect = SKPathEffect.CreateDash(DashArray, 0);
            }
            
            return paint;
        }
    }
}