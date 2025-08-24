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
    /// Tile-based political map manager backed by a grid renderer. LOD removed.
    /// Renders fixed 512px tiles from the base grid and scales according to pixels-per-cell.
    /// </summary>
    public partial class PoliticalTileManager : IDisposable
    {
        private readonly PoliticalBorderManager _politicalManager;
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);

        // Grid system
        private readonly GridControlEngine _gridEngine;
        private readonly GridRenderer _gridRenderer;
        private readonly GridPopulator _gridPopulator;
        private bool _gridInitialized = false;
        private readonly object _gridLock = new object();

        // Data cache
        private PoliticalDataCache _dataCache;

        // Tile and cache config
        private const int TileSizePx = 512;
        private int _maxCacheSize = 64; // lowered default to reduce memory footprint
        public void SetMaxCacheSize(int size)
        {
            _maxCacheSize = Math.Max(8, Math.Min(512, size));
        }

        // Spatial index (legacy lookups)
        private readonly PoliticalSpatialIndex _spatialIndex;
        private readonly OptimizedPoliticalMaskGenerator _maskGenerator;
        private bool _spatialIndexBuilt = false;
        private readonly object _indexLock = new object();

        // Selection
        private IndexedCountryFeature? _selectedCountry = null;
        private readonly object _selectionLock = new object();
        private int _selectedRasterCode = -1;

        // Caches
        private readonly ConcurrentDictionary<string, CacheEntry> _tileCache = new();
        private readonly ConcurrentDictionary<string, int[,]> _maskCache = new();
        private readonly object _cacheLock = new object();
        private long _cacheAccessCounter = 0;
        // Deduplicate and control concurrency of tile renders
        private readonly ConcurrentDictionary<string, Lazy<Task<SKBitmap?>>> _inFlightTasks = new();
        private readonly SemaphoreSlim _tileGenSemaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));

        // Random
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(
            () => new Random(Environment.TickCount + Thread.CurrentThread.ManagedThreadId));

        // Zoom mapping (pixels per cell)
        private int[] _pixelsPerCellLevels = MultiResolutionMapManager.PixelsPerCellLevels;
        public void SetPixelsPerCellLevels(params int[] levels)
        {
            if (levels != null && levels.Length > 0)
                _pixelsPerCellLevels = levels;
        }

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

            _gridEngine = new GridControlEngine(baseWidth, baseHeight, TileSizePx);
            _gridPopulator = new GridPopulator(politicalManager);

            _dataCache = new PoliticalDataCache(_politicalMapDate);
            _gridRenderer = new GridRenderer(_gridEngine, _dataCache);

            _spatialIndex = new PoliticalSpatialIndex();
            _maskGenerator = new OptimizedPoliticalMaskGenerator(_spatialIndex);

            Debug.WriteLine($"[PoliticalTileManager] Initialized with base grid {_baseWidth}x{_baseHeight}");
        }

        public void Dispose()
        {
            try { ClearCacheForDateChange(); } catch { }
            ThreadLocalRandom.Dispose();
            _maskGenerator?.Dispose();
            _spatialIndex?.Dispose();
            _gridEngine?.Dispose();
            _tileGenSemaphore?.Dispose();
        }

        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate == date) return;
            _politicalMapDate = date;
            _dataCache = new PoliticalDataCache(_politicalMapDate);
            lock (_gridLock) { _gridInitialized = false; }
            ClearCacheForDateChange();
            lock (_indexLock) { _spatialIndexBuilt = false; }
        }

        public void SetSelectedCountry(IndexedCountryFeature? country)
        {
            lock (_selectionLock)
            {
                _selectedCountry = country;
                _selectedRasterCode = country?.RasterCode ?? -1;
                try
                {
                    if (_gridEngine.HasDirtyTiles())
                    {
                        var dirty = _gridEngine.GetDirtyTiles(clearAfterGet: true);
                        ClearTilesFromCache(dirty);
                    }
                    else
                    {
                        ClearTileCache();
                    }
                }
                catch { ClearTileCache(); }
            }
        }

        public void ChangeControl(int countryId, IEnumerable<System.Drawing.Point> cells)
        {
            EnsureGridInitialized();
            _gridEngine.ChangeControl(countryId, cells);
            var affectedTiles = _gridEngine.GetTileCoordinates(cells);
            ClearTilesFromCache(affectedTiles);
        }

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
                if (isBorder && adjacentToTarget) changes.Add((new Point(x, y), cur));
            }
            foreach (var (pt, _) in changes) grid[pt.Y, pt.X] = targetCountryId;
            if (changes.Count > 0)
            {
                var cells = changes.Select(ch => ch.cell);
                var affectedTiles = _gridEngine.GetTileCoordinates(cells);
                _gridEngine.MarkCellsDirty(cells);
                ClearTilesFromCache(affectedTiles);
            }
            return changes;
        }

        public void FloodFillControl(System.Drawing.Point seed, int newCountryId, Func<int, bool> canReplace)
        {
            EnsureGridInitialized();
            _gridEngine.FloodFillControl(seed, newCountryId, canReplace);
            ClearTileCache();
        }

        public IReadOnlyList<System.Drawing.Point> ComputeFrontline()
        {
            EnsureGridInitialized();
            return _gridEngine.ComputeFrontline();
        }

        public int GetCountryAtGeographic(double longitude, double latitude)
        {
            EnsureGridInitialized();
            var (cellX, cellY) = CoordinateTransform.GeographicToGridCell(longitude, latitude, _gridEngine.Width, _gridEngine.Height);
            if (cellX < 0 || cellY < 0 || cellX >= _gridEngine.Width || cellY >= _gridEngine.Height) return 0;
            return _gridEngine.ControlGrid[cellY, cellX];
        }

        public IndexedCountryFeature? GetCountryAtGeographicPoint(double longitude, double latitude)
        {
            try
            {
                EnsureGridInitialized();
                var (cellX, cellY) = CoordinateTransform.GeographicToGridCell(longitude, latitude, _gridEngine.Width, _gridEngine.Height);
                if (cellX < 0 || cellY < 0 || cellX >= _gridEngine.Width || cellY >= _gridEngine.Height) return null;
                int countryId = _gridEngine.ControlGrid[cellY, cellX];
                if (countryId <= 0) return null;
                string? cshapesPath = FindCShapesFile();
                if (!string.IsNullOrEmpty(cshapesPath)) _ = _dataCache.GetOrGenerateCountryData(cshapesPath);
                var country = _dataCache.GetCountryFeatureByRasterCode(countryId);
                if (country != null) return country;
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
                catch { }
                return country;
            }
            catch { return null; }
        }

        /// <summary>
        /// Gets the country feature at a political pixel coordinate for the given zoom.
        /// This performs a direct lookup against the control grid by converting pixels to grid cells.
        /// </summary>
        public IndexedCountryFeature? GetCountryAtPoliticalPixel(int politicalPixelX, int politicalPixelY, int zoomLevel)
        {
            try
            {
                EnsureGridInitialized();
                int cellSize = GetCellSizeForZoom(zoomLevel);
                if (cellSize <= 0) return null;

                int gridX = politicalPixelX / cellSize;
                int gridY = politicalPixelY / cellSize;
                if (gridX < 0 || gridY < 0 || gridX >= _gridEngine.Width || gridY >= _gridEngine.Height)
                    return null;

                int countryId = _gridEngine.ControlGrid[gridY, gridX];
                if (countryId <= 0) return null;

                // Try cache first
                string? cshapesPath = FindCShapesFile();
                if (!string.IsNullOrEmpty(cshapesPath))
                {
                    try { _ = _dataCache.GetOrGenerateCountryData(cshapesPath); } catch { }
                }
                var feature = _dataCache.GetCountryFeatureByRasterCode(countryId);
                if (feature != null) return feature;

                // Fallback to spatial index mapping by raster code
                try
                {
                    EnsureSpatialIndexBuilt();
                    var idxCountry = _spatialIndex.GetCountryByRasterCode(countryId);
                    if (idxCountry != null)
                    {
                        return new IndexedCountryFeature
                        {
                            CountryCode = idxCountry.CountryCode,
                            CountryName = idxCountry.CountryName,
                            RasterCode = idxCountry.RasterCode,
                            Bounds = idxCountry.Bounds,
                            Geometry = idxCountry.Geometry
                        };
                    }
                }
                catch { }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetCountryAtPoliticalPixel error: {ex.Message}");
                return null;
            }
        }

        private void ClearTileCache()
        {
            List<CacheEntry> toDispose = new();
            lock (_cacheLock)
            {
                foreach (var kv in _tileCache)
                {
                    if (_tileCache.TryRemove(kv.Key, out var entry))
                    {
                        if (entry.RefCount == 0 && !entry.Disposed) toDispose.Add(entry);
                        else entry.DisposeRequested = true;
                    }
                }
            }
            foreach (var e in toDispose)
            {
                e.Disposed = true;
                e.Bitmap.Dispose();
            }
        }

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
                            if (entry.RefCount == 0 && !entry.Disposed) toDispose.Add(entry);
                            else entry.DisposeRequested = true;
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
        /// Assemble view with option to force synchronous tile generation.
        /// Map incoming view (scaled by zoom) to base grid; draw fixed 512px tiles scaled by cellSize.
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady, bool forceSync)
        {
            try
            {
                EnsureGridInitialized();

                if (_cacheAccessCounter % 100 == 0)
                {
                    PurgeCompletedTasks();
                    ClearLegacyMaskCache();
                    LogCacheMetrics();
                }

                int cellSize = GetCellSizeForZoom(zoomLevel);
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                float invScaleX = (float)_baseWidth / scaledMapWidth;
                float invScaleY = (float)_baseHeight / scaledMapHeight;

                var gridViewArea = new SKRectI(
                    (int)(viewArea.Left * invScaleX),
                    (int)(viewArea.Top * invScaleY),
                    (int)(viewArea.Right * invScaleX),
                    (int)(viewArea.Bottom * invScaleY));

                int tilesX = (_baseWidth + TileSizePx - 1) / TileSizePx;
                int tilesY = (_baseHeight + TileSizePx - 1) / TileSizePx;
                int tileStartX = Math.Clamp(gridViewArea.Left / TileSizePx, 0, Math.Max(0, tilesX - 1));
                int tileStartY = Math.Clamp(gridViewArea.Top / TileSizePx, 0, Math.Max(0, tilesY - 1));
                int tileEndX = Math.Clamp((gridViewArea.Right + TileSizePx - 1) / TileSizePx, 0, tilesX);
                int tileEndY = Math.Clamp((gridViewArea.Bottom + TileSizePx - 1) / TileSizePx, 0, tilesY);

                var info = new SKImageInfo(viewArea.Width, viewArea.Height);
                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(135, 206, 235, 255));

                for (int ty = tileStartY; ty < tileEndY; ty++)
                {
                    for (int tx = tileStartX; tx < tileEndX; tx++)
                    {
                        int gridTileLeft = tx * TileSizePx;
                        int gridTileTop = ty * TileSizePx;

                        int scaledTileLeft = (int)(gridTileLeft / invScaleX);
                        int scaledTileTop = (int)(gridTileTop / invScaleY);
                        int scaledTileWidth = (int)(TileSizePx / invScaleX);
                        int scaledTileHeight = (int)(TileSizePx / invScaleY);
                        int destX = scaledTileLeft - viewArea.Left;
                        int destY = scaledTileTop - viewArea.Top;

                        using var lease = AcquireTileLease(tx, ty);
                        if (lease?.Bitmap != null && !lease.Bitmap.IsNull && !lease.Bitmap.IsEmpty)
                        {
                            canvas.DrawBitmap(lease.Bitmap, SKRect.Create(destX, destY, scaledTileWidth, scaledTileHeight));
                        }
                        else
                        {
                            if (forceSync)
                            {
                                var bmp = GenerateTileFromGrid(tx, ty, onTileReady);
                                if (bmp != null)
                                {
                                    using var lease2 = AcquireTileLease(tx, ty);
                                    if (lease2?.Bitmap != null && !lease2.Bitmap.IsNull && !lease2.Bitmap.IsEmpty)
                                    {
                                        canvas.DrawBitmap(lease2.Bitmap, SKRect.Create(destX, destY, scaledTileWidth, scaledTileHeight));
                                    }
                                }
                            }
                            else
                            {
                                _ = GetTileAsync(tx, ty, onTileReady);
                            }
                        }
                    }
                }

                // Prefetch a 1-tile border around the current viewport for smoother panning
                PrefetchNeighborTiles(tileStartX, tileStartY, tileEndX, tileEndY, tilesX, tilesY, onTileReady);

                var result = new SKBitmap(info);
                surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling political view: {ex.Message}");
                return CreateUnavailablePlaceholder(viewArea.Width, viewArea.Height);
            }
        }

        private TileLease? AcquireTileLease(int tileX, int tileY)
        {
            string cacheKey = $"tile_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";
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
                if (entry.Disposed) return;
                entry.RefCount = Math.Max(0, entry.RefCount - 1);
                if (entry.RefCount == 0 && entry.DisposeRequested)
                {
                    entry.Disposed = true;
                    disposeNow = true;
                }
            }
            if (disposeNow) entry.Bitmap.Dispose();
        }

        private async Task<SKBitmap?> GetTileAsync(int tileX, int tileY, Action? onComplete = null)
        {
            string cacheKey = $"tile_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";

            var lazyTask = _inFlightTasks.GetOrAdd(cacheKey, key => new Lazy<Task<SKBitmap?>>(() =>
                Task.Run(async () =>
                {
                    await _tileGenSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var bmpInner = _gridRenderer.RenderGridTile(tileX, tileY, TileSizePx, _selectedRasterCode, 0);
                        if (bmpInner != null) CacheTile(cacheKey, bmpInner);
                        onComplete?.Invoke();
                        return bmpInner;
                    }
                    finally
                    {
                        _tileGenSemaphore.Release();
                    }
                }),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var bmp = await lazyTask.Value.ConfigureAwait(false);
                return bmp;
            }
            finally
            {
                // Clean up completed tasks to keep dictionary small
                if (lazyTask.IsValueCreated && lazyTask.Value.IsCompleted)
                {
                    _inFlightTasks.TryRemove(cacheKey, out _);
                }
            }
        }

        private SKBitmap? GenerateTileFromGrid(int tileX, int tileY, Action? onComplete)
        {
            try
            {
                _tileGenSemaphore.Wait();
                try
                {
                    int maxTileX = (_baseWidth + TileSizePx - 1) / TileSizePx;
                    int maxTileY = (_baseHeight + TileSizePx - 1) / TileSizePx;
                    if (tileX >= maxTileX || tileY >= maxTileY)
                    {
                        var water = new SKBitmap(TileSizePx, TileSizePx, SKColorType.Rgba8888, SKAlphaType.Opaque);
                        water.Erase(new SKColor(135, 206, 235, 255));
                        CacheTile($"tile_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}", water);
                        onComplete?.Invoke();
                        return water;
                    }
                    var bmp = _gridRenderer.RenderGridTile(tileX, tileY, TileSizePx, _selectedRasterCode, 0);
                    if (bmp != null)
                    {
                        CacheTile($"tile_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}", bmp);
                        onComplete?.Invoke();
                    }
                    return bmp;
                }
                finally
                {
                    _tileGenSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GenerateTileFromGrid error: {ex.Message}");
                return CreateUnavailablePlaceholder(TileSizePx, TileSizePx);
            }
        }

        private void PrefetchNeighborTiles(int tileStartX, int tileStartY, int tileEndX, int tileEndY, int tilesX, int tilesY, Action? onTileReady)
        {
            int prefetchMargin = 1;
            int px0 = Math.Max(0, tileStartX - prefetchMargin);
            int py0 = Math.Max(0, tileStartY - prefetchMargin);
            int px1 = Math.Min(tilesX, tileEndX + prefetchMargin);
            int py1 = Math.Min(tilesY, tileEndY + prefetchMargin);

            // Top and bottom rows
            for (int tx = px0; tx < px1; tx++)
            {
                int topY = py0;
                int botY = py1 - 1;
                _ = GetTileAsync(tx, topY, onTileReady);
                _ = GetTileAsync(tx, botY, onTileReady);
            }
            // Left and right columns
            for (int ty = py0 + 1; ty < py1 - 1; ty++)
            {
                int leftX = px0;
                int rightX = px1 - 1;
                _ = GetTileAsync(leftX, ty, onTileReady);
                _ = GetTileAsync(rightX, ty, onTileReady);
            }
        }

        private void PurgeCompletedTasks()
        {
            var completedKeys = new List<string>();
            foreach (var kvp in _inFlightTasks)
            {
                if (kvp.Value.IsValueCreated && kvp.Value.Value.IsCompleted) completedKeys.Add(kvp.Key);
            }
            foreach (var key in completedKeys) _inFlightTasks.TryRemove(key, out _);
        }

        private void ClearLegacyMaskCache()
        {
            _maskCache.Clear();
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
            Debug.WriteLine($"Cache metrics - Tiles: {tileCount}, VRAM: {totalBitmapBytes / (1024 * 1024)}MB, InFlight: {_inFlightTasks.Count}, Masks: {_maskCache.Count}");
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
                while (_tileCache.Count >= _maxCacheSize) EvictOldestCacheEntry_NoLock();
                _tileCache[cacheKey] = entry;
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
                else removed.DisposeRequested = true;
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
            canvas.DrawText("Loading political data...", width / 2f, height / 2f, paint);
            return bitmap;
        }

        private void EnsureGridInitialized()
        {
            lock (_gridLock)
            {
                if (_gridInitialized) return;

                Debug.WriteLine($"Populating grid ({_gridEngine.Width}x{_gridEngine.Height}) from shapefile for date {_politicalMapDate:yyyy-MM-dd}");
                // Force fresh populate to honor current base size; ignore any legacy exports with mismatched dimensions
                bool loadExport = false; // previously: TryLoadGridFromExport();
                if (loadExport)
                {
                    _gridInitialized = true;
                }
                else
                {
                    string? cshapesPath = FindCShapesFile();
                    if (string.IsNullOrEmpty(cshapesPath))
                    {
                        _gridPopulator.PopulateTestPattern(_gridEngine);
                    }
                    else
                    {
                        try { _gridPopulator.PopulateFromShapefile(_gridEngine, cshapesPath, _politicalMapDate); }
                        catch { _gridPopulator.PopulateTestPattern(_gridEngine); }
                        try { _ = _dataCache.GetOrGenerateCountryData(cshapesPath); } catch { }
                    }
                    _gridInitialized = true;
                }
            }
        }

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
                if (exportDir == null) return false;

                string countriesJson = Path.Combine(exportDir, "countries.json");
                if (File.Exists(countriesJson))
                {
                    try { _dataCache.LoadFromCountriesJson(countriesJson); } catch { }
                }

                string wxh = $"{_gridEngine.Width}x{_gridEngine.Height}";
                var gridCandidates = new[]
                {
                    Path.Combine(exportDir, $"grid_{wxh}.bin"),
                    Path.Combine(exportDir, "grid.bin"),
                    Path.Combine(exportDir, $"base_grid_{wxh}.bin"),
                    Path.Combine(exportDir, "base_grid.bin")
                };
                string? gridPath = gridCandidates.FirstOrDefault(File.Exists);
                if (gridPath == null) return false;

                int width = _gridEngine.Width;
                int height = _gridEngine.Height;
                var grid = new int[height, width];
                using (var fs = File.OpenRead(gridPath))
                using (var br = new BinaryReader(fs))
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (fs.Position + 4 > fs.Length) throw new EndOfStreamException("Exported grid is smaller than expected");
                            grid[y, x] = br.ReadInt32();
                        }
                    }
                }
                _gridEngine.InitializeBaseGrid(grid);
                return true;
            }
            catch { return false; }
        }

        private void EnsureSpatialIndexBuilt()
        {
            lock (_indexLock)
            {
                if (_spatialIndexBuilt) return;
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                    throw new ApplicationException("CShapes file not found for spatial index generation");
                var cleanCountryData = _dataCache.GetOrGenerateCountryData(cshapesPath);
                _spatialIndex.BuildIndex(cshapesPath, cleanCountryData);
                _spatialIndexBuilt = true;
            }
        }

        private string? FindCShapesFile()
        {
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "data", "country_borders");
            string primaryPath = Path.Combine(userDataPath, "CShapes-2.0.shp");
            if (File.Exists(primaryPath)) return primaryPath;
            string fallbackPath = Path.Combine(userDataPath, "ne_10m_admin_0_countries.shp");
            if (File.Exists(fallbackPath)) return fallbackPath;
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
            foreach (string path in possiblePaths) if (File.Exists(path)) return path;
            return null;
        }

        public int[,] ComputeBorderMask(SKRectI viewArea, int zoomLevel)
        {
            try
            {
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                float invScaleX = (float)_baseWidth / scaledMapWidth;
                float invScaleY = (float)_baseHeight / scaledMapHeight;
                var gridArea = new SKRectI(
                    (int)(viewArea.Left * invScaleX),
                    (int)(viewArea.Top * invScaleY),
                    (int)(viewArea.Right * invScaleX),
                    (int)(viewArea.Bottom * invScaleY));

                int maskWidth = viewArea.Width;
                int maskHeight = viewArea.Height;
                var mask = new int[maskHeight, maskWidth];
                Parallel.For(0, maskHeight, y =>
                {
                    for (int x = 0; x < maskWidth; x++)
                    {
                        int globalX = gridArea.Left + x;
                        int globalY = gridArea.Top + y;
                        if (globalX < 0 || globalY < 0 || globalX >= _gridEngine.Width || globalY >= _gridEngine.Height) continue;
                        int countryId = _gridEngine.ControlGrid[globalY, globalX];
                        mask[y, x] = countryId;
                        if (IsBorderCell(globalX, globalY, countryId))
                        {
                            MarkSurroundingCellsAsBorder(mask, x, y, countryId);
                        }
                    }
                });
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ComputeBorderMask: {ex.Message}");
                return new int[viewArea.Height, viewArea.Width];
            }
        }

        private bool IsBorderCell(int globalX, int globalY, int countryId)
        {
            return GetAdjacentCountryIds(globalX, globalY).Any(adjacentId => adjacentId != countryId && adjacentId != 0);
        }

        private void MarkSurroundingCellsAsBorder(int[,] mask, int centerX, int centerY, int countryId)
        {
            int maskWidth = mask.GetLength(1);
            int maskHeight = mask.GetLength(0);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x < 0 || y < 0 || x >= maskWidth || y >= maskHeight) continue;
                    if (mask[y, x] != 0) mask[y, x] = countryId;
                }
            }
        }

        private IEnumerable<int> GetAdjacentCountryIds(int globalX, int globalY)
        {
            if (globalX > 0) yield return _gridEngine.ControlGrid[globalY, globalX - 1];
            if (globalX < _gridEngine.Width - 1) yield return _gridEngine.ControlGrid[globalY, globalX + 1];
            if (globalY > 0) yield return _gridEngine.ControlGrid[globalY - 1, globalX];
            if (globalY < _gridEngine.Height - 1) yield return _gridEngine.ControlGrid[globalY + 1, globalX];
        }

        public int[,]? GetViewMask(int cellSize, int pixelX, int pixelY, int width, int height)
        {
            try
            {
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                int effectiveWidth = Math.Min(width, scaledMapWidth - pixelX);
                int effectiveHeight = Math.Min(height, scaledMapHeight - pixelY);
                if (effectiveWidth <= 0 || effectiveHeight <= 0) return null;
                return GetTileMask(cellSize, pixelX, pixelY, effectiveWidth, effectiveHeight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting view mask: {ex.Message}");
                return null;
            }
        }

        private int[,]? GetTileMask(int cellSize, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            string maskKey = $"mask_{cellSize}_{pixelX}_{pixelY}_{tileWidth}_{tileHeight}_{_politicalMapDate:yyyyMMdd}";
            if (_maskCache.TryGetValue(maskKey, out var cached)) return cached;
            try
            {
                EnsureSpatialIndexBuilt();
                var mask = _maskGenerator.GenerateOptimizedMask(
                    cellSize, pixelX, pixelY, tileWidth, tileHeight, _baseWidth, _baseHeight);
                if (mask == null) return null;
                if (_maskCache.Count < _maxCacheSize * 3)
                    _maskCache.TryAdd(maskKey, mask);
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating optimized tile mask: {ex.Message}");
                return null;
            }
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

        public PoliticalDataCache GetDataCache()
        {
            EnsureGridInitialized();
            return _dataCache;
        }

        public IndexedCountryFeature? GetCountryFeatureByRasterCode(int rasterCode)
        {
            EnsureGridInitialized();
            string? cshapesPath = FindCShapesFile();
            if (!string.IsNullOrEmpty(cshapesPath)) _ = _dataCache.GetOrGenerateCountryData(cshapesPath);
            return _dataCache.GetCountryFeatureByRasterCode(rasterCode);
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
                        if (entry.RefCount == 0 && !entry.Disposed) toDispose.Add(entry);
                        else entry.DisposeRequested = true;
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
            lock (_gridLock) { _gridInitialized = false; }
        }

        private int GetCellSizeForZoom(int zoomLevel)
        {
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, _pixelsPerCellLevels.Length - 1);
            return _pixelsPerCellLevels[index];
        }
    }
}