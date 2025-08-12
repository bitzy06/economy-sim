using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Forces regeneration of country data, ignoring existing cache
        /// </summary>
        public void ForceRegenerateCountryData(string cshapesPath)
        {
            Debug.WriteLine($"Force regenerating country data for year {_targetDate.Year}...");
            GenerateCountryData(cshapesPath);
            SaveToCache();
            _cacheLoaded = true;
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

                Debug.WriteLine($"Processing CShapes data for target year: {targetYear:F1}");

                // Open the CShapes shapefile
                DataSource ds = Ogr.Open(cshapesPath, 0);
                if (ds == null)
                    throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");

                Layer layer = ds.GetLayerByIndex(0);
                if (layer == null)
                    throw new ApplicationException("No layer found in CShapes file");

                _rasterCodeToCountry.Clear();
                _countryColors.Clear();

                // Use sets to track unique countries and prevent duplicates
                var uniqueCountryCodes = new HashSet<string>();
                var uniqueCountryNames = new HashSet<string>();
                var featuresPerCountry = new Dictionary<string, List<(Feature feature, double startYear, double endYear)>>();

                // First pass: collect all features and group by country
                layer.ResetReading();
                Feature feature;
                int totalFeatures = 0;
                int featuresWithValidDates = 0;
                int featuresInTimeRange = 0;

                Debug.WriteLine("First pass: Collecting and validating features...");
                
                // Debug: Log available fields in the first feature
                Feature firstFeature = layer.GetNextFeature();
                if (firstFeature != null)
                {
                    FeatureDefn featureDefn = firstFeature.GetDefnRef();
                    Debug.WriteLine("Available fields in CShapes dataset:");
                    for (int i = 0; i < featureDefn.GetFieldCount(); i++)
                    {
                        FieldDefn fieldDefn = featureDefn.GetFieldDefn(i);
                        Debug.WriteLine($"  {i}: {fieldDefn.GetName()} ({fieldDefn.GetTypeName()})");
                    }
                    firstFeature.Dispose();
                }
                layer.ResetReading();

                while ((feature = layer.GetNextFeature()) != null)
                {
                    totalFeatures++;

                    // Get start and end dates with permissive handling
                    double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                    double endYear = GetFieldAsDouble(feature, "GWEYER");

                    // Handle missing or invalid dates with reasonable defaults for 1950 processing
                    if (startYear <= 0 || startYear > 2020) startYear = 1900;
                    if (endYear <= 0 || endYear < startYear) endYear = 2000;
                    
                    // Now validate the processed dates
                    if (startYear > endYear)
                    {
                        Debug.WriteLine($"Skipping feature with inconsistent dates: start={startYear}, end={endYear}");
                        feature.Dispose();
                        continue;
                    }

                    featuresWithValidDates++;

                    // Check if this feature is valid for 1950 (strict temporal filtering)
                    if (targetYear >= startYear && targetYear <= endYear)
                    {
                        featuresInTimeRange++;

                        string countryName = GetFieldAsString(feature, "CNTRY_NAME");
                        string iso3Code = GetCountryCodeWithFallback(feature, 0);

                        // Group features by country code for duplicate resolution
                        string key = !string.IsNullOrEmpty(iso3Code) ? iso3Code : countryName ?? "UNKNOWN";
                        
                        if (!featuresPerCountry.ContainsKey(key))
                        {
                            featuresPerCountry[key] = new List<(Feature, double, double)>();
                        }
                        featuresPerCountry[key].Add((feature, startYear, endYear));
                    }
                    else
                    {
                        feature.Dispose();
                    }
                }

                Debug.WriteLine($"Feature analysis: {totalFeatures} total, {featuresWithValidDates} with valid dates, {featuresInTimeRange} in time range");
                Debug.WriteLine($"Found {featuresPerCountry.Count} unique countries/territories");

                // Second pass: resolve duplicates and create final country data
                int countryCode = 1; // Start from 1 (0 is typically nodata)
                int processedCountries = 0;

                Debug.WriteLine("Second pass: Resolving duplicates and creating country data...");

                foreach (var kvp in featuresPerCountry)
                {
                    string countryKey = kvp.Key;
                    var countryFeatures = kvp.Value;

                    // For countries with multiple features, prefer the one with the most appropriate time range
                    // or the most recent start date within the valid range
                    var bestFeature = countryFeatures
                        .OrderBy(f => Math.Abs(f.startYear - targetYear)) // Prefer features starting closest to target year
                        .ThenBy(f => f.endYear - f.startYear) // Prefer shorter time ranges (more specific)
                        .First();

                    var selectedFeature = bestFeature.feature;
                    
                    string countryName = GetFieldAsString(selectedFeature, "CNTRY_NAME") ?? countryKey;
                    string iso3Code = GetCountryCodeWithFallback(selectedFeature, countryCode);

                    // Ensure unique country codes
                    string uniqueCode = iso3Code;
                    int suffix = 1;
                    while (uniqueCountryCodes.Contains(uniqueCode))
                    {
                        uniqueCode = $"{iso3Code}_{suffix}";
                        suffix++;
                    }
                    uniqueCountryCodes.Add(uniqueCode);
                    
                    // Ensure unique country names
                    string uniqueName = countryName;
                    suffix = 1;
                    while (uniqueCountryNames.Contains(uniqueName))
                    {
                        uniqueName = $"{countryName}_{suffix}";
                        suffix++;
                    }
                    uniqueCountryNames.Add(uniqueName);

                    var cachedCountry = new CachedCountryData
                    {
                        CountryCode = uniqueCode,
                        CountryName = uniqueName,
                        RasterCode = countryCode,
                        StartYear = bestFeature.startYear,
                        EndYear = bestFeature.endYear
                    };

                    // Generate consistent color
                    var color = GenerateSimpleColor(countryCode);
                    cachedCountry.ColorHex = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

                    _rasterCodeToCountry[countryCode] = cachedCountry;
                    _countryColors[uniqueCode] = color;

                    Debug.WriteLine($"Added country {countryCode}: {uniqueName} ({uniqueCode}) - {bestFeature.startYear:F1} to {bestFeature.endYear:F1}");

                    countryCode++;
                    processedCountries++;

                    // Dispose all features for this country
                    foreach (var f in countryFeatures)
                    {
                        f.feature.Dispose();
                    }
                }

                Debug.WriteLine($"Country data generation complete: {processedCountries} unique countries processed for year {targetYear:F1}");

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
                try
                {
                    return feature.GetFieldAsDouble(fieldIndex);
                }
                catch
                {
                    // Try parsing as string if direct double access fails
                    string strValue = feature.GetFieldAsString(fieldIndex);
                    if (double.TryParse(strValue, out double result))
                    {
                        return result;
                    }
                }
            }
            
            // Try alternative field names for dates
            if (fieldName == "GWSYEAR")
            {
                string[] alternatives = { "STARTDATE", "START_YEAR", "STYEAR", "GWSDATE" };
                foreach (string alt in alternatives)
                {
                    fieldIndex = feature.GetFieldIndex(alt);
                    if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
                    {
                        try
                        {
                            return feature.GetFieldAsDouble(fieldIndex);
                        }
                        catch
                        {
                            string strValue = feature.GetFieldAsString(fieldIndex);
                            if (double.TryParse(strValue, out double result))
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            
            if (fieldName == "GWEYER")
            {
                string[] alternatives = { "ENDDATE", "END_YEAR", "ENYEAR", "GWEDATE" };
                foreach (string alt in alternatives)
                {
                    fieldIndex = feature.GetFieldIndex(alt);
                    if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
                    {
                        try
                        {
                            return feature.GetFieldAsDouble(fieldIndex);
                        }
                        catch
                        {
                            string strValue = feature.GetFieldAsString(fieldIndex);
                            if (double.TryParse(strValue, out double result))
                            {
                                return result;
                            }
                        }
                    }
                }
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