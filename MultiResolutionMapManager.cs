using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SystemDrawing = System.Drawing;
using System.Collections.Concurrent;

namespace StrategyGame
{
    /// <summary>
    /// Generates and stores maps for each zoom level at game start.
    /// Maps are kept in memory so they can be cropped when rendering.
    /// </summary>
    public class MultiResolutionMapManager
    {
        public enum ZoomLevel { Global = 1, Continental, Country, State, City }
        private readonly HashSet<(int cellSize, int tileX, int tileY)> _tilesBeingLoaded = new();
        private readonly object _tileLoadLock = new();
        private readonly SemaphoreSlim _preloadSemaphore = new(1, 1);
        private readonly Dictionary<(int cellSize, int tileX, int tileY), Task<SystemDrawing.Bitmap>> _inFlightTasks = new();
        private readonly object _taskLock = new();

        // Cache of individual tiles for each zoom level
        private readonly Dictionary<(int cellSize, int x, int y), SystemDrawing.Bitmap> _tileCache = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _cacheLock = new();
        private readonly object _assembleLock = new();
        private static readonly ConcurrentQueue<MemoryStream> _msPool = new();

        /// <summary>
        /// Raised during tile cache generation. The first parameter is the
        /// number of tiles processed so far and the second is the total tile
        /// count.
        /// </summary>
        public event Action<int, int> TileGenerationProgress;

        /// <summary>
        /// Raised when a tile is loaded.
        /// </summary>
        public event Action OnTileLoaded;

        /// <summary>
        /// Maximum number of tiles kept in the cache.
        /// </summary>
        private const int TileCacheLimit = 1024;

        /// <summary>
        /// Size in pixels of each cached tile.
        /// </summary>
        public const int TileSizePx = 512;

        /// <summary>
        /// Number of pixels per map cell for each zoom level from
        /// <see cref="ZoomLevel.Global"/> through <see cref="ZoomLevel.City"/>.
        /// Adjusting this array changes both the zoom anchors and the
        /// maximum cell size used when generating maps.
        /// </summary>
        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80, 160, 320, 640, 1280 };
        
        private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private static readonly object _fileLockDictLock = new();

        private static readonly string TileCacheDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data", "tile_cache");

        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private List<City> _cities; // Cities field for thread-safe swapping

        public MultiResolutionMapManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
            
            // Clear corrupt tile cache on startup
            ClearTileCache();
            
            // NEW: Initialize PixelMapGenerator optimizations
            _ = Task.Run(async () =>
            {
                await PixelMapGenerator.InitializeAsync();
                Console.WriteLine("[DEBUG] PixelMapGenerator optimization features initialized");
            });
            
