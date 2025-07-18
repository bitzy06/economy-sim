using Nts = NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using NetTopologySuite.Simplify;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Drawing;
using economy_sim;

namespace StrategyGame
{
    public static class ProceduralCityRenderer
    {
        private static readonly ConcurrentDictionary<Guid, CityDataModel> _modelCache = new();
        // This method is now async to support awaiting the data model.
        public static async Task<Image<Rgba32>> RenderCityTileAsync(GeoBounds tileBounds, int cellSize)
        {
            var img = new Image<Rgba32>(MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);

            var processedModelIds = new HashSet<Guid>();
            
            var relevantUrbanAreas = UrbanAreaManager.Query(tileBounds);

            // This loop now awaits the new async methods, preventing deadlocks.
            foreach (var urban in relevantUrbanAreas)
            {
                if (!urban.EnvelopeInternal.Intersects(tilePoly.EnvelopeInternal) || !urban.Intersects(tilePoly))
                    continue;

                var modelId = await RoadNetworkGenerator.GetCityDataModelIdAsync(urban).ConfigureAwait(false);
                if (!modelId.HasValue || !processedModelIds.Add(modelId.Value))
                    continue;

                if (!_modelCache.TryGetValue(modelId.Value, out var model))
                {
                    model = await RoadNetworkGenerator.LoadCityDataModelAsync(modelId.Value).ConfigureAwait(false);
                    if (model == null) continue;
                    _modelCache[modelId.Value] = model;
                }

                DrawRoads(img, modelId.Value, model.RoadNetwork, tileBounds, cellSize);
                
                // Query visible buildings using the model's spatial index
                var tileEnv = tilePoly.EnvelopeInternal;
                var candidates = (model.BuildingIndex?.Query(tileEnv).Cast<Building>() ?? model.Buildings);
                var toDraw = new List<(Nts.Polygon, LandUseType)>();
                foreach (var b in candidates)
                {
                    var env = b.Footprint.EnvelopeInternal;
                    if (!env.Intersects(tileEnv)) continue;
                    var baseGeom = b.SimplifiedFootprint ?? b.Footprint;
                    Nts.Geometry clipped = tileEnv.Contains(env)
                        ? baseGeom
                        : baseGeom.Intersection(tilePoly);

                    if (clipped is Nts.Polygon p && !p.IsEmpty)
                        toDraw.Add((p, b.LandUse));
                    else if (clipped is Nts.MultiPolygon mp)
                    {
                        for (int i = 0; i < mp.NumGeometries; i++)
                            if (mp.GetGeometryN(i) is Nts.Polygon pp && !pp.IsEmpty)
                                toDraw.Add((pp, b.LandUse));
                    }
                }

                DrawBuildings(img, modelId.Value, toDraw, tileBounds, cellSize);
            }

            return img;
        }

        // Batched building drawing with caching
        private static void DrawBuildings(Image<Rgba32> img, Guid modelId, List<(Nts.Polygon Poly, LandUseType Use)> buildings, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();
            var cached = CityModelCache.GetOrAddBuildings(modelId, cellSize, bounds, buildings);

            img.Mutate(ctx =>
            {
                foreach (var kvp in cached.PathsByColor)
                {
                    ctx.Fill(kvp.Key, kvp.Value);
                }
            });
            PerformanceTracker.Record("CityRenderer-BuildingRendering", sw.Elapsed);
        }

        // Batched road drawing
        internal static IEnumerable<LineSegment> SimplifyRoads(IEnumerable<LineSegment> roads, GeoBounds bounds)
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

        private static void DrawRoads(Image<Rgba32> img, Guid modelId, IEnumerable<LineSegment> roads, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();
            if (cellSize <= 40)
            {
                roads = roads.Where(r => r.Type == RoadType.Primary);
            }

            var cached = CityModelCache.GetOrAddRoads(modelId, cellSize, roads, bounds);

            var primaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 2f);
            var secondaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 1f);

            img.Mutate(ctx => ctx
                .Draw(secondaryPen, cached.SecondaryPath)
                .Draw(primaryPen, cached.PrimaryPath)
            );
            PerformanceTracker.Record("CityRenderer-RoadDrawing", sw.Elapsed);
        }

        internal static SixLabors.ImageSharp.PointF ToPointF(double lon, double lat, GeoBounds b) =>
            new SixLabors.ImageSharp.PointF(
                (float)((lon - b.MinLon) / (b.MaxLon - b.MinLon) * MultiResolutionMapManager.TileSizePx),
                (float)((b.MaxLat - lat) / (b.MaxLat - b.MinLat) * MultiResolutionMapManager.TileSizePx));
        private static Nts.Polygon ToPolygon(GeoBounds b) => new Nts.Polygon(new Nts.LinearRing(new[] { new Nts.Coordinate(b.MinLon, b.MinLat), new Nts.Coordinate(b.MaxLon, b.MinLat), new Nts.Coordinate(b.MaxLon, b.MaxLat), new Nts.Coordinate(b.MinLon, b.MaxLat), new Nts.Coordinate(b.MinLon, b.MinLat) }));
        internal static Rgba32 GetBuildingColor(LandUseType use) => use switch { LandUseType.Commercial => new Rgba32(200, 50, 50, 180), LandUseType.Residential => new Rgba32(50, 50, 200, 180), LandUseType.Industrial => new Rgba32(120, 120, 120, 180), _ => new Rgba32(60, 160, 60, 180) };
    }
}
