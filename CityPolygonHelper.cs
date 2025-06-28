using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace StrategyGame
{
    public static class CityPolygonHelper
    {
        public static void AssignUrbanPolygons(List<City> cities, List<Polygon> urbanAreaPolygons)
        {
            foreach (var city in cities)
            {
                var point = new Point(city.Longitude, city.Latitude);
                Polygon best = null;
                double bestDist = double.MaxValue;

                foreach (var poly in urbanAreaPolygons)
                {
                    if (poly.Contains(point))
                    {
                        best = poly;
                        break;
                    }

                    double dist = poly.Distance(point);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = poly;
                    }
                }

                city.OriginalPolygon = best;
                city.CurrentPolygon = best;
            }
        }

        public static void DrawCityPolygon(Image<Rgba32> image, City city, int mapWidthPx, int mapHeightPx)
        {
            if (city.CurrentPolygon == null)
                return;

            var fill = new Rgba32(150, 150, 150, 90);
            var outline = new Rgba32(70, 70, 70, 180);

            var exterior = city.CurrentPolygon.ExteriorRing.Coordinates
                .Select(c => new PointF(
                    (float)((c.X + 180.0) / 360.0 * mapWidthPx),
                    (float)(((90.0 - c.Y) / 180.0) * mapHeightPx)))
                .ToArray();

            image.Mutate(ctx => ctx.FillPolygon(fill, exterior));
            image.Mutate(ctx => ctx.DrawPolygon(outline, 2, exterior));
        }
    }
}
