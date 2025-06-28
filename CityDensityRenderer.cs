using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace StrategyGame
{
    /// <summary>
    /// Utility to load Natural Earth city data and render urban areas as
    /// pixel-art base colors. This keeps city shapes conforming to the
    /// underlying density polygons.
    /// </summary>
    public static class CityDensityRenderer
    {
        /// <summary>
        /// Load populated places and assign the nearest/containing urban area
        /// polygon to each created city.
        /// </summary>
        public static List<City> LoadCitiesFromNaturalEarth(
            string placesShpPath,
            string urbanAreasShpPath)
        {
            var cities = new List<City>();
            using var reader = new ShapefileDataReader(placesShpPath, GeometryFactory.Default);

            int nameIdx = reader.GetOrdinal("NAME");
            int popIdx = reader.GetOrdinal("POP_MAX");
            while (reader.Read())
            {
                if (reader.Geometry is not Point pt)
                    continue;

                var city = new City(reader.GetString(nameIdx))
                {
                    Latitude = pt.Y,
                    Longitude = pt.X,
                    Population = reader.GetInt32(popIdx)
                };
                cities.Add(city);
            }

            var urbanPolys = CityPolygonHelper.LoadUrbanPolygons(urbanAreasShpPath);
            CityPolygonHelper.AssignUrbanPolygons(cities, urbanPolys);
            CityPolygonHelper.UpdateAllCityPolygons(cities);
            return cities;
        }

        /// <summary>
        /// Draw a city's current polygon as a simple base color. This does not
        /// include buildings yet but respects the irregular urban area shape.
        /// </summary>
        public static void DrawBasePalette(
            Image<Rgba32> image,
            City city,
            int mapWidthPx,
            int mapHeightPx,
            int offsetX = 0,
            int offsetY = 0)
        {
            var fill = new Rgba32(120, 120, 120, 100);
            var outline = new Rgba32(30, 30, 30, 180);
            CityPolygonHelper.DrawCityPolygonOnTile(
                image,
                city,
                mapWidthPx,
                mapHeightPx,
                offsetX,
                offsetY,
                fill,
                outline);
        }
    }
}
