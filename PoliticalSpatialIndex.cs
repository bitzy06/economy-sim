using System;
using System.Collections.Generic;
using System.Diagnostics;
using OSGeo.OGR;

namespace StrategyGame
{
    /// <summary>
    /// Extension methods for existing GeoBounds struct
    /// </summary>
    public static class GeoBoundsExtensions
    {
        public static bool Intersects(this GeoBounds bounds, GeoBounds other)
        {
            return !(other.MinLon > bounds.MaxLon || other.MaxLon < bounds.MinLon ||
                     other.MinLat > bounds.MaxLat || other.MaxLat < bounds.MinLat);
        }
    }

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
    /// Spatial index for fast country lookup by geographic bounds
    /// </summary>
    public class PoliticalSpatialIndex
    {
        private readonly List<IndexedCountryFeature> _countries = new();
        private readonly Dictionary<int, IndexedCountryFeature> _rasterCodeLookup = new();
        private bool _indexBuilt = false;

        /// <summary>
        /// Builds spatial index from CShapes data for the target year
        /// </summary>
        public void BuildIndex(string cshapesPath, DateTime targetDate)
        {
            if (_indexBuilt) return;

            Debug.WriteLine($"Building spatial index for year {targetDate.Year}...");
            var sw = Stopwatch.StartNew();

            try
            {
                double targetYear = targetDate.Year + (targetDate.DayOfYear - 1) / 
                    (DateTime.IsLeapYear(targetDate.Year) ? 366.0 : 365.0);

                DataSource ds = Ogr.Open(cshapesPath, 0);
                if (ds == null)
                    throw new ApplicationException($"Failed to open CShapes file: {cshapesPath}");

                Layer layer = ds.GetLayerByIndex(0);
                if (layer == null)
                    throw new ApplicationException("No layer found in CShapes file");

                _countries.Clear();
                _rasterCodeLookup.Clear();

                layer.ResetReading();
                Feature feature;
                int rasterCode = 1;
                int totalFeatures = 0;
                int indexedFeatures = 0;

                while ((feature = layer.GetNextFeature()) != null)
                {
                    totalFeatures++;

                    try
                    {
                        // Get temporal information
                        double startYear = GetFieldAsDouble(feature, "GWSYEAR");
                        double endYear = GetFieldAsDouble(feature, "GWEYER");

                        // Handle missing dates
                        if (startYear <= 0 || startYear > 2020) startYear = 1900;
                        if (endYear <= 0 || endYear < startYear) endYear = 2000;

                        // Filter for target year with tolerance
                        if (targetYear < startYear - 0.5 || targetYear > endYear + 0.5)
                        {
                            feature.Dispose();
                            continue;
                        }

                        // Get geometry and calculate bounds
                        Geometry geom = feature.GetGeometryRef();
                        if (geom == null || geom.IsEmpty())
                        {
                            feature.Dispose();
                            continue;
                        }

                        // Calculate bounding box
                        Envelope envelope = new Envelope();
                        geom.GetEnvelope(envelope);
                        var bounds = new GeoBounds
                        {
                            MinLon = envelope.MinX,
                            MinLat = envelope.MinY,
                            MaxLon = envelope.MaxX,
                            MaxLat = envelope.MaxY
                        };

                        // Get country information
                        string countryName = GetFieldAsString(feature, "CNTRY_NAME") ?? $"Country_{rasterCode}";
                        string countryCode = GetCountryCodeWithFallback(feature, rasterCode);

                        // Create indexed feature with cloned geometry for thread safety
                        var indexedFeature = new IndexedCountryFeature
                        {
                            CountryCode = countryCode,
                            CountryName = countryName,
                            RasterCode = rasterCode,
                            Bounds = bounds,
                            Geometry = geom.Clone(), // Clone for thread safety
                            StartYear = startYear,
                            EndYear = endYear
                        };

                        _countries.Add(indexedFeature);
                        _rasterCodeLookup[rasterCode] = indexedFeature;
                        
                        rasterCode++;
                        indexedFeatures++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing feature {totalFeatures}: {ex.Message}");
                    }
                    finally
                    {
                        feature.Dispose();
                    }
                }

                ds.Dispose();
                _indexBuilt = true;

                Debug.WriteLine($"Spatial index built in {sw.ElapsedMilliseconds}ms: {indexedFeatures}/{totalFeatures} countries indexed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error building spatial index: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets countries that intersect with the specified geographic bounds
        /// </summary>
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

        /// <summary>
        /// Gets country by raster code
        /// </summary>
        public IndexedCountryFeature? GetCountryByRasterCode(int rasterCode)
        {
            return _rasterCodeLookup.GetValueOrDefault(rasterCode);
        }

        /// <summary>
        /// Gets all indexed countries
        /// </summary>
        public IReadOnlyList<IndexedCountryFeature> GetAllCountries()
        {
            return _countries.AsReadOnly();
        }

        /// <summary>
        /// Gets the total number of indexed countries
        /// </summary>
        public int CountryCount => _countries.Count;

        private double GetFieldAsDouble(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsDouble(fieldIndex);
            }
            return -1;
        }

        private string GetFieldAsString(Feature feature, string fieldName)
        {
            int fieldIndex = feature.GetFieldIndex(fieldName);
            if (fieldIndex >= 0 && feature.IsFieldSet(fieldIndex))
            {
                return feature.GetFieldAsString(fieldIndex);
            }
            return null;
        }

        private string GetCountryCodeWithFallback(Feature feature, int rasterCode)
        {
            string[] possibleFields = { "ISO1AL3", "COWCODE", "GWCODE", "ISO", "CNTRY_NAME" };

            foreach (string fieldName in possibleFields)
            {
                string value = GetFieldAsString(feature, fieldName);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return $"UNK{rasterCode:D3}";
        }

        /// <summary>
        /// Releases resources held by the spatial index
        /// </summary>
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