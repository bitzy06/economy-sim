using System;

namespace StrategyGame
{
    public static class GeoBoundsExtensions
    {
        /// <summary>
        /// Returns true if this bounds intersects with another bounds.
        /// </summary>
        public static bool Intersects(this GeoBounds a, GeoBounds b)
        {
            return a.MinLon <= b.MaxLon &&
                   a.MaxLon >= b.MinLon &&
                   a.MinLat <= b.MaxLat &&
                   a.MaxLat >= b.MinLat;
        }
    }
}