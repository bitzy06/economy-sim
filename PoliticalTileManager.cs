using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using OSGeo.OGR;

namespace StrategyGame
{
    /// <summary>
    /// High-performance tile-based political map manager with spatial indexing optimizations
    /// </summary>
    public class PoliticalTileManager : IDisposable
    {
        private readonly PoliticalBorderManager _politicalManager;
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);

        // Data source for clean, non-overlapping country data and colors for the current date.
        private PoliticalDataCache _dataCache;

        // Performance optimizations with spatial indexing
        private const int TileSizePx = 512;
        private const int MaxCacheSize = 100; // Increased cache size for better performance

        // Spatial index for fast country lookup
        private readonly PoliticalSpatialIndex _spatialIndex;
        private readonly OptimizedPoliticalMaskGenerator _maskGenerator;
        private bool _spatialIndexBuilt = false;
        private readonly object _indexLock = new object();

        // Selected country for white border highlighting
        private IndexedCountryFeature? _selectedCountry = null;
        private readonly object _selectionLock = new object();

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

            // Initialize date-specific cache for clean data and color lookup
            _dataCache = new PoliticalDataCache(_politicalMapDate);

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

                ClearCacheForDateChange();

                // Mark spatial index as needing rebuild for new date
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

                // Clear tile cache to force re-rendering with new selection
                ClearTileCache();
            }
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

                for (int ty = tileStartY; ty < tileEndY; ty++)
                {
                    for (int tx = tileStartX; tx < tileEndX; tx++)
                    {
                        int tileX = tx, tileY = ty; // Capture for closure

                        // Calculate tile position in the composite image
                        int destX = tileX * TileSizePx - viewArea.Left;
                        int destY = tileY * TileSizePx - viewArea.Top;

                        // Get tile (async if not cached)
                        using var lease = AcquireTileLease(cellSize, tileX, tileY);
                        if (lease?.Bitmap != null && !lease.Bitmap.IsNull && !lease.Bitmap.IsEmpty)
                        {
                            var destRect = SKRect.Create(destX, destY, lease.Bitmap.Width, lease.Bitmap.Height);
                            canvas.DrawBitmap(lease.Bitmap, destRect);
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

        // Replaces direct bitmap access with a ref-counted lease.
        private TileLease? AcquireTileLease(int cellSize, int tileX, int tileY)
        {
            string cacheKey = $"{cellSize}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}";

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
                // Calculate tile bounds in pixel space using SCALED map dimensions
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

                // Get or generate political mask for this tile
                var tileMask = GetTileMask(cellSize, pixelX, pixelY, tileWidth, tileHeight);
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
                // Ensure spatial index is built
                EnsureSpatialIndexBuilt();

                // Use optimized mask generation with spatial indexing
                var mask = _maskGenerator.GenerateOptimizedMask(
                    cellSize, pixelX, pixelY, tileWidth, tileHeight, _baseWidth, _baseHeight);

                if (mask == null)
                {
                    Debug.WriteLine($"Failed to generate optimized mask for tile ({pixelX}, {pixelY})");
                    return null;
                }

                // Verify mask has data
                bool hasData = false;
                for (int y = 0; y < mask.GetLength(0) && !hasData; y++)
                {
                    for (int x = 0; x < mask.GetLength(1) && !hasData; x++)
                    {
                        if (mask[y, x] > 0)
                        {
                            hasData = true;
                        }
                    }
                }

                Debug.WriteLine($"Optimized political mask generated for tile ({pixelX}, {pixelY}): {(hasData ? "HAS DATA" : "NO DATA")}");

                // Cache the mask if successful
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

                // 1) Retrieve clean, non-overlapping data for the current date from cache.
                var cleanCountryData = _dataCache.GetOrGenerateCountryData(cshapesPath);

                // 2) Build spatial index strictly from this clean data.
                _spatialIndex.BuildIndex(cshapesPath, cleanCountryData);

                _spatialIndexBuilt = true;
                Debug.WriteLine($"Spatial index built with {_spatialIndex.CountryCount} countries");
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

                    // Parallel processing for optimal performance
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

                                // Check if this pixel is on a country border
                                bool isBorder = IsBorderPixel(mask, x, y, width, height);
                                bool isSelectedCountry = IsSelectedCountryPixel(countryId);

                                if (countryId == 0)
                                {
                                    // Water - light blue
                                    color = 0xFF87CEEB; // LightSkyBlue in ARGB
                                }
                                else if (isBorder && isSelectedCountry)
                                {
                                    // Selected country border - white
                                    color = 0xFFFFFFFF; // White in ARGB
                                }
                                else if (isBorder)
                                {
                                    // Regular country border - dark gray/black
                                    color = 0xFF404040; // Dark gray in ARGB
                                }
                                else
                                {
                                    // Color from date-correct cache
                                    SKColor baseColor = _dataCache.GetCountryColorByRasterCode(countryId);

                                    // Add slight random variation for visual interest (reduced for performance)
                                    int variation = rng.Next(-5, 6);
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

        private SKColor GenerateConsistentColor(string countryCode)
        {
            // Generate consistent colors based on country code hash for performance
            int hash = countryCode.GetHashCode();
            byte r = (byte)(100 + Math.Abs(hash % 156));
            byte g = (byte)(100 + Math.Abs((hash >> 8) % 156));
            byte b = (byte)(100 + Math.Abs((hash >> 16) % 156));
            return new SKColor(r, g, b, 255);
        }

        /// <summary>
        /// Determines if a pixel is on a country border by checking adjacent pixels
        /// </summary>
        private bool IsBorderPixel(int[,] mask, int x, int y, int width, int height)
        {
            if (x >= mask.GetLength(1) || y >= mask.GetLength(0)) return false;

            int currentCountryId = mask[y, x];

            // Don't draw borders for water (country ID 0)
            if (currentCountryId == 0) return false;

            // Check all 8 surrounding pixels (including diagonals for better border detection)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip center pixel

                    int newX = x + dx;
                    int newY = y + dy;

                    // Check bounds
                    if (newX < 0 || newX >= mask.GetLength(1) ||
                        newY < 0 || newY >= mask.GetLength(0))
                    {
                        continue; // Skip out-of-bounds pixels
                    }

                    int neighborCountryId = mask[newY, newX];

                    // If neighbor has different country ID (including water), this is a border pixel
                    if (neighborCountryId != currentCountryId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a pixel belongs to the currently selected country
        /// </summary>
        private bool IsSelectedCountryPixel(int countryId)
        {
            lock (_selectionLock)
            {
                if (_selectedCountry == null) return false;

                // Get the country information for this raster code
                var country = _spatialIndex.GetCountryByRasterCode(countryId);
                if (country == null) return false;

                // Check if this is the selected country (match by country code)
                return country.CountryCode == _selectedCountry.CountryCode;
            }
        }

        private void CacheTile(string cacheKey, SKBitmap bitmap)
        {
            var entry = new CacheEntry
            {
                Key = cacheKey,
                Bitmap = bitmap.Copy(), // Make a copy to avoid disposal issues
                AccessTime = Interlocked.Increment(ref _cacheAccessCounter),
                CreatedForDate = _politicalMapDate,
                RefCount = 0,
                DisposeRequested = false,
                Disposed = false
            };

            lock (_cacheLock)
            {
                // Implement LRU eviction if cache is full
                if (_tileCache.Count >= MaxCacheSize)
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
                    // Dispose outside lock to avoid blocking
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

            // Clear spatial index as well since it's date-specific
            lock (_indexLock)
            {
                _spatialIndex.Dispose();
                _spatialIndexBuilt = false;
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
            // Use the same map size calculation as the terrain manager for alignment
            int cellSize = GetCellSizeForZoom(zoomLevel);
            return new SKSizeI(_baseWidth * cellSize, _baseHeight * cellSize);
        }

        private int GetCellSizeForZoom(int zoomLevel)
        {
            // Use the same zoom level calculation as MultiResolutionMapManager for alignment
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1);
            return MultiResolutionMapManager.PixelsPerCellLevels[index];
        }

        /// <summary>
        /// Gets the country at a specific geographic point
        /// </summary>
        /// <param name="longitude">Longitude in degrees</param>
        /// <param name="latitude">Latitude in degrees</param>
        /// <returns>Country information if found, null otherwise</returns>
        public IndexedCountryFeature? GetCountryAtGeographicPoint(double longitude, double latitude)
        {
            try
            {
                // Ensure spatial index is built
                EnsureSpatialIndexBuilt();

                // Create a small search bounds around the point
                double tolerance = 0.01; // Small tolerance for point-in-polygon tests
                var searchBounds = new GeoBounds
                {
                    MinLon = longitude - tolerance,
                    MaxLon = longitude + tolerance,
                    MinLat = latitude - tolerance,
                    MaxLat = latitude + tolerance
                };

                // Get countries that might contain this point
                var candidates = _spatialIndex.GetCountriesInBounds(searchBounds);

                // Test each candidate to see if it actually contains the point
                foreach (var candidate in candidates)
                {
                    if (IsPointInCountry(longitude, latitude, candidate))
                    {
                        Debug.WriteLine($"Found country at ({longitude:F4}, {latitude:F4}): {candidate.CountryName} ({candidate.CountryCode})");
                        return candidate;
                    }
                }

                Debug.WriteLine($"No country found at ({longitude:F4}, {latitude:F4})");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding country at ({longitude:F4}, {latitude:F4}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Tests if a geographic point is within a country's boundaries
        /// </summary>
        private bool IsPointInCountry(double longitude, double latitude, IndexedCountryFeature country)
        {
            try
            {
                // First check if point is within the bounding box for quick elimination
                if (longitude < country.Bounds.MinLon || longitude > country.Bounds.MaxLon ||
                    latitude < country.Bounds.MinLat || latitude > country.Bounds.MaxLat)
                {
                    return false;
                }

                // Use OGR geometry to test if point is within the country polygon
                using var point = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
                point.AddPoint_2D(longitude, latitude);

                // Test if the point is within the country geometry
                return country.Geometry.Contains(point);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error testing point in country {country.CountryCode}: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            ClearCacheForDateChange();
            ThreadLocalRandom.Dispose();
            _maskGenerator?.Dispose();
            _spatialIndex?.Dispose();
        }
    }
}