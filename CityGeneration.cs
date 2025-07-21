using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Geometries.Utilities;

namespace StrategyGame
{
    // Existing CityGenerationData class
    public class CityGenerationData
    {
        public float[,] PopulationDensity { get; }
        public bool[,] WaterBodies { get; }
        public float[,] Elevation { get; }

        public CityGenerationData(float[,] populationDensity, bool[,] waterBodies, float[,] elevation)
        {
            PopulationDensity = populationDensity ?? throw new ArgumentNullException(nameof(populationDensity));
            WaterBodies = waterBodies ?? throw new ArgumentNullException(nameof(waterBodies));
            Elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
        }

        private static int ClampIndex(double value, int size) => (int)Math.Clamp(value * (size - 1), 0, size - 1);

        public float GetPopulationDensity(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, PopulationDensity.GetLength(0));
            int iy = ClampIndex(yNorm, PopulationDensity.GetLength(1));
            return PopulationDensity[ix, iy];
        }

        public bool IsWater(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, WaterBodies.GetLength(0));
            int iy = ClampIndex(yNorm, WaterBodies.GetLength(1));
            return WaterBodies[ix, iy];
        }

        public float GetElevation(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, Elevation.GetLength(0));
            int iy = ClampIndex(yNorm, Elevation.GetLength(1));
            return Elevation[ix, iy];
        }
    }

    // Original ParcelGenerator
    public static class ParcelGenerator
    {
        private const double Epsilon = 1e-9;

        private static bool IsBad(Coordinate c) =>
            double.IsNaN(c.X) || double.IsNaN(c.Y) ||
            double.IsInfinity(c.X) || double.IsInfinity(c.Y);

        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcels = new List<Parcel>();
            // Ensure there's a road network and an urban area to work with.
            if (model.RoadNetwork == null || !model.RoadNetwork.Any() || model.UrbanArea == null)
                return parcels;

            var gf = Nts.GeometryFactory.Default;

            // 1. Get all valid road line segments as Geometry.
            var roadLines = model.RoadNetwork
                .Where(seg =>
                    !IsBad(new Coordinate(seg.X1, seg.Y1)) &&
                    !IsBad(new Coordinate(seg.X2, seg.Y2)) &&
                    Math.Abs(seg.X1 - seg.X2) + Math.Abs(seg.Y1 - seg.Y2) > Epsilon)
                .Select(seg => gf.CreateLineString(new[]
                {
                    new Coordinate(seg.X1, seg.Y1),
                    new Coordinate(seg.X2, seg.Y2)
                }))
                .ToList<Nts.Geometry>();

            // 2. Add the urban area boundary to the same collection.
            if (model.UrbanArea.IsValid)
            {
                roadLines.Add(model.UrbanArea.Boundary);
            }

            // 3. Union ALL lines together at once. This correctly nodes the entire geometry set.
            var nodedLines = UnaryUnionOp.Union(roadLines);

            // 4. Polygonize the fully noded line network.
            var polygonizer = new Polygonizer();
            polygonizer.Add(nodedLines);
            var rawPolys = polygonizer.GetPolygons();
            Debug.WriteLine($"[ParcelGenerator] Polygonizer produced {rawPolys.Count} raw polygons");

            var blocks = rawPolys.OfType<Nts.Polygon>()
                .Where(p => p.IsValid && p.Area > 1e-9)
                .ToList();

            // The rest of the process can now proceed.
            model.RawBlocks = blocks;
            parcels = GenerateParcelsFromBlocks(blocks);

            Debug.WriteLine($"[ParcelGenerator] Final parcel count: {parcels.Count}");
            return parcels;
        }

        public static List<Parcel> GenerateParcelsFromBlocks(List<Nts.Polygon> blocks)
        {
            var parcels = new List<Parcel>();
            foreach (var block in blocks)
            {
                if (block.IsValid && !block.IsEmpty && block.Area > 1e-9)
                    SubdividePolygon(block, parcels);
            }
            return parcels;
        }

        private static void SubdividePolygon(Nts.Polygon poly, List<Parcel> output)
        {
            const double MinParcelArea = 0.00005;
            RecursiveSplit(poly, output, MinParcelArea);
        }

        private static void RecursiveSplit(Nts.Polygon poly, List<Parcel> output, double minArea)
        {
            // --- Start Diagnostic Logging ---
            Debug.WriteLine($"Splitting polygon. Area: {poly.Area}, IsValid: {poly.IsValid}, Envelope: {poly.EnvelopeInternal}");
            // --- End Diagnostic Logging ---

            if (poly.Area < minArea * 1.5)
            {
                if (poly.IsValid && !poly.IsEmpty)
                    output.Add(new Parcel { Shape = poly });
                return;
            }

            var envelope = poly.EnvelopeInternal;
            var gf = poly.Factory;
            bool splitVertical = envelope.Width > envelope.Height;

            Nts.Geometry splitLine;
            if (splitVertical)
            {
                double midX = envelope.MinX + envelope.Width / 2;
                splitLine = gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(midX, envelope.MinY),
                    new Nts.Coordinate(midX, envelope.MaxY)
                });
            }
            else
            {
                double midY = envelope.MinY + envelope.Height / 2;
                splitLine = gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(envelope.MinX, midY),
                    new Nts.Coordinate(envelope.MaxX, midY)
                });
            }

            try
            {
                var splitGeometries = poly.Difference(splitLine);

                // If the split did not create two or more pieces, stop recursion to avoid an infinite loop.
                if (splitGeometries.NumGeometries < 2)
                {
                    if (poly.IsValid && !poly.IsEmpty)
                        output.Add(new Parcel { Shape = poly });
                    return;
                }

                for (int i = 0; i < splitGeometries.NumGeometries; i++)
                {
                    if (splitGeometries.GetGeometryN(i) is Nts.Polygon splitPoly && splitPoly.IsValid && !splitPoly.IsEmpty)
                    {
                        RecursiveSplit(splitPoly, output, minArea);
                    }
                }
            }
            catch (Exception ex)
            {
                // This will tell us if the split itself throws a C# error
                Debug.WriteLine($"[RECURSIVE SPLIT ERROR] {ex.Message}");
                if (poly.IsValid && !poly.IsEmpty)
                    output.Add(new Parcel { Shape = poly });
            }
        }
    }

    // Basic developer agent classes used by LandUseSimulator
    public abstract class DeveloperAgent
    {
        protected DeveloperAgent(LandUseType type) => LandUse = type;
        public LandUseType LandUse { get; }
        public abstract double Evaluate(Parcel parcel);
    }

    public class ResidentialDeveloper : DeveloperAgent
    {
        public ResidentialDeveloper() : base(LandUseType.Residential) { }
        public override double Evaluate(Parcel parcel)
            => Math.Max(0, 80 - Math.Abs(parcel.LandValue - 60));
    }

    public class CommercialDeveloper : DeveloperAgent
    {
        public CommercialDeveloper() : base(LandUseType.Commercial) { }
        public override double Evaluate(Parcel parcel) => parcel.LandValue;
    }

    public class IndustrialDeveloper : DeveloperAgent
    {
        public IndustrialDeveloper() : base(LandUseType.Industrial) { }
        public override double Evaluate(Parcel parcel) => Math.Max(0, 100 - parcel.LandValue);
    }

    // Land use simulation with simple land value map
    public static class LandUseSimulator
    {
        private static readonly ThreadLocal<Random> Rng = new(() => new Random());
        private static readonly DeveloperAgent[] Agents =
        {
            new ResidentialDeveloper(),
            new CommercialDeveloper(),
            new IndustrialDeveloper()
        };

        public static void Run(CityDataModel model)
        {
            if (model.Parcels == null || model.RoadNetwork == null)
                return;

            var env = new Nts.Envelope();
            foreach (var seg in model.RoadNetwork)
            {
                env.ExpandToInclude(seg.X1, seg.Y1);
                env.ExpandToInclude(seg.X2, seg.Y2);
            }
            if (env.IsNull) return;

            var center = env.Centre;
            var centerPt = new Nts.Point(center);
            double maxDist = center.Distance(new Nts.Coordinate(env.MinX, env.MinY));
            if (maxDist < 1e-6) maxDist = 1.0;

            foreach (var parcel in model.Parcels)
            {
                var pCenter = parcel.Shape.Centroid;
                double dist = pCenter.Distance(centerPt);
                double landValue = Math.Max(0, 100 * (1 - dist / maxDist));
                landValue += Rng.Value.NextDouble() * 20 - 10; // noise
                parcel.LandValue = Math.Clamp(landValue, 0, 100);

                var weights = new Dictionary<LandUseType, double>();
                foreach (var agent in Agents)
                    weights[agent.LandUse] = agent.Evaluate(parcel) + 0.1;

                parcel.LandUse = SelectLandUse(weights);
            }
        }

        private static LandUseType SelectLandUse(Dictionary<LandUseType, double> weights)
        {
            double total = weights.Values.Sum();
            double r = Rng.Value.NextDouble() * total;
            foreach (var kvp in weights)
            {
                if (r < kvp.Value)
                    return kvp.Key;
                r -= kvp.Value;
            }
            return LandUseType.Park;
        }
    }

    // Building generation and refinement
    public static class BuildingGenerator
    {
        public static List<Building> GenerateBuildings(CityDataModel model)
        {
            // We need the ConcurrentBag again for thread-safe collection
            var buildingBag = new ConcurrentBag<Building>();
            var gf = Nts.GeometryFactory.Default;

            const double CommercialInset = -0.00002;
            const double ResidentialInset = -0.00004;
            const double IndustrialInset = -0.00003;

            // Create a partitioner to process parcels in efficient, thread-safe chunks.
            var partitioner = Partitioner.Create(model.Parcels, true);

            Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, parcel =>
            {
                try
                {
                    Nts.Geometry foot = null;
                    switch (parcel.LandUse)
                    {
                        case LandUseType.Commercial:
                            foot = parcel.Shape.Buffer(CommercialInset);
                            break;
                        case LandUseType.Residential:
                            foot = parcel.Shape.Buffer(ResidentialInset);
                            break;
                        case LandUseType.Industrial:
                            var temp = parcel.Shape.Buffer(IndustrialInset);
                            if (!temp.IsEmpty) foot = gf.ToGeometry(temp.EnvelopeInternal);
                            else foot = temp;
                            break;
                        default:
                            // No need for continue, just don't process
                            return;
                    }

                    if (foot is Nts.Polygon p && p.IsValid && !p.IsEmpty)
                    {
                        var cleanedFootprint = p.Buffer(0);

                        if (cleanedFootprint is Nts.Polygon cleanedP && cleanedP.IsValid && !cleanedP.IsEmpty)
                        {
                            buildingBag.Add(new Building { Footprint = cleanedP, LandUse = parcel.LandUse });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // It's still good practice to keep this catch block for diagnostics
                    Debug.WriteLine($"[BUILDING GEN ERROR] on a parcel. Error: {ex.Message}");
                }
            });

            var buildings = buildingBag.ToList();
            model.Buildings = buildings;
            return buildings;
        }
    }

    public static class BuildingRefiner
    {
        public static void RefineBuildings(CityDataModel model)
        {
            if (model.Buildings == null || model.Parcels == null)
                return;

            foreach (var building in model.Buildings)
            {
                var parcel = model.Parcels.FirstOrDefault(p => p.Shape.Contains(building.Footprint.Centroid));
                double value = parcel?.LandValue ?? 50;
                ApplyRules(building, value);
            }
        }

        private static void ApplyRules(Building b, double landValue)
        {
            switch (b.LandUse)
            {
                case LandUseType.Residential:
                    b.Level = landValue > 75 ? 5 : landValue > 50 ? 3 : 1;
                    b.PopulationCapacity = b.Level * 20;
                    b.EconomicOutput = b.Level * 2;
                    break;
                case LandUseType.Commercial:
                    b.Level = landValue > 70 ? 4 : 2;
                    b.EconomicOutput = b.Level * 5;
                    break;
                case LandUseType.Industrial:
                    b.Level = landValue > 50 ? 3 : 1;
                    b.EconomicOutput = b.Level * 8;
                    b.PollutionOutput = b.Level * 4;
                    break;
                default:
                    b.Level = 0;
                    break;
            }

            var style = AestheticMappingLayer.Instance.CurrentParameters.BuildingStyle;
            if (style == BuildingStyle.Modern)
            {
                b.Level += 1;
            }
        }
    }

    // Manager used during generation time
    public class CityGenerationManager
    {
        private readonly Queue<Nts.Polygon> queue = new();
        private readonly CityGenerationData data;
        private bool processing;

        public CityGenerationManager(CityGenerationData data)
        {
            this.data = data;
            RoadNetworkGenerator.Data = data;
        }

        public void QueueArea(Nts.Polygon area)
        {
            lock (queue)
            {
                queue.Enqueue(area);
                if (!processing)
                {
                    processing = true;
                    _ = ProcessQueue();
                }
            }
        }

        public bool IsProcessing()
        {
            lock (queue)
            {
                return processing || queue.Count > 0;
            }
        }

        public int GetQueueCount()
        {
            lock (queue)
            {
                return queue.Count;
            }
        }

        private async Task ProcessQueue()
        {
            while (true)
            {
                List<Nts.Polygon> batch;
                lock (queue)
                {
                    if (queue.Count == 0)
                    {
                        processing = false;
                        return;
                    }
                    batch = new List<Nts.Polygon>(queue);
                    queue.Clear();
                }

                await Parallel.ForEachAsync(batch, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (area, token) =>
                {
                    var p = AestheticMappingLayer.Instance.CurrentParameters;
                    await RoadNetworkGenerator.GenerateModelAsync(area, 10, p).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
    }

    // Simple evolution manager that updates land use and buildings each year
    public class CityEvolutionManager
    {
        private readonly CityDataModel model;

        public CityEvolutionManager(CityDataModel model)
        {
            this.model = model;
        }

        public void SimulateYear()
        {
            LandUseSimulator.Run(model);
            BuildingRefiner.RefineBuildings(model);
        }
    }
}
