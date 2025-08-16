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
        // Unified: manage states here
        private readonly StateBorderManager _stateManager;
        
        private MapViewType _currentViewType = MapViewType.Terrain;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        // Selected country tracking for white border highlighting
        private IndexedCountryFeature? _selectedCountry = null;
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        public IndexedCountryFeature? SelectedCountry => _selectedCountry;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        public event EventHandler<IndexedCountryFeature?>? SelectedCountryChanged;
        
        public HybridMapManager(int baseWidth = 4096, int baseHeight = 2048)
        {
            _terrainManager = new MultiResolutionMapManager(baseWidth, baseHeight);
            _politicalManager = new PoliticalBorderManager();
            _politicalTileManager = new PoliticalTileManager(_politicalManager, baseWidth, baseHeight);
            _stateManager = new StateBorderManager();
        }
        
        public void SetViewType(MapViewType viewType)
        {
            if (_currentViewType != viewType)
            {
                _currentViewType = viewType;
                ViewTypeChanged?.Invoke(this, viewType);
                
                // Clear terrain cache when switching to political to save memory
                if (viewType == MapViewType.Political)
                {
                    // Force garbage collection of terrain tiles to free memory
                    GC.Collect();
                }
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
                    // Convert screen pixel to map pixel (accounting for view offset)
                    int mapPixelX = mousePosition.X;
                    int mapPixelY = mousePosition.Y;
                    
                    // Get current map dimensions for this zoom level
                    int cellSize = GetCellSizeForZoom(zoomLevel);
                    int mapWidth = BaseWidth * cellSize;
                    int mapHeight = BaseHeight * cellSize;
                    
                    // Check bounds
                    if (mapPixelX < 0 || mapPixelX >= mapWidth || mapPixelY < 0 || mapPixelY >= mapHeight)
                    {
                        Debug.WriteLine($"Point ({mapPixelX}, {mapPixelY}) is outside map bounds ({mapWidth}x{mapHeight})");
                        return;
                    }
                    
                    // Convert map pixel to geographic coordinates
                    var (longitude, latitude) = CoordinateTransform.PixelToGeographic(mapPixelX, mapPixelY, mapWidth, mapHeight);
                    
                    Debug.WriteLine($"Highlight: Map ({mapPixelX},{mapPixelY}) -> Geo ({longitude:F4},{latitude:F4})");
                    
                    // Find country at this geographic location and select it directly
                    // This is better than using visual highlighting with red blocks/crosshairs
                    var country = _politicalTileManager.GetCountryAtGeographicPoint(longitude, latitude);
                    if (country != null)
                    {
                        // We won't call SelectCountry here as that will be done by the caller if needed
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
                    // Force synchronous rendering for political tiles to ensure they appear immediately
                    result = _politicalTileManager.AssembleView(zoomLevel, viewArea, onTileReady, forceSync: true);
                    break;
                
                default:
                    result = null;
                    break;
            }
            
            return result;
        }

        /// <summary>
        /// Unified rendering helper to draw either Countries or States into a bitmap of the given outputSize.
        /// </summary>
        public SKBitmap? RenderAdminBitmap(MapViewLevel level, int zoomLevel, SKRectI viewArea, SKSizeI outputSize)
        {
            switch (level)
            {
                case MapViewLevel.Countries:
                    return _politicalTileManager.AssembleView(zoomLevel, viewArea, null, forceSync: true);
                case MapViewLevel.States:
                    var bmp = new SKBitmap(outputSize.Width, outputSize.Height);
                    using (var canvas = new SKCanvas(bmp))
                    {
                        canvas.Clear(new SKColor(135, 206, 235)); // water background
                        var viewport = new SKRect(viewArea.Left, viewArea.Top, viewArea.Right, viewArea.Bottom);
                        var mapSize = GetMapSize(zoomLevel);
                        _stateManager.RenderStateFills(canvas, viewport, mapSize);
                        _stateManager.RenderStateBorders(canvas, viewport, mapSize, 2.0f, SKColors.Black);
                    }
                    return bmp;
                default:
                    return null;
            }
        }
        
        public SKSizeI GetMapSize(int zoomLevel)
        {
            return _terrainManager.GetMapSize(zoomLevel);
        }
        
        public int GetCellSizeForZoom(int zoomLevel)
        {
            return _terrainManager.GetCellSizeForZoom(zoomLevel);
        }
        
        public int BaseWidth => _terrainManager.BaseWidth;
        public int BaseHeight => _terrainManager.BaseHeight;

        /// <summary>
        /// Gets the country at a specific pixel position (accounting for zoom and pan).
        /// Includes a relaxed search radius around the cursor to make selection easier.
        /// </summary>
        public IndexedCountryFeature? GetCountryAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
        {
            try
            {
                // Validate inputs
                if (pixelX < 0 || pixelY < 0 || zoomLevel < 1)
                {
                    Debug.WriteLine($"Invalid input to GetCountryAtPixel: pixel=({pixelX},{pixelY}), zoom={zoomLevel}");
                    return null;
                }

                int cellSize = GetCellSizeForZoom(zoomLevel);
                int mapWidth = BaseWidth * cellSize;
                int mapHeight = BaseHeight * cellSize;

                // relaxed spiral search around the pointer
                const int radius = 5; // pixels
                foreach (var (dx, dy) in GetSpiralOffsets(radius))
                {
                    int mapPixelX = pixelX + viewOffset.X + dx;
                    int mapPixelY = pixelY + viewOffset.Y + dy;

                    if (mapPixelX < 0 || mapPixelX >= mapWidth || mapPixelY < 0 || mapPixelY >= mapHeight)
                        continue;

                    // Convert map pixel to geographic coordinates
                    var (longitude, latitude) = CoordinateTransform.PixelToGeographic(mapPixelX, mapPixelY, mapWidth, mapHeight);
                    
                    // Find country at this geographic location
                    var country = _politicalTileManager.GetCountryAtGeographicPoint(longitude, latitude);
                    if (country != null)
                    {
                        return country;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting country at pixel ({pixelX}, {pixelY}): {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Selects a country for highlighting with white borders
        /// </summary>
        public void SelectCountry(IndexedCountryFeature? country)
        {
            if (_selectedCountry != country)
            {
                _selectedCountry = country;
                
                // Notify the political tile manager about the selection change
                _politicalTileManager.SetSelectedCountry(country);
                
                // Notify listeners about the selection change
                SelectedCountryChanged?.Invoke(this, country);
                
                Debug.WriteLine($"Country selection changed: {(country != null ? $"{country.CountryName} ({country.CountryCode})" : "None")}");
            }
        }

        /// <summary>
        /// Clears the current country selection
        /// </summary>
        public void ClearCountrySelection()
        {
            SelectCountry(null);
        }

        // ----- New helpers for editor integration -----

        /// <summary>
        /// Returns all available country data (names, codes, raster codes) for the current date.
        /// </summary>
        public List<CachedCountryData> GetAllCountryData()
        {
            return _politicalTileManager.GetAllCountryData();
        }

        /// <summary>
        /// Finds a country by name (case-insensitive) and returns an IndexedCountryFeature with raster code.
        /// </summary>
        public IndexedCountryFeature? FindCountryByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var all = _politicalTileManager.GetAllCountryData();
            var match = all.Find(c => string.Equals(c.CountryName, name, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            // Convert to feature using political manager helper
            return _politicalTileManager.GetCountryFeatureByRasterCode(match.RasterCode);
        }

        /// <summary>
        /// Change control of specific grid cells to a given country raster code.
        /// </summary>
        public void ChangeCountryControlAtGrid(int rasterCode, IEnumerable<Point> cells)
        {
            _politicalTileManager.ChangeControl(rasterCode, cells);
        }

        // Optimized rectangle change for countries
        public void ChangeCountryControlRect(int rasterCode, Rectangle region)
        {
            IEnumerable<Point> Cells()
            {
                int x0 = Math.Max(0, region.Left);
                int y0 = Math.Max(0, region.Top);
                int x1 = Math.Min(BaseWidth, region.Right);
                int y1 = Math.Min(BaseHeight, region.Bottom);
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        yield return new Point(x, y);
            }
            _politicalTileManager.ChangeControl(rasterCode, Cells());
        }

        // Surface ChangeControlZeroSum from PoliticalTileManager
        public List<(Point cell, int previousId)> ChangeCountryControlZeroSum(int rasterCode, IEnumerable<Point> brushCells)
        {
            return _politicalTileManager.ChangeControlZeroSum(rasterCode, brushCells);
        }

        // ----- Unified State layer wrappers -----
        public List<StateBorderManager.StateFeature> GetAllStates()
        {
            return _stateManager.GetAllStates();
        }

        public StateBorderManager.StateFeature? GetStateAtPixel(int pixelX, int pixelY, int zoomLevel, SKPointI viewOffset)
        {
            return _stateManager.GetStateAtPixel(pixelX, pixelY, zoomLevel, viewOffset);
        }

        public void SetSelectedState(StateBorderManager.StateFeature? state)
        {
            _stateManager.SetSelectedState(state);
        }

        public void RenderStateFills(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize)
        {
            _stateManager.RenderStateFills(canvas, viewport, mapPixelSize);
        }

        public void RenderStateBorders(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize, float borderWidth = 1.0f, SKColor? borderColor = null)
        {
            _stateManager.RenderStateBorders(canvas, viewport, mapPixelSize, borderWidth, borderColor);
        }

        public void ChangeStateControlAtGrid(int rasterCode, IEnumerable<Point> cells)
        {
            _stateManager.ChangeControlAtGrid(rasterCode, cells);
        }

        public void ChangeStateControlRect(int rasterCode, Rectangle region)
        {
            _stateManager.ChangeControlRect(rasterCode, region);
        }

        public List<(Point cell, int previousId)> ChangeStateControlZeroSum(int rasterCode, IEnumerable<Point> brushCells)
        {
            return _stateManager.ChangeControlZeroSum(rasterCode, brushCells);
        }

        public List<(Point cell, int previousId)> ChangeStateControlWaterOnly(int rasterCode, IEnumerable<Point> brushCells)
        {
            return _stateManager.ChangeControlWaterOnly(rasterCode, brushCells);
        }

        // ----- Unified overloads for both levels -----
        public void ChangeAdminControlAtGrid(MapViewLevel level, int rasterCode, IEnumerable<Point> cells)
        {
            if (level == MapViewLevel.Countries) ChangeCountryControlAtGrid(rasterCode, cells);
            else ChangeStateControlAtGrid(rasterCode, cells);
        }

        public void ChangeAdminControlRect(MapViewLevel level, int rasterCode, Rectangle region)
        {
            if (level == MapViewLevel.Countries) ChangeCountryControlRect(rasterCode, region);
            else ChangeStateControlRect(rasterCode, region);
        }

        public List<(Point cell, int previousId)> ChangeAdminControlZeroSum(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells)
        {
            return level == MapViewLevel.Countries
                ? ChangeCountryControlZeroSum(rasterCode, brushCells)
                : ChangeStateControlZeroSum(rasterCode, brushCells);
        }

        public List<(Point cell, int previousId)> ChangeAdminControlWaterOnly(MapViewLevel level, int rasterCode, IEnumerable<Point> brushCells)
        {
            return level == MapViewLevel.Countries
                ? new List<(Point cell, int previousId)>() // countries don't use water-only path
                : ChangeStateControlWaterOnly(rasterCode, brushCells);
        }

        public void Dispose()
        {
            _politicalTileManager?.Dispose();
            _stateManager?.Dispose();
            // Note: MultiResolutionMapManager doesn't implement IDisposable
            // _terrainManager?.Dispose();
        }

        private static IEnumerable<(int dx, int dy)> GetSpiralOffsets(int radius)
        {
            yield return (0, 0);
            for (int r = 1; r <= radius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int dx = -r;
                    yield return (dx, dy);
                }
                for (int dx = -r + 1; dx <= r; dx++)
                {
                    int dy = r;
                    yield return (dx, dy);
                }
                for (int dy = r - 1; dy >= -r; dy--)
                {
                    int dx = r;
                    yield return (dx, dy);
                }
                for (int dx = r - 1; dx >= -r + 1; dx--)
                {
                    int dy = -r;
                    yield return (dx, dy);
                }
            }
        }
    }
}