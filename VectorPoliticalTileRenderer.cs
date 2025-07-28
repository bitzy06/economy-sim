using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using NetTopologySuite.Geometries;

namespace StrategyGame
{
    /// <summary>
    /// Vector-based political tile renderer with GPU acceleration and customizable styling
    /// </summary>
    public class VectorPoliticalTileRenderer : VectorTileRenderer
    {
        private readonly PoliticalBorderManager _politicalManager;
        
        // Political styling themes
        private readonly Dictionary<string, PoliticalTheme> _themes = new();
        private string _currentTheme = "Default";
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        public VectorPoliticalTileRenderer(PoliticalBorderManager politicalManager, int baseWidth, int baseHeight) 
            : base(baseWidth, baseHeight)
        {
            _politicalManager = politicalManager;
            InitializePoliticalThemes();
        }
        
        private void InitializePoliticalThemes()
        {
            // Default political theme
            _themes["Default"] = new PoliticalTheme
            {
                Name = "Default",
                WaterColor = new SKColor(200, 230, 255), // Light blue
                BorderColor = new SKColor(100, 100, 100), // Dark gray
                BorderWidth = 1.0f,
                ShowBorders = true,
                ShowCountryLabels = false,
                CountryColors = GenerateDistinctCountryColors()
            };
            
            // High contrast theme
            _themes["HighContrast"] = new PoliticalTheme
            {
                Name = "HighContrast",
                WaterColor = new SKColor(0, 0, 255), // Pure blue
                BorderColor = new SKColor(0, 0, 0), // Black
                BorderWidth = 2.0f,
                ShowBorders = true,
                ShowCountryLabels = false,
                CountryColors = new Dictionary<string, SKColor>()
            };
            
            // Minimal theme
            _themes["Minimal"] = new PoliticalTheme
            {
                Name = "Minimal",
                WaterColor = new SKColor(245, 245, 245), // Light gray
                BorderColor = new SKColor(180, 180, 180), // Medium gray
                BorderWidth = 0.5f,
                ShowBorders = true,
                ShowCountryLabels = false,
                CountryColors = new Dictionary<string, SKColor>()
            };
            
            // Dark theme
            _themes["Dark"] = new PoliticalTheme
            {
                Name = "Dark",
                WaterColor = new SKColor(30, 30, 30), // Dark gray
                BorderColor = new SKColor(100, 100, 100), // Medium gray
                BorderWidth = 1.0f,
                ShowBorders = true,
                ShowCountryLabels = false,
                CountryColors = new Dictionary<string, SKColor>()
            };
        }
        
        public void SetTheme(string themeName)
        {
            if (_themes.ContainsKey(themeName))
            {
                _currentTheme = themeName;
                Debug.WriteLine($"Political theme changed to: {themeName}");
                
                // Clear cache to force re-rendering with new theme
                lock (_cacheLock)
                {
                    _vectorTileCache.Clear();
                }
            }
        }
        
        public string[] GetAvailableThemes()
        {
            return _themes.Keys.ToArray();
        }
        
        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;
                
