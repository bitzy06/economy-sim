using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Drawing;

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
        private bool _stateSplittingProcessed = false;
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        public IndexedCountryFeature? SelectedCountry => _selectedCountry;
        public StateBorderManager.StateFeature? SelectedState => _selectedState;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        public event EventHandler<IndexedCountryFeature?>? SelectedCountryChanged;
        public event EventHandler<StateBorderManager.StateFeature?>? SelectedStateChanged;
        
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
                        result = resized;
                    }
                    else
                    {
                        result = polBmp;
                    }
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

        private (int px, int py) TerrainPixelToPoliticalPixel(int terrainX, int terrainY, int zoomLevel)
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
                _selectedState = state;
                _stateManager.SetSelectedState(state);
                _politicalTileManager.SetSelectedState(state);
                SelectedStateChanged?.Invoke(this, state);
                Debug.WriteLine($"State selection changed: {(state != null ? $"{state.StateName} in {state.CountryName}" : "None")}");
            }
        }
        
        public void ClearCountrySelection() => SelectCountry(null);
        public void ClearStateSelection() => SelectState(null);

        public List<CachedCountryData> GetAllCountryData() => _politicalTileManager.GetAllCountryData();
        public PoliticalDataCache GetPoliticalDataCache() => _politicalTileManager.GetDataCache();
        public IndexedCountryFeature? FindCountryByName(string name)
        { if (string.IsNullOrWhiteSpace(name)) return null; var all = _politicalTileManager.GetAllCountryData(); var match = all.Find(c => string.Equals(c.CountryName, name, StringComparison.OrdinalIgnoreCase)); return match == null ? null : _politicalTileManager.GetCountryFeatureByRasterCode(match.RasterCode); }
        public void ChangeCountryControlAtGrid(int rasterCode, IEnumerable<Point> cells) => _politicalTileManager.ChangeControl(rasterCode, cells);
        public void ChangeCountryControlRect(int rasterCode, Rectangle region)
        { IEnumerable<Point> Cells() { int x0 = Math.Max(0, region.Left); int y0 = Math.Max(0, region.Top); int x1 = Math.Min(PoliticalBaseWidth, region.Right); int y1 = Math.Min(PoliticalBaseHeight, region.Bottom); for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) yield return new Point(x, y); } _politicalTileManager.ChangeControl(rasterCode, Cells()); }
        public List<(Point cell, int previousId)> ChangeCountryControlZeroSum(int rasterCode, IEnumerable<Point> brushCells) => _politicalTileManager.ChangeControlZeroSum(rasterCode, brushCells);
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
                foreach (var (dx, dy) in GetSpiralOffsets(radius)) { var st = _stateManager.GetStateAtGrid(gx + dx, gy + dy); if (st != null) return st; }
                return null;
            }
            catch (Exception ex) { Debug.WriteLine($"Error detecting state at pixel ({pixelX}, {pixelY}): {ex.Message}"); return null; }
        }
        public void SetSelectedState(StateBorderManager.StateFeature? state) => SelectState(state);
        public void RenderStateFills(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize) => _stateManager.RenderStateFills(canvas, viewport, mapPixelSize);
        public void RenderStateBorders(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize, float borderWidth = 1.0f, SKColor? borderColor = null) => _stateManager.RenderStateBorders(canvas, viewport, mapPixelSize, borderWidth, borderColor);
        public void ChangeStateControlAtGrid(int rasterCode, IEnumerable<Point> cells) => _stateManager.ChangeControlAtGrid(rasterCode, cells);
        public void ChangeStateControlRect(int rasterCode, Rectangle region) => _stateManager.ChangeControlRect(rasterCode, region);
        public List<(Point cell, int previousId)> ChangeStateControlZeroSum(int rasterCode, IEnumerable<Point> brushCells) => _stateManager.ChangeControlZeroSum(rasterCode, brushCells);
        public List<(Point cell, int previousId)> ChangeStateControlWaterOnly(int rasterCode, IEnumerable<Point> brushCells) => _stateManager.ChangeControlWaterOnly(rasterCode, brushCells);
        public void ChangeAdminControlAtGrid(MapViewLevel level, int rasterCode, IEnumerable<Point> cells) { if (level == MapViewLevel.Countries) ChangeCountryControlAtGrid(rasterCode, cells); else ChangeStateControlAtGrid(rasterCode, cells); }
        public void ChangeAdminControlRect(MapViewLevel level, int rasterCode, Rectangle region) { if (level == MapViewLevel.Countries) ChangeCountryControlRect(rasterCode, region); else ChangeStateControlRect(rasterCode, region); }
        public List<(Point cell, int previousId)> ChangeAdminControlZeroSum(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells) => level == MapViewLevel.Countries ? ChangeCountryControlZeroSum(rasterCode, brushCells) : ChangeStateControlZeroSum(rasterCode, brushCells);
        public List<(Point cell, int previousId)> ChangeAdminControlWaterOnly(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells) => level == MapViewLevel.Countries ? new List<(Point cell, int previousId)>() : ChangeStateControlWaterOnly(rasterCode, brushCells);
        
        /// <summary>
        /// Processes state/country border mismatches by splitting states and merging small fragments
        /// </summary>
        public void ProcessStateSplittingAndMerging()
        {
            if (_stateSplittingProcessed)
            {
                Debug.WriteLine("[HYBRID MANAGER] State splitting already processed");
                return;
            }
            
            try
            {
                Debug.WriteLine("[HYBRID MANAGER] Starting state splitting and merging process...");
                
                // Get the country grid from the political tile manager
                var countryGrid = _politicalTileManager.GetControlGrid();
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
    }
}