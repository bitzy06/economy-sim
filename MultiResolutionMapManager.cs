using System;
using System.Collections.Generic;
using SystemDrawing = System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Advanced;


namespace StrategyGame
{
    /// <summary>
    /// Generates and stores maps for each zoom level at game start.
    /// Maps are kept in memory so they can be cropped when rendering.
    /// </summary>
    public class MultiResolutionMapManager
    {
        public enum ZoomLevel { Global = 1, Continental, Country, State, City }

        private readonly Dictionary<ZoomLevel, SystemDrawing.Bitmap> _maps = new();
        private readonly Dictionary<ZoomLevel, Image<Rgba32>> _largeMaps = new();
        private readonly int _baseWidth;
        private readonly int _baseHeight;

        public MultiResolutionMapManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
        }

        /// <summary>

        /// Generate maps for all zoom levels. Each level increases the pixel
        /// density without creating excessively large bitmaps.
        /// </summary>
        public void GenerateMaps()
        {
            int[] cellSizes = { 1, 2, 4, 6, 40 };

            int highestIndex = cellSizes.Length - 1;
            ZoomLevel highestLevel = (ZoomLevel)(highestIndex + 1);
            int highestCell = cellSizes[highestIndex];

            int widthPx = _baseWidth * highestCell;
            int heightPx = _baseHeight * highestCell;

            SystemDrawing.Bitmap highestMap;

            if (widthPx > PixelMapGenerator.MaxBitmapDimension || heightPx > PixelMapGenerator.MaxBitmapDimension)
            {
                var img = PixelMapGenerator.GeneratePixelArtMapWithCountriesLarge(_baseWidth, _baseHeight, highestCell);
                OverlayFeaturesLarge(img, highestLevel);
                using var ms = new MemoryStream();
                img.SaveAsBmp(ms);
                ms.Position = 0;
                highestMap = new SystemDrawing.Bitmap(ms);
            }
            else
            {
                highestMap = PixelMapGenerator.GeneratePixelArtMapWithCountries(_baseWidth, _baseHeight, highestCell);
                OverlayFeatures(highestMap, highestLevel);
            }

            _maps[highestLevel] = highestMap;

            for (int i = highestIndex - 1; i >= 0; i--)
            {
                var level = (ZoomLevel)(i + 1);
                int cellSize = cellSizes[i];
                int w = _baseWidth * cellSize;
                int h = _baseHeight * cellSize;
                SystemDrawing.Bitmap scaled = ScaleBitmapNearest(highestMap, w, h);
                OverlayFeatures(scaled, level);
                _maps[level] = scaled;
            }
        }

        /// <summary>
        /// Retrieve the full map bitmap for a zoom level.
        /// </summary>
        public SystemDrawing.Bitmap GetMap(ZoomLevel level)
        {
            return _maps.TryGetValue(level, out var bmp) ? bmp : null;
        }


        /// <summary>
        /// Return a cropped portion of the map at the requested zoom level.
        /// </summary>
        public SystemDrawing.Bitmap GetMap(ZoomLevel level, SystemDrawing.Rectangle view)
        {
            if (!_maps.TryGetValue(level, out var bmp))
                return null;
            SystemDrawing.Rectangle src = SystemDrawing.Rectangle.Intersect(new SystemDrawing.Rectangle(SystemDrawing.Point.Empty, bmp.Size), view);
            if (src.Width <= 0 || src.Height <= 0)
                return null;
            SystemDrawing.Bitmap dest = new SystemDrawing.Bitmap(src.Width, src.Height);
            using (SystemDrawing.Graphics g = SystemDrawing.Graphics.FromImage(dest))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bmp, new SystemDrawing.Rectangle(0, 0, dest.Width, dest.Height), src, SystemDrawing.GraphicsUnit.Pixel);
            }
            return dest;
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
                    // Draw a simple grid of roads
                    using (SystemDrawing.Pen road = new SystemDrawing.Pen(SystemDrawing.Color.Gray, 1))
                    {
                        for (int x = 0; x < bmp.Width; x += 20)
                            g.DrawLine(road, x, 0, x, bmp.Height);
                        for (int y = 0; y < bmp.Height; y += 20)
                            g.DrawLine(road, 0, y, bmp.Width, y);
                    }
                    // Add buildings and cars
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
                    for (int x = 0; x < img.Width; x += 20)
                        DrawLine(img, x, 0, x, img.Height, SixLabors.ImageSharp.Color.Gray);
                    for (int y = 0; y < img.Height; y += 20)
                        DrawLine(img, 0, y, img.Width, y, SixLabors.ImageSharp.Color.Gray);
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
    }
}
