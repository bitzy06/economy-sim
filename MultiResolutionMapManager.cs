using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
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
using SystemDrawing = System.Drawing;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;




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

        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80,160,320,640 };
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

        private static readonly string TileCacheDir = Path.Combine(
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




        public async Task<System.Drawing.Bitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
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
                lock (_cacheLock)
                {
                    _tileCache[key] = bmp;
                    _tileLru.AddLast(key);
                    EnforceTileLimit();
                }
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
                    Task.Run(() =>
                    {
                        var fileLock = GetFileLock(tilePath); // use the correct variable
                        fileLock.Wait();
                        try
                        {
                            var tile = PixelMapGenerator.GenerateTileWithCountriesLarge(
                                _baseWidth,
                                _baseHeight,
                                cellSize,
                                coord.X,
                                coord.Y,
                                tileSize);

                            Directory.CreateDirectory(Path.GetDirectoryName(tilePath));
                            tile.Save(tilePath); // image saving  still needs lock
                            tile.Dispose();
                        }
                        finally
                        {
                            fileLock.Release();
                        }
                    });
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
        public System.Drawing.Bitmap AssembleView(float zoom, System.Drawing.Rectangle viewArea, Action triggerRefresh = null)
        {
            lock (_assembleLock)
            {
                int cellSize = GetCellSize(zoom);
                int tileSize = TileSizePx;

                if (viewArea.Width <= 0 || viewArea.Height <= 0 || cellSize <= 0)
                    return new System.Drawing.Bitmap(1, 1); // Safe fallback

                int tileStartX = Math.Max(0, viewArea.X / tileSize);
                int tileStartY = Math.Max(0, viewArea.Y / tileSize);
                int tileEndX = (viewArea.Right + tileSize - 1) / tileSize;
                int tileEndY = (viewArea.Bottom + tileSize - 1) / tileSize;

                var output = new System.Drawing.Bitmap(viewArea.Width, viewArea.Height);
                using var g = System.Drawing.Graphics.FromImage(output);
                g.Clear(System.Drawing.Color.DarkGray); // Use a placeholder color

                for (int ty = tileStartY; ty < tileEndY; ty++)
                {
                    for (int tx = tileStartX; tx < tileEndX; tx++)
                    {
                        var key = (cellSize, tx, ty);
                        var rect = new System.Drawing.Rectangle(
                            tx * tileSize - viewArea.X,
                            ty * tileSize - viewArea.Y,
                            tileSize,
                            tileSize
                        );

                        if (rect.Width <= 0 || rect.Height <= 0)
                            continue;

                        System.Drawing.Bitmap tile = null;
                        lock (_cacheLock)
                        {
                            // First, try to get the tile from the in-memory cache.
                            _tileCache.TryGetValue(key, out tile);
                        }

                        if (tile != null && tile.Width > 0 && tile.Height > 0)
                        {
                            // If the tile was in the cache, draw it.
                            try
                            {
                                g.DrawImage(tile, rect);
                            }
                            catch (ExternalException ex)
                            {
                                Debug.WriteLine($"DrawImage ExternalException at ({tx},{ty}): {ex.Message}");
                            }
                        }
                        else
                        {
                            // --- This is the new non-blocking logic ---
                            // If the tile is NOT in the cache, request it asynchronously.
                            // Do NOT block to wait for it. The UI will remain responsive.
                            lock (_tileLoadLock)
                            {
                                if (!_tilesBeingLoaded.Contains(key))
                                {
                                    _tilesBeingLoaded.Add(key);
                                    // GetTileAsync will load from disk or generate if needed.
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            // This runs in the background.
                                            await GetTileAsync(zoom, tx, ty, CancellationToken.None);
                                            // Once the tile is loaded/generated, trigger a refresh to draw it.
                                            triggerRefresh?.Invoke();
                                            Debug.WriteLine($"[TILE] Triggering redraw for tile ({tx},{ty})");
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
                            // A placeholder (the gray background) is drawn for the missing tile for now.
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
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

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
        private SystemDrawing.Bitmap LoadOrGenerateTileFromData(int cellSize, int tileX, int tileY)
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
                    return new SystemDrawing.Bitmap(path);
                }
                catch (Exception ex)
                {
#if DEBUG
                    DebugLogger.Log($"[Tile Load Error] Failed to load tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
                    try { File.Delete(path); } catch { }
                }
            }

            SystemDrawing.Bitmap bmp;
            using var img = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
            OverlayFeaturesLarge(img, ZoomLevel.City);
            bmp = ImageSharpToBitmap(img);

            try
            {
                Directory.CreateDirectory(dir);
                using var imgSharp = ConvertBitmapToImageSharpFast(bmp);
                imgSharp.Save(path); // PNG
            }
            catch (Exception ex)
            {
#if DEBUG

                DebugLogger.Log($"[Tile Save Error] Failed to save generated tile '{path}' for ({tileX},{tileY}): {ex}");

#endif
            }

            return bmp;
        }

        private async Task<SystemDrawing.Bitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, CancellationToken token)
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
                    return ImageSharpToBitmap(imageSharpImg);
                }
                catch (Exception ex)
                {
#if DEBUG
                    DebugLogger.Log($"[Tile Load Error] Failed to load tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
                    try { File.Delete(path); } catch { }
                }
            }

            SystemDrawing.Bitmap bmp;
            using var img = await Task.Run(() =>
                {
                    var generated = PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY);
                    OverlayFeaturesLarge(generated, ZoomLevel.City);
                    return generated;
                }, token).ConfigureAwait(false);

            bmp = ImageSharpToBitmap(img);


            try
            {
                Directory.CreateDirectory(dir);
                using var imgSharp = ConvertBitmapToImageSharpFast(bmp);
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await imgSharp.SaveAsPngAsync(fs, token).ConfigureAwait(false);
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
