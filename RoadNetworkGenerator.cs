using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly Dictionary<Polygon, List<LineSegment>> networkCache = new();
        private static readonly Dictionary<string, CityDataModel> modelCache = new();

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

        public static async Task<CityDataModel> GenerateOrLoadModelAsync(Polygon urbanArea)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            string hash = Math.Abs(urbanArea.EnvelopeInternal.GetHashCode()).ToString();
            string path = Path.Combine(dir, $"{hash}.json");

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                var model = System.Text.Json.JsonSerializer.Deserialize<CityDataModel>(json);
                modelCache[hash] = model;
                return model;
            }

            var result = new CityDataModel { Id = Guid.NewGuid() };

            var env = urbanArea.EnvelopeInternal;
            var rnd = new Random();
            var lines = new List<LineSegment>();
            var queue = new Queue<LineSegment>();
            queue.Enqueue(new LineSegment(env.MinX, env.MinY, env.MaxX, env.MaxY));

            int iterations = 3;
            var factory = GeometryFactory.Default;

            for (int i = 0; i < iterations && queue.Count < 100; i++)
            {
                int count = queue.Count;
                for (int j = 0; j < count; j++)
                {
                    var seg = queue.Dequeue();
                    var ls = factory.CreateLineString(new[] { new Coordinate(seg.X1, seg.Y1), new Coordinate(seg.X2, seg.Y2) });
                    if (!urbanArea.Intersects(ls))
                        continue;
                    var inter = urbanArea.Intersection(ls);
                    if (inter is LineString l)
                    {
                        var coords = l.Coordinates;
                        for (int k = 0; k < coords.Length - 1; k++)
                        {
                            var s = new LineSegment(coords[k].X, coords[k].Y, coords[k + 1].X, coords[k + 1].Y);
                            lines.Add(s);
                            double midX = (s.X1 + s.X2) / 2.0;
                            double midY = (s.Y1 + s.Y2) / 2.0;
                            double angle = rnd.NextDouble() * Math.PI;
                            double len = (env.Width + env.Height) / 40.0;
                            var nx = midX + Math.Cos(angle) * len;
                            var ny = midY + Math.Sin(angle) * len;
                            queue.Enqueue(new LineSegment(midX, midY, nx, ny));
                        }
                    }
                }
            }

            result.RoadNetwork = lines;
            var jsonOut = System.Text.Json.JsonSerializer.Serialize(result);
            await File.WriteAllTextAsync(path, jsonOut).ConfigureAwait(false);
            modelCache[hash] = result;
            return result;
        }
    }
}
