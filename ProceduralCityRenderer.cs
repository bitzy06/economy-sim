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
using System.Diagnostics;
using economy_sim;

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

        public static Task<Image<Rgba32>> RenderCityTileAsync(GeoBounds tileBounds, int cellSize)
        {
            var img = new Image<Rgba32>(MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);

            var allBuildingsToDraw = new List<(Nts.Polygon Poly, LandUseType Use)>();
            var allRoadsToDraw = new List<LineSegment>();
            var processedModelIds = new HashSet<Guid>();

            var relevantUrbanAreas = UrbanAreaManager.Query(tileBounds);
            foreach (var urban in relevantUrbanAreas)
            {
                if (!urban.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal) || !urban.Intersects(tilePoly)) continue;
                var modelId = RoadNetworkGenerator.GetCityDataModelId(urban);
                if (!modelId.HasValue || !processedModelIds.Add(modelId.Value)) continue;

                var model = RoadNetworkGenerator.LoadCityDataModel(modelId.Value);
                if (model == null) continue;

                allRoadsToDraw.AddRange(model.RoadNetwork);

                var buildingGeoms = model.Buildings
                    .AsParallel()
                    .Select(b => new { LandUse = b.LandUse, Visible = b.Footprint.Intersection(tilePoly) })
                    .Where(b => b.Visible != null && !b.Visible.IsEmpty)
                    .ToList();

                foreach (var b in buildingGeoms)
                {
                    if (b.Visible is Nts.Polygon p) lock (allBuildingsToDraw) allBuildingsToDraw.Add((p, b.LandUse));
                    else if (b.Visible is Nts.MultiPolygon mp)
                    {
                        for (int i = 0; i < mp.NumGeometries; i++)
                            if (mp.GetGeometryN(i) is Nts.Polygon pp) lock (allBuildingsToDraw) allBuildingsToDraw.Add((pp, b.LandUse));
                    }
                }
            }

            DrawBuildings(img, allBuildingsToDraw, tileBounds);
            DrawRoads(img, allRoadsToDraw, tileBounds);

            return Task.FromResult(img);
        }

        private static void DrawBuildings(Image<Rgba32> img, List<(Nts.Polygon Poly, LandUseType Use)> buildings, GeoBounds bounds)
        {
            var buildingsByColor = buildings.GroupBy(b => GetBuildingColor(b.Use));

            img.Mutate(ctx =>
            {
                foreach (var group in buildingsByColor)
                {
                    var color = group.Key;
                    var paths = new PathCollection(group.Select(item =>
                        new Polygon(new LinearLineSegment(item.Poly.ExteriorRing.Coordinates.Select(c => ToPointF(c.X, c.Y, bounds)).ToArray()))));
                    ctx.Fill(color, paths);
                }
            });
        }

        private static void DrawRoads(Image<Rgba32> img, IEnumerable<LineSegment> roads, GeoBounds bounds)
        {
            var primaryPathBuilder = new PathBuilder();
            var secondaryPathBuilder = new PathBuilder();
            foreach (var seg in roads)
            {
                var builder = seg.Type == RoadType.Primary ? primaryPathBuilder : secondaryPathBuilder;
                builder.AddLine(ToPointF(seg.X1, seg.Y1, bounds), ToPointF(seg.X2, seg.Y2, bounds));
            }

            var primaryPen = Pens.Solid(new Rgba32(180, 180, 180, 200), 2f);
            var secondaryPen = Pens.Solid(new Rgba32(180, 180, 180, 200), 1f);

            img.Mutate(ctx => ctx
                .Draw(secondaryPen, secondaryPathBuilder.Build())
                .Draw(primaryPen, primaryPathBuilder.Build()));
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
            var sw = Stopwatch.StartNew();
            if (b.MinLon >= b.MaxLon || b.MinLat >= b.MaxLat)
                throw new ArgumentException("Invalid GeoBounds: Min must be less than Max.");

            var gf = Nts.GeometryFactory.Default;
            var result = gf.CreatePolygon(new[]
            {
                new Nts.Coordinate(b.MinLon, b.MinLat),
                new Nts.Coordinate(b.MaxLon, b.MinLat),
                new Nts.Coordinate(b.MaxLon, b.MaxLat),
                new Nts.Coordinate(b.MinLon, b.MaxLat),
                new Nts.Coordinate(b.MinLon, b.MinLat)
            });
            PerformanceTracker.Record("ToPolygon", sw.Elapsed);
            return result;
        }

    }
}
