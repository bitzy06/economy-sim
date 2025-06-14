using System;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace StrategyGame
{
    /// <summary>
    /// Utility class that creates a raster mask where each pixel contains
    /// the ISO country code from the Natural Earth shapefile.
    /// </summary>
    public static class CountryMaskGenerator
    {
        /// <summary>
        /// Generates a rasterized country mask matching the given DEM.
        /// </summary>
        /// <param name="demPath">Path to the DEM GeoTIFF.</param>
        /// <param name="shpPath">Path to the Natural Earth countries shapefile.</param>
        /// <returns>Two dimensional array of ISO codes indexed by row/column.</returns>
        public static int[,] CreateCountryMask(string demPath, string shpPath)
        {
            Gdal.AllRegister();
            Ogr.RegisterAll();

            Dataset dem = Gdal.Open(demPath, Access.GA_ReadOnly);
            if (dem == null)
                throw new ApplicationException($"Failed to open {demPath}");

            double[] gt = new double[6];
            dem.GetGeoTransform(gt);
            int cols = dem.RasterXSize;
            int rows = dem.RasterYSize;

            OSGeo.GDAL.Driver memDrv = Gdal.GetDriverByName("MEM");
             Dataset maskDs = memDrv.Create("", cols, rows, 1, DataType.GDT_Int32, null);
            maskDs.SetGeoTransform(gt);
            maskDs.SetProjection(dem.GetProjection());

            DataSource ds = Ogr.Open(shpPath, 0);
            if (ds == null)
                throw new ApplicationException($"Failed to open {shpPath}");
            Layer layer = ds.GetLayerByIndex(0);

            Gdal.RasterizeLayer(
         maskDs,               // 1) target Dataset
         1,                    // 2) number of bands
         new[] { 1 },          // 3) band list
         layer,                // 4) source OGR Layer
         IntPtr.Zero,          // 5) papszRasterizeOptions (unused)
         IntPtr.Zero,          // 6) pProgressData (unused)
         0,                    // 7) nBurnValues = 0 (using ATTRIBUTE mode instead)
         null,                 // 8) padfBurnValues (none)
         new[] { "ATTRIBUTE=ISO_A3_EH" }, // 9) your rasterize options
         null,                 // 10) no progress callback
         ""                    // 11) empty progress‐arg string
     );

            Band band = maskDs.GetRasterBand(1);
            int[] flat = new int[cols * rows];
            band.ReadRaster(0, 0, cols, rows, flat, cols, rows, 0, 0);

            int[,] result = new int[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    result[r, c] = flat[r * cols + c];
                }
            }

            return result;
        }
    }
}
