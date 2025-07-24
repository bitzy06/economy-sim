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

        // Cache of individual tiles for each zoom level
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileCache = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _masterCacheLock = new();
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileTextures = new();

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


        /// <summary>
        /// Number of pixels per map cell for each zoom level from
        /// <see cref="ZoomLevel.Global"/> through <see cref="ZoomLevel.City"/>.
        /// Adjusting this array changes both the zoom anchors and the
        /// maximum cell size used when generating maps.
        /// </summary>

        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80,160,320,640,1280 };
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

        /// <summary>
        /// Get the full map pixel dimensions for the provided zoom level.
        /// </summary>
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

            // Try cache first
            lock (_masterCacheLock)
            {
                if (_tileCache.TryGetValue(key, out var cached))
                {
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    return cached;
                }
            }

            // Build file path
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

            // Try disk cache (locked)
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

            // Generate fallback if still missing
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

            // Cache result
            if (bmp != null)
            {
                lock (_masterCacheLock)
                {
                    _tileCache[key] = bmp;
                    _tileLru.AddLast(key);
                    EnforceTileLimit();
                }
                UploadTileTexture(key, bmp);
            }

            return bmp;
        }
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
        /// <summary>
        /// Assemble a view rectangle from cached tiles.
        /// </summary>
        public SKBitmap AssembleView(float zoom, SKRectI viewArea, Action triggerRefresh = null)
        {
            int cellSize = GetCellSize(zoom);
            int tileSize = TileSizePx;

            if (viewArea.Width <= 0 || viewArea.Height <= 0 || cellSize <= 0)
                return new SKBitmap(1, 1); // Safe fallback

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

                        // --- START: Replacement Logic ---
                        SKBitmap textureCopy = null;
                        lock (_masterCacheLock)
                        {
                            if (_tileTextures.TryGetValue(key, out var texture))
                            {
                                // Create a private, safe copy of the texture inside the lock
                                textureCopy = texture.Copy();
                            }
                        }

                        if (textureCopy != null)
                        {
                            using (textureCopy)
                            {
                                try
                                {
                                    canvas.DrawBitmap(textureCopy, rect);
                                }
                                catch (AccessViolationException ex)
                                {
                                    DebugLogger.Log($"Access violation drawing texture copy {key}: {ex.Message}");
                                }
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
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            var t = await GetTileAsync(zoom, ttx, tty, CancellationToken.None);
                                            if (t != null)
                                                UploadTileTexture(tileKey, t);
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
                        // --- END: Replacement Logic ---
                    }
                }

                var result = new SKBitmap(info);
                surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
                return result;
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

        private static void OverlayFeaturesLarge(Image<Rgba32> img, ZoomLevel level)
        {
            //Random rng = new Random(42);
            //switch (level)
            //{
            //    case ZoomLevel.Country:
            //        for (int i = 0; i < 3; i++)
            //        {
            //            int size = img.Width / 15;
            //            int x = rng.Next(img.Width - size);
            //            int y = rng.Next(img.Height - size);
            //            FillCircle(img, x, y, size, SixLabors.ImageSharp.Color.LightGray);
            //        }
            //        break;
            //    case ZoomLevel.State:
            //        DrawLine(img, 0, img.Height / 3, img.Width, img.Height / 3, SixLabors.ImageSharp.Color.Gray, 2);
            //        DrawLine(img, img.Width / 2, 0, img.Width / 2, img.Height, SixLabors.ImageSharp.Color.Gray, 2);
            //        DrawDashedLine(img, 0, img.Height * 2 / 3, img.Width, img.Height * 2 / 3, SixLabors.ImageSharp.Color.DarkGray);
            //        break;
            //    case ZoomLevel.City:
            //        // Skip drawing the repetitive road grid on the large map as well
            //        for (int i = 0; i < 50; i++)
            //        {
            //            int w = rng.Next(4, 8);
            //            int h = rng.Next(4, 8);
            //            int x = rng.Next(img.Width - w);
            //            int y = rng.Next(img.Height - h);
            //            FillRect(img, x, y, w, h, SixLabors.ImageSharp.Color.DarkSlateBlue);
            //        }
            //        for (int i = 0; i < 20; i++)
            //        {
            //            int x = rng.Next(img.Width - 3);
            //            int y = rng.Next(img.Height - 2);
            //            FillRect(img, x, y, 3, 2, SixLabors.ImageSharp.Color.Red);
            //        }
            //        break;
            //}
        }

        private static void FillCircle(Image<Rgba32> img, int x, int y, int size, SixLabors.ImageSharp.Color color)
        {
            int radius = size / 2;
            int cx = x + radius;
            int cy = y + radius;
            for (int iy = -radius; iy <= radius; iy++)
            {
                int yy = cy + iy;
                if (yy < 0 || yy >= img.Height) continue;
                int dx = (int)Math.Sqrt(radius * radius - iy * iy);
                int start = cx - dx;
                int end = cx + dx;
                if (start < 0) start = 0;
                if (end >= img.Width) end = img.Width - 1;
                var row = img.DangerousGetPixelRowMemory(yy).Span;
                for (int ix = start; ix <= end; ix++)
                    row[ix] = color;
            }
        }

        private static void DrawLine(Image<Rgba32> img, int x0, int y0, int x1, int y1, SixLabors.ImageSharp.Color color, int thickness = 1)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            while (true)
            {
                FillRect(img, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, color);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static void DrawDashedLine(Image<Rgba32> img, int x0, int y0, int x1, int y1, SixLabors.ImageSharp.Color color)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            bool draw = true;
            int count = 0;
            while (true)
            {
                if (draw)
                    FillRect(img, x0, y0, 1, 1, color);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
                count++;
                if (count % 4 == 0) draw = !draw;
            }
        }

        private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, SixLabors.ImageSharp.Color color)
        {
            for (int yy = y; yy < y + height; yy++)
            {
                if (yy < 0 || yy >= img.Height) continue;
                var row = img.DangerousGetPixelRowMemory(yy).Span;
                for (int xx = x; xx < x + width; xx++)
                {
                    if (xx < 0 || xx >= img.Width) continue;
                    row[xx] = color;
                }
            }
        }

        private int GetCellSize(float zoom)
        {
            // 1) Interpolate between the discrete anchor values (3,4,6,10,40,80,160)
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
                int lower = (int)Math.Floor(zoom) - 1;   // anchors are 1-based
                float t = zoom - (lower + 1);            // fractional part
                size = anchors[lower] + t * (anchors[lower + 1] - anchors[lower]);
            }

            // 2) ***Removed*** the bitmap-size clamp that forced cellSize ≤ 100 000/_baseWidth
            //    because we tile; we never build the full image in one piece.
            //    If you really need that guard, put it behind a flag.

            if (size < 1f)
                size = 1f;

            return (int)Math.Round(size);
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

        private void UploadTileTexture((int cellSize, int x, int y) key, SKBitmap bmp)
        {
            var sk = bmp;
            lock (_masterCacheLock)
            {
                if (_tileTextures.TryGetValue(key, out var old))
                    old.Dispose();
                _tileTextures[key] = sk;
            }
        }

        /// <summary>
        /// Dispose all cached bitmaps without affecting tile caches.
        /// </summary>

        /// <summary>
        /// Dispose all cached tiles and clear the tile cache.
        /// </summary>

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
                lock (_masterCacheLock)
                {
                    if (_tileTextures.TryGetValue(oldest, out var tex))
                    {
                        tex.Dispose();
                        _tileTextures.Remove(oldest);
                    }
                }
            }
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

        // DONT CHANGE

        private void SaveTileToDisk(int cellSize, int tileX, int tileY, SKBitmap bmp)
        {
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

            try
            {
                Directory.CreateDirectory(dir);

                var fileLock = GetFileLock(path);
                fileLock.Wait();
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
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    data.SaveTo(fs);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowMessage($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error");
                Debug.WriteLine($"[TILE SAVE ERROR] {ex.Message} while saving {path}");
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
                    // Force close by ensuring exclusive write
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
                _ = DialogHelper.ShowMessage($"Failed to save tile:\n{path}\n{ioEx.Message}", "Tile Save Error");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ASYNC TILE SAVE ERROR] {ex.Message} while saving {path}");
                if (!token.IsCancellationRequested)
                {
                    _ = DialogHelper.ShowMessage($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error");
                }
            }
        }
        private SKBitmap LoadOrGenerateTileFromData(int cellSize, int tileX, int tileY)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            if (!PixelMapGenerator.TileContainsLand(_baseWidth, _baseHeight, cellSize, tileX, tileY))
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
                int fullW = _baseWidth * cellSize;
                int fullH = _baseHeight * cellSize;
                int offsetX = tileX * TileSizePx;
                int offsetY = tileY * TileSizePx;
                int widthPx = Math.Min(TileSizePx, fullW - offsetX);
                int heightPx = Math.Min(TileSizePx, fullH - offsetY);
                return CreateWaterTile(widthPx, heightPx);
            }

            if (File.Exists(path))
            {
                try
                {
                    return SafeLoadTile(path);
                }
                catch (Exception ex)
                {
#if DEBUG
                    DebugLogger.Log($"[Tile Load Error] Failed to load tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
                    try { File.Delete(path); } catch { }
                }
            }

            SKBitmap bmp;
            using var img = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
            OverlayFeaturesLarge(img, ZoomLevel.City);
            bmp = ImageSharpToSkBitmap(img);

            try
            {
                Directory.CreateDirectory(dir);
                using var image = SKImage.FromBitmap(bmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                data.SaveTo(fs);
            }
            catch (Exception ex)
            {
#if DEBUG

                DebugLogger.Log($"[Tile Save Error] Failed to save generated tile '{path}' for ({tileX},{tileY}): {ex}");

#endif
            }

            return bmp;
        }

        private async Task<SKBitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            if (!PixelMapGenerator.TileContainsLand(_baseWidth, _baseHeight, cellSize, tileX, tileY))
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
                int fullW = _baseWidth * cellSize;
                int fullH = _baseHeight * cellSize;
                int offsetX = tileX * TileSizePx;
                int offsetY = tileY * TileSizePx;
                int widthPx = Math.Min(TileSizePx, fullW - offsetX);
                int heightPx = Math.Min(TileSizePx, fullH - offsetY);
                return CreateWaterTile(widthPx, heightPx);
            }

            if (File.Exists(path))
            {
                try
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    using var imageSharpImg = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(fs, token).ConfigureAwait(false);
                    return ImageSharpToSkBitmap(imageSharpImg);
                }
                catch (Exception ex)
                {
#if DEBUG
                    DebugLogger.Log($"[Tile Load Error] Failed to load tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
                    try { File.Delete(path); } catch { }
                }
            }

            SKBitmap bmp;
            using var img = await Task.Run(() =>
                {
                    var generated = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
                    OverlayFeaturesLarge(generated, ZoomLevel.City);
                    return generated;
                }, token).ConfigureAwait(false);

            bmp = ImageSharpToSkBitmap(img);


            try
            {
                Directory.CreateDirectory(dir);
                using var image = SKImage.FromBitmap(bmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                data.SaveTo(fs);
            }
            catch (Exception ex)
            {
#if DEBUG
                DebugLogger.Log($"[Tile Save Error] Failed to save generated tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
            }

            return bmp;
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
