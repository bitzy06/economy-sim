using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OSGeo.OGR;

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
        
        // Spatial index for fast country lookup
        private readonly PoliticalSpatialIndex _spatialIndex;
        private bool _spatialIndexBuilt = false;
        private readonly object _indexLock = new object();
        
        public VectorPoliticalTileRenderer(PoliticalBorderManager politicalManager, int baseWidth, int baseHeight) 
            : base(baseWidth, baseHeight)
        {
            _politicalManager = politicalManager;
            _spatialIndex = new PoliticalSpatialIndex();
            
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
                
                // Clear cache and rebuild spatial index for new date
                lock (_cacheLock)
                {
                    _vectorTileCache.Clear();
                }
                
                lock (_indexLock)
                {
                    _spatialIndexBuilt = false;
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
                // Ensure spatial index is built
                EnsureSpatialIndexBuilt();
                
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
                
                // Convert tile bounds to geographic bounds for spatial queries
                var geoBounds = CoordinateTransform.GetTileGeographicBounds(tileX, tileY, TileSizePx, scaledMapWidth, scaledMapHeight);
                
                // Load political vector data for this tile
                LoadPoliticalVectorData(vectorTile, geoBounds);
                
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading vector data for political tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        private void LoadPoliticalVectorData(VectorTile vectorTile, GeoBounds geoBounds)
        {
            var theme = _themes[_currentTheme];
            
            try
            {
                // Query spatial index for countries intersecting this tile
                var intersectingCountries = _spatialIndex.GetCountriesInBounds(geoBounds);
                
                foreach (var country in intersectingCountries)
                {
                    // Skip if no valid country data
                    if (string.IsNullOrEmpty(country.CountryCode)) continue;
                    
                    // Get country color
                    var countryColor = GetCountryColor(country.CountryCode, theme);
                    
                    // For now, create a simple rectangular feature for the country bounds
                    // TODO: Convert OGR geometry to NTS geometry for proper vector rendering
                    var geometryFactory = new GeometryFactory();
                    var bounds = country.Bounds;
                    
                    var coordinates = new[]
                    {
                        new Coordinate(bounds.MinLon, bounds.MinLat),
                        new Coordinate(bounds.MaxLon, bounds.MinLat),
                        new Coordinate(bounds.MaxLon, bounds.MaxLat),
                        new Coordinate(bounds.MinLon, bounds.MaxLat),
                        new Coordinate(bounds.MinLon, bounds.MinLat)
                    };
                    
                    var polygon = geometryFactory.CreatePolygon(coordinates);
                    
                    // Create vector feature for this country
                    var feature = new VectorFeature
                    {
                        Geometry = polygon,
                        Properties = new Dictionary<string, object>
                        {
                            ["countryCode"] = country.CountryCode,
                            ["countryName"] = country.CountryName,
                            ["rasterCode"] = country.RasterCode
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading political vector data: {ex.Message}");
            }
        }
        
        private void EnsureSpatialIndexBuilt()
        {
            lock (_indexLock)
            {
                if (_spatialIndexBuilt) return;
                
                string? cshapesPath = FindCShapesFile();
                if (string.IsNullOrEmpty(cshapesPath))
                {
                    throw new ApplicationException("CShapes file not found for spatial index generation");
                }
                
                Debug.WriteLine("Building spatial index for vector political rendering...");
                _spatialIndex.BuildIndex(cshapesPath, _politicalMapDate);
                _spatialIndexBuilt = true;
                Debug.WriteLine($"Vector spatial index built with {_spatialIndex.CountryCount} countries");
            }
        }
        
        private string? FindCShapesFile()
        {
            // Check user Documents data directory first
            string userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "data", "country_borders");
                
            string primaryPath = Path.Combine(userDataPath, "CShapes-2.0.shp");
            if (File.Exists(primaryPath))
            {
                return primaryPath;
            }
            
            // Also check for ne_10m_admin_0_countries.shp as fallback
            string fallbackPath = Path.Combine(userDataPath, "ne_10m_admin_0_countries.shp");
            if (File.Exists(fallbackPath))
            {
                return fallbackPath;
            }
            
            // Check additional common locations
            string[] possiblePaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "CShapes-2.0.shp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "ne_10m_admin_0_countries.shp"),
                "data/country_borders/CShapes-2.0.shp",
                "data/country_borders/ne_10m_admin_0_countries.shp"
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
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
            
            // Convert to tile-relative coordinates
            float x = (float)((centroid.X * _baseWidth) - tileBounds.Left);
            float y = (float)((centroid.Y * _baseHeight) - tileBounds.Top);
            
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
                // Convert geographic coordinates to tile-relative pixel coordinates
                var coords = polygon.ExteriorRing.Coordinates;
                if (coords.Length < 4) return null;
                
                bool first = true;
                foreach (var coord in coords)
                {
                    // Convert normalized geographic coordinates to tile pixel coordinates
                    float x = (float)((coord.X * _baseWidth) - tileBounds.Left);
                    float y = (float)((coord.Y * _baseHeight) - tileBounds.Top);
                    
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
        
        public override void Dispose()
        {
            base.Dispose();
            _spatialIndex?.Dispose();
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