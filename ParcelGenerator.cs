using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Geometries.Utilities; // Add this namespace for splitting polygons
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace StrategyGame
{
    public static class ParcelGenerator
    {
        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcelsBag = new System.Collections.Concurrent.ConcurrentBag<Parcel>();
            if (model.RoadNetwork == null || !model.RoadNetwork.Any())
            {
                return new List<Parcel>();
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
                return new List<Parcel>();
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

            Parallel.ForEach(rawPolys.OfType<Nts.Polygon>().Where(p => p.IsValid && p.Area > 1e-9), poly =>
            {
                var local = new List<Parcel>();
                if (poly.Area > 0.0001)
                    RecursiveOBBSplitting(poly, local, 0);
                else
                    local.Add(new Parcel { Shape = poly });
                foreach (var p in local) parcelsBag.Add(p);
            });

            var parcels = parcelsBag.ToList();

            Debug.WriteLine($"[ParcelGenerator] Final parcel count: {parcels.Count}");
            return parcels;
        }

        private static void RecursiveOBBSplitting(Nts.Polygon poly, List<Parcel> output, int depth)
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