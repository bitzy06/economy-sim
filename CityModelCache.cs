using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    internal static class CityModelCache
    {
        private static readonly ConcurrentDictionary<(Guid modelId, int cellSize), CachedRoads> _roads = new();
        private static readonly ConcurrentDictionary<(Guid modelId, int cellSize, double minLon, double minLat, double maxLon, double maxLat), CachedBuildings> _buildings = new();

        public static CachedRoads GetOrAddRoads(Guid modelId, int cellSize, IEnumerable<LineSegment> rawRoads, GeoBounds bounds)
        {
            return _roads.GetOrAdd((modelId, cellSize), _ =>
            {
                var tileBox = new NetTopologySuite.Geometries.Envelope(bounds.MinLon, bounds.MaxLon, bounds.MinLat, bounds.MaxLat);
                var clipped = rawRoads.Where(r =>
                {
                    double minX = Math.Min(r.X1, r.X2);
                    double maxX = Math.Max(r.X1, r.X2);
                    double minY = Math.Min(r.Y1, r.Y2);
                    double maxY = Math.Max(r.Y1, r.Y2);
                    return !(maxX < tileBox.MinX || minX > tileBox.MaxX || maxY < tileBox.MinY || minY > tileBox.MaxY);
                });

                var simplified = ProceduralCityRenderer.SimplifyRoads(clipped, bounds).ToList();

                var primaryBuilder = new PathBuilder();
                var secondaryBuilder = new PathBuilder();
                foreach (var seg in simplified)
                {
                    var pb = seg.Type == RoadType.Primary ? primaryBuilder : secondaryBuilder;
                    pb.AddLine(
                        ProceduralCityRenderer.ToPointF(seg.X1, seg.Y1, bounds),
                        ProceduralCityRenderer.ToPointF(seg.X2, seg.Y2, bounds));
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
                foreach (var group in buildings.GroupBy(b => ProceduralCityRenderer.GetBuildingColor(b.Use)))
                {
                    var pb = new PathBuilder();
                    foreach (var item in group)
                    {
                        pb.AddPolygon(new Polygon(new LinearLineSegment(item.Poly.ExteriorRing.Coordinates.Select(c => ProceduralCityRenderer.ToPointF(c.X, c.Y, bounds)).ToArray())));
                    }
                    dict[group.Key] = pb.Build();
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
