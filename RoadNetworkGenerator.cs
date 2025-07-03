using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Operation.Union;
using ComputeSharp;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly ConcurrentDictionary<string, List<(Nts.LineString Line, RoadType Type)>> networkCache = new();
        private static readonly ConcurrentDictionary<string, CityDataModel> modelCache = new();

        public static PopulationDensityMap? DensityMap { get; set; }
        public static TerrainData? Terrain { get; set; }
        public static WaterBodyMap? Water { get; set; }

        private static float[,]? costGrid;

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            IncludeFields = true,
            WriteIndented = true,
            Converters = { new GeoJsonConverterFactory() }
        };

        private static void EnsureCostGrid()
        {
            if (costGrid != null || Terrain == null || Water == null)
                return;

            int width = Terrain.Width;
            int height = Terrain.Height;
            costGrid = new float[width, height];

            var waterFloat = new float[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    waterFloat[x, y] = Water.Values[x, y] ? 1f : 0f;

            using var elevTex = GraphicsDevice.GetDefault().AllocateReadOnlyTexture2D(Terrain.Elevation);
            using var waterTex = GraphicsDevice.GetDefault().AllocateReadOnlyTexture2D(waterFloat);
            using var costTex = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<float>(width, height);
            GraphicsDevice.GetDefault().For(width, height, new CostGridShader(elevTex, waterTex, costTex));
            costTex.CopyTo(costGrid);
        }

        public static async Task<CityDataModel> GenerateModelAsync(Nts.Polygon urbanArea, int cellSize)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();

            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            string hashPath = Path.Combine(cacheDir, $"{hash}.txt");
            if (File.Exists(hashPath))
            {
                try
                {
                    string id = await File.ReadAllTextAsync(hashPath).ConfigureAwait(false);
                    string modelPath = Path.Combine(cacheDir, $"{id}.json");
                    if (File.Exists(modelPath))
                    {
                        string jsonIn = await File.ReadAllTextAsync(modelPath).ConfigureAwait(false);
                        var loaded = JsonSerializer.Deserialize<CityDataModel>(jsonIn, jsonOptions);
                        if (loaded != null)
                        {
                            modelCache[hash] = loaded;
                            return loaded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to load cached model by hash: {ex.Message}");
                }
            }

            var result = new CityDataModel { Id = Guid.NewGuid() };
            var roadGeometries = GetOrGenerateFor(urbanArea, cellSize);

            result.RoadNetwork = roadGeometries
                .SelectMany(tuple => tuple.Line.Coordinates.Zip(tuple.Line.Coordinates.Skip(1), (s, e) =>
                    new LineSegment(s.X, s.Y, e.X, e.Y, tuple.Type)))
                .ToList();

            result.Parcels = ParcelGenerator.GenerateParcels(result);
            LandUseAssigner.AssignLandUse(result);
            result.Buildings = BuildingGenerator.GenerateBuildings(result);

            Debug.WriteLine($">> Generated {result.Parcels.Count} parcels, {result.Buildings.Count} buildings for {hash}");

            try
            {
                string modelPath = Path.Combine(cacheDir, $"{result.Id}.json");
                string jsonOut = JsonSerializer.Serialize(result, jsonOptions);
                await File.WriteAllTextAsync(modelPath, jsonOut).ConfigureAwait(false);
                await File.WriteAllTextAsync(hashPath, result.Id.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to serialize CityDataModel: {ex.Message}");
            }

            modelCache[hash] = result;
            return result;
        }

        public static List<(Nts.LineString Line, RoadType Type)> GetOrGenerateFor(Nts.Polygon urbanArea, int cellSize)
        {
            string key = ComputeHash(urbanArea);
            if (networkCache.TryGetValue(key, out var cachedNet))
                return cachedNet;

            var highways = GenerateHighways(urbanArea).ToList();
            var localRoads = GenerateLocalRoads(urbanArea, highways);

            var allRoads = highways.Select(h => (h, RoadType.Primary))
                                 .Concat(localRoads.Select(l => (l, RoadType.Secondary)))
                                 .ToList();

            networkCache[key] = allRoads;
            return allRoads;
        }

        private static IEnumerable<Nts.LineString> GenerateHighways(Nts.Polygon urbanArea)
        {
            EnsureCostGrid();
            if (costGrid == null || Terrain == null)
                yield break;

            var gf = Nts.GeometryFactory.Default;
            var center = urbanArea.Centroid;
            var neighbours = UrbanAreaManager.UrbanPolygons
                .Where(p => !ReferenceEquals(p, urbanArea) && p.IsValid)
                .OrderBy(p => p.Centroid.Distance(center))
                .Take(2);

            foreach (var other in neighbours)
            {
                var path = AStarPath(center.Coordinate, other.Centroid.Coordinate);
                if (path.Count >= 2)
                    yield return gf.CreateLineString(path.ToArray());
            }
        }

        private static List<Nts.Coordinate> AStarPath(Nts.Coordinate start, Nts.Coordinate goal)
        {
            int width = costGrid!.GetLength(0);
            int height = costGrid.GetLength(1);

            int StartX() => (int)Math.Clamp((start.X + 180) / 360.0 * (width - 1), 0, width - 1);
            int StartY() => (int)Math.Clamp((start.Y + 90) / 180.0 * (height - 1), 0, height - 1);
            int GoalX() => (int)Math.Clamp((goal.X + 180) / 360.0 * (width - 1), 0, width - 1);
            int GoalY() => (int)Math.Clamp((goal.Y + 90) / 180.0 * (height - 1), 0, height - 1);

            var sx = StartX();
            var sy = StartY();
            var gx = GoalX();
            var gy = GoalY();

            var open = new PriorityQueue<(int x, int y), float>();
            var came = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), float>();

            open.Enqueue((sx, sy), 0);
            gScore[(sx, sy)] = 0;

            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current.x == gx && current.y == gy)
                    break;

                float currentG = gScore[current];

                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;
                    float tentative = currentG + costGrid[nx, ny];
                    var key = (nx, ny);
                    if (!gScore.TryGetValue(key, out float g) || tentative < g)
                    {
                        came[key] = current;
                        gScore[key] = tentative;
                        float h = Math.Abs(nx - gx) + Math.Abs(ny - gy);
                        open.Enqueue(key, tentative + h);
                    }
                }
            }

            var path = new List<Nts.Coordinate>();
            var node = (gx, gy);
            path.Add(goal);
            while (node != (sx, sy) && came.TryGetValue(node, out var prev))
            {
                double lon = (double)node.Item1 / (width - 1) * 360.0 - 180.0;
                double lat = (double)node.Item2 / (height - 1) * 180.0 - 90.0;
                path.Add(new Nts.Coordinate(lon, lat));
                node = prev;
            }
            path.Add(start);
            path.Reverse();
            return path;
        }

        private static List<Nts.LineString> GenerateLocalRoads(Nts.Polygon area, List<Nts.LineString> highways)
        {
            const double roadSegmentLength = 0.005;
            const int maxIterations = 500;
            var gf = Nts.GeometryFactory.Default;
            var random = new Random();
            var roadNetwork = new List<Nts.LineString>(highways);
            var index = new NetTopologySuite.Index.Strtree.STRtree<Nts.LineString>();
            foreach (var h in highways)
                index.Insert(h.EnvelopeInternal, h);
            var queue = new Queue<(Nts.Coordinate origin, double angle)>();

            // Seed the L-system from points on the highways
            foreach (var highway in highways)
            {
                for (double i = 0.2; i < 1.0; i += 0.3)
                {
                    var pt = highway.GetCoordinateN((int)(highway.NumPoints * i));
                    queue.Enqueue((pt, Math.Atan2(highway.EndPoint.Y - highway.StartPoint.Y, highway.EndPoint.X - highway.StartPoint.X) + Math.PI / 2));
                    queue.Enqueue((pt, Math.Atan2(highway.EndPoint.Y - highway.StartPoint.Y, highway.EndPoint.X - highway.StartPoint.X) - Math.PI / 2));
                }
            }
            if (queue.Count == 0 && area.EnvelopeInternal.Width > 0)
            {
                queue.Enqueue((area.Centroid.Coordinate, random.NextDouble() * 2 * Math.PI));
            }


            int iterations = 0;
            while (queue.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                var (origin, angle) = queue.Dequeue();

                var endPoint = new Nts.Coordinate(
                    origin.X + Math.Cos(angle) * roadSegmentLength,
                    origin.Y + Math.Sin(angle) * roadSegmentLength
                );
                var proposedSegment = gf.CreateLineString(new[] { origin, endPoint });

                // Global Constraint: Must be within the urban area polygon
                if (!area.Contains(proposedSegment.EndPoint)) continue;

                // Local Constraint: Check for intersections
                Nts.Coordinate? closestIntersection = null;
                double minDistance = double.MaxValue;

                var candidates = index.Query(proposedSegment.EnvelopeInternal);
                foreach (var existingRoad in candidates)
                {
                    if (proposedSegment.Intersects(existingRoad))
                    {
                        var intersection = proposedSegment.Intersection(existingRoad);
                        var intersectionPoint = intersection.Coordinate;
                        if (intersectionPoint != null)
                        {
                            double dist = origin.Distance(intersectionPoint);
                            if (dist > 1e-6 && dist < minDistance)
                            {
                                minDistance = dist;
                                closestIntersection = intersectionPoint;
                            }
                        }
                    }
                }

                if (closestIntersection != null)
                {
                    endPoint = closestIntersection;
                    proposedSegment = gf.CreateLineString(new[] { origin, endPoint });
                }

                roadNetwork.Add(proposedSegment);
                index.Insert(proposedSegment.EnvelopeInternal, proposedSegment);

                // If the road didn't hit anything, it's a candidate for branching
                if (closestIntersection == null)
                {
                    // Global Goal: Higher density areas have more branches
                    double density = DensityMap?.GetDensity((endPoint.X + 180) / 360.0, (endPoint.Y + 90) / 180.0) ?? 0.5;

                    // Continue straight
                    queue.Enqueue((endPoint, angle));

                    // Branch left/right
                    if (random.NextDouble() < (0.2 + density * 0.5)) // Branching probability
                    {
                        queue.Enqueue((endPoint, angle + Math.PI / 2));
                    }
                    if (random.NextDouble() < (0.2 + density * 0.5))
                    {
                        queue.Enqueue((endPoint, angle - Math.PI / 2));
                    }
                }
            }
            return roadNetwork.Except(highways).ToList();
        }

        private static string ComputeHash(Nts.Polygon area)
        {
            var e = area.EnvelopeInternal;
            return $"{e.MinX:F2}_{e.MinY:F2}_{e.MaxX:F2}_{e.MaxY:F2}";
        }

        private static string GetCacheDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private sealed class CoordComparer : IEqualityComparer<Nts.Coordinate>
        {
            public bool Equals(Nts.Coordinate? x, Nts.Coordinate? y)
            {
                if (x == null || y == null) return false;
                return Math.Abs(x.X - y.X) < 1e-6 && Math.Abs(x.Y - y.Y) < 1e-6;
            }

            public int GetHashCode(Nts.Coordinate obj)
            {
                return HashCode.Combine(Math.Round(obj.X, 6), Math.Round(obj.Y, 6));
            }
        }

        private static double Distance(Nts.Coordinate a, Nts.Coordinate b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

      
        /// <summary>
        /// Checks if a city data model exists for the given urban area
        /// </summary>
        public static bool HasCityDataModel(Nts.Polygon urbanArea)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();
            
            // Check in-memory cache first
            if (modelCache.ContainsKey(hash))
                return true;
                
            // Check disk cache
            string hashPath = Path.Combine(cacheDir, $"{hash}.txt");
            if (File.Exists(hashPath))
            {
                try
                {
                    string id = File.ReadAllText(hashPath);
                    string modelPath = Path.Combine(cacheDir, $"{id}.json");
                    return File.Exists(modelPath);
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Gets the ID of the city data model for a given urban area, if it exists
        /// </summary>
        public static Guid? GetCityDataModelId(Nts.Polygon urbanArea)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();
            
            // Check in-memory cache first
            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel.Id;
                
            // Check disk cache
            string hashPath = Path.Combine(cacheDir, $"{hash}.txt");
            if (File.Exists(hashPath))
            {
                try
                {
                    string id = File.ReadAllText(hashPath);
                    if (Guid.TryParse(id, out Guid guid))
                    {
                        string modelPath = Path.Combine(cacheDir, $"{guid}.json");
                        if (File.Exists(modelPath))
                            return guid;
                    }
                }
                catch
                {
                    // Fall through
                }
            }
            
            return null;
        }

        /// <summary>
        /// Gets statistics about cached city data models
        /// </summary>
        public static (int InMemoryCount, int DiskCount, int TotalUnique) GetCacheStatistics()
        {
            int inMemoryCount = modelCache.Count;
            
            string cacheDir = GetCacheDir();
            int diskCount = 0;
            int totalUnique = 0;
            
            if (Directory.Exists(cacheDir))
            {
                var hashFiles = Directory.GetFiles(cacheDir, "*.txt");
                var jsonFiles = Directory.GetFiles(cacheDir, "*.json");
                
                diskCount = jsonFiles.Length;
                
                // Count unique models (hash files that have corresponding JSON files)
                foreach (string hashFile in hashFiles)
                {
                    try
                    {
                        string id = File.ReadAllText(hashFile);
                        string modelPath = Path.Combine(cacheDir, $"{id}.json");
                        if (File.Exists(modelPath))
                            totalUnique++;
                    }
                    catch
                    {
                        // Skip invalid files
                    }
                }
            }
            
            return (inMemoryCount, diskCount, totalUnique);
        }
    }
}
