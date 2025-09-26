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

        private record CityPoint(string IsoCode, float Lon, float Lat, int PopMax, int ScaleRank, int PixelX, int PixelY, int RasterCode);

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

                int targetRaster = _selectedCountry?.RasterCode ?? -1;
                string iso = _selectedCountry?.CountryCode?.ToUpperInvariant() ?? string.Empty;
                int drawn = 0;
                int considered = 0;

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
                }

                if (drawn == 0 && considered > 0)
                {
                    Debug.WriteLine($"[CITIES] No cities drawn for {iso} (raster {targetRaster}). Considered {considered} candidates. ViewArea={terrainView} cellSize={cellSize}");
                }
                else if (drawn > 0)
                {
                    Debug.WriteLine($"[CITIES] Drew {drawn} city dots for {iso} at zoom {zoomLevel}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CITIES] Overlay failed: {ex.Message}");
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
                    if (!File.Exists(shp))
                    {
                        Debug.WriteLine($"[CITIES] Populated places shapefile not found at {shp}");
                        return;
                    }
                    // Configure OGR only (we avoid GDAL raster pieces to prevent missing symbol errors)
                    try { OSGeo.OGR.Ogr.RegisterAll(); } catch { }
                    using var ds = OSGeo.OGR.Ogr.Open(shp, 0);
                    if (ds == null) { Debug.WriteLine("[CITIES] Failed to open shapefile"); return; }
                    var layer = ds.GetLayerByIndex(0);
                    if (layer == null) { Debug.WriteLine("[CITIES] Layer missing"); return; }
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
                            int px = (int)Math.Round((lon + 180.0) / 360.0 * (PoliticalBaseWidth - 1));
                            int py = (int)Math.Round((90.0 - lat) / 180.0 * (PoliticalBaseHeight - 1));
                            if (px < 0 || py < 0 || px >= PoliticalBaseWidth || py >= PoliticalBaseHeight) continue;
                            int rasterCode = -1;
                            if (controlGrid != null && py >= 0 && py < controlGrid.GetLength(0) && px >= 0 && px < controlGrid.GetLength(1))
                                rasterCode = controlGrid[py, px];
                            list.Add(new CityPoint(iso.ToUpperInvariant(), (float)lon, (float)lat, pop, scalerank, px, py, rasterCode));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CITIES] Feature error: {ex.Message}");
                        }
                        finally { feat.Dispose(); }
                    }
                    _cityPoints = list;
                    Debug.WriteLine($"[CITIES] Loaded {list.Count} populated places (with raster sampling {(controlGrid!=null ? "enabled" : "disabled")})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CITIES] Load failed: {ex.Message}");
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

        private static string SafeString(OSGeo.OGR.Feature f, string field)
        { try { int idx = f.GetFieldIndex(field); return idx >= 0 ? f.GetFieldAsString(idx) ?? string.Empty : string.Empty; } catch { return string.Empty; } }
        private static int SafeInt(OSGeo.OGR.Feature f, string field)
        { try { int idx = f.GetFieldIndex(field); return idx >= 0 ? f.GetFieldAsInteger(idx) : 0; } catch { return 0; } }
    }
}