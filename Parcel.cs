using NetTopologySuite.Geometries;

namespace StrategyGame
{
    public enum LandUseType { Commercial, Residential, Industrial, Park }

    public class Parcel
    {
        public Polygon Shape { get; set; }
        public LandUseType LandUse { get; set; }
    }
}
