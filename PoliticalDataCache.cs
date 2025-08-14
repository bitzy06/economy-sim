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
    /// Cached country data for a specific year to prevent repeated filtering operations.
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
    /// Manages cached political data to prevent repeated filtering and improve performance.
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
        /// Gets cached country data or generates it if not available.
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
        /// Gets the color for a country by its raster code.
        /// </summary>
        public SKColor GetCountryColorByRasterCode(int rasterCode)
        {
            if (_rasterCodeToCountry.TryGetValue(rasterCode, out var countryData) &&
                !string.IsNullOrEmpty(countryData.CountryCode) &&
                _countryColors.TryGetValue(countryData.CountryCode, out var color))
            {
                return color;
            }
            return new SKColor(128, 128, 128, 255); // A default grey color.
        }

        /// <summary>
        /// Gets all country colors.
        /// </summary>
        public Dictionary<string, SKColor> GetAllCountryColors()
        {
            return new Dictionary<string, SKColor>(_countryColors);
        }

        /// <summary>
        /// Forces the regeneration of country data, ignoring any existing cache.
        /// </summary>
        public void ForceRegenerateCountryData(string cshapesPath)
        {
            Debug.WriteLine($"Force regenerating country data for the year {_targetDate.Year}...");
            GenerateCountryData(cshapesPath);
            SaveToCache();
            _cacheLoaded = true;
        }

        private void LoadFromCacheOrGenerate(string cshapesPath)
        {
            Debug.WriteLine($"Attempting to load cache from: {_cacheFilePath}");

            if (LoadFromCache())
            {
                Debug.WriteLine($"Loaded {_rasterCodeToCountry.Count} countries from cache for the year {_targetDate.Year}");
                return;
            }

            Debug.WriteLine($"Cache not found or invalid. Generating fresh country data for the year {_targetDate.Year}...");
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
                    Debug.WriteLine("Cache file does not exist.");
                    return false;
                }

                Debug.WriteLine("Cache file found. Attempting to load...");
                string json = File.ReadAllText(_cacheFilePath);
                var cachedData = JsonSerializer.Deserialize<List<CachedCountryData>>(json);

                if (cachedData == null || !cachedData.Any())
                {
                    Debug.WriteLine("Cache file is empty or invalid.");
                    return false;
                }

                _rasterCodeToCountry.Clear();
                _countryColors.Clear();

                foreach (var country in cachedData)
                {
                    _rasterCodeToCountry[country.RasterCode] = country;

                    if (SKColor.TryParse(country.ColorHex, out SKColor color))
                    {
                        // Use the standard code from the cache as the key
                        _countryColors[country.CountryCode] = color;
                    }
                    else
                    {
                        _countryColors[country.CountryCode] = GenerateSimpleColor(country.RasterCode);
                    }
                }

                Debug.WriteLine($"Successfully loaded {cachedData.Count} countries from cache.");
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
                double targetYear = _targetDate.Year + (_targetDate.DayOfYear - 1) /
                    (DateTime.IsLeapYear(_targetDate.Year) ? 366.0 : 365.0);

                Debug.WriteLine($"Processing CShapes data for target year: {targetYear:F2}");

                using var ds = Ogr.Open(cshapesPath, 0) ?? throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");
                using var layer = ds.GetLayerByIndex(0) ?? throw new ApplicationException("No layer found in the CShapes file.");

                _rasterCodeToCountry.Clear();
                _countryColors.Clear();

                var featuresPerCountry = new Dictionary<string, List<(Feature feature, double startYear, double endYear)>>();

                layer.ResetReading();
                Feature feature;

                // First pass: Group features by a reliable identifier
                while ((feature = layer.GetNextFeature()) != null)
                {
                    double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                    double endYear = GetFieldAsDouble(feature, "GWEYEAR");

                    if (startYear <= 0 || endYear <= 0 || startYear > endYear)
                    {
                        feature.Dispose();
                        continue;
                    }

                    if (targetYear >= startYear && targetYear < endYear)
                    {
                        // Use a consistent method to get the best possible identifier for grouping.
                        string groupIdentifier = GetBestIdentifier(feature);
                        if (string.IsNullOrEmpty(groupIdentifier))
                        {
                            feature.Dispose();
                            continue; // Skip if no usable identifier
                        }

                        if (!featuresPerCountry.ContainsKey(groupIdentifier))
                        {
                            featuresPerCountry[groupIdentifier] = new List<(Feature, double, double)>();
                        }
                        featuresPerCountry[groupIdentifier].Add((feature.Clone(), startYear, endYear));
                    }
                    feature.Dispose();
                }

                Debug.WriteLine($"Found {featuresPerCountry.Count} unique country groups for the target year.");

                // Second pass: Resolve duplicates and create final country data
                int rasterCode = 1;
                foreach (var kvp in featuresPerCountry)
                {
                    var countryFeatures = kvp.Value;
                    var bestFeatureTuple = countryFeatures.OrderByDescending(f => f.startYear).First();
                    var selectedFeature = bestFeatureTuple.feature;

                    // Prioritize standard codes (ISO/COW) over names for the cache entry.
                    string countryName = GetFieldAsString(selectedFeature, "CNTRY_NAME")?.Trim() ?? kvp.Key;
                    string finalCode = GetBestIdentifier(selectedFeature);

                    var cachedCountry = new CachedCountryData
                    {
                        CountryCode = finalCode, // <-- Ensures a standard code is stored
                        CountryName = countryName,
                        RasterCode = rasterCode,
                        StartYear = bestFeatureTuple.startYear,
                        EndYear = bestFeatureTuple.endYear
                    };

                    var color = GenerateSimpleColor(rasterCode);
                    cachedCountry.ColorHex = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

                    _rasterCodeToCountry[rasterCode] = cachedCountry;
                    _countryColors[finalCode] = color;

                    rasterCode++;

                    foreach (var f in countryFeatures) f.feature.Dispose();
                }

                Debug.WriteLine($"Country data generation complete: {_rasterCodeToCountry.Count} unique countries processed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while generating country data: {ex.Message}");
                throw;
            }
        }

        private void SaveToCache()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Debug.WriteLine($"Creating cache directory: {directory}");
                    Directory.CreateDirectory(directory);
                }

                var cacheData = new List<CachedCountryData>(_rasterCodeToCountry.Values);
                string json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(_cacheFilePath, json);
                Debug.WriteLine($"Saved {cacheData.Count} countries to cache: {_cacheFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving cache to {_cacheFilePath}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Gets the best available identifier for a feature, prioritizing standard codes.
        /// </summary>
        private string GetBestIdentifier(Feature feature)
        {
            string id = GetFieldAsString(feature, "ISO1AL3")?.Trim();
            if (!string.IsNullOrEmpty(id)) return id;

            id = GetFieldAsString(feature, "COWCODE")?.Trim();
            if (!string.IsNullOrEmpty(id)) return id;

            // Fallback to name ONLY if no standard code is available.
            return GetFieldAsString(feature, "CNTRY_NAME")?.Trim();
        }

        private double GetFieldAsDouble(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex != -1 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsDouble(fieldIndex);
            }
            return -1;
        }

        private string? GetFieldAsString(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex != -1 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsString(fieldIndex);
            }
            return null;
        }

        private SKColor GenerateSimpleColor(int index)
        {
            byte r = (byte)(100 + (index * 67) % 156);
            byte g = (byte)(100 + (index * 113) % 156);
            byte b = (byte)(100 + (index * 151) % 156);
            return new SKColor(r, g, b, 255);
        }
    }
}