using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OSGeo.OGR;

namespace Economy_sim
{
    /// <summary>
    /// Cached state feature for spatial indexing
    /// </summary>
    public class IndexedStateFeature
    {
        public string CountryCode { get; set; } = "";
        public string StateName { get; set; } = "";
        public int RasterCode { get; set; }
        public GeoBounds Bounds { get; set; }
        public Geometry Geometry { get; set; } = null!;
    }

    /// <summary>
    /// Spatial index for fast state lookup by geographic bounds and country
    /// </summary>
    public class StatesSpatialIndex
    {
        private readonly List<IndexedStateFeature> _states = new();
        private readonly Dictionary<int, IndexedStateFeature> _rasterCodeLookup = new();
        private readonly Dictionary<string, List<IndexedStateFeature>> _countryToStates = new();
        private bool _indexBuilt = false;

        /// <summary>
        /// Builds spatial index from states data for fast lookups
        /// </summary>
        public void BuildIndex(string statesShapefilePath, IReadOnlyDictionary<int, CachedStateData> statesData)
        {
            if (_indexBuilt) return;

            Debug.WriteLine($"Building states spatial index from {statesData.Count} states...");
            var sw = Stopwatch.StartNew();

            try
            {
                using var ds = Ogr.Open(statesShapefilePath, 0);
                if (ds == null)
                {
                    Debug.WriteLine("Failed to open states shapefile for spatial indexing");
                    return;
                }

                var layer = ds.GetLayerByIndex(0);
                if (layer == null)
                {
                    Debug.WriteLine("No layer found in states shapefile");
                    return;
                }

                _states.Clear();
                _rasterCodeLookup.Clear();
                _countryToStates.Clear();

                layer.ResetReading();
                Feature feature;
                int indexedFeatures = 0;

                while ((feature = layer.GetNextFeature()) != null)
                {
                    try
                    {
                        string countryCode = GetFieldAsString(feature, "ISO_A2") ?? 
                                           GetFieldAsString(feature, "ADM0_A3") ?? "";
                        string stateName = GetFieldAsString(feature, "NAME") ?? 
                                         GetFieldAsString(feature, "ADM1_NAME") ?? "";

                        if (string.IsNullOrEmpty(countryCode) || string.IsNullOrEmpty(stateName))
                            continue;

                        // Find matching cached state data
                        var matchingState = statesData.Values.FirstOrDefault(s => 
                            s.CountryCode == countryCode && s.StateName == stateName);

                        if (matchingState == null) continue;

                        var geometry = feature.GetGeometryRef();
                        if (geometry == null) continue;

                        // Get bounds
                        Envelope envelope = new Envelope();
                        geometry.GetEnvelope(envelope);
                        var bounds = new GeoBounds
                        {
                            MinLon = envelope.MinX,
                            MinLat = envelope.MinY,
                            MaxLon = envelope.MaxX,
                            MaxLat = envelope.MaxY
                        };

                        // Create indexed feature
                        var indexedFeature = new IndexedStateFeature
                        {
                            CountryCode = countryCode,
                            StateName = stateName,
                            RasterCode = matchingState.RasterCode,
                            Bounds = bounds,
                            Geometry = geometry.Clone() // Clone for thread safety
                        };

                        _states.Add(indexedFeature);
                        _rasterCodeLookup[indexedFeature.RasterCode] = indexedFeature;

                        // Add to country grouping
                        if (!_countryToStates.ContainsKey(countryCode))
                        {
                            _countryToStates[countryCode] = new List<IndexedStateFeature>();
                        }
                        _countryToStates[countryCode].Add(indexedFeature);

                        indexedFeatures++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error indexing state feature: {ex.Message}");
                    }
                    finally
                    {
                        feature.Dispose();
                    }
                }

                _indexBuilt = true;
                sw.Stop();
                Debug.WriteLine($"States spatial index built: {indexedFeatures} features in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error building states spatial index: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets state by raster code
        /// </summary>
        public IndexedStateFeature? GetStateByRasterCode(int rasterCode)
        {
            _rasterCodeLookup.TryGetValue(rasterCode, out var state);
            return state;
        }

        /// <summary>
        /// Gets all states for a specific country
        /// </summary>
        public List<IndexedStateFeature> GetStatesForCountry(string countryCode)
        {
            _countryToStates.TryGetValue(countryCode, out var states);
            return states ?? new List<IndexedStateFeature>();
        }

        /// <summary>
        /// Finds states that intersect with the given geographic bounds
        /// </summary>
        public List<IndexedStateFeature> GetStatesInBounds(GeoBounds bounds, string? countryFilter = null)
        {
            var result = new List<IndexedStateFeature>();

            foreach (var state in _states)
            {
                // Apply country filter if specified
                if (!string.IsNullOrEmpty(countryFilter) && 
                    !state.CountryCode.Equals(countryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Check if bounds intersect
                if (BoundsIntersect(bounds, state.Bounds))
                {
                    result.Add(state);
                }
            }

            return result;
        }

        private bool BoundsIntersect(GeoBounds a, GeoBounds b)
        {
            return !(a.MaxLon < b.MinLon || a.MinLon > b.MaxLon ||
                     a.MaxLat < b.MinLat || a.MinLat > b.MaxLat);
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

        public bool IsIndexBuilt => _indexBuilt;
        public int StateCount => _states.Count;
    }
}