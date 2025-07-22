using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    /// <summary>
    /// Generates city parcels by polygonizing the road network.
    /// The algorithm unions all road and boundary lines into a
    /// single noded network before polygonization to ensure
    /// valid polygons within the urban area.
    /// </summary>
    public static class ParcelGenerator
    {
        private const double Epsilon = 1e-9;

        private static bool IsBad(Coordinate c) =>
            double.IsNaN(c.X) || double.IsNaN(c.Y) ||
            double.IsInfinity(c.X) || double.IsInfinity(c.Y);

        public static List<Parcel> GenerateParcels(CityDataModel model)
        {
            var parcels = new List<Parcel>();
            if (model.RoadNetwork == null || !model.RoadNetwork.Any() || model.UrbanArea == null)
                return parcels;

            var gf = Nts.GeometryFactory.Default;

            // 1. Convert road segments to line strings.
            var roadLines = model.RoadNetwork
                .Where(seg =>
                    !IsBad(new Coordinate(seg.X1, seg.Y1)) &&
                    !IsBad(new Coordinate(seg.X2, seg.Y2)) &&
                    Math.Abs(seg.X1 - seg.X2) + Math.Abs(seg.Y1 - seg.Y2) > Epsilon)
                .Select(seg => gf.CreateLineString(new[]
                {
                    new Coordinate(seg.X1, seg.Y1),
                    new Coordinate(seg.X2, seg.Y2)
                }))
                .ToList<Nts.Geometry>();

            // 2. Add the urban area boundary.
            if (model.UrbanArea.IsValid)
            {
                roadLines.Add(model.UrbanArea.Boundary);
            }

            // 3. Node the entire line network via union.
            var nodedLines = UnaryUnionOp.Union(roadLines);

            // 4. Polygonize the noded lines.
            var polygonizer = new Polygonizer();
            polygonizer.Add(nodedLines);
            var rawPolys = polygonizer.GetPolygons();
            Debug.WriteLine($"[ParcelGenerator] Polygonizer produced {rawPolys.Count} raw polygons");

            var blocks = rawPolys.OfType<Nts.Polygon>()
                .Where(p => p.IsValid && p.Area > 1e-9)
                .ToList();

            model.RawBlocks = blocks;
            parcels = GenerateParcelsFromBlocks(blocks);

            Debug.WriteLine($"[ParcelGenerator] Final parcel count: {parcels.Count}");
            return parcels;
        }

        public static List<Parcel> GenerateParcelsFromBlocks(List<Nts.Polygon> blocks)
        {
            var parcels = new List<Parcel>();
            foreach (var block in blocks)
            {
                if (block.IsValid && !block.IsEmpty && block.Area > 1e-9)
                    SubdividePolygon(block, parcels);
            }
            return parcels;
        }

        private static void SubdividePolygon(Nts.Polygon poly, List<Parcel> output)
        {
            const double MinParcelArea = 0.00005;
            RecursiveSplit(poly, output, MinParcelArea);
        }

        private static void RecursiveSplit(Nts.Polygon poly, List<Parcel> output, double minArea)
        {
            Debug.WriteLine($"Splitting polygon. Area: {poly.Area}, IsValid: {poly.IsValid}, Envelope: {poly.EnvelopeInternal}");

            if (poly.Area < minArea * 1.5)
            {
                if (poly.IsValid && !poly.IsEmpty)
                    output.Add(new Parcel { Shape = poly });
                return;
            }

            var envelope = poly.EnvelopeInternal;
            var gf = poly.Factory;
            bool splitVertical = envelope.Width > envelope.Height;

            Nts.Geometry splitLine;
            if (splitVertical)
            {
                double midX = envelope.MinX + envelope.Width / 2;
                splitLine = gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(midX, envelope.MinY),
                    new Nts.Coordinate(midX, envelope.MaxY)
                });
            }
            else
            {
                double midY = envelope.MinY + envelope.Height / 2;
                splitLine = gf.CreateLineString(new[]
                {
                    new Nts.Coordinate(envelope.MinX, midY),
                    new Nts.Coordinate(envelope.MaxX, midY)
                });
            }

            try
            {
                var splitGeometries = poly.Difference(splitLine);
                if (splitGeometries.NumGeometries < 2)
                {
                    if (poly.IsValid && !poly.IsEmpty)
                        output.Add(new Parcel { Shape = poly });
                    return;
                }

                for (int i = 0; i < splitGeometries.NumGeometries; i++)
                {
                    if (splitGeometries.GetGeometryN(i) is Nts.Polygon splitPoly && splitPoly.IsValid && !splitPoly.IsEmpty)
                    {
                        RecursiveSplit(splitPoly, output, minArea);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RECURSIVE SPLIT ERROR] {ex.Message}");
                if (poly.IsValid && !poly.IsEmpty)
                    output.Add(new Parcel { Shape = poly });
            }
        }
    }
}
