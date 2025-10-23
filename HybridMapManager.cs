using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Manages both terrain and political map layers with memory-efficient rendering
    /// </summary>
    public class HybridMapManager
    {
        private readonly MultiResolutionMapManager _terrainManager;
        private readonly PoliticalBorderManager _politicalManager;
        private readonly PoliticalTileManager _politicalTileManager;
        private readonly StateBorderManager _stateManager;

        private MapViewType _currentViewType = MapViewType.Terrain;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        private SKBitmap? _populationDensityMap = null;
        private bool _populationLoadAttempted = false; 
        
        public int PoliticalBaseWidth { get; }
        public int PoliticalBaseHeight { get; }
        
        private IndexedCountryFeature? _selectedCountry = null;
        private StateBorderManager.StateFeature? _selectedState = null;
        private CitySelection? _selectedCity = null;
        private bool _stateSplittingProcessed = false;
        private bool _mergeSmallStatesWithCities = false;
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        public IndexedCountryFeature? SelectedCountry => _selectedCountry;
        public StateBorderManager.StateFeature? SelectedState => _selectedState;
        public CitySelection? SelectedCity => _selectedCity;
        public bool MergeSmallStatesWithCities
        {
            get => _mergeSmallStatesWithCities;
            set => _mergeSmallStatesWithCities = value;
        }
        public bool UsePersistedStateMap
        {
            get => _stateManager.UsePersistedStateMap;
            set
            {
                if (_stateManager.UsePersistedStateMap == value)
                    return;

                _stateManager.UsePersistedStateMap = value;
                _stateSplittingProcessed = false;
            }
        }

        public event EventHandler<MapViewType>? ViewTypeChanged;
        public event EventHandler<IndexedCountryFeature?>? SelectedCountryChanged;
        public event EventHandler<StateBorderManager.StateFeature?>? SelectedStateChanged;
        public event EventHandler<CitySelection?>? SelectedCityChanged;

        public sealed record CitySelection(
            string Name,
            string CountryCode,
            float Longitude,
            float Latitude,
            int PixelX,
            int PixelY,
            int PopulationEstimate,
            int ScaleRank,
            int RasterCode);

        public sealed record EconomyCityInfo(
            string CountryName,
            string StateName,
            string CityName,
            int Population,
            double? Latitude,
            double? Longitude);
        
        public HybridMapManager(int baseWidth = (4096*4), int baseHeight = (2048*4), int? politicalBaseWidth = null, int? politicalBaseHeight = null)
        {
            _terrainManager = new MultiResolutionMapManager(baseWidth, baseHeight);
            _politicalManager = new PoliticalBorderManager();

            int polW = politicalBaseWidth ?? ParseEnvOrDefault("ES_POL_BASE_WIDTH", baseWidth);
            int polH = politicalBaseHeight ?? ParseEnvOrDefault("ES_POL_BASE_HEIGHT", baseHeight);
            PoliticalBaseWidth = polW;
            PoliticalBaseHeight = polH;

            _politicalTileManager = new PoliticalTileManager(_politicalManager, PoliticalBaseWidth, PoliticalBaseHeight);
            _stateManager = new StateBorderManager(PoliticalBaseWidth, PoliticalBaseHeight);
            
            // Connect state manager to political tile manager
            _politicalTileManager.SetStateManager(_stateManager);
        }
        
        private static int ParseEnvOrDefault(string key, int def)
        {
            var s = Environment.GetEnvironmentVariable(key);
            return int.TryParse(s, out var v) && v > 0 ? v : def;
        }
        
        public void SetViewType(MapViewType viewType)
        {
            if (_currentViewType != viewType)
            {
                _currentViewType = viewType;
                
                if (viewType == MapViewType.Political)
                {
                    GC.Collect();
                }
                else if (viewType == MapViewType.States)
                {
                    try
                    {
                        _stateManager.LoadStateData();
                        ProcessStateSplittingAndMerging();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[HYBRID MANAGER] Failed preparing states view: {ex.Message}");
                    }
                }
                else if (viewType == MapViewType.PopulationDensity)
                {
                    // Synchronous generation on first switch so base zoom shows real data immediately
                    if (!_populationLoadAttempted || _populationDensityMap == null)
                    {
                        _populationLoadAttempted = true;
                        try
                        {
                            _populationDensityMap = PopulationDensityRenderer.GeneratePopulationDensityMap();
                            Debug.WriteLine(_populationDensityMap != null
                                ? "[PopulationDensity] Generated synchronously on view switch."
                                : "[PopulationDensity] Generation returned null; will use fallback.");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PopulationDensity] Synchronous generation failed: {ex.Message}");
                        }
                    }
                }
                
                ViewTypeChanged?.Invoke(this, viewType);
            }
        }
        
        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;
                _politicalTileManager.SetPoliticalMapDate(date);
                _politicalMapDate = date;
            }
        }
        
        /// <summary>
        /// Expose effective scale used by terrain at a given zoom (accounts for extra min-zoom FOV)
        /// </summary>
        public float GetEffectiveScaleForZoom(int zoomLevel) => 1.0f;
        
        /// <summary>
        /// Highlights a country border at the specified mouse position
        /// </summary>
        /// <param name="mousePosition">The mouse position in map coordinates</param>
        /// <param name="zoomLevel">The current zoom level</param>
        public void HighlightCountryBorder(Point mousePosition, int zoomLevel = 1)
        {
            if (_currentViewType == MapViewType.Political)
            {
                try
                {
                    int cellSize = GetCellSizeForZoom(zoomLevel);
                    // Terrain map pixels
                    int tMapWidth = BaseWidth * cellSize;
                    int tMapHeight = BaseHeight * cellSize;
                    if (mousePosition.X < 0 || mousePosition.X >= tMapWidth || mousePosition.Y < 0 || mousePosition.Y >= tMapHeight)
                        return;

                    // Convert terrain pixel -> normalized -> political pixel
                    var (px, py) = TerrainPixelToPoliticalPixel(mousePosition.X, mousePosition.Y, zoomLevel);
                    int pMapWidth = PoliticalBaseWidth * cellSize;
                    int pMapHeight = PoliticalBaseHeight * cellSize;
                    px = Math.Clamp(px, 0, pMapWidth - 1);
                    py = Math.Clamp(py, 0, pMapHeight - 1);

                    var (lon, lat) = CoordinateTransform.PixelToGeographic(px, py, pMapWidth, pMapHeight);
                    var country = _politicalTileManager.GetCountryAtGeographicPoint(lon, lat);
                    if (country != null)
                    {
                        Debug.WriteLine($"Found country at highlight position: {country.CountryName} ({country.CountryCode})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error highlighting country border: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Assembles the current view based on the selected map type
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            SKBitmap? result = null;
            
            switch (_currentViewType)
            {
                case MapViewType.Terrain:
                    result = _terrainManager.AssembleView(zoomLevel, viewArea, onTileReady);
                    break;
                
                case MapViewType.Political:
                    // Convert terrain viewArea to political pixel space and apply min-zoom FOV expansion
                    var polView = ConvertTerrainViewToPoliticalView(viewArea, zoomLevel);
                    var polBmp = _politicalTileManager.AssembleView(zoomLevel, polView, onTileReady, forceSync: true);
                    if (polBmp != null && (polBmp.Width != viewArea.Width || polBmp.Height != viewArea.Height))
                    {
                        var resized = ResizeBitmap(polBmp, viewArea.Width, viewArea.Height);
                        polBmp.Dispose();
                        polBmp = resized; // now matches viewArea
                    }

                    if (polBmp != null)
                    {
                        // Removed state fill overlay (was producing blocky square artifacts).
                        // Optionally draw thin state borders only if a state is selected for context.
                        try
                        {
                            using var canvas = new SKCanvas(polBmp);
                            if (_selectedState != null)
                            {
                                int cellSize = GetCellSizeForZoom(zoomLevel);
                                var politicalPixelSize = new SKSizeI(PoliticalBaseWidth * cellSize, PoliticalBaseHeight * cellSize);
                                _stateManager.RenderStateBorders(canvas, polView, politicalPixelSize, 1.0f, new SKColor(0, 0, 0, 160));
                            }

                            RenderCitiesOverlay(canvas, viewArea, new SKSizeI(polBmp.Width, polBmp.Height), zoomLevel);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[POLITICAL VIEW] Overlay failed: {ex.Message}");
                        }
                        result = polBmp;
                    }
                    else
                    {
                        result = null;
                    }
                    break;

                case MapViewType.States:
                    var stateViewport = ConvertTerrainViewToPoliticalView(viewArea, zoomLevel);
                    int stateCell = GetCellSizeForZoom(zoomLevel);
                    var statePixelSize = new SKSizeI(PoliticalBaseWidth * stateCell, PoliticalBaseHeight * stateCell);
                    var stateBitmap = new SKBitmap(viewArea.Width, viewArea.Height);
                    using (var canvas = new SKCanvas(stateBitmap))
                    {
                        canvas.Clear(new SKColor(135, 206, 235));
                        try
                        {
                            _stateManager.RenderStateFills(canvas, stateViewport, statePixelSize);
                            _stateManager.RenderStateBorders(canvas, stateViewport, statePixelSize, 2.0f, new SKColor(0, 0, 0, 200));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[STATE VIEW] Failed to render states: {ex.Message}");
                        }
                    }
                    result = stateBitmap;
                    break;

                case MapViewType.PopulationDensity:
                    // Ensure map exists synchronously if not already
                    if (_populationDensityMap == null && !_populationLoadAttempted)
                    {
                        _populationLoadAttempted = true;
                        try
                        {
                            _populationDensityMap = PopulationDensityRenderer.GeneratePopulationDensityMap();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PopulationDensity] Late synchronous generation failed: {ex.Message}");
                        }
                    }
                    result = GetPopulationDensityView(viewArea, zoomLevel) ?? CreatePopulationFallback(viewArea.Width, viewArea.Height, "Null view");
                    break;
                
                default:
                    result = null;
                    break;
            }
            
            return result;
        }

        private static SKBitmap ResizeBitmap(SKBitmap source, int width, int height)
        {
            var resized = new SKBitmap(width, height, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(resized);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, new SKRect(0, 0, width, height));
            canvas.Flush();
            return resized;
        }

        private SKBitmap? GetPopulationDensityView(SKRectI viewArea, int zoomLevel)
        {
            try
            {
                if (_populationDensityMap == null)
                {
                    return CreatePopulationFallback(viewArea.Width, viewArea.Height, "No data (still null)");
                }
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int terrainPixelWidth = BaseWidth * cellSize;
                int terrainPixelHeight = BaseHeight * cellSize;
                double sx = _populationDensityMap.Width / (double)terrainPixelWidth;
                double sy = _populationDensityMap.Height / (double)terrainPixelHeight;
                var sourceRect = new SKRectI(
                    (int)(viewArea.Left * sx),
                    (int)(viewArea.Top * sy),
                    (int)(viewArea.Right * sx),
                    (int)(viewArea.Bottom * sy));
                sourceRect.Left = Math.Max(0, sourceRect.Left);
                sourceRect.Top = Math.Max(0, sourceRect.Top);
                sourceRect.Right = Math.Min(_populationDensityMap.Width, sourceRect.Right);
                sourceRect.Bottom = Math.Min(_populationDensityMap.Height, sourceRect.Bottom);
                if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
                    return CreatePopulationFallback(viewArea.Width, viewArea.Height, "Empty rect");
                var result = new SKBitmap(viewArea.Width, viewArea.Height);
                using var canvas = new SKCanvas(result);
                canvas.Clear(new SKColor(15, 15, 15));
                canvas.DrawBitmap(_populationDensityMap, sourceRect, new SKRect(0,0, viewArea.Width, viewArea.Height));
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Population density view error: {ex.Message}");
                return CreatePopulationFallback(viewArea.Width, viewArea.Height, ex.GetType().Name);
            }
        }

        private SKBitmap CreatePopulationFallback(int width, int height, string reason)
        {
            var bmp = new SKBitmap(Math.Max(1, width), Math.Max(1, height));
            using var canvas = new SKCanvas(bmp);
            using var paint = new SKPaint { IsAntialias = true };
            for (int y = 0; y < height; y += 4)
            {
                float t = height > 1 ? (float)y / (height - 1) : 0f;
                var color = SKColor.FromHsl(120f * t, 70, (byte)(20 + 40 * t));
                paint.Color = color;
                canvas.DrawRect(new SKRect(0, y, width, y + 4), paint);
            }
            using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 12, IsAntialias = true };
            canvas.DrawText(reason, 6, 16, textPaint);
            return bmp;
        }

        private SKRectI ConvertTerrainViewToPoliticalView(SKRectI terrainView, int zoomLevel)
        {
            int cell = GetCellSizeForZoom(zoomLevel);
            int tW = BaseWidth * cell;
            int tH = BaseHeight * cell;
            int pW = PoliticalBaseWidth * cell;
            int pH = PoliticalBaseHeight * cell;
            float sx = pW / (float)tW;
            float sy = pH / (float)tH;
            return new SKRectI(
                (int)(terrainView.Left * sx),
                (int)(terrainView.Top * sy),
                (int)(terrainView.Right * sx),
                (int)(terrainView.Bottom * sy));
        }

        // NOTE: Was private; exposed publicly so editor can correctly map to higher resolution political grid.
        public (int px, int py) TerrainPixelToPoliticalPixel(int terrainX, int terrainY, int zoomLevel)
        {
            int cell = GetCellSizeForZoom(zoomLevel);
            int tW = BaseWidth * cell;
            int tH = BaseHeight * cell;
            int pW = PoliticalBaseWidth * cell;
            int pH = PoliticalBaseHeight * cell;
            int px = (int)Math.Round(terrainX * (pW / (double)tW));
            int py = (int)Math.Round(terrainY * (pH / (double)tH));
            return (px, py);
        }

        /// <summary>
        /// Helper to convert a screen (image) coordinate plus current view offset into political grid cell coordinates.
        /// Accounts for political grid being higher resolution than terrain.
        /// </summary>
        public (int gridX, int gridY) ScreenToPoliticalGrid(int screenX, int screenY, int zoomLevel, SKPointI viewOffset)
        {
            int cellSize = GetCellSizeForZoom(zoomLevel);

            // Translate the screen coordinate into terrain pixel space for the active zoom level.
            double terrainPixelX = screenX + viewOffset.X;
            double terrainPixelY = screenY + viewOffset.Y;

            // Convert terrain pixel offsets into terrain grid coordinates so we can scale them into
            // the higher resolution political grid without rounding bias.
            double terrainGridX = terrainPixelX / cellSize;
            double terrainGridY = terrainPixelY / cellSize;

            double scaleX = PoliticalBaseWidth / (double)BaseWidth;
            double scaleY = PoliticalBaseHeight / (double)BaseHeight;

            int gridX = (int)Math.Floor(terrainGridX * scaleX);
            int gridY = (int)Math.Floor(terrainGridY * scaleY);

            gridX = Math.Clamp(gridX, 0, PoliticalBaseWidth - 1);
            gridY = Math.Clamp(gridY, 0, PoliticalBaseHeight - 1);

            return (gridX, gridY);
        }

        /// <summary>
        /// Unified rendering helper to draw either Countries or States into a bitmap of the given outputSize.
        /// </summary>
        public SKBitmap? RenderAdminBitmap(MapViewLevel level, int zoomLevel, SKRectI viewArea, SKSizeI outputSize)
        {
            switch (level)
            {
                case MapViewLevel.Countries:
                    var polView = ConvertTerrainViewToPoliticalView(viewArea, zoomLevel);
                    var polBmp = _politicalTileManager.AssembleView(zoomLevel, polView, null, forceSync: true);
                    if (polBmp == null) return null;
                    var composed = new SKBitmap(outputSize.Width, outputSize.Height, polBmp.ColorType, polBmp.AlphaType);
                    using (var canvas = new SKCanvas(composed))
                    {
                        canvas.Clear(SKColors.Transparent);
                        canvas.DrawBitmap(polBmp, new SKRect(0, 0, outputSize.Width, outputSize.Height));

                        RenderCitiesOverlay(canvas, viewArea, outputSize, zoomLevel);
                    }
                    polBmp.Dispose();
                    return composed;
                case MapViewLevel.States:
                    var polViewport = ConvertTerrainViewToPoliticalView(viewArea, zoomLevel);
                    int cell = GetCellSizeForZoom(zoomLevel);
                    var politicalPixelSize = new SKSizeI(PoliticalBaseWidth * cell, PoliticalBaseHeight * cell);
                    var bmp = new SKBitmap(outputSize.Width, outputSize.Height);
                    using (var canvas = new SKCanvas(bmp))
                    {
                        canvas.Clear(new SKColor(135, 206, 235));
                        _stateManager.RenderStateFills(canvas, polViewport, politicalPixelSize);
                        _stateManager.RenderStateBorders(canvas, polViewport, politicalPixelSize, 2.0f, SKColors.Black);
                    }
                    return bmp;
                default:
                    return null;
            }
        }
        
        public SKSizeI GetMapSize(int zoomLevel) => _terrainManager.GetMapSize(zoomLevel);
        public int GetCellSizeForZoom(int zoomLevel) => _terrainManager.GetCellSizeForZoom(zoomLevel);
        public int BaseWidth => _terrainManager.BaseWidth;
        public int BaseHeight => _terrainManager.BaseHeight;

        public IndexedCountryFeature? GetCountryAtPixel(int pixelX, int pixelY, int zoomLevel, int viewOffsetX, int viewOffsetY)
        {
            try
            {
                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1) return null;
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int tMapWidth = BaseWidth * cellSize;
                int tMapHeight = BaseHeight * cellSize;
                int tpx = pixelX + viewOffsetX;
                int tpy = pixelY + viewOffsetY;
                var (ppx, ppy) = TerrainPixelToPoliticalPixel(tpx, tpy, zoomLevel);
                const int radius = 5;
                for (int r = 0; r <= radius; r++)
                {
                    foreach (var (dx, dy) in GetSpiralOffsets(r))
                    {
                        int sx = ppx + dx;
                        int sy = ppy + dy;
                        var c = _politicalTileManager.GetCountryAtPoliticalPixel(sx, sy, zoomLevel);
                        if (c != null) return c;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting country at pixel ({pixelX}, {pixelY}): {ex.Message}");
                return null;
            }
        }

        // Back-compat overloads for callers passing SKPointI offset
        public IndexedCountryFeature? GetCountryAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
            => GetCountryAtPixel(pixelX, pixelY, zoomLevel, viewOffset.X, viewOffset.Y);

        // Optional overload taking only X offset (Y=0)
        public IndexedCountryFeature? GetCountryAtPixel(int pixelX, int pixelY, int zoomLevel, int viewOffsetX)
            => GetCountryAtPixel(pixelX, pixelY, zoomLevel, viewOffsetX, 0);

        public void SelectCountry(IndexedCountryFeature? country)
        {
            if (_selectedCountry != country)
            {
                SelectCity(null);
                _selectedCountry = country;
                _politicalTileManager.SetSelectedCountry(country);
                
                // Process state splitting when first country is selected
                if (country != null && !_stateSplittingProcessed)
                {
                    Task.Run(() => ProcessStateSplittingAndMerging());
                }
                
                // Clear state selection when selecting a different country
                if (_selectedState != null)
                {
                    SelectCity(null);
                    _selectedState = null;
                    _stateManager.SetSelectedState(null);
                    _politicalTileManager.SetSelectedState(null);
                    SelectedStateChanged?.Invoke(this, null);
                }
                
                SelectedCountryChanged?.Invoke(this, country);
                Debug.WriteLine($"Country selection changed: {(country != null ? $"{country.CountryName} ({country.CountryCode})" : "None")}");
            }
        }
        
        public void SelectState(StateBorderManager.StateFeature? state)
        {
            if (_selectedState != state)
            {
                SelectCity(null);
                _selectedState = state;
                _stateManager.SetSelectedState(state);
                _politicalTileManager.SetSelectedState(state);
                SelectedStateChanged?.Invoke(this, state);
                Debug.WriteLine($"State selection changed: {(state != null ? $"{state.StateName} in {state.CountryName}" : "None")}");
            }
        }

        public void SelectCity(CitySelection? city)
        {
            if (_selectedCity != city)
            {
                _selectedCity = city;
                SelectedCityChanged?.Invoke(this, city);
            }
        }
        
        public void ClearCountrySelection() => SelectCountry(null);
        public void ClearStateSelection() => SelectState(null);

        public List<CachedCountryData> GetAllCountryData() => _politicalTileManager.GetAllCountryData();
        public PoliticalDataCache GetPoliticalDataCache() => _politicalTileManager.GetDataCache();
        public IndexedCountryFeature? FindCountryByName(string name)
        { if (string.IsNullOrWhiteSpace(name)) return null; var all = _politicalTileManager.GetAllCountryData(); var match = all.Find(c => string.Equals(c.CountryName, name, StringComparison.OrdinalIgnoreCase)); return match == null ? null : _politicalTileManager.GetCountryFeatureByRasterCode(match.RasterCode); }
        public void ChangeCountryControlAtGrid(int rasterCode, IEnumerable<Point> cells)
        {
            _politicalTileManager.ChangeControl(rasterCode, cells);
            _stateSplittingProcessed = false;
        }

        public void ChangeCountryControlRect(int rasterCode, Rectangle region)
        {
            IEnumerable<Point> Cells()
            {
                int x0 = Math.Max(0, region.Left);
                int y0 = Math.Max(0, region.Top);
                int x1 = Math.Min(PoliticalBaseWidth, region.Right);
                int y1 = Math.Min(PoliticalBaseHeight, region.Bottom);
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        yield return new Point(x, y);
                    }
                }
            }

            _politicalTileManager.ChangeControl(rasterCode, Cells());
            _stateSplittingProcessed = false;
        }

        public List<(Point cell, int previousId)> ChangeCountryControlZeroSum(int rasterCode, IEnumerable<Point> brushCells)
        {
            var changes = _politicalTileManager.ChangeControlZeroSum(rasterCode, brushCells);
            if (changes.Count > 0)
            {
                _stateSplittingProcessed = false;
            }
            return changes;
        }
        public List<StateBorderManager.StateFeature> GetAllStates() => _stateManager.GetAllStates();
        public StateBorderManager.StateFeature? GetStateAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
        {
            try
            {
                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1) return null;
                int cellSize = GetCellSizeForZoom(zoomLevel);
                int tpx = pixelX + viewOffset.X;
                int tpy = pixelY + viewOffset.Y;
                var (ppx, ppy) = TerrainPixelToPoliticalPixel(tpx, tpy, zoomLevel);
                int gx = ppx / cellSize; int gy = ppy / cellSize; const int radius = 3;
                foreach (var (dx, dy) in GetSpiralOffsets(radius)) { var st = _stateManager.GetStateAtGridScaled(gx + dx, gy + dy); if (st != null) return st; }
                return null;
            }
            catch (Exception ex) { Debug.WriteLine($"Error detecting state at pixel ({pixelX}, {pixelY}): {ex.Message}"); return null; }
        }

        public CitySelection? GetCityAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset, StateBorderManager.StateFeature? stateHint = null)
        {
            try
            {
                EnsureCitiesLoaded();
                if (_cityPoints == null || _cityPoints.Count == 0)
                    return null;

                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1)
                    return null;

                int cellSize = GetCellSizeForZoom(zoomLevel);
                double terrainTotalWidthPx = BaseWidth * (double)cellSize;
                double terrainTotalHeightPx = BaseHeight * (double)cellSize;
                if (terrainTotalWidthPx <= 0 || terrainTotalHeightPx <= 0)
                    return null;

                double politiToTerrainScaleX = terrainTotalWidthPx / PoliticalBaseWidth;
                double politiToTerrainScaleY = terrainTotalHeightPx / PoliticalBaseHeight;

                string? isoFilter = _selectedCountry?.CountryCode?.ToUpperInvariant();
                int stateRaster = stateHint?.RasterCode ?? _selectedState?.RasterCode ?? -1;
                double tolerance = zoomLevel switch
                {
                    <= 1 => 28.0,
                    2 => 24.0,
                    3 => 20.0,
                    4 => 18.0,
                    5 => 16.0,
                    _ => 14.0
                };
                double maxDistSq = tolerance * tolerance;

                CityPoint? best = null;
                double bestDist = maxDistSq + 1;

                foreach (var city in _cityPoints)
                {
                    if (!string.IsNullOrWhiteSpace(isoFilter))
                    {
                        if (!string.Equals(city.IsoCode, isoFilter, StringComparison.OrdinalIgnoreCase) && (stateRaster <= 0 || city.RasterCode != stateRaster))
                            continue;
                    }

                    if (stateRaster > 0 && city.RasterCode > 0 && city.RasterCode != stateRaster)
                    {
                        // Allow cities outside the raster hint but deprioritize them.
                        if (best != null && best.RasterCode == stateRaster)
                            continue;
                    }

                    double cityTerrainX = city.PixelX * politiToTerrainScaleX;
                    double cityTerrainY = city.PixelY * politiToTerrainScaleY;
                    double screenX = cityTerrainX - viewOffset.X;
                    double screenY = cityTerrainY - viewOffset.Y;

                    double dx = screenX - pixelX;
                    double dy = screenY - pixelY;
                    double distSq = dx * dx + dy * dy;

                    if (distSq > maxDistSq)
                        continue;

                    bool isBetter = distSq < bestDist;
                    if (!isBetter && stateRaster > 0)
                    {
                        if (best == null || best.RasterCode != stateRaster)
                        {
                            isBetter = city.RasterCode == stateRaster;
                        }
                    }

                    if (isBetter)
                    {
                        best = city;
                        bestDist = distSq;
                    }
                }

                if (best == null)
                    return null;

                return new CitySelection(best.Name, best.IsoCode, best.Lon, best.Lat, best.PixelX, best.PixelY, best.PopMax, best.ScaleRank, best.RasterCode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CITIES] Error detecting city at pixel ({pixelX}, {pixelY}): {ex.Message}");
                return null;
            }
        }
        public void SetSelectedState(StateBorderManager.StateFeature? state) => SelectState(state);
        public void RenderStateFills(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize) => _stateManager.RenderStateFills(canvas, viewport, mapPixelSize);
        public void RenderStateBorders(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize, float borderWidth = 1.0f, SKColor? borderColor = null) => _stateManager.RenderStateBorders(canvas, viewport, mapPixelSize, borderWidth, borderColor);
        public void ChangeStateControlAtGrid(int rasterCode, IEnumerable<Point> cells) { /* state editing disabled due to scaled grid */ }
        public void ChangeStateControlRect(int rasterCode, Rectangle region) { /* state editing disabled due to scaled grid */ }
        public List<(Point cell, int previousId)> ChangeStateControlZeroSum(int rasterCode, IEnumerable<Point> brushCells) => new List<(Point cell, int previousId)>();
        public List<(Point cell, int previousId)> ChangeStateControlWaterOnly(int rasterCode, IEnumerable<Point> brushCells) => new List<(Point cell, int previousId)>();
        public List<(Point cell, int previousId)> ChangeAdminControlZeroSum(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells) => level == MapViewLevel.Countries ? ChangeCountryControlZeroSum(rasterCode, brushCells) : new List<(Point cell, int previousId)>();
        public List<(Point cell, int previousId)> ChangeAdminControlWaterOnly(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells) => level == MapViewLevel.Countries ? new List<(Point cell, int previousId)>() : new List<(Point cell, int previousId)>();
        public void ChangeAdminControlRect(MapViewLevel level, int rasterCode, Rectangle region)
        {
            if (level == MapViewLevel.Countries)
            {
                ChangeCountryControlRect(rasterCode, region);
            }
            else
            {
                // State editing disabled with scaled grid; no-op
            }
        }
        
        /// <summary>
        /// Processes state/country border mismatches by splitting states and merging small fragments
        /// </summary>
        public void ProcessStateSplittingAndMerging(bool force = false, int[,]? providedCountryGrid = null)
        {
            if (_stateSplittingProcessed && !force)
            {
                Debug.WriteLine("[HYBRID MANAGER] State splitting already processed");
                return;
            }

            try
            {
                Debug.WriteLine("[HYBRID MANAGER] Starting state splitting and merging process...");

                // Get the country grid from the political tile manager
                var countryGrid = providedCountryGrid ?? _politicalTileManager.GetControlGrid();
                if (countryGrid != null)
                {
                    _stateManager.ProcessStateSplittingAndMerging(countryGrid);
                    _stateSplittingProcessed = true;
                    Debug.WriteLine("[HYBRID MANAGER] State splitting and merging completed");
                }
                else
                {
                    Debug.WriteLine("[HYBRID MANAGER] Could not get country grid for state processing");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HYBRID MANAGER] Error during state splitting: {ex.Message}");
            }
        }

        public Task<int> CullStatesWithoutCitiesAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CullStatesWithoutCitiesInternal(progress, cancellationToken), cancellationToken);
        }

        public int CullStatesWithoutCities()
        {
            return CullStatesWithoutCitiesInternal(progress: null, cancellationToken: CancellationToken.None);
        }

        public bool SaveStateMapToDisk(string? filePath = null)
        {
            return _stateManager.SaveStateDataToFile(filePath);
        }

        private sealed class StateGridStats
        {
            public long SumX;
            public long SumY;
            public int CellCount;
        }

        private int CullStatesWithoutCitiesInternal(IProgress<double>? progress, CancellationToken cancellationToken)
        {
            try
            {
                _stateManager.LoadStateData();
                EnsureCitiesLoaded();

                if (_cityPoints == null || _cityPoints.Count == 0)
                {
                    Debug.WriteLine("[HYBRID MANAGER] City data unavailable; skipping state cull");
                    return 0;
                }

                var states = _stateManager.GetAllStates();
                if (states == null || states.Count == 0)
                {
                    Debug.WriteLine("[HYBRID MANAGER] No states available for culling");
                    return 0;
                }

                var stateGrid = _stateManager.GetStateGrid();
                if (stateGrid == null)
                {
                    Debug.WriteLine("[HYBRID MANAGER] State grid unavailable; skipping cull");
                    return 0;
                }

                var stateByCode = states
                    .Where(s => s.RasterCode > 0)
                    .GroupBy(s => s.RasterCode)
                    .ToDictionary(g => g.Key, g => g.First());

                var cityCounts = new Dictionary<int, int>();
                foreach (var city in _cityPoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var state = _stateManager.GetStateAtGridScaled(city.PixelX, city.PixelY);
                    if (state == null)
                        continue;

                    if (cityCounts.TryGetValue(state.RasterCode, out var count))
                        cityCounts[state.RasterCode] = count + 1;
                    else
                        cityCounts[state.RasterCode] = 1;
                }

                var candidateStates = states.Where(s => cityCounts.ContainsKey(s.RasterCode)).ToList();
                if (candidateStates.Count == 0)
                {
                    Debug.WriteLine("[HYBRID MANAGER] No states with city assignments found; skipping cull");
                    return 0;
                }

                var emptyStates = states.Where(s => !cityCounts.ContainsKey(s.RasterCode)).ToList();
                if (emptyStates.Count == 0 && !_mergeSmallStatesWithCities)
                {
                    Debug.WriteLine("[HYBRID MANAGER] No states without cities detected");
                    return 0;
                }

                var gridStats = new Dictionary<int, StateGridStats>();
                var adjacency = new Dictionary<int, HashSet<int>>();
                var coverageByState = new Dictionary<int, Dictionary<int, int>>();
                int height = stateGrid.GetLength(0);
                int width = stateGrid.GetLength(1);
                var countryGrid = _politicalTileManager.GetControlGrid();

                if (countryGrid != null)
                {
                    // Reduce cross-border fragments before we start evaluating full-state merges so only the
                    // mismatched pieces are considered for reassignment.
                    ProcessStateSplittingAndMerging(force: true, providedCountryGrid: countryGrid);

                    stateGrid = _stateManager.GetStateGrid();
                    if (stateGrid == null)
                    {
                        Debug.WriteLine("[HYBRID MANAGER] State grid unavailable after splitting");
                        return 0;
                    }

                    height = stateGrid.GetLength(0);
                    width = stateGrid.GetLength(1);
                }

                int countryHeight = countryGrid?.GetLength(0) ?? 0;
                int countryWidth = countryGrid?.GetLength(1) ?? 0;

                for (int y = 0; y < height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    for (int x = 0; x < width; x++)
                    {
                        int code = stateGrid[y, x];
                        if (code <= 0)
                            continue;

                        if (!gridStats.TryGetValue(code, out var stats))
                        {
                            stats = new StateGridStats();
                            gridStats[code] = stats;
                        }

                        stats.CellCount++;
                        stats.SumX += x;
                        stats.SumY += y;

                        if (countryGrid != null && y < countryHeight && x < countryWidth)
                        {
                            int countryCode = countryGrid[y, x];
                            if (countryCode > 0)
                            {
                                if (!coverageByState.TryGetValue(code, out var coverage))
                                {
                                    coverage = new Dictionary<int, int>();
                                    coverageByState[code] = coverage;
                                }

                                if (coverage.TryGetValue(countryCode, out var count))
                                    coverage[countryCode] = count + 1;
                                else
                                    coverage[countryCode] = 1;
                            }
                        }

                        if (!adjacency.ContainsKey(code))
                        {
                            adjacency[code] = new HashSet<int>();
                        }

                        if (x + 1 < width)
                        {
                            int right = stateGrid[y, x + 1];
                            if (right > 0 && right != code)
                            {
                                AddAdjacency(adjacency, code, right);
                            }
                        }

                        if (y + 1 < height)
                        {
                            int down = stateGrid[y + 1, x];
                            if (down > 0 && down != code)
                            {
                                AddAdjacency(adjacency, code, down);
                            }
                        }
                    }
                }

                foreach (var state in stateByCode.Keys)
                {
                    if (!adjacency.ContainsKey(state))
                        adjacency[state] = new HashSet<int>();
                }

                var countryKeyCache = new Dictionary<int, string>();
                var coverageByStateKey = ConvertCoverageToCountryKeys(coverageByState, countryKeyCache);
                var effectiveCountryByState = new Dictionary<int, string>();
                var foreignMergeTargets = new Dictionary<int, Dictionary<string, double>>();
                PopulateEffectiveCountryAssignments(states, coverageByStateKey, effectiveCountryByState, foreignMergeTargets);

                var stateAreas = new Dictionary<int, double>();
                foreach (var state in states)
                {
                    double area = CalculateStateArea(state, gridStats);
                    stateAreas[state.RasterCode] = area;
                }

                var smallCityStates = new List<StateBorderManager.StateFeature>();
                if (_mergeSmallStatesWithCities && candidateStates.Count > 0)
                {
                    var sampleSizes = candidateStates
                        .Select(c => gridStats.TryGetValue(c.RasterCode, out var stats) ? stats.CellCount : 0)
                        .Where(count => count > 0)
                        .ToList();

                    if (sampleSizes.Count > 0)
                    {
                        double average = sampleSizes.Average();
                        int dynamicThreshold = (int)Math.Max(32, Math.Round(average * 0.35));
                        foreach (var candidate in candidateStates)
                        {
                            if (gridStats.TryGetValue(candidate.RasterCode, out var stats) && stats.CellCount > 0 && stats.CellCount <= dynamicThreshold)
                            {
                                smallCityStates.Add(candidate);
                            }
                        }

                        if (smallCityStates.Count > 0)
                        {
                            Debug.WriteLine($"[HYBRID MANAGER] Identified {smallCityStates.Count} small city state(s) (threshold {dynamicThreshold} cells) for merging");
                        }
                    }
                }

                var mergeableStates = new List<StateBorderManager.StateFeature>();
                if (emptyStates.Count > 0)
                    mergeableStates.AddRange(emptyStates);
                if (smallCityStates.Count > 0)
                    mergeableStates.AddRange(smallCityStates);

                if (mergeableStates.Count == 0)
                {
                    Debug.WriteLine("[HYBRID MANAGER] No eligible states found for merging");
                    return 0;
                }

                int maxCityCount = cityCounts.Count > 0 ? cityCounts.Values.Max() : 0;
                int totalMergeSources = mergeableStates.Count;
                int processed = 0;
                int culled = 0;

                var orderedMergeableStates = mergeableStates
                    .OrderBy(s => gridStats.TryGetValue(s.RasterCode, out var stats) ? stats.CellCount : int.MaxValue)
                    .ToList();

                var perCountryQueues = new Dictionary<string, ConcurrentQueue<StateBorderManager.StateFeature>>(StringComparer.OrdinalIgnoreCase);
                foreach (var mergeSource in orderedMergeableStates)
                {
                    string countryKey = GetEffectiveCountryKey(mergeSource, effectiveCountryByState);
                    if (!perCountryQueues.TryGetValue(countryKey, out var queue))
                    {
                        queue = new ConcurrentQueue<StateBorderManager.StateFeature>();
                        perCountryQueues[countryKey] = queue;
                    }
                    queue.Enqueue(mergeSource);
                }

                using var cullDataLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
                var tasks = new List<Task>();

                foreach (var kvp in perCountryQueues)
                {
                    if (kvp.Value.IsEmpty)
                        continue;

                    var queue = kvp.Value;
                    tasks.Add(Task.Run(() =>
                    {
                        while (!cancellationToken.IsCancellationRequested && queue.TryDequeue(out var mergeState))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            cullDataLock.EnterUpgradeableReadLock();
                            try
                            {
                                if (!stateByCode.ContainsKey(mergeState.RasterCode))
                                    continue;

                                var target = FindWeightedCullTarget(
                                    mergeState,
                                    candidateStates,
                                    cityCounts,
                                    adjacency,
                                    gridStats,
                                    stateByCode,
                                    maxCityCount,
                                    stateAreas,
                                    effectiveCountryByState,
                                    foreignMergeTargets);

                                if (target == null)
                                {
                                    Debug.WriteLine($"[HYBRID MANAGER] No merge target found for {mergeState.StateName} ({mergeState.CountryCode})");
                                }
                                else
                                {
                                    cullDataLock.EnterWriteLock();
                                    try
                                    {
                                        if (!stateByCode.TryGetValue(mergeState.RasterCode, out var refreshedSource))
                                            continue;

                                        if (!stateByCode.TryGetValue(target.RasterCode, out var refreshedTarget))
                                            continue;

                                        double sourceAreaBefore = GetOrCalculateStateArea(refreshedSource, stateAreas, gridStats);
                                        double targetAreaBefore = GetOrCalculateStateArea(refreshedTarget, stateAreas, gridStats);

                                        if (_stateManager.MergeStateInto(refreshedSource, refreshedTarget))
                                        {
                                            Interlocked.Increment(ref culled);
                                            Debug.WriteLine($"[HYBRID MANAGER] Merged {refreshedSource.StateName} into {refreshedTarget.StateName}");

                                            UpdateAdjacencyAfterMerge(adjacency, refreshedSource.RasterCode, refreshedTarget.RasterCode);
                                            UpdateGridStatsAfterMerge(gridStats, refreshedSource.RasterCode, refreshedTarget.RasterCode);
                                            stateAreas[refreshedTarget.RasterCode] = targetAreaBefore + sourceAreaBefore;
                                            stateAreas.Remove(refreshedSource.RasterCode);
                                            stateByCode.Remove(refreshedSource.RasterCode);
                                            effectiveCountryByState.Remove(refreshedSource.RasterCode);
                                            foreignMergeTargets.Remove(refreshedSource.RasterCode);

                                            if (cityCounts.TryGetValue(refreshedSource.RasterCode, out var transferredCities))
                                            {
                                                cityCounts.Remove(refreshedSource.RasterCode);
                                                if (transferredCities > 0)
                                                {
                                                    if (cityCounts.TryGetValue(refreshedTarget.RasterCode, out var targetCities))
                                                        cityCounts[refreshedTarget.RasterCode] = targetCities + transferredCities;
                                                    else
                                                        cityCounts[refreshedTarget.RasterCode] = transferredCities;

                                                    if (cityCounts[refreshedTarget.RasterCode] > maxCityCount)
                                                        maxCityCount = cityCounts[refreshedTarget.RasterCode];
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (cullDataLock.IsWriteLockHeld)
                                            cullDataLock.ExitWriteLock();
                                    }
                                }
                            }
                            finally
                            {
                                if (cullDataLock.IsUpgradeableReadLockHeld)
                                    cullDataLock.ExitUpgradeableReadLock();

                                int processedValue = Interlocked.Increment(ref processed);
                                if (totalMergeSources > 0)
                                {
                                    progress?.Report(processedValue / (double)totalMergeSources);
                                }
                            }
                        }
                    }, cancellationToken));
                }

                try
                {
                    Task.WaitAll(tasks.ToArray());
                }
                catch (AggregateException aex)
                {
                    aex.Handle(ex => ex is OperationCanceledException);
                    if (aex.InnerExceptions.Any(ex => ex is OperationCanceledException))
                        throw new OperationCanceledException(cancellationToken);
                    throw;
                }

                if (cancellationToken.IsCancellationRequested)
                    cancellationToken.ThrowIfCancellationRequested();

                if (culled > 0)
                {
                    _stateSplittingProcessed = false;
                }

                return culled;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[HYBRID MANAGER] State cull cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HYBRID MANAGER] Failed to cull empty states: {ex.Message}");
                return 0;
            }
        }

        private static void AddAdjacency(Dictionary<int, HashSet<int>> adjacency, int a, int b)
        {
            if (!adjacency.TryGetValue(a, out var setA))
            {
                setA = new HashSet<int>();
                adjacency[a] = setA;
            }
            setA.Add(b);

            if (!adjacency.TryGetValue(b, out var setB))
            {
                setB = new HashSet<int>();
                adjacency[b] = setB;
            }
            setB.Add(a);
        }

        private static void UpdateAdjacencyAfterMerge(Dictionary<int, HashSet<int>> adjacency, int sourceCode, int targetCode)
        {
            if (!adjacency.TryGetValue(targetCode, out var targetNeighbors))
            {
                targetNeighbors = new HashSet<int>();
                adjacency[targetCode] = targetNeighbors;
            }

            if (adjacency.TryGetValue(sourceCode, out var sourceNeighbors))
            {
                foreach (var neighbor in sourceNeighbors)
                {
                    if (neighbor == targetCode)
                        continue;

                    targetNeighbors.Add(neighbor);
                    if (adjacency.TryGetValue(neighbor, out var neighborSet))
                    {
                        neighborSet.Remove(sourceCode);
                        if (neighbor != targetCode)
                        {
                            neighborSet.Add(targetCode);
                        }
                    }
                }

                adjacency.Remove(sourceCode);
            }

            targetNeighbors.Remove(targetCode);
        }

        private static void UpdateGridStatsAfterMerge(Dictionary<int, StateGridStats> gridStats, int sourceCode, int targetCode)
        {
            if (!gridStats.TryGetValue(targetCode, out var targetStats))
            {
                targetStats = new StateGridStats();
                gridStats[targetCode] = targetStats;
            }

            if (gridStats.TryGetValue(sourceCode, out var sourceStats))
            {
                targetStats.SumX += sourceStats.SumX;
                targetStats.SumY += sourceStats.SumY;
                targetStats.CellCount += sourceStats.CellCount;
                gridStats.Remove(sourceCode);
            }
        }

        private static string NormalizeCountryKey(StateBorderManager.StateFeature? state)
        {
            if (state == null)
                return "__UNKNOWN__";

            var code = state.CountryCode;
            if (string.IsNullOrWhiteSpace(code))
                return "__UNKNOWN__";

            return code.Trim().ToUpperInvariant();
        }

        private static string GetEffectiveCountryKey(StateBorderManager.StateFeature? state, Dictionary<int, string> overrides)
        {
            if (state == null)
                return "__UNKNOWN__";

            if (overrides.TryGetValue(state.RasterCode, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            return NormalizeCountryKey(state);
        }

        private static string GetEffectiveCountryKey(int stateCode, Dictionary<int, string> overrides, Dictionary<int, StateBorderManager.StateFeature> stateByCode)
        {
            if (overrides.TryGetValue(stateCode, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            if (stateByCode.TryGetValue(stateCode, out var state))
                return NormalizeCountryKey(state);

            return "__UNKNOWN__";
        }

        private static double CalculateStateArea(StateBorderManager.StateFeature state, Dictionary<int, StateGridStats> gridStats)
        {
            if (gridStats.TryGetValue(state.RasterCode, out var stats) && stats.CellCount > 0)
            {
                return stats.CellCount;
            }

            var bounds = state.Bounds;
            double width = Math.Max(1.0, bounds.Width);
            double height = Math.Max(1.0, bounds.Height);
            return Math.Max(1.0, width * height);
        }

        private static double GetOrCalculateStateArea(
            StateBorderManager.StateFeature state,
            Dictionary<int, double> stateAreas,
            Dictionary<int, StateGridStats> gridStats)
        {
            if (stateAreas.TryGetValue(state.RasterCode, out var area) && area > 0)
                return area;

            double computed = CalculateStateArea(state, gridStats);
            stateAreas[state.RasterCode] = computed;
            return computed;
        }

        private StateBorderManager.StateFeature? FindWeightedCullTarget(
            StateBorderManager.StateFeature source,
            List<StateBorderManager.StateFeature> candidates,
            Dictionary<int, int> cityCounts,
            Dictionary<int, HashSet<int>> adjacency,
            Dictionary<int, StateGridStats> gridStats,
            Dictionary<int, StateBorderManager.StateFeature> stateByCode,
            int maxCityCount,
            Dictionary<int, double> stateAreas,
            Dictionary<int, string> effectiveCountryByState,
            Dictionary<int, Dictionary<string, double>> foreignMergeTargets)
        {
            if (source == null)
                return null;

            var neighborCodes = adjacency.TryGetValue(source.RasterCode, out var neighbors)
                ? neighbors.Where(code => code != source.RasterCode && cityCounts.ContainsKey(code) && stateByCode.ContainsKey(code)).ToList()
                : new List<int>();

            if (neighborCodes.Count == 0)
            {
                neighborCodes = candidates
                    .Where(c => c.RasterCode != source.RasterCode && cityCounts.ContainsKey(c.RasterCode))
                    .Select(c => c.RasterCode)
                    .Distinct()
                    .Where(code => stateByCode.ContainsKey(code))
                    .ToList();
            }

            if (neighborCodes.Count == 0)
                return null;

            gridStats.TryGetValue(source.RasterCode, out var sourceStats);
            _ = GetOrCalculateStateArea(source, stateAreas, gridStats);

            string sourceCountry = GetEffectiveCountryKey(source, effectiveCountryByState);
            foreignMergeTargets.TryGetValue(source.RasterCode, out var foreignCandidates);

            StateBorderManager.StateFeature? best = null;
            double bestScore = double.MinValue;

            foreach (var neighborCode in neighborCodes)
            {
                if (!stateByCode.TryGetValue(neighborCode, out var candidate))
                    continue;

                gridStats.TryGetValue(neighborCode, out var candidateStats);
                _ = GetOrCalculateStateArea(candidate, stateAreas, gridStats);

                double cityScore = 0.0;
                if (cityCounts.TryGetValue(neighborCode, out var count))
                {
                    cityScore = maxCityCount > 0 ? count / (double)maxCityCount : 0.0;
                }

                double distanceScore = CalculateDistanceScore(source, candidate, sourceStats, candidateStats);
                double areaScore = CalculateAreaSimilarityScore(sourceStats, candidateStats, source, candidate);
                bool adjacent = adjacency.TryGetValue(source.RasterCode, out var set) && set.Contains(neighborCode);
                double adjacencyScore = adjacent ? 1.0 : 0.0;

                string candidateCountry = GetEffectiveCountryKey(candidate, effectiveCountryByState);
                bool sameCountry = string.Equals(candidateCountry, sourceCountry, StringComparison.OrdinalIgnoreCase);
                double countryAffinity;

                if (sameCountry)
                {
                    countryAffinity = 1.0;
                }
                else
                {
                    double foreignShare = 0.0;
                    bool hasForeignShare = foreignCandidates != null && foreignCandidates.TryGetValue(candidateCountry, out foreignShare);

                    if (!hasForeignShare || foreignShare < 0.5)
                    {
                        // Only allow full-state merges into another country when the majority of the state's
                        // political coverage already lies with that country. Smaller overlaps should be handled
                        // by the fragment splitter instead of absorbing the entire state here.
                        continue;
                    }

                    // Weight affinity by how dominant the foreign country is for this state's coverage.
                    countryAffinity = 0.4 + Math.Min(0.5, foreignShare);
                }

                double closenessScore = distanceScore;
                if (adjacent)
                {
                    closenessScore = Math.Min(1.0, closenessScore + 0.25);
                }

                double score = (closenessScore * 0.6) + (adjacencyScore * 0.2) + (countryAffinity * 0.1) + (cityScore * 0.05) + (areaScore * 0.05);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private Dictionary<int, Dictionary<string, int>> ConvertCoverageToCountryKeys(
            Dictionary<int, Dictionary<int, int>> coverageByState,
            Dictionary<int, string> cache)
        {
            var result = new Dictionary<int, Dictionary<string, int>>();

            foreach (var kvp in coverageByState)
            {
                var stringCoverage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var inner in kvp.Value)
                {
                    int countryCode = inner.Key;
                    int count = inner.Value;
                    if (count <= 0 || countryCode <= 0)
                        continue;

                    if (!cache.TryGetValue(countryCode, out var key))
                    {
                        var feature = _politicalTileManager.GetCountryFeatureByRasterCode(countryCode);
                        key = feature?.CountryCode?.Trim().ToUpperInvariant();
                        if (string.IsNullOrWhiteSpace(key))
                            key = "__UNKNOWN__";
                        cache[countryCode] = key;
                    }

                    if (stringCoverage.TryGetValue(key, out var total))
                        stringCoverage[key] = total + count;
                    else
                        stringCoverage[key] = count;
                }

                if (stringCoverage.Count > 0)
                    result[kvp.Key] = stringCoverage;
            }

            return result;
        }

        private static void PopulateEffectiveCountryAssignments(
            IEnumerable<StateBorderManager.StateFeature> states,
            Dictionary<int, Dictionary<string, int>> coverageByState,
            Dictionary<int, string> effectiveCountryByState,
            Dictionary<int, Dictionary<string, double>> foreignMergeTargets)
        {
            foreach (var state in states)
            {
                string fallback = NormalizeCountryKey(state);
                if (!coverageByState.TryGetValue(state.RasterCode, out var coverage) || coverage.Count == 0)
                {
                    effectiveCountryByState[state.RasterCode] = fallback;
                    continue;
                }

                int totalCells = coverage.Values.Sum();
                if (totalCells <= 0)
                {
                    effectiveCountryByState[state.RasterCode] = fallback;
                    continue;
                }

                string effectiveKey = fallback;
                var sorted = coverage.OrderByDescending(k => k.Value).ToList();
                var top = sorted[0];
                double topShare = top.Value / (double)totalCells;
                coverage.TryGetValue(fallback, out var fallbackCount);

                if (string.Equals(fallback, "__UNKNOWN__", StringComparison.OrdinalIgnoreCase) || fallbackCount == 0 || topShare >= 0.6)
                {
                    effectiveKey = top.Key;
                }
                else if (!string.Equals(fallback, top.Key, StringComparison.OrdinalIgnoreCase) && topShare >= 0.45 && top.Value >= Math.Max(1, fallbackCount) * 1.2)
                {
                    effectiveKey = top.Key;
                }

                effectiveCountryByState[state.RasterCode] = effectiveKey;

                var foreignTargets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in sorted)
                {
                    if (string.Equals(kv.Key, effectiveKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    double share = kv.Value / (double)totalCells;
                    if (share >= 0.15 || (fallbackCount == 0 && share >= 0.05))
                    {
                        foreignTargets[kv.Key] = share;
                    }
                }

                if (foreignTargets.Count > 0)
                    foreignMergeTargets[state.RasterCode] = foreignTargets;
            }
        }

        private static double CalculateDistanceScore(
            StateBorderManager.StateFeature source,
            StateBorderManager.StateFeature candidate,
            StateGridStats? sourceStats,
            StateGridStats? candidateStats)
        {
            double sx;
            double sy;

            if (sourceStats != null && sourceStats.CellCount > 0)
            {
                sx = sourceStats.SumX / (double)sourceStats.CellCount;
                sy = sourceStats.SumY / (double)sourceStats.CellCount;
            }
            else
            {
                sx = source.Bounds.MidX;
                sy = source.Bounds.MidY;
            }

            double tx;
            double ty;

            if (candidateStats != null && candidateStats.CellCount > 0)
            {
                tx = candidateStats.SumX / (double)candidateStats.CellCount;
                ty = candidateStats.SumY / (double)candidateStats.CellCount;
            }
            else
            {
                tx = candidate.Bounds.MidX;
                ty = candidate.Bounds.MidY;
            }

            double dx = sx - tx;
            double dy = sy - ty;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));

            return 1.0 / (1.0 + distance);
        }

        private static double CalculateAreaSimilarityScore(
            StateGridStats? sourceStats,
            StateGridStats? candidateStats,
            StateBorderManager.StateFeature source,
            StateBorderManager.StateFeature candidate)
        {
            double sourceArea;
            double candidateArea;

            if (sourceStats != null && sourceStats.CellCount > 0)
            {
                sourceArea = sourceStats.CellCount;
            }
            else
            {
                sourceArea = Math.Max(1.0, source.Bounds.Width * source.Bounds.Height);
            }

            if (candidateStats != null && candidateStats.CellCount > 0)
            {
                candidateArea = candidateStats.CellCount;
            }
            else
            {
                candidateArea = Math.Max(1.0, candidate.Bounds.Width * candidate.Bounds.Height);
            }

            double maxArea = Math.Max(sourceArea, candidateArea);
            if (maxArea <= 0)
                return 0.0;

            double minArea = Math.Min(sourceArea, candidateArea);
            double similarity = minArea / maxArea;

            // Penalize extremely large disparities to discourage merging into massive states
            if (similarity < 0.1)
            {
                similarity *= 0.5;
            }

            return similarity;
        }

        public void EquilibrateStateBorders(int iterations = 2)
        {
            try
            {
                var countryGrid = _politicalTileManager.GetControlGrid();
                if (countryGrid == null)
                {
                    Debug.WriteLine("[HYBRID MANAGER] Cannot equilibrate states without a country grid");
                    return;
                }

                ProcessStateSplittingAndMerging(force: true, providedCountryGrid: countryGrid);
                iterations = Math.Clamp(iterations, 1, 8);
                _stateManager.RelaxStateBorders(countryGrid, iterations);
                Debug.WriteLine($"[HYBRID MANAGER] Equilibrated state borders with {iterations} relaxation pass(es)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HYBRID MANAGER] Failed to equilibrate state borders: {ex.Message}");
            }
        }
        
        public void Dispose() { _politicalTileManager?.Dispose(); _stateManager?.Dispose(); _populationDensityMap?.Dispose(); _populationDensityMap = null; }
        private static IEnumerable<(int dx, int dy)> GetSpiralOffsets(int radius)
        {
            yield return (0, 0);
            for (int r = 1; r <= radius; r++)
            {
                for (int dy = -r; dy <= r; dy++) { int dx = -r; yield return (dx, dy); }
                for (int dx = -r + 1; dx <= r; dx++) { int dy = r; yield return (dx, dy); }
                for (int dy = r - 1; dy >= -r; dy--) { int dx = r; yield return (dx, dy); }
                for (int dx = r - 1; dx >= -r + 1; dx--) { int dy = -r; yield return (dx, dy); }
            }
        }

        private List<CityPoint>? _cityPoints;
        private bool _citiesLoadAttempted = false;
        private readonly object _cityLock = new();
        private List<EconomyCityInfo> _economyCityInfos = new();
        
        // Store city label bounds for hover/click detection
        public record CityLabelBounds(string CityName, SKRect Bounds, object? CityObject);
        private List<CityLabelBounds> _lastRenderedCityLabels = new();

        public List<CityLabelBounds> GetCityLabelBounds()
        {
            lock (_cityLock)
            {
                return new List<CityLabelBounds>(_lastRenderedCityLabels);
            }
        }

        private record CityPoint(string IsoCode, float Lon, float Lat, int PopMax, int ScaleRank, int PixelX, int PixelY, int RasterCode, string Name);

        private void RenderCitiesOverlay(SKCanvas canvas, SKRectI terrainView, SKSizeI outputSize, int zoomLevel)
        {
            if (canvas == null) return;

            try
            {
                if (_selectedCountry == null)
                    return;

                EnsureCitiesLoaded();
                if (_cityPoints == null || _cityPoints.Count == 0)
                    return;

                if (outputSize.Width <= 0 || outputSize.Height <= 0)
                    return;

                int cellSize = GetCellSizeForZoom(zoomLevel);
                double terrainTotalWidthPx = BaseWidth * (double)cellSize;
                double terrainTotalHeightPx = BaseHeight * (double)cellSize;
                if (terrainTotalWidthPx <= 0 || terrainTotalHeightPx <= 0)
                    return;

                double politiToTerrainScaleX = terrainTotalWidthPx / PoliticalBaseWidth;
                double politiToTerrainScaleY = terrainTotalHeightPx / PoliticalBaseHeight;

                using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
                using var outlinePaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1f, Color = SKColors.Black, IsAntialias = true };
                using var textPaint = new SKPaint { IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Arial"), Color = SKColors.White };
                using var textBgPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 160), Style = SKPaintStyle.Fill };

                int targetRaster = _selectedCountry?.RasterCode ?? -1;
                string iso = _selectedCountry?.CountryCode?.ToUpperInvariant() ?? string.Empty;
                int drawn = 0;
                int considered = 0;
                int labeled = 0;

                // Simple collision list for labels
                List<SKRect> placedLabels = new();
                
                // Clear and prepare to store new city label bounds
                var newCityLabelBounds = new List<CityLabelBounds>();

                foreach (var city in _cityPoints)
                {
                    if (!string.Equals(city.IsoCode, iso, StringComparison.OrdinalIgnoreCase) && city.RasterCode != targetRaster)
                        continue;

                    considered++;

                    double terrainPx = city.PixelX * politiToTerrainScaleX;
                    double terrainPy = city.PixelY * politiToTerrainScaleY;

                    double screenX = terrainPx - terrainView.Left;
                    double screenY = terrainPy - terrainView.Top;
                    if (screenX < 0 || screenY < 0 || screenX >= outputSize.Width || screenY >= outputSize.Height)
                        continue;

                    float radius = 2f;
                    if (city.ScaleRank <= 2)
                        radius = 4f;
                    else if (city.ScaleRank <= 4)
                        radius = 3f;

                    if (city.PopMax > 5_000_000)
                        radius += 2f;
                    else if (city.PopMax > 1_000_000)
                        radius += 1f;

                    fillPaint.Color = city.ScaleRank <= 2
                        ? new SKColor(255, 220, 0, 220)
                        : new SKColor(255, 255, 255, 200);

                    canvas.DrawCircle((float)screenX, (float)screenY, radius, fillPaint);
                    canvas.DrawCircle((float)screenX, (float)screenY, radius, outlinePaint);
                    drawn++;

                    // Decide if we draw a label
                    if (!string.IsNullOrWhiteSpace(city.Name) && ShouldDrawCityLabel(city, zoomLevel))
                    {
                        float baseTextSize = zoomLevel switch
                        {
                            <= 1 => 10f,
                            2 => 11f,
                            3 => 12f,
                            4 => 13f,
                            5 => 14f,
                            6 => 16f,
                            _ => 18f
                        };
                        // Adjust by population lightly
                        if (city.PopMax > 10_000_000) baseTextSize += 3f;
                        else if (city.PopMax > 5_000_000) baseTextSize += 2f;
                        else if (city.PopMax > 1_000_000) baseTextSize += 1f;

                        textPaint.TextSize = baseTextSize;
                        var bounds = new SKRect();
                        textPaint.MeasureText(city.Name, ref bounds);
                        float labelPadX = 4f;
                        float labelPadY = 2f;
                        float offsetX = radius + 4f; // place label to right of dot
                        float labelX = (float)screenX + offsetX;
                        float labelY = (float)screenY - bounds.MidY; // vertically center
                        var bgRect = new SKRect(labelX - labelPadX, labelY + bounds.Top - labelPadY, labelX + bounds.Width + labelPadX, labelY + bounds.Bottom + labelPadY);

                        // Keep label fully in view (shift left if overflow)
                        if (bgRect.Right > outputSize.Width)
                        {
                            float shift = bgRect.Right - outputSize.Width;
                            bgRect.Offset(-shift, 0);
                            labelX -= shift;
                        }
                        if (bgRect.Left < 0)
                        {
                            float shift = -bgRect.Left;
                            bgRect.Offset(shift, 0);
                            labelX += shift;
                        }
                        if (bgRect.Top < 0)
                        {
                            float shift = -bgRect.Top;
                            bgRect.Offset(0, shift);
                            labelY += shift;
                        }
                        if (bgRect.Bottom > outputSize.Height)
                        {
                            float shift = bgRect.Bottom - outputSize.Height;
                            bgRect.Offset(0, -shift);
                            labelY -= shift;
                        }

                        // Collision check (allow tiny overlaps < 2px area ignored)
                        bool collides = placedLabels.Any(r => r.IntersectsWith(bgRect));
                        if (!collides)
                        {
                            canvas.DrawRect(bgRect, textBgPaint);
                            // Light shadow for readability
                            using var shadowPaint = textPaint.Clone();
                            shadowPaint.Color = new SKColor(0, 0, 0, 200);
                            canvas.DrawText(city.Name, labelX + 1, labelY + 1, shadowPaint);
                            canvas.DrawText(city.Name, labelX, labelY, textPaint);
                            placedLabels.Add(bgRect);
                            labeled++;

                            // Store label bounds for hover/click detection
                            // Try to find the corresponding economy city object
                            object? cityObj = _economyCityInfos.FirstOrDefault(c => c.CityName == city.Name);
                            newCityLabelBounds.Add(new CityLabelBounds(city.Name, bgRect, cityObj));
                        }
                    }
                }

                if (drawn == 0 && considered > 0)
                {
                    Debug.WriteLine($"[CITIES] No cities drawn for {iso} (raster {targetRaster}). Considered {considered} candidates. ViewArea={terrainView} cellSize={cellSize}");
                }
                else if (drawn > 0)
                {
                    Debug.WriteLine($"[CITIES] Drew {drawn} city dots (+{labeled} labels) for {iso} at zoom {zoomLevel}.");
                }

                // Store the city label bounds for this render
                lock (_cityLock)
                {
                    _lastRenderedCityLabels = newCityLabelBounds;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CITIES] Overlay failed: {ex.Message}");
            }
        }

        public void RegisterEconomyCities(IEnumerable<EconomyCityInfo> cityInfos)
        {
            lock (_cityLock)
            {
                _economyCityInfos = cityInfos?.ToList() ?? new List<EconomyCityInfo>();
                _cityPoints = null;
                _citiesLoadAttempted = false;
            }
        }

        private void EnsureCitiesLoaded()
        {
            if (_citiesLoadAttempted) return;
            lock (_cityLock)
            {
                if (_citiesLoadAttempted) return;
                _citiesLoadAttempted = true;

                try
                {
                    string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string dataDir = Path.Combine(baseDir, "data");
                    string shp = Path.Combine(dataDir, "ne_10m_populated_places.shp");
                    if (!File.Exists(shp))
                    {
                        shp = FindFileRecursive(dataDir, "ne_10m_populated_places.shp") ?? shp;
                    }

                    bool loadedFromShapefile = false;
                    if (File.Exists(shp))
                    {
                        try
                        {
                            try { OSGeo.OGR.Ogr.RegisterAll(); } catch { }
                            using var ds = OSGeo.OGR.Ogr.Open(shp, 0);
                            if (ds != null)
                            {
                                var layer = ds.GetLayerByIndex(0);
                                if (layer != null)
                                {
                                    var list = new List<CityPoint>(5000);
                                    int[,]? controlGrid = null;
                                    try { controlGrid = _politicalTileManager.GetControlGrid(); } catch { }
                                    layer.ResetReading();
                                    OSGeo.OGR.Feature feat;
                                    while ((feat = layer.GetNextFeature()) != null)
                                    {
                                        try
                                        {
                                            var geom = feat.GetGeometryRef();
                                            if (geom == null) continue;
                                            var gType = geom.GetGeometryType();
                                            if (gType != OSGeo.OGR.wkbGeometryType.wkbPoint && gType != OSGeo.OGR.wkbGeometryType.wkbPoint25D)
                                                continue;
                                            double lon = geom.GetX(0);
                                            double lat = geom.GetY(0);
                                            string iso = SafeString(feat, "ADM0_A3");
                                            if (string.IsNullOrWhiteSpace(iso)) iso = SafeString(feat, "ISO_A3");
                                            if (string.IsNullOrWhiteSpace(iso)) continue;
                                            int pop = SafeInt(feat, "POP_MAX");
                                            int scalerank = SafeInt(feat, "SCALERANK");
                                            string name = SafeString(feat, "NAMEASCII");
                                            if (string.IsNullOrWhiteSpace(name)) name = SafeString(feat, "NAME_EN");
                                            if (string.IsNullOrWhiteSpace(name)) name = SafeString(feat, "NAME");
                                            int px = (int)Math.Round((lon + 180.0) / 360.0 * (PoliticalBaseWidth - 1));
                                            int py = (int)Math.Round((90.0 - lat) / 180.0 * (PoliticalBaseHeight - 1));
                                            if (px < 0 || py < 0 || px >= PoliticalBaseWidth || py >= PoliticalBaseHeight) continue;
                                            int rasterCode = -1;
                                            if (controlGrid != null && py >= 0 && py < controlGrid.GetLength(0) && px >= 0 && px < controlGrid.GetLength(1))
                                                rasterCode = controlGrid[py, px];
                                            list.Add(new CityPoint(iso.ToUpperInvariant(), (float)lon, (float)lat, pop, scalerank, px, py, rasterCode, name));
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"[CITIES] Feature error: {ex.Message}");
                                        }
                                        finally { feat.Dispose(); }
                                    }

                                    if (list.Count > 0)
                                    {
                                        _cityPoints = list;
                                        loadedFromShapefile = true;
                                        Debug.WriteLine($"[CITIES] Loaded {list.Count} populated places from shapefile.");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CITIES] Shapefile loading failed: {ex.Message}");
                        }
                    }

                    if (!loadedFromShapefile)
                    {
                        _cityPoints = GenerateCityPointsFromEconomyData();
                        if (_cityPoints != null && _cityPoints.Count > 0)
                        {
                            Debug.WriteLine($"[CITIES] Generated {_cityPoints.Count} city anchor(s) from economy data.");
                        }
                        else
                        {
                            Debug.WriteLine("[CITIES] City data unavailable; overlays will be disabled.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CITIES] Load failed: {ex.Message}");
                    if (_cityPoints == null || _cityPoints.Count == 0)
                    {
                        _cityPoints = GenerateCityPointsFromEconomyData();
                    }
                }
            }
        }


        private static string? FindFileRecursive(string root, string targetName)
        {
            try
            {
                if (!Directory.Exists(root)) return null;
                return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), targetName, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        private List<CityPoint>? GenerateCityPointsFromEconomyData()
        {
            if (_economyCityInfos == null || _economyCityInfos.Count == 0)
                return null;

            var result = new List<CityPoint>();
            var statesByName = new Dictionary<string, StateBorderManager.StateFeature>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (_stateManager.GetAllStates().Count == 0)
                {
                    _stateManager.LoadStateData();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CITIES] Unable to load state data for economy fallback: {ex.Message}");
            }

            foreach (var state in _stateManager.GetAllStates())
            {
                if (!string.IsNullOrWhiteSpace(state.StateName))
                {
                    statesByName[state.StateName] = state;
                }
            }

            foreach (var group in _economyCityInfos.GroupBy(c => c.StateName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var descriptors = group.ToList();
                if (descriptors.Count == 0)
                    continue;

                statesByName.TryGetValue(group.Key, out var stateFeature);
                var placements = CreateCityPlacements(stateFeature, descriptors.Count, PoliticalBaseWidth, PoliticalBaseHeight);

                for (int i = 0; i < descriptors.Count; i++)
                {
                    var info = descriptors[i];
                    int pixelX;
                    int pixelY;
                    double lon;
                    double lat;

                    if (info.Longitude.HasValue && info.Latitude.HasValue)
                    {
                        var pixel = CoordinateTransform.GeographicToPixel(info.Longitude.Value, info.Latitude.Value, PoliticalBaseWidth, PoliticalBaseHeight);
                        pixelX = pixel.X;
                        pixelY = pixel.Y;
                        lon = info.Longitude.Value;
                        lat = info.Latitude.Value;
                    }
                    else
                    {
                        var placement = placements.Count > i ? placements[i] : (PoliticalBaseWidth / 2, PoliticalBaseHeight / 2);
                        pixelX = placement.Item1;
                        pixelY = placement.Item2;
                        (lon, lat) = CoordinateTransform.PixelToGeographic(pixelX, pixelY, PoliticalBaseWidth, PoliticalBaseHeight);
                    }

                    pixelX = Math.Clamp(pixelX, 0, PoliticalBaseWidth - 1);
                    pixelY = Math.Clamp(pixelY, 0, PoliticalBaseHeight - 1);

                    int population = info.Population > 0 ? info.Population : 100_000;
                    int scaleRank = EstimateScaleRank(population);
                    int rasterCode = stateFeature?.RasterCode ?? -1;
                    string iso = !string.IsNullOrWhiteSpace(stateFeature?.CountryCode)
                        ? stateFeature!.CountryCode
                        : info.CountryName ?? string.Empty;

                    result.Add(new CityPoint(
                        iso.ToUpperInvariant(),
                        (float)lon,
                        (float)lat,
                        population,
                        scaleRank,
                        pixelX,
                        pixelY,
                        rasterCode,
                        info.CityName));
                }
            }

            return result;
        }

        private static List<(int, int)> CreateCityPlacements(StateBorderManager.StateFeature? stateFeature, int cityCount, int politicalBaseWidth, int politicalBaseHeight)
        {
            var placements = new List<(int, int)>(cityCount);
            if (cityCount <= 0)
                return placements;

            SKRect bounds = stateFeature?.Bounds ?? SKRect.Create(0, 0, 0, 0);
            if (bounds.Width <= 1 || bounds.Height <= 1)
            {
                for (int i = 0; i < cityCount; i++)
                {
                    placements.Add((stateFeature != null ? (int)stateFeature.Bounds.MidX : politicalBaseWidth / 2,
                                     stateFeature != null ? (int)stateFeature.Bounds.MidY : politicalBaseHeight / 2));
                }
                return placements;
            }

            int columns = cityCount <= 2 ? 1 : cityCount <= 4 ? 2 : 3;
            int rows = (int)Math.Ceiling(cityCount / (double)columns);

            float marginX = Math.Clamp(bounds.Width * 0.15f, 4f, bounds.Width / 3f);
            float marginY = Math.Clamp(bounds.Height * 0.15f, 4f, bounds.Height / 3f);

            float usableWidth = Math.Max(2f, bounds.Width - marginX * 2f);
            float usableHeight = Math.Max(2f, bounds.Height - marginY * 2f);

            for (int index = 0; index < cityCount; index++)
            {
                int row = index / columns;
                int column = index % columns;
                float xFraction = columns == 1 ? 0.5f : column / (float)(columns - 1);
                float yFraction = rows == 1 ? 0.5f : row / (float)(rows - 1);

                float x = bounds.Left + marginX + usableWidth * xFraction;
                float y = bounds.Top + marginY + usableHeight * yFraction;

                placements.Add(((int)Math.Round(x), (int)Math.Round(y)));
            }

            return placements;
        }

        private static int EstimateScaleRank(int population)
        {
            if (population >= 5_000_000) return 1;
            if (population >= 2_500_000) return 2;
            if (population >= 1_000_000) return 3;
            if (population >= 500_000) return 4;
            if (population >= 250_000) return 5;
            if (population >= 100_000) return 6;
            return 7;
        }

        private static string SafeString(OSGeo.OGR.Feature f, string field)
        { try { int idx = f.GetFieldIndex(field); return idx >= 0 ? f.GetFieldAsString(idx) ?? string.Empty : string.Empty; } catch { return string.Empty; } }
        private static int SafeInt(OSGeo.OGR.Feature f, string field)
        { try { int idx = f.GetFieldIndex(field); return idx >= 0 ? f.GetFieldAsInteger(idx) : 0; } catch { return 0; } }
        
        private static bool ShouldDrawCityLabel(CityPoint city, int zoomLevel)
        {
            // Basic heuristic: show only largest cities at low zoom; more as you zoom in
            // ScaleRank: lower is more important
            if (zoomLevel <= 1)
                return city.ScaleRank <= 1 || city.PopMax >= 3_000_000;
            if (zoomLevel == 2)
                return city.ScaleRank <= 2 || city.PopMax >= 2_000_000;
            if (zoomLevel == 3)
                return city.ScaleRank <= 4 || city.PopMax >= 1_000_000;
            if (zoomLevel == 4)
                return city.ScaleRank <= 6 || city.PopMax >= 500_000;
            // high zoom: show almost everything but avoid very small settlements
            return city.PopMax >= 50_000 || city.ScaleRank <= 8;
        }
    }
}