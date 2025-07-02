using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class ParcelGenerator
    {
        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcels = new List<Parcel>();
            var gf = GeometryFactory.Default;

            var pizer = new Polygonizer();
            foreach (var seg in model.RoadNetwork)
            {
                var ls = gf.CreateLineString(new[]
                {
                    new Coordinate(seg.X1, seg.Y1),
                    new Coordinate(seg.X2, seg.Y2)
                });
                pizer.Add(ls);
            }

            foreach (Polygon poly in pizer.GetPolygons())
            {
                Subdivide(poly, parcels, 0);
            }

            model.Parcels = parcels;
            return parcels;
        }

        private static void Subdivide(Polygon poly, List<Parcel> output, int depth)
        {
            const double MinArea = 0.0001; // arbitrary small area threshold
            if (poly.Area < MinArea || depth > 4)
            {
                output.Add(new Parcel { Shape = poly });
                return;
            }
            var env = poly.EnvelopeInternal;
            bool vertical = env.Width > env.Height;
            var gf = GeometryFactory.Default;
            if (vertical)
            {
                double midX = (env.MinX + env.MaxX) / 2.0;
                var leftRect = gf.ToGeometry(new Envelope(env.MinX, midX, env.MinY, env.MaxY));
                var rightRect = gf.ToGeometry(new Envelope(midX, env.MaxX, env.MinY, env.MaxY));
                var left = poly.Intersection(leftRect);
                var right = poly.Intersection(rightRect);
                if (left is Polygon lp)
                    Subdivide(lp, output, depth + 1);
                if (right is Polygon rp)
                    Subdivide(rp, output, depth + 1);
            }
            else
            {
                double midY = (env.MinY + env.MaxY) / 2.0;
                var botRect = gf.ToGeometry(new Envelope(env.MinX, env.MaxX, env.MinY, midY));
                var topRect = gf.ToGeometry(new Envelope(env.MinX, env.MaxX, midY, env.MaxY));
                var bot = poly.Intersection(botRect);
                var top = poly.Intersection(topRect);
                if (bot is Polygon bp)
                    Subdivide(bp, output, depth + 1);
                if (top is Polygon tp)
                    Subdivide(tp, output, depth + 1);
            }
        }
    }
}
