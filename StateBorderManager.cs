using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Manages state/province borders using Natural Earth admin-1 data
    /// </summary>
    public class StateBorderManager : IDisposable
    {
        private static readonly object GdalLock = new object();
        private static bool _gdalRegistered = false;

        // When true, skip any further attempts to open the states shapefile
        private static volatile bool _disableShapefileLoading = false;
        private static int _shpLoadAttempts = 0;
        private static readonly bool _optInEnableShp =
            string.Equals(Environment.GetEnvironmentVariable("ES_ENABLE_STATE_SHP"), "1", StringComparison.Ordinal);

        // Base map size used by the grid/political systems (configurable)
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        public int BaseWidth => _baseWidth; // logical political pixel width (unscaled reference)
        public int BaseHeight => _baseHeight; // logical political pixel height (unscaled reference)

        // Scaled grid dimensions to reduce memory footprint
        private readonly int _gridScaleFactor; // power-of-two scale divisor (1,2,4,...)
        private readonly int _gridWidth;  // = BaseWidth / _gridScaleFactor
        private readonly int _gridHeight; // = BaseHeight / _gridScaleFactor
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;

        private readonly string _stateDataPath;
        private readonly string _colorMappingPath;
        private readonly List<StateFeature> _stateFeatures = new();
        private bool _dataLoaded = false;

        // Grid-based state rendering
        private volatile bool _stateGridBuilt = false;
        private int[,]? _stateGrid; // [y, x] -> raster code
        private readonly Dictionary<int, SKColor> _stateColorsByCode = new();

        private int _selectedStateCode = -1;

        public class StateFeature
        {
            public string StateName { get; set; } = string.Empty;
            public string StateCode { get; set; } = string.Empty;
            public string CountryName { get; set; } = string.Empty;
            public string CountryCode { get; set; } = string.Empty;
            public SKColor Color { get; set; } = SKColors.Gray;
            public List<SKPath> Geometry { get; set; } = new();
            public SKRect Bounds { get; set; }
            public int RasterCode { get; set; }
        }

        public StateBorderManager(int baseWidth = 4096, int baseHeight = 2048,
                                  string stateDataPath = "data/country_borders/states/ne_10m_admin_1_states_provinces.shp",
                                  string colorMappingPath = "data/country_borders/state_colors.json")
        {
            _baseWidth = Math.Max(1, baseWidth);
            _baseHeight = Math.Max(1, baseHeight);

            // Determine adaptive scale factor to keep allocated pixel count reasonable (< ~40M)
            _gridScaleFactor = 1;
            long maxPixels = 40_000_000; // ~160MB at 4 bytes each
            while ((long)(_baseWidth / _gridScaleFactor) * (_baseHeight / _gridScaleFactor) > maxPixels)
            {
                _gridScaleFactor *= 2;
            }
            _gridWidth = Math.Max(1, _baseWidth / _gridScaleFactor);
            _gridHeight = Math.Max(1, _baseHeight / _gridScaleFactor);

            if (_gridScaleFactor > 1)
            {
                Debug.WriteLine($"[STATE MANAGER] Applying downscale factor x{_gridScaleFactor} for state grid => {_gridWidth}x{_gridHeight} (from {_baseWidth}x{_baseHeight})");
            }

            _stateDataPath = stateDataPath;
            _colorMappingPath = colorMappingPath;
            EnsureGdalRegistered();
            
            // Reset the disable flag for new instances unless opt-in is disabled
            // This allows each new instance to attempt loading the shapefile
            if (!_optInEnableShp && _disableShapefileLoading)
            {
                Debug.WriteLine("[STATE MANAGER] Resetting shapefile disable flag for new instance");
                _disableShapefileLoading = false;
                _shpLoadAttempts = 0;
            }
        }

        /// <summary>
        /// Reset the static loading state to allow fresh attempts at loading shapefiles
        /// </summary>
        public static void ResetLoadingState()
        {
            _disableShapefileLoading = false;
            _shpLoadAttempts = 0;
            Debug.WriteLine("[STATE MANAGER] Loading state reset");
        }

        private void EnsureGdalRegistered()
        {
            lock (GdalLock)
            {
                if (_gdalRegistered) return;

                try { GdalBase.ConfigureAll(); } catch (Exception ex) { Debug.WriteLine($"[STATE MANAGER] MaxRev.Gdal.Core configure failed: {ex.Message}"); }
                try { Gdal.DontUseExceptions(); } catch { }
                try { Ogr.DontUseExceptions(); } catch { }
                try { Gdal.PushErrorHandler("CPLQuietErrorHandler"); } catch { }
                try { Gdal.AllRegister(); } catch (Exception ex) { Debug.WriteLine($"[STATE MANAGER] Gdal.AllRegister failed: {ex.Message}"); }
                try { Ogr.RegisterAll(); } catch (Exception ex) { Debug.WriteLine($"[STATE MANAGER] Ogr.RegisterAll failed: {ex.Message}"); }
                try { Gdal.SetConfigOption("SHAPE_ENCODING", ""); } catch { }

                _gdalRegistered = true;
            }
        }

        private string? FindStatesShapefile()
        {
            // User Documents preferred location
            string userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "country_borders", "states");
            string primary = Path.Combine(userDir, "ne_10m_admin_1_states_provinces.shp");
            if (ValidateShapefileSet(primary)) return primary;

            // Project relative locations
            string[] candidates = new[]
            {
                _stateDataPath,
                "data/country_borders/states/ne_10m_admin_1_states_provinces.shp",
                "data/ne_10m_admin_1_states_provinces.shp",
                Path.Combine(Environment.CurrentDirectory, "data", "country_borders", "states", "ne_10m_admin_1_states_provinces.shp")
            };
            foreach (var c in candidates)
            {
                if (ValidateShapefileSet(c)) return c;
            }
            return null;
        }

        private bool ValidateShapefileSet(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!File.Exists(path)) return false;
            string dir = Path.GetDirectoryName(path)!;
            string baseName = Path.GetFileNameWithoutExtension(path);
            // Require at least DBF and SHX alongside SHP; PRJ is optional
            bool ok = File.Exists(Path.Combine(dir, baseName + ".dbf")) &&
                      File.Exists(Path.Combine(dir, baseName + ".shx"));
            if (!ok)
            {
                Debug.WriteLine($"[STATE MANAGER] Missing sidecar files for shapefile: {path}");
            }
            return ok;
        }

        public void LoadStateData()
        {
            if (_dataLoaded) return;

            try
            {
                // Only use the disable flag if opt-in is not enabled and we've had multiple failures
                if (_disableShapefileLoading && !_optInEnableShp && _shpLoadAttempts > 2)
                {
                    Debug.WriteLine("[STATE MANAGER] State shapefile loading disabled due to multiple previous errors. Using mock data.");
                    CreateMockStateData();
                    _dataLoaded = true;
                    BuildStateGrid();
                    return;
                }

                string? shp = FindStatesShapefile();
                if (!string.IsNullOrEmpty(shp))
                {
                    Debug.WriteLine($"[STATE MANAGER] Found shapefile: {shp}");
                    if (TryLoadFromShapefile(shp))
                    {
                        Debug.WriteLine("[STATE MANAGER] Successfully loaded from shapefile");
                    }
                    else
                    {
                        Debug.WriteLine("[STATE MANAGER] Falling back to mock rectangles due to load error.");
                        CreateMockStateData();
                    }
                }
                else
                {
                    Debug.WriteLine("[STATE MANAGER] State shapefile not found, using mock rectangles.");
                    CreateMockStateData();
                }
                
                _dataLoaded = true;
                Debug.WriteLine($"[STATE MANAGER] Loaded {_stateFeatures.Count} state features");

                // Build grid representation for square rendering
                BuildStateGrid();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE MANAGER ERROR] Failed to load state data: {ex.Message}");
                _stateFeatures.Clear();
                CreateMockStateData();
                _dataLoaded = true;
                BuildStateGrid();
            }
        }

        private bool TryLoadFromShapefile(string shapefilePath)
        {
            _shpLoadAttempts++;
            Debug.WriteLine($"[STATE MANAGER] Loading states from: {shapefilePath} (attempt {_shpLoadAttempts})");

            // Only disable after multiple consecutive failures and without opt-in
            if (_disableShapefileLoading && !_optInEnableShp && _shpLoadAttempts > 2)
            {
                Debug.WriteLine("[STATE MANAGER] Shapefile loading previously disabled after multiple failures. Skipping.");
                return false;
            }

            // Verify driver is available to avoid runtime crashes
            try
            {
                var shpDriver = Ogr.GetDriverByName("ESRI Shapefile");
                if (shpDriver == null)
                {
                    Debug.WriteLine("[STATE MANAGER] OGR 'ESRI Shapefile' driver not available. Skipping shapefile load.");
                    if (_shpLoadAttempts > 2) _disableShapefileLoading = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE MANAGER] Error checking OGR driver: {ex.Message}");
                if (_shpLoadAttempts > 2) _disableShapefileLoading = true;
                return false;
            }

            try
            {
                // Prefer opening via the driver directly
                var driver = Ogr.GetDriverByName("ESRI Shapefile");
                using var ds = driver?.Open(shapefilePath, 0);
                if (ds == null)
                {
                    Debug.WriteLine("[STATE MANAGER] OGR driver.Open returned null for states shapefile");
                    if (_shpLoadAttempts > 2) _disableShapefileLoading = true;
                    return false;
                }

                var layer = ds.GetLayerByIndex(0);
                if (layer == null)
                {
                    Debug.WriteLine("[STATE MANAGER] States layer not found in shapefile");
                    if (_shpLoadAttempts > 2) _disableShapefileLoading = true;
                    return false;
                }

                layer.ResetReading();

                int errorCount = 0;
                const int maxErrors = 50; // fail-fast to avoid crash loops
                int nextRaster = 1;

                Feature? feat;
                while ((feat = layer.GetNextFeature()) != null)
                {
                    try
                    {
                        var geom = feat.GetGeometryRef();
                        if (geom == null) { feat.Dispose(); continue; }

                        string stateName = GetFirstNonEmpty(feat,
                            "name_en", "name", "name_long", "adm1name", "gns_name") ?? string.Empty;
                        string stateCode = GetFirstNonEmpty(feat, "postal", "adm1_code", "iso_3166_2", "sr_adm1") ?? string.Empty;
                        string countryName = GetFirstNonEmpty(feat, "adm0_name", "sr_adm0", "name_0", "name_en_0") ?? string.Empty;
                        string countryCode = GetFirstNonEmpty(feat, "iso_a2", "iso_a3", "adm0_a3", "sr_sov_a3") ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(stateName))
                        {
                            feat.Dispose();
                            continue; // require at least a name
                        }

                        var state = new StateFeature
                        {
                            StateName = stateName,
                            StateCode = string.IsNullOrWhiteSpace(stateCode) ? stateName : stateCode,
                            CountryName = countryName,
                            CountryCode = countryCode,
                            RasterCode = nextRaster,
                            Color = GenerateColor(nextRaster)
                        };
                        nextRaster++;

                        var paths = ConvertGeometryToPaths(geom);
                        if (paths.Count == 0)
                        {
                            state.DisposePaths();
                            feat.Dispose();
                            continue;
                        }

                        state.Geometry.AddRange(paths);
                        SKRect b = SKRect.Empty;
                        foreach (var p in paths)
                        {
                            b = b.IsEmpty ? p.Bounds : SKRect.Union(b, p.Bounds);
                        }
                        state.Bounds = b;

                        _stateFeatures.Add(state);
                    }
                    catch (Exception gex)
                    {
                        errorCount++;
                        Debug.WriteLine($"[STATE MANAGER] Skipping feature due to error: {gex.Message}");
                        if (errorCount > maxErrors)
                        {
                            Debug.WriteLine("[STATE MANAGER] Too many errors while reading states. Aborting shapefile load.");
                            break;
                        }
                    }
                    finally
                    {
                        feat.Dispose();
                    }
                }

                bool ok = _stateFeatures.Count > 0 && errorCount <= maxErrors;
                if (!ok && _shpLoadAttempts > 2)
                {
                    _disableShapefileLoading = true;
                }
                
                // Reset attempt counter on successful load
                if (ok)
                {
                    _shpLoadAttempts = 0;
                    _disableShapefileLoading = false;
                }
                
                return ok;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE MANAGER] Failed to open/read states shapefile: {ex.Message}");
                if (_shpLoadAttempts > 2) _disableShapefileLoading = true;
                return false;
            }
        }

        private static string? GetFirstNonEmpty(Feature f, params string[] fields)
        {
            try
            {
                var defn = f.GetDefnRef();
                foreach (var name in fields)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    int idx = -1;
                    try { idx = f.GetFieldIndex(name); } catch { idx = -1; }
                    if (idx < 0 && defn != null)
                    {
                        try { idx = defn.GetFieldIndex(name); } catch { idx = -1; }
                    }

                    if (idx >= 0)
                    {
                        try
                        {
                            // Skip if field not set (avoids exceptions in some drivers)
                            if (f.IsFieldSet(idx))
                            {
                                var val = f.GetFieldAsString(idx);
                                if (!string.IsNullOrWhiteSpace(val) && !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                                    return val;
                            }
                        }
                        catch { /* ignore per-field errors safely */ }
                    }
                }
            }
            catch { /* ignore feature/defn access errors */ }
            return null;
        }

        private List<SKPath> ConvertGeometryToPaths(Geometry geom)
        {
            var paths = new List<SKPath>();
            var type = geom.GetGeometryType();

            if (type == wkbGeometryType.wkbPolygon || type == wkbGeometryType.wkbPolygon25D)
            {
                AddPolygonPaths(geom, paths);
            }
            else if (type == wkbGeometryType.wkbMultiPolygon || type == wkbGeometryType.wkbMultiPolygon25D)
            {
                int count = geom.GetGeometryCount();
                for (int i = 0; i < count; i++)
                {
                    var g = geom.GetGeometryRef(i); // borrowed; do not dispose
                    if (g != null)
                    {
                        AddPolygonPaths(g, paths);
                    }
                }
            }

            return paths;
        }

        private void AddPolygonPaths(Geometry polygon, List<SKPath> paths)
        {
            var ext = polygon.GetGeometryRef(0); // outer ring
            if (ext == null) return;

            var path = new SKPath
            {
                FillType = SKPathFillType.EvenOdd // ensure holes are treated correctly
            };

            AddRingToPath(ext, path);

            int ringCount = polygon.GetGeometryCount();
            for (int r = 1; r < ringCount; r++)
            {
                var hole = polygon.GetGeometryRef(r);
                if (hole != null)
                {
                    AddRingToPath(hole, path);
                }
            }

            paths.Add(path);
        }

        // Adjust LonLatToGridPixels to produce scaled grid coordinates for rasterization
        private (double x, double y) LonLatToGridPixels(double lon, double lat)
        {
            double x = (lon + 180.0) / 360.0 * _gridWidth;
            double y = (90.0 - lat) / 180.0 * _gridHeight;
            return (x, y);
        }

        // Modify AddRingToPath to draw into scaled grid coordinate space (used only for grid building)
        private void AddRingToPathScaled(Geometry ring, SKPath path)
        {
            int n = ring.GetPointCount();
            if (n < 3) return;

            // Decimate points to reduce path complexity if extremely dense
            int step = Math.Max(1, n / 5000);

            var pts = new List<SKPoint>(Math.Min(n, 5000));

            for (int i = 0; i < n; i += step)
            {
                double x = ring.GetX(i);
                double y = ring.GetY(i);
                var (gx, gy) = LonLatToGridPixels(x, y);
                pts.Add(new SKPoint((float)gx, (float)gy));
             }

            // Ensure the ring is closed
            if (pts.Count >= 3)
                path.AddPoly(pts.ToArray(), true);
        }

        private void AddRingToPath(Geometry ring, SKPath path)
        {
            int n = ring.GetPointCount();
            if (n < 3) return;

            // Decimate points to reduce path complexity if extremely dense
            int step = Math.Max(1, n / 5000);

            var pts = new List<SKPoint>(Math.Min(n, 5000));

            for (int i = 0; i < n; i += step)
            {
                double x = ring.GetX(i);
                double y = ring.GetY(i);
                var (lx, ly) = LonLatToBasePixels(x, y);
                pts.Add(new SKPoint((float)lx, (float)ly));
            }

            // Ensure the ring is closed
            if (pts.Count >= 3)
            {
                // Use array overload to avoid CollectionsMarshal dependency
                path.AddPoly(pts.ToArray(), close: true);
            }
        }

        private void CreateMockStateData()
        {
            int nextRaster = 1;
            var mockStates = new[]
            {
                new { Country = "USA", CountryCode = "US", States = new[] { "California", "Texas", "New York", "Florida", "Illinois" } },
                new { Country = "Canada", CountryCode = "CA", States = new[] { "Ontario", "Quebec", "British Columbia", "Alberta", "Manitoba" } },
                new { Country = "Germany", CountryCode = "DE", States = new[] { "Bavaria", "Baden-Württemberg", "North Rhine-Westphalia", "Hesse", "Saxony" } },
                new { Country = "Australia", CountryCode = "AU", States = new[] { "New South Wales", "Victoria", "Queensland", "Western Australia", "South Australia" } }
            };

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
                        RasterCode = nextRaster,
                        Color = GenerateColor(nextRaster)
                    };
                    nextRaster++;

                    var bbox = GetMockLonLatBounds(state.CountryCode, state.StateName);
                    if (bbox != null)
                    {
                        var path = CreateRectPathFromLonLat(bbox.Value.minLon, bbox.Value.minLat, bbox.Value.maxLon, bbox.Value.maxLat);
                        state.Geometry.Add(path);
                        state.Bounds = path.Bounds;
                    }

                    _stateFeatures.Add(state);
                }
            }
        }

        private (double minLon, double minLat, double maxLon, double maxLat)? GetMockLonLatBounds(string countryCode, string stateName)
        {
            countryCode = countryCode.ToUpperInvariant();
            switch (countryCode)
            {
                case "US":
                    return stateName switch
                    {
                        "California" => (-124.5, 32.0, -114.0, 42.0),
                        "Texas" => (-106.7, 25.8, -93.5, 36.5),
                        "New York" => (-79.8, 40.5, -71.5, 45.1),
                        "Florida" => (-87.6, 24.5, -80.0, 31.0),
                        "Illinois" => (-91.5, 36.9, -87.5, 42.5),
                        _ => null
                    };
                case "CA":
                    return stateName switch
                    {
                        "Ontario" => (-95.0, 42.0, -74.0, 57.0),
                        "Quebec" => (-79.0, 45.0, -57.0, 62.0),
                        "British Columbia" => (-139.0, 48.3, -114.0, 60.0),
                        "Alberta" => (-120.0, 49.0, -110.0, 60.0),
                        "Manitoba" => (-102.0, 49.0, -95.0, 60.0),
                        _ => null
                    };
                case "DE":
                    return stateName switch
                    {
                        "Bavaria" => (10.0, 47.0, 13.5, 50.6),
                        "Baden-Württemberg" => (7.5, 47.5, 10.5, 49.7),
                        "North Rhine-Westphalia" => (5.8, 50.3, 8.7, 52.5),
                        "Hesse" => (7.8, 49.4, 10.2, 51.7),
                        "Saxony" => (11.9, 50.2, 15.1, 51.7),
                        _ => null
                    };
                case "AU":
                    return stateName switch
                    {
                        "New South Wales" => (141.0, -37.5, 154.0, -28.0),
                        "Victoria" => (141.0, -39.0, 150.0, -34.0),
                        "Queensland" => (138.0, -29.0, 154.0, -10.0),
                        "Western Australia" => (112.0, -35.0, 129.0, -14.0),
                        "South Australia" => (129.0, -38.0, 141.0, -26.0),
                        _ => null
                    };
                default:
                    return null;
            }
        }

        private SKPath CreateRectPathFromLonLat(double minLon, double minLat, double maxLon, double maxLat)
        {
            var (minX, minY) = LonLatToBasePixels(minLon, maxLat); // y inverted
            var (maxX, maxY) = LonLatToBasePixels(maxLon, minLat);
            var rect = SKRect.Create((float)minX, (float)minY, (float)(maxX - minX), (float)(maxY - minY));
            var path = new SKPath { FillType = SKPathFillType.EvenOdd };
            path.AddRect(rect);
            return path;
        }

        private (double x, double y) LonLatToBasePixels(double lon, double lat)
        {
            // Preserve legacy signature for other callers that expect base logical pixels
            double x = (lon + 180.0) / 360.0 * _baseWidth;
            double y = (90.0 - lat) / 180.0 * _baseHeight;
            return (x, y);
        }

        private static SKColor GenerateColor(int index)
        {
            byte r = (byte)(120 + (index * 47) % 136);
            byte g = (byte)(120 + (index * 73) % 136);
            byte b = (byte)(120 + (index * 101) % 136);
            return new SKColor(r, g, b, 255);
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

        public void SetSelectedState(StateFeature? state)
        {
            _selectedStateCode = state?.RasterCode ?? -1;
        }

        /// <summary>
        /// Gets the state grid for rendering state borders
        /// </summary>
        public int[,]? GetStateGrid()
        {
            if (!_dataLoaded) LoadStateData();
            EnsureStateGridBuilt();
            return _stateGrid;
        }

        /// <summary>
        /// Gets the currently selected state raster code
        /// </summary>
        public int GetSelectedStateCode() => _selectedStateCode;

        public void RenderStateFills(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize)
        {
            if (!_dataLoaded) LoadStateData();
            EnsureStateGridBuilt();
            if (_stateGrid == null) return;

            float scaleX = mapPixelSize.Width / (float)_baseWidth;
            float scaleY = mapPixelSize.Height / (float)_baseHeight;
            var baseViewport = new SKRect(viewport.Left / scaleX, viewport.Top / scaleY, viewport.Right / scaleX, viewport.Bottom / scaleY);
            var clip = canvas.DeviceClipBounds;
            int outW = Math.Max(1, clip.Width);
            int outH = Math.Max(1, clip.Height);
            using var bitmap = new SKBitmap(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
            bitmap.Erase(SKColors.Transparent);
            unsafe
            {
                uint* pixels = (uint*)bitmap.GetPixels().ToPointer();
                int stride = bitmap.RowBytes / 4;
                int gridW = _gridWidth;
                int gridH = _gridHeight;
                Parallel.For(0, outH, y =>
                {
                    for (int x = 0; x < outW; x++)
                    {
                        int logicalX = (int)(baseViewport.Left + (x * baseViewport.Width) / outW);
                        int logicalY = (int)(baseViewport.Top + (y * baseViewport.Height) / outH);
                        int gridX = logicalX / _gridScaleFactor;
                        int gridY = logicalY / _gridScaleFactor;
                        if (gridX < 0 || gridY < 0 || gridX >= gridW || gridY >= gridH) continue;
                        int code = _stateGrid![gridY, gridX];
                        if (code <= 0) continue;
                        if (!_stateColorsByCode.TryGetValue(code, out var color))
                        {
                            color = GenerateColor(code);
                            _stateColorsByCode[code] = color;
                        }
                        uint packed = (uint)(0xFF000000 | (color.Red << 16) | (color.Green << 8) | color.Blue);
                        pixels[y * stride + x] = packed;
                    }
                });
            }
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
            canvas.DrawBitmap(bitmap, new SKPoint(0, 0), paint);
        }

        public void RenderStateBorders(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize, float borderWidth = 1.0f, SKColor? borderColor = null)
        {
            if (!_dataLoaded) LoadStateData();
            EnsureStateGridBuilt();
            if (_stateFeatures.Count == 0) return;

            float scaleX = mapPixelSize.Width / (float)_baseWidth;
            float scaleY = mapPixelSize.Height / (float)_baseHeight;
            if (scaleX <= 0 || scaleY <= 0)
                return;

            var baseViewport = new SKRect(viewport.Left / scaleX, viewport.Top / scaleY, viewport.Right / scaleX, viewport.Bottom / scaleY);
            if (baseViewport.Width <= 0 || baseViewport.Height <= 0)
                return;

            var clip = canvas.DeviceClipBounds;
            if (clip.Width <= 0 || clip.Height <= 0)
                return;

            float viewWidth = clip.Width;
            float viewHeight = clip.Height;
            float scaleToViewX = viewWidth / baseViewport.Width;
            float scaleToViewY = viewHeight / baseViewport.Height;
            float avgScale = Math.Max(0.0001f, (scaleToViewX + scaleToViewY) * 0.5f);

            float desiredScreenWidth = Math.Max(1f, borderWidth);
            float selectedScreenWidth = desiredScreenWidth * 1.5f;
            float strokeInBase = desiredScreenWidth / avgScale;
            float selectedStrokeInBase = selectedScreenWidth / avgScale;

            using var normalPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = (borderColor ?? SKColors.Black).WithAlpha(255),
                StrokeWidth = strokeInBase,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round
            };

            using var selectedPaint = normalPaint.Clone();
            selectedPaint.Color = SKColors.White;
            selectedPaint.StrokeWidth = selectedStrokeInBase;

            var translate = SKMatrix.CreateTranslation(-baseViewport.Left, -baseViewport.Top);
            var scale = SKMatrix.CreateScale(scaleToViewX, scaleToViewY);
            var matrix = SKMatrix.Concat(scale, translate);

            canvas.Save();
            canvas.Concat(ref matrix);

            try
            {
                var viewportClip = new SKRect(baseViewport.Left, baseViewport.Top, baseViewport.Right, baseViewport.Bottom);
                canvas.ClipRect(viewportClip);

                foreach (var state in _stateFeatures)
                {
                    if (state.Geometry == null || state.Geometry.Count == 0)
                        continue;

                    if (!RectsIntersect(state.Bounds, baseViewport))
                        continue;

                    bool isSelected = _selectedStateCode > 0 && state.RasterCode == _selectedStateCode;
                    var paint = isSelected ? selectedPaint : normalPaint;

                    foreach (var path in state.Geometry)
                    {
                        if (path == null || path.IsEmpty)
                            continue;

                        canvas.DrawPath(path, paint);
                    }
                }
            }
            finally
            {
                canvas.Restore();
            }
        }

        // --- Quick lookup helpers for selection ---
        private static bool RectsIntersect(SKRect a, SKRect b)
        {
            if (a.IsEmpty || b.IsEmpty)
                return false;

            return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
        }

        public StateFeature? GetStateAtGrid(int gridX, int gridY)
        {
            EnsureStateGridBuilt();
            if (_stateGrid == null) return null;
            int sx = gridX / _gridScaleFactor;
            int sy = gridY / _gridScaleFactor;
            if (sx < 0 || sy < 0 || sx >= _gridWidth || sy >= _gridHeight) return null;
            int code = _stateGrid[sy, sx];
            if (code <= 0) return null;
            return _stateFeatures.Find(s => s.RasterCode == code);
        }

        public StateFeature? GetStateAtGridScaled(int gridX, int gridY)
        {
            EnsureStateGridBuilt();
            if (_stateGrid == null) return null;
            int sx = gridX / _gridScaleFactor;
            int sy = gridY / _gridScaleFactor;
            if (sx < 0 || sy < 0 || sx >= _gridWidth || sy >= _gridHeight) return null;
            int code = _stateGrid[sy, sx];
            if (code <= 0) return null;
            return _stateFeatures.Find(s => s.RasterCode == code);
        }

        public void Dispose()
        {
            foreach (var state in _stateFeatures)
            {
                state.DisposePaths();
            }
            _stateFeatures.Clear();
            _stateGrid = null;
            _stateGridBuilt = false;
            _stateColorsByCode.Clear();
            _dataLoaded = false;
        }

        /// <summary>
        /// Force reload of state data, clearing any existing loaded data
        /// </summary>
        public void ReloadStateData()
        {
            Debug.WriteLine("[STATE MANAGER] Force reloading state data");
            
            // Clear existing data
            foreach (var state in _stateFeatures)
            {
                state.DisposePaths();
            }
            _stateFeatures.Clear();
            _stateGrid = null;
            _stateGridBuilt = false;
            _stateColorsByCode.Clear();
            _dataLoaded = false;
            
            // Reset loading flags
            _disableShapefileLoading = false;
            _shpLoadAttempts = 0;
            
            // Reload
            LoadStateData();
        }

        private void BuildStateGrid()
        {
            if (_stateGridBuilt) return;
            try
            {
                _stateColorsByCode.Clear();
                foreach (var s in _stateFeatures)
                {
                    if (s.RasterCode > 0)
                    {
                        _stateColorsByCode[s.RasterCode] = s.Color;
                    }
                }

                using var codeBitmap = new SKBitmap(_gridWidth, _gridHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
                codeBitmap.Erase(SKColors.Transparent);
                using (var canvas = new SKCanvas(codeBitmap))
                using (var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false, BlendMode = SKBlendMode.Src })
                {
                    var scale = 1f / _gridScaleFactor;
                    var scaleMatrix = SKMatrix.CreateScale(scale, scale);
                    foreach (var state in _stateFeatures)
                    {
                        byte r = (byte)(state.RasterCode & 0xFF);
                        byte g = (byte)((state.RasterCode >> 8) & 0xFF);
                        byte b = (byte)((state.RasterCode >> 16) & 0xFF);
                        paint.Color = new SKColor(r, g, b, 0xFF);

                        foreach (var geoPath in state.Geometry)
                        {
                            if (geoPath == null || geoPath.IsEmpty) continue;

                            using var scaledPath = new SKPath(geoPath);
                            scaledPath.Transform(scaleMatrix);
                            canvas.DrawPath(scaledPath, paint);
                        }
                    }
                }

                var grid = new int[_gridHeight, _gridWidth];
                unsafe
                {
                    uint* pixels = (uint*)codeBitmap.GetPixels().ToPointer();
                    int stride = codeBitmap.RowBytes / 4;
                    for (int y = 0; y < _gridHeight; y++)
                    {
                        for (int x = 0; x < _gridWidth; x++)
                        {
                            uint p = pixels[y * stride + x];
                            int r = (int)((p >> 16) & 0xFF);
                            int g = (int)((p >> 8) & 0xFF);
                            int b = (int)(p & 0xFF);
                            int code = r | (g << 8) | (b << 16);
                            grid[y, x] = code;
                        }
                    }
                }

                _stateGrid = grid;
                _stateGridBuilt = true;
                Debug.WriteLine($"[STATE MANAGER] Built scaled state grid {_gridWidth}x{_gridHeight} (scale factor {_gridScaleFactor})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE MANAGER] Failed to build state grid: {ex.Message}");
                _stateGrid = new int[_gridHeight, _gridWidth];
                _stateGridBuilt = true;
            }
        }

        private void EnsureStateGridBuilt()
        {
            if (!_stateGridBuilt)
            {
                BuildStateGrid();
            }
        }

        /// <summary>
        /// Handles state/country border mismatches by splitting states across countries
        /// and merging small fragments into neighboring states
        /// </summary>
        public void ProcessStateSplittingAndMerging(int[,] countryGrid)
        {
            if (!_dataLoaded) LoadStateData();
            EnsureStateGridBuilt();

            if (_stateGrid == null || countryGrid == null)
            {
                Debug.WriteLine("[STATE SPLITTING] Missing required grids for processing");
                return;
            }

            Debug.WriteLine("[STATE SPLITTING] Starting state splitting and merging process...");

            try
            {
                var stateCountryAnalysis = AnalyzeStateCountryOverlaps(countryGrid);
                ProcessStateSplits(stateCountryAnalysis, countryGrid);

                Debug.WriteLine("[STATE SPLITTING] State splitting and merging completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STATE SPLITTING] Error during processing: {ex.Message}");
            }
        }

        public void RelaxStateBorders(int[,] countryGrid, int iterations = 2)
        {
            if (!_dataLoaded) LoadStateData();
            EnsureStateGridBuilt();
            if (_stateGrid == null)
            {
                Debug.WriteLine("[STATE RELAX] State grid is not available for relaxation");
                return;
            }

            if (countryGrid == null)
            {
                Debug.WriteLine("[STATE RELAX] Country grid is not available for relaxation");
                return;
            }

            iterations = Math.Clamp(iterations, 1, 8);
            int height = Math.Min(_stateGrid.GetLength(0), countryGrid.GetLength(0));
            int width = Math.Min(_stateGrid.GetLength(1), countryGrid.GetLength(1));
            bool anyChange = false;

            for (int iter = 0; iter < iterations; iter++)
            {
                bool iterationChanged = false;
                var nextGrid = (int[,])_stateGrid.Clone();

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int countryCode = countryGrid[y, x];
                        if (countryCode <= 0)
                            continue;

                        int currentState = _stateGrid[y, x];
                        Span<int> neighborStates = stackalloc int[8];
                        Span<int> neighborCounts = stackalloc int[8];
                        int trackedStates = 0;
                        int bestState = currentState;
                        int bestCount = 0;

                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                                    continue;

                                if (countryGrid[ny, nx] != countryCode)
                                    continue;

                                int neighborState = _stateGrid[ny, nx];
                                if (neighborState <= 0)
                                    continue;

                                bool recorded = false;
                                for (int i = 0; i < trackedStates; i++)
                                {
                                    if (neighborStates[i] == neighborState)
                                    {
                                        int newCount = ++neighborCounts[i];
                                        if (newCount > bestCount)
                                        {
                                            bestCount = newCount;
                                            bestState = neighborState;
                                        }
                                        recorded = true;
                                        break;
                                    }
                                }

                                if (!recorded && trackedStates < neighborStates.Length)
                                {
                                    neighborStates[trackedStates] = neighborState;
                                    neighborCounts[trackedStates] = 1;
                                    if (bestCount < 1)
                                    {
                                        bestCount = 1;
                                        bestState = neighborState;
                                    }
                                    trackedStates++;
                                }
                            }
                        }

                        if (bestCount == 0)
                            continue;

                        if (bestState != currentState && bestCount >= 3)
                        {
                            nextGrid[y, x] = bestState;
                            iterationChanged = true;
                        }
                        else if (currentState <= 0 && bestCount >= 2)
                        {
                            nextGrid[y, x] = bestState;
                            iterationChanged = true;
                        }
                    }
                }

                if (!iterationChanged)
                    break;

                _stateGrid = nextGrid;
                anyChange = true;
            }

            if (anyChange)
            {
                Debug.WriteLine("[STATE RELAX] State borders relaxed to better fit country outlines");
            }
            else
            {
                Debug.WriteLine("[STATE RELAX] No state border adjustments were necessary");
            }
        }

        /// <summary>
        /// Analyzes which states overlap with which countries and calculates sizes
        /// </summary>
        private Dictionary<int, Dictionary<int, int>> AnalyzeStateCountryOverlaps(int[,] countryGrid)
        {
            var stateCountryPixels = new Dictionary<int, Dictionary<int, int>>();
            
            int height = _stateGrid!.GetLength(0);
            int width = _stateGrid.GetLength(1);
            
            // Count pixels for each state-country combination
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int stateCode = _stateGrid[y, x];
                    int countryCode = y < countryGrid.GetLength(0) && x < countryGrid.GetLength(1) 
                        ? countryGrid[y, x] : 0;
                    
                    if (stateCode > 0 && countryCode > 0)
                    {
                        if (!stateCountryPixels.ContainsKey(stateCode))
                            stateCountryPixels[stateCode] = new Dictionary<int, int>();
                        
                        if (!stateCountryPixels[stateCode].ContainsKey(countryCode))
                            stateCountryPixels[stateCode][countryCode] = 0;
                        
                        stateCountryPixels[stateCode][countryCode]++;
                    }
                }
            }
            
            return stateCountryPixels;
        }

        /// <summary>
        /// Processes state splits and merges small fragments
        /// </summary>
        private void ProcessStateSplits(Dictionary<int, Dictionary<int, int>> stateCountryAnalysis, int[,] countryGrid)
        {
            var statesToProcess = new List<int>();
            
            // Identify states that span multiple countries
            foreach (var kvp in stateCountryAnalysis)
            {
                int stateCode = kvp.Key;
                var countryPixels = kvp.Value;
                
                if (countryPixels.Count > 1)
                {
                    statesToProcess.Add(stateCode);
                    
                    int totalPixels = countryPixels.Values.Sum();
                    Debug.WriteLine($"[STATE SPLITTING] State {stateCode} spans {countryPixels.Count} countries, total pixels: {totalPixels}");
                    
                    foreach (var countryKvp in countryPixels)
                    {
                        double percentage = (double)countryKvp.Value / totalPixels * 100;
                        Debug.WriteLine($"  - Country {countryKvp.Key}: {countryKvp.Value} pixels ({percentage:F1}%)");
                    }
                }
            }
            
            // Process each multi-country state
            foreach (int stateCode in statesToProcess)
            {
                ProcessSingleStateSplit(stateCode, stateCountryAnalysis[stateCode], countryGrid);
            }
        }

        /// <summary>
        /// Processes a single state that spans multiple countries
        /// </summary>
        private void ProcessSingleStateSplit(int stateCode, Dictionary<int, int> countryPixels, int[,] countryGrid)
        {
            int totalPixels = countryPixels.Values.Sum();
            const double mergeThreshold = 0.6; // 60% threshold
            
            var smallFragments = new List<int>();
            var largeFragments = new List<int>();
            
            // Categorize fragments by size
            foreach (var kvp in countryPixels)
            {
                double percentage = (double)kvp.Value / totalPixels;
                if (percentage < mergeThreshold)
                {
                    smallFragments.Add(kvp.Key);
                }
                else
                {
                    largeFragments.Add(kvp.Key);
                }
            }
            
            Debug.WriteLine($"[STATE SPLITTING] State {stateCode}: {smallFragments.Count} small fragments, {largeFragments.Count} large fragments");
            
            // Merge small fragments into neighboring states
            foreach (int countryCode in smallFragments)
            {
                MergeStateFragmentIntoNeighbors(stateCode, countryCode, countryGrid);
            }
        }

        /// <summary>
        /// Merges a small state fragment into neighboring states within the same country
        /// </summary>
        private void MergeStateFragmentIntoNeighbors(int stateCode, int countryCode, int[,] countryGrid)
        {
            if (_stateGrid == null) return;
            
            int height = _stateGrid.GetLength(0);
            int width = _stateGrid.GetLength(1);
            
            // Find all pixels of this state fragment
            var fragmentPixels = new List<(int x, int y)>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (_stateGrid[y, x] == stateCode && 
                        y < countryGrid.GetLength(0) && x < countryGrid.GetLength(1) &&
                        countryGrid[y, x] == countryCode)
                    {
                        fragmentPixels.Add((x, y));
                    }
                }
            }
            
            Debug.WriteLine($"[STATE SPLITTING] Merging {fragmentPixels.Count} pixels of state {stateCode} in country {countryCode}");
            
            // For each pixel in the fragment, find the most common neighboring state in the same country
            var neighboringStates = new Dictionary<int, int>();
            
            foreach (var (px, py) in fragmentPixels)
            {
                // Check 8-connected neighbors
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int nx = px + dx;
                        int ny = py + dy;
                        
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            int neighborState = _stateGrid[ny, nx];
                            int neighborCountry = ny < countryGrid.GetLength(0) && nx < countryGrid.GetLength(1) 
                                ? countryGrid[ny, nx] : 0;
                            
                            // Only consider neighbors in the same country that are different states
                            if (neighborState != stateCode && neighborState > 0 && 
                                neighborCountry == countryCode)
                            {
                                if (!neighboringStates.ContainsKey(neighborState))
                                    neighboringStates[neighborState] = 0;
                                neighboringStates[neighborState]++;
                            }
                        }
                    }
                }
            }
            
            // Find the most common neighboring state to merge into
            if (neighboringStates.Count > 0)
            {
                int targetState = neighboringStates.OrderByDescending(kvp => kvp.Value).First().Key;
                
                Debug.WriteLine($"[STATE SPLITTING] Merging state {stateCode} fragment into state {targetState} (country {countryCode})");
                
                // Update all pixels in this fragment to the target state
                foreach (var (px, py) in fragmentPixels)
                {
                    _stateGrid[py, px] = targetState;
                }
                
                // Update the state feature's country code if needed
                var stateFeature = _stateFeatures.Find(s => s.RasterCode == stateCode);
                var targetFeature = _stateFeatures.Find(s => s.RasterCode == targetState);
                
                if (stateFeature != null && targetFeature != null)
                {
                    Debug.WriteLine($"[STATE SPLITTING] Fragment of {stateFeature.StateName} merged into {targetFeature.StateName}");
                }
            }
            else
            {
                Debug.WriteLine($"[STATE SPLITTING] No neighboring states found for state {stateCode} fragment in country {countryCode}");
            }
        }
    }

    internal static class StateFeatureExtensions
    {
        public static void DisposePaths(this StateBorderManager.StateFeature feature)
        {
            if (feature.Geometry == null) return;
            foreach (var p in feature.Geometry)
            {
                p?.Dispose();
            }
            feature.Geometry.Clear();
        }
    }
}