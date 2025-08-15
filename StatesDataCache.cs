using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using OSGeo.OGR;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Cached state data for rendering and lookup operations
    /// </summary>
    public class CachedStateData
    {
        public string CountryCode { get; set; } = "";
        public string StateName { get; set; } = "";
        public int RasterCode { get; set; }
        public string ColorHex { get; set; } = "";
        public GeoBounds Bounds { get; set; }
    }

    /// <summary>
    /// Manages cached state/province data to prevent repeated processing and improve performance
    /// </summary>
    public class StatesDataCache
    {
        private readonly string _colorMappingPath;
        private readonly Dictionary<int, CachedStateData> _rasterCodeToState = new();
        private readonly Dictionary<string, SKColor> _stateColors = new();
        private readonly Dictionary<string, List<CachedStateData>> _countryToStates = new();
        private bool _cacheLoaded = false;
        private int _nextRasterCode = 1;

        public StatesDataCache(string colorMappingPath = "data/country_borders/states_colors.json")
        {
            _colorMappingPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                colorMappingPath);
        }

        /// <summary>
        /// Gets cached state data or generates it if not available
        /// </summary>
        public Dictionary<int, CachedStateData> GetOrGenerateStatesData(string statesShapefilePath, string? countryFilter = null)
        {
            if (!_cacheLoaded)
            {
                LoadStatesData(statesShapefilePath, countryFilter);
                _cacheLoaded = true;
            }

            // Filter by country if specified
            if (!string.IsNullOrEmpty(countryFilter))
            {
                return _rasterCodeToState.Values
                    .Where(s => s.CountryCode.Equals(countryFilter, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(s => s.RasterCode, s => s);
            }

            return _rasterCodeToState;
        }

        private void LoadStatesData(string statesShapefilePath, string? countryFilter)
        {
            Debug.WriteLine($"Loading states data from {statesShapefilePath}");
            
            if (!File.Exists(statesShapefilePath))
            {
                Debug.WriteLine("States shapefile not found, creating empty cache");
                return;
            }

            try
            {
                // Load existing color mapping if available
                LoadExistingColors();

                using var ds = Ogr.Open(statesShapefilePath, 0);
                if (ds == null)
                {
                    Debug.WriteLine("Failed to open states shapefile");
                    return;
                }

                var layer = ds.GetLayerByIndex(0);
                if (layer == null)
                {
                    Debug.WriteLine("No layer found in states shapefile");
                    return;
                }

                _rasterCodeToState.Clear();
                _countryToStates.Clear();

                layer.ResetReading();
                Feature feature;
                int processedFeatures = 0;

                while ((feature = layer.GetNextFeature()) != null)
                {
                    try
                    {
                        // Get state information from Natural Earth fields
                        string countryCode = GetFieldAsString(feature, "ISO_A2") ?? 
                                           GetFieldAsString(feature, "ADM0_A3") ?? "";
                        string stateName = GetFieldAsString(feature, "NAME") ?? 
                                         GetFieldAsString(feature, "ADM1_NAME") ?? 
                                         GetFieldAsString(feature, "NAME_EN") ?? "";

                        // Skip if we don't have essential data
                        if (string.IsNullOrEmpty(countryCode) || string.IsNullOrEmpty(stateName))
                        {
                            continue;
                        }

                        // Apply country filter if specified
                        if (!string.IsNullOrEmpty(countryFilter) && 
                            !countryCode.Equals(countryFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Get geometry bounds
                        var geometry = feature.GetGeometryRef();
                        if (geometry == null) continue;

                        Envelope envelope = new Envelope();
                        geometry.GetEnvelope(envelope);
                        var bounds = new GeoBounds
                        {
                            MinLon = envelope.MinX,
                            MinLat = envelope.MinY,
                            MaxLon = envelope.MaxX,
                            MaxLat = envelope.MaxY
                        };

                        // Create state data
                        var stateData = new CachedStateData
                        {
                            CountryCode = countryCode,
                            StateName = stateName,
                            RasterCode = _nextRasterCode++,
                            Bounds = bounds,
                            ColorHex = GenerateStateColor(countryCode, stateName)
                        };

                        _rasterCodeToState[stateData.RasterCode] = stateData;

                        // Add to country grouping
                        if (!_countryToStates.ContainsKey(countryCode))
                        {
                            _countryToStates[countryCode] = new List<CachedStateData>();
                        }
                        _countryToStates[countryCode].Add(stateData);

                        processedFeatures++;
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

                Debug.WriteLine($"Loaded {processedFeatures} states from shapefile");

                // Save color mapping
                SaveStateColors();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading states data: {ex.Message}");
            }
        }

        private void LoadExistingColors()
        {
            try
            {
                if (File.Exists(_colorMappingPath))
                {
                    var json = File.ReadAllText(_colorMappingPath);
                    var colorDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    if (colorDict != null)
                    {
                        foreach (var kvp in colorDict)
                        {
                            if (SKColor.TryParse(kvp.Value, out var color))
                            {
                                _stateColors[kvp.Key] = color;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading existing state colors: {ex.Message}");
            }
        }

        private string GenerateStateColor(string countryCode, string stateName)
        {
            var stateKey = $"{countryCode}_{stateName}";
            
            if (_stateColors.ContainsKey(stateKey))
            {
                return _stateColors[stateKey].ToString();
            }

            // Generate color based on country and state name hash for consistency
            var combinedHash = $"{countryCode}_{stateName}".GetHashCode();
            var random = new Random(combinedHash);
            
            // Generate softer, more appealing colors for states
            byte r = (byte)(120 + random.Next(136)); // 120-255
            byte g = (byte)(120 + random.Next(136));
            byte b = (byte)(120 + random.Next(136));
            
            var color = new SKColor(r, g, b);
            _stateColors[stateKey] = color;
            
            return color.ToString();
        }

        private void SaveStateColors()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_colorMappingPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var colorDict = _stateColors.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.ToString());

                var json = JsonSerializer.Serialize(colorDict, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(_colorMappingPath, json);
                Debug.WriteLine($"Saved {colorDict.Count} state colors to {_colorMappingPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving state colors: {ex.Message}");
            }
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
            if (_rasterCodeToState.TryGetValue(rasterCode, out var stateData))
            {
                var stateKey = $"{stateData.CountryCode}_{stateData.StateName}";
                if (_stateColors.TryGetValue(stateKey, out var color))
                {
                    return color;
                }
            }
            
            // Return default color if not found
            return new SKColor(128, 128, 128);
        }

        public Dictionary<string, SKColor> GetAllStateColors()
        {
            return new Dictionary<string, SKColor>(_stateColors);
        }

        public CachedStateData? GetStateByRasterCode(int rasterCode)
        {
            _rasterCodeToState.TryGetValue(rasterCode, out var stateData);
            return stateData;
        }

        public List<CachedStateData> GetStatesForCountry(string countryCode)
        {
            _countryToStates.TryGetValue(countryCode, out var states);
            return states ?? new List<CachedStateData>();
        }
    }
}