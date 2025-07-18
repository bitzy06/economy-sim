using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    public class Building
    {
        public Nts.Polygon Footprint { get; set; }
        // A simplified version of the footprint used for rendering
        public Nts.Geometry? SimplifiedFootprint { get; set; }
        public LandUseType LandUse { get; set; }

        // Fields used by the BuildingRefiner
        public int Level { get; set; }
        public int PopulationCapacity { get; set; }
        public double EconomicOutput { get; set; }
        public double PollutionOutput { get; set; }
    }
}
