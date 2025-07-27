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
    /// Manages political borders using CShapes-2.0.shp data with temporal filtering
    /// </summary>
    public class PoliticalBorderManager
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalRegistered = false;
        
        private readonly Dictionary<string, SKColor> _countryColors = new();
        private readonly string _colorMappingPath;
        private readonly Random _random = new();
        
        public PoliticalBorderManager(string colorMappingPath = "data/country_borders/country_colors.json")
        {
            _colorMappingPath = colorMappingPath;
            EnsureGdalRegistered();
            LoadOrCreateColorMapping();
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
        /// Creates a political border mask for the specified date from CShapes data
        /// </summary>
        /// <param name="cshapesPath">Path to CShapes-2.0.shp file</param>
        /// <param name="targetDate">Target date (will find closest available)</param>
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
                
                // Filter features by date and rasterize
                FilterAndRasterizeByDate(layer, maskDs, targetDate);
                
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
        
        private void FilterAndRasterizeByDate(Layer layer, Dataset maskDs, DateTime targetDate)
        {
            // CShapes uses decimal years (e.g., 1950.0 for Jan 1950)
            double targetYear = targetDate.Year + (targetDate.DayOfYear - 1) / (DateTime.IsLeapYear(targetDate.Year) ? 366.0 : 365.0);
            
            Debug.WriteLine($"Filtering CShapes data for target year: {targetYear:F3}");
            
            // Create a memory layer for filtered features
            OSGeo.OGR.Driver memDrvOgr = Ogr.GetDriverByName("Memory");
            DataSource memDs = memDrvOgr.CreateDataSource("temp", new string[0]);
            Layer filteredLayer = memDs.CreateLayer("filtered", layer.GetSpatialRef(), layer.GetGeomType(), null);
            
            // Copy field definitions
            FeatureDefn layerDefn = layer.GetLayerDefn();
            for (int i = 0; i < layerDefn.GetFieldCount(); i++)
            {
                FieldDefn fieldDefn = layerDefn.GetFieldDefn(i);
                filteredLayer.CreateField(fieldDefn, 1);
            }
            
            // Add a field for the country code we'll use for rasterization
            FieldDefn codeField = new FieldDefn("RASTER_CODE", FieldType.OFTInteger);
            filteredLayer.CreateField(codeField, 1);
            
            FeatureDefn filteredDefn = filteredLayer.GetLayerDefn();
            
            // Filter features by date and add to filtered layer
            layer.ResetReading();
            Feature feature;
            int countryCode = 1; // Start from 1 (0 is typically nodata)
            int totalFeatures = 0;
            int validFeatures = 0;
            
            while ((feature = layer.GetNextFeature()) != null)
            {
                totalFeatures++;
                
                // Get start and end dates - be more flexible with missing data
                double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                double endYear = GetFieldAsDouble(feature, "GWEYER");
                
                // Handle missing or invalid dates more gracefully
                if (startYear <= 0)
                {
                    startYear = 1945; // Default start year if missing
                }
                if (endYear <= 0 || endYear < startYear)
                {
                    endYear = 2020; // Default end year if missing or invalid
                }
                
                // Check if target date falls within the validity period
                // Make the filter more inclusive for countries around 1950
                if (targetYear >= startYear && targetYear <= endYear)
                {
                    validFeatures++;
                    
                    // Create new feature for filtered layer
                    Feature newFeature = new Feature(filteredDefn);
                    
                    // Copy geometry
                    Geometry geom = feature.GetGeometryRef();
                    newFeature.SetGeometry(geom);
                    
                    // Copy fields
                    for (int i = 0; i < layerDefn.GetFieldCount(); i++)
                    {
                        FieldDefn fieldDefn = layerDefn.GetFieldDefn(i);
                        string fieldName = fieldDefn.GetName();
                        
                        if (feature.IsFieldSet(i))
                        {
                            switch (fieldDefn.GetFieldType())
                            {
                                case FieldType.OFTString:
                                    newFeature.SetField(fieldName, feature.GetFieldAsString(i));
                                    break;
                                case FieldType.OFTInteger:
                                    newFeature.SetField(fieldName, feature.GetFieldAsInteger(i));
                                    break;
                                case FieldType.OFTReal:
                                    newFeature.SetField(fieldName, feature.GetFieldAsDouble(i));
                                    break;
                            }
                        }
                    }
                    
                    // Set raster code
                    newFeature.SetField("RASTER_CODE", countryCode);
                    
                    // Store country info for color mapping - ensure every country gets a unique color
                    string countryName = GetFieldAsString(feature, "CNTRY_NAME") ?? $"Country_{countryCode}";
                    string iso3Code = GetCountryCodeWithFallback(feature, countryCode);
                    
                    // Ensure this country has a color assigned
                    if (!_countryColors.ContainsKey(iso3Code))
                    {
                        _countryColors[iso3Code] = GenerateDistinctColor(countryCode);
                    }
                    
                    Debug.WriteLine($"Added country: {countryName} ({iso3Code}) with code {countryCode}, valid {startYear}-{endYear}");
                    
                    filteredLayer.CreateFeature(newFeature);
                    countryCode++;
                    
                    newFeature.Dispose();
                }
                else
                {
                    Debug.WriteLine($"Filtered out country: {GetFieldAsString(feature, "CNTRY_NAME") ?? "Unknown"}, " +
                                  $"target: {targetYear:F1}, valid: {startYear:F1}-{endYear:F1}");
                }
                
                feature.Dispose();
            }
            
            Debug.WriteLine($"Date filtering results: {validFeatures}/{totalFeatures} features included for year {targetYear:F1}");
            
            // Rasterize the filtered layer
            if (filteredLayer.GetFeatureCount(1) > 0)
            {
                Debug.WriteLine($"Rasterizing {filteredLayer.GetFeatureCount(1)} countries...");
                Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, filteredLayer, IntPtr.Zero, IntPtr.Zero,
                    0, null, new[] { "ATTRIBUTE=RASTER_CODE" }, null, "");
            }
            else
            {
                Debug.WriteLine("WARNING: No countries found for the target date after filtering!");
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
        
        private SKColor GenerateRandomColor()
        {
            // Generate a reasonably bright, distinguishable color
            byte r = (byte)_random.Next(80, 255);
            byte g = (byte)_random.Next(80, 255);
            byte b = (byte)_random.Next(80, 255);
            return new SKColor(r, g, b, 255);
        }
        
        private SKColor GenerateDistinctColor(int index)
        {
            // Generate more distinct colors using HSV color space for better distribution
            float hue = (index * 137.508f) % 360f; // Golden angle for good distribution
            float saturation = 0.7f + (index % 3) * 0.1f; // Vary saturation slightly
            float value = 0.8f + (index % 2) * 0.2f; // Vary brightness slightly
            
            return HSVToRGB(hue, saturation, value);
        }
        
        private SKColor HSVToRGB(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
            float m = v - c;
            
            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            
            return new SKColor(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255),
                255);
        }
        
        private void LoadOrCreateColorMapping()
        {
            if (File.Exists(_colorMappingPath))
            {
                try
                {
                    string json = File.ReadAllText(_colorMappingPath);
                    var colorData = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (colorData != null)
                    {
                        foreach (var kvp in colorData)
                        {
                            if (SKColor.TryParse(kvp.Value, out SKColor color))
                            {
                                _countryColors[kvp.Key] = color;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading color mapping: {ex.Message}");
                }
            }
        }
        
        public void SaveColorMapping()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(_colorMappingPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Convert SKColor to hex strings for JSON serialization
                var colorData = new Dictionary<string, string>();
                foreach (var kvp in _countryColors)
                {
                    colorData[kvp.Key] = $"#{kvp.Value.Red:X2}{kvp.Value.Green:X2}{kvp.Value.Blue:X2}";
                }
                
                string json = JsonSerializer.Serialize(colorData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_colorMappingPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving color mapping: {ex.Message}");
            }
        }
        
        public SKColor GetCountryColor(string countryCode)
        {
            return _countryColors.GetValueOrDefault(countryCode, SKColor.Parse("#808080")); // Gray default
        }
        
        public Dictionary<string, SKColor> GetAllCountryColors()
        {
            return new Dictionary<string, SKColor>(_countryColors);
        }
        
        /// <summary>
        /// Renders political borders as a colored bitmap
        /// </summary>
        /// <param name="mask">Political mask array</param>
        /// <param name="width">Bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <returns>Rendered political map bitmap</returns>
        public SKBitmap RenderPoliticalMap(int[,] mask, int width, int height)
        {
            var bitmap = new SKBitmap(width, height);
            var canvas = new SKCanvas(bitmap);
            
            // Clear to transparent
            canvas.Clear(SKColors.Transparent);
            
            // Create color lookup for country codes
            var codeToColor = new Dictionary<int, SKColor>();
            int code = 1;
            foreach (var kvp in _countryColors)
            {
                codeToColor[code] = kvp.Value;
                code++;
            }
            
            // Render pixels
            for (int y = 0; y < height && y < mask.GetLength(0); y++)
            {
                for (int x = 0; x < width && x < mask.GetLength(1); x++)
                {
                    int countryCode = mask[y, x];
                    if (countryCode > 0 && codeToColor.TryGetValue(countryCode, out SKColor color))
                    {
                        bitmap.SetPixel(x, y, color);
                    }
                }
            }
            
            canvas.Dispose();
            return bitmap;
        }
    }
}