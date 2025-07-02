using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class ParcelGenerator
    {
        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcels = new List<Parcel>();
            var gf = Nts.GeometryFactory.Default;

            var pizer = new Polygonizer();
            foreach (var seg in model.RoadNetwork)
            {
                var ls = gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(seg.X1, seg.Y1),
                    new Nts.Coordinate(seg.X2, seg.Y2)
                });
                pizer.Add(ls);
            }

            foreach (Nts.Polygon poly in pizer.GetPolygons())
            {
                SubdivideBlock(poly, parcels);
            }

            model.Parcels = parcels;
            return parcels;
        }

        private static void RecursiveOBBSplitting(Nts.Polygon poly, List<Parcel> output, int depth)
        {
            const double MinArea = 0.0001; // arbitrary small area threshold
            if (poly.Area < MinArea || depth > 4)
            {
                output.Add(new Parcel { Shape = poly });
                return;
            }
            var env = poly.EnvelopeInternal;
            bool vertical = env.Width > env.Height;
            var gf = Nts.GeometryFactory.Default;
            if (vertical)
            {
                double midX = (env.MinX + env.MaxX) / 2.0;
                var leftRect = gf.ToGeometry(new Nts.Envelope(env.MinX, midX, env.MinY, env.MaxY));
                var rightRect = gf.ToGeometry(new Nts.Envelope(midX, env.MaxX, env.MinY, env.MaxY));
                var left = poly.Intersection(leftRect);
                var right = poly.Intersection(rightRect);
                if (left is Nts.Polygon lp)
                    RecursiveOBBSplitting(lp, output, depth + 1);
                if (right is Nts.Polygon rp)
                    RecursiveOBBSplitting(rp, output, depth + 1);
            }
            else
            {
                double midY = (env.MinY + env.MaxY) / 2.0;
                var botRect = gf.ToGeometry(new Nts.Envelope(env.MinX, env.MaxX, env.MinY, midY));
                var topRect = gf.ToGeometry(new Nts.Envelope(env.MinX, env.MaxX, midY, env.MaxY));
                var bot = poly.Intersection(botRect);
                var top = poly.Intersection(topRect);
                if (bot is Nts.Polygon bp)
                    RecursiveOBBSplitting(bp, output, depth + 1);
                if (top is Nts.Polygon tp)
                    RecursiveOBBSplitting(tp, output, depth + 1);
            }
        }

        private static void SkeletonBasedSubdivision(Nts.Polygon poly, List<Parcel> output)
        {
            // Placeholder: simply add the polygon as a parcel
            output.Add(new Parcel { Shape = poly });
        }

        private static void SubdivideBlock(Nts.Polygon poly, List<Parcel> output)
        {
            var env = poly.EnvelopeInternal;
            double aspect = env.Width / env.Height;
            bool regular = aspect > 0.5 && aspect < 2.0 && poly.NumPoints <= 6;
            if (regular)
                RecursiveOBBSplitting(poly, output, 0);
            else
                SkeletonBasedSubdivision(poly, output);
        }
    }
}
