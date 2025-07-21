using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Operation.Polygonize;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using NetTopologySuite.IO;
using NetTopologySuite.Index.Quadtree;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Simplify;
using System.Windows.Forms;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly ConcurrentDictionary<string, List<(Nts.LineString Line, RoadType Type)>> networkCache = new();
        private static readonly ConcurrentDictionary<string, CityDataModel> modelCache = new();
        private static readonly ConcurrentDictionary<Guid, CityDataModel> modelCacheById = new();

        public static IReadOnlyDictionary<Guid, CityDataModel> ModelCacheById => modelCacheById;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        private static void BuildBuildingStructures(CityDataModel model)
        {
            const double tol = 0.0001; // degrees ~10m
            var index = new STRtree<Building>();
            foreach (var b in model.Buildings)
            {
                var simplified = DouglasPeuckerSimplifier.Simplify(b.Footprint, tol);
                if (simplified == null || simplified.IsEmpty)
                    simplified = b.Footprint;
                b.SimplifiedFootprints[0] = simplified;
                index.Insert(b.Footprint.EnvelopeInternal, b);
            }
            index.Build();
            model.BuildingIndex = index;
        }

        public static CityGenerationData? Data { get; set; }

        private const double Epsilon = 1e-9;

        private static bool IsBad(Coordinate c) =>
            double.IsNaN(c.X) || double.IsNaN(c.Y) ||
            double.IsInfinity(c.X) || double.IsInfinity(c.Y);

        private static SemaphoreSlim GetFileLock(string path)
        {
            return _fileLocks.GetOrAdd(path, p => new SemaphoreSlim(1, 1));
        }

        private static void AddToCache(string hash, CityDataModel model)
        {
            modelCache[hash] = model;
            modelCacheById[model.Id] = model;
        }


        private static async Task SaveModelBinaryAsync(string path, CityDataModel model)
        {
            var fileLock = GetFileLock(path);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var wkbWriter = new WKBWriter();
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                using var bw = new BinaryWriter(fs);

                bw.Write(model.Id.ToByteArray());

            if (model.UrbanArea != null)
            {
                bw.Write(true);
                byte[] ua = wkbWriter.Write(model.UrbanArea);
                bw.Write(ua.Length);
                bw.Write(ua);
            }
            else
            {
                bw.Write(false);
            }

            bw.Write(model.RoadNetwork.Count);
            foreach (var seg in model.RoadNetwork)
            {
                bw.Write(seg.X1);
                bw.Write(seg.Y1);
                bw.Write(seg.X2);
                bw.Write(seg.Y2);
                bw.Write((int)seg.Type);
            }

            bw.Write(model.RawBlocks.Count);
            foreach (var poly in model.RawBlocks)
            {
                byte[] data = wkbWriter.Write(poly);
                bw.Write(data.Length);
                bw.Write(data);
            }

            bw.Write(model.Parcels.Count);
            foreach (var parcel in model.Parcels)
            {
                byte[] data = wkbWriter.Write(parcel.Shape);
                bw.Write(data.Length);
                bw.Write(data);
                bw.Write((int)parcel.LandUse);
                bw.Write(parcel.LandValue);
            }

                bw.Write(model.Buildings.Count);
                foreach (var building in model.Buildings)
                {
                    byte[] data = wkbWriter.Write(building.Footprint);
                    bw.Write(data.Length);
                    bw.Write(data);
                    bw.Write((int)building.LandUse);
                    bw.Write(building.Level);
                    bw.Write(building.PopulationCapacity);
                    bw.Write(building.EconomicOutput);
                    bw.Write(building.PollutionOutput);
                }
            }
            finally
            {
                fileLock.Release();
            }
        }

        private static async Task<CityDataModel> LoadModelBinaryAsync(string path)
        {
            var fileLock = GetFileLock(path);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var wkbReader = new WKBReader();
                byte[] fileBytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                using var ms = new MemoryStream(fileBytes);
                using var br = new BinaryReader(ms);

            var model = new CityDataModel();
            model.Id = new Guid(br.ReadBytes(16));

            if (br.ReadBoolean())
            {
                int len = br.ReadInt32();
                byte[] ua = br.ReadBytes(len);
                model.UrbanArea = (Nts.Polygon)wkbReader.Read(ua);
            }

            int roadCount = br.ReadInt32();
            for (int i = 0; i < roadCount; i++)
            {
                double x1 = br.ReadDouble();
                double y1 = br.ReadDouble();
                double x2 = br.ReadDouble();
                double y2 = br.ReadDouble();
                var type = (RoadType)br.ReadInt32();
                model.RoadNetwork.Add(new LineSegment(x1, y1, x2, y2, type));
            }

            int blockCount = br.ReadInt32();
            for (int i = 0; i < blockCount; i++)
            {
                int len = br.ReadInt32();
                byte[] data = br.ReadBytes(len);
                model.RawBlocks.Add((Nts.Polygon)wkbReader.Read(data));
            }

            int parcelCount = br.ReadInt32();
            for (int i = 0; i < parcelCount; i++)
            {
                int len = br.ReadInt32();
                byte[] data = br.ReadBytes(len);
                var shape = (Nts.Polygon)wkbReader.Read(data);
                var landUse = (LandUseType)br.ReadInt32();
                double value = br.ReadDouble();
                model.Parcels.Add(new Parcel { Shape = shape, LandUse = landUse, LandValue = value });
            }

            int buildingCount = br.ReadInt32();
            for (int i = 0; i < buildingCount; i++)
            {
                int len = br.ReadInt32();
                byte[] data = br.ReadBytes(len);
                var footprint = (Nts.Polygon)wkbReader.Read(data);
                var landUse = (LandUseType)br.ReadInt32();
                int level = br.ReadInt32();
                int popCap = br.ReadInt32();
                double econ = br.ReadDouble();
                double poll = br.ReadDouble();
                model.Buildings.Add(new Building
                {
                    Footprint = footprint,
                    LandUse = landUse,
                    Level = level,
                    PopulationCapacity = popCap,
                    EconomicOutput = econ,
                    PollutionOutput = poll
                });
            }

            BuildBuildingStructures(model);

            return model;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public static async Task<CityDataModel> GenerateModelAsync(Nts.Polygon urbanArea, int cellSize)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();

            if (modelCache.TryGetValue(hash, out var cachedModel))
            {
                modelCacheById[cachedModel.Id] = cachedModel;
                return cachedModel;
            }

            string hashPath = Path.Combine(cacheDir, $"{hash}.txt");
            if (File.Exists(hashPath))
            {
                try
                {
                    string id = await File.ReadAllTextAsync(hashPath).ConfigureAwait(false);
                    string modelPath = Path.Combine(cacheDir, $"{id}.bin");
                    if (File.Exists(modelPath))
                    {
                        var loaded = await LoadModelBinaryAsync(modelPath);
                        AddToCache(hash, loaded);
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to load cached model by hash: {ex.Message}");
                }
            }

            // --- Start of Corrected Logic ---

            // 1. Assign urbanArea immediately upon creation.
            var result = new CityDataModel
            {
                Id = Guid.NewGuid(),
                UrbanArea = urbanArea
            };
            var roadGeometries = GetOrGenerateFor(urbanArea, cellSize);

            result.RoadNetwork = roadGeometries
                .SelectMany(tuple => tuple.Line.Coordinates.Zip(tuple.Line.Coordinates.Skip(1), (s, e) =>
                    new LineSegment(s.X, s.Y, e.X, e.Y, tuple.Type)))
                .ToList();

            // 2. Delegate ALL block and parcel generation to the robust ParcelGenerator.
            //    This single line replaces the previous faulty logic.
            result.Parcels = ParcelGenerator.GenerateParcels(result);
            
            // --- End of Corrected Logic ---
            LandUseSimulator.Run(result);
            result.Buildings = BuildingGenerator.GenerateBuildings(result);
            BuildingRefiner.RefineBuildings(result);
            BuildBuildingStructures(result);

            Debug.WriteLine($">> Generated {result.Parcels.Count} parcels, {result.Buildings.Count} buildings for {hash}");

            try
            {
                string modelPath = Path.Combine(cacheDir, $"{result.Id}.bin");
                await SaveModelBinaryAsync(modelPath, result);
                await File.WriteAllTextAsync(hashPath, result.Id.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to save city model file for hash {hash}.\n\n" +
                                      $"Error: {ex.GetType().Name}\n\n" +
                                      $"Message: {ex.Message}\n\n" +
                                      $"Stack Trace:\n{ex.StackTrace}";
                MessageBox.Show(errorMessage, "Critical Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"[CRITICAL ERROR] Failed to serialize CityDataModel: {errorMessage}");
            }

            AddToCache(hash, result);
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
            // This is a simplified placeholder. A true A* implementation would be more complex.
            var gf = Nts.GeometryFactory.Default;
            var center = urbanArea.Centroid;
            var neighbours = UrbanAreaManager.UrbanPolygons
                .Where(p => !ReferenceEquals(p, urbanArea) && p.IsValid)
                .OrderBy(p => p.Centroid.Distance(center))
                .Take(2); // Connect to nearest 2 for a cleaner network

            foreach (var other in neighbours)
            {
                yield return gf.CreateLineString(new[] { center.Coordinate, other.Centroid.Coordinate });
            }
        }

        private static List<Nts.LineString> GenerateLocalRoads(Nts.Polygon area, List<Nts.LineString> highways)
        {
            double roadSegmentLength = 0.005 * CityGen.AestheticMappingLayer.Parameters.RoadDensity;
            // Make the iteration limit proportional to the area. Ensures small
            // towns generate quickly while large cities have room to expand.
            int maxIterations = (int)Math.Max(500, area.Area * 5000000 * CityGen.AestheticMappingLayer.Parameters.RoadDensity);
            var gf = Nts.GeometryFactory.Default;
            var random = new Random();
            var roadNetwork = new List<Nts.LineString>(highways);
            var index = BuildIndex(roadNetwork);
            var queue = new Queue<(Nts.Coordinate origin, double angle)>();

            // Seed the L-system from points on the highways
            foreach (var highway in highways)
            {
                if (highway.NumPoints < 2)
                    continue;
                for (double i = 0.2; i < 1.0; i += 0.3)
                {
                    int idx = (int)Math.Min(highway.NumPoints - 1, Math.Round(highway.NumPoints * i));
                    var pt = highway.GetCoordinateN(idx);
                    double baseAngle = Math.Atan2(
                        highway.EndPoint.Y - highway.StartPoint.Y,
                        highway.EndPoint.X - highway.StartPoint.X);
                    queue.Enqueue((pt, baseAngle + Math.PI / 2));
                    queue.Enqueue((pt, baseAngle - Math.PI / 2));
                }
            }
            if (queue.Count == 0 && area.EnvelopeInternal.Width > 0)
            {
                // Keep the original centroid seed
                queue.Enqueue((area.Centroid.Coordinate, random.NextDouble() * 2 * Math.PI));

                // Add additional random seeds for large polygons to accelerate generation
                if (area.Area > 0.001)
                {
                    var envelope = area.EnvelopeInternal;
                    for (int i = 0; i < 5; i++)
                    {
                        var randX = envelope.MinX + random.NextDouble() * envelope.Width;
                        var randY = envelope.MinY + random.NextDouble() * envelope.Height;
                        var randomPoint = new Nts.Coordinate(randX, randY);

                        if (area.Contains(gf.CreatePoint(randomPoint)))
                        {
                            queue.Enqueue((randomPoint, random.NextDouble() * 2 * Math.PI));
                        }
                    }
                }
            }


            int iterations = 0;
            while (queue.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                var (origin, angle) = queue.Dequeue();

                var endPoint = new Coordinate(
                    origin.X + Math.Cos(angle) * roadSegmentLength,
                    origin.Y + Math.Sin(angle) * roadSegmentLength
                );
                if (IsBad(endPoint) || origin.Distance(endPoint) < Epsilon)
                    continue;
                // Envelope for quadtree lookup
                var proposedEnv = new Nts.Envelope(origin, endPoint);
                // Global Constraint: Must be within the urban area polygon
                if (!area.Contains(gf.CreatePoint(endPoint))) continue;

                // Local Constraint: Check for intersections
                Nts.Coordinate? closestIntersection = null;
                double minDistance = double.MaxValue;

                var candidates = index.Query(proposedEnv);
                foreach (var existingRoad in candidates)
                {
                    var p1 = existingRoad.GetCoordinateN(0);
                    var p2 = existingRoad.GetCoordinateN(existingRoad.NumPoints - 1);
                    if (GeometryUtil.TryGetIntersection(origin, endPoint, p1, p2, out var intersectionPoint))
                    {
                        double dist = origin.Distance(intersectionPoint);
                        // Ensure intersection is not at the start point and is the closest one
                        if (dist > 1e-6 && dist < minDistance)
                        {
                            minDistance = dist;
                            closestIntersection = intersectionPoint;
                        }
                    }
                }

                if (closestIntersection != null)
                {
                    endPoint = closestIntersection;
                }

                var proposedSegment = gf.CreateLineString(new[] { origin, endPoint });
                roadNetwork.Add(proposedSegment);
                index.Insert(proposedSegment.EnvelopeInternal, proposedSegment);

                // If the road didn't hit anything, it's a candidate for branching
                if (closestIntersection == null)
                {
                    // Global Goal: Higher density areas have more branches
                    double density = Data?.GetPopulationDensity((endPoint.X + 180) / 360.0, (endPoint.Y + 90) / 180.0) ?? 0.5;

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

        private static Quadtree<Nts.LineString> BuildIndex(IEnumerable<Nts.LineString> roads)
        {
            var tree = new Quadtree<Nts.LineString>();
            foreach (var road in roads)
            {
                tree.Insert(road.EnvelopeInternal, road);
            }
            return tree;
        }

        private static List<Nts.Polygon> PolygonizeRoadNetwork(List<LineSegment> roads)
        {
            var gf = Nts.GeometryFactory.Default;
            var validLineStrings = roads
                .Where(s =>
                    !IsBad(new Coordinate(s.X1, s.Y1)) &&
                    !IsBad(new Coordinate(s.X2, s.Y2)) &&
                    Math.Abs(s.X1 - s.X2) + Math.Abs(s.Y1 - s.Y2) > Epsilon)
                .Select(s => gf.CreateLineString(new[]
                {
                    new Coordinate(s.X1, s.Y1),
                    new Coordinate(s.X2, s.Y2)
                }))
                .ToArray();

            if (validLineStrings.Length == 0)
                return new List<Nts.Polygon>();

            var nodedLines = CascadedPolygonUnion.Union(validLineStrings);
            var polygonizer = new Polygonizer();
            polygonizer.Add(nodedLines);
            var rawPolys = polygonizer.GetPolygons();
            return rawPolys.OfType<Nts.Polygon>()
                .Where(p => p.IsValid && p.Area > 1e-9)
                .ToList();
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
                    string modelPath = Path.Combine(cacheDir, $"{id}.bin");
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
        public static async Task<Guid?> GetCityDataModelIdAsync(Nts.Polygon urbanArea)
        {
            string hash = ComputeHash(urbanArea);
            string cacheDir = GetCacheDir();
            
            // Check in-memory cache first
            if (modelCache.TryGetValue(hash, out var cachedModel))
            {
                modelCacheById[cachedModel.Id] = cachedModel;
                return cachedModel.Id;
            }
                
            // Check disk cache
            string hashPath = Path.Combine(cacheDir, $"{hash}.txt");
            if (File.Exists(hashPath))
            {
                try
                {
                    string idStr = await File.ReadAllTextAsync(hashPath).ConfigureAwait(false);
                    if (Guid.TryParse(idStr, out Guid guid))
                    {
                        string modelPath = Path.Combine(cacheDir, $"{guid}.bin");
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
                var binFiles = Directory.GetFiles(cacheDir, "*.bin");

                diskCount = binFiles.Length;

                // Count unique models (hash files that have corresponding binary files)
                foreach (string hashFile in hashFiles)
                {
                    try
                    {
                        string id = File.ReadAllText(hashFile);
                        string modelPath = Path.Combine(cacheDir, $"{id}.bin");
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

        public static async Task<CityDataModel?> LoadCityDataModelAsync(Guid id)
        {
            if (modelCacheById.TryGetValue(id, out var cached))
                return cached;

            string cacheDir = GetCacheDir();
            string modelPath = Path.Combine(cacheDir, $"{id}.bin");
            if (!File.Exists(modelPath)) return null;

            try
            {
                var model = await LoadModelBinaryAsync(modelPath).ConfigureAwait(false);
                if (model.UrbanArea != null)
                {
                    string hash = ComputeHash(model.UrbanArea);
                    AddToCache(hash, model);
                }
                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load CityDataModel {id}: {ex.Message}");
                return null;
            }
        }
    }
}
