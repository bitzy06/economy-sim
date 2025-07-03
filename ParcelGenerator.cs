using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Geometries.Utilities; // Add this namespace for splitting polygons
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class ParcelGenerator
    {
        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcelsBag = new ConcurrentBag<Parcel>();
            if (model.RoadNetwork == null || !model.RoadNetwork.Any())
            {
                return parcelsBag.ToList();
            }

            var gf = Nts.GeometryFactory.Default;

            var lineStrings = model.RoadNetwork
                .Select(seg => gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(seg.X1, seg.Y1),
                    new Nts.Coordinate(seg.X2, seg.Y2)
                }))
                .ToArray();

            if (lineStrings.Length == 0)
            {
                return parcelsBag.ToList();
            }

            var nodedLines = (Nts.MultiLineString)lineStrings.First().Union(lineStrings.Skip(1).First());
            for (int i = 2; i < lineStrings.Length; i++)
            {
                nodedLines = (Nts.MultiLineString)nodedLines.Union(lineStrings[i]);
            }

            var polygonizer = new Polygonizer();
            polygonizer.Add(nodedLines);

            var rawPolys = polygonizer.GetPolygons();
            Debug.WriteLine($"[ParcelGenerator] Polygonizer produced {rawPolys.Count} raw polygons");

            Parallel.ForEach(rawPolys.Cast<Nts.Geometry>(), geom =>
            {
                if (geom is Nts.Polygon poly && poly.IsValid && poly.Area > 1e-9)
                {
                    if (poly.Area > 0.0001)
                    {
                        RecursiveOBBSplitting(poly, parcelsBag, 0);
                    }
                    else
                    {
                        parcelsBag.Add(new Parcel { Shape = poly });
                    }
                }
            });

            Debug.WriteLine($"[ParcelGenerator] Final parcel count: {parcelsBag.Count}");
            return parcelsBag.ToList();
        }

        private static void RecursiveOBBSplitting(Nts.Polygon poly, ICollection<Parcel> output, int depth)
        {
            const double MinArea = 0.00005;
            const int MaxDepth = 4;

            if (poly.Area < MinArea || depth > MaxDepth)
            {
                if (poly.IsValid && poly.Area > 1e-9)
                {
                    output.Add(new Parcel { Shape = poly });
                }
                return;
            }

            var env = poly.EnvelopeInternal;
            bool splitVertical = env.Width > env.Height;
            var gf = Nts.GeometryFactory.Default;

            try
            {
                Nts.Geometry splitLine;
                if (splitVertical)
                {
                    double midX = env.MinX + env.Width / 2;
                    splitLine = gf.CreateLineString(new[] { new Nts.Coordinate(midX, env.MinY), new Nts.Coordinate(midX, env.MaxY) });
                }
                else
                {
                    double midY = env.MinY + env.Height / 2;
                    splitLine = gf.CreateLineString(new[] { new Nts.Coordinate(env.MinX, midY), new Nts.Coordinate(env.MaxX, midY) });
                }

                // Replace the problematic line with a manual splitting approach
                var splitPolygons = poly.Difference(splitLine);

                if (splitPolygons is Nts.MultiPolygon multiPoly)
                {
                    foreach (var geom in multiPoly.Geometries)
                    {
                        if (geom is Nts.Polygon splitPoly && splitPoly.IsValid)
                        {
                            RecursiveOBBSplitting(splitPoly, output, depth + 1);
                        }
                    }
                }
                else
                {
                    if (poly.IsValid && poly.Area > 1e-9)
                    {
                        output.Add(new Parcel { Shape = poly });
                    }
                }
            }
            catch (System.Exception)
            {
                if (poly.IsValid && poly.Area > 1e-9)
                {
                    output.Add(new Parcel { Shape = poly });
                }
            }
        }
    }
}