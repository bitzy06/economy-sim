using Nts = NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly Dictionary<Nts.Polygon, List<LineSegment>> networkCache = new();
        private static readonly Dictionary<string, CityDataModel> modelCache = new();

        public static List<LineSegment> GetOrGenerateFor(Nts.Polygon urbanArea, int cellSize)
        {
            if (cellSize < 40)
                return new List<LineSegment>();

            if (networkCache.TryGetValue(urbanArea, out var cached))
                return cached;

            var env = urbanArea.EnvelopeInternal;
            double diagonal = Math.Sqrt(env.Width * env.Width + env.Height * env.Height);
            int divisions = Math.Clamp((int)(diagonal * 40.0), 5, 100);

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
            string hash = Math.Abs(urbanArea.GetHashCode()).ToString();
            string path = Path.Combine(dir, $"{hash}.json");

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                var model = System.Text.Json.JsonSerializer.Deserialize<CityDataModel>(json);
                if (model != null)
                {
                    modelCache[hash] = model;
                    return model;
                }
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
            string hash = Math.Abs(urbanArea.GetHashCode()).ToString();
            string path = Path.Combine(dir, $"{hash}.json");

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            var result = new CityDataModel { Id = Guid.NewGuid() };

            // --- START: Copied and adapted from GetOrGenerateFor ---
            var env = urbanArea.EnvelopeInternal;
            double diagonal = Math.Sqrt(env.Width * env.Width + env.Height * env.Height);
            int divisions = Math.Clamp((int)(diagonal * 40.0), 10, 150); // Increased density

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

            // This part will now work correctly
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
