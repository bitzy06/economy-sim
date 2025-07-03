using Nts = NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Index.Strtree;
using System.Threading;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class LandUseAssigner
    {
        private static readonly ThreadLocal<Random> Rng =
            new ThreadLocal<Random>(() => new Random());

        public static void AssignLandUse(CityDataModel model)
        {
            if (model.Parcels == null || !model.Parcels.Any() || model.RoadNetwork == null || !model.RoadNetwork.Any())
                return;

            var gf = Nts.GeometryFactory.Default;
            var cityEnvelope = new Nts.Envelope();
            foreach (var seg in model.RoadNetwork)
            {
                cityEnvelope.ExpandToInclude(seg.X1, seg.Y1);
                cityEnvelope.ExpandToInclude(seg.X2, seg.Y2);
            }

            if (cityEnvelope.IsNull) return;

            var centerPoint = cityEnvelope.Centre;
            var maxDist = centerPoint.Distance(new Nts.Coordinate(cityEnvelope.MinX, cityEnvelope.MinY));
            if (maxDist < 1e-6) maxDist = 1.0; // Avoid division by zero for very small areas

            var allRoads = model.RoadNetwork
                .Select(seg =>
                {
                    var ls = gf.CreateLineString(new[]
                    {
                        new Nts.Coordinate(seg.X1, seg.Y1),
                        new Nts.Coordinate(seg.X2, seg.Y2)
                    });
                    ls.UserData = seg.Type;
                    return ls;
                })
                .ToList();

            var roadIndex = new STRtree<Nts.LineString>();
            foreach (var road in allRoads)
            {
                roadIndex.Insert(road.EnvelopeInternal, road);
            }
            roadIndex.Build();

            Parallel.ForEach(model.Parcels, parcel =>
            {
                var weights = new Dictionary<LandUseType, double>();
                var parcelCenter = parcel.Shape.Centroid;

                double distToCenter = parcelCenter.Distance(new Nts.Point(centerPoint));

                var searchEnv = new Nts.Envelope(parcel.Shape.EnvelopeInternal);
                searchEnv.ExpandBy(0.01);
                var candidates = roadIndex.Query(searchEnv);

                double distToPrimary = double.MaxValue;
                double distToAnyRoad = double.MaxValue;
                foreach (var candidate in candidates)
                {
                    double d = candidate.Distance(parcel.Shape);
                    distToAnyRoad = Math.Min(distToAnyRoad, d);
                    if (candidate.UserData is RoadType type && type == RoadType.Primary)
                    {
                        distToPrimary = Math.Min(distToPrimary, d);
                    }
                }

                // --- Calculate Weights ---

                // Commercial: High value near primary roads and the city center.
                double commercialWeight = 0;
                if (distToPrimary < 0.005) commercialWeight += 50;
                commercialWeight += Math.Max(0, 30 * (1 - distToCenter / (maxDist * 0.4)));

                // Residential: Prefers being away from the absolute center but still well-connected.
                double residentialWeight = 20; // Base desire
                if (distToCenter > maxDist * 0.1 && distToCenter < maxDist * 0.7) residentialWeight += 30;
                if (distToAnyRoad < 0.002) residentialWeight += 20;

                // Industrial: Prefers the outskirts and good highway access.
                double industrialWeight = 0;
                if (distToPrimary < 0.01) industrialWeight += 20;
                industrialWeight += Math.Max(0, 40 * (distToCenter / (maxDist * 0.6) - 1.0));

                // Park: Fills in areas that aren't ideal for other uses.
                double parkWeight = 5 + (1 - residentialWeight / 70) * 15;

                weights[LandUseType.Commercial] = Math.Max(0.1, commercialWeight);
                weights[LandUseType.Residential] = Math.Max(0.1, residentialWeight);
                weights[LandUseType.Industrial] = Math.Max(0.1, industrialWeight);
                weights[LandUseType.Park] = Math.Max(0.1, parkWeight);

                // --- Select Land Use ---
                parcel.LandUse = GetRandomLandUse(weights);
            });
        }

        private static LandUseType GetRandomLandUse(Dictionary<LandUseType, double> weights)
        {
            double totalWeight = weights.Values.Sum();
            if (totalWeight <= 0) return LandUseType.Park; // Fallback

            double randomValue = Rng.Value.NextDouble() * totalWeight;

            foreach (var (landUse, weight) in weights)
            {
                if (randomValue < weight)
                {
                    return landUse;
                }
                randomValue -= weight;
            }

            return LandUseType.Park; // Fallback
        }
    }
}