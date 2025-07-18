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

        internal class CachedRoads
        {
            public IPath SecondaryPath { get; init; }
            public IPath PrimaryPath { get; init; }
        }
    }
}
