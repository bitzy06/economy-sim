using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;
using OSGeo.OGR;

namespace Economy_sim
{
    /// <summary>
    /// High-performance tile-based political map manager with spatial indexing optimizations
    /// </summary>
    public partial class PoliticalTileManager : IDisposable
    {
        private readonly PoliticalBorderManager _politicalManager;
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);

        // Grid-based rendering system
        private readonly GridControlEngine _gridEngine;
        private readonly GridRenderer _gridRenderer;
        private readonly GridPopulator _gridPopulator;
        private bool _gridInitialized = false;
        private readonly object _gridLock = new object();

        // Data source for clean, non-overlapping country data and colors for the current date.
        private PoliticalDataCache _dataCache;

        // Performance optimizations with spatial indexing (legacy - kept for compatibility)
        private const int TileSizePx = 512;
        private const int MaxCacheSize = 100; // Increased cache size for better performance

        // Spatial index for fast country lookup (legacy)
        private readonly PoliticalSpatialIndex _spatialIndex;
        private readonly OptimizedPoliticalMaskGenerator _maskGenerator;
        private bool _spatialIndexBuilt = false;
        private readonly object _indexLock = new object();

        // Selected country for white border highlighting
        private IndexedCountryFeature? _selectedCountry = null;
        private readonly object _selectionLock = new object();
        private int _selectedRasterCode = -1; // Cached selected country's raster code for fast comparisons

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

        private sealed class CacheEntry
        {
            public required string Key { get; init; }
            public required SKBitmap Bitmap { get; init; }
            public long AccessTime { get; set; }
            public DateTime CreatedForDate { get; set; }
            public int RefCount { get; set; }
            public bool DisposeRequested { get; set; }
            public bool Disposed { get; set; }
        }

        private sealed class TileLease : IDisposable
        {
            private readonly PoliticalTileManager _owner;
            private CacheEntry? _entry;
            public SKBitmap Bitmap { get; }

            public TileLease(PoliticalTileManager owner, CacheEntry entry)
            {
                _owner = owner;
                _entry = entry;
                Bitmap = entry.Bitmap;
            }

            public void Dispose()
            {
                var e = Interlocked.Exchange(ref _entry, null);
                if (e != null)
                {
                    _owner.ReleaseLease(e);
                }
            }
        }

        public PoliticalTileManager(PoliticalBorderManager politicalManager, int baseWidth, int baseHeight)
        {
            _politicalManager = politicalManager;
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;

            // Initialize grid-based rendering system
            _gridEngine = new GridControlEngine(baseWidth, baseHeight, TileSizePx);
            _gridPopulator = new GridPopulator(politicalManager);

            // Initialize date-specific cache for clean data and color lookup
            _dataCache = new PoliticalDataCache(_politicalMapDate);
            _gridRenderer = new GridRenderer(_gridEngine, _dataCache);

            // Initialize spatial optimization components
            _spatialIndex = new PoliticalSpatialIndex();
            _maskGenerator = new OptimizedPoliticalMaskGenerator(_spatialIndex);
        }

        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;

                // Recreate date-specific cache for clean country data and colors
                _dataCache = new PoliticalDataCache(_politicalMapDate);

                // Mark grid as needing reinitialization for new date
                lock (_gridLock)
                {
                    _gridInitialized = false;
                }

                ClearCacheForDateChange();

