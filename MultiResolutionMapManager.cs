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

        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80, 160, 320, 640, 1280 };
        private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private static readonly object _fileLockDictLock = new();
        private SKBitmap SafeLoadTile(string path)
        {
            var fileLock = GetFileLock(path);
            fileLock.Wait();
            try
            {
                // 1️⃣ copy the file into memory so the OS handle is released immediately
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(fs);
                return ImageSharpToSkBitmap(img);
            }
            finally { fileLock.Release(); }
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

        private static readonly string RepoRoot =
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string TileCacheDir = Path.Combine(
             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
             "data", "tile_cache");

        private sealed class ImagePixelOwner : IDisposable
        {
            public Image<Rgba32> Image { get; }
            public MemoryHandle Handle { get; }

            public ImagePixelOwner(Image<Rgba32> image)
            {
                Image = image;
                Handle = image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out var memory) ? memory.Pin() : throw new InvalidOperationException("Unable to pin pixel memory.");
            }

            public void Dispose()
            {
                Handle.Dispose();
                Image.Dispose();
            }
        }

        private readonly int _baseWidth;
        private readonly int _baseHeight;

        public MultiResolutionMapManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
        }

        public SKSizeI GetMapSize(float zoom)
        {
            int cellSize = GetCellSize(zoom);
            return new SKSizeI(_baseWidth * cellSize, _baseHeight * cellSize);
        }

        public Task<SKBitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
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

        private async Task<SKBitmap> LoadTileInternalAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            Debug.WriteLine($"[TILE LOAD] Starting tile ({tileX}, {tileY})");
            int cellSize = GetCellSize(zoom);
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

            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

            if (File.Exists(path))
            {
                try
                {
                    Debug.WriteLine($"[TILE LOAD] Finished tile ({tileX}, {tileY})");
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
        private void QueueTileLoad(float zoom, int tx, int ty, Action triggerRefresh)
        {
            var key = (GetCellSize(zoom), tx, ty);
            lock (_tileLoadLock)
                if (!_tilesBeingLoaded.Add(key)) return;   // already loading

            _ = Task.Run(async () =>
            {
                try { await GetTileAsync(zoom, tx, ty, CancellationToken.None); }
                catch (Exception ex) { Debug.WriteLine(ex); }
                finally
                {
                    lock (_tileLoadLock) _tilesBeingLoaded.Remove(key);
                    triggerRefresh?.Invoke();
                }
            });
        }
        public SKBitmap AssembleView(float zoom, SKRectI viewArea, Action triggerRefresh = null)
        {
            int cellSize = GetCellSize(zoom);
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
                    var rect = new SKRect(
                        tx * tileSize - viewArea.Left,
                        ty * tileSize - viewArea.Top,
                        tx * tileSize - viewArea.Left + tileSize,
                        ty * tileSize - viewArea.Top + tileSize
                    );

                    if (rect.Width <= 0 || rect.Height <= 0)
                        continue;

                    SKBitmap textureCopy = null;
                    lock (_masterCacheLock)
                    {
                        if (!_tileTextures.TryGetValue(key, out var tex))
                        {
                            QueueTileLoad(zoom, tx, ty, triggerRefresh);
                            continue;
                        }
                        canvas.DrawBitmap(tex, rect);

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
                                var ttx = tx;
                                var tty = ty;
                                var tileKey = key;
                                // CORRECTED: This logic now correctly calls GetTileAsync
                                // and lets it handle the caching.
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await GetTileAsync(zoom, ttx, tty, CancellationToken.None);
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

        private int GetCellSize(float zoom)
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

        // --- The rest of the file remains the same ---
        // (Helper methods like ImageSharpToSkBitmap, CreateWaterTile, etc.)

        public void PreloadVisibleTiles(int zoomLevel, DrawingRectangle viewRect)
        {
            int cellSize = GetCellSize(zoomLevel);
            int tileSize = TileSizePx;
            var tiles = GetTilesForView(zoomLevel, viewRect);

            foreach (var tileCoord in tiles)
            {
                var coord = tileCoord; // prevent closure bug
                string tilePath = GetTilePath(cellSize, coord.X, coord.Y);

                if (!File.Exists(tilePath))
                {
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
            string tileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "tile_cache", $"{cellSize}");
            return Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }

        private static void OverlayFeatures(SKBitmap bmp, ZoomLevel level)
        {
            using var canvas = new SKCanvas(bmp);
            var paint = new SKPaint { IsAntialias = false, FilterQuality = SKFilterQuality.None };
            Random rng = new Random(42);
            switch (level)
            {
                case ZoomLevel.Country:
                    for (int i = 0; i < 3; i++)
                    {
                        int size = bmp.Width / 15;
                        int x = rng.Next(bmp.Width - size);
                        int y = rng.Next(bmp.Height - size);
                        paint.Color = SKColors.LightGray;
                        paint.Style = SKPaintStyle.Fill;
                        canvas.DrawOval(new SKRect(x, y, x + size, y + size), paint);
                    }
                    break;
                case ZoomLevel.State:
                    paint.Color = SKColors.Gray;
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = 2;
                    canvas.DrawLine(0, bmp.Height / 3, bmp.Width, bmp.Height / 3, paint);
                    canvas.DrawLine(bmp.Width / 2, 0, bmp.Width / 2, bmp.Height, paint);
                    paint.Color = SKColors.DarkGray;
                    paint.StrokeWidth = 1;
                    canvas.DrawLine(0, bmp.Height * 2 / 3, bmp.Width, bmp.Height * 2 / 3, paint);
                    break;
                case ZoomLevel.City:
                    paint.Style = SKPaintStyle.Fill;
                    for (int i = 0; i < 20; i++)
                    {
                        int w = rng.Next(4, 8);
                        int h = rng.Next(4, 8);
                        int x = rng.Next(bmp.Width - w);
                        int y = rng.Next(bmp.Height - h);
                        paint.Color = SKColors.DarkSlateBlue;
                        canvas.DrawRect(new SKRect(x, y, x + w, y + h), paint);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        int x = rng.Next(bmp.Width - 3);
                        int y = rng.Next(bmp.Height - 2);
                        paint.Color = SKColors.Red;
                        canvas.DrawRect(new SKRect(x, y, x + 3, y + 2), paint);
                    }
                    break;
            }
        }

        private static unsafe SKBitmap ImageSharpToSkBitmap(Image<Rgba32> img)
        {
            var info = new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var owner = new ImagePixelOwner(img);
            var bmp = new SKBitmap();
            bmp.InstallPixels(info, (IntPtr)owner.Handle.Pointer, img.Width * Unsafe.SizeOf<Rgba32>(), (addr, ctx) => ((ImagePixelOwner)ctx!).Dispose(), owner);
            return bmp;
        }

        private static SKBitmap CreateWaterTile(int width, int height)
        {
            var bmp = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.LightSkyBlue);
            return bmp;
        }

        public async Task PreloadTilesAsync(float zoom, SKRectI view, int radius = 1, CancellationToken token = default)
        {
            await _preloadSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var size = GetMapSize(zoom);
                int firstTileX = Math.Max(0, view.Left / TileSizePx - radius);
                int lastTileX = Math.Min((size.Width - 1) / TileSizePx, (view.Right - 1) / TileSizePx + radius);
                int firstTileY = Math.Max(0, view.Top / TileSizePx - radius);
                int lastTileY = Math.Min((size.Height - 1) / TileSizePx, (view.Bottom - 1) / TileSizePx + radius);

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
                                await GetTileAsync(zoom, ttx, tty, token).ConfigureAwait(false);
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
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
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
            // NOTE: This method now correctly uses your PixelMapGenerator to create the actual map tile.

            // Your original logic to check for land vs. water tiles can be re-integrated here if you have it.
            // For example:
            // if (!PixelMapGenerator.TileContainsLand(_baseWidth, _baseHeight, cellSize, tileX, tileY))
            // {
            //     ... return CreateWaterTile(...);
            // }

            // Call your actual map generator to create the tile image.
            using var imageSharpImage = await Task.Run(() =>
            {
                // This is the key line that generates your map content.
                // I have commented out dependencies that were not in the provided code.
                var generated = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
                // OverlayFeaturesLarge(generated, ZoomLevel.City);
                return generated;
            }, token).ConfigureAwait(false);


            if (imageSharpImage == null)
            {
                // Return a fallback tile if generation fails
                return new SKBitmap(TileSizePx, TileSizePx);
            }

            // Convert the generated ImageSharp image to an SKBitmap
            SKBitmap bmp = ImageSharpToSkBitmap(imageSharpImage);

            return bmp;
        }
    }
}

// Dummy classes to allow the code to compile without all original project files.
// You should have the real versions in your project.
public static class DebugLogger
{
    public static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
    }
}

public static class DialogHelper
{
    public static Task ShowMessage(string message, string title)
    {
        System.Diagnostics.Debug.WriteLine($"DIALOG: {title} - {message}");
        return Task.CompletedTask;
    }
}