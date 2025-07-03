using Nts = NetTopologySuite.Geometries;

namespace StrategyGame
{
    public struct GeoBounds { public double MinLon, MaxLon, MinLat, MaxLat; }

    public static class GeometryUtil
    {
        public static bool ClipLine(GeoBounds bounds, ref double x1, ref double y1, ref double x2, ref double y2)
        {
            double t0 = 0.0, t1 = 1.0;
            double dx = x2 - x1, dy = y2 - y1;
            double p, q, r;

            p = -dx; q = x1 - bounds.MinLon;
            if (p == 0 && q < 0) return false;
            if (p != 0)
            {
                r = q / p;
                if (p < 0)
                {
                    if (r > t1) return false; if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false; if (r < t1) t1 = r;
                }
            }

            p = dx; q = bounds.MaxLon - x1;
            if (p == 0 && q < 0) return false;
            if (p != 0)
            {
                r = q / p;
                if (p < 0)
                {
                    if (r > t1) return false; if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false; if (r < t1) t1 = r;
                }
            }

            p = -dy; q = y1 - bounds.MinLat;
            if (p == 0 && q < 0) return false;
            if (p != 0)
            {
                r = q / p;
                if (p < 0)
                {
                    if (r > t1) return false; if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false; if (r < t1) t1 = r;
                }
            }

            p = dy; q = bounds.MaxLat - y1;
            if (p == 0 && q < 0) return false;
            if (p != 0)
            {
                r = q / p;
                if (p < 0)
                {
                    if (r > t1) return false; if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false; if (r < t1) t1 = r;
                }
            }

            if (t1 < 1.0) { x2 = x1 + t1 * dx; y2 = y1 + t1 * dy; }
            if (t0 > 0.0) { x1 = x1 + t0 * dx; y1 = y1 + t0 * dy; }

            return true;
        }

        /// <summary>
        /// Computes the intersection point of two line segments without
        /// allocating geometry objects.
        /// </summary>
        /// <param name="p1">First segment start.</param>
        /// <param name="p2">First segment end.</param>
        /// <param name="q1">Second segment start.</param>
        /// <param name="q2">Second segment end.</param>
        /// <param name="intersection">Output intersection point if any.</param>
        /// <returns>True if the segments intersect.</returns>
        public static bool TryGetIntersection(Nts.Coordinate p1, Nts.Coordinate p2,
            Nts.Coordinate q1, Nts.Coordinate q2, out Nts.Coordinate intersection)
        {
            var intersector = new NetTopologySuite.Algorithm.RobustLineIntersector();
            intersector.ComputeIntersection(p1, p2, q1, q2);
            if (intersector.HasIntersection)
            {
                var pt = intersector.GetIntersection(0);
                intersection = new Nts.Coordinate(pt.X, pt.Y);
                return true;
            }

            intersection = default;
            return false;
        }
    }
}
