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
        private Image<Rgba32> _largeBaseMap;
        private SystemDrawing.Bitmap _baseMap;

        private static readonly Dictionary<string, object> _fileLocks = new();
        private static readonly object _fileLockDictLock = new();

        // Cache of scaled bitmaps keyed by cell size
        private readonly Dictionary<int, SystemDrawing.Bitmap> _cachedMaps = new();
        // Cache of individual tiles for each zoom level
        private readonly Dictionary<(int cellSize, int x, int y), SystemDrawing.Bitmap> _tileCache = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _cacheLock = new();

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

        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80 };
        private static object GetFileLock(string path)
        {
            lock (_fileLockDictLock)
            {
                if (!_fileLocks.TryGetValue(path, out var locker))
                {
                    locker = new object();
                    _fileLocks[path] = locker;
                }
                return locker;
            }
        }

        private static int MaxCellSize => PixelsPerCellLevels[PixelsPerCellLevels.Length - 1];
        private const int MAX_DIMENSION = 100_000;
        private const long MAX_PIXEL_COUNT = 6_000_000_000L;

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

        /// <summary>

        /// Generate maps for all zoom levels. Each level increases the pixel
        /// density without creating excessively large bitmaps.
        /// </summary>
        public void GenerateMaps()
        {
            if (!IsTileCacheComplete(MaxCellSize))
            {
                GenerateTileCache();
            }

            ClearMapCache();
            ClearTileCache();
        }




        /// <summary>
        /// Generate tile cache for all predefined zoom levels.
        /// </summary>
        public void GenerateTileCache()
        {
            ClearTileCache();

            int total = 0;
            foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
            {
                var size = GetMapSize((int)level);
                total += ((size.Width + TileSizePx - 1) / TileSizePx) *
                         ((size.Height + TileSizePx - 1) / TileSizePx);
            }

            int count = 0;
            foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
            {
                float z = (int)level;
                int cellSize = GetCellSize(z);
                var size = GetMapSize(z);
                int tilesX = (size.Width + TileSizePx - 1) / TileSizePx;
                int tilesY = (size.Height + TileSizePx - 1) / TileSizePx;

                for (int x = 0; x < tilesX; x++)
                {
                    for (int y = 0; y < tilesY; y++)
                    {
                        LoadOrGenerateTileFromData(cellSize, x, y);
                        count++;
                        TileGenerationProgress?.Invoke(count, total);
                    }
                }
            }

            ClearTileCache();
            ClearMapCache();
        }

        /// <summary>
        /// Generate tile caches for zoom levels that are missing tiles on disk.
        /// Existing caches are left untouched.
        /// </summary>
        public void GenerateMissingTileCaches()
        {
            int total = 0;
            foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
            {
                float z = (int)level;
                int cellSize = GetCellSize(z);
                if (IsTileCacheComplete(cellSize))
                    continue;

                var size = GetMapSize(z);
                int tilesX = (size.Width + TileSizePx - 1) / TileSizePx;
                int tilesY = (size.Height + TileSizePx - 1) / TileSizePx;

                for (int x = 0; x < tilesX; x++)
                {
                    for (int y = 0; y < tilesY; y++)
                    {
                        string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
                        string path = System.IO.Path.Combine(dir, $"{x}_{y}.png");
                        if (!File.Exists(path))
                            total++;
                    }
                }
            }

            int count = 0;
            foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
            {
                float z = (int)level;
                int cellSize = GetCellSize(z);
                if (IsTileCacheComplete(cellSize))
                    continue;

                var size = GetMapSize(z);
                int tilesX = (size.Width + TileSizePx - 1) / TileSizePx;
                int tilesY = (size.Height + TileSizePx - 1) / TileSizePx;

                for (int x = 0; x < tilesX; x++)
                {
                    for (int y = 0; y < tilesY; y++)
                    {
                        string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
                        string path = System.IO.Path.Combine(dir, $"{x}_{y}.png");
                        if (!File.Exists(path))
                        {
                            LoadOrGenerateTileFromData(cellSize, x, y);
                            count++;
                            TileGenerationProgress?.Invoke(count, total);
                        }
                    }
                }
            }

            ClearTileCache();
            ClearMapCache();
        }

        /// <summary>
        /// Retrieve a single tile bitmap for the given zoom level and tile coordinates.
        /// </summary>
        public SystemDrawing.Bitmap GetTile(float zoom, int tileX, int tileY)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            SystemDrawing.Bitmap bmp = null;

            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(key, out var bmpCached))
                {
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    return bmpCached;
                }
            }

            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");

            if (File.Exists(path))
            {
                try
                {
                    using var test = new System.Drawing.Bitmap(path);
                    if (test.Width > 0 && test.Height > 0)
                    {
                        bmp = new System.Drawing.Bitmap(test); // make a copy
                    }
                    else
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    try { File.Delete(path); } catch { }
                }
            }

            if (bmp == null)
            {
                bmp = LoadOrGenerateTileFromData(cellSize, tileX, tileY);
                if (bmp != null)
                {
                    SaveTileToDisk(cellSize, tileX, tileY, bmp);
                }
            }

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

        public async Task<System.Drawing.Bitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
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

            // Try disk cache
            if (File.Exists(path))
            {
                try
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    using var img = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(fs, token);

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
                    bmp = await LoadOrGenerateTileFromDataAsync(cellSize, tileX, tileY, token);
                    if (bmp != null && bmp.Width > 0 && bmp.Height > 0)
                    {
                        await SaveTileToDiskAsync(cellSize, tileX, tileY, bmp, token);
                    }
                    else
                    {
                        bmp = null; // Discard invalid
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Tile generation failed ({tileX},{tileY}): {ex.Message}");
                }
            }

            // Store in cache
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
            int tileSize = GetTileSizeForZoom(zoomLevel);
            var tiles = GetTilesForView(zoomLevel, viewRect);

            foreach (var tileCoord in tiles)
            {
                string tilePath = GetTilePath(zoomLevel, tileCoord.X, tileCoord.Y);
                if (!File.Exists(tilePath))
                {
                    Task.Run(() =>
                    {
                        var tile = PixelMapGenerator.GenerateTileWithCountriesLarge(
     _baseWidth,
     _baseHeight,
     tileSize,        // cellSize
     tileCoord.X,
     tileCoord.Y,
     tileSize         // tileSizePx
 );

                        Directory.CreateDirectory(Path.GetDirectoryName(tilePath));
                        tile.Save(tilePath);
                        tile.Dispose();
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
        public string GetTilePath(int zoomLevel, int tileX, int tileY)
        {
            string tileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "tile_cache", $"{zoomLevel}");
            return Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }
        /// <summary>
        /// Assemble a view rectangle from cached tiles.
        /// </summary>
        public System.Drawing.Bitmap AssembleView(float zoom, System.Drawing.Rectangle viewArea, Action triggerRefresh = null)
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
            g.Clear(System.Drawing.Color.DarkGray);

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
                        _tileCache.TryGetValue(key, out tile);
                    }

                    if (tile != null && tile.Width > 0 && tile.Height > 0)
                    {
                        try
                        {
                            g.DrawImage(tile, rect);
                        }
                        catch (ArgumentException ex)
                        {
                            Debug.WriteLine($"DrawImage failed at ({tx},{ty}): {ex.Message}");
                        }
                    }
                    else
                    {
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
                    }
                }
            }

            return output;
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
            // Convert the pixel-per-cell configuration to a float array for interpolation
            float[] anchors = new float[PixelsPerCellLevels.Length];
            for (int i = 0; i < anchors.Length; i++)
                anchors[i] = PixelsPerCellLevels[i];

            float size;
            if (zoom <= 1f) size = anchors[0];
            else if (zoom >= anchors.Length)
                size = anchors[anchors.Length - 1];
            else
            {
                int lowerIndex = (int)Math.Floor(zoom) - 1;
                float t = zoom - (lowerIndex + 1);
                size = anchors[lowerIndex] + t * (anchors[lowerIndex + 1] - anchors[lowerIndex]);
            }

            int maxDimSize = MAX_DIMENSION / Math.Max(_baseWidth, _baseHeight);
            double maxPixelSize = Math.Sqrt((double)MAX_PIXEL_COUNT / ((long)_baseWidth * _baseHeight));
            int maxAllowed = (int)Math.Floor(Math.Min(maxDimSize, maxPixelSize));

            if (size > maxAllowed)
                size = maxAllowed;

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
        public void ClearMapCache()
        {
            foreach (var bmp in _cachedMaps.Values)
            {
                bmp.Dispose();
            }
            _cachedMaps.Clear();
        }


        /// <summary>
        /// Dispose all cached tiles and clear the tile cache.
        /// </summary>
        public void ClearTileCache()
        {
            lock (_cacheLock)
            {
                foreach (var tile in _tileCache.Values)
                {
                    tile.Dispose();
                }
                _tileCache.Clear();
                _tileLru.Clear();
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

        public async Task PreloadTilesAsync(float zoom, SystemDrawing.Rectangle view, int radius = 1, CancellationToken token = default)
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
                    await throttler.WaitAsync(token);
                    var ttx = tx;
                    var tty = ty;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await GetTileAsync(zoom, ttx, tty, token);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }, token));
                }
            }

            await Task.WhenAll(tasks);
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


        private static void SaveTileToDisk(int cellSize, int tileX, int tileY, SystemDrawing.Bitmap bmp)
        {
            if (bmp == null || bmp.Width == 0 || bmp.Height == 0)
                return;

            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");

            try
            {
                Directory.CreateDirectory(dir); // Ensure directory exists before checking or saving
                if (!File.Exists(path))
                {
                    DebugLogger.Log($"Saving bitmap: {path}, size: {bmp.Width}x{bmp.Height}");
                    using var imgSharp = ConvertBitmapToImageSharpFast(bmp);
                    imgSharp.Save(path); // PNG
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                DebugLogger.Log($"[Tile Save Error] Failed to save tile '{path}' for ({tileX},{tileY}): {ex.Message}");
#endif
                try
                {
                    MessageBox.Show($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch
                {
                    // Ignore MessageBox errors in headless mode
                }
            }
        }

        private static async Task SaveTileToDiskAsync(int cellSize, int x, int y, System.Drawing.Bitmap bmp, CancellationToken token)
        {
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{x}_{y}.png");

            var locker = GetFileLock(path);
            lock (locker)
            {
                try
                {
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
                    using var imageSharpImg = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(fs, token);
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
                }, token);

            bmp = ImageSharpToBitmap(img);


            try
            {
                Directory.CreateDirectory(dir);
                using var imgSharp = ConvertBitmapToImageSharpFast(bmp);
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await imgSharp.SaveAsPngAsync(fs, token);
            }
            catch (Exception ex)
            {
#if DEBUG
                DebugLogger.Log($"[Tile Save Error] Failed to save generated tile '{path}' for ({tileX},{tileY}): {ex}");
#endif
            }

            return bmp;
        }

        private bool IsTileCacheComplete(int cellSize)
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
