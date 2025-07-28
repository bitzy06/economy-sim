using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using OSGeo.OGR;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Cached country data for 1950 to prevent repeated filtering operations
    /// </summary>
    public class CachedCountryData
    {
        public string CountryCode { get; set; } = "";
        public string CountryName { get; set; } = "";
        public int RasterCode { get; set; }
        public string ColorHex { get; set; } = "";
        public double StartYear { get; set; }
        public double EndYear { get; set; }
    }

    /// <summary>
    /// Manages cached political data to prevent repeated filtering and improve performance
    /// </summary>
    public class PoliticalDataCache
    {
        private readonly string _cacheFilePath;
        private readonly DateTime _targetDate;
        private readonly Dictionary<int, CachedCountryData> _rasterCodeToCountry = new();
        private readonly Dictionary<string, SKColor> _countryColors = new();
        private bool _cacheLoaded = false;

        public PoliticalDataCache(DateTime targetDate, string cacheDirectory = "data/country_borders")
        {
            _targetDate = targetDate;
            _cacheFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                cacheDirectory, 
                $"country_cache_{targetDate.Year}.json");
        }

        /// <summary>
        /// Gets cached country data or generates it if not available
        /// </summary>
        public Dictionary<int, CachedCountryData> GetOrGenerateCountryData(string cshapesPath)
        {
            if (!_cacheLoaded)
            {
                LoadFromCacheOrGenerate(cshapesPath);
                _cacheLoaded = true;
            }
            return _rasterCodeToCountry;
        }

        /// <summary>
        /// Gets color for a country by raster code
        /// </summary>
        public SKColor GetCountryColorByRasterCode(int rasterCode)
        {
            if (_rasterCodeToCountry.TryGetValue(rasterCode, out var countryData) &&
                _countryColors.TryGetValue(countryData.CountryCode, out var color))
            {
                return color;
            }
            return new SKColor(128, 128, 128, 255); // Grey default
        }

        /// <summary>
        /// Gets all country colors
        /// </summary>
        public Dictionary<string, SKColor> GetAllCountryColors()
        {
            return new Dictionary<string, SKColor>(_countryColors);
        }

        private void LoadFromCacheOrGenerate(string cshapesPath)
        {
            Debug.WriteLine($"Attempting to load cache from: {_cacheFilePath}");
            
            // Try to load from cache first
            if (LoadFromCache())
            {
                Debug.WriteLine($"Loaded {_rasterCodeToCountry.Count} countries from cache for year {_targetDate.Year}");
                return;
            }

            // Generate fresh data
            Debug.WriteLine($"Cache not found or invalid, generating fresh country data for year {_targetDate.Year}...");
            GenerateCountryData(cshapesPath);
            SaveToCache();
        }

        private bool LoadFromCache()
        {
            try
            {
                Debug.WriteLine($"Checking for cache file at: {_cacheFilePath}");
                
                if (!File.Exists(_cacheFilePath))
                {
                    Debug.WriteLine("Cache file does not exist");
                    return false;
                }

                Debug.WriteLine("Cache file found, attempting to load...");
                string json = File.ReadAllText(_cacheFilePath);
                var cachedData = JsonSerializer.Deserialize<List<CachedCountryData>>(json);

                if (cachedData == null || cachedData.Count == 0)
                {
                    Debug.WriteLine("Cache file is empty or invalid");
                    return false;
                }

                _rasterCodeToCountry.Clear();
                _countryColors.Clear();

                foreach (var country in cachedData)
                {
                    _rasterCodeToCountry[country.RasterCode] = country;
                    
                    if (SKColor.TryParse(country.ColorHex, out SKColor color))
                    {
                        _countryColors[country.CountryCode] = color;
                    }
                    else
                    {
                        // Generate fallback color
                        _countryColors[country.CountryCode] = GenerateSimpleColor(country.RasterCode);
                    }
                }

                Debug.WriteLine($"Successfully loaded {cachedData.Count} countries from cache");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading cache from {_cacheFilePath}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private void GenerateCountryData(string cshapesPath)
        {
            try
            {
                // CShapes uses decimal years (e.g., 1950.0 for Jan 1950)
                double targetYear = _targetDate.Year + (_targetDate.DayOfYear - 1) / 
                    (DateTime.IsLeapYear(_targetDate.Year) ? 366.0 : 365.0);

                // Open the CShapes shapefile
                DataSource ds = Ogr.Open(cshapesPath, 0);
                if (ds == null)
                    throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");

                Layer layer = ds.GetLayerByIndex(0);
                if (layer == null)
                    throw new ApplicationException("No layer found in CShapes file");

                _rasterCodeToCountry.Clear();
                _countryColors.Clear();

                layer.ResetReading();
                Feature feature;
                int countryCode = 1; // Start from 1 (0 is typically nodata)
                int totalFeatures = 0;
                int validFeatures = 0;

                while ((feature = layer.GetNextFeature()) != null)
                {
                    totalFeatures++;

                    // Get start and end dates
                    double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                    double endYear = GetFieldAsDouble(feature, "GWEYER");

                    // Handle missing dates - use reasonable defaults for 1950
                    if (startYear <= 0 || startYear > 2020) startYear = 1900;
                    if (endYear <= 0 || endYear < startYear) endYear = 2000;

                    // Strict filtering for 1950 only (allow small tolerance for data precision)
                    if (targetYear >= startYear - 0.5 && targetYear <= endYear + 0.5)
                    {
                        validFeatures++;

                        string countryName = GetFieldAsString(feature, "CNTRY_NAME") ?? $"Country_{countryCode}";
                        string iso3Code = GetCountryCodeWithFallback(feature, countryCode);

                        var cachedCountry = new CachedCountryData
                        {
                            CountryCode = iso3Code,
                            CountryName = countryName,
                            RasterCode = countryCode,
                            StartYear = startYear,
                            EndYear = endYear
                        };

                        // Generate consistent color
                        var color = GenerateSimpleColor(countryCode);
                        cachedCountry.ColorHex = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

                        _rasterCodeToCountry[countryCode] = cachedCountry;
                        _countryColors[iso3Code] = color;

                        countryCode++;
                    }

                    feature.Dispose();
                }

                Debug.WriteLine($"Country data generation complete: {validFeatures}/{totalFeatures} features included for year {targetYear:F1}");

                ds.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating country data: {ex.Message}");
                throw;
            }
        }

        private void SaveToCache()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    if (!Directory.Exists(directory))
                    {
                        Debug.WriteLine($"Creating cache directory: {directory}");
                        Directory.CreateDirectory(directory);
                    }
                }

                var cacheData = new List<CachedCountryData>(_rasterCodeToCountry.Values);
                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(_cacheFilePath, json);
                Debug.WriteLine($"Saved {cacheData.Count} countries to cache: {_cacheFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving cache to {_cacheFilePath}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
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

        private SKColor GenerateSimpleColor(int index)
        {
            // Deterministic color generation based on index for consistency
            byte r = (byte)(100 + (index * 67) % 156);
            byte g = (byte)(100 + (index * 113) % 156);
            byte b = (byte)(100 + (index * 151) % 156);
            return new SKColor(r, g, b, 255);
        }
    }
}