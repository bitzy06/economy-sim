using System;
using System.Collections.Generic;
using SystemDrawing = System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;


namespace StrategyGame
{
    /// <summary>
    /// Generates and stores maps for each zoom level at game start.
    /// Maps are kept in memory so they can be cropped when rendering.
    /// </summary>
    public class MultiResolutionMapManager
    {
        public enum ZoomLevel { Global = 1, Continental, Country, State, City }

        private Image<Rgba32> _largeBaseMap;
        private SystemDrawing.Bitmap _baseMap;

        // Cache of scaled bitmaps keyed by cell size
        private readonly Dictionary<int, SystemDrawing.Bitmap> _cachedMaps = new();
        // Cache of individual tiles for each zoom level
        private readonly Dictionary<(int cellSize, int x, int y), SystemDrawing.Bitmap> _tileCache = new();
        // LRU order for tile cache entries
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();

        /// <summary>
        /// Maximum number of tiles kept in the cache.
        /// </summary>
        private const int TileCacheLimit = 256;

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
        public static readonly int[] PixelsPerCellLevels = { 1, 2, 4, 6, 40 };

        private static int MaxCellSize => PixelsPerCellLevels[PixelsPerCellLevels.Length - 1];
        private const int MAX_DIMENSION = 32767;
        private const int MAX_PIXEL_COUNT = 250_000_000;

        private static readonly string RepoRoot =
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string TileCacheDir =
            System.IO.Path.Combine(RepoRoot, "tile_cache");

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
            int widthPx = _baseWidth * MaxCellSize;
            int heightPx = _baseHeight * MaxCellSize;

            if (widthPx > PixelMapGenerator.MaxBitmapDimension || heightPx > PixelMapGenerator.MaxBitmapDimension)
            {
                if (IsTileCacheComplete(MaxCellSize))
                {
                    _largeBaseMap = null;
                    _baseMap = null;
                    _cachedMaps.Clear();
                    return;
                }

                _largeBaseMap = PixelMapGenerator.GeneratePixelArtMapWithCountriesLarge(_baseWidth, _baseHeight, MaxCellSize);
                OverlayFeaturesLarge(_largeBaseMap, ZoomLevel.City);

                foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
                {
                    GetMap((float)level);
                }
                GenerateTileCache();

                _largeBaseMap.Dispose();
                _largeBaseMap = null;
                _baseMap = null;
            }
            else
            {
                var bmp = PixelMapGenerator.GeneratePixelArtMapWithCountries(_baseWidth, _baseHeight, MaxCellSize);
                OverlayFeatures(bmp, ZoomLevel.City);
                _baseMap = bmp;
                _largeBaseMap = null;
                GenerateTileCache();
            }
        }

        /// <summary>
        /// Retrieve the full map bitmap for a zoom level.
        /// </summary>
        public SystemDrawing.Bitmap GetMap(float zoom)
        {
            int cellSize = GetCellSize(zoom);

            if (_cachedMaps.TryGetValue(cellSize, out var cached))
            {
                return cached;
            }

            int w = _baseWidth * cellSize;
            int h = _baseHeight * cellSize;

            SystemDrawing.Bitmap bmp = null;

            if (_baseMap != null)
            {
                bmp = ScaleBitmapNearest(_baseMap, w, h);
            }
            else if (_largeBaseMap != null)
            {
                bmp = ScaleImageSharp(_largeBaseMap, w, h);
            }

            if (bmp != null)
            {
                _cachedMaps[cellSize] = bmp;
            }

            return bmp;
        }


        /// <summary>
        /// Return a cropped portion of the map at the requested zoom level.
        /// </summary>
        public SystemDrawing.Bitmap GetMap(float zoom, SystemDrawing.Rectangle view)
        {
            int cellSize = GetCellSize(zoom);

            if (_baseMap != null)
            {
                float scale = (float)MaxCellSize / cellSize;
                var srcRect = new SystemDrawing.Rectangle(
                    (int)(view.X * scale),
                    (int)(view.Y * scale),
                    (int)(view.Width * scale),
                    (int)(view.Height * scale));

                srcRect = SystemDrawing.Rectangle.Intersect(new SystemDrawing.Rectangle(SystemDrawing.Point.Empty, _baseMap.Size), srcRect);
                if (srcRect.Width <= 0 || srcRect.Height <= 0)
                    return null;

                return CropScaleBitmapNearest(_baseMap, srcRect, view.Width, view.Height);
            }

            if (_largeBaseMap != null)
            {
                float scale = (float)MaxCellSize / cellSize;
                var srcRect = new SystemDrawing.Rectangle(
                    (int)(view.X * scale),
                    (int)(view.Y * scale),
                    (int)(view.Width * scale),
                    (int)(view.Height * scale));

                srcRect = SystemDrawing.Rectangle.Intersect(new SystemDrawing.Rectangle(SystemDrawing.Point.Empty, new SystemDrawing.Size(_largeBaseMap.Width, _largeBaseMap.Height)), srcRect);
                if (srcRect.Width <= 0 || srcRect.Height <= 0)
                    return null;

                return CropScaleImageSharp(_largeBaseMap, srcRect, view.Width, view.Height);
            }

            return null;
        }

