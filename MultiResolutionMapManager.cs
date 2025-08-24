using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DrawingPoint = SkiaSharp.SKPointI;
using DrawingRectangle = SkiaSharp.SKRectI;

namespace Economy_sim
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
        private readonly Dictionary<(int cellSize, int tileX, int tileY), Task<SKBitmap>> _inFlightTasks = new();
        private readonly object _taskLock = new();
        private Image<Rgba32> _largeBaseMap;

        // Unified cache for tiles using _tileTextures as the single source of truth.
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileTextures = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _masterCacheLock = new();

        public static readonly bool GpuAvailable;
        internal static readonly GRContext? SharedContext;

        static MultiResolutionMapManager()
        {
            try
            {
                SharedContext = GRContext.CreateGl();
                GpuAvailable = SharedContext != null;
            }
            catch
            {
                GpuAvailable = false;
            }
        }

        /// <summary>
        /// Raised during tile cache generation. The first parameter is the
        /// number of tiles processed so far and the second is the total tile
        /// count.
        /// </summary>
        public event Action<int, int> TileGenerationProgress;

        /// <summary>
        /// Maximum number of tiles kept in the cache.
        /// </summary>
        private const int TileCacheLimit = 1024;

        /// <summary>
        /// Size in pixels of each cached tile.
        /// </summary>
        public const int TileSizePx = 512;

        // New zoom ladder: larger FOV at lowest zoom, still supports deep zoom
        public static readonly int[] PixelsPerCellLevels = { 1, 2, 3, 5, 10, 20, 40, 80, 160, 320 };
        private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private static readonly object _fileLockDictLock = new();
        
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

        private static readonly string RepoRoot =
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string TileCacheDir = Path.Combine(
             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
             "data", "tile_cache");

        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private readonly string _instanceTileCacheRoot;
        
        public int BaseWidth => _baseWidth;
        public int BaseHeight => _baseHeight;

        // Baseline used to keep perceived zoom constant across base sizes (matches GameView defaults)
        private const int BaselineBaseWidth = 4096 * 4;   // 16384
        private const int BaselineBaseHeight = 2048 * 4;  // 8192

        // Extra zoom-out factor applied only at minimum zoom (level 1) to show more world area.
        // 0.66 shows ~1.5x area. Tweakable via env var ES_MIN_ZOOM_SCALE (0.3 - 1.0)
        private readonly float _minZoomScale;

        public MultiResolutionMapManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
            // Namespace cache by base grid size to avoid cross-configuration collisions
            _instanceTileCacheRoot = Path.Combine(TileCacheDir, $"{_baseWidth}x{_baseHeight}");

            // Read optional override for min zoom scale
            var s = Environment.GetEnvironmentVariable("ES_MIN_ZOOM_SCALE");
            if (float.TryParse(s, out var v))
                _minZoomScale = Math.Clamp(v, 0.3f, 1.0f);
            else
                _minZoomScale = 0.66f; // default: show ~50% more area
        }

        /// <summary>
        /// Gets the cell size for a given integer zoom level, normalized so perceived zoom
        /// does not change when the base grid size changes.
        /// </summary>
        public int GetCellSizeForZoom(int zoomLevel)
        {
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, PixelsPerCellLevels.Length - 1);

            // Normalize by width (height has same ratio in our 2:1 world grid)
            double scale = (double)BaselineBaseWidth / Math.Max(1, _baseWidth);
            int normalized = (int)Math.Round(PixelsPerCellLevels[index] * scale);
            return Math.Max(1, normalized);
        }

        /// <summary>
        /// Gets the total map size in pixels for a given integer zoom level.
        /// </summary>
        public SKSizeI GetMapSize(int zoomLevel)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            // _baseWidth and _baseHeight are the map dimensions in cells.
            return new SKSizeI(_baseWidth * cellSize, _baseHeight * cellSize);
        }

        /// <summary>
        /// Asynchronously gets a tile, loading it from cache, disk, or generating it if needed.
        /// This method is now standardized to use integer-based zoom levels.
        /// </summary>
        public Task<SKBitmap> GetTileAsync(int zoomLevel, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            var key = (cellSize, tileX, tileY);

            lock (_taskLock)
            {
                if (_inFlightTasks.TryGetValue(key, out var existing))
                    return existing;

                var task = LoadTileInternalAsync(zoomLevel, tileX, tileY, token);
                _inFlightTasks[key] = task;

                // Ensure the task is removed from the in-flight dictionary upon completion.
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

        private async Task<SKBitmap> LoadTileInternalAsync(int zoomLevel, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            var key = (cellSize, tileX, tileY);
            SKBitmap bmp = null;

            // Try cache first (using the unified _tileTextures cache)
            lock (_masterCacheLock)
            {
                if (_tileTextures.TryGetValue(key, out var cached))
                {
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    return cached;
                }
            }

            string dir = Path.Combine(_instanceTileCacheRoot, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

            if (File.Exists(path))
            {
                try
                {
                    var fileLock = GetFileLock(path);
                    await fileLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(stream);
                        if (img.Width > 0 && img.Height > 0)
                        {
                            bmp = ImageSharpToSkBitmap(img);
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

            if (bmp == null)
            {
                try
                {
                    // This method correctly uses cellSize, which is derived from the correct zoomLevel.
                    bmp = await LoadOrGenerateTileFromDataAsync(cellSize, tileX, tileY, token).ConfigureAwait(false);
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

            if (bmp != null)
            {
                lock (_masterCacheLock)
                {
                    if (_tileTextures.ContainsKey(key))
                    {
                        _tileTextures[key]?.Dispose();
                    }
                    _tileTextures[key] = bmp;
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    EnforceTileLimit();
                }
            }
            return bmp;
        }

        private void EnforceTileLimit()
        {
            // This method assumes a lock on _masterCacheLock is already held
            while (_tileLru.Count > TileCacheLimit)
            {
                var oldest = _tileLru.First.Value;
                _tileLru.RemoveFirst();

                if (_tileTextures.Remove(oldest, out var bitmapToDispose))
                {
                    bitmapToDispose?.Dispose();
                }
            }
        }

        public SKBitmap AssembleView(int zoomLevel, SKRectI viewArea, Action triggerRefresh = null)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            int tileSize = TileSizePx;
            if (viewArea.Width <= 0 || viewArea.Height <= 0 || cellSize <= 0)
                return new SKBitmap(1, 1);

            int tileStartX = Math.Max(0, viewArea.Left / tileSize);
            int tileStartY = Math.Max(0, viewArea.Top / tileSize);
            int tileEndX = (viewArea.Right + tileSize - 1) / tileSize;
            int tileEndY = (viewArea.Bottom + tileSize - 1) / tileSize;

            var info = new SKImageInfo(viewArea.Width, viewArea.Height);
            var context = GpuAvailable ? SharedContext : null;
            using var surface = context != null ? SKSurface.Create(context, false, info) : SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            for (int ty = tileStartY; ty < tileEndY; ty++)
            {
                for (int tx = tileStartX; tx < tileEndX; tx++)
                {
                    var key = (cellSize, tx, ty);

                    int dx = tx * tileSize - viewArea.Left;
                    int dy = ty * tileSize - viewArea.Top;
                    var rect = new SKRect(dx, dy, dx + tileSize, dy + tileSize);
                    if (rect.Width <= 0 || rect.Height <= 0) continue;

                    SKBitmap textureCopy = null;
                    lock (_masterCacheLock)
                    {
                        if (_tileTextures.TryGetValue(key, out var texture))
                            textureCopy = texture.Copy();
                    }

                    if (textureCopy != null)
                    {
                        using (textureCopy)
                        {
                            canvas.DrawBitmap(textureCopy, rect);
                        }
                    }
                    else
                    {
                        lock (_tileLoadLock)
                        {
                            if (!_tilesBeingLoaded.Contains(key))
                            {
                                _tilesBeingLoaded.Add(key);
                                var ttx = tx; var tty = ty; var tileKey = key;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await GetTileAsync(zoomLevel, ttx, tty, CancellationToken.None);
                                        triggerRefresh?.Invoke();
                                    }
                                    finally
                                    {
                                        lock (_tileLoadLock)
                                            _tilesBeingLoaded.Remove(tileKey);
                                    }
                                });
                            }
                        }
                    }
                }
            }

            var result = new SKBitmap(info);
            surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
            return result;
        }

        public void PreloadVisibleTiles(int zoomLevel, DrawingRectangle viewRect)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);
            int tileSize = TileSizePx;
            var tiles = GetTilesForView(zoomLevel, viewRect);

            foreach (var tileCoord in tiles)
            {
                var coord = tileCoord; // prevent closure bug
                string tilePath = GetTilePath(cellSize, coord.X, coord.Y);

                if (!File.Exists(tilePath))
                {
                    // CORRECTED: Pass the correct zoomLevel.
                    _ = GetTileAsync(zoomLevel, coord.X, coord.Y, CancellationToken.None);
                }
            }
        }
        public int GetTileSizeForZoom(int zoomLevel) => 512;
        private List<DrawingPoint> GetTilesForView(int zoomLevel, DrawingRectangle viewRect)
        {
            int tileSize = GetTileSizeForZoom(zoomLevel);
            int startX = viewRect.Left / tileSize;
            int endX = (viewRect.Right + tileSize - 1) / tileSize;
            int startY = viewRect.Top / tileSize;
            int endY = (viewRect.Bottom + tileSize - 1) / tileSize;

            List<DrawingPoint> tiles = new List<DrawingPoint>();

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    tiles.Add(new DrawingPoint(x, y));
                }
            }

            return tiles;
        }
        public string GetTilePath(int cellSize, int tileX, int tileY)
        {
            string tileFolder = Path.Combine(_instanceTileCacheRoot, $"{cellSize}");
            return Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }

        

        /// <summary>
        /// Safely converts an ImageSharp image to a new, independent SKBitmap by copying pixel data.
        /// This avoids complex memory lifetime issues with InstallPixels.
        /// </summary>
        private static unsafe SKBitmap ImageSharpToSkBitmap(Image<Rgba32> img)
        {
            var info = new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var bmp = new SKBitmap(info);

            if (img.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> pixelMemory))
            {
                IntPtr destPtr = bmp.GetPixels();
                var sourceBytes = MemoryMarshal.AsBytes(pixelMemory.Span);
                var destSpan = new Span<byte>(destPtr.ToPointer(), sourceBytes.Length);
                sourceBytes.CopyTo(destSpan);
            }
            else
            {
                Debug.WriteLine("CRITICAL: Could not get pixel memory from ImageSharp image.");
                // Fill with a visible error color if pixel data is inaccessible
                using (var canvas = new SKCanvas(bmp))
                {
                    canvas.Clear(SKColors.Magenta);
                }
            }
            return bmp;
        }

        

        public async Task PreloadTilesAsync(int zoomLevel, SKRectI view, int radius = 1, CancellationToken token = default)
        {
            await _preloadSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var mapSize = GetMapSize(zoomLevel);
                int firstTileX = Math.Max(0, view.Left / TileSizePx - radius);
                int lastTileX = Math.Min((mapSize.Width - 1) / TileSizePx, (view.Right - 1) / TileSizePx + radius);
                int firstTileY = Math.Max(0, view.Top / TileSizePx - radius);
                int lastTileY = Math.Min((mapSize.Height - 1) / TileSizePx, (view.Bottom - 1) / TileSizePx + radius);

                const int maxParallel = 4;
                using var throttler = new SemaphoreSlim(maxParallel);
                var tasks = new List<Task>();

                for (int tx = firstTileX; tx <= lastTileX; tx++)
                {
                    for (int ty = firstTileY; ty <= lastTileY; ty++)
                    {
                        await throttler.WaitAsync(token).ConfigureAwait(false);
                        var ttx = tx;
                        var tty = ty;
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                // CORRECTED: Pass the correct zoomLevel.
                                await GetTileAsync(zoomLevel, ttx, tty, token).ConfigureAwait(false);
                            }
                            finally
                            {
                                throttler.Release();
                            }
                        }, token));
                    }
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                _preloadSemaphore.Release();
            }
        }

        private async Task SaveTileToDiskAsync(int cellSize, int tileX, int tileY, SKBitmap bmp, CancellationToken token)
        {
            string dir = Path.Combine(_instanceTileCacheRoot, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

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

                    using var image = SKImage.FromBitmap(bmp);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    data.SaveTo(fs);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"[FILE IN USE] {path} - {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ASYNC TILE SAVE ERROR] {ex.Message} while saving {path}");
            }
        }

        private async Task<SKBitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            using var imageSharpImage = await Task.Run(() =>
            {
                var generated = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
                return generated;
            }, token).ConfigureAwait(false);

            if (imageSharpImage == null)
            {
                return new SKBitmap(TileSizePx, TileSizePx);
            }

            SKBitmap bmp = ImageSharpToSkBitmap(imageSharpImage);

            return bmp;
        }

        public float GetEffectiveScaleForZoom(int zoomLevel)
        {
            // Keep a constant scale for all zoom levels to avoid dramatic first step
            return 1.0f;
        }
    }
}