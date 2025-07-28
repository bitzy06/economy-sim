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
    /// Vector-based political boundary tile renderer with GPU acceleration and customizable styling
    /// </summary>
    public class VectorPoliticalTileRenderer : VectorTileRenderer
    {
        private readonly PoliticalBorderManager _politicalManager;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        // Political styling themes
        private readonly Dictionary<string, PoliticalTheme> _themes = new();
        private string _currentTheme = "Default";
        
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
                WaterColor = new SKColor(135, 206, 235),     // Light sky blue
                BorderColor = new SKColor(80, 80, 80),       // Dark gray borders
                BorderWidth = 1.5f,
                CountryColors = GenerateDistinctCountryColors(),
                ShowBorders = true,
                ShowCountryLabels = false
            };
            
            // High contrast theme
            _themes["HighContrast"] = new PoliticalTheme
            {
                Name = "HighContrast",
                WaterColor = new SKColor(0, 0, 255),         // Pure blue
                BorderColor = new SKColor(255, 255, 255),    // White borders  
                BorderWidth = 2.0f,
                CountryColors = GenerateHighContrastColors(),
                ShowBorders = true,
                ShowCountryLabels = true
            };
            
            // Minimal theme
            _themes["Minimal"] = new PoliticalTheme
            {
                Name = "Minimal",
                WaterColor = new SKColor(240, 248, 255),     // Alice blue
                BorderColor = new SKColor(105, 105, 105),    // Dim gray
                BorderWidth = 0.8f,
                CountryColors = GenerateMinimalColors(),
                ShowBorders = true,
                ShowCountryLabels = false
            };
            
            // Dark theme
            _themes["Dark"] = new PoliticalTheme
            {
                Name = "Dark",
                WaterColor = new SKColor(25, 25, 112),       // Midnight blue
                BorderColor = new SKColor(128, 128, 128),    // Gray borders
                BorderWidth = 1.0f,
                CountryColors = GenerateDarkThemeColors(),
                ShowBorders = true,
                ShowCountryLabels = false
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
                
                // Generate procedural political boundaries instead of relying on complex spatial indexing
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
            
            // Convert pixel coordinates to geographic coordinates
            double startLon = (double)pixelX / _baseWidth * 360.0 - 180.0;
            double startLat = 90.0 - (double)pixelY / _baseHeight * 180.0;
            double endLon = (double)(pixelX + tileWidth) / _baseWidth * 360.0 - 180.0;
            double endLat = 90.0 - (double)(pixelY + tileHeight) / _baseHeight * 180.0;
            
            // Generate simplified country regions based on geographic areas
            var countries = GetCountriesInRegion(startLon, startLat, endLon, endLat);
            
            foreach (var country in countries)
            {
                // Create simplified rectangular country boundaries
                var countryBounds = GetCountryBounds(country.code);
                
                // Check if country intersects with tile
                if (countryBounds.maxLon < startLon || countryBounds.minLon > endLon ||
                    countryBounds.maxLat < endLat || countryBounds.minLat > startLat)
                    continue;
                
                // Convert geographic bounds to pixel coordinates relative to tile
                var pixelBounds = GeographicToTilePixels(countryBounds, startLon, startLat, endLon, endLat, tileWidth, tileHeight);
                
                // Create country polygon
                var countryCoords = new[]
                {
                    new Coordinate(pixelX + pixelBounds.left, pixelY + pixelBounds.top),
                    new Coordinate(pixelX + pixelBounds.right, pixelY + pixelBounds.top),
                    new Coordinate(pixelX + pixelBounds.right, pixelY + pixelBounds.bottom),
                    new Coordinate(pixelX + pixelBounds.left, pixelY + pixelBounds.bottom),
                    new Coordinate(pixelX + pixelBounds.left, pixelY + pixelBounds.top)
                };
                
                var polygon = geometryFactory.CreatePolygon(countryCoords);
                var countryColor = GetCountryColor(country.code, theme);
                
                var feature = new VectorFeature
                {
                    Geometry = polygon,
                    Properties = new Dictionary<string, object>
                    {
                        ["countryCode"] = country.code,
                        ["countryName"] = country.name,
                        ["region"] = country.region
                    },
                    Style = new VectorStyle
                    {
                        FillColor = countryColor,
                        StrokeColor = theme.BorderColor,
                        StrokeWidth = theme.BorderWidth,
                        IsVisible = true,
                        Opacity = 1.0f
                    }
                };
                
                vectorTile.Features.Add(feature);
            }
        }
        
        private List<(string code, string name, string region)> GetCountriesInRegion(double minLon, double minLat, double maxLon, double maxLat)
        {
            var countries = new List<(string code, string name, string region)>();
            
            // Debug output to understand the region being queried
            Debug.WriteLine($"Querying region: Lon[{minLon:F2}, {maxLon:F2}], Lat[{minLat:F2}, {maxLat:F2}]");
            
            // Major countries with simplified geographic regions
            var majorCountries = new[]
            {
                ("USA", "United States", "North America", -125.0, -66.0, 25.0, 49.0),
                ("CAN", "Canada", "North America", -141.0, -52.0, 42.0, 83.0),
                ("MEX", "Mexico", "North America", -118.0, -86.0, 14.0, 32.0),
                ("BRA", "Brazil", "South America", -74.0, -34.0, -34.0, 5.0),
                ("ARG", "Argentina", "South America", -73.0, -53.0, -55.0, -22.0),
                ("RUS", "Russia", "Asia", -180.0, 180.0, 41.0, 82.0),
                ("CHN", "China", "Asia", 73.0, 135.0, 18.0, 54.0),
                ("IND", "India", "Asia", 68.0, 97.0, 6.0, 37.0),
                ("AUS", "Australia", "Oceania", 113.0, 154.0, -44.0, -10.0),
                ("GBR", "United Kingdom", "Europe", -8.0, 2.0, 50.0, 61.0),
                ("FRA", "France", "Europe", -5.0, 9.0, 42.0, 51.0),
                ("DEU", "Germany", "Europe", 6.0, 15.0, 47.0, 55.0),
                ("ESP", "Spain", "Europe", -9.0, 4.0, 36.0, 44.0),
                ("ITA", "Italy", "Europe", 7.0, 19.0, 36.0, 47.0),
                ("NOR", "Norway", "Europe", 4.0, 31.0, 58.0, 81.0),
                ("SWE", "Sweden", "Europe", 11.0, 24.0, 55.0, 69.0),
                ("FIN", "Finland", "Europe", 20.0, 32.0, 60.0, 70.0),
                ("JPN", "Japan", "Asia", 129.0, 146.0, 30.0, 46.0),
                ("KOR", "South Korea", "Asia", 125.0, 130.0, 33.0, 39.0),
                ("THA", "Thailand", "Asia", 97.0, 106.0, 5.0, 21.0),
                ("VNM", "Vietnam", "Asia", 102.0, 110.0, 8.0, 24.0),
                ("IDN", "Indonesia", "Asia", 95.0, 141.0, -11.0, 6.0),
                ("MYS", "Malaysia", "Asia", 100.0, 119.0, 1.0, 7.0),
                ("PHL", "Philippines", "Asia", 116.0, 127.0, 5.0, 19.0),
                ("EGY", "Egypt", "Africa", 25.0, 35.0, 22.0, 32.0),
                ("ZAF", "South Africa", "Africa", 16.0, 33.0, -35.0, -22.0),
                ("NGA", "Nigeria", "Africa", 3.0, 15.0, 4.0, 14.0),
                ("KEN", "Kenya", "Africa", 34.0, 42.0, -5.0, 5.0),
                ("MAR", "Morocco", "Africa", -13.0, -1.0, 28.0, 36.0),
                ("DZA", "Algeria", "Africa", -9.0, 12.0, 19.0, 37.0),
                ("LBY", "Libya", "Africa", 10.0, 25.0, 20.0, 33.0),
                ("IRN", "Iran", "Asia", 44.0, 63.0, 25.0, 40.0),
                ("IRQ", "Iraq", "Asia", 39.0, 49.0, 29.0, 37.0),
                ("SAU", "Saudi Arabia", "Asia", 34.0, 56.0, 16.0, 32.0),
                ("TUR", "Turkey", "Asia", 26.0, 45.0, 36.0, 42.0),
                ("PER", "Peru", "South America", -81.0, -68.0, -18.0, 0.0),
                ("COL", "Colombia", "South America", -79.0, -67.0, -4.0, 12.0),
                ("VEN", "Venezuela", "South America", -73.0, -60.0, 1.0, 12.0),
                ("CHL", "Chile", "South America", -76.0, -67.0, -56.0, -17.0),
                ("BOL", "Bolivia", "South America", -70.0, -57.0, -23.0, -10.0),
                ("PAR", "Paraguay", "South America", -63.0, -54.0, -28.0, -19.0),
                ("URY", "Uruguay", "South America", -58.0, -53.0, -35.0, -30.0)
            };
            
            foreach (var (code, name, region, cMinLon, cMaxLon, cMinLat, cMaxLat) in majorCountries)
            {
                // Check if country bounds intersect with tile bounds
                if (!(cMaxLon < minLon || cMinLon > maxLon || cMaxLat < minLat || cMinLat > maxLat))
                {
                    countries.Add((code, name, region));
                    Debug.WriteLine($"Country intersects: {name} [{cMinLon}, {cMaxLon}, {cMinLat}, {cMaxLat}]");
                }
            }
            
            // If no countries found in the exact region, add some default countries for testing
            if (countries.Count == 0)
            {
                Debug.WriteLine("No countries found, adding defaults for testing");
                // Add a few major countries that span large areas to ensure we always have something to render
                countries.Add(("USA", "United States", "North America"));
                countries.Add(("RUS", "Russia", "Asia"));
                countries.Add(("CHN", "China", "Asia"));
            }
            
            Debug.WriteLine($"Total countries found: {countries.Count}");
            return countries;
        }
        
        private (double minLon, double maxLon, double minLat, double maxLat) GetCountryBounds(string countryCode)
        {
            // Return simplified bounds for major countries
            return countryCode switch
            {
                "USA" => (-125.0, -66.0, 25.0, 49.0),
                "CAN" => (-141.0, -52.0, 42.0, 83.0),
                "MEX" => (-118.0, -86.0, 14.0, 32.0),
                "BRA" => (-74.0, -34.0, -34.0, 5.0),
                "ARG" => (-73.0, -53.0, -55.0, -22.0),
                "RUS" => (-180.0, 180.0, 41.0, 82.0),
                "CHN" => (73.0, 135.0, 18.0, 54.0),
                "IND" => (68.0, 97.0, 6.0, 37.0),
                "AUS" => (113.0, 154.0, -44.0, -10.0),
                "GBR" => (-8.0, 2.0, 50.0, 61.0),
                "FRA" => (-5.0, 9.0, 42.0, 51.0),
                "DEU" => (6.0, 15.0, 47.0, 55.0),
                "JPN" => (129.0, 146.0, 30.0, 46.0),
                _ => (0.0, 1.0, 0.0, 1.0) // Default small bounds
            };
        }
        
        private (double left, double right, double top, double bottom) GeographicToTilePixels(
            (double minLon, double maxLon, double minLat, double maxLat) geoBounds,
            double tileMinLon, double tileMinLat, double tileMaxLon, double tileMaxLat,
            int tileWidth, int tileHeight)
        {
            // Convert geographic coordinates to tile-relative pixel coordinates
            double left = Math.Max(0, (geoBounds.minLon - tileMinLon) / (tileMaxLon - tileMinLon) * tileWidth);
            double right = Math.Min(tileWidth, (geoBounds.maxLon - tileMinLon) / (tileMaxLon - tileMinLon) * tileWidth);
            double top = Math.Max(0, (tileMaxLat - geoBounds.maxLat) / (tileMaxLat - tileMinLat) * tileHeight);
            double bottom = Math.Min(tileHeight, (tileMaxLat - geoBounds.minLat) / (tileMaxLat - tileMinLat) * tileHeight);
            
            return (left, right, top, bottom);
        }
        
        protected override void RenderVectorTile(SKCanvas canvas, VectorTile vectorTile, int destX, int destY, int cellSize)
        {
            var theme = _themes[_currentTheme];
            
            canvas.Save();
            canvas.Translate(destX, destY);
            
            try
            {
                // Render country fills first
                foreach (var feature in vectorTile.Features)
                {
                    if (!feature.Style.IsVisible || feature.Geometry == null)
                        continue;
                    
                    RenderCountryFill(canvas, feature, vectorTile.Bounds);
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
                
                // Render country labels if enabled
                if (theme.ShowCountryLabels)
                {
                    foreach (var feature in vectorTile.Features)
                    {
                        if (!feature.Style.IsVisible || feature.Geometry == null)
                            continue;
                        
                        RenderCountryLabel(canvas, feature, vectorTile.Bounds);
                    }
                }
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
            else if (feature.Geometry is MultiPolygon multiPolygon)
            {
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    var poly = (Polygon)multiPolygon.GetGeometryN(i);
                    var path = CreatePathFromGeometry(poly, tileBounds);
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
            else if (feature.Geometry is MultiPolygon multiPolygon)
            {
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    var poly = (Polygon)multiPolygon.GetGeometryN(i);
                    var path = CreatePathFromGeometry(poly, tileBounds);
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
        }
        
        private void RenderCountryLabel(SKCanvas canvas, VectorFeature feature, SKRect tileBounds)
        {
            if (!feature.Properties.TryGetValue("countryName", out var nameObj) || 
                nameObj is not string countryName || 
                string.IsNullOrEmpty(countryName))
                return;
            
            // Get centroid of the geometry for label placement
            var centroid = feature.Geometry?.Centroid;
            if (centroid == null) return;
            
            // Convert absolute coordinates to tile-relative coordinates
            float x = (float)(centroid.X - tileBounds.Left);
            float y = (float)(centroid.Y - tileBounds.Top);
            
            // Only render if centroid is within tile bounds
            if (x >= 0 && x <= TileSizePx && y >= 0 && y <= TileSizePx)
            {
                using var labelPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 12,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };
                
                // Draw text with white outline for better readability
                using var outlinePaint = new SKPaint
                {
                    Color = SKColors.White,
                    TextSize = 12,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 3,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };
                
                canvas.DrawText(countryName, x, y, outlinePaint);
                canvas.DrawText(countryName, x, y, labelPaint);
            }
        }
        
        private SKPath? CreatePathFromGeometry(Polygon polygon, SKRect tileBounds)
        {
            var path = new SKPath();
            
            try
            {
                // Convert absolute coordinates to tile-relative coordinates
                var coords = polygon.ExteriorRing.Coordinates;
                if (coords.Length < 4) return null;
                
                bool first = true;
                foreach (var coord in coords)
                {
                    float x = (float)(coord.X - tileBounds.Left);
                    float y = (float)(coord.Y - tileBounds.Top);
                    
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
            catch
            {
                path.Dispose();
                return null;
            }
        }
        
        private SKColor GetCountryColor(string countryCode, PoliticalTheme theme)
        {
            if (theme.CountryColors.TryGetValue(countryCode, out var color))
            {
                return color;
            }
            
            // Generate a consistent color based on country code hash
            int hash = countryCode.GetHashCode();
            byte r = (byte)(100 + Math.Abs(hash % 156));
            byte g = (byte)(100 + Math.Abs((hash >> 8) % 156));
            byte b = (byte)(100 + Math.Abs((hash >> 16) % 156));
            
            var generatedColor = new SKColor(r, g, b, 255);
            theme.CountryColors[countryCode] = generatedColor; // Cache for consistency
            return generatedColor;
        }
        
        protected override SKColor GetBackgroundColor()
        {
            return _themes[_currentTheme].WaterColor;
        }
        
        private Dictionary<string, SKColor> GenerateDistinctCountryColors()
        {
            // Pre-generate some distinct colors for major countries
            return new Dictionary<string, SKColor>
            {
                ["USA"] = new SKColor(173, 216, 230),    // Light blue
                ["RUS"] = new SKColor(255, 182, 193),    // Light pink
                ["CHN"] = new SKColor(255, 255, 224),    // Light yellow
                ["IND"] = new SKColor(255, 218, 185),    // Peach
                ["BRA"] = new SKColor(144, 238, 144),    // Light green
                ["CAN"] = new SKColor(221, 160, 221),    // Plum
                ["AUS"] = new SKColor(255, 165, 0),      // Orange
                ["ARG"] = new SKColor(176, 224, 230),    // Powder blue
                ["MEX"] = new SKColor(255, 192, 203),    // Pink
                ["GBR"] = new SKColor(230, 230, 250)     // Lavender
            };
        }
        
        private Dictionary<string, SKColor> GenerateHighContrastColors()
        {
            return new Dictionary<string, SKColor>
            {
                ["USA"] = new SKColor(255, 0, 0),        // Red
                ["RUS"] = new SKColor(0, 255, 0),        // Green
                ["CHN"] = new SKColor(255, 255, 0),      // Yellow
                ["IND"] = new SKColor(255, 0, 255),      // Magenta
                ["BRA"] = new SKColor(0, 255, 255),      // Cyan
                ["CAN"] = new SKColor(128, 0, 128),      // Purple
                ["AUS"] = new SKColor(255, 165, 0),      // Orange
                ["ARG"] = new SKColor(0, 128, 0),        // Dark green
                ["MEX"] = new SKColor(128, 0, 0),        // Maroon
                ["GBR"] = new SKColor(0, 0, 128)         // Navy
            };
        }
        
        private Dictionary<string, SKColor> GenerateMinimalColors()
        {
            var baseColor = new SKColor(220, 220, 220); // Light gray
            return new Dictionary<string, SKColor>();    // Use generated colors only
        }
        
        private Dictionary<string, SKColor> GenerateDarkThemeColors()
        {
            return new Dictionary<string, SKColor>
            {
                ["USA"] = new SKColor(70, 130, 180),     // Steel blue
                ["RUS"] = new SKColor(139, 69, 19),      // Saddle brown
                ["CHN"] = new SKColor(85, 107, 47),      // Dark olive green
                ["IND"] = new SKColor(72, 61, 139),      // Dark slate blue
                ["BRA"] = new SKColor(47, 79, 79),       // Dark slate gray
                ["CAN"] = new SKColor(105, 105, 105),    // Dim gray
                ["AUS"] = new SKColor(184, 134, 11),     // Dark goldenrod
                ["ARG"] = new SKColor(112, 128, 144),    // Slate gray
                ["MEX"] = new SKColor(160, 82, 45),      // Saddle brown
                ["GBR"] = new SKColor(75, 0, 130)        // Indigo
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