        /// <summary>
        /// Generate tile cache for all predefined zoom levels.
        /// </summary>
        public void GenerateTileCache()
        {
            ClearTileCache();
            foreach (ZoomLevel level in Enum.GetValues(typeof(ZoomLevel)))
            {
                float z = (int)level;
                var size = GetMapSize(z);
                int tilesX = (size.Width + TileSizePx - 1) / TileSizePx;
                int tilesY = (size.Height + TileSizePx - 1) / TileSizePx;

                for (int x = 0; x < tilesX; x++)
                {
                    for (int y = 0; y < tilesY; y++)
                    {
                        // Use the public method so any generated tile is also
                        // written to disk and cached consistently
                        GetTile(z, x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve a single tile bitmap for the given zoom level and tile coordinates.
        /// </summary>
        public SystemDrawing.Bitmap GetTile(float zoom, int tileX, int tileY)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            if (_tileCache.TryGetValue(key, out var bmp))
            {
                _tileLru.Remove(key);
                _tileLru.AddLast(key);
                return bmp;
            }

            var size = GetMapSize(zoom);
            var rect = new SystemDrawing.Rectangle(tileX * TileSizePx, tileY * TileSizePx,
                Math.Min(TileSizePx, size.Width - tileX * TileSizePx),
                Math.Min(TileSizePx, size.Height - tileY * TileSizePx));

            bmp = GetMap(zoom, rect);


            if (bmp != null)
            {
                SaveTileToDisk(cellSize, tileX, tileY, bmp);
            }
            else if (_baseMap == null)
            {
                bmp = LoadOrGenerateTileFromData(cellSize, tileX, tileY, rect);
            }

            if (bmp != null)
            {
                _tileCache[key] = bmp;
                _tileLru.AddLast(key);
                EnforceTileLimit();
            }
            return bmp;
        }

        /// <summary>
        /// Assemble a view rectangle from cached tiles.
        /// </summary>
        public SystemDrawing.Bitmap AssembleView(float zoom, SystemDrawing.Rectangle view)
        {
            var size = GetMapSize(zoom);
            view = SystemDrawing.Rectangle.Intersect(new SystemDrawing.Rectangle(SystemDrawing.Point.Empty, size), view);
            if (view.Width <= 0 || view.Height <= 0)
                return new SystemDrawing.Bitmap(1, 1);

            int firstTileX = view.X / TileSizePx;
            int lastTileX = (view.Right - 1) / TileSizePx;
            int firstTileY = view.Y / TileSizePx;
            int lastTileY = (view.Bottom - 1) / TileSizePx;

            var result = new SystemDrawing.Bitmap(view.Width, view.Height);
            using (var g = SystemDrawing.Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                for (int tx = firstTileX; tx <= lastTileX; tx++)
                {
                    for (int ty = firstTileY; ty <= lastTileY; ty++)
                    {
                        var tile = GetTile(zoom, tx, ty);
                        if (tile == null) continue;
                        int destX = tx * TileSizePx - view.X;
                        int destY = ty * TileSizePx - view.Y;
                        g.DrawImage(tile, destX, destY, tile.Width, tile.Height);
                    }
                }
            }
            return result;
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
            Random rng = new Random(42);
            switch (level)
            {
                case ZoomLevel.Country:
                    for (int i = 0; i < 3; i++)
                    {
                        int size = img.Width / 15;
                        int x = rng.Next(img.Width - size);
                        int y = rng.Next(img.Height - size);
                        FillCircle(img, x, y, size, SixLabors.ImageSharp.Color.LightGray);
                    }
                    break;
                case ZoomLevel.State:
                    DrawLine(img, 0, img.Height / 3, img.Width, img.Height / 3, SixLabors.ImageSharp.Color.Gray, 2);
                    DrawLine(img, img.Width / 2, 0, img.Width / 2, img.Height, SixLabors.ImageSharp.Color.Gray, 2);
                    DrawDashedLine(img, 0, img.Height * 2 / 3, img.Width, img.Height * 2 / 3, SixLabors.ImageSharp.Color.DarkGray);
                    break;
                case ZoomLevel.City:
                    // Skip drawing the repetitive road grid on the large map as well
                    for (int i = 0; i < 50; i++)
                    {
                        int w = rng.Next(4, 8);
                        int h = rng.Next(4, 8);
                        int x = rng.Next(img.Width - w);
                        int y = rng.Next(img.Height - h);
                        FillRect(img, x, y, w, h, SixLabors.ImageSharp.Color.DarkSlateBlue);
                    }
                    for (int i = 0; i < 20; i++)
                    {
                        int x = rng.Next(img.Width - 3);
                        int y = rng.Next(img.Height - 2);
                        FillRect(img, x, y, 3, 2, SixLabors.ImageSharp.Color.Red);
                    }
                    break;
            }
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

        private static SystemDrawing.Bitmap ScaleBitmapNearest(SystemDrawing.Bitmap src, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return new SystemDrawing.Bitmap(1, 1);
            }

            const int MAX_DIMENSION = 32767;
            if (width > MAX_DIMENSION || height > MAX_DIMENSION)
            {
                return new SystemDrawing.Bitmap(1, 1);
            }

            SystemDrawing.Bitmap dest = new SystemDrawing.Bitmap(width, height);
            using (var g = SystemDrawing.Graphics.FromImage(dest))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, 0, 0, width, height);
            }
            return dest;
        }

        private static SystemDrawing.Bitmap CropScaleBitmapNearest(SystemDrawing.Bitmap src, SystemDrawing.Rectangle rect, int width, int height)
        {
            SystemDrawing.Bitmap dest = new SystemDrawing.Bitmap(width, height);
            using (var g = SystemDrawing.Graphics.FromImage(dest))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new SystemDrawing.Rectangle(0, 0, width, height), rect, SystemDrawing.GraphicsUnit.Pixel);
            }
            return dest;
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
            double maxPixelSize = Math.Sqrt((double)MAX_PIXEL_COUNT / (_baseWidth * _baseHeight));
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

        private static SystemDrawing.Bitmap ScaleImageSharp(Image<Rgba32> img, int width, int height)
        {
            using var clone = img.Clone(ctx => ctx.Resize(width, height, KnownResamplers.NearestNeighbor));
            return ImageSharpToBitmap(clone);
        }

        private static SystemDrawing.Bitmap CropImageSharp(Image<Rgba32> img, SystemDrawing.Rectangle rect)
        {
            var src = new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            using var clone = img.Clone(ctx => ctx.Crop(src));
            return ImageSharpToBitmap(clone);
        }

        private static SystemDrawing.Bitmap CropScaleImageSharp(Image<Rgba32> img, SystemDrawing.Rectangle rect, int width, int height)
        {
            var src = new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
            using var clone = img.Clone(ctx => ctx.Crop(src).Resize(width, height, KnownResamplers.NearestNeighbor));
            return ImageSharpToBitmap(clone);
        }

        /// <summary>
        /// Dispose all cached bitmaps and clear the cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (var bmp in _cachedMaps.Values)
            {
                bmp.Dispose();
            }
            _cachedMaps.Clear();
            ClearTileCache();
        }

