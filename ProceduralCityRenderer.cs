using Nts = NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SkiaSharp;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Drawing;

namespace StrategyGame
{
    public static class ProceduralCityRenderer
    {
        private static readonly bool GpuAvailable;

        static ProceduralCityRenderer()
        {
            try
            {
                using var context = GRContext.CreateGl();
                GpuAvailable = context != null;
            }
            catch
            {
                GpuAvailable = false;
            }
        }

        public static async Task<Image<Rgba32>> RenderCityTileAsync(GeoBounds tileBounds, int cellSize)
        {
            try
            {
                Console.WriteLine($"[Debug] RenderCityTileAsync: Bounds={{MinLon={tileBounds.MinLon},MaxLon={tileBounds.MaxLon},MinLat={tileBounds.MinLat},MaxLat={tileBounds.MaxLat}}}, cellSize={cellSize}");

                var img = new Image<Rgba32>(Configuration.Default,
                    MultiResolutionMapManager.TileSizePx,
                    MultiResolutionMapManager.TileSizePx,
                    new Rgba32(0, 0, 0, 0));

                SKSurface? surface = null;
                SKCanvas? canvas = null;
                if (GpuAvailable)
                {
                    var info = new SKImageInfo(MultiResolutionMapManager.TileSizePx,
                        MultiResolutionMapManager.TileSizePx);
                    try
                    {
                        var context = GRContext.CreateGl();
                        surface = SKSurface.Create(context, false, info);
                        canvas = surface.Canvas;
                        canvas.Clear(SKColors.Transparent);
                    }
                    catch
                    {
                        surface = null;
                        canvas = null;
                    }
                }
                var tilePoly = ToPolygon(tileBounds);

                foreach (var urban in UrbanAreaManager.UrbanPolygons)
                {
                    if (!urban.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal) || !urban.Intersects(tilePoly))
                        continue;

                    var model = await RoadNetworkGenerator.GenerateModelAsync(urban, cellSize).ConfigureAwait(false);
                    if (model == null)
                        continue;

                    DrawRoads(img, canvas, model.RoadNetwork, tileBounds);

                    var drawList = new List<(Nts.Polygon Poly, LandUseType Use)>();
                    Parallel.ForEach(model.Buildings, b =>
                    {
                        if (!b.Footprint.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal))
                            return;
                        var visible = b.Footprint.Intersection(tilePoly);
                        if (visible is Nts.Polygon p)
                        {
                            lock (drawList) drawList.Add((p, b.LandUse));
                        }
                        else if (visible is Nts.MultiPolygon mp)
                        {
                            for (int i = 0; i < mp.NumGeometries; i++)
                            {
                                if (mp.GetGeometryN(i) is Nts.Polygon pp)
                                    lock (drawList) drawList.Add((pp, b.LandUse));
                            }
                        }
                    });

                    if (canvas != null)
                    {
                        foreach (var grp in drawList.GroupBy(d => d.Use))
                        {
                            using var path = new SKPath();
                            foreach (var item in grp)
                            {
                                AppendPolygon(path, item.Poly, tileBounds);
                            }
                            using var paint = new SKPaint
                            {
                                Color = ToSkColor(GetBuildingColor(grp.Key)),
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            };
                            canvas.DrawPath(path, paint);
                        }
                    }
                    else
                    {
                        foreach (var item in drawList)
                            RenderPolygon(img, null, item.Poly, tileBounds, GetBuildingColor(item.Use));
                    }
                }

                if (surface != null)
                {
                    using var snapshot = surface.Snapshot();
                    using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
                    img.Dispose();
                    img = Image.Load<Rgba32>(data.AsStream());
                }

