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
            var sw = Stopwatch.StartNew();
            var img = new Image<Rgba32>(MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);

            var processedModelIds = new HashSet<Guid>();
            var relevantUrbanAreas = UrbanAreaManager.Query(tileBounds).Where(u => u.Intersects(tilePoly)).ToList();

            var models = await Task.WhenAll(relevantUrbanAreas.Select(async u =>
            {
                var id = await RoadNetworkGenerator.GetCityDataModelIdAsync(u).ConfigureAwait(false);
                if (!id.HasValue || !processedModelIds.Add(id.Value))
                    return null;

                if (!_modelCache.TryGetValue(id.Value, out var m))
                {
                    m = await RoadNetworkGenerator.LoadCityDataModelAsync(id.Value).ConfigureAwait(false);
                    if (m == null) return null;
                    _modelCache[id.Value] = m;
                }
                return m;
            }));

            var roadCaches = new List<CityModelCache.CachedRoads>();
            var buildingCaches = new List<CityModelCache.CachedBuildings>();

            foreach (var model in models.Where(m => m != null))
            {
                IEnumerable<LineSegment> roads = model.RoadNetwork;
                if (cellSize <= 40)
                    roads = roads.Where(r => r.Type == RoadType.Primary);
                var roadCache = CityModelCache.GetOrAddRoads(model.Id, cellSize, roads, tileBounds);
                roadCaches.Add(roadCache);

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

                var buildingCache = PrepareBuildingCache(model.Id, toDraw, tileBounds, cellSize);
                buildingCaches.Add(buildingCache);
            }

            if (roadCaches.Count > 0)
                DrawRoads(img, roadCaches, cellSize);
            if (buildingCaches.Count > 0)
                DrawBuildings(img, buildingCaches);

            PerformanceTracker.Record("CityRenderer-RenderTile", sw.Elapsed);
            return img;
        }

        // Convert building polygons into cached paths
        private static CityModelCache.CachedBuildings PrepareBuildingCache(Guid modelId, List<(Nts.Polygon Poly, LandUseType Use)> buildings, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();

            // 1) Cull buildings that would be too small on screen
            if (cellSize <= 40)
            {
                buildings = buildings.Where(b =>
                {
                    var env = b.Poly.EnvelopeInternal;
                    var p0 = ToPointF(env.MinX, env.MinY, bounds);
                    var p1 = ToPointF(env.MaxX, env.MaxY, bounds);
                    return Math.Abs(p1.X - p0.X) > 4 || Math.Abs(p1.Y - p0.Y) > 4;
                }).ToList();
            }

            // 2) Optionally drop residential buildings at very small scales
            if (cellSize <= 20)
            {
                buildings = buildings.Where(b => b.Use != LandUseType.Residential).ToList();
            }

            // 3) Dynamically simplify footprints based on zoom level
            double tol = (bounds.MaxLon - bounds.MinLon)
                         / MultiResolutionMapManager.TileSizePx
                         * (cellSize <= 80 ? 2.5 : 1.0);

            var reduced = new List<(Nts.Polygon Poly, LandUseType Use)>();
            foreach (var (poly, use) in buildings)
            {
                var simplified = DouglasPeuckerSimplifier.Simplify(poly, tol);
                if (simplified == null || simplified.IsEmpty) continue;

                if (simplified is Nts.Polygon p)
                {
                    reduced.Add((p, use));
                }
                else if (simplified is Nts.MultiPolygon mp)
                {
                    for (int i = 0; i < mp.NumGeometries; i++)
                    {
                        if (mp.GetGeometryN(i) is Nts.Polygon pp && !pp.IsEmpty)
                            reduced.Add((pp, use));
                    }
                }
            }

            var cached = CityModelCache.GetOrAddBuildings(modelId, cellSize, bounds, reduced);
            PerformanceTracker.Record("CityRenderer-BuildingRendering", sw.Elapsed);
            return cached;
        }

        private static void DrawBuildings(Image<Rgba32> img, IEnumerable<CityModelCache.CachedBuildings> buildings)
        {
            img.Mutate(ctx =>
            {
                foreach (var cached in buildings)
                {
                    foreach (var kvp in cached.PathsByColor)
                        ctx.Fill(kvp.Key, kvp.Value);
                }
            });
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

        private static void DrawRoads(Image<Rgba32> img, IEnumerable<CityModelCache.CachedRoads> roads, int cellSize)
        {
            var sw = Stopwatch.StartNew();
            var primaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 2f);
            var secondaryPen = SixLabors.ImageSharp.Drawing.Processing.Pens.Solid(new Rgba32(180, 180, 180, 200), 1f);

            img.Mutate(ctx =>
            {
                foreach (var cached in roads)
                {
                    ctx.Draw(secondaryPen, cached.SecondaryPath);
                    ctx.Draw(primaryPen, cached.PrimaryPath);
                }
            });
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
