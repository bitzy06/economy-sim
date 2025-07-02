using NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class ProceduralCityRenderer
    {
        public static async Task<Image<Rgba32>> RenderCityTileAsync(GeoBounds tileBounds, int cellSize)
        {
            var img = new Image<Rgba32>(Configuration.Default,
                MultiResolutionMapManager.TileSizePx,
                MultiResolutionMapManager.TileSizePx,
                new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);
            foreach (var urban in UrbanAreaManager.UrbanPolygons)
            {
                if (!urban.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal))
                    continue;
                if (!urban.Intersects(tilePoly))
                    continue;

                var model = await RoadNetworkGenerator.GenerateOrLoadModelAsync(urban).ConfigureAwait(false);
                if (model.Buildings == null)
                    continue;

                foreach (var b in model.Buildings)
                {
                    if (!b.Footprint.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal))
                        continue;
                    var visible = b.Footprint.Intersection(tilePoly);
                    if (visible is Polygon p)
                    {
                        RenderPolygon(img, p, tileBounds, MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, GetColor(b.LandUse));
                    }
                    else if (visible is MultiPolygon mp)
                    {
                        for (int i = 0; i < mp.NumGeometries; i++)
                        {
                            if (mp.GetGeometryN(i) is Polygon pp)
                                RenderPolygon(img, pp, tileBounds, MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, GetColor(b.LandUse));
                        }
                    }
                }
            }
            return img;
        }

        private static Rgba32 GetColor(LandUseType use)
        {
            return use switch
            {
                LandUseType.Commercial => new Rgba32(200, 50, 50, 180),
                LandUseType.Residential => new Rgba32(50, 50, 200, 180),
                LandUseType.Industrial => new Rgba32(120, 120, 120, 180),
                LandUseType.Park => new Rgba32(60, 160, 60, 180),
                _ => new Rgba32(100, 100, 100, 180)
            };
        }

        private static Polygon ToPolygon(GeoBounds b)
        {
            var gf = GeometryFactory.Default;
            return gf.CreatePolygon(new[]
            {
                new Coordinate(b.MinLon,b.MinLat),
                new Coordinate(b.MaxLon,b.MinLat),
                new Coordinate(b.MaxLon,b.MaxLat),
                new Coordinate(b.MinLon,b.MaxLat),
                new Coordinate(b.MinLon,b.MinLat)
            });
        }

        private static void RenderPolygon(Image<Rgba32> img, Polygon poly, GeoBounds bounds, int tileWidth, int tileHeight, Rgba32 color)
        {
            var coords = poly.ExteriorRing.Coordinates;
            var pts = new List<(int X, int Y)>(coords.Length);
            foreach (var c in coords)
            {
                int px = (int)((c.X - bounds.MinLon) / (bounds.MaxLon - bounds.MinLon) * tileWidth);
                int py = (int)((bounds.MaxLat - c.Y) / (bounds.MaxLat - bounds.MinLat) * tileHeight);
                pts.Add((px, py));
            }
            FillPolygon(img, pts, color);
        }

        private static void FillPolygon(Image<Rgba32> img, List<(int X, int Y)> points, Rgba32 color)
        {
            if (points.Count < 3) return;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in points)
            {
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            for (int y = minY; y <= maxY; y++)
            {
                List<int> nodeX = new();
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                {
                    var pi = points[i];
                    var pj = points[j];
                    if ((pi.Y < y && pj.Y >= y) || (pj.Y < y && pi.Y >= y))
                    {
                        int x = (int)(pi.X + (double)(y - pi.Y) / (pj.Y - pi.Y) * (pj.X - pi.X));
                        nodeX.Add(x);
                    }
                }
                nodeX.Sort();
                for (int k = 0; k < nodeX.Count - 1; k += 2)
                {
                    int start = nodeX[k];
                    int end = nodeX[k + 1];
                    for (int x = start; x <= end; x++)
                    {
                        if (x < 0 || x >= img.Width || y < 0 || y >= img.Height) continue;
                        var basePix = img[x, y];
                        float a = color.A / 255f;
                        byte r = (byte)(basePix.R * (1 - a) + color.R * a);
                        byte g = (byte)(basePix.G * (1 - a) + color.G * a);
                        byte b = (byte)(basePix.B * (1 - a) + color.B * a);
                        img[x, y] = new Rgba32(r, g, b, 255);
                    }
                }
            }
        }
    }
}