                // Clear cache when date changes
                lock (_cacheLock)
                {
                    _vectorTileCache.Clear();
                }
            }
        }
        
        protected override string GetCacheKey(int cellSize, int tileX, int tileY)
        {
            return $"political_{cellSize}_{tileX}_{tileY}_{_politicalMapDate:yyyyMMdd}_{_currentTheme}";
        }
        
        protected override async Task<VectorTile?> LoadVectorDataForTile(int cellSize, int tileX, int tileY)
        {
            await Task.Yield(); // Make this properly async
            
            try
            {
                // Calculate tile bounds in pixel space
                int scaledMapWidth = _baseWidth * cellSize;
                int scaledMapHeight = _baseHeight * cellSize;
                
                int pixelX = tileX * TileSizePx;
                int pixelY = tileY * TileSizePx;
                
                int tileWidth = Math.Min(TileSizePx, scaledMapWidth - pixelX);
                int tileHeight = Math.Min(TileSizePx, scaledMapHeight - pixelY);
                
                if (tileWidth <= 0 || tileHeight <= 0)
                {
                    return null;
                }
                
                var vectorTile = new VectorTile
                {
                    TileX = tileX,
                    TileY = tileY,
                    CellSize = cellSize,
                    Bounds = new SKRect(pixelX, pixelY, pixelX + tileWidth, pixelY + tileHeight)
                };
                
                // Generate simple procedural political boundaries
                GenerateProceduralCountries(vectorTile, pixelX, pixelY, tileWidth, tileHeight);
                
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading vector data for political tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        private void GenerateProceduralCountries(VectorTile vectorTile, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            var theme = _themes[_currentTheme];
            var geometryFactory = new GeometryFactory();
            
            // Create a simple grid pattern with different countries
            // This ensures every tile has visible political boundaries
            int cellSize = 128; // Size of each country cell in pixels
            
            // Define country colors for easy debugging
            var countryColors = new SKColor[]
            {
                new SKColor(255, 0, 0),     // Red
                new SKColor(0, 255, 0),     // Green
                new SKColor(0, 0, 255),     // Blue
                new SKColor(255, 255, 0),   // Yellow
                new SKColor(255, 0, 255),   // Magenta
                new SKColor(0, 255, 255),   // Cyan
                new SKColor(255, 128, 0),   // Orange
                new SKColor(128, 0, 255)    // Purple
            };
            
            for (int y = 0; y < tileHeight; y += cellSize)
            {
                for (int x = 0; x < tileWidth; x += cellSize)
                {
                    int cellWidth = Math.Min(cellSize, tileWidth - x);
                    int cellHeight = Math.Min(cellSize, tileHeight - y);
                    
                    // Use a simple pattern to determine country
                    int gridX = x / cellSize;
                    int gridY = y / cellSize;
                    int countryIndex = (gridX + gridY * 2) % countryColors.Length;
                    
                    // Create country polygon in local tile coordinates
                    var countryCoords = new[]
                    {
                        new Coordinate(x, y),
                        new Coordinate(x + cellWidth, y),
                        new Coordinate(x + cellWidth, y + cellHeight),
                        new Coordinate(x, y + cellHeight),
                        new Coordinate(x, y)
                    };
                    
                    var polygon = geometryFactory.CreatePolygon(countryCoords);
                    
                    var feature = new VectorFeature
                    {
                        Geometry = polygon,
                        Properties = new Dictionary<string, object>
                        {
                            ["countryCode"] = $"C{countryIndex:D2}",
                            ["countryName"] = $"Country {countryIndex + 1}",
                            ["gridX"] = gridX,
                            ["gridY"] = gridY
                        },
                        Style = new VectorStyle
                        {
                            FillColor = countryColors[countryIndex],
                            StrokeColor = new SKColor(0, 0, 0, 255), // Black border
                            StrokeWidth = 2,
                            IsVisible = true,
                            Opacity = 0.8f
                        }
                    };
                    
                    vectorTile.Features.Add(feature);
                }
            }
            
            Debug.WriteLine($"Generated {vectorTile.Features.Count} country features for political tile ({vectorTile.TileX}, {vectorTile.TileY})");
        }
        
        protected override void RenderVectorTile(SKCanvas canvas, VectorTile vectorTile, int destX, int destY, int cellSize)
        {
            var theme = _themes[_currentTheme];
            
            canvas.Save();
            canvas.Translate(destX, destY);
            
            try
            {
                Debug.WriteLine($"Rendering {vectorTile.Features.Count} political features for tile ({vectorTile.TileX}, {vectorTile.TileY})");
                
                int featuresRendered = 0;
                
                // Render country fills first
                foreach (var feature in vectorTile.Features)
                {
                    if (!feature.Style.IsVisible || feature.Geometry == null)
                        continue;
                    
                    RenderCountryFill(canvas, feature, vectorTile.Bounds);
                    featuresRendered++;
                }
                
                // Render borders on top if enabled
                if (theme.ShowBorders)
                {
                    foreach (var feature in vectorTile.Features)
                    {
                        if (!feature.Style.IsVisible || feature.Geometry == null)
                            continue;
                        
                        RenderCountryBorder(canvas, feature, vectorTile.Bounds);
                    }
                }
                
                Debug.WriteLine($"Rendered {featuresRendered} political features for tile ({vectorTile.TileX}, {vectorTile.TileY})");
            }
            finally
            {
                canvas.Restore();
            }
        }
        
        private void RenderCountryFill(SKCanvas canvas, VectorFeature feature, SKRect tileBounds)
        {
            if (feature.Geometry is Polygon polygon)
            {
                var path = CreatePathFromGeometry(polygon, tileBounds);
                if (path != null)
                {
                    using (path)
                    {
                        if (feature.Style.FillColor.Alpha > 0)
                        {
                            using var fillPaint = feature.Style.CreateFillPaint();
                            canvas.DrawPath(path, fillPaint);
                        }
                    }
                }
            }
        }
        
        private void RenderCountryBorder(SKCanvas canvas, VectorFeature feature, SKRect tileBounds)
        {
            if (feature.Style.StrokeWidth <= 0 || feature.Style.StrokeColor.Alpha == 0)
                return;
            
            if (feature.Geometry is Polygon polygon)
            {
                var path = CreatePathFromGeometry(polygon, tileBounds);
                if (path != null)
                {
                    using (path)
                    using (var strokePaint = feature.Style.CreateStrokePaint())
                    {
                        canvas.DrawPath(path, strokePaint);
                    }
                }
            }
        }
        
        private SKPath? CreatePathFromGeometry(Polygon polygon, SKRect tileBounds)
        {
            var path = new SKPath();
            
            try
            {
                // The coordinates are already in tile-local space (0 to TileSize)
                // No need to transform them relative to tileBounds
                var coords = polygon.ExteriorRing.Coordinates;
                if (coords.Length < 4) return null;
                
                bool first = true;
                foreach (var coord in coords)
                {
                    float x = (float)coord.X;
                    float y = (float)coord.Y;
                    
                    if (first)
                    {
                        path.MoveTo(x, y);
                        first = false;
                    }
                    else
                    {
                        path.LineTo(x, y);
                    }
                }
                
                path.Close();
                return path;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating path from geometry: {ex.Message}");
                path.Dispose();
                return null;
            }
        }
        
        protected override SKColor GetBackgroundColor()
        {
            // Use a neutral background for political maps too
            return new SKColor(250, 250, 250); // Very light gray background
        }
        
        private Dictionary<string, SKColor> GenerateDistinctCountryColors()
        {
            // Pre-generate some distinct colors for major countries
            return new Dictionary<string, SKColor>
            {
                ["USA"] = new SKColor(173, 216, 230),    // Light blue
                ["CAN"] = new SKColor(255, 182, 193),    // Light pink
                ["MEX"] = new SKColor(255, 255, 224),    // Light yellow
                ["BRA"] = new SKColor(144, 238, 144),    // Light green
                ["ARG"] = new SKColor(255, 218, 185),    // Peach
                ["CHN"] = new SKColor(255, 160, 122),    // Light salmon
                ["RUS"] = new SKColor(221, 160, 221),    // Plum
                ["IND"] = new SKColor(175, 238, 238),    // Pale turquoise
                ["IDN"] = new SKColor(255, 228, 181),    // Moccasin
                ["GBR"] = new SKColor(230, 230, 250),    // Lavender
                ["FRA"] = new SKColor(255, 240, 245),    // Lavender blush
                ["DEU"] = new SKColor(240, 248, 255),    // Alice blue
                ["ITA"] = new SKColor(255, 250, 240),    // Floral white
                ["ESP"] = new SKColor(253, 245, 230),    // Old lace
                ["JPN"] = new SKColor(255, 239, 213),    // Papaya whip
                ["KOR"] = new SKColor(250, 235, 215),    // Antique white
                ["AUS"] = new SKColor(245, 255, 250),    // Mint cream
                ["UNK"] = new SKColor(220, 220, 220)     // Light gray for unknown
            };
        }
    }
    
    /// <summary>
    /// Customizable political theme for vector rendering
    /// </summary>
    public class PoliticalTheme
    {
        public string Name { get; set; } = string.Empty;
        public SKColor WaterColor { get; set; }
        public SKColor BorderColor { get; set; }
        public float BorderWidth { get; set; }
        public Dictionary<string, SKColor> CountryColors { get; set; } = new();
        public bool ShowBorders { get; set; } = true;
        public bool ShowCountryLabels { get; set; } = false;
    }
}