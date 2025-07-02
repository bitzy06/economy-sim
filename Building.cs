using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    public class Building
    {
        public Nts.Polygon Footprint { get; set; }
        public LandUseType LandUse { get; set; }
    }
}
