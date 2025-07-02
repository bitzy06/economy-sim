using NetTopologySuite.Geometries;

namespace StrategyGame
{
    public class Building
    {
        public Polygon Footprint { get; set; }
        public LandUseType LandUse { get; set; }
    }
}
