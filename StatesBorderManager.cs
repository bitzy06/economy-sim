using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OSGeo.GDAL;
using OSGeo.OGR;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Manages state/province borders using Natural Earth ne_10m_admin_1_states_provinces data
    /// Integrated with the existing political borders system
    /// </summary>
    public class StatesBorderManager
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalRegistered = false;

        private readonly StatesDataCache _dataCache;
        private readonly string _colorMappingPath;
        private readonly string _statesShapefilePath;

        public StatesBorderManager(string colorMappingPath = "data/country_borders/states_colors.json", 
                                   string statesPath = "data/country_borders/states")
        {
            _colorMappingPath = colorMappingPath;
            _statesShapefilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                statesPath,
                "ne_10m_admin_1_states_provinces.shp");
                
            EnsureGdalRegistered();
            
            // Initialize cache for states data
            _dataCache = new StatesDataCache(_colorMappingPath);
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
        /// Creates state mask for rendering, filtered by country if specified
        /// </summary>
        public int[,] CreateStatesMask(int width, int height, string? countryFilter = null)
        {
            if (!File.Exists(_statesShapefilePath))
            {
                Debug.WriteLine($"States shapefile not found at: {_statesShapefilePath}");
                return new int[height, width]; // Return empty mask
            }

            try
            {
                var statesData = _dataCache.GetOrGenerateStatesData(_statesShapefilePath, countryFilter);
                return RasterizeStates(statesData, width, height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating states mask: {ex.Message}");
                return new int[height, width];
            }
        }

        private int[,] RasterizeStates(Dictionary<int, CachedStateData> statesData, int width, int height)
        {
            int[,] mask = new int[height, width];
            
            using var ds = Ogr.Open(_statesShapefilePath, 0);
            if (ds == null) return mask;

            var layer = ds.GetLayerByIndex(0);
            if (layer == null) return mask;

            // Create memory raster for rasterization
            var memDrv = Gdal.GetDriverByName("MEM");
            using var rasterDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
            
            // Set geotransform for world coordinates
            double[] geoTransform = new double[6];
            geoTransform[0] = -180.0; // Top left X
            geoTransform[1] = 360.0 / width; // W-E pixel resolution
            geoTransform[2] = 0; // Rotation
            geoTransform[3] = 90.0; // Top left Y  
            geoTransform[4] = 0; // Rotation
            geoTransform[5] = -180.0 / height; // N-S pixel resolution

            rasterDs.SetGeoTransform(geoTransform);

            // Create filtered layer with only the states we want
            var memDrvOgr = Ogr.GetDriverByName("Memory");
            using var memDs = memDrvOgr.CreateDataSource("temp", new string[0]);
            using var filteredLayer = memDs.CreateLayer("filtered", layer.GetSpatialRef(), layer.GetGeomType(), null);

            // Copy field definitions
            var layerDefn = layer.GetLayerDefn();
            for (int i = 0; i < layerDefn.GetFieldCount(); i++)
            {
                var fieldDefn = layerDefn.GetFieldDefn(i);
                filteredLayer.CreateField(fieldDefn, 1);
            }

            // Add raster code field
            var rasterCodeField = new FieldDefn("RASTER_CODE", FieldType.OFTInteger);
            filteredLayer.CreateField(rasterCodeField, 1);

            // Add features with raster codes
            layer.ResetReading();
            Feature feature;
            while ((feature = layer.GetNextFeature()) != null)
            {
                try
                {
                    string countryCode = GetFieldAsString(feature, "ISO_A2") ?? 
                                       GetFieldAsString(feature, "ADM0_A3") ?? "";
                    string stateName = GetFieldAsString(feature, "NAME") ?? 
                                     GetFieldAsString(feature, "ADM1_NAME") ?? "";

                    // Find matching cached state data
                    var stateKey = $"{countryCode}_{stateName}";
                    var matchingState = statesData.Values.FirstOrDefault(s => 
                        s.CountryCode == countryCode && s.StateName == stateName);

                    if (matchingState != null)
                    {
                        var newFeature = new Feature(filteredLayer.GetLayerDefn());
                        newFeature.SetGeometry(feature.GetGeometryRef());
                        
                        // Copy all existing fields
                        for (int i = 0; i < layerDefn.GetFieldCount(); i++)
                        {
                            var fieldDefn = layerDefn.GetFieldDefn(i);
                            newFeature.SetField(fieldDefn.GetName(), feature.GetFieldAsString(i));
                        }
                        
                        // Set raster code
                        newFeature.SetField("RASTER_CODE", matchingState.RasterCode);
                        filteredLayer.CreateFeature(newFeature);
                        newFeature.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing state feature: {ex.Message}");
                }
                finally
                {
                    feature.Dispose();
                }
            }

            // Rasterize the filtered layer
            var band = rasterDs.GetRasterBand(1);
            string[] options = { "ATTRIBUTE=RASTER_CODE" };
            Gdal.RasterizeLayer(rasterDs, 1, new[] { 1 }, filteredLayer, IntPtr.Zero, IntPtr.Zero, 
                0, null, options, null, "");

            // Read raster data into mask array
            int[] buffer = new int[width * height];
            band.ReadRaster(0, 0, width, height, buffer, width, height, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask[y, x] = buffer[y * width + x];
                }
            }

            return mask;
        }

        private string? GetFieldAsString(Feature feature, string fieldName)
        {
            try
            {
                int fieldIndex = feature.GetFieldIndex(fieldName);
                return fieldIndex >= 0 ? feature.GetFieldAsString(fieldIndex) : null;
            }
            catch
            {
                return null;
            }
        }

        public SKColor GetStateColorByRasterCode(int rasterCode)
        {
            return _dataCache.GetStateColorByRasterCode(rasterCode);
        }

        public Dictionary<string, SKColor> GetAllStateColors()
        {
            return _dataCache.GetAllStateColors();
        }

        public CachedStateData? GetStateByRasterCode(int rasterCode)
        {
            return _dataCache.GetStateByRasterCode(rasterCode);
        }

        /// <summary>
        /// Gets states for a specific country
        /// </summary>
        public List<CachedStateData> GetStatesForCountry(string countryCode)
        {
            return _dataCache.GetStatesForCountry(countryCode);
        }
    }
}