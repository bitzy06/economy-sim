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

            var assigned = new List<Parcel>();
            foreach (var parcel in model.Parcels)
            {
                double nearPrimary = double.MaxValue;
                double nearSecondary = double.MaxValue;
                foreach (var seg in model.RoadNetwork)
                {
                    var ls = gf.CreateLineString(new[]
                    {
                        new Nts.Coordinate(seg.X1, seg.Y1),
                        new Nts.Coordinate(seg.X2, seg.Y2)
                    });
                    double d = ls.Distance(parcel.Shape);
                    if (seg.Type == RoadType.Primary && d < nearPrimary) nearPrimary = d;
                    if (seg.Type == RoadType.Secondary && d < nearSecondary) nearSecondary = d;
                }

                double distToCenter = parcel.Shape.Centroid.Distance(centerPoint);

                double commercialScore = 1.0 / (nearPrimary + 0.0001) + 1.0 / (distToCenter + 0.0001);
                double residentialScore = 1.0 / (nearSecondary + 0.0001) + Math.Exp(-distToCenter * 30);
                double industrialScore = (distToCenter * 2) / (nearPrimary + 0.0001);
                double parkScore = 0.2;

                bool nearIndustrial = assigned.Any(p => p.LandUse == LandUseType.Industrial && p.Shape.Distance(parcel.Shape) < 0.003);
                if (nearIndustrial)
                    residentialScore *= 0.3;

                double total = commercialScore + residentialScore + industrialScore + parkScore;
                double r = Random.Shared.NextDouble() * total;
                if ((r -= commercialScore) <= 0)
                    parcel.LandUse = LandUseType.Commercial;
                else if ((r -= residentialScore) <= 0)
                    parcel.LandUse = LandUseType.Residential;
                else if ((r -= industrialScore) <= 0)
                    parcel.LandUse = LandUseType.Industrial;
                else
                    parcel.LandUse = LandUseType.Park;

                assigned.Add(parcel);
            }
        }
    }
}
