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

            var env = urbanArea.EnvelopeInternal;
            double width = env.Width;
            double height = env.Height;
            double step = cellSize / 1000.0; // constant road spacing
            int divisionsX = Math.Max(1, (int)Math.Round(width / step));
            int divisionsY = Math.Max(1, (int)Math.Round(height / step));
            double stepX = width / divisionsX;
            double stepY = height / divisionsY;

            var prepared = PreparedGeometryFactory.Prepare(urbanArea);
            var clippedNetwork = new List<(Nts.LineString, RoadType)>();

            // Create horizontal and vertical grid lines, then clip
            for (int i = 0; i <= divisionsY; i++)
            {
                // Horizontal line at Y
                double y = env.MinY + stepY * i;
                var horiz = new Nts.LineString(new[]
                {
                    new Nts.Coordinate(env.MinX, y),
                    new Nts.Coordinate(env.MaxX, y)
                });
                if (prepared.Intersects(horiz))
                {
                    var intersected = urbanArea.Intersection(horiz);
                    switch (intersected)
                    {
                        case Nts.LineString ls:
                            clippedNetwork.Add((ls, RoadType.Secondary));
                            break;
                        case Nts.MultiLineString mls:
                            foreach (var l in mls.Geometries.Cast<Nts.LineString>())
                                clippedNetwork.Add((l, RoadType.Secondary));
                            break;
                    }
                }

                // Vertical line at X
                double x = env.MinX + stepX * i;
                var vert = new Nts.LineString(new[]
                {
                    new Nts.Coordinate(x, env.MinY),
                    new Nts.Coordinate(x, env.MaxY)
                });
                if (prepared.Intersects(vert))
                {
                    var intersected = urbanArea.Intersection(vert);
                    switch (intersected)
                    {
                        case Nts.LineString ls:
                            clippedNetwork.Add((ls, RoadType.Secondary));
                            break;
                        case Nts.MultiLineString mls:
                            foreach (var l in mls.Geometries.Cast<Nts.LineString>())
                                clippedNetwork.Add((l, RoadType.Secondary));
                            break;
                    }
                }
            }

            // Add highway connections
            foreach (var hw in GenerateHighways(urbanArea))
                clippedNetwork.Add((hw, RoadType.Primary));

            networkCache[key] = clippedNetwork;
            return clippedNetwork;
        }

        /// <summary>
        /// Generates simple highway connections from the given urban area to its nearest neighbours.
        /// </summary>
        private static IEnumerable<Nts.LineString> GenerateHighways(Nts.Polygon urbanArea)
        {
            var center = urbanArea.Centroid.Coordinate;
            var gf = Nts.GeometryFactory.Default;
            var neighbours = UrbanAreaManager.UrbanPolygons
                .Where(p => !ReferenceEquals(p, urbanArea))
                .OrderBy(p => p.Centroid.Distance(urbanArea.Centroid))
                .Take(3);
            foreach (var other in neighbours)
            {
                yield return gf.CreateLineString(new[]
                {
                    center,
                    other.Centroid.Coordinate
                });
            }
        }

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
