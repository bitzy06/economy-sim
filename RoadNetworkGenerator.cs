using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetTopologySuite.IO; // Replace this with the correct namespace
using NetTopologySuite.IO.Converters; // Add this namespace

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        // Cache for generated networks and models
        private static readonly ConcurrentDictionary<string, List<(Nts.LineString Line, RoadType Type)>> networkCache = new();
        private static readonly ConcurrentDictionary<string, CityDataModel> modelCache = new();

        public static PopulationDensityMap? DensityMap { get; set; }
        public static TerrainData? Terrain { get; set; }
        public static WaterBodyMap? Water { get; set; }

        // Options for JSON serialization, including geo-JSON support
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            IncludeFields = true,
            WriteIndented = true
        };

        // Static constructor to register converters
        static RoadNetworkGenerator()
        {
            jsonOptions.Converters.Add(new GeoJsonConverterFactory());
        }

        /// <summary>
        /// Generates or retrieves a cached city data model (roads, parcels, buildings) for the given urban area.
        /// </summary>
        public static async Task<CityDataModel> GenerateModelAsync(Nts.Polygon urbanArea, int cellSize)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();

            // Return in-memory cache if available
            if (modelCache.TryGetValue(hash, out var cachedModel))
                return cachedModel;

            // Try loading from disk if a mapping exists
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
                        modelCache[hash] = loaded; // Cache in-memory
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to load cached model by hash: {ex.Message}");
                    // Fall through to regenerate
                }
            }

            // Create fresh model
            var result = new CityDataModel { Id = Guid.NewGuid() };
            result.RoadNetwork = GetOrGenerateFor(urbanArea, cellSize)
                .SelectMany(tuple => tuple.Line.Coordinates.Zip(tuple.Line.Coordinates.Skip(1), (s, e) =>
                    new LineSegment(s.X, s.Y, e.X, e.Y, tuple.Type)))
                .ToList();
            result.Parcels = ParcelGenerator.GenerateParcels(result);

            // Assign land use and generate buildings in parallel
            var landUseTask = Task.Run(() => LandUseAssigner.AssignLandUse(result));
            var buildingTask = Task.Run(() =>
            {
                try
                {
                    return BuildingGenerator.GenerateBuildings(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Building generation failed: {ex.Message}");
                    return new List<Building>();
                }
            });

            await Task.WhenAll(landUseTask, buildingTask).ConfigureAwait(false);
            result.Buildings = buildingTask.Result;

            Debug.WriteLine($">> Generated {result.Parcels.Count} parcels, {result.Buildings.Count} buildings for {hash}");

            // Serialize and write to disk (safe against errors)
            try
            {
                string modelPath = Path.Combine(cacheDir, $"{result.Id}.json");
                string jsonOut = JsonSerializer.Serialize(result, jsonOptions);
                await File.WriteAllTextAsync(modelPath, jsonOut).ConfigureAwait(false);

                // Save the hash-to-ID mapping
                await File.WriteAllTextAsync(hashPath, result.Id.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to serialize CityDataModel: {ex.Message}");
            }

            // Cache in-memory and return
            modelCache[hash] = result;
            return result;
        }

        /// <summary>
        /// Generates or retrieves a cached list of line segments representing roads within the urban polygon.
        /// </summary>
        public static List<(Nts.LineString Line, RoadType Type)> GetOrGenerateFor(Nts.Polygon urbanArea, int cellSize)
        {
            string key = $"{ComputeHash(urbanArea)}_{cellSize}";
            if (networkCache.TryGetValue(key, out var cachedNet))
                return cachedNet;

            var clippedNetwork = new List<(Nts.LineString, RoadType)>();

            // Generate highways to neighbours using A*
            var highways = GenerateHighways(urbanArea).ToList();
            clippedNetwork.AddRange(highways.Select(h => (h, RoadType.Primary)));

            // Generate secondary roads via simple L-system
            var locals = GenerateLocalRoads(urbanArea, highways, cellSize);
            clippedNetwork.AddRange(locals.Select(l => (l, RoadType.Secondary)));

            networkCache[key] = clippedNetwork;
            return clippedNetwork;
        }

        /// <summary>
        /// Generates simple highway connections from the given urban area to its nearest neighbours.
        /// </summary>
        private static IEnumerable<Nts.LineString> GenerateHighways(Nts.Polygon urbanArea)
        {
            var gf = Nts.GeometryFactory.Default;
            var neighbours = UrbanAreaManager.UrbanPolygons
                .Where(p => !ReferenceEquals(p, urbanArea))
                .OrderBy(p => p.Centroid.Distance(urbanArea.Centroid))
                .Take(3)
                .ToList();

            foreach (var other in neighbours)
            {
                var raw = AStarPath(urbanArea.Centroid.Coordinate, other.Centroid.Coordinate);
                var smooth = SmoothPath(raw);
                yield return gf.CreateLineString(smooth.ToArray());
            }
        }

        private static List<Nts.Coordinate> AStarPath(Nts.Coordinate start, Nts.Coordinate goal)
        {
            double step = 0.01;
            var open = new PriorityQueue<(Nts.Coordinate C, double F) , double>();
            var cameFrom = new Dictionary<Nts.Coordinate, Nts.Coordinate>();
            var gScore = new Dictionary<Nts.Coordinate, double>(new CoordComparer()) { [start] = 0 };

            open.Enqueue((start, Distance(start, goal)), Distance(start, goal));

            var neighbourDirs = new[]
            {
                (1,0),(0,1),(-1,0),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)
            };

            var minX = Math.Min(start.X, goal.X) - 1;
            var minY = Math.Min(start.Y, goal.Y) - 1;
            var maxX = Math.Max(start.X, goal.X) + 1;
            var maxY = Math.Max(start.Y, goal.Y) + 1;

            while (open.Count > 0)
            {
                var current = open.Dequeue().C;
                if (Distance(current, goal) < step)
                {
                    var path = new List<Nts.Coordinate> { current };
                    while (cameFrom.TryGetValue(current, out var prev))
                    {
                        path.Add(prev);
                        current = prev;
                    }
                    path.Reverse();
                    return path;
                }

                foreach (var (dx, dy) in neighbourDirs)
                {
                    var next = new Nts.Coordinate(current.X + dx * step, current.Y + dy * step);
                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY)
                        continue;

                    double cost = step;
                    if (Terrain != null)
                    {
                        double nx = (next.X + 180) / 360.0;
                        double ny = (next.Y + 90) / 180.0;
                        double elevNow = Terrain.GetElevation((current.X + 180) / 360.0, (current.Y + 90) / 180.0);
                        double elevNext = Terrain.GetElevation(nx, ny);
                        cost += Math.Abs(elevNext - elevNow) * 10;
                        if (Water != null && Water.IsWater(nx, ny))
                            cost += 1000;
                    }

                    double tentative = gScore[current] + cost;
                    if (!gScore.TryGetValue(next, out var existing) || tentative < existing)
                    {
                        cameFrom[next] = current;
                        gScore[next] = tentative;
                        double f = tentative + Distance(next, goal);
                        open.Enqueue((next, f), f);
                    }
                }
            }

            return new List<Nts.Coordinate> { start, goal };
        }

        private static List<Nts.Coordinate> SmoothPath(List<Nts.Coordinate> path)
        {
            for (int iter = 0; iter < 3; iter++)
            {
                var smoothed = new List<Nts.Coordinate> { path[0] };
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var p = path[i];
                    var q = path[i + 1];
                    smoothed.Add(new Nts.Coordinate(0.75 * p.X + 0.25 * q.X, 0.75 * p.Y + 0.25 * q.Y));
                    smoothed.Add(new Nts.Coordinate(0.25 * p.X + 0.75 * q.X, 0.25 * p.Y + 0.75 * q.Y));
                }
                smoothed.Add(path[^1]);
                path = smoothed;
            }
            return path;
        }

        private static IEnumerable<Nts.LineString> GenerateLocalRoads(Nts.Polygon area, IEnumerable<Nts.LineString> highways, int cellSize)
        {
            var gf = Nts.GeometryFactory.Default;
            double segLen = cellSize / 1000.0;
            var network = highways.ToList();

            var seeds = new Queue<(Nts.Coordinate pt, double angle, int depth)>();
            foreach (var hw in highways)
            {
                seeds.Enqueue((hw.StartPoint.Coordinate, 0, 0));
                seeds.Enqueue((hw.EndPoint.Coordinate, Math.PI, 0));
            }

            while (seeds.Count > 0 && network.Count < 500)
            {
                var (pt, angle, depth) = seeds.Dequeue();
                if (depth > 8) continue;
                var next = new Nts.Coordinate(pt.X + Math.Cos(angle) * segLen, pt.Y + Math.Sin(angle) * segLen);
                var line = gf.CreateLineString(new[] { pt, next });
                if (!area.Contains(line))
                    continue;
                if (network.Any(l => l.Intersects(line)))
                    continue;

                network.Add(line);

                double nx = (next.X + 180) / 360.0;
                double ny = (next.Y + 90) / 180.0;
                double density = DensityMap?.GetDensity(nx, ny) ?? 0.5;

                if (Random.Shared.NextDouble() < 0.3 * density)
                    seeds.Enqueue((next, angle + (Random.Shared.NextDouble() - 0.5) * Math.PI / 2, depth + 1));

                seeds.Enqueue((next, angle, depth + 1));
            }

            return network;
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
        /// Computes a simple hash string for caching based on the polygon.
        /// </summary>
        private static string ComputeHash(Nts.Polygon area)
        {
            // Simple envelope-based hash; replace with robust hash if needed
            var e = area.EnvelopeInternal;
            return $"{e.MinX:F2}_{e.MinY:F2}_{e.MaxX:F2}_{e.MaxY:F2}";
        }

        /// <summary>
        /// Ensures the cache directory exists.
        /// </summary>
        private static string GetCacheDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            return dir;
        }

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
