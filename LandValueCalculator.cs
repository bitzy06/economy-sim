using System;
using System.Linq;
using System.Threading;
using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    /// <summary>
    /// Calculates land value for each parcel in a city model.
    /// </summary>
    public static class LandValueCalculator
    {
        private static readonly ThreadLocal<Random> Rng = new(() => new Random());

        /// <summary>
        /// Assigns a land value between 0 and 100 to each parcel based on
        /// distance from the road network centre. Placeholder implementation.
        /// </summary>
        public static void Calculate(CityDataModel model)
        {
            if (model?.Parcels == null || model.RoadNetwork == null)
                return;

            var env = new Nts.Envelope();
            foreach (var seg in model.RoadNetwork)
            {
                env.ExpandToInclude(seg.X1, seg.Y1);
                env.ExpandToInclude(seg.X2, seg.Y2);
            }
            if (env.IsNull)
                return;

            var center = env.Centre;
            var centerPt = new Nts.Point(center);
            double maxDist = center.Distance(new Nts.Coordinate(env.MinX, env.MinY));
            if (maxDist < 1e-6)
                maxDist = 1.0;

            foreach (var parcel in model.Parcels)
            {
                var pCenter = parcel.Shape.Centroid;
                double dist = pCenter.Distance(centerPt);
                double landValue = Math.Max(0, 100 * (1 - dist / maxDist));
                landValue += Rng.Value.NextDouble() * 20 - 10;
                parcel.LandValue = Math.Clamp(landValue, 0, 100);
            }
        }
    }
}
