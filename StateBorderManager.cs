using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OSGeo.GDAL;
using OSGeo.OGR;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Manages state/province borders using Natural Earth admin-1 data
    /// </summary>
    public class StateBorderManager
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalRegistered = false;

        private readonly string _stateDataPath;
        private readonly string _colorMappingPath;
        private List<StateFeature> _stateFeatures = new List<StateFeature>();
        private bool _dataLoaded = false;

        public class StateFeature
        {
            public string StateName { get; set; } = "";
            public string StateCode { get; set; } = "";
            public string CountryName { get; set; } = "";
            public string CountryCode { get; set; } = "";
            public SKColor Color { get; set; } = SKColors.Gray;
            public List<SKPath> Geometry { get; set; } = new List<SKPath>();
            public int RasterCode { get; set; }
        }

        public StateBorderManager(string stateDataPath = "data/country_borders/states/ne_10m_admin_1_states_provinces.shp", 
                                 string colorMappingPath = "data/country_borders/state_colors.json")
        {
            _stateDataPath = stateDataPath;
            _colorMappingPath = colorMappingPath;
            EnsureGdalRegistered();
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

        public void LoadStateData()
        {
            if (_dataLoaded) return;

            try
            {
                // For now, create mock state data since we don't have the actual shapefile
                // In a real implementation, this would load from the shapefile
                CreateMockStateData();
                _dataLoaded = true;
                Debug.WriteLine($"[STATE MANAGER] Loaded {_stateFeatures.Count} state features");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE MANAGER ERROR] Failed to load state data: {ex.Message}");
                CreateMockStateData(); // Fallback to mock data
                _dataLoaded = true;
            }
        }

        private void CreateMockStateData()
        {
            // Create some mock state data for testing
            var random = new Random(42); // Fixed seed for consistent colors
            var mockStates = new[]
            {
                new { Country = "USA", CountryCode = "US", States = new[] { "California", "Texas", "New York", "Florida", "Illinois" } },
                new { Country = "Canada", CountryCode = "CA", States = new[] { "Ontario", "Quebec", "British Columbia", "Alberta", "Manitoba" } },
                new { Country = "Germany", CountryCode = "DE", States = new[] { "Bavaria", "Baden-Württemberg", "North Rhine-Westphalia", "Hesse", "Saxony" } },
                new { Country = "Australia", CountryCode = "AU", States = new[] { "New South Wales", "Victoria", "Queensland", "Western Australia", "South Australia" } }
            };

            int rasterCode = 1;
            foreach (var country in mockStates)
            {
                foreach (var stateName in country.States)
                {
                    var state = new StateFeature
                    {
                        StateName = stateName,
                        StateCode = stateName.Substring(0, Math.Min(2, stateName.Length)).ToUpper(),
                        CountryName = country.Country,
                        CountryCode = country.CountryCode,
                        Color = new SKColor((byte)random.Next(50, 255), (byte)random.Next(50, 255), (byte)random.Next(50, 255)),
                        RasterCode = rasterCode++
                    };
                    _stateFeatures.Add(state);
                }
            }
        }

        public List<StateFeature> GetStatesForCountry(string countryCode)
        {
            if (!_dataLoaded) LoadStateData();
            return _stateFeatures.FindAll(s => s.CountryCode.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
        }

        public List<StateFeature> GetAllStates()
        {
            if (!_dataLoaded) LoadStateData();
            return new List<StateFeature>(_stateFeatures);
        }

        public StateFeature? GetStateByName(string stateName)
        {
            if (!_dataLoaded) LoadStateData();
            return _stateFeatures.Find(s => s.StateName.Equals(stateName, StringComparison.OrdinalIgnoreCase));
        }

        public StateFeature? GetStateByCode(string stateCode)
        {
            if (!_dataLoaded) LoadStateData();
            return _stateFeatures.Find(s => s.StateCode.Equals(stateCode, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Renders state borders on the given canvas
        /// </summary>
        public void RenderStateBorders(SKCanvas canvas, SKRect viewport, float borderWidth = 1.0f, SKColor? borderColor = null)
        {
            if (!_dataLoaded) LoadStateData();

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth,
                Color = borderColor ?? SKColors.Black,
                IsAntialias = true
            };

            foreach (var state in _stateFeatures)
            {
                foreach (var path in state.Geometry)
                {
                    canvas.DrawPath(path, paint);
                }
            }

            paint.Dispose();
        }

        /// <summary>
        /// Renders state fills on the given canvas
        /// </summary>
        public void RenderStateFills(SKCanvas canvas, SKRect viewport)
        {
            if (!_dataLoaded) LoadStateData();

            var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            foreach (var state in _stateFeatures)
            {
                paint.Color = state.Color;
                foreach (var path in state.Geometry)
                {
                    canvas.DrawPath(path, paint);
                }
            }

            paint.Dispose();
        }

        public void Dispose()
        {
            foreach (var state in _stateFeatures)
            {
                foreach (var path in state.Geometry)
                {
                    path?.Dispose();
                }
            }
            _stateFeatures.Clear();
        }
    }
}