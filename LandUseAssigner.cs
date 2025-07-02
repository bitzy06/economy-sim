using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class LandUseAssigner
    {
        public static void AssignLandUse(CityDataModel model)
        {
            if (model.Parcels == null)
                return;
            var gf = GeometryFactory.Default;
            foreach (var parcel in model.Parcels)
            {
                double best = double.MaxValue;
                foreach (var seg in model.RoadNetwork)
                {
                    var ls = gf.CreateLineString(new[]
                    {
                        new Coordinate(seg.X1, seg.Y1),
                        new Coordinate(seg.X2, seg.Y2)
                    });
                    double d = ls.Distance(parcel.Shape);
                    if (d < best) best = d;
                }

                if (best < 0.001)
                    parcel.LandUse = LandUseType.Commercial;
                else if (best < 0.005)
                    parcel.LandUse = LandUseType.Residential;
                else if (parcel.Shape.Area > 0.005)
                    parcel.LandUse = LandUseType.Industrial;
                else
                    parcel.LandUse = LandUseType.Park;
            }
        }
    }
}
