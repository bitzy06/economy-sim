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
        private static readonly ConcurrentDictionary<(int cellSize, double minLon, double minLat, double maxLon, double maxLat), CachedRoads> _roadTiles = new();
        private static readonly ConcurrentDictionary<(int cellSize, double minLon, double minLat, double maxLon, double maxLat), CachedBuildings> _buildingTiles = new();

        public static CachedRoads GetOrAddRoads(int cellSize, GeoBounds bounds, IEnumerable<LineSegment> rawRoads)
        {
            var key = (cellSize, bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat);
            return _roadTiles.GetOrAdd(key, _ =>
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

        public static CachedBuildings GetOrAddBuildings(int cellSize, GeoBounds bounds, IEnumerable<(NetTopologySuite.Geometries.Polygon Poly, LandUseType Use)> buildings)
        {
            var key = (cellSize, bounds.MinLon, bounds.MinLat, bounds.MaxLon, bounds.MaxLat);
            return _buildingTiles.GetOrAdd(key, _ =>
            {
                var dict = new Dictionary<Rgba32, IPath>();
                foreach (var group in buildings.GroupBy(b => ProceduralCityRenderer.GetBuildingColor(b.Use)))
                {
                    var pb = new PathBuilder();
                    foreach (var item in group)
                    {
                        var points = item.Poly.ExteriorRing.Coordinates.Select(c => ProceduralCityRenderer.ToPointF(c.X, c.Y, bounds)).ToArray();
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
