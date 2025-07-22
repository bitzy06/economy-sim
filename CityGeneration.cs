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

        public static void RunWithCompetition(CityDataModel model, AgentDevelopmentManager manager, EconomicData data)
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
            }

            manager.AllocateParcels(model.Parcels, data);
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
    public class CityGenerationManager : IDisposable
    {
        private readonly ConcurrentQueue<Nts.Polygon> queue = new();
        private readonly SemaphoreSlim signal = new(0);
        private readonly CancellationTokenSource cts = new();
        private readonly CityGenerationData data;
        private int activeWorkers;
        private readonly Task worker;

        public CityGenerationManager(CityGenerationData data)
        {
            this.data = data;
            RoadNetworkGenerator.Data = data;
            MessageBus.Instance.Subscribe<CityGenerationRequestEventData>(OnRequest);
            worker = Task.Run(ProcessQueueAsync);
        }

        private void OnRequest(CityGenerationRequestEventData req)
        {
            QueueArea(req.Area);
        }

        public void QueueArea(Nts.Polygon area)
        {
            queue.Enqueue(area);
            signal.Release();
        }

        public bool IsProcessing() => !queue.IsEmpty || Volatile.Read(ref activeWorkers) > 0;

        public int GetQueueCount() => queue.Count;

        private async Task ProcessQueueAsync()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await signal.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!queue.TryDequeue(out var area))
                    continue;

                Interlocked.Increment(ref activeWorkers);
                try
                {
                    var p = AestheticMappingLayer.Instance.CurrentParameters;
                    var model = await RoadNetworkGenerator.GenerateModelAsync(area, 10, p).ConfigureAwait(false);
                    MessageBus.Instance.Publish(new CityGenerationCompletedEventData(model.Id, area));
                }
                finally
                {
                    Interlocked.Decrement(ref activeWorkers);
                }
            }
        }

        public void Cancel() => cts.Cancel();

        public void Dispose()
        {
            Cancel();
            signal.Dispose();
            cts.Dispose();
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
