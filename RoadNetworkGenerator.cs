using Nts = NetTopologySuite.Geometries;
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
using NetTopologySuite.IO;
using NetTopologySuite.Index.Quadtree;

namespace StrategyGame
{
    public static class RoadNetworkGenerator
    {
        private static readonly ConcurrentDictionary<string, List<(Nts.LineString Line, RoadType Type)>> networkCache = new();
        private static readonly ConcurrentDictionary<string, CityDataModel> modelCache = new();

        public static CityGenerationData? Data { get; set; }


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
                    string modelPath = Path.Combine(cacheDir, $"{id}.bin");
                    if (File.Exists(modelPath))
                    {
                        var loaded = ReadCityDataModelBinary(modelPath);
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

            result.RawBlocks = PolygonizeRoadNetwork(result.RoadNetwork);
            result.Parcels = ParcelGenerator.GenerateParcelsFromBlocks(result.RawBlocks);
            LandUseSimulator.Run(result);
            result.Buildings = BuildingGenerator.GenerateBuildings(result);
            BuildingRefiner.RefineBuildings(result);

            // Clean geometries to avoid serialization errors
            result.Parcels = CleanParcels(result.Parcels);
            result.Buildings = CleanBuildings(result.Buildings);

            Debug.WriteLine($">> Generated {result.Parcels.Count} parcels, {result.Buildings.Count} buildings for {hash}");

            try
            {
                string modelPath = Path.Combine(cacheDir, $"{result.Id}.bin");
                using (var fs = File.Create(modelPath))
                {
                    WriteCityDataModelBinary(result, fs);
                }
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
            const double roadSegmentLength = 0.005;
            // Make the iteration limit proportional to the area. Ensures small
            // towns generate quickly while large cities have room to expand.
            int maxIterations = (int)Math.Max(500, area.Area * 5000000);
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

                var endPoint = new Nts.Coordinate(
                    origin.X + Math.Cos(angle) * roadSegmentLength,
                    origin.Y + Math.Sin(angle) * roadSegmentLength
                );
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
            var lineStrings = roads.Select(seg =>
                gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(seg.X1, seg.Y1),
                    new Nts.Coordinate(seg.X2, seg.Y2)
                })).ToArray();

            if (lineStrings.Length == 0)
                return new List<Nts.Polygon>();

            var nodedLines = UnaryUnionOp.Union(lineStrings);
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

        private static List<Parcel> CleanParcels(List<Parcel> parcels)
        {
            var cleanedList = new ConcurrentBag<Parcel>();
            Parallel.ForEach(parcels, p =>
            {
                var cleanShape = p.Shape.Buffer(0);
                if (cleanShape is Nts.Polygon poly && poly.IsValid && !poly.IsEmpty)
                {
                    p.Shape = poly;
                    cleanedList.Add(p);
                }
            });
            return cleanedList.ToList();
        }

        private static List<Building> CleanBuildings(List<Building> buildings)
        {
            var cleanedList = new ConcurrentBag<Building>();
            Parallel.ForEach(buildings, b =>
            {
                var cleanFootprint = b.Footprint.Buffer(0);
                if (cleanFootprint is Nts.Polygon poly && poly.IsValid && !poly.IsEmpty)
                {
                    b.Footprint = poly;
                    cleanedList.Add(b);
                }
            });
            return cleanedList.ToList();
        }

        private static string GetCacheDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "city_models");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void WriteCityDataModelBinary(CityDataModel model, Stream stream)
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            var wkbWriter = new WKBWriter();

            writer.Write(model.Id.ToByteArray());

            writer.Write(model.RoadNetwork.Count);
            foreach (var seg in model.RoadNetwork)
            {
                writer.Write(seg.X1);
                writer.Write(seg.Y1);
                writer.Write(seg.X2);
                writer.Write(seg.Y2);
                writer.Write((int)seg.Type);
            }

            writer.Write(model.RawBlocks.Count);
            foreach (var poly in model.RawBlocks)
            {
                var bytes = wkbWriter.Write(poly);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            writer.Write(model.Parcels.Count);
            foreach (var parcel in model.Parcels)
            {
                var bytes = wkbWriter.Write(parcel.Shape);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                writer.Write((int)parcel.LandUse);
                writer.Write(parcel.LandValue);
            }

            writer.Write(model.Buildings.Count);
            foreach (var b in model.Buildings)
            {
                var bytes = wkbWriter.Write(b.Footprint);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                writer.Write((int)b.LandUse);
                writer.Write(b.Level);
                writer.Write(b.PopulationCapacity);
                writer.Write(b.EconomicOutput);
                writer.Write(b.PollutionOutput);
            }
        }

        private static CityDataModel? ReadCityDataModelBinary(string path)
        {
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs, System.Text.Encoding.UTF8, leaveOpen: false);
            var wkbReader = new WKBReader();

            var model = new CityDataModel();
            model.Id = new Guid(reader.ReadBytes(16));

            int roadCount = reader.ReadInt32();
            var roads = new List<LineSegment>(roadCount);
            for (int i = 0; i < roadCount; i++)
            {
                double x1 = reader.ReadDouble();
                double y1 = reader.ReadDouble();
                double x2 = reader.ReadDouble();
                double y2 = reader.ReadDouble();
                var type = (RoadType)reader.ReadInt32();
                roads.Add(new LineSegment(x1, y1, x2, y2, type));
            }
            model.RoadNetwork = roads;

            int blockCount = reader.ReadInt32();
            var blocks = new List<Nts.Polygon>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                int len = reader.ReadInt32();
                var bytes = reader.ReadBytes(len);
                if (wkbReader.Read(bytes) is Nts.Polygon p)
                    blocks.Add(p);
            }
            model.RawBlocks = blocks;

            int parcelCount = reader.ReadInt32();
            var parcels = new List<Parcel>(parcelCount);
            for (int i = 0; i < parcelCount; i++)
            {
                int len = reader.ReadInt32();
                var bytes = reader.ReadBytes(len);
                var shape = wkbReader.Read(bytes) as Nts.Polygon;
                var use = (LandUseType)reader.ReadInt32();
                double value = reader.ReadDouble();
                if (shape != null)
                    parcels.Add(new Parcel { Shape = shape, LandUse = use, LandValue = value });
            }
            model.Parcels = parcels;

            int buildCount = reader.ReadInt32();
            var buildings = new List<Building>(buildCount);
            for (int i = 0; i < buildCount; i++)
            {
                int len = reader.ReadInt32();
                var bytes = reader.ReadBytes(len);
                var foot = wkbReader.Read(bytes) as Nts.Polygon;
                var use = (LandUseType)reader.ReadInt32();
                int level = reader.ReadInt32();
                int capacity = reader.ReadInt32();
                double output = reader.ReadDouble();
                double pollution = reader.ReadDouble();
                if (foot != null)
                    buildings.Add(new Building
                    {
                        Footprint = foot,
                        LandUse = use,
                        Level = level,
                        PopulationCapacity = capacity,
                        EconomicOutput = output,
                        PollutionOutput = pollution
                    });
            }
            model.Buildings = buildings;

            return model;
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

        public static CityDataModel? LoadCityDataModel(Guid id)
        {
            string cacheDir = GetCacheDir();
            string modelPath = Path.Combine(cacheDir, $"{id}.bin");
            if (!File.Exists(modelPath))
                return null;

            try
            {
                return ReadCityDataModelBinary(modelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load CityDataModel {id}: {ex.Message}");
                return null;
            }
        }
    }
}