            // Load cities off the UI thread
            _ = Task.Run(async () =>
            {
                var loaded = await CityDensityRenderer.LoadCitiesFromNaturalEarthAsync(
                    NaturalEarthOverlayGenerator.CitiesPath,
                    NaturalEarthOverlayGenerator.UrbanAreasShpPath
                );
                Interlocked.Exchange(ref _cities, loaded);
                Console.WriteLine($"[DEBUG] Loaded {loaded?.Count ?? 0} cities");
            });
        }

        /// <summary>
        /// Get the full map pixel dimensions for the provided zoom level.
        /// </summary>
        public SystemDrawing.Size GetMapSize(float zoom)
        {
            int cellSize = GetCellSize(zoom);
            return new SystemDrawing.Size(_baseWidth * cellSize, _baseHeight * cellSize);
        }

        /// <summary>
        /// Preloads tiles for the specified view area asynchronously.
        /// </summary>
        /// <param name="zoom">The zoom level</param>
        /// <param name="viewArea">The view area rectangle</param>
        /// <param name="priority">Priority level (optional, defaults to 1)</param>
        /// <param name="cancellationToken">Cancellation token (optional)</param>
        /// <returns>A task representing the preload operation</returns>
        public async Task PreloadTilesAsync(float zoom, SystemDrawing.Rectangle viewArea, int priority = 1, CancellationToken cancellationToken = default)
        {
            await _preloadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int cellSize = GetCellSize(zoom);
                int tileSize = TileSizePx;
                int mapPixelWidth = _baseWidth * cellSize;
                int mapPixelHeight = _baseHeight * cellSize;

                // Calculate which tiles are in view
                int tileStartX = Math.Max(0, viewArea.Left / tileSize);
                int tileStartY = Math.Max(0, viewArea.Top / tileSize);
                int lastTileX = Math.Min((mapPixelWidth - 1) / tileSize, (viewArea.Right - 1) / tileSize);
                int lastTileY = Math.Min((mapPixelHeight - 1) / tileSize, (viewArea.Bottom - 1) / tileSize);

                var tasks = new List<Task>();

                // Preload all tiles in the view area
                for (int ty = tileStartY; ty <= lastTileY; ty++)
                {
                    for (int tx = tileStartX; tx <= lastTileX; tx++)
                    {
                        var key = (cellSize, tx, ty);
                        
                        // Check if tile is already in cache
                        bool inCache;
                        lock (_cacheLock)
                        {
                            inCache = _tileCache.ContainsKey(key);
                        }

                        if (!inCache)
                        {
                            // Start loading the tile asynchronously
                            var task = GetTileAsync(zoom, tx, ty, cancellationToken);
                            tasks.Add(task);
                        }
                    }
                }

                // Wait for all tiles to load
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            finally
            {
                _preloadSemaphore.Release();
            }
        }

        public Task<SystemDrawing.Bitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);

            lock (_taskLock)
            {
                if (_inFlightTasks.TryGetValue(key, out var existing))
                    return existing;

                var task = LoadTileInternalAsync(zoom, tileX, tileY, token);
                _inFlightTasks[key] = task;

                task.ContinueWith(_ =>
                {
                    lock (_taskLock)
                    {
                        _inFlightTasks.Remove(key);
                    }
                }, TaskScheduler.Default);

                return task;
            }
        }

        private async Task<SystemDrawing.Bitmap> LoadTileInternalAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            Debug.WriteLine($"[TILE LOAD] Starting tile ({tileX}, {tileY})");
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            SystemDrawing.Bitmap bmp = null;

            // Hard-clamp off-map tiles to prevent GDAL crashes
            int maxTileX = (_baseWidth * cellSize + TileSizePx - 1) / TileSizePx;
            int maxTileY = (_baseHeight * cellSize + TileSizePx - 1) / TileSizePx;
            if (tileX >= maxTileX || tileY >= maxTileY)
            {
                // All off-map tiles are rendered as a solid water bitmap
                return CreateWaterTile(TileSizePx, TileSizePx);
            }

            // Try cache first
            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(key, out var cached))
                {
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    return cached;
                }
            }

            // Build file path
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            // Try disk cache (locked)
            if (File.Exists(path))
            {
                try
                {
                    Debug.WriteLine($"[TILE LOAD] Loading tile from disk ({tileX}, {tileY})");

                    var fileLock = GetFileLock(path);
                    await fileLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                        if (img.Width > 0 && img.Height > 0)
                        {
                            bmp = ImageSharpToBitmap(img);
                        }
                        else
                        {
                            Debug.WriteLine($"Discarding corrupted tile at {path}");
                            File.Delete(path);
                        }
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load tile {tileX},{tileY} from disk: {ex.Message}");
                    try { File.Delete(path); } catch { }
                }
            }

            // Generate fallback if still missing
            if (bmp == null)
            {
                try
                {
                    bmp = await LoadOrGenerateTileFromDataAsync(cellSize, tileX, tileY, zoom, token).ConfigureAwait(false);
                    if (bmp != null && bmp.Width > 0 && bmp.Height > 0)
                    {
                        await SaveTileToDiskAsync(cellSize, tileX, tileY, bmp, token).ConfigureAwait(false);
                    }
                    else
                    {
                        bmp = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Tile generation failed ({tileX},{tileY}): {ex.Message}");
                }
            }

            // Cache result
            if (bmp != null)
            {
                lock (_cacheLock)
                {
                    _tileCache[key] = bmp;
                    _tileLru.AddLast(key);
                    EnforceTileLimit();
                }
            }

            return bmp;
        }

        private async Task<SystemDrawing.Bitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, float zoom, CancellationToken token)
        {
            int mapWidth = _baseWidth;
            int mapHeight = _baseHeight;
            int tileSizePx = 512;

            // Calculate full pixel dimensions for the entire map at the current zoom
            int fullWidthPx = _baseWidth * cellSize;
            int fullHeightPx = _baseHeight * cellSize;
            int offsetXpx = tileX * tileSizePx;
            int offsetYpx = tileY * tileSizePx;

            // Check for and handle tiles that are completely off the map
            int tileWidth = Math.Min(tileSizePx, fullWidthPx - offsetXpx);
            int tileHeight = Math.Min(tileSizePx, fullHeightPx - offsetYpx);

            if (tileWidth <= 0 || tileHeight <= 0)
            {
                // For tiles fully off-map, return a standard water tile.
                return CreateWaterTile(tileSizePx, tileSizePx);
            }

            // 1. Generate the base tile with terrain and country colors using ImageSharp.
            // The PixelMapGenerator will now handle partial tiles correctly
            var image = PixelMapGenerator.GenerateTileWithCountriesLarge(
                mapWidth, mapHeight, cellSize, tileX, tileY, TileSizePx);

            // 2. Apply overlays like state borders, country tints, and the brightness boost.
            NaturalEarthOverlayGenerator.ApplyOverlays(
                    image, mapWidth, mapHeight, cellSize, tileX, tileY, zoom);

            // 3. Convert the final ImageSharp image to a System.Drawing.Bitmap once at the end.
            return ImageSharpToBitmap(image);
        }

        public SystemDrawing.Bitmap AssembleView(float zoom, SystemDrawing.Rectangle viewArea, SystemDrawing.Bitmap reuse = null)
        {
            lock (_assembleLock)
            {
                int cellSize = GetCellSize(zoom);
                int tileSize = TileSizePx;
                int mapPixelWidth = _baseWidth * cellSize;
                int mapPixelHeight = _baseHeight * cellSize;

                // Center the map if viewArea is bigger than map
                int offsetX = 0, offsetY = 0;
                if (viewArea.Width > mapPixelWidth)
                    offsetX = (viewArea.Width - mapPixelWidth) / 2;
                if (viewArea.Height > mapPixelHeight)
                    offsetY = (viewArea.Height - mapPixelHeight) / 2;

                // Calculate which tiles are in view
                int tileStartX = Math.Max(0, viewArea.Left / tileSize);
                int tileStartY = Math.Max(0, viewArea.Top / tileSize);
                int lastTileX = Math.Min((mapPixelWidth - 1) / tileSize, (viewArea.Right - 1) / tileSize);
                int lastTileY = Math.Min((mapPixelHeight - 1) / tileSize, (viewArea.Bottom - 1) / tileSize);

                // Create or clear the output bitmap
                SystemDrawing.Bitmap output;
                if (reuse != null && reuse.Width == viewArea.Width && reuse.Height == viewArea.Height)
                {
                    output = reuse;
                    using var gg = SystemDrawing.Graphics.FromImage(output);
                    gg.Clear(SystemDrawing.Color.DarkGray);
                }
                else
                {
                    output = new SystemDrawing.Bitmap(viewArea.Width, viewArea.Height);
                    using var gg = SystemDrawing.Graphics.FromImage(output);
                    gg.Clear(SystemDrawing.Color.DarkGray);
                }
                using var g = SystemDrawing.Graphics.FromImage(output);

                // Draw each tile
                for (int ty = tileStartY; ty <= lastTileY; ty++)
                {
                    for (int tx = tileStartX; tx <= lastTileX; tx++)
                    {
                        var key = (cellSize, tx, ty);

                        // Calculate destination position with offset applied immediately
                        int destX = tx * tileSize - viewArea.Left + offsetX;
                        int destY = ty * tileSize - viewArea.Top + offsetY;

                        SystemDrawing.Bitmap tile = null;
                        lock (_cacheLock)
                        {
                            _tileCache.TryGetValue(key, out tile);
                        }

                        if (tile != null && tile.Width > 0 && tile.Height > 0)
                        {
                            // Initialize source clipping coordinates
                            int srcX = 0, srcY = 0;

                            // Crop left/top that fall off the bitmap
                            if (destX < 0) { srcX = -destX; destX = 0; }
                            if (destY < 0) { srcY = -destY; destY = 0; }

                            // Calculate draw dimensions accounting for clipping
                            int drawW = Math.Min(tile.Width - srcX, output.Width - destX);
                            int drawH = Math.Min(tile.Height - srcY, output.Height - destY);
                            
                            // Only draw if we have valid dimensions
                            if (drawW > 0 && drawH > 0)
                            {
                                var srcRect = new SystemDrawing.Rectangle(srcX, srcY, drawW, drawH);
                                var destRect = new SystemDrawing.Rectangle(destX, destY, drawW, drawH);
                                g.DrawImage(tile, destRect, srcRect, SystemDrawing.GraphicsUnit.Pixel);
                                
                                // Debug output for edge tiles
                                if (drawW != tileSize || drawH != tileSize || srcX != 0 || srcY != 0)
                                {
                                    Debug.WriteLine($"[TILE ASSEMBLY] Clipped tile ({tx},{ty}): tile={tile.Width}x{tile.Height}, src=({srcX},{srcY}) {drawW}x{drawH}, dest=({destX},{destY}) {drawW}x{drawH}");
                                }
                            }
                        }
                        else
                        {
                            // Async request tile if missing
                            lock (_tileLoadLock)
                            {
                                if (!_tilesBeingLoaded.Contains(key))
                                {
                                    _tilesBeingLoaded.Add(key);
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await GetTileAsync(zoom, tx, ty, CancellationToken.None);
                                            OnTileLoaded?.Invoke();
                                        }
                                        finally
                                        {
                                            lock (_tileLoadLock)
                                            {
                                                _tilesBeingLoaded.Remove(key);
                                            }
                                        }
                                    });
                                }
                            }
                        }
                    }
                }

                return output;
            }
        }

        public int GetCellSize(float zoom)
        {
            float[] anchors = new float[PixelsPerCellLevels.Length];
            for (int i = 0; i < anchors.Length; i++)
                anchors[i] = PixelsPerCellLevels[i];

            float size;
            if (zoom <= 1f)
                size = anchors[0];
            else if (zoom >= anchors.Length)
                size = anchors[^1];
            else
            {
                int lower = (int)Math.Floor(zoom) - 1;
                float t = zoom - (lower + 1);
                size = anchors[lower] + t * (anchors[lower + 1] - anchors[lower]);
            }

            if (size < 1f)
                size = 1f;

            return (int)Math.Round(size);
        }

        private static SystemDrawing.Bitmap ImageSharpToBitmap(Image<Rgba32> img)
        {
            if (!_msPool.TryDequeue(out var ms))
            {
                ms = new MemoryStream();
            }

            // Save the ImageSharp image to the pooled memory stream
            img.SaveAsBmp(ms);
            ms.Position = 0;

            // Bitmap constructed from a stream keeps that stream alive for the
            // lifetime of the bitmap. Clone the bitmap to detach it so the
            // stream can be reused.
            using var temp = new SystemDrawing.Bitmap(ms);
            var bmp = new SystemDrawing.Bitmap(temp);

            // Reset and return the memory stream to the pool
            ms.SetLength(0);
            _msPool.Enqueue(ms);

            return bmp;
        }

        private static SystemDrawing.Bitmap CreateWaterTile(int width, int height)
        {
            var bmp = new SystemDrawing.Bitmap(width, height);
            using var g = SystemDrawing.Graphics.FromImage(bmp);
            g.Clear(SystemDrawing.Color.LightSkyBlue);
            return bmp;
        }

        private static SemaphoreSlim GetFileLock(string path)
        {
            lock (_fileLockDictLock)
            {
                if (!_fileLocks.TryGetValue(path, out var sem))
                {
                    sem = new SemaphoreSlim(1, 1);
                    _fileLocks[path] = sem;
                }
                return sem;
            }
        }

        private async Task SaveTileToDiskAsync(int cellSize, int tileX, int tileY, SystemDrawing.Bitmap bmp, CancellationToken token)
        {
            // Allow saving tiles of any size, not just full TileSizePx
            if (bmp.Width <= 0 || bmp.Height <= 0)
                return;

            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            try
            {
                Directory.CreateDirectory(dir);

                var fileLock = GetFileLock(path);
                await fileLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        if (fi.IsReadOnly)
                            fi.IsReadOnly = false;
                    }

                    bmp.Save(path, SystemDrawing.Imaging.ImageFormat.Png);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TILE SAVE ERROR] {ex.Message} while saving {path}");
            }
        }

        public void ClearTileCache()
        {
            try
            {
                // Clear memory cache
                lock (_cacheLock)
                {
                    foreach (var bmp in _tileCache.Values)
                    {
                        bmp.Dispose();
                    }
                    _tileCache.Clear();
                    _tileLru.Clear();
                }

                // Clear disk cache
                if (Directory.Exists(TileCacheDir))
                {
                    Directory.Delete(TileCacheDir, true);
                    Console.WriteLine("[DEBUG] Tile cache cleared from disk");
                }
                
                // Clear the terrain and country mask caches in PixelMapGenerator
                PixelMapGenerator.ClearCaches();
                NaturalEarthOverlayGenerator.ClearAllOverlayCaches();
                
                Console.WriteLine("[DEBUG] All tile-related caches cleared - tiles will regenerate with edge tile fixes");
                Console.WriteLine("[DEBUG] Edge tiles should now render with proper dimensions instead of being scaled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to clear tile cache: {ex.Message}");
            }
        }

        private void EnforceTileLimit()
        {
            while (_tileLru.Count > TileCacheLimit)
            {
                var oldest = _tileLru.First.Value;
                _tileLru.RemoveFirst();
                if (_tileCache.TryGetValue(oldest, out var oldBmp))
                {
                    oldBmp.Dispose();
                    _tileCache.Remove(oldest);
                }
            }
        }

        public bool IsTileCacheComplete(int cellSize)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            if (!Directory.Exists(dir))
                return false;

            int widthPx = _baseWidth * cellSize;
            int heightPx = _baseHeight * cellSize;
            int tilesX = (widthPx + TileSizePx - 1) / TileSizePx;
            int tilesY = (heightPx + TileSizePx - 1) / TileSizePx;

            for (int x = 0; x < tilesX; x++)
            {
                for (int y = 0; y < tilesY; y++)
                {
                    string path = System.IO.Path.Combine(dir, $"{x}_{y}.png");
                    if (!File.Exists(path))
                        return false;
                }
            }
            return true;
        }
    }
}
