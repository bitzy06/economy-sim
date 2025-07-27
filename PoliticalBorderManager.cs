using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using OSGeo.GDAL;
using OSGeo.OGR;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Manages political borders using CShapes-2.0.shp data with temporal filtering and caching
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
            
            // Initialize cache for 1950 data
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
        /// Creates a political border mask for the specified date from CShapes data using cached country data
        /// </summary>
        /// <param name="cshapesPath">Path to CShapes-2.0.shp file</param>
        /// <param name="targetDate">Target date (will use cached 1950 data)</param>
        /// <param name="width">Output width</param>
        /// <param name="height">Output height</param>
        /// <param name="bounds">Geographic bounds [minX, minY, maxX, maxY]</param>
        /// <returns>Political border mask with country codes</returns>
        public int[,] CreatePoliticalMask(string cshapesPath, DateTime targetDate, int width, int height, double[] bounds = null)
        {
            lock (GdalLock)
            {
                // Default to global bounds if not specified
                bounds ??= new[] { -180.0, -90.0, 180.0, 90.0 };
                
                // Get cached country data (this will load from cache or generate once)
                var countryData = _dataCache.GetOrGenerateCountryData(cshapesPath);
                
                Debug.WriteLine($"Using cached country data: {countryData.Count} countries for 1950");
                
                // Open the CShapes shapefile
                DataSource ds = Ogr.Open(cshapesPath, 0);
                if (ds == null)
                    throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");
                
                Layer layer = ds.GetLayerByIndex(0);
                if (layer == null)
                    throw new ApplicationException("No layer found in CShapes file");
                
                // Create in-memory raster
                OSGeo.GDAL.Driver memDrv = Gdal.GetDriverByName("MEM");
                Dataset maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
                
                // Set geotransform for the specified bounds
                double[] geoTransform = new double[6];
                geoTransform[0] = bounds[0]; // Top-left X
                geoTransform[1] = (bounds[2] - bounds[0]) / width; // Pixel width
                geoTransform[2] = 0; // Rotation (usually 0)
                geoTransform[3] = bounds[3]; // Top-left Y
                geoTransform[4] = 0; // Rotation (usually 0)
                geoTransform[5] = -(bounds[3] - bounds[1]) / height; // Pixel height (negative for north-up)
                
                maskDs.SetGeoTransform(geoTransform);
                maskDs.SetProjection("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]");
                
                // Filter and rasterize using cached data
                FilterAndRasterizeUsingCache(layer, maskDs, countryData);
                
                // Read the result
                Band band = maskDs.GetRasterBand(1);
                int[] flat = new int[width * height];
                band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);
                
                // Convert to 2D array
                int[,] result = new int[height, width];
                for (int r = 0; r < height; r++)
                {
                    for (int c = 0; c < width; c++)
                    {
                        result[r, c] = flat[r * width + c];
                    }
                }
                
                // Clean up
                maskDs.Dispose();
                ds.Dispose();
                
                return result;
            }
        }
        
        private void FilterAndRasterizeUsingCache(Layer layer, Dataset maskDs, Dictionary<int, CachedCountryData> countryData)
        {
            Debug.WriteLine($"Rasterizing {countryData.Count} countries from cached data...");
            
            // Create a memory layer for filtered features
            OSGeo.OGR.Driver memDrvOgr = Ogr.GetDriverByName("Memory");
            DataSource memDs = memDrvOgr.CreateDataSource("temp", new string[0]);
            Layer filteredLayer = memDs.CreateLayer("filtered", layer.GetSpatialRef(), layer.GetGeomType(), null);
            
            // Copy field definitions and add raster code field
            FeatureDefn layerDefn = layer.GetLayerDefn();
            for (int i = 0; i < layerDefn.GetFieldCount(); i++)
            {
                FieldDefn fieldDefn = layerDefn.GetFieldDefn(i);
                filteredLayer.CreateField(fieldDefn, 1);
            }
            
            FieldDefn codeField = new FieldDefn("RASTER_CODE", FieldType.OFTInteger);
            filteredLayer.CreateField(codeField, 1);
            
            FeatureDefn filteredDefn = filteredLayer.GetLayerDefn();
            
            // Filter features using cached country data
            layer.ResetReading();
            Feature feature;
            int addedFeatures = 0;
            
            while ((feature = layer.GetNextFeature()) != null)
            {
                // Get country information for matching with cached data
                string countryName = GetFieldAsString(feature, "CNTRY_NAME");
                string iso3Code = GetCountryCodeWithFallback(feature, 0);
                double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                double endYear = GetFieldAsDouble(feature, "GWEYER");
                
                // Find matching cached country data
                foreach (var kvp in countryData)
                {
                    var cachedCountry = kvp.Value;
                    
                    // Match by country code or name, and verify year range matches cached data
                    bool isMatch = (cachedCountry.CountryCode == iso3Code || 
                                   cachedCountry.CountryName == countryName) &&
                                   Math.Abs(cachedCountry.StartYear - startYear) < 1.0 &&
                                   Math.Abs(cachedCountry.EndYear - endYear) < 1.0;
                    
                    if (isMatch)
                    {
                        // Create new feature for filtered layer
                        Feature newFeature = new Feature(filteredDefn);
                        
                        // Copy geometry
                        Geometry geom = feature.GetGeometryRef();
                        newFeature.SetGeometry(geom);
                        
                        // Set raster code from cached data
                        newFeature.SetField("RASTER_CODE", cachedCountry.RasterCode);
                        
                        filteredLayer.CreateFeature(newFeature);
                        addedFeatures++;
                        
                        newFeature.Dispose();
                        break; // Found match, no need to continue searching
                    }
                }
                
                feature.Dispose();
            }
            
            Debug.WriteLine($"Added {addedFeatures} features from cached data for rasterization");
            
            // Rasterize the filtered layer
            if (filteredLayer.GetFeatureCount(1) > 0)
            {
                Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, filteredLayer, IntPtr.Zero, IntPtr.Zero,
                    0, null, new[] { "ATTRIBUTE=RASTER_CODE" }, null, "");
            }
            else
            {
                Debug.WriteLine("WARNING: No features found for rasterization from cached data!");
            }
            
            // Clean up
            memDs.Dispose();
        }
        
        private double GetFieldAsDouble(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsDouble(fieldIndex);
            }
            return -1; // Default for missing fields
        }
        
        private string GetFieldAsString(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsString(fieldIndex);
            }
            return null; // Default for missing fields
        }
        
        private string GetCountryCodeWithFallback(Feature feature, int countryCode)
        {
            // Try different possible field names for country code in order of preference
            string[] possibleFields = { "ISO1AL3", "COWCODE", "GWCODE", "ISO", "CNTRY_NAME" };
            
            foreach (string fieldName in possibleFields)
            {
                string value = GetFieldAsString(feature, fieldName);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            
            // If no field is found, generate a fallback code
            return $"UNK{countryCode:D3}";
        }
        
        public SKColor GetCountryColor(string countryCode)
        {
            var colors = _dataCache.GetAllCountryColors();
            return colors.GetValueOrDefault(countryCode, SKColor.Parse("#808080")); // Gray default
        }
        
        public SKColor GetCountryColorByRasterCode(int rasterCode)
        {
            return _dataCache.GetCountryColorByRasterCode(rasterCode);
        }
        
        public Dictionary<string, SKColor> GetAllCountryColors()
        {
            return _dataCache.GetAllCountryColors();
        }
    }
}