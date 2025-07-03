using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;  // <- correct namespace for UnaryUnionOp
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StrategyGame
{
    public static class ParcelGenerator
    {
        /// <summary>
        /// Generates parcels by noding (via unary-union) and polygonizing the road network.
        /// </summary>
        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcels = new List<Parcel>();
            var gf = Nts.GeometryFactory.Default;

            // 1. Build LineStrings from road segments
            var lineStrings = model.RoadNetwork
                .Select(seg => gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(seg.X1, seg.Y1),
                    new Nts.Coordinate(seg.X2, seg.Y2)
                }))
                .ToArray();

            if (lineStrings.Length == 0)
            {
                model.Parcels = parcels;
                return parcels;
            }

            // 2. Combine and node via UnaryUnion (overlay)
            var multiLine = gf.CreateMultiLineString(lineStrings);
            // UnaryUnion automatically snaps / nodes linework and splits at intersections
            var nodedGeometry = UnaryUnionOp.Union((Nts.Geometry)multiLine);

            // 3. Extract all LineStrings from the noded result
            var nodedLines = new List<Nts.LineString>();
            CollectLineStrings(nodedGeometry, nodedLines);
            Debug.WriteLine($"[ParcelGenerator] Noded line count: {nodedLines.Count}");

            // 4. Polygonize the fully noded network
            var polygonizer = new Polygonizer();
            foreach (var ls in nodedLines)
                polygonizer.Add(ls);

            var rawPolys = polygonizer.GetPolygons();
            Debug.WriteLine($"[ParcelGenerator] Polygonizer produced {rawPolys.Count} raw polygons");

            // 5. Extract closed polygons and subdivide into parcels
            foreach (var geom in rawPolys)
            {
                if (geom is Nts.Polygon poly)
                    SubdivideBlock(poly, parcels);
            }

            Debug.WriteLine($"[ParcelGenerator] Final parcel count: {parcels.Count}");
            model.Parcels = parcels;
            return parcels;
        }

        /// <summary>
        /// Recursively collects LineStrings from any geometry type.
        /// </summary>
        private static void CollectLineStrings(Nts.Geometry geom, List<Nts.LineString> output)
        {
            switch (geom)
            {
                case Nts.LineString ls:
                    output.Add(ls);
                    break;
                case Nts.MultiLineString mls:
                case Nts.GeometryCollection gc:
                    for (int i = 0; i < geom.NumGeometries; i++)
                        CollectLineStrings(geom.GetGeometryN(i), output);
                    break;
            }
        }

        private static void RecursiveOBBSplitting(Nts.Polygon poly, List<Parcel> output, int depth)
        {
            const double MinArea = 0.0001;
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
                if (poly.Intersection(leftRect) is Nts.Polygon lp)
                    RecursiveOBBSplitting(lp, output, depth + 1);
                if (poly.Intersection(rightRect) is Nts.Polygon rp)
                    RecursiveOBBSplitting(rp, output, depth + 1);
            }
            else
            {
                double midY = (env.MinY + env.MaxY) / 2.0;
                var botRect = gf.ToGeometry(new Nts.Envelope(env.MinX, env.MaxX, env.MinY, midY));
                var topRect = gf.ToGeometry(new Nts.Envelope(env.MinX, env.MaxX, midY, env.MaxY));
                if (poly.Intersection(botRect) is Nts.Polygon bp)
                    RecursiveOBBSplitting(bp, output, depth + 1);
                if (poly.Intersection(topRect) is Nts.Polygon tp)
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