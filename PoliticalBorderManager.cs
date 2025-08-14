using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OSGeo.GDAL;
using OSGeo.OGR;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Manages political borders using CShapes-2.0 with strict 1950 filtering and exact country matching.
    /// Fixes:
    ///  - Uses correct end-year field name (GWEYEAR).
    ///  - Skips features with missing/invalid dates instead of defaulting to 1900/2000.
    ///  - Uses strict temporal check (no ±0.5 yr fuzz).
    ///  - Removes partial country-name fallback; matches by ISO/code or exact name only.
    ///  - Uses case-insensitive dictionaries for stable lookups.
    /// </summary>
    public class PoliticalBorderManager
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalRegistered = false;

        private readonly PoliticalDataCache _dataCache;
        private readonly string _colorMappingPath;

        public PoliticalBorderManager(string colorMappingPath = "data/country_borders/country_colors.json")
        {
            _colorMappingPath = colorMappingPath;
            EnsureGdalRegistered();

            // Cache is pinned to 1950 in this manager
            _dataCache = new PoliticalDataCache(new DateTime(1950, 1, 1));
        }

        private void EnsureGdalRegistered()
        {
            lock (GdalLock)
            {
                if (!_gdalRegistered)
                {
                    Gdal.AllRegister();
                    Ogr.RegisterAll();
                    _gdalRegistered = true;
                }
            }
        }

        /// <summary>
        /// Builds a raster mask for political borders. Values are "raster codes" for countries.
        /// </summary>
        /// <param name="cshapesPath">Path to CShapes-2.0.shp file</param>
        /// <param name="targetDate">Target date (this manager uses 1950 via its data cache)</param>
        /// <param name="width">Output width</param>
        /// <param name="height">Output height</param>
        /// <param name="bounds">Geographic bounds [minX, minY, maxX, maxY]; defaults to world</param>
        /// <returns>Int32 2D mask of raster codes (height x width)</returns>
        public int[,] CreatePoliticalMask(string cshapesPath, DateTime targetDate, int width, int height, double[] bounds = null)
        {
            lock (GdalLock)
            {
                bounds ??= new[] { -180.0, -90.0, 180.0, 90.0 };

                // Get cached per-country data for 1950 (generated once, then reused)
                var countryData = _dataCache.GetOrGenerateCountryData(cshapesPath);
                Debug.WriteLine($"Using cached country data: {countryData.Count} countries for 1950");

                // Open the CShapes shapefile
                using var ds = Ogr.Open(cshapesPath, 0) ?? throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");
                using var layer = ds.GetLayerByIndex(0) ?? throw new ApplicationException("No layer found in CShapes file");

                // Create in-memory raster
                using var memDrv = Gdal.GetDriverByName("MEM");
                using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);

                // Set geotransform for the specified bounds
                double[] geoTransform = new double[6];
                geoTransform[0] = bounds[0];                       // Top-left X
                geoTransform[1] = (bounds[2] - bounds[0]) / width; // Pixel width
                geoTransform[2] = 0;                               // Rotation
                geoTransform[3] = bounds[3];                       // Top-left Y
                geoTransform[4] = 0;                               // Rotation
                geoTransform[5] = -(bounds[3] - bounds[1]) / height; // Pixel height (negative for north-up)
                maskDs.SetGeoTransform(geoTransform);

                // Basic WGS84 projection
                maskDs.SetProjection("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]");

                // Filter and rasterize using strict 1950 logic
                FilterAndRasterizeUsingCache(layer, maskDs, countryData);

                // Read the result back into a 2D int array
                Band band = maskDs.GetRasterBand(1);
                int[] flat = new int[width * height];
                band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

                int[,] result = new int[height, width];
                for (int r = 0; r < height; r++)
                {
                    int rowOffset = r * width;
                    for (int c = 0; c < width; c++)
                    {
                        result[r, c] = flat[rowOffset + c];
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Filters the input layer for features valid in 1950 and assigns RASTER_CODEs from cached country data,
        /// then rasterizes into maskDs. Strict date checks; exact country matches only.
        /// </summary>
        private void FilterAndRasterizeUsingCache(Layer layer, Dataset maskDs, Dictionary<int, CachedCountryData> countryData)
        {
            Debug.WriteLine($"Rasterizing {countryData.Count} countries from cached data...");

            // Temp memory layer to hold only the features we want to burn
            using var memDrvOgr = Ogr.GetDriverByName("Memory");
            using var memDs = memDrvOgr.CreateDataSource("temp", new string[0]);
            using var filteredLayer = memDs.CreateLayer("filtered", layer.GetSpatialRef(), layer.GetGeomType(), null);

            // Copy field definitions and add RASTER_CODE
            FeatureDefn layerDefn = layer.GetLayerDefn();
            for (int i = 0; i < layerDefn.GetFieldCount(); i++)
            {
                using var fieldDefn = layerDefn.GetFieldDefn(i);
                filteredLayer.CreateField(fieldDefn, 1);
            }
            using var codeField = new FieldDefn("RASTER_CODE", FieldType.OFTInteger);
            filteredLayer.CreateField(codeField, 1);
            FeatureDefn filteredDefn = filteredLayer.GetLayerDefn();

            // Build lookups (case-insensitive)
            var codeToRaster = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nameToRaster = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in countryData)
            {
                CachedCountryData cc = kvp.Value;
                if (!string.IsNullOrWhiteSpace(cc.CountryCode))
                    codeToRaster[cc.CountryCode.Trim()] = cc.RasterCode;
                if (!string.IsNullOrWhiteSpace(cc.CountryName))
                    nameToRaster[cc.CountryName.Trim()] = cc.RasterCode;
            }
            Debug.WriteLine($"Lookup tables ready: {codeToRaster.Count} codes, {nameToRaster.Count} names");

            // Strict 1950 check
            const double targetYear = 1950.0;
            int added = 0;
            int total = 0;

            layer.ResetReading();
            Feature feature;
            while ((feature = layer.GetNextFeature()) != null)
            {
                try
                {
                    total++;

                    // Read dates — correct field names; skip if bad
                    double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                    double endYear = GetFieldAsDouble(feature, "GWEYEAR");
                    if (startYear <= 0 || endYear <= 0 || endYear < startYear)
                        continue;
                    if (targetYear < startYear || targetYear > endYear)
                        continue;

                    // Read identifiers for matching
                    string countryName = GetFieldAsString(feature, "CNTRY_NAME")?.Trim();
                    string isoOrCode = GetCountryCodeWithFallback(feature, 0)?.Trim();

                    int rasterCode;
                    if (!string.IsNullOrEmpty(isoOrCode) && codeToRaster.TryGetValue(isoOrCode, out rasterCode))
                    {
                        // ok
                    }
                    else if (!string.IsNullOrEmpty(countryName) && nameToRaster.TryGetValue(countryName, out rasterCode))
                    {
                        // ok
                    }
                    else
                    {
                        // No exact match — skip (no partial name heuristics)
                        continue;
                    }

                    using var newFeature = new Feature(filteredDefn);
                    newFeature.SetGeometry(feature.GetGeometryRef());

                    // Copy original fields
                    for (int i = 0; i < layerDefn.GetFieldCount(); i++)
                    {
                        using var fld = layerDefn.GetFieldDefn(i);
                        string fldName = fld.GetName();
                        int idx = feature.GetFieldIndex(fldName);
                        if (idx >= 0 && feature.IsFieldSet(idx))
                        {
                            switch (fld.GetFieldType())
                            {
                                case FieldType.OFTInteger:
                                    newFeature.SetField(fldName, feature.GetFieldAsInteger(idx));
                                    break;
                                case FieldType.OFTReal:
                                    newFeature.SetField(fldName, feature.GetFieldAsDouble(idx));
                                    break;
                                default:
                                    newFeature.SetField(fldName, feature.GetFieldAsString(idx));
                                    break;
                            }
                        }
                    }

                    newFeature.SetField("RASTER_CODE", rasterCode);
                    filteredLayer.CreateFeature(newFeature);
                    added++;
                }
                finally
                {
                    feature.Dispose();
                }
            }

            Debug.WriteLine($"Added {added} features from {total} total for rasterization");

            if (filteredLayer.GetFeatureCount(1) > 0)
            {
                // Burn the RASTER_CODE attribute into the raster
                Gdal.RasterizeLayer(
                    maskDs,
                    1,
                    new[] { 1 },
                    filteredLayer,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    null,
                    new[] { "ATTRIBUTE=RASTER_CODE" },
                    null,
                    "");
            }
        }

        private static double GetFieldAsDouble(Feature feature, string fieldName)
        {
            int idx = feature.GetFieldIndex(fieldName);
            if (idx >= 0 && feature.IsFieldSet(idx))
                return feature.GetFieldAsDouble(idx);
            return -1;
        }

        private static string GetFieldAsString(Feature feature, string fieldName)
        {
            int idx = feature.GetFieldIndex(fieldName);
            if (idx >= 0 && feature.IsFieldSet(idx))
                return feature.GetFieldAsString(idx);
            return null;
        }

        /// <summary>
        /// Attempts to read a stable country identifier from common CShapes fields.
        /// Returns first non-empty among: ISO1AL3, COWCODE, GWCODE, ISO, CNTRY_NAME.
        /// </summary>
        private static string GetCountryCodeWithFallback(Feature feature, int countryCode)
        {
            string[] fields = { "ISO1AL3", "COWCODE", "GWCODE", "ISO", "CNTRY_NAME" };
            foreach (string f in fields)
            {
                string v = GetFieldAsString(feature, f);
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return $"UNK{countryCode:D3}";
        }

        // ---- Color helpers pass-through to cache ----
        public SKColor GetCountryColor(string countryCode)
        {
            var colors = _dataCache.GetAllCountryColors();
            return colors.GetValueOrDefault(countryCode, SKColor.Parse("#808080"));
        }

        public SKColor GetCountryColorByRasterCode(int rasterCode)
        {
            return _dataCache.GetCountryColorByRasterCode(rasterCode);
        }

        public Dictionary<string, SKColor> GetAllCountryColors()
        {
            return _dataCache.GetAllCountryColors();
        }

        /// <summary>
        /// Force-regenerate the 1950 country cache from CShapes.
        /// </summary>
        public void GenerateCountryCacheForced(string cshapesPath)
        {
            _dataCache.ForceRegenerateCountryData(cshapesPath);
        }
    }
}
