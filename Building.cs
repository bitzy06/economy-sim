using Nts = NetTopologySuite.Geometries;
using System;
using System.Collections.Concurrent;

namespace StrategyGame
{
    public class Building
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Nts.Polygon Footprint { get; set; }
        // Simplified footprints cached by cell size for rendering
        public ConcurrentDictionary<int, Nts.Geometry> SimplifiedFootprints { get; } = new();
        public LandUseType LandUse { get; set; }

        // Fields used by the BuildingRefiner
        public int Level { get; set; }
        public int PopulationCapacity { get; set; }
        public double EconomicOutput { get; set; }
        public double PollutionOutput { get; set; }
    }
}
