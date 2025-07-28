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
    /// Vector-based terrain tile renderer with GPU acceleration and customizable styling
    /// </summary>
    public class VectorTerrainTileRenderer : VectorTileRenderer
    {
        // Terrain styling themes
        private readonly Dictionary<string, TerrainTheme> _themes = new();
        private string _currentTheme = "Default";
        
        public VectorTerrainTileRenderer(int baseWidth, int baseHeight) : base(baseWidth, baseHeight)
        {
            InitializeTerrainThemes();
        }
        
        private void InitializeTerrainThemes()
        {
            // Default natural earth theme
            _themes["Default"] = new TerrainTheme
            {
                Name = "Default",
                WaterColor = new SKColor(135, 206, 235), // Light sky blue
                LandColorMap = new Dictionary<TerrainType, SKColor>
                {
                    { TerrainType.Forest, new SKColor(34, 139, 34) },      // Forest green
                    { TerrainType.Desert, new SKColor(238, 203, 173) },    // Sandy brown
                    { TerrainType.Mountain, new SKColor(139, 137, 137) },  // Dim gray
                    { TerrainType.Plains, new SKColor(154, 205, 50) },     // Yellow green
                    { TerrainType.Tundra, new SKColor(176, 196, 222) },    // Light steel blue
                    { TerrainType.Ice, new SKColor(240, 248, 255) }        // Alice blue
                }
            };
            
            // High contrast theme for accessibility
            _themes["HighContrast"] = new TerrainTheme
            {
                Name = "HighContrast", 
                WaterColor = new SKColor(0, 0, 255),     // Pure blue
                LandColorMap = new Dictionary<TerrainType, SKColor>
                {
                    { TerrainType.Forest, new SKColor(0, 128, 0) },        // Pure green
                    { TerrainType.Desert, new SKColor(255, 255, 0) },      // Pure yellow
                    { TerrainType.Mountain, new SKColor(128, 128, 128) },  // Gray
                    { TerrainType.Plains, new SKColor(0, 255, 0) },        // Bright green
                    { TerrainType.Tundra, new SKColor(255, 255, 255) },    // White
                    { TerrainType.Ice, new SKColor(192, 192, 192) }        // Silver
                }
            };
            
            // Satellite-like theme
            _themes["Satellite"] = new TerrainTheme
            {
                Name = "Satellite",
                WaterColor = new SKColor(25, 25, 112),   // Midnight blue
                LandColorMap = new Dictionary<TerrainType, SKColor>
                {
                    { TerrainType.Forest, new SKColor(0, 100, 0) },        // Dark green
                    { TerrainType.Desert, new SKColor(205, 133, 63) },     // Peru
                    { TerrainType.Mountain, new SKColor(105, 105, 105) },  // Dim gray
                    { TerrainType.Plains, new SKColor(107, 142, 35) },     // Olive drab
                    { TerrainType.Tundra, new SKColor(119, 136, 153) },    // Light slate gray
                    { TerrainType.Ice, new SKColor(230, 230, 250) }        // Lavender
                }
            };
        }
        
        public void SetTheme(string themeName)
        {
            if (_themes.ContainsKey(themeName))
            {
                _currentTheme = themeName;
                Debug.WriteLine($"Terrain theme changed to: {themeName}");
                
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
                
                // Generate procedural terrain data instead of relying on external files
                GenerateProceduralTerrain(vectorTile, pixelX, pixelY, tileWidth, tileHeight);
                
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading vector data for terrain tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        private void GenerateProceduralTerrain(VectorTile vectorTile, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            var theme = _themes[_currentTheme];
            var geometryFactory = new GeometryFactory();
            
            // Convert pixel coordinates to geographic coordinates for procedural generation
            double startLon = (double)pixelX / _baseWidth * 360.0 - 180.0;
            double startLat = 90.0 - (double)pixelY / _baseHeight * 180.0;
            double endLon = (double)(pixelX + tileWidth) / _baseWidth * 360.0 - 180.0;
            double endLat = 90.0 - (double)(pixelY + tileHeight) / _baseHeight * 180.0;
            
            // Create efficient terrain regions (larger polygons instead of per-pixel)
            int regionSize = Math.Max(16, Math.Min(tileWidth, tileHeight) / 8); // Adaptive region size
            
            for (int y = 0; y < tileHeight; y += regionSize)
            {
                for (int x = 0; x < tileWidth; x += regionSize)
                {
                    int regionWidth = Math.Min(regionSize, tileWidth - x);
                    int regionHeight = Math.Min(regionSize, tileHeight - y);
                    
                    // Sample terrain type at region center
                    double centerLon = startLon + (x + regionWidth / 2.0) / tileWidth * (endLon - startLon);
                    double centerLat = startLat + (y + regionHeight / 2.0) / tileHeight * (endLat - startLat);
                    
                    var terrainType = ClassifyTerrainFromCoordinates(centerLon, centerLat);
                    
                    // Create region polygon
                    var regionCoords = new[]
                    {
                        new Coordinate(pixelX + x, pixelY + y),
                        new Coordinate(pixelX + x + regionWidth, pixelY + y),
                        new Coordinate(pixelX + x + regionWidth, pixelY + y + regionHeight),
                        new Coordinate(pixelX + x, pixelY + y + regionHeight),
                        new Coordinate(pixelX + x, pixelY + y)
                    };
                    
                    var polygon = geometryFactory.CreatePolygon(regionCoords);
                    
                    var feature = new VectorFeature
                    {
                        Geometry = polygon,
                        Properties = new Dictionary<string, object>
                        {
                            ["terrainType"] = terrainType,
                            ["longitude"] = centerLon,
                            ["latitude"] = centerLat
                        },
                        Style = new VectorStyle
                        {
                            FillColor = theme.LandColorMap.GetValueOrDefault(terrainType, theme.WaterColor),
                            StrokeColor = SKColors.Transparent,
                            StrokeWidth = 0,
                            IsVisible = true,
                            Opacity = 1.0f
                        }
                    };
                    
                    vectorTile.Features.Add(feature);
                }
            }
        }
        
        private TerrainType ClassifyTerrainFromCoordinates(double longitude, double latitude)
        {
            // Use geographic heuristics to classify terrain
            // This is much faster than reading from files and gives reasonable results
            
            // Use absolute latitude for climate zones
            double absLat = Math.Abs(latitude);
            
            // Use longitude and latitude to create noise for variety
            double noise = SimplexNoise(longitude * 0.1, latitude * 0.1);
            double elevation = SimplexNoise(longitude * 0.05, latitude * 0.05);
            
            // Water bodies (simplified)
            if (IsOceanArea(longitude, latitude))
            {
                return TerrainType.Water;
            }
            
            // Ice caps (high latitudes)
            if (absLat > 75 || (absLat > 65 && elevation > 0.6))
            {
                return TerrainType.Ice;
            }
            
            // Tundra (high latitudes, not ice)
            if (absLat > 60)
            {
                return TerrainType.Tundra;
            }
            
            // Mountains (high elevation with noise)
            if (elevation > 0.7)
            {
                return TerrainType.Mountain;
            }
            
            // Desert (specific longitude bands and low latitudes)
            if ((absLat < 35 && (IsDesertRegion(longitude, latitude) || elevation < -0.3)))
            {
                return TerrainType.Desert;
            }
            
            // Forest (temperate and tropical regions with good conditions)
            if (absLat < 60 && noise > 0.2 && elevation > 0.1)
            {
                return TerrainType.Forest;
            }
            
            // Default to plains
            return TerrainType.Plains;
        }
        
        private bool IsOceanArea(double longitude, double latitude)
        {
            // Simplified ocean detection based on major ocean areas
            // Pacific Ocean
            if ((longitude < -120 || longitude > 120) && Math.Abs(latitude) < 65)
                return true;
            
            // Atlantic Ocean (between Americas and Europe/Africa)
            if (longitude > -80 && longitude < -10 && Math.Abs(latitude) < 65)
                return true;
            
            // Indian Ocean
            if (longitude > 30 && longitude < 120 && latitude < 30 && latitude > -50)
                return true;
            
            // Arctic Ocean
            if (Math.Abs(latitude) > 75)
                return true;
                
            return false;
        }
        
        private bool IsDesertRegion(double longitude, double latitude)
        {
            // Sahara, Middle East, Central Asia
            if (longitude > -10 && longitude < 60 && latitude > 15 && latitude < 40)
                return true;
            
            // Australian deserts
            if (longitude > 110 && longitude < 155 && latitude > -35 && latitude < -15)
                return true;
            
            // Southwest USA, Northern Mexico
            if (longitude > -125 && longitude < -100 && latitude > 25 && latitude < 40)
                return true;
            
            // Patagonia
            if (longitude > -75 && longitude < -60 && latitude > -50 && latitude < -35)
                return true;
                
            return false;
        }
        
        private double SimplexNoise(double x, double y)
        {
            // Simple noise function for terrain variation
            // This is a simplified version for performance
            double value = 0.0;
            value += Math.Sin(x * 2.1) * Math.Cos(y * 1.7) * 0.5;
            value += Math.Sin(x * 0.8) * Math.Cos(y * 2.3) * 0.3;
            value += Math.Sin(x * 4.2) * Math.Cos(y * 3.9) * 0.2;
            return Math.Clamp(value, -1.0, 1.0);
        }
        
        protected override void RenderVectorTile(SKCanvas canvas, VectorTile vectorTile, int destX, int destY, int cellSize)
        {
            canvas.Save();
            canvas.Translate(destX, destY);
            
            try
            {
                // Render all terrain features
                foreach (var feature in vectorTile.Features)
                {
                    if (!feature.Style.IsVisible || feature.Geometry == null)
                        continue;
                    
                    RenderVectorFeature(canvas, feature, vectorTile.Bounds);
                }
            }
            finally
            {
                canvas.Restore();
            }
        }
        
        private void RenderVectorFeature(SKCanvas canvas, VectorFeature feature, SKRect tileBounds)
        {
            if (feature.Geometry is Polygon polygon)
            {
                var path = CreatePathFromPolygon(polygon, tileBounds);
                if (path != null)
                {
                    using (path)
                    {
                        // Fill
                        if (feature.Style.FillColor.Alpha > 0)
                        {
                            using var fillPaint = feature.Style.CreateFillPaint();
                            canvas.DrawPath(path, fillPaint);
                        }
                        
                        // Stroke
                        if (feature.Style.StrokeWidth > 0 && feature.Style.StrokeColor.Alpha > 0)
                        {
                            using var strokePaint = feature.Style.CreateStrokePaint();
                            canvas.DrawPath(path, strokePaint);
                        }
                    }
                }
            }
        }
        
        private SKPath? CreatePathFromPolygon(Polygon polygon, SKRect tileBounds)
        {
            var path = new SKPath();
            
            try
            {
                // Convert world coordinates to tile-relative coordinates
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
        
        protected override SKColor GetBackgroundColor()
        {
            return _themes[_currentTheme].WaterColor;
        }
    }
    
    /// <summary>
    /// Terrain classification for vector styling
    /// </summary>
    public enum TerrainType
    {
        Water,
        Forest,
        Desert,
        Mountain,
        Plains,
        Tundra,
        Ice
    }
    
    /// <summary>
    /// Customizable terrain theme for vector rendering
    /// </summary>
    public class TerrainTheme
    {
        public string Name { get; set; } = string.Empty;
        public SKColor WaterColor { get; set; }
        public Dictionary<TerrainType, SKColor> LandColorMap { get; set; } = new();
    }
}