                return img;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[Error] RenderCityTileAsync ArgumentException: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] RenderCityTileAsync Exception: {ex.Message}");
                throw;
            }
        }

        private static void DrawRoads(Image<Rgba32> img, SKCanvas? canvas, IEnumerable<LineSegment> roads, GeoBounds bounds)
        {
            if (canvas != null)
            {
                foreach (var seg in roads)
                {
                    float width = seg.Type == RoadType.Primary ? 2f : 1f;
                    using var paint = new SKPaint
                    {
                        Color = new SKColor(180, 180, 180, 200),
                        StrokeWidth = width,
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true
                    };
                    var p1 = ToSKPoint(seg.X1, seg.Y1, bounds);
                    var p2 = ToSKPoint(seg.X2, seg.Y2, bounds);
                    canvas.DrawLine(p1, p2, paint);
                }
            }
            else
            {
                img.Mutate(ctx =>
                {
                    foreach (var seg in roads)
                    {
                        float width = seg.Type == RoadType.Primary ? 2f : 1f;
                        var pen = Pens.Solid(new Rgba32(180, 180, 180, 200), width);
                        var p1 = ToPointF(seg.X1, seg.Y1, bounds);
                        var p2 = ToPointF(seg.X2, seg.Y2, bounds);
                        ctx.DrawLine(pen, p1, p2);
                    }
                });
            }
        }

        private static SixLabors.ImageSharp.PointF ToPointF(double lon, double lat, GeoBounds b)
        {
            float x = (float)((lon - b.MinLon) / (b.MaxLon - b.MinLon) * MultiResolutionMapManager.TileSizePx);
            float y = (float)((b.MaxLat - lat) / (b.MaxLat - b.MinLat) * MultiResolutionMapManager.TileSizePx);
            return new SixLabors.ImageSharp.PointF(x, y);
        }

        private static SKPoint ToSKPoint(double lon, double lat, GeoBounds b)
        {
            float x = (float)((lon - b.MinLon) / (b.MaxLon - b.MinLon) * MultiResolutionMapManager.TileSizePx);
            float y = (float)((b.MaxLat - lat) / (b.MaxLat - b.MinLat) * MultiResolutionMapManager.TileSizePx);
            return new SKPoint(x, y);
        }

        private static SKColor ToSkColor(Rgba32 c) => new SKColor(c.R, c.G, c.B, c.A);

        private static void AppendPolygon(SKPath path, Nts.Polygon poly, GeoBounds bounds)
        {
            var coords = poly.ExteriorRing.Coordinates;
            if (coords.Length == 0) return;
            path.MoveTo(ToSKPoint(coords[0].X, coords[0].Y, bounds));
            for (int i = 1; i < coords.Length; i++)
            {
                path.LineTo(ToSKPoint(coords[i].X, coords[i].Y, bounds));
            }
            path.Close();
        }

        private static Rgba32 GetBuildingColor(LandUseType use)
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

        private static Nts.Polygon ToPolygon(GeoBounds b)
        {
            if (b.MinLon >= b.MaxLon || b.MinLat >= b.MaxLat)
                throw new ArgumentException("Invalid GeoBounds: Min must be less than Max.");

            var gf = Nts.GeometryFactory.Default;
            return gf.CreatePolygon(new[]
            {
                new Nts.Coordinate(b.MinLon, b.MinLat),
                new Nts.Coordinate(b.MaxLon, b.MinLat),
                new Nts.Coordinate(b.MaxLon, b.MaxLat),
                new Nts.Coordinate(b.MinLon, b.MaxLat),
                new Nts.Coordinate(b.MinLon, b.MinLat)
            });
        }

        private static void RenderPolygon(Image<Rgba32> img, SKCanvas? canvas, Nts.Polygon poly, GeoBounds bounds, Rgba32 color)
        {
            if (canvas != null)
            {
                using var path = new SKPath();
                AppendPolygon(path, poly, bounds);
                using var paint = new SKPaint
                {
                    Color = ToSkColor(color),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                canvas.DrawPath(path, paint);
            }
            else
            {
                var coords = poly.ExteriorRing.Coordinates.Select(c => ToPointF(c.X, c.Y, bounds)).ToArray();
                img.Mutate(ctx => ctx.Fill(color, new Polygon(coords)));
            }
        }
    }
}
