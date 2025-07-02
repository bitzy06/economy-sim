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

        public static async Task<CityDataModel?> LoadModelAsync(Polygon urbanArea)
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
            return null;
        }

        public static async Task<CityDataModel> GenerateModelAsync(Polygon urbanArea)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            string hash = Math.Abs(urbanArea.EnvelopeInternal.GetHashCode()).ToString();
            string path = Path.Combine(dir, $"{hash}.json");

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            var result = new CityDataModel { Id = Guid.NewGuid() };

            var env = urbanArea.EnvelopeInternal;
            var rnd = new Random();
            var factory = GeometryFactory.Default;
            var lines = new List<LineSegment>();

            double step = Math.Min(env.Width, env.Height) / 12.0;
            var center = new Coordinate((env.MinX + env.MaxX) / 2.0, (env.MinY + env.MaxY) / 2.0);
            var initial = new (Coordinate start, double angle, int depth)[]
            {
                (center, 0.0, 0)
            };

            var queue = new Queue<(Coordinate start, double angle, int depth)>(initial);
            int maxDepth = 4;

            bool IsValid(LineSegment s)
            {
                var ls = factory.CreateLineString(new[] { new Coordinate(s.X1, s.Y1), new Coordinate(s.X2, s.Y2) });
                if (!urbanArea.Contains(ls.EndPoint))
                    return false;
                foreach (var existing in lines)
                {
                    var existingLs = factory.CreateLineString(new[] { new Coordinate(existing.X1, existing.Y1), new Coordinate(existing.X2, existing.Y2) });
                    if (ls.Distance(existingLs) < step * 0.5)
                        return false;
                }
                return true;
            }

            while (queue.Count > 0)
            {
                var (start, angle, depth) = queue.Dequeue();
                double len = step * (1.0 + rnd.NextDouble() * 0.5);
                var end = new Coordinate(start.X + Math.Cos(angle) * len, start.Y + Math.Sin(angle) * len);
                var seg = new LineSegment(start.X, start.Y, end.X, end.Y);
                if (IsValid(seg))
                {
                    lines.Add(seg);
                    if (depth < maxDepth)
                    {
                        double branch = Math.PI / 6.0; // ~30 degrees
                        queue.Enqueue((end, angle + branch, depth + 1));
                        queue.Enqueue((end, angle - branch, depth + 1));
                    }
                }
            }

            result.RoadNetwork = lines;

            ParcelGenerator.GenerateParcels(result);
            LandUseAssigner.AssignLandUse(result);
            BuildingGenerator.GenerateBuildings(result);

            var jsonOut = System.Text.Json.JsonSerializer.Serialize(result);
            await File.WriteAllTextAsync(path, jsonOut).ConfigureAwait(false);
            modelCache[hash] = result;
            return result;
        }
    }
}
