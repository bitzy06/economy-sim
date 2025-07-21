using Nts = NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using NetTopologySuite.Simplify;
using NetTopologySuite.Geometries.Prepared;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Drawing;
using SkiaSharp;
using economy_sim;

namespace StrategyGame
{
    public static class ProceduralCityRenderer
    {
        // Rendering should never block on disk. Models must be supplied via cache.
        private static readonly ConcurrentDictionary<(Guid modelId, int cellSize), List<LineSegment>> _simplifiedRoadCache = new();


        // Pre-created brushes and pens for drawing roads. These objects are immutable
        // which makes them safe for use across threads during tile rendering.
        private static readonly IBrush PrimaryRoadBrush = new SolidBrush<Rgba32>(new Rgba32(180, 180, 180, 200));
        private static readonly IBrush SecondaryRoadBrush = new SolidBrush<Rgba32>(new Rgba32(180, 180, 180, 200));

        private static readonly IPen PrimaryRoadPen = Pens.Solid(PrimaryRoadBrush, 2f);
        private static readonly IPen SecondaryRoadPen = Pens.Solid(SecondaryRoadBrush, 1f);

        private static readonly SKPaint PrimaryRoadPaint = new SKPaint
        {
            Color = new SKColor(180, 180, 180, 200),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static readonly SKPaint SecondaryRoadPaint = new SKPaint
        {
            Color = new SKColor(180, 180, 180, 200),
            StrokeWidth = 1f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        private static readonly Nts.GeometryFactory _geomFactory = Nts.GeometryFactory.Default;

        public static async Task<Image<Rgba32>> RenderCityTileAsync(
            GeoBounds tileBounds,
            int cellSize,
            IReadOnlyDictionary<Guid, CityDataModel> cityModelCache,
            Action<Guid> requestModel)
        {
            var sw = Stopwatch.StartNew();
            var img = new Image<Rgba32>(MultiResolutionMapManager.TileSizePx, MultiResolutionMapManager.TileSizePx, new Rgba32(0, 0, 0, 0));
            var tilePoly = ToPolygon(tileBounds);

            var preparedFactory = new PreparedGeometryFactory();
            var preparedTilePoly = preparedFactory.Create(tilePoly);

            var processedModelIds = new HashSet<Guid>();

            var relevantUrbanAreas = UrbanAreaManager.Query(tileBounds);
            var intersectingUrbanAreas = relevantUrbanAreas
                .Where(preparedTilePoly.Intersects)
                .ToList();

            var results = await Task
                .WhenAll(intersectingUrbanAreas
                    .Select(RoadNetworkGenerator.GetCityDataModelIdAsync))
                .ConfigureAwait(false);

            for (int i = 0; i < results.Length; i++)
            {
                var modelId = results[i];
                var urban = intersectingUrbanAreas[i];

                if (!modelId.HasValue || !processedModelIds.Add(modelId.Value))
                    continue;

                if (!cityModelCache.TryGetValue(modelId.Value, out var model))
                {
                    requestModel(modelId.Value);
                    continue;
                }

                DrawRoads(img, modelId.Value, model.RoadNetwork, tileBounds, cellSize);

                var tileEnv = tilePoly.EnvelopeInternal;
                var candidates = (model.BuildingIndex?.Query(tileEnv).Cast<Building>() ?? model.Buildings);
                var toDraw = new List<(Nts.Polygon, LandUseType, Building)>();
                foreach (var b in candidates)
                {
                    var env = b.Footprint.EnvelopeInternal;
                    if (!env.Intersects(tileEnv)) continue;
                    var baseGeom = b.SimplifiedFootprints.TryGetValue(0, out var g) ? g : b.Footprint;

                    if (baseGeom is Nts.Polygon p && !p.IsEmpty)
                    {
                        toDraw.Add((p, b.LandUse, b));
                    }
                    else if (baseGeom is Nts.MultiPolygon mp)
                    {
                        for (int i = 0; i < mp.NumGeometries; i++)
                        {
                            if (mp.GetGeometryN(i) is Nts.Polygon pp && !pp.IsEmpty)
                                toDraw.Add((pp, b.LandUse, b));
                        }
                    }
                }

                DrawBuildings(img, modelId.Value, toDraw, tileBounds, cellSize);
            }

            PerformanceTracker.Record("CityRenderer-RenderTile", sw.Elapsed);
            return img;
        }

        // Batched building drawing with caching and dynamic LOD
        private static void DrawBuildings(Image<Rgba32> img, Guid modelId, List<(Nts.Polygon Poly, LandUseType Use, Building Bld)> buildings, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();

            // Consolidated culling loop to reduce allocations
            var culledBuildings = new List<(Nts.Polygon Poly, LandUseType Use, Building Bld)>();
            bool cullBySize = cellSize <= 40;
            bool cullResidential = cellSize <= 20;

            float sx = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLon - bounds.MinLon);
            float sy = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLat - bounds.MinLat);
            foreach (var bld in buildings)
            {
                if (cullResidential && bld.Use == LandUseType.Residential)
                    continue;

                if (cullBySize)
                {
                    var env = bld.Poly.EnvelopeInternal;
                    float p0x = (float)((env.MinX - bounds.MinLon) * sx);
                    float p0y = (float)((bounds.MaxLat - env.MinY) * sy);
                    float p1x = (float)((env.MaxX - bounds.MinLon) * sx);
                    float p1y = (float)((bounds.MaxLat - env.MaxY) * sy);
                    if (Math.Abs(p1x - p0x) < 4 && Math.Abs(p1y - p0y) < 4)
                        continue;
                }

                culledBuildings.Add(bld);
            }

            // 3) Dynamically simplify footprints based on zoom level
            double tol = (bounds.MaxLon - bounds.MinLon)
                         / MultiResolutionMapManager.TileSizePx
                         * (cellSize <= 80 ? 2.5 : 1.0);

            var reduced = new List<(Nts.Polygon Poly, LandUseType Use)>();
            foreach (var (poly, use, bld) in culledBuildings)
            {
                var simplified = bld.SimplifiedFootprints.GetOrAdd(cellSize, _ =>
                {
                    var simplifiedGeom = DouglasPeuckerSimplifier.Simplify(poly, tol);
                    return (simplifiedGeom == null || simplifiedGeom.IsEmpty) ? poly : simplifiedGeom;
                });

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

            img.Mutate(ctx =>
            {
                foreach (var kvp in cached.PathsByColor)
                    ctx.Fill(kvp.Key, kvp.Value);
            });

            PerformanceTracker.Record("CityRenderer-BuildingRendering", sw.Elapsed);
        }

        // Batched road drawing
        internal static IEnumerable<LineSegment> SimplifyRoads(IEnumerable<LineSegment> roads, GeoBounds bounds)
        {
            double tolerance = (bounds.MaxLon - bounds.MinLon) / MultiResolutionMapManager.TileSizePx * 2.0;

            foreach (var group in roads.GroupBy(r => r.Type))
            {
                var polylines = GroupSegments(group.ToList());
                foreach (var lineCoords in polylines)
                {
                    var line = _geomFactory.CreateLineString(lineCoords.Select(p => new Nts.Coordinate(p.X, p.Y)).ToArray());
                    var simplified = DouglasPeuckerSimplifier.Simplify(line, tolerance);
                    if (simplified is Nts.LineString ln)
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

        private static List<List<(double X, double Y)>> GroupSegments(List<LineSegment> segments)
        {
            var result = new List<List<(double X, double Y)>>();
            var remaining = new List<LineSegment>(segments);

            while (remaining.Count > 0)
            {
                var seg = remaining[^1];
                remaining.RemoveAt(remaining.Count - 1);
                var poly = new List<(double X, double Y)> { (seg.X1, seg.Y1), (seg.X2, seg.Y2) };

                bool changed;
                do
                {
                    changed = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var s = remaining[i];
                        if (PointsEqual(poly[^1], (s.X1, s.Y1)))
                        {
                            poly.Add((s.X2, s.Y2));
                            remaining.RemoveAt(i);
                            changed = true;
                            break;
                        }
                        if (PointsEqual(poly[^1], (s.X2, s.Y2)))
                        {
                            poly.Add((s.X1, s.Y1));
                            remaining.RemoveAt(i);
                            changed = true;
                            break;
                        }
                        if (PointsEqual(poly[0], (s.X2, s.Y2)))
                        {
                            poly.Insert(0, (s.X1, s.Y1));
                            remaining.RemoveAt(i);
                            changed = true;
                            break;
                        }
                        if (PointsEqual(poly[0], (s.X1, s.Y1)))
                        {
                            poly.Insert(0, (s.X2, s.Y2));
                            remaining.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }
                } while (changed);

                result.Add(poly);
            }

            return result;
        }

        private static bool PointsEqual((double X, double Y) a, (double X, double Y) b)
        {
            const double Eps = 1e-9;
            return Math.Abs(a.X - b.X) < Eps && Math.Abs(a.Y - b.Y) < Eps;
        }

        private static void DrawRoads(Image<Rgba32> img, Guid modelId, IEnumerable<LineSegment> roads, GeoBounds bounds, int cellSize)
        {
            var sw = Stopwatch.StartNew();

            var simplifiedRoads = _simplifiedRoadCache.GetOrAdd((modelId, cellSize), _ =>
            {
                var filteredRoads = cellSize <= 40
                    ? roads.Where(r => r.Type == RoadType.Primary).ToList()
                    : roads.ToList();

                return SimplifyRoads(filteredRoads, bounds).ToList();
            });

            if (!simplifiedRoads.Any())
            {
                PerformanceTracker.Record("CityRenderer-RoadDrawing", sw.Elapsed);
                return;
            }

            var cachedPaths = CityModelCache.GetOrAddRoads(modelId, cellSize, simplifiedRoads, bounds);

            // Use shared pen instances to avoid allocations during rendering
            img.Mutate(ctx => ctx
                .Draw(SecondaryRoadPen, cachedPaths.SecondaryPath)
                .Draw(PrimaryRoadPen, cachedPaths.PrimaryPath)
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