        /// <summary>
        /// Dispose all cached tiles and clear the tile cache.
        /// </summary>
        public void ClearTileCache()
        {
            foreach (var tile in _tileCache.Values)
            {
                tile.Dispose();
            }
            _tileCache.Clear();
            _tileLru.Clear();
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


        private static void SaveTileToDisk(int cellSize, int tileX, int tileY, SystemDrawing.Bitmap bmp)
        {
            string path = string.Empty;
            try
            {
                string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
                path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(dir);
                    bmp.Save(path, SystemDrawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
#if DEBUG

                DebugLogger.Log($"[Tile Save Error] Failed to save tile '{path}' for ({tileX},{tileY}): {ex}");

#endif
                try
                {
                    MessageBox.Show($"Failed to save tile:\n{path}\n{ex.Message}", "Tile Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch
                {
                    // Ignore UI errors
                }
            }
        }

        private SystemDrawing.Bitmap LoadOrGenerateTileFromData(int cellSize, int tileX, int tileY, SystemDrawing.Rectangle rect)
        {
            string dir = System.IO.Path.Combine(TileCacheDir, cellSize.ToString());
            string path = System.IO.Path.Combine(dir, $"{tileX}_{tileY}.png");
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
                    // Corrupt tile file: remove and regenerate
                    try { File.Delete(path); } catch { }
                }

            }

            using var img = PixelMapGenerator.GeneratePixelArtMapWithCountriesLarge(_baseWidth, _baseHeight, cellSize);
            OverlayFeaturesLarge(img, ZoomLevel.City);
            var bmp = CropImageSharp(img, rect);

            try
            {
                Directory.CreateDirectory(dir);
                bmp.Save(path, SystemDrawing.Imaging.ImageFormat.Png);
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