                // Mark spatial index as needing rebuild for new date (legacy)
                lock (_indexLock)
                {
                    _spatialIndexBuilt = false;
                }
            }
        }

        /// <summary>
        /// Sets the selected country for white border highlighting
        /// </summary>
        public void SetSelectedCountry(IndexedCountryFeature? country)
        {
            lock (_selectionLock)
            {
                _selectedCountry = country;
                _selectedRasterCode = country?.RasterCode ?? -1; // Cache raster code for fast comparisons

                // Clear only tiles in view by marking them dirty via grid engine; fall back to full clear
                try
                {
                    if (_gridEngine.HasDirtyTiles())
                    {
                        // If engine tracks dirty tiles, clear those tiles specifically
                        var dirty = _gridEngine.GetDirtyTiles(clearAfterGet: true);
                        ClearTilesFromCache(dirty);
                    }
                    else
                    {
                        ClearTileCache();
                    }
                }
                catch
                {
                    ClearTileCache();
                }
            }
        }

        /// <summary>
        /// Change control of specific cells (war mechanics)
        /// </summary>
        public void ChangeControl(int countryId, IEnumerable<System.Drawing.Point> cells)
        {
            EnsureGridInitialized();
            _gridEngine.ChangeControl(countryId, cells);
            
            // Clear affected tiles from cache
            var affectedTiles = _gridEngine.GetTileCoordinates(cells);
            ClearTilesFromCache(affectedTiles);
        }

        /// <summary>
        /// Zero-sum change: only reassign border cells adjacent to the target country.
        /// Returns list of changed cells with previous IDs for undo.
        /// </summary>
        public List<(Point cell, int previousId)> ChangeControlZeroSum(int targetCountryId, IEnumerable<Point> brushCells)
        {
            EnsureGridInitialized();
            var changes = new List<(Point cell, int previousId)>();
            var grid = _gridEngine.ControlGrid;
            int w = grid.GetLength(1), h = grid.GetLength(0);

            foreach (var c in brushCells)
            {
                int x = c.X, y = c.Y;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int cur = grid[y, x];
                if (cur <= 0 || cur == targetCountryId) continue;

                int nx0 = x + 1 < w ? grid[y, x + 1] : 0;
                int nx1 = x - 1 >= 0 ? grid[y, x - 1] : 0;
                int ny0 = y + 1 < h ? grid[y + 1, x] : 0;
                int ny1 = y - 1 >= 0 ? grid[y - 1, x] : 0;
                bool isBorder = nx0 != cur || nx1 != cur || ny0 != cur || ny1 != cur;
                bool adjacentToTarget = nx0 == targetCountryId || nx1 == targetCountryId || ny0 == targetCountryId || ny1 == targetCountryId;
                if (isBorder && adjacentToTarget)
                {
                    changes.Add((new Point(x, y), cur));
                }
            }

            foreach (var (pt, _) in changes)
            {
                grid[pt.Y, pt.X] = targetCountryId;
            }

            if (changes.Count > 0)
            {
                var cells = changes.Select(ch => ch.cell);
                var affectedTiles = _gridEngine.GetTileCoordinates(cells);
                _gridEngine.MarkCellsDirty(cells);
                ClearTilesFromCache(affectedTiles);
            }

            return changes;
        }

        /// <summary>
        /// Flood fill control from a seed point (war mechanics)
        /// </summary>
        public void FloodFillControl(System.Drawing.Point seed, int newCountryId, Func<int, bool> canReplace)
        {
            EnsureGridInitialized();
            _gridEngine.FloodFillControl(seed, newCountryId, canReplace);
            
            // Clear all tiles from cache since flood fill can affect many tiles
            ClearTileCache();
        }

        /// <summary>
        /// Compute frontline cells (differences between base and control grids)
        /// </summary>
        public IReadOnlyList<System.Drawing.Point> ComputeFrontline()
        {
            EnsureGridInitialized();
            return _gridEngine.ComputeFrontline();
        }

        /// <summary>
        /// Get country at specific geographic coordinate
        /// </summary>
        public int GetCountryAtGeographic(double longitude, double latitude)
        {
            EnsureGridInitialized();
            return _gridRenderer.GetCountryAtGeographic(longitude, latitude);
        }

        /// <summary>
        /// Clears the tile cache to force re-rendering
        /// </summary>
        private void ClearTileCache()
        {
            List<CacheEntry> toDispose = new();
            lock (_cacheLock)
            {
                foreach (var kv in _tileCache)
                {
                    if (_tileCache.TryRemove(kv.Key, out var entry))
                    {
                        if (entry.RefCount == 0 && !entry.Disposed)
                        {
                            toDispose.Add(entry);
                        }
                        else
                        {
                            entry.DisposeRequested = true;
                        }
                    }
                }
            }

            foreach (var e in toDispose)
            {
                e.Disposed = true;
                e.Bitmap.Dispose();
            }
        }

        /// <summary>
        /// Clear specific tiles from cache
        /// </summary>
        private void ClearTilesFromCache(IEnumerable<(int tileX, int tileY)> tiles)
        {
            List<CacheEntry> toDispose = new();
            lock (_cacheLock)
            {
                foreach (var (tileX, tileY) in tiles)
                {
                    var keysToRemove = _tileCache.Keys.Where(key => key.Contains($"_{tileX}_{tileY}_")).ToList();
                    
                    foreach (var key in keysToRemove)
                    {
                        if (_tileCache.TryRemove(key, out var entry))
                        {
                            if (entry.RefCount == 0 && !entry.Disposed)
                            {
                                toDispose.Add(entry);
                            }
                            else
                            {
                                entry.DisposeRequested = true;
                            }
                        }
                    }
                }
            }

            foreach (var e in toDispose)
            {
                e.Disposed = true;
                e.Bitmap.Dispose();
            }
        }

        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            return AssembleView(zoomLevel, viewArea, onTileReady, forceSync: false);
        }

        /// <summary>
        /// Assemble view with option to force synchronous tile generation
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady, bool forceSync)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                int cellSize = GetCellSizeForZoom(zoomLevel);

                Debug.WriteLine($"PoliticalTileManager.AssembleView: zoomLevel={zoomLevel}, cellSize={cellSize}, viewArea={viewArea}");

                // Ensure grid is initialized before proceeding (not just inside tile generation)
                EnsureGridInitialized();

                // Periodic cleanup to prevent memory growth
                if (_cacheAccessCounter % 100 == 0) // Every 100 tile accesses
                {
                    PurgeCompletedTasks();
                    ClearLegacyMaskCache();
                    LogCacheMetrics();
                }

                // Convert viewArea to grid space
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                float scaleX = (float)_baseWidth / scaledMapWidth;
                float scaleY = (float)_baseHeight / scaledMapHeight;
                int gridViewLeft = (int)(viewArea.Left * scaleX);
                int gridViewTop = (int)(viewArea.Top * scaleY);
                int gridViewRight = (int)(viewArea.Right * scaleX);
                int gridViewBottom = (int)(viewArea.Bottom * scaleY);
                var gridViewArea = new SKRectI(gridViewLeft, gridViewTop, gridViewRight, gridViewBottom);

                Debug.WriteLine($"PoliticalTileManager.AssembleView: scaled map={scaledMapWidth}x{scaledMapHeight}, grid={_baseWidth}x{_baseHeight}");
                Debug.WriteLine($"PoliticalTileManager.AssembleView: viewArea transform: {viewArea} -> {gridViewArea}");

                // Calculate tile boundaries for the grid view area
                int tileStartX = gridViewArea.Left / TileSizePx;
                int tileStartY = gridViewArea.Top / TileSizePx;
                int tileEndX = (gridViewArea.Right + TileSizePx - 1) / TileSizePx;
                int tileEndY = (gridViewArea.Bottom + TileSizePx - 1) / TileSizePx;

                Debug.WriteLine($"PoliticalTileManager.AssembleView: grid tiles from ({tileStartX},{tileStartY}) to ({tileEndX},{tileEndY})");

                // Get the LOD-specific grid to check its actual dimensions
                int lodLevel = GetLodLevelForCellSize(cellSize);
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);
                int maxTileX = (gridWidth + TileSizePx - 1) / TileSizePx;
                int maxTileY = (gridHeight + TileSizePx - 1) / TileSizePx;

                Debug.WriteLine($"PoliticalTileManager.AssembleView: LOD={lodLevel}, grid={gridWidth}x{gridHeight}, maxTiles={maxTileX}x{maxTileY}");

                // Adjust view and tile ranges for the current LOD scale
                int lodScale = 1 << lodLevel;
                var gridViewAreaLod = new SKRectI(
                    gridViewArea.Left / lodScale,
                    gridViewArea.Top / lodScale,
                    gridViewArea.Right / lodScale,
                    gridViewArea.Bottom / lodScale);

                int lodTileStartX = Math.Clamp(gridViewAreaLod.Left / TileSizePx, 0, Math.Max(0, maxTileX - 1));
                int lodTileStartY = Math.Clamp(gridViewAreaLod.Top / TileSizePx, 0, Math.Max(0, maxTileY - 1));
                int lodTileEndX = Math.Clamp((gridViewAreaLod.Right + TileSizePx - 1) / TileSizePx, 0, maxTileX);
                int lodTileEndY = Math.Clamp((gridViewAreaLod.Bottom + TileSizePx - 1) / TileSizePx, 0, maxTileY);

                Debug.WriteLine($"PoliticalTileManager.AssembleView: adjusted for LOD -> view {gridViewAreaLod} tiles ({lodTileStartX},{lodTileStartY}) to ({lodTileEndX},{lodTileEndY})");

                // Create composite bitmap at the original viewArea size
                var info = new SKImageInfo(viewArea.Width, viewArea.Height);
                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(135, 206, 235, 255)); // Light blue background

                for (int ty = lodTileStartY; ty < lodTileEndY; ty++)
                {
                    for (int tx = lodTileStartX; tx < lodTileEndX; tx++)
                    {
                        int tileX = tx, tileY = ty;
                        int gridTileLeftBase = tileX * TileSizePx * lodScale;
                        int gridTileTopBase = tileY * TileSizePx * lodScale;
                        int scaledTileLeft = (int)(gridTileLeftBase / scaleX);
                        int scaledTileTop = (int)(gridTileTopBase / scaleY);
                        int scaledTileWidth = (int)((TileSizePx * lodScale) / scaleX);
                        int scaledTileHeight = (int)((TileSizePx * lodScale) / scaleY);
                        int destX = scaledTileLeft - viewArea.Left;
                        int destY = scaledTileTop - viewArea.Top;

                        using var lease = AcquireTileLease(cellSize, tileX, tileY);
                        if (lease?.Bitmap != null && !lease.Bitmap.IsNull && !lease.Bitmap.IsEmpty)
                        {
                            var destRect = SKRect.Create(destX, destY, scaledTileWidth, scaledTileHeight);
                            canvas.DrawBitmap(lease.Bitmap, destRect);
                        }
                        else
                        {
                            if (forceSync)
                            {
                                string cacheKey = $"lod{lodLevel}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";
                                var bitmap = GenerateTileFromGrid(cellSize, tileX, tileY, cacheKey, onTileReady);
                                if (bitmap != null)
                                {
                                    using var lease2 = AcquireTileLease(cellSize, tileX, tileY);
                                    if (lease2?.Bitmap != null && !lease2.Bitmap.IsNull && !lease2.Bitmap.IsEmpty)
                                    {
                                        var destRect = SKRect.Create(destX, destY, scaledTileWidth, scaledTileHeight);
                                        canvas.DrawBitmap(lease2.Bitmap, destRect);
                                    }
                                }
                            }
                            else
                            {
                                _ = GetTileAsync(cellSize, tileX, tileY, onTileReady);
                            }
                        }
                    }
                }

                var result = new SKBitmap(info);
                surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);

                Debug.WriteLine($"Political view assembled in {sw.ElapsedMilliseconds}ms (sync: {forceSync})");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling political view: {ex.Message}");
                return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
            }
        }

        private TileLease? AcquireTileLease(int cellSize, int tileX, int tileY)
        {
            int lodLevel = GetLodLevelForCellSize(cellSize);
            string cacheKey = $"lod{lodLevel}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";

            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(cacheKey, out var entry) && !entry.Disposed)
                {
                    entry.AccessTime = Interlocked.Increment(ref _cacheAccessCounter);
                    entry.RefCount++;
                    return new TileLease(this, entry);
                }
            }
            return null;
        }

        private void ReleaseLease(CacheEntry entry)
        {
            bool disposeNow = false;

            lock (_cacheLock)
            {
                if (entry.Disposed)
                    return;

                entry.RefCount = Math.Max(0, entry.RefCount - 1);

                if (entry.RefCount == 0 && entry.DisposeRequested)
                {
                    entry.Disposed = true;
                    disposeNow = true;
                }
            }

            if (disposeNow)
            {
                entry.Bitmap.Dispose();
            }
        }

        private async Task<SKBitmap?> GetTileAsync(int cellSize, int tileX, int tileY, Action? onComplete = null)
        {
            int lodLevel = GetLodLevelForCellSize(cellSize);
            string cacheKey = $"lod{lodLevel}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";

            if (_inFlightTasks.TryGetValue(cacheKey, out var existingTask))
            {
                return await existingTask;
            }

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
            return GenerateTileFromGrid(cellSize, tileX, tileY, cacheKey, onComplete);
        }

        private SKBitmap? GenerateTileFromGrid(int cellSize, int tileX, int tileY, string cacheKey, Action? onComplete)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                Debug.WriteLine($"GenerateTileFromGrid: Starting generation for tile ({tileX}, {tileY}) at cellSize={cellSize}");
                EnsureGridInitialized();
                int lodLevel = GetLodLevelForCellSize(cellSize);
                var controlGrid = _gridEngine.GetControlGridLod(lodLevel);
                int gridWidth = controlGrid.GetLength(1);
                int gridHeight = controlGrid.GetLength(0);
                int maxTileX = (gridWidth + TileSizePx - 1) / TileSizePx;
                int maxTileY = (gridHeight + TileSizePx - 1) / TileSizePx;

                Debug.WriteLine($"GenerateTileFromGrid: LOD={lodLevel}, grid={gridWidth}x{gridHeight}, maxTiles={maxTileX}x{maxTileY}");

                if (tileX >= maxTileX || tileY >= maxTileY)
                {
                    Debug.WriteLine($"GenerateTileFromGrid: Tile ({tileX}, {tileY}) is beyond grid bounds. Grid: {gridWidth}x{gridHeight}, Max tiles: {maxTileX}x{maxTileY}");
                    var waterTile = new SKBitmap(TileSizePx, TileSizePx, SKColorType.Rgba8888, SKAlphaType.Opaque);
                    waterTile.Erase(new SKColor(135, 206, 235, 255));
                    CacheTile(cacheKey, waterTile);
                    onComplete?.Invoke();
                    Debug.WriteLine($"GenerateTileFromGrid: Created water tile for out-of-bounds");
                    return waterTile;
                }

                int selectedCountryId = -1;
                lock (_selectionLock)
                {
                    selectedCountryId = _selectedRasterCode;
                }

                Debug.WriteLine($"GenerateTileFromGrid: Calling GridRenderer.RenderGridTile({tileX}, {tileY}, {TileSizePx}, {selectedCountryId}, {lodLevel})");
                var bitmap = _gridRenderer.RenderGridTile(tileX, tileY, TileSizePx, selectedCountryId, lodLevel);

                if (bitmap != null)
                {
                    Debug.WriteLine($"GenerateTileFromGrid: GridRenderer returned bitmap {bitmap.Width}x{bitmap.Height}");
                    var centerPixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
                    var cornerPixel = bitmap.GetPixel(0, 0);
                    Debug.WriteLine($"GenerateTileFromGrid: Bitmap pixels - center: #{centerPixel.Red:X2}{centerPixel.Green:X2}{centerPixel.Blue:X2}, corner: #{cornerPixel.Red:X2}{cornerPixel.Green:X2}{cornerPixel.Blue:X2}");
                    CacheTile(cacheKey, bitmap);
                    onComplete?.Invoke();
                    Debug.WriteLine($"GenerateTileFromGrid: Cached tile successfully in {sw.ElapsedMilliseconds}ms");
                }
                else
                {
                    Debug.WriteLine($"GenerateTileFromGrid: GridRenderer returned NULL bitmap for tile ({tileX}, {tileY})");
                    var fallbackTile = new SKBitmap(TileSizePx, TileSizePx, SKColorType.Rgba8888, SKAlphaType.Opaque);
                    fallbackTile.Erase(new SKColor(135, 206, 235, 255));
                    CacheTile(cacheKey, fallbackTile);
                    onComplete?.Invoke();
                    Debug.WriteLine($"GenerateTileFromGrid: Created fallback water tile");
                    return fallbackTile;
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GenerateTileFromGrid: ERROR generating tile ({tileX}, {tileY}): {ex.Message}");
                Debug.WriteLine($"GenerateTileFromGrid: Stack trace: {ex.StackTrace}");
                return CreateUnavailablePlaceholder(TileSizePx, TileSizePx);
            }
        }

        private SKBitmap? GenerateTileAsyncLegacy(int cellSize, int tileX, int tileY, string cacheKey, Action? onComplete)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                int pixelX = tileX * TileSizePx;
                int pixelY = tileY * TileSizePx;
                int tileWidth = Math.Min(TileSizePx, scaledMapWidth - pixelX);
                int tileHeight = Math.Min(TileSizePx, scaledMapHeight - pixelY);
                if (tileWidth <= 0 || tileHeight <= 0)
                {
                    return null;
                }
                var tileMask = GetTileMask(cellSize, pixelX, pixelY, tileWidth, tileHeight);
                if (tileMask == null)
                {
                    return CreateUnavailablePlaceholder(tileWidth, tileHeight);
                }
                var bitmap = RenderPoliticalTileOptimized(tileMask, tileWidth, tileHeight);
                if (bitmap != null)
                {
                    CacheTile(cacheKey, bitmap);
                    onComplete?.Invoke();
                }
                Debug.WriteLine($"Legacy political tile ({tileX}, {tileY}) generated in {sw.ElapsedMilliseconds}ms");
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating legacy political tile ({tileX}, {tileY}): {ex.Message}");
                return CreateUnavailablePlaceholder(TileSizePx, TileSizePx);
            }
        }

        private SKBitmap? RenderPoliticalTileOptimized(int[,] mask, int width, int height)
        {
            try
            {
                var bitmap = new SKBitmap(width, height);
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
                    bitmap.Erase(new SKColor(135, 206, 235, 255));
                    return bitmap;
                }
                int selectedId;
                lock (_selectionLock)
                {
                    selectedId = _selectedRasterCode;
                }
                unsafe
                {
                    var pixelPtr = (uint*)bitmap.GetPixels().ToPointer();
                    int stride = bitmap.RowBytes / 4;
                    Parallel.For(0, height, y =>
                    {
                        var rng = ThreadLocalRandom.Value;
                        for (int x = 0; x < width; x++)
                        {
                            uint color;
                            if (y < mask.GetLength(0) && x < mask.GetLength(1))
                            {
                                int countryId = mask[y, x];
                                if (countryId == 0)
                                {
                                    color = 0xFF87CEEB; // water
                                }
                                else
                                {
                                    SKColor baseColor = _dataCache.GetCountryColorByRasterCode(countryId);
                                    int variation = rng.Next(-5, 6);
                                    byte r = (byte)Math.Clamp(baseColor.Red + variation, 0, 255);
                                    byte g = (byte)Math.Clamp(baseColor.Green + variation, 0, 255);
                                    byte b = (byte)Math.Clamp(baseColor.Blue + variation, 0, 255);
                                    color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);
                                }
                            }
                            else
                            {
                                color = 0xFF87CEEB;
                            }
                            pixelPtr[y * stride + x] = color;
                        }
                    });

                    uint blackBorder = 0xFF000000;
                    uint whiteBorder = 0xFFFFFFFF;
                    Parallel.For(0, height, y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (y >= mask.GetLength(0) || x >= mask.GetLength(1))
                                continue;
                            int countryId = mask[y, x];
                            if (countryId == 0)
                                continue;
                            bool draw = false;
                            bool drawWhite = false;
                            if (y > 0)
                            {
                                int n = mask[y - 1, x];
                                if (n != countryId)
                                {
                                    draw = true;
                                    if (countryId == selectedId || (n > 0 && n == selectedId))
                                        drawWhite = true;
                                }
                            }
                            if (!draw && y + 1 < mask.GetLength(0))
                            {
                                int n = mask[y + 1, x];
                                if (n != countryId)
                                {
                                    draw = true;
                                    if (countryId == selectedId || (n > 0 && n == selectedId))
                                        drawWhite = true;
                                }
                            }
                            if (!draw && x > 0)
                            {
                                int n = mask[y, x - 1];
                                if (n != countryId)
                                {
                                    draw = true;
                                    if (countryId == selectedId || (n > 0 && n == selectedId))
                                        drawWhite = true;
                                }
                            }
                            if (!draw && x + 1 < mask.GetLength(1))
                            {
                                int n = mask[y, x + 1];
                                if (n != countryId)
                                {
                                    draw = true;
                                    if (countryId == selectedId || (n > 0 && n == selectedId))
                                        drawWhite = true;
                                }
                            }
                            if (draw)
                            {
                                pixelPtr[y * stride + x] = drawWhite ? whiteBorder : blackBorder;
                            }
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

        private int[,]? GetTileMask(int cellSize, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            string maskKey = $"mask_{cellSize}_{pixelX}_{pixelY}_{tileWidth}_{tileHeight}_{_politicalMapDate:yyyyMMdd}";
            if (_maskCache.TryGetValue(maskKey, out var cached))
            {
                Debug.WriteLine($"Using cached political mask for tile ({pixelX}, {pixelY})");
                return cached;
            }
            try
            {
                EnsureSpatialIndexBuilt();
                var mask = _maskGenerator.GenerateOptimizedMask(
                    cellSize, pixelX, pixelY, tileWidth, tileHeight, _baseWidth, _baseHeight);
                if (mask == null)
                {
                    Debug.WriteLine($"Failed to generate optimized mask for tile ({pixelX}, {pixelY})");
                    return null;
                }
                bool hasData = false;
                for (int y = 0; y < mask.GetLength(0) && !hasData; y++)
                {
                    for (int x = 0; x < mask.GetLength(1) && !hasData; x++)
                    {
                        if (mask[y, x] > 0) hasData = true;
                    }
                }
                Debug.WriteLine($"Optimized political mask generated for tile ({pixelX}, {pixelY}): {(hasData ? "HAS DATA" : "NO DATA")}");
                if (mask != null && _maskCache.Count < MaxCacheSize * 3)
                {
                    _maskCache.TryAdd(maskKey, mask);
                    Debug.WriteLine($"Cached optimized political mask for tile ({pixelX}, {pixelY})");
                }
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating optimized tile mask: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private void EnsureSpatialIndexBuilt()
        {
            lock (_indexLock)
            {
                if (_spatialIndexBuilt) return;
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    throw new ApplicationException("CShapes file not found for spatial index generation");
                }
                Debug.WriteLine($"Ensuring data is ready for date: {_politicalMapDate:yyyy-MM-dd}");
                var cleanCountryData = _dataCache.GetOrGenerateCountryData(cshapesPath);
                _spatialIndex.BuildIndex(cshapesPath, cleanCountryData);
                _spatialIndexBuilt = true;
                Debug.WriteLine($"Spatial index built with {_spatialIndex.CountryCount} countries");
            }
        }

        private void EnsureGridInitialized()
        {
            lock (_gridLock)
            {
                if (_gridInitialized)
                {
                    Debug.WriteLine("Grid already initialized, skipping");
                    return;
                }
                Debug.WriteLine("Starting grid initialization...");

                // 1) Try to load pre-exported grid first (exe-relative export folder)
                if (TryLoadGridFromExport())
                {
                    _gridInitialized = true;
                    Debug.WriteLine("Grid initialization completed from export data");
                    return;
                }

                // 2) Fall back to shapefile pipeline
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    Debug.WriteLine("CShapes file not found, using test pattern for grid");
                    _gridPopulator.PopulateTestPattern(_gridEngine);
                }
                else
                {
                    Debug.WriteLine($"Found CShapes file at: {cshapesPath}");
                    Debug.WriteLine($"Initializing grid from shapefile for date: {_politicalMapDate:yyyy-MM-dd}");
                    Debug.WriteLine($"Grid engine dimensions: {_gridEngine.Width}x{_gridEngine.Height}");
                    try
                    {
                        _gridPopulator.PopulateFromShapefile(_gridEngine, cshapesPath, _politicalMapDate);
                        Debug.WriteLine($"Grid initialized successfully with {_gridEngine.Width}x{_gridEngine.Height} resolution");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to initialize grid from shapefile: {ex.Message}");
                        Debug.WriteLine($"Error stack trace: {ex.StackTrace}");
                        Debug.WriteLine("Falling back to test pattern");
                        _gridPopulator.PopulateTestPattern(_gridEngine);
                    }
                    try
                    {
                        _ = _dataCache.GetOrGenerateCountryData(cshapesPath);
                        Debug.WriteLine("Preloaded political color cache for initial render");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Warning: failed to preload political data cache: {ex.Message}");
                    }
                }

                if (_gridEngine.BaseOwnerGrid != null)
                {
                    var uniqueIds = new HashSet<int>();
                    int totalCells = _gridEngine.Width * _gridEngine.Height;
                    int nonWaterCells = 0;
                    for (int y = 0; y < _gridEngine.Height; y++)
                    {
                        for (int x = 0; x < _gridEngine.Width; x++)
                        {
                            int id = _gridEngine.BaseOwnerGrid[y, x];
                            uniqueIds.Add(id);
                            if (id > 0) nonWaterCells++;
                        }
                    }
                    Debug.WriteLine($"Grid populated with {uniqueIds.Count} unique country IDs from {totalCells} total cells");
                    Debug.WriteLine($"Non-water cells: {nonWaterCells} ({(nonWaterCells * 100.0 / totalCells):F1}%)");
                }
                else
                {
                    Debug.WriteLine("ERROR: Grid BaseOwnerGrid is null after initialization!");
                }

                _gridInitialized = true;
                Debug.WriteLine("Grid initialization completed");
            }
        }

        /// <summary>
        /// Attempt to load a pre-exported grid and country color mapping from an export directory.
        /// This allows running without shapefiles if an offline export is present.
        /// </summary>
        /// <returns>true if grid was loaded and initialized; false otherwise.</returns>
        private bool TryLoadGridFromExport()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                var candidates = new List<string>
                {
                    Path.Combine(baseDir, "export"),
                    Path.Combine(baseDir, "data", "export"),
                    Path.Combine(baseDir, "data", "country_borders", "export"),
                    Path.Combine(documents, "data", "country_borders", "export")
                };

                string? exportDir = candidates.FirstOrDefault(Directory.Exists);
                if (exportDir == null)
                {
                    Debug.WriteLine("[TryLoadGridFromExport] No export directory found.");
                    return false;
                }

                // Optionally load countries.json to seed colors/mappings
                string countriesJson = Path.Combine(exportDir, "countries.json");
                if (File.Exists(countriesJson))
                {
                    try
                    {
                        if (_dataCache.LoadFromCountriesJson(countriesJson))
                        {
                            Debug.WriteLine($"[TryLoadGridFromExport] Loaded countries.json from {countriesJson}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TryLoadGridFromExport] Failed to load countries.json: {ex.Message}");
                    }
                }

                // Look for a binary grid dump named grid_{WxH}.bin or grid.bin
                string wxh = $"{_gridEngine.Width}x{_gridEngine.Height}";
                var gridCandidates = new[]
                {
                    Path.Combine(exportDir, $"grid_{wxh}.bin"),
                    Path.Combine(exportDir, "grid.bin"),
                    Path.Combine(exportDir, $"base_grid_{wxh}.bin"),
                    Path.Combine(exportDir, "base_grid.bin")
                };

                string? gridPath = gridCandidates.FirstOrDefault(File.Exists);
                if (gridPath == null)
                {
                    Debug.WriteLine("[TryLoadGridFromExport] No exported grid file found.");
                    return false;
                }

                int width = _gridEngine.Width;
                int height = _gridEngine.Height;
                var grid = new int[height, width];

                using (var fs = File.OpenRead(gridPath))
                using (var br = new BinaryReader(fs))
                {
                    long expectedInts = (long)width * height;

                    // If file starts with a header (magic + w + h), detect it
                    // Try to read possible header safely
                    fs.Seek(0, SeekOrigin.Begin);
                    long remainingBytes = fs.Length;

                    // Heuristic: if file size equals expectedInts*4 -> raw data; 
                    // if larger, try to read 3 ints header (magic 0xBEEFBEEF, width, height)
                    if (remainingBytes >= (expectedInts * 4))
                    {
                        if (remainingBytes >= (expectedInts * 4) + 12)
                        {
                            int magic = br.ReadInt32();
                            int w = br.ReadInt32();
                            int h = br.ReadInt32();
                            if (magic != unchecked((int)0xBEEFBEEF) || w != width || h != height)
                            {
                                // Not a recognized header; reset to start
                                fs.Seek(0, SeekOrigin.Begin);
                            }
                        }
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (fs.Position + 4 > fs.Length)
                                throw new EndOfStreamException("Exported grid is smaller than expected");
                            grid[y, x] = br.ReadInt32();
                        }
                    }
                }

                _gridEngine.InitializeBaseGrid(grid);
                Debug.WriteLine($"[TryLoadGridFromExport] Loaded exported grid from {gridPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TryLoadGridFromExport] Failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Compute the political border mask for a given view area and zoom level.
        /// </summary>
        public int[,] ComputeBorderMask(SKRectI viewArea, int zoomLevel)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int maskWidth = viewArea.Width;
                int maskHeight = viewArea.Height;
                var mask = new int[maskHeight, maskWidth];

                Debug.WriteLine($"ComputeBorderMask: cellSize={cellSize}, viewArea={viewArea}");

                // Iterate over each cell in the view area
                Parallel.For(0, maskHeight, y =>
                {
                    for (int x = 0; x < maskWidth; x++)
                    {
                        // Map to global grid coordinates
                        int globalX = viewArea.Left + x;
                        int globalY = viewArea.Top + y;

                        // Skip out-of-bounds
                        if (globalX < 0 || globalY < 0 || globalX >= _gridEngine.Width || globalY >= _gridEngine.Height)
                            continue;

                        // Get the country ID from the control grid
                        int countryId = _gridEngine.ControlGrid[globalY, globalX];
                        mask[y, x] = countryId;

                        // If this is a border cell, ensure surrounding cells are also marked
                        if (IsBorderCell(globalX, globalY, countryId))
                        {
                            // TODO: Optimize - only mark directly adjacent cells
                            MarkSurroundingCellsAsBorder(mask, x, y, countryId);
                        }
                    }
                });

                Debug.WriteLine($"ComputeBorderMask completed in {sw.ElapsedMilliseconds}ms");
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ComputeBorderMask: {ex.Message}");
                return new int[viewArea.Height, viewArea.Width]; // Empty mask on error
            }
        }

        /// <summary>
        /// Check if a given cell is a border cell (surrounded by different country IDs).
        /// </summary>
        private bool IsBorderCell(int globalX, int globalY, int countryId)
        {
            // Simple check for land border - can be extended for sea borders, etc.
            return GetAdjacentCountryIds(globalX, globalY).Any(adjacentId => adjacentId != countryId && adjacentId != 0);
        }

        /// <summary>
        /// Mark surrounding cells as border in the mask.
        /// </summary>
        private void MarkSurroundingCellsAsBorder(int[,] mask, int centerX, int centerY, int countryId)
        {
            int maskWidth = mask.GetLength(1);
            int maskHeight = mask.GetLength(0);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip the center cell
                    int x = centerX + dx;
                    int y = centerY + dy;

                    // Skip out-of-bounds
                    if (x < 0 || y < 0 || x >= maskWidth || y >= maskHeight)
                        continue;

                    // If the adjacent cell is land (non-zero ID), mark as border
                    if (mask[y, x] != 0)
                    {
                        mask[y, x] = countryId;
                    }
                }
            }
        }

        /// <summary>
        /// Get the country IDs of adjacent cells (N, S, E, W) for a given cell.
        /// </summary>
        private IEnumerable<int> GetAdjacentCountryIds(int globalX, int globalY)
        {
            if (globalX > 0) // West
                yield return _gridEngine.ControlGrid[globalY, globalX - 1];
            if (globalX < _gridEngine.Width - 1) // East
                yield return _gridEngine.ControlGrid[globalY, globalX + 1];
            if (globalY > 0) // North
                yield return _gridEngine.ControlGrid[globalY - 1, globalX];
            if (globalY < _gridEngine.Height - 1) // South
                yield return _gridEngine.ControlGrid[globalY + 1, globalX];
        }

        public SKBitmap? CountryBoarderSelectAdd (SKBitmap bitmap, int[,] mask, int width, int height, Point mousepoint)
        {
            try
            {
                if (bitmap == null || mask == null)
                {
                    Debug.WriteLine("CountryBoarderSelectAdd: Bitmap or mask is null");
                    return bitmap;
                }
                if (mousepoint.X < 0 || mousepoint.X >= width || mousepoint.Y < 0 || mousepoint.Y >= height)
                {
                    Debug.WriteLine($"CountryBoarderSelectAdd: Mouse point {mousepoint} out of bounds (width={width}, height={height})");
                    return bitmap;
                }
                int maskY = Math.Min(mousepoint.Y, mask.GetLength(0) - 1);
                int maskX = Math.Min(mousepoint.X, mask.GetLength(1) - 1);
                int selectedCountryId = mask[maskY, maskX];
                Debug.WriteLine($"CountryBoarderSelectAdd: Selected country ID = {selectedCountryId} at position {mousepoint}");
                if (selectedCountryId == 0)
                {
                    Debug.WriteLine("CountryBoarderSelectAdd: Selected point is water (country ID = 0)");
                    return bitmap;
                }
                var newBitmap = bitmap.Copy();
                int borderPixelCount = 0;
                unsafe
                {
                    var pixelPtr = (uint*)newBitmap.GetPixels().ToPointer();
                    int stride = newBitmap.RowBytes / 4;
                    uint borderColor = 0xFFFFFFFF;
                    for (int y = 0; y < height; y++)
                    {
                        if (y >= mask.GetLength(0)) continue;
                        for (int x = 0; x < width; x++)
                        {
                            if (x >= mask.GetLength(1)) continue;
                            if (mask[y, x] == selectedCountryId)
                            {
                                bool isBorder = false;
                                if (y > 0 && y - 1 < mask.GetLength(0) && mask[y - 1, x] != selectedCountryId) { isBorder = true; }
                                else if (y < height - 1 && y + 1 < mask.GetLength(0) && mask[y + 1, x] != selectedCountryId) { isBorder = true; }
                                else if (x > 0 && x - 1 < mask.GetLength(1) && mask[y, x - 1] != selectedCountryId) { isBorder = true; }
                                else if (x < width - 1 && x + 1 < mask.GetLength(1) && mask[y, x + 1] != selectedCountryId) { isBorder = true; }
                                if (isBorder)
                                {
                                    pixelPtr[y * stride + x] = borderColor;
                                    borderPixelCount++;
                                }
                            }
                        }
                    }
                }
                int blockSize = 10;
                int startX = Math.Max(0, mousepoint.X - blockSize / 2);
                int startY = Math.Max(0, mousepoint.Y - blockSize / 2);
                int endX = Math.Min(width - 1, startX + blockSize);
                int endY = Math.Min(height - 1, startY + blockSize);
                Debug.WriteLine($"Drawing test block at mouse position ({mousepoint.X},{mousepoint.Y}), block bounds: ({startX},{startY}) to ({endX},{endY})");
                for (int y = startY; y <= endY; y++)
                {
                    for (int x = startX; x <= endY; x++)
                    {
                        newBitmap.SetPixel(x, y, 0xFFFF0000);
                    }
                }
                for (int i = -15; i <= 15; i++)
                {
                    int x = mousepoint.X + i;
                    int y = mousepoint.Y;
                    if (x >= 0 && x < width)
                        newBitmap.SetPixel(x, y, 0xFF00FFFF);
                    x = mousepoint.X;
                    y = mousepoint.Y + i;
                    if (y >= 0 && y < height)
                        newBitmap.SetPixel(x, y, 0xFF00FFFF);
                }
                Debug.WriteLine($"CountryBoarderSelectAdd: Added {borderPixelCount} border pixels for country ID {selectedCountryId}");
                return newBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CountryBoarderSelectAdd: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                return bitmap;
            }
        }

        private SKColor GenerateConsistentColor(string countryCode)
        {
            int hash = countryCode.GetHashCode();
            byte r = (byte)(100 + Math.Abs(hash % 156));
            byte g = (byte)(100 + Math.Abs((hash >> 8) % 156));
            byte b = (byte)(100 + Math.Abs((hash >> 16) % 156));
            return new SKColor(r, g, b, 255);
        }

        private bool IsBorderPixel(int[,] mask, int x, int y, int width, int height)
        {
            if (x >= mask.GetLength(1) || y >= mask.GetLength(0)) return false;
            int currentCountryId = mask[y, x];
            if (currentCountryId == 0) return false;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int newX = x + dx;
                    int newY = y + dy;
                    if (newX < 0 || newX >= mask.GetLength(1) || newY < 0 || newY >= mask.GetLength(0))
                    {
                        continue;
                    }
                    int neighborCountryId = mask[newY, newX];
                    if (neighborCountryId != currentCountryId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsSelectedCountryPixel(int countryId)
        {
            lock (_selectionLock)
            {
                if (_selectedCountry == null) return false;
                if (_selectedRasterCode != -1)
                {
                    return countryId == _selectedRasterCode;
                }
                var country = _spatialIndex.GetCountryByRasterCode(countryId);
                if (country == null) return false;
                return country.CountryCode == _selectedCountry.CountryCode;
            }
        }

        private void CacheTile(string cacheKey, SKBitmap bitmap)
        {
            var entry = new CacheEntry
            {
                Key = cacheKey,
                Bitmap = bitmap,
                AccessTime = Interlocked.Increment(ref _cacheAccessCounter),
                CreatedForDate = _politicalMapDate,
                RefCount = 0,
                DisposeRequested = false,
                Disposed = false
            };
            lock (_cacheLock)
            {
                while (_tileCache.Count >= MaxCacheSize)
                {
                    EvictOldestCacheEntry_NoLock();
                }
                _tileCache[cacheKey] = entry;
            }
        }

        private void EvictOldestCacheEntry()
        {
            lock (_cacheLock)
            {
                EvictOldestCacheEntry_NoLock();
            }
        }

        private void EvictOldestCacheEntry_NoLock()
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
                if (removed.RefCount == 0 && !removed.Disposed)
                {
                    removed.Disposed = true;
                    Task.Run(() => removed.Bitmap.Dispose());
                }
                else
                {
                    removed.DisposeRequested = true;
                }
            }
        }

        private void ClearCacheForDateChange()
        {
            List<CacheEntry> toDispose = new();
            lock (_cacheLock)
            {
                foreach (var kv in _tileCache)
                {
                    if (_tileCache.TryRemove(kv.Key, out var entry))
                    {
                        if (entry.RefCount == 0 && !entry.Disposed)
                        {
                            toDispose.Add(entry);
                        }
                        else
                        {
                            entry.DisposeRequested = true;
                        }
                    }
                }
                _maskCache.Clear();
            }
            foreach (var e in toDispose)
            {
                e.Disposed = true;
                e.Bitmap.Dispose();
            }
            lock (_indexLock)
            {
                _spatialIndex.Dispose();
                _spatialIndexBuilt = false;
            }
            lock (_gridLock)
            {
                _gridInitialized = false;
            }
        }

        private void PurgeCompletedTasks()
        {
            var completedKeys = new List<string>();
            foreach (var kvp in _inFlightTasks)
            {
                if (kvp.Value.IsCompleted)
                {
                    completedKeys.Add(kvp.Key);
                }
            }
            foreach (var key in completedKeys)
            {
                _inFlightTasks.TryRemove(key, out _);
            }
            if (completedKeys.Count > 0)
            {
                Debug.WriteLine($"Purged {completedKeys.Count} completed tasks from in-flight cache");
            }
        }

        private void ClearLegacyMaskCache()
        {
            int removedCount = _maskCache.Count;
            _maskCache.Clear();
            if (removedCount > 0)
            {
                Debug.WriteLine($"Cleared {removedCount} entries from legacy mask cache");
            }
        }

        private void LogCacheMetrics()
        {
            long totalBitmapBytes = 0;
            int tileCount = 0;
            lock (_cacheLock)
            {
                foreach (var entry in _tileCache.Values)
                {
                    if (!entry.Disposed)
                    {
                        totalBitmapBytes += entry.Bitmap.ByteCount;
                        tileCount++;
                    }
                }
            }
            int lodCount = _gridEngine.GetLodCount();
            Debug.WriteLine($"Cache metrics - Tiles: {tileCount}, VRAM: {totalBitmapBytes / (1024 * 1024)}MB, LODs: {lodCount}, InFlight: {_inFlightTasks.Count}, Masks: {_maskCache.Count}");
        }
        
        public int[,]? GetViewMask(int cellSize, int pixelX, int pixelY, int width, int height)
        {
            try
            {
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                int effectiveWidth = Math.Min(width, scaledMapWidth - pixelX);
                int effectiveHeight = Math.Min(height, scaledMapHeight - pixelY);
                if (effectiveWidth <= 0 || effectiveHeight <= 0)
                {
                    return null;
                }
                return GetTileMask(cellSize, pixelX, pixelY, effectiveWidth, effectiveHeight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting view mask: {ex.Message}");
                return null;
            }
        }
        
        private SKBitmap CreateUnavailablePlaceholder(int width, int height)
        {
            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            var oceanBlue = new SKColor(135, 206, 235, 255);
            canvas.Clear(oceanBlue);
            using var paint = new SKPaint
            {
                Color = new SKColor(80, 120, 150, 180),
                TextSize = Math.Min(width, height) / 24f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            string message = "Loading political data...";
            float x = width / 2f;
            float y = height / 2f;
            canvas.DrawText(message, x, y, paint);
            return bitmap;
        }

        private string? FindCShapesFile()
        {
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "data", "country_borders");
            string primaryPath = Path.Combine(userDataPath, "CShapes-2.0.shp");
            if (File.Exists(primaryPath))
            {
                Debug.WriteLine($"Found CShapes file at: {primaryPath}");
                return primaryPath;
            }
            string fallbackPath = Path.Combine(userDataPath, "ne_10m_admin_0_countries.shp");
            if (File.Exists(fallbackPath))
            {
                Debug.WriteLine($"Found fallback shapefile at: {fallbackPath}");
                return fallbackPath;
            }
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
            Debug.WriteLine("No political boundary files found.");
            return null;
        }

        private SKSizeI GetMapSize(int zoomLevel)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            return new SKSizeI(_baseWidth * cellSize, _baseHeight * cellSize);
        }

        private int GetLodLevelForCellSize(int cellSize)
        {
            return 0;
        }

        private int GetCellSizeForZoom(int zoomLevel)
        {
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1);
            return MultiResolutionMapManager.PixelsPerCellLevels[index];
        }

        public IndexedCountryFeature? GetCountryAtGeographicPoint(double longitude, double latitude)
        {
            try
            {
                EnsureGridInitialized();
                int countryId = _gridRenderer.GetCountryAtGeographic(longitude, latitude);
                if (countryId <= 0)
                {
                    Debug.WriteLine($"No country found at ({longitude:F4}, {latitude:F4}) - grid returned {countryId}");
                    return null;
                }
                string? cshapesPath = FindCShapesFile();
                if (!string.IsNullOrEmpty(cshapesPath))
                {
                    _ = _dataCache.GetOrGenerateCountryData(cshapesPath);
                }
                var country = _dataCache.GetCountryFeatureByRasterCode(countryId);
                if (country != null)
                {
                    Debug.WriteLine($"Found country at ({longitude:F4}, {latitude:F4}): {country.CountryName} ({country.CountryCode})");
                }
                else
                {
                    try
                    {
                        EnsureSpatialIndexBuilt();
                        var idxCountry = _spatialIndex.GetCountryByRasterCode(countryId);
                        if (idxCountry != null)
                        {
                            country = new IndexedCountryFeature
                            {
                                CountryCode = idxCountry.CountryCode,
                                CountryName = idxCountry.CountryName,
                                RasterCode = idxCountry.RasterCode,
                                Bounds = idxCountry.Bounds,
                                Geometry = idxCountry.Geometry
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Fallback spatial index lookup failed: {ex.Message}");
                    }
                }
                return country;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding country at ({longitude:F4}, {latitude:F4}): {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            ClearCacheForDateChange();
            ThreadLocalRandom.Dispose();
            _maskGenerator?.Dispose();
            _spatialIndex?.Dispose();
            _gridEngine?.Dispose();
        }

        public List<CachedCountryData> GetAllCountryData()
        {
            EnsureGridInitialized();
            string? cshapesPath = FindCShapesFile();
            if (!string.IsNullOrEmpty(cshapesPath))
            {
                var map = _dataCache.GetOrGenerateCountryData(cshapesPath);
                return new List<CachedCountryData>(map.Values);
            }
            return new List<CachedCountryData>();
        }

        public IndexedCountryFeature? GetCountryFeatureByRasterCode(int rasterCode)
        {
            EnsureGridInitialized();
            string? cshapesPath = FindCShapesFile();
            if (!string.IsNullOrEmpty(cshapesPath))
            {
                _ = _dataCache.GetOrGenerateCountryData(cshapesPath);
            }
            return _dataCache.GetCountryFeatureByRasterCode(rasterCode);
        }
    }
}