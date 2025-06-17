using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;


namespace StrategyGame
{
    /// <summary>
    /// Generates and stores maps for each zoom level at game start.
    /// Maps are kept in memory so they can be cropped when rendering.
    /// </summary>
    public class MultiResolutionMapManager
    {
        public enum ZoomLevel { Global = 1, Continental, Country, State, City }

        private readonly Dictionary<ZoomLevel, Bitmap> _maps = new();
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
            int[] cellSizes = { 1, 2, 4, 6, 8 };


            for (int i = 1; i <= 5; i++)
            {
                var level = (ZoomLevel)i;

                int cellSize = cellSizes[i - 1];
                Bitmap bmp = PixelMapGenerator.GeneratePixelArtMapWithCountries(_baseWidth, _baseHeight, cellSize);
                OverlayFeatures(bmp, level);
                _maps[level] = bmp;
            }

        }

        /// <summary>
        /// Retrieve the full map bitmap for a zoom level.
        /// </summary>
        public Bitmap GetMap(ZoomLevel level)
        {
            return _maps.TryGetValue(level, out var bmp) ? bmp : null;
        }


        /// <summary>
        /// Return a cropped portion of the map at the requested zoom level.
        /// </summary>
        public Bitmap GetMap(ZoomLevel level, Rectangle view)
        {
            if (!_maps.TryGetValue(level, out var bmp))
                return null;
            Rectangle src = Rectangle.Intersect(new Rectangle(Point.Empty, bmp.Size), view);
            if (src.Width <= 0 || src.Height <= 0)
                return null;
            Bitmap dest = new Bitmap(src.Width, src.Height);
            using (Graphics g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bmp, new Rectangle(0, 0, dest.Width, dest.Height), src, GraphicsUnit.Pixel);
            }
            return dest;
        }

        private static void OverlayFeatures(Bitmap bmp, ZoomLevel level)
        {
            using Graphics g = Graphics.FromImage(bmp);
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
                        g.FillEllipse(Brushes.LightGray, x, y, size, size);
                    }
                    break;
                case ZoomLevel.State:
                    // Highways and railways as lines
                    using (Pen highway = new Pen(Color.Gray, 2))
                    {
                        g.DrawLine(highway, 0, bmp.Height / 3, bmp.Width, bmp.Height / 3);
                        g.DrawLine(highway, bmp.Width / 2, 0, bmp.Width / 2, bmp.Height);
                    }
                    using (Pen rail = new Pen(Color.DarkGray, 1) { DashStyle = DashStyle.Dot })
                    {
                        g.DrawLine(rail, 0, bmp.Height * 2 / 3, bmp.Width, bmp.Height * 2 / 3);
                    }
                    break;
                case ZoomLevel.City:
                    // Draw a simple grid of roads
                    using (Pen road = new Pen(Color.Gray, 1))
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
                        g.FillRectangle(Brushes.DarkSlateBlue, x, y, w, h);
                    }
                    for (int i = 0; i < 20; i++)
                    {
                        int x = rng.Next(bmp.Width - 3);
                        int y = rng.Next(bmp.Height - 2);
                        g.FillRectangle(Brushes.Red, x, y, 3, 2);
                    }
                    break;
            }
        }
    }
}
