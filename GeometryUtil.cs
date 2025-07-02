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
    }
}
