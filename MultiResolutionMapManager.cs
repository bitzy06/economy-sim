using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Drawing; // Needed for Path and PointF
using SixLabors.ImageSharp.Drawing.Processing; // Needed for DrawLines
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using SystemDrawing = System.Drawing;

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
        private Image<Rgba32> _largeBaseMap;
        private SystemDrawing.Bitmap _baseMap;

       
        

        // Cache of scaled bitmaps keyed by cell size
        private readonly Dictionary<int, SystemDrawing.Bitmap> _cachedMaps = new();
        // Cache of individual tiles for each zoom level
        private readonly Dictionary<(int cellSize, int x, int y), SystemDrawing.Bitmap> _tileCache = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _cacheLock = new();
        private readonly object _assembleLock = new();

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
        private Bitmap SafeLoadTile(string path)
        {
            var fileLock = GetFileLock(path);
            fileLock.Wait();
            try
            {
                // 1️⃣ copy the file into memory so the OS handle is released immediately
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(fs);
                return ImageSharpToBitmap(img);            // returns a System.Drawing.Bitmap
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

        private static readonly string TileCacheDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data", "tile_cache");

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
        public SystemDrawing.Size GetMapSize(float zoom)
        {
            int cellSize = GetCellSize(zoom);
            return new SystemDrawing.Size(_baseWidth * cellSize, _baseHeight * cellSize);
        }




        public Task<System.Drawing.Bitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
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

        private async Task<System.Drawing.Bitmap> LoadTileInternalAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            Debug.WriteLine($"[TILE LOAD] Starting tile ({tileX}, {tileY})");
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            System.Drawing.Bitmap bmp = null;

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
                    Debug.WriteLine($"[TILE LOAD] Finished tile ({tileX}, {tileY})");

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
        public async Task PreloadTilesAsync(
     float zoom,
     SixLabors.ImageSharp.Rectangle view,               // unambiguous: System.Drawing.Rectangle
     int radius = 1,
     CancellationToken token = default)
        {
            await _preloadSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var size = GetMapSize(zoom);
                int minX = Math.Max(0, view.X / TileSizePx - radius);
                int maxX = Math.Min((size.Width - 1) / TileSizePx + radius, (size.Width - 1) / TileSizePx);
                int minY = Math.Max(0, view.Y / TileSizePx - radius);
                int maxY = Math.Min((size.Height - 1) / TileSizePx + radius, (size.Height - 1) / TileSizePx);

                var maxParallel = Environment.ProcessorCount;
                using var throttler = new SemaphoreSlim(maxParallel);

                var tasks = new List<Task>();
                for (int tx = minX; tx <= maxX; tx++)
                {
                    for (int ty = minY; ty <= maxY; ty++)
                    {
                        await throttler.WaitAsync(token).ConfigureAwait(false);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await GetTileAsync(zoom, tx, ty, token).ConfigureAwait(false);
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
        public string GetTilePath(int cellSize, int tileX, int tileY)
        {
            string tileFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "tile_cache", $"{cellSize}");
            return System.IO.Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }
        /// <summary>
        /// Assemble a view rectangle from cached tiles.
        /// </summary>
        public System.Drawing.Bitmap AssembleView(float zoom, System.Drawing.Rectangle viewArea, Action triggerRefresh = null)
        {
            lock (_assembleLock)
            {
                // === 1. Calculate core sizes ===
                int cellSize = GetCellSize(zoom);                   // Your per-zoom cell size
                int tileSize = TileSizePx;                          // Your tile pixel size, probably 512
                int mapPixelWidth = _baseWidth * cellSize;
                int mapPixelHeight = _baseHeight * cellSize;

                // === 2. Center the map if viewArea is bigger than map ===
                int offsetX = 0, offsetY = 0;
                if (viewArea.Width > mapPixelWidth)
                    offsetX = (viewArea.Width - mapPixelWidth) / 2;
                if (viewArea.Height > mapPixelHeight)
                    offsetY = (viewArea.Height - mapPixelHeight) / 2;

                // === 3. Clamp which tiles are in view (never outside map bounds) ===
                int tileStartX = Math.Max(0, viewArea.Left / tileSize);
                int tileStartY = Math.Max(0, viewArea.Top / tileSize);
                int lastTileX = Math.Min((mapPixelWidth - 1) / tileSize, (viewArea.Right - 1) / tileSize);
                int lastTileY = Math.Min((mapPixelHeight - 1) / tileSize, (viewArea.Bottom - 1) / tileSize);

                // === 4. Create the output bitmap ===
                var output = new System.Drawing.Bitmap(viewArea.Width, viewArea.Height);
                using var g = System.Drawing.Graphics.FromImage(output);
                g.Clear(System.Drawing.Color.DarkGray); // change if you want another color

                // === 5. Draw each tile ===
                for (int ty = tileStartY; ty <= lastTileY; ty++)
                {
                    for (int tx = tileStartX; tx <= lastTileX; tx++)
                    {
                        var key = (cellSize, tx, ty);

                        int destX = tx * tileSize - viewArea.Left;
                        int destY = ty * tileSize - viewArea.Top;

                        // Edge cropping
                        int tilePixelX = tx * tileSize;
                        int tilePixelY = ty * tileSize;
                        int visibleWidth = Math.Min(tileSize, mapPixelWidth - tilePixelX);
                        int visibleHeight = Math.Min(tileSize, mapPixelHeight - tilePixelY);

                        // Clamp if our panel is even smaller than the map tile (shouldn't happen but safe)
                        if (destX + visibleWidth > output.Width)
                            visibleWidth = output.Width - destX;
                        if (destY + visibleHeight > output.Height)
                            visibleHeight = output.Height - destY;

                        if (visibleWidth <= 0 || visibleHeight <= 0)
                            continue;

                        System.Drawing.Bitmap tile = null;
                        lock (_cacheLock)
                        {
                            _tileCache.TryGetValue(key, out tile);
                        }

                        if (tile != null && tile.Width > 0 && tile.Height > 0)
                        {
                            var srcRect = new System.Drawing.Rectangle(0, 0, visibleWidth, visibleHeight);
                            var destRect = new System.Drawing.Rectangle(destX + offsetX, destY + offsetY, visibleWidth, visibleHeight);
                            g.DrawImage(tile, destRect, srcRect, System.Drawing.GraphicsUnit.Pixel);

                            // Optional: Draw tile border for debugging
                            // g.DrawRectangle(System.Drawing.Pens.Red, destRect);
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
                                            triggerRefresh?.Invoke();
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
                            // Draw nothing if tile is missing, or fill with placeholder if you like
                        }
                    }
                }

                return output;
            }
        }

        private static void OverlayFeatures(SystemDrawing.Bitmap bmp, ZoomLevel level)
        {
            using SystemDrawing.Graphics g = SystemDrawing.Graphics.FromImage(bmp);
            Random rng = new Random(42);
            switch (level)
            {
                case ZoomLevel.Country:
                    // Simple storms as grey circles
                    for (int i = 0; i < 3; i++)
                    {
                        int size = bmp.Width / 15;
                        int x = rng.Next(bmp.Width - size);
                        int y = rng.Next(bmp.Height - size);
                        g.FillEllipse(SystemDrawing.Brushes.LightGray, x, y, size, size);
                    }
                    break;
                case ZoomLevel.State:
                    // Highways and railways as lines
                    using (SystemDrawing.Pen highway = new SystemDrawing.Pen(SystemDrawing.Color.Gray, 2))
                    {
                        g.DrawLine(highway, 0, bmp.Height / 3, bmp.Width, bmp.Height / 3);
                        g.DrawLine(highway, bmp.Width / 2, 0, bmp.Width / 2, bmp.Height);
                    }
                    using (SystemDrawing.Pen rail = new SystemDrawing.Pen(SystemDrawing.Color.DarkGray, 1) { DashStyle = DashStyle.Dot })
                    {
                        g.DrawLine(rail, 0, bmp.Height * 2 / 3, bmp.Width, bmp.Height * 2 / 3);
                    }
                    break;
                case ZoomLevel.City:
                    // Add buildings and cars without drawing a full grid of road lines
                    for (int i = 0; i < 50; i++)
                    {
                        int w = rng.Next(4, 8);
                        int h = rng.Next(4, 8);
                        int x = rng.Next(bmp.Width - w);
                        int y = rng.Next(bmp.Height - h);
                        g.FillRectangle(SystemDrawing.Brushes.DarkSlateBlue, x, y, w, h);
                    }
                    for (int i = 0; i < 20; i++)
                    {
                        int x = rng.Next(bmp.Width - 3);
                        int y = rng.Next(bmp.Height - 2);
                        g.FillRectangle(SystemDrawing.Brushes.Red, x, y, 3, 2);
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

        private static SystemDrawing.Bitmap ImageSharpToBitmap(Image<Rgba32> img)
        {
            using var ms = new MemoryStream();
            img.SaveAsBmp(ms);
            ms.Position = 0;
            return new SystemDrawing.Bitmap(ms);
        }

        private static SystemDrawing.Bitmap CreateWaterTile(int width, int height)
        {
            var bmp = new SystemDrawing.Bitmap(width, height);
            using var g = SystemDrawing.Graphics.FromImage(bmp);
            g.Clear(SystemDrawing.Color.LightSkyBlue);
            return bmp;
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
            }
        }

        public async Task PreloadTilesAsync(float zoom, SystemDrawing.Rectangle view, int radius = 1, CancellationToken token = default)
        {
            await _preloadSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
            var size = GetMapSize(zoom);
            int firstTileX = Math.Max(0, view.X / TileSizePx - radius);
            int lastTileX = Math.Min((size.Width - 1) / TileSizePx, (view.Right - 1) / TileSizePx + radius);
            int firstTileY = Math.Max(0, view.Y / TileSizePx - radius);
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
        private static Image<Rgba32> ConvertBitmapToImageSharpFast(Bitmap bmp)
        {
            var image = new Image<Rgba32>(bmp.Width, bmp.Height);

            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            PixelFormat format = bmp.PixelFormat;

            // Convert to 32bppArgb if it's not already compatible
            if (format != PixelFormat.Format24bppRgb && format != PixelFormat.Format32bppArgb)
            {
                using var converted = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(converted))
                    g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                bmp = converted;
                format = PixelFormat.Format32bppArgb;
            }

            var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, format);

            unsafe
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    byte* row = (byte*)bmpData.Scan0 + (y * bmpData.Stride);
                    var pixelRow = image.DangerousGetPixelRowMemory(y).Span;

                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (format == PixelFormat.Format24bppRgb)
                        {
                            byte b = row[x * 3 + 0];
                            byte g = row[x * 3 + 1];
                            byte r = row[x * 3 + 2];
                            pixelRow[x] = new Rgba32(r, g, b, 255);
                        }
                        else if (format == PixelFormat.Format32bppArgb)
                        {
                            byte b = row[x * 4 + 0];
                            byte g = row[x * 4 + 1];
                            byte r = row[x * 4 + 2];
                            byte a = row[x * 4 + 3];
                            pixelRow[x] = new Rgba32(r, g, b, a);
                        }
                    }
                }
            }

            bmp.UnlockBits(bmpData);
            return image;
        }


        private void SaveTileToDisk(int cellSize, int tileX, int tileY, System.Drawing.Bitmap bmp)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

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

                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine($"[TILE SAVE ERROR] {ex.Message} while saving {path}");
            }
        }

        private async Task SaveTileToDiskAsync(int cellSize, int tileX, int tileY, System.Drawing.Bitmap bmp, CancellationToken token)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            try
            {
                Directory.CreateDirectory(dir);

                // Convert to ImageSharp outside the lock
                using var image = ImageSharpToImageSharp(bmp);

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

                    // Save via ImageSharp to memory
                    using var ms = new MemoryStream();
                    image.SaveAsPng(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    // Write to file synchronously inside lock to avoid race
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                    ms.CopyTo(fs);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"[FILE IN USE] {path} - {ioEx.Message}");
                MessageBox.Show($"Failed to save tile:\n{path}\n{ioEx.Message}", "Tile Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ASYNC TILE SAVE ERROR] {ex.Message} while saving {path}");
                if (!token.IsCancellationRequested)
                {
                    MessageBox.Show($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private static SixLabors.ImageSharp.Image<Rgba32> ImageSharpToImageSharp(System.Drawing.Bitmap bmp)
        {
            var img = new SixLabors.ImageSharp.Image<Rgba32>(bmp.Width, bmp.Height);
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var pixel = bmp.GetPixel(x, y);
                    img[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                }
            }
            return img;
        }


        private static Rgba32 GetCountryColor(int countryId)
        {
            var rng = new Random(countryId * 997);
            return new Rgba32((byte)rng.Next(40, 200), (byte)rng.Next(40, 200), (byte)rng.Next(40, 200), 40);
        }

        private static Rgba32 BlendColors(Rgba32 baseColor, Rgba32 overlay)
        {
            float alpha = overlay.A / 255f;
            return new Rgba32(
                (byte)(baseColor.R * (1 - alpha) + overlay.R * alpha),
                (byte)(baseColor.G * (1 - alpha) + overlay.G * alpha),
                (byte)(baseColor.B * (1 - alpha) + overlay.B * alpha),
                255);
        }

        private async Task<Bitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, float zoom, CancellationToken token)
        {
            int mapWidth = GetBaseMapWidth();
            int mapHeight = GetBaseMapHeight();
            int tileSizePx = 512;

            var image = PixelMapGenerator.GenerateTileWithCountriesLarge(
                mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx);

            var countryMask = PixelMapGenerator.CreateCountryMaskTile(
                mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx);

            int width = image.Width;
            int height = image.Height;

            if (countryMask.GetLength(0) != width || countryMask.GetLength(1) != height)
            {
                Console.WriteLine($"[WARN] countryMask dimensions ({countryMask.GetLength(0)}x{countryMask.GetLength(1)}) don't match image ({width}x{height})");
            }

            for (int y = 0; y < height && y < countryMask.GetLength(1); y++)
            {
                for (int x = 0; x < width && x < countryMask.GetLength(0); x++)
                {
                    int countryId = countryMask[x, y]; // assume layout is [x, y]
                    if (countryId > 0)
                    {
                        var tint = GetCountryColor(countryId);
                        image[x, y] = BlendColors(image[x, y], tint);
                    }
                }
            }

            NaturalEarthOverlayGenerator.ApplyOverlays(image, mapWidth, mapHeight, cellSize, tileX, tileY, zoom);

  

            return ConvertImageToBitmap(image);
        }

        private Bitmap ConvertImageToBitmap(Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return new Bitmap(ms);
        }

        private int GetBaseMapWidth() => _baseWidth;
        private int GetBaseMapHeight() => _baseHeight;




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
