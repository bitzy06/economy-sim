using NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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

                var model = await RoadNetworkGenerator.LoadModelAsync(urban).ConfigureAwait(false);
                if (model == null)
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
            if (points.Count < 3)
                return;

            var polygonShape = new SixLabors.ImageSharp.Drawing.Polygon(points.Select(p => new PointF(p.X, p.Y)).ToArray());
            img.Mutate(ctx => ctx.Fill(color, polygonShape));
        }
    }
}
