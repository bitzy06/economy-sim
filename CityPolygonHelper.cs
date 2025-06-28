using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using NetTopologySuite.IO;
using SixLabors.ImageSharp.Processing;

namespace StrategyGame
{
    public static class CityPolygonHelper
    {
        public static void AssignUrbanPolygons(List<City> cities, List<Polygon> urbanAreaPolygons)
        {
            foreach (var city in cities)
            {
                var point = new NetTopologySuite.Geometries.Point(city.Longitude, city.Latitude);
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

        public static void DrawCityPolygon(
            Image<Rgba32> image,
            City city,
            int mapWidthPx,
            int mapHeightPx,
            Rgba32? fillColor = null,
            Rgba32? outlineColor = null)
        {
            DrawCityPolygonOnTile(
                image,
                city,
                mapWidthPx,
                mapHeightPx,
                0,
                0,
                fillColor,
                outlineColor);
        }

        public static void DrawCityPolygonOnTile(
            Image<Rgba32> image,
            City city,
            int mapWidthPx,
            int mapHeightPx,
            int offsetX,
            int offsetY,
            Rgba32? fillColor = null,
            Rgba32? outlineColor = null)
        {
            if (city.CurrentPolygon == null)
                return;

            var fill = fillColor ?? new Rgba32(150, 150, 150, 90);
            var outline = outlineColor ?? new Rgba32(70, 70, 70, 180);

            var exterior = city.CurrentPolygon.ExteriorRing.Coordinates
                .Select(c => new SixLabors.ImageSharp.PointF(
                    (float)((c.X + 180.0) / 360.0 * mapWidthPx),
                    (float)(((90.0 - c.Y) / 180.0) * mapHeightPx)))
                .ToArray();

            image.Mutate(ctx => ctx.FillPolygon(fill, exterior));
            image.Mutate(ctx => ctx.DrawPolygon(outline, 2, exterior));
        }

        public static List<Polygon> LoadUrbanPolygons(string shapefilePath)
        {
            var result = new List<Polygon>();
            using var reader = new ShapefileDataReader(shapefilePath, GeometryFactory.Default);
            while (reader.Read())
            {
                var geom = reader.Geometry;
                if (geom is Polygon p)
                    result.Add(p);
                else if (geom is MultiPolygon multi)
                {
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        if (multi.GetGeometryN(i) is Polygon poly)
                            result.Add(poly);
                    }
                }
            }
            return result;
        }

        public static void UpdateAllCityPolygons(List<City> cities)
        {
            foreach (var city in cities)
            {
                if (city.OriginalPolygon == null)
                    continue;

                double bufferDist = Math.Log(city.Population / 100000.0 + 1) * 0.01;
                city.CurrentPolygon = (Polygon)city.OriginalPolygon.Buffer(bufferDist);
            }
        }

        // Example update loop combining economics and polygon growth
        //
        // foreach (var city in allCitiesInWorld)
        // {
        //     Economy.UpdateCityEconomy(city);
        // }
        // CityPolygonHelper.UpdateAllCityPolygons(allCitiesInWorld);
    }
}
