using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static Dictionary<Polygon, List<LineSegment>> networkCache = new();

        public static List<LineSegment> GetOrGenerateFor(Polygon urbanArea)
        {
            if (networkCache.TryGetValue(urbanArea, out var cached))
                return cached;

            var env = urbanArea.EnvelopeInternal;
            int grid = 50;
            double stepX = (env.MaxX - env.MinX) / grid;
            double stepY = (env.MaxY - env.MinY) / grid;
            var list = new List<LineSegment>();
            for (int i = 0; i <= grid; i++)
            {
                double x = env.MinX + i * stepX;
                list.Add(new LineSegment(x, env.MinY, x, env.MaxY));
            }
            for (int j = 0; j <= grid; j++)
            {
                double y = env.MinY + j * stepY;
                list.Add(new LineSegment(env.MinX, y, env.MaxX, y));
            }
            networkCache[urbanArea] = list;
            return list;
        }
    }
}
