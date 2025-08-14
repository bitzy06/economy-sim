using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OSGeo.OGR;

namespace StrategyGame
{
    

    /// <summary>
    /// Cached country feature for spatial indexing
    /// </summary>
    public class IndexedCountryFeature
    {
        public string CountryCode { get; set; } = "";
        public string CountryName { get; set; } = "";
        public int RasterCode { get; set; }
        public GeoBounds Bounds { get; set; }
        public Geometry Geometry { get; set; } = null!;
        public double StartYear { get; set; }
        public double EndYear { get; set; }
    }

    /// <summary>
    /// Spatial index for fast country lookup, built from pre-filtered data.
    /// </summary>
    public class PoliticalSpatialIndex
    {
        private readonly List<IndexedCountryFeature> _countries = new();
        private readonly Dictionary<int, IndexedCountryFeature> _rasterCodeLookup = new();
        private bool _indexBuilt = false;

        /// <summary>
        /// Builds the spatial index using a pre-filtered, clean set of country data.
        /// It no longer performs its own filtering, preventing overlaps.
        /// </summary>
        public void BuildIndex(string cshapesPath, IReadOnlyDictionary<int, CachedCountryData> cleanCountryData)
        {
            if (_indexBuilt) return;

            Debug.WriteLine($"Building spatial index from {cleanCountryData.Count} pre-filtered countries...");
            var sw = Stopwatch.StartNew();

            try
            {
                var featureLookup = cleanCountryData.Values.ToDictionary(c => $"{c.CountryCode}_{c.StartYear}", c => c);

                using var ds = Ogr.Open(cshapesPath, 0) ?? throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");
                using var layer = ds.GetLayerByIndex(0) ?? throw new ApplicationException("No layer found in CShapes file");

                _countries.Clear();
                _rasterCodeLookup.Clear();
                layer.ResetReading();
                Feature feature;
                int featuresAdded = 0;

                while ((feature = layer.GetNextFeature()) != null)
                {
                    try
                    {
                        double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                        CachedCountryData cachedCountry = null;

                        // Attempt to match using the same identifier priority as the cache generator.
                        string identifier = GetBestIdentifier(feature);
                        if (!string.IsNullOrEmpty(identifier))
                        {
                            featureLookup.TryGetValue($"{identifier}_{startYear}", out cachedCountry);
                        }

                        if (cachedCountry != null)
                        {
                            // A match was found! Index this feature.
                            Geometry geom = feature.GetGeometryRef();
                            if (geom == null || geom.IsEmpty()) continue;

                            Envelope envelope = new Envelope();
                            geom.GetEnvelope(envelope);
                            var bounds = new GeoBounds { MinLon = envelope.MinX, MinLat = envelope.MinY, MaxLon = envelope.MaxX, MaxLat = envelope.MaxY };

                            var indexedFeature = new IndexedCountryFeature
                            {
                                CountryCode = cachedCountry.CountryCode,
                                CountryName = cachedCountry.CountryName,
                                RasterCode = cachedCountry.RasterCode,
                                StartYear = cachedCountry.StartYear,
                                EndYear = cachedCountry.EndYear,
                                Bounds = bounds,
                                Geometry = geom.Clone(),
                            };

                            _countries.Add(indexedFeature);
                            _rasterCodeLookup[indexedFeature.RasterCode] = indexedFeature;
                            featuresAdded++;
                        }
                    }
                    finally
                    {
                        feature.Dispose();
                    }
                }

                _indexBuilt = true;
                Debug.WriteLine($"Spatial index built in {sw.ElapsedMilliseconds}ms: {featuresAdded} features indexed.");
                if (featuresAdded == 0 && cleanCountryData.Any())
                {
                    Debug.WriteLine("WARNING: No features were added to the spatial index. Check for identifier mismatches between cache and shapefile.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error building spatial index from clean data: {ex.Message}");
                throw;
            }
        }

        public List<IndexedCountryFeature> GetCountriesInBounds(GeoBounds bounds)
        {
            var result = new List<IndexedCountryFeature>();
            foreach (var country in _countries)
            {
                if (country.Bounds.Intersects(bounds))
                {
                    result.Add(country);
                }
            }
            return result;
        }

        public IndexedCountryFeature? GetCountryByRasterCode(int rasterCode)
        {
            return _rasterCodeLookup.GetValueOrDefault(rasterCode);
        }

        public IReadOnlyList<IndexedCountryFeature> GetAllCountries()
        {
            return _countries.AsReadOnly();
        }

        public int CountryCount => _countries.Count;

        /// <summary>
        /// Gets the best available identifier for a feature, prioritizing standard codes.
        /// </summary>
        private string GetBestIdentifier(Feature feature)
        {
            string id = GetFieldAsString(feature, "ISO1AL3")?.Trim();
            if (!string.IsNullOrEmpty(id)) return id;

            id = GetFieldAsString(feature, "COWCODE")?.Trim();
            if (!string.IsNullOrEmpty(id)) return id;

            return GetFieldAsString(feature, "CNTRY_NAME")?.Trim();
        }

        private double GetFieldAsDouble(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex)) return feature.GetFieldAsDouble(fieldIndex);
            return -1;
        }

        private string? GetFieldAsString(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex)) return feature.GetFieldAsString(fieldIndex);
            return null;
        }

        public void Dispose()
        {
            foreach (var country in _countries)
            {
                country.Geometry?.Dispose();
            }
            _countries.Clear();
            _rasterCodeLookup.Clear();
            _indexBuilt = false;
        }
    }
}