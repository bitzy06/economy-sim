using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    internal static class CityModelCache
    {
        private static readonly ConcurrentDictionary<TileKey, CachedRoads> _roads = new();
        private static readonly ConcurrentDictionary<(Guid modelId, int cellSize, double minLon, double minLat, double maxLon, double maxLat), CachedBuildings> _buildings = new();

        public static CachedRoads GetOrAddRoads(Guid modelId, int cellSize, int tileX, int tileY, IEnumerable<LineSegment> rawRoads, GeoBounds bounds)
        {
            var key = new TileKey(modelId, cellSize, tileX, tileY);
            return _roads.GetOrAdd(key, _ =>
            {
                var clippedSegments = new List<LineSegment>();
                foreach (var seg in rawRoads)
                {
                    double x1 = seg.X1;
                    double y1 = seg.Y1;
                    double x2 = seg.X2;
                    double y2 = seg.Y2;
                    if (GeometryUtil.ClipLine(bounds, ref x1, ref y1, ref x2, ref y2))
                    {
                        clippedSegments.Add(new LineSegment(x1, y1, x2, y2, seg.Type));
                    }
                }

                var simplified = ProceduralCityRenderer.SimplifyRoads(clippedSegments, bounds).ToList();

                var primaryBuilder = new PathBuilder();
                var secondaryBuilder = new PathBuilder();
                float sx = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLon - bounds.MinLon);
                float sy = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLat - bounds.MinLat);
                foreach (var seg in simplified)
                {
                    var pb = seg.Type == RoadType.Primary ? primaryBuilder : secondaryBuilder;
                    pb.AddLine(
                        new SixLabors.ImageSharp.PointF(
                            (float)((seg.X1 - bounds.MinLon) * sx),
                            (float)((bounds.MaxLat - seg.Y1) * sy)),
                        new SixLabors.ImageSharp.PointF(
                            (float)((seg.X2 - bounds.MinLon) * sx),
                            (float)((bounds.MaxLat - seg.Y2) * sy)));
                }

                return new CachedRoads
                {
                    SecondaryPath = secondaryBuilder.Build(),
                    PrimaryPath = primaryBuilder.Build()
                };
            });
        }

        public static CachedBuildings GetOrAddBuildings(Guid modelId, int cellSize, GeoBounds bounds, IEnumerable<(NetTopologySuite.Geometries.Polygon Poly, LandUseType Use)> buildings)
        {
            var key = (modelId, cellSize, bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat);
            return _buildings.GetOrAdd(key, _ =>
            {
                var dict = new Dictionary<Rgba32, IPath>();
                float sx = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLon - bounds.MinLon);
                float sy = MultiResolutionMapManager.TileSizePx / (float)(bounds.MaxLat - bounds.MinLat);
                foreach (var group in buildings.GroupBy(b => ProceduralCityRenderer.GetBuildingColor(b.Use)))
                {
                    var pb = new PathBuilder();
                    foreach (var item in group)
                    {
                        var points = item.Poly.ExteriorRing.Coordinates.Select(c =>
                            new SixLabors.ImageSharp.PointF(
                                (float)((c.X - bounds.MinLon) * sx),
                                (float)((bounds.MaxLat - c.Y) * sy))).ToArray();
                        pb.AddLines(points);
                        pb.CloseFigure();
                    }
                    dict[(Rgba32)group.Key] = pb.Build();
                }

                return new CachedBuildings { PathsByColor = dict };
            });
        }

        internal class CachedRoads
        {
            public IPath SecondaryPath { get; init; }
            public IPath PrimaryPath { get; init; }
        }

        internal class CachedBuildings
        {
            public Dictionary<Rgba32, IPath> PathsByColor { get; init; } = new();
        }
    }
}
