using Nts = NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using NetTopologySuite.Simplify;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Drawing;
using economy_sim;

namespace StrategyGame
{
    public static class ProceduralCityRenderer
    {
        // This method is now async to support awaiting the data model.
        public static async Task<Image<Rgba32>> RenderCityTileAsync(GeoBounds tileBounds, int cellSize)
        {
            var img = new Image<Rgba32>(MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);

            var allBuildingsToDraw = new List<(Nts.Polygon Poly, LandUseType Use)>();
            var allRoadsToDraw = new List<LineSegment>();
            var processedModelIds = new HashSet<Guid>();
            
            var relevantUrbanAreas = UrbanAreaManager.Query(tileBounds);

            // This loop now awaits the new async methods, preventing deadlocks.
            foreach (var urban in relevantUrbanAreas)
            {
                if (!urban.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal) || !urban.Intersects(tilePoly)) continue;

                // Use the new async GetCityDataModelIdAsync
                var modelId = await RoadNetworkGenerator.GetCityDataModelIdAsync(urban).ConfigureAwait(false);
                if (!modelId.HasValue || !processedModelIds.Add(modelId.Value)) continue;
                
                // Use the new async LoadCityDataModelAsync
                var model = await RoadNetworkGenerator.LoadCityDataModelAsync(modelId.Value).ConfigureAwait(false);
                if (model == null) continue;

                allRoadsToDraw.AddRange(model.RoadNetwork);
                
                // Process building geometry in parallel
                var buildingGeoms = model.Buildings
                    .AsParallel()
                    .Select(b => new { b.LandUse, Visible = b.Footprint.Intersection(tilePoly) })
                    .Where(b => b.Visible != null && !b.Visible.IsEmpty)
                    .ToList();

                foreach(var b in buildingGeoms)
                {
                    if (b.Visible is Nts.Polygon p) lock (allBuildingsToDraw) allBuildingsToDraw.Add((p, b.LandUse));
                    else if (b.Visible is Nts.MultiPolygon mp)
                    {
                        for (int i = 0; i < mp.NumGeometries; i++)
                            if (mp.GetGeometryN(i) is Nts.Polygon pp) lock (allBuildingsToDraw) allBuildingsToDraw.Add((pp, b.LandUse));
                    }
                }
            }

            // Call the optimized, batched drawing methods
            DrawBuildings(img, allBuildingsToDraw, tileBounds);
            DrawRoads(img, allRoadsToDraw, tileBounds, cellSize);
            
            return img;
        }
        
        // Batched building drawing
        private static void DrawBuildings(Image<Rgba32> img, List<(Nts.Polygon Poly, LandUseType Use)> buildings, GeoBounds bounds)
        {
            var sw = Stopwatch.StartNew();
            var buildingsByColor = buildings.GroupBy(b => GetBuildingColor(b.Use));

            img.Mutate(ctx =>
            {
                foreach (var group in buildingsByColor)
                {
                    var color = group.Key;
                    var paths = new PathCollection(group.Select(item =>
                        new Polygon(new LinearLineSegment(item.Poly.ExteriorRing.Coordinates.Select(c => ToPointF(c.X, c.Y, bounds)).ToArray()))
                    ));
                    ctx.Fill(color, paths);
                }
            });
            PerformanceTracker.Record("CityRenderer-BuildingRendering", sw.Elapsed);
        }

        // Batched road drawing
        private static IEnumerable<LineSegment> SimplifyRoads(IEnumerable<LineSegment> roads, GeoBounds bounds)
        {
            double tolerance = (bounds.MaxLon - bounds.MinLon) / MultiResolutionMapManager.TileSizePx * 2.0;

            var gf = Nts.GeometryFactory.Default;
            foreach (var group in roads.GroupBy(r => r.Type))
            {
                var lineStrings = group.Select(s =>
                    gf.CreateLineString(new[]
                    {
                        new Nts.Coordinate(s.X1, s.Y1),
                        new Nts.Coordinate(s.X2, s.Y2)
                    })).ToArray();

                if (lineStrings.Length == 0) continue;

                var multi = gf.CreateMultiLineString(lineStrings);
                var simplified = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(multi, tolerance) as Nts.MultiLineString;
                if (simplified == null) continue;

                for (int i = 0; i < simplified.NumGeometries; i++)
                {
                    if (simplified.GetGeometryN(i) is Nts.LineString ln)
                    {
                        for (int j = 0; j < ln.NumPoints - 1; j++)
                        {
                            var c1 = ln.GetCoordinateN(j);
                            var c2 = ln.GetCoordinateN(j + 1);
                            yield return new LineSegment(c1.X, c1.Y, c2.X, c2.Y, group.Key);
                        }
                    }
                }
            }
        }

        private static void DrawRoads(Image<Rgba32> img, IEnumerable<LineSegment> roads, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();
            if (cellSize <= 40)
            {
                roads = roads.Where(r => r.Type == RoadType.Primary);
            }

            roads = SimplifyRoads(roads, bounds).ToList();
            var primaryPathBuilder = new PathBuilder();
            var secondaryPathBuilder = new PathBuilder();
            foreach (var seg in roads)
            {
                var pathBuilder = seg.Type == RoadType.Primary ? primaryPathBuilder : secondaryPathBuilder;
                pathBuilder.AddLine(ToPointF(seg.X1, seg.Y1, bounds), ToPointF(seg.X2, seg.Y2, bounds));
            }

            var primaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 2f);
            var secondaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 1f);

            img.Mutate(ctx => ctx
                .Draw(secondaryPen, secondaryPathBuilder.Build())
                .Draw(primaryPen, primaryPathBuilder.Build())
            );
            PerformanceTracker.Record("CityRenderer-RoadDrawing", sw.Elapsed);
        }

        private static SixLabors.ImageSharp.PointF ToPointF(double lon, double lat, GeoBounds b) =>
            new SixLabors.ImageSharp.PointF(
                (float)((lon - b.MinLon) / (b.MaxLon - b.MinLon) * MultiResolutionMapManager.TileSizePx),
                (float)((b.MaxLat - lat) / (b.MaxLat - b.MinLat) * MultiResolutionMapManager.TileSizePx));
        private static Nts.Polygon ToPolygon(GeoBounds b) => new Nts.Polygon(new Nts.LinearRing(new[] { new Nts.Coordinate(b.MinLon, b.MinLat), new Nts.Coordinate(b.MaxLon, b.MinLat), new Nts.Coordinate(b.MaxLon, b.MaxLat), new Nts.Coordinate(b.MinLon, b.MaxLat), new Nts.Coordinate(b.MinLon, b.MinLat) }));
        private static Rgba32 GetBuildingColor(LandUseType use) => use switch { LandUseType.Commercial => new Rgba32(200, 50, 50, 180), LandUseType.Residential => new Rgba32(50, 50, 200, 180), LandUseType.Industrial => new Rgba32(120, 120, 120, 180), _ => new Rgba32(60, 160, 60, 180) };
    }
}
