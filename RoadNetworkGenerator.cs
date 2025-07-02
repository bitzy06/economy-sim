using Nts = NetTopologySuite.Geometries;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly Dictionary<Nts.Polygon, List<LineSegment>> networkCache = new();
        private static readonly Dictionary<string, CityDataModel> modelCache = new();

        public static List<LineSegment> GetOrGenerateFor(Nts.Polygon urbanArea, int cellSize)
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
            var factory = Nts.GeometryFactory.Default;

            foreach (var line in gridLines)
            {
                var lineString = factory.CreateLineString(new[]
                {
                    new Nts.Coordinate(line.X1, line.Y1),
                    new Nts.Coordinate(line.X2, line.Y2)
                });

                if (!urbanArea.Intersects(lineString))
                    continue;

                var intersection = urbanArea.Intersection(lineString);

                if (intersection is Nts.LineString ls)
                {
                    var coords = ls.Coordinates;
                    for (int c = 0; c < coords.Length - 1; c++)
                    {
                        clippedNetwork.Add(new LineSegment(
                            coords[c].X, coords[c].Y,
                            coords[c + 1].X, coords[c + 1].Y));
                    }
                }
                else if (intersection is Nts.MultiLineString mls)
                {
                    foreach (var geom in mls.Geometries)
                    {
                        if (geom is Nts.LineString subLs)
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

        public static async Task<CityDataModel?> LoadModelAsync(Nts.Polygon urbanArea)
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

        public static async Task<CityDataModel> GenerateModelAsync(
            Nts.Polygon urbanArea,
            PopulationDensityMap popDensity,
            WaterBodyMap waterMap,
            TerrainData terrain)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            string hash = Math.Abs(urbanArea.EnvelopeInternal.GetHashCode()).ToString();
            string path = Path.Combine(dir, $"{hash}.json");

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            var result = new CityDataModel { Id = Guid.NewGuid() };

            // --- START: Copied and adapted from GetOrGenerateFor ---
            var env = urbanArea.EnvelopeInternal;
            double diagonal = System.Math.Sqrt(env.Width * env.Width + env.Height * env.Height);
            int divisions = System.Math.Clamp((int)(diagonal * 40.0), 10, 150);

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
            var factory = Nts.GeometryFactory.Default;

            foreach (var line in gridLines)
            {
                var lineString = factory.CreateLineString(new[]
                {
                    new Nts.Coordinate(line.X1, line.Y1),
                    new Nts.Coordinate(line.X2, line.Y2)
                });

                if (!urbanArea.Intersects(lineString))
                    continue;

                var intersection = urbanArea.Intersection(lineString);

                if (intersection is Nts.LineString ls)
                {
                    var coords = ls.Coordinates;
                    for (int c = 0; c < coords.Length - 1; c++)
                    {
                        clippedNetwork.Add(new LineSegment(
                            coords[c].X, coords[c].Y,
                            coords[c + 1].X, coords[c + 1].Y));
                    }
                }
                else if (intersection is Nts.MultiLineString mls)
                {
                    foreach (var geom in mls.Geometries)
                    {
                        if (geom is Nts.LineString subLs)
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
            result.RoadNetwork = clippedNetwork;
            // --- END: Copied logic ---

            ParcelGenerator.GenerateParcels(result);
            LandUseAssigner.AssignLandUse(result);
            BuildingGenerator.GenerateBuildings(result);

            var jsonOut = System.Text.Json.JsonSerializer.Serialize(result);
            await File.WriteAllTextAsync(path, jsonOut).ConfigureAwait(false);
            modelCache[hash] = result;
            return result;
        }

        private static LineSegment globalGoals(LineSegment proposed, PopulationDensityMap density, Nts.Envelope env)
        {
            double normX = (proposed.X2 - env.MinX) / env.Width;
            double normY = (proposed.Y2 - env.MinY) / env.Height;
            float center = density.GetDensity(normX, normY);
            float ahead = density.GetDensity(Math.Clamp(normX + 0.02, 0, 1), Math.Clamp(normY + 0.02, 0, 1));
            double factor = 1.0 + (ahead - center) * 0.5;
            double dx = proposed.X2 - proposed.X1;
            double dy = proposed.Y2 - proposed.Y1;
            double newLen = Math.Sqrt(dx * dx + dy * dy) * factor;
            double angle = Math.Atan2(dy, dx);
            return new LineSegment(proposed.X1, proposed.Y1,
                proposed.X1 + Math.Cos(angle) * newLen,
                proposed.Y1 + Math.Sin(angle) * newLen);
        }

        private static LineSegment? localConstraints(
            LineSegment proposed,
            List<LineSegment> network,
            WaterBodyMap water,
            TerrainData terrain,
            Nts.Envelope env)
        {
            var factory = Nts.GeometryFactory.Default;
            var ls = factory.CreateLineString(new[]
            {
                new Nts.Coordinate(proposed.X1, proposed.Y1),
                new Nts.Coordinate(proposed.X2, proposed.Y2)
            });

            // snap to existing roads
            foreach (var existing in network)
            {
                var ex = factory.CreateLineString(new[]
                {
                    new Nts.Coordinate(existing.X1, existing.Y1),
                    new Nts.Coordinate(existing.X2, existing.Y2)
                });
                if (ls.Distance(ex) < env.Width * 0.01)
                {
                    var inter = ls.Intersection(ex);
                    if (inter is Nts.Point p)
                        return new LineSegment(proposed.X1, proposed.Y1, p.X, p.Y);
                }
            }

            double midX = (proposed.X1 + proposed.X2) / 2.0;
            double midY = (proposed.Y1 + proposed.Y2) / 2.0;
            double nx = (midX - env.MinX) / env.Width;
            double ny = (midY - env.MinY) / env.Height;
            if (water.IsWater(nx, ny))
                return null;

            return proposed;
        }
    }
}
