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
        private static readonly Dictionary<string, CityDataModel> modelCache = new();

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
            var env = urbanArea.EnvelopeInternal;
            var network = new List<Nts.LineString>();
            var queue = new Queue<(Nts.Coordinate start, double angle, int depth)>();
            var rnd = new Random();
            var factory = Nts.GeometryFactory.Default;

            // Seed initial road growth points
            var center = urbanArea.InteriorPoint;
            queue.Enqueue((center.Coordinate, rnd.NextDouble() * 2 * Math.PI, 0));
            queue.Enqueue((center.Coordinate, rnd.NextDouble() * 2 * Math.PI, 0));

            int roadsGenerated = 0;
            const int roadLimit = 300; // Stop after a certain number of segments

            while (queue.Count > 0 && roadsGenerated < roadLimit)
            {
                var (start, angle, depth) = queue.Dequeue();

                // Propose a new segment
                var proposedSegment = globalGoals(start, angle, env, popDensity, rnd);
                if (!urbanArea.Intersects(proposedSegment)) continue;

                // Check constraints and handle intersections (noding)
                var finalSegments = localConstraints(proposedSegment, network, waterMap, env, factory);

                foreach (var segment in finalSegments)
                {
                    network.Add(segment);
                    roadsGenerated++;

                    // Add new growth points to the queue
                    if (depth < 8 && rnd.NextDouble() < 0.7) // Branching probability
                    {
                        var endPoint = segment.EndPoint.Coordinate;
                        var newAngle = Math.Atan2(endPoint.Y - segment.StartPoint.Y, endPoint.X - segment.StartPoint.X);

                        // Main branch continues forward
                        queue.Enqueue((endPoint, newAngle + (rnd.NextDouble() - 0.5) * 0.2, depth + 1));

                        // Side branch
                        if (rnd.NextDouble() < 0.3) // Chance of a side branch
                        {
                            double branchAngle = Math.PI / 2 * (rnd.Next(0, 2) == 0 ? 1 : -1);
                            queue.Enqueue((endPoint, newAngle + branchAngle + (rnd.NextDouble() - 0.5) * 0.3, depth + 1));
                        }
                    }
                }
            }

            result.RoadNetwork = network.Select(ls => new LineSegment(ls.StartPoint.X, ls.StartPoint.Y, ls.EndPoint.X, ls.EndPoint.Y)).ToList();

            // This part will now work correctly
            ParcelGenerator.GenerateParcels(result);
            LandUseAssigner.AssignLandUse(result);
            BuildingGenerator.GenerateBuildings(result);

            var jsonOut = System.Text.Json.JsonSerializer.Serialize(result);
            await File.WriteAllTextAsync(path, jsonOut).ConfigureAwait(false);
            modelCache[hash] = result;
            return result;
        }

        private static Nts.LineString globalGoals(Nts.Coordinate start, double angle, Nts.Envelope env, PopulationDensityMap density, Random rnd)
        {
            double step = Math.Min(env.Width, env.Height) / 15.0;
            double bestAngle = angle;
            float maxDensity = -1f;

            // Sample angles to find the most promising direction based on population density
            for (int i = 0; i < 5; i++)
            {
                double testAngle = angle + (rnd.NextDouble() - 0.5) * (Math.PI / 2);
                var testEnd = new Nts.Coordinate(start.X + Math.Cos(testAngle) * step, start.Y + Math.Sin(testAngle) * step);
                double normX = (testEnd.X - env.MinX) / env.Width;
                double normY = (testEnd.Y - env.MinY) / env.Height;

                float currentDensity = density.GetDensity(normX, normY);
                if (currentDensity > maxDensity)
                {
                    maxDensity = currentDensity;
                    bestAngle = testAngle;
                }
            }

            double len = step * (0.8 + rnd.NextDouble() * 0.4);
            var end = new Nts.Coordinate(start.X + Math.Cos(bestAngle) * len, start.Y + Math.Sin(bestAngle) * len);
            return new Nts.LineString(new[] { start, end });
        }

        private static List<Nts.LineString> localConstraints(
            Nts.LineString proposed,
            List<Nts.LineString> network,
            WaterBodyMap water,
            Nts.Envelope env,
            Nts.GeometryFactory factory)
        {
            var segmentsToAdd = new List<Nts.LineString> { proposed };
            double snapThreshold = env.Width * 0.02;

            // Water check
            double normX = (proposed.EndPoint.X - env.MinX) / env.Width;
            double normY = (proposed.EndPoint.Y - env.MinY) / env.Height;
            if (water.IsWater(normX, normY))
            {
                return new List<Nts.LineString>(); // Discard if it ends in water
            }

            // Intersection and Noding Check
            for (int i = 0; i < network.Count; i++)
            {
                var existing = network[i];
                if (proposed.Distance(existing) < snapThreshold)
                {
                    var intersectionPoint = proposed.Intersection(existing);
                    if (intersectionPoint is Nts.Point p && p.IsEmpty == false)
                    {
                        // An intersection exists. We must "node" the network.
                        // 1. Remove the existing segment from the network.
                        network.RemoveAt(i);

                        // 2. Add back the two new segments created by splitting the existing road.
                        if (existing.StartPoint.Coordinate.Distance(p.Coordinate) > 1e-6)
                            network.Add(factory.CreateLineString(new[] { existing.StartPoint.Coordinate, p.Coordinate }));
                        if (existing.EndPoint.Coordinate.Distance(p.Coordinate) > 1e-6)
                            network.Add(factory.CreateLineString(new[] { p.Coordinate, existing.EndPoint.Coordinate }));

                        // 3. Shorten the proposed segment so it ends at the new intersection.
                        var shortenedProposed = factory.CreateLineString(new[] { proposed.StartPoint.Coordinate, p.Coordinate });

                        // 4. Return the new, valid segment to be added.
                        return new List<Nts.LineString> { shortenedProposed };
                    }
                }
            }

            return segmentsToAdd;
        }
    }
}
