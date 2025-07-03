using Nts = NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class LandUseAssigner
    {
        public static void AssignLandUse(CityDataModel model)
        {
            if (model.Parcels == null)
                return;
            var gf = Nts.GeometryFactory.Default;

            // Determine approximate city center from the road network envelope
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var seg in model.RoadNetwork)
            {
                minX = Math.Min(minX, Math.Min(seg.X1, seg.X2));
                minY = Math.Min(minY, Math.Min(seg.Y1, seg.Y2));
                maxX = Math.Max(maxX, Math.Max(seg.X1, seg.X2));
                maxY = Math.Max(maxY, Math.Max(seg.Y1, seg.Y2));
            }
            var centerPoint = gf.CreatePoint(new Nts.Coordinate((minX + maxX) / 2.0, (minY + maxY) / 2.0));

            foreach (var parcel in model.Parcels)
            {
                double distToRoad = double.MaxValue;
                double distToPrimary = double.MaxValue;
                foreach (var seg in model.RoadNetwork)
                {
                    var ls = gf.CreateLineString(new[]
                    {
                        new Nts.Coordinate(seg.X1, seg.Y1),
                        new Nts.Coordinate(seg.X2, seg.Y2)
                    });
                    double d = ls.Distance(parcel.Shape);
                    if (d < distToRoad) distToRoad = d;
                    if (seg.Type == RoadType.Primary && d < distToPrimary) distToPrimary = d;
                }

                double distToCenter = parcel.Shape.Centroid.Distance(centerPoint);

                if (distToPrimary < 0.002)
                    parcel.LandUse = LandUseType.Commercial;
                else if (distToCenter < 0.02 && distToRoad < 0.004)
                    parcel.LandUse = LandUseType.Residential;
                else if (distToCenter > 0.03 && distToRoad < 0.01)
                    parcel.LandUse = LandUseType.Industrial;
                else
                    parcel.LandUse = LandUseType.Park;
            }
        }
    }
}
