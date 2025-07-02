using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static Dictionary<Polygon, List<LineSegment>> networkCache = new();

        public static List<LineSegment> GetOrGenerateFor(Polygon urbanArea, int cellSize)
        {
            // Low zoom levels use a simple fill instead of detailed roads
            if (cellSize < 40)
                return new List<LineSegment>();

            if (networkCache.TryGetValue(urbanArea, out var cached))
                return cached;

            var env = urbanArea.EnvelopeInternal;
            double diagonal = System.Math.Sqrt(env.Width * env.Width + env.Height * env.Height);
            int divisions = System.Math.Clamp((int)(diagonal * 40.0), 5, 100);

            double stepX = env.Width / divisions;
            double stepY = env.Height / divisions;

            var gridLines = new List<LineSegment>();
            for (int i = 0; i <= divisions; i++)
            {
                double x = env.MinX + i * stepX;
                gridLines.Add(new LineSegment(x, env.MinY, x, env.MaxY));
            }
            for (int j = 0; j <= divisions; j++)
            {
                double y = env.MinY + j * stepY;
                gridLines.Add(new LineSegment(env.MinX, y, env.MaxX, y));
            }

            var clippedNetwork = new List<LineSegment>();
            var factory = GeometryFactory.Default;

            foreach (var line in gridLines)
            {
                var lineString = factory.CreateLineString(new[]
                {
                    new Coordinate(line.X1, line.Y1),
                    new Coordinate(line.X2, line.Y2)
                });

                if (!urbanArea.Intersects(lineString))
                    continue;

                var intersection = urbanArea.Intersection(lineString);

                if (intersection is LineString ls)
                {
                    var coords = ls.Coordinates;
                    for (int c = 0; c < coords.Length - 1; c++)
                    {
                        clippedNetwork.Add(new LineSegment(
                            coords[c].X, coords[c].Y,
                            coords[c + 1].X, coords[c + 1].Y));
                    }
                }
                else if (intersection is MultiLineString mls)
                {
                    foreach (var geom in mls.Geometries)
                    {
                        if (geom is LineString subLs)
                        {
                            var coords = subLs.Coordinates;
                            for (int c = 0; c < coords.Length - 1; c++)
                            {
                                clippedNetwork.Add(new LineSegment(
                                    coords[c].X, coords[c].Y,
                                    coords[c + 1].X, coords[c + 1].Y));
                            }
                        }
                    }
                }
            }

            networkCache[urbanArea] = clippedNetwork;
            return clippedNetwork;
        }
    }
}
