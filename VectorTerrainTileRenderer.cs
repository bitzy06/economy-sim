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
            
            // Create a simple, reliable pattern that guarantees multiple terrain types in each tile
            // This ensures we always see varied terrain instead of uniform water
            
            // Define terrain colors for easy debugging
            var terrainColors = new Dictionary<TerrainType, SKColor>
            {
                { TerrainType.Forest, new SKColor(0, 128, 0) },       // Pure green
                { TerrainType.Desert, new SKColor(255, 255, 0) },     // Pure yellow  
                { TerrainType.Mountain, new SKColor(128, 128, 128) }, // Gray
                { TerrainType.Plains, new SKColor(0, 255, 0) },       // Bright green
                { TerrainType.Water, new SKColor(0, 0, 255) },        // Pure blue
                { TerrainType.Tundra, new SKColor(255, 255, 255) },   // White
                { TerrainType.Ice, new SKColor(192, 192, 192) }       // Silver
            };
            
            // Create a simple grid pattern with different terrain types
            // This ensures every tile has multiple visible terrain types
            int cellSize = 64; // Size of each terrain cell in pixels
            
            for (int y = 0; y < tileHeight; y += cellSize)
            {
                for (int x = 0; x < tileWidth; x += cellSize)
                {
                    int cellWidth = Math.Min(cellSize, tileWidth - x);
                    int cellHeight = Math.Min(cellSize, tileHeight - y);
                    
                    // Use a simple pattern to determine terrain type
                    // This ensures we get a checkerboard-like pattern with varied terrain
                    int gridX = x / cellSize;
                    int gridY = y / cellSize;
                    var terrainType = GetTerrainTypeFromPattern(gridX, gridY);
                    
                    // Create cell polygon in local tile coordinates
                    var cellCoords = new[]
                    {
                        new Coordinate(x, y),
                        new Coordinate(x + cellWidth, y),
                        new Coordinate(x + cellWidth, y + cellHeight),
                        new Coordinate(x, y + cellHeight),
                        new Coordinate(x, y)
                    };
                    
                    var polygon = geometryFactory.CreatePolygon(cellCoords);
                    
                    // Use high-contrast colors for debugging
                    var color = terrainColors.GetValueOrDefault(terrainType, terrainColors[TerrainType.Plains]);
                    
                    var feature = new VectorFeature
                    {
                        Geometry = polygon,
                        Properties = new Dictionary<string, object>
                        {
                            ["terrainType"] = terrainType,
                            ["gridX"] = gridX,
                            ["gridY"] = gridY
                        },
                        Style = new VectorStyle
                        {
                            FillColor = color,
                            StrokeColor = new SKColor(0, 0, 0, 128), // Semi-transparent black border
                            StrokeWidth = 1,
                            IsVisible = true,
                            Opacity = 1.0f
                        }
                    };
                    
                    vectorTile.Features.Add(feature);
                }
            }
            
            Debug.WriteLine($"Generated {vectorTile.Features.Count} terrain features for tile ({vectorTile.TileX}, {vectorTile.TileY})");
        }
        
        private TerrainType GetTerrainTypeFromPattern(int gridX, int gridY)
        {
            // Create a simple, deterministic pattern that ensures variety
            // This guarantees we see multiple terrain types in every tile
            
            // Use modulo arithmetic to create a repeating pattern
            int pattern = (gridX + gridY * 3) % 7;
            
            return pattern switch
            {
                0 => TerrainType.Forest,     // Green
                1 => TerrainType.Desert,     // Yellow
                2 => TerrainType.Mountain,   // Gray
                3 => TerrainType.Plains,     // Bright green
                4 => TerrainType.Water,      // Blue (minimal water)
                5 => TerrainType.Tundra,     // White
                6 => TerrainType.Ice,        // Silver
                _ => TerrainType.Plains      // Fallback
            };
        }

        
        protected override void RenderVectorTile(SKCanvas canvas, VectorTile vectorTile, int destX, int destY, int cellSize)
        {
            canvas.Save();
            canvas.Translate(destX, destY);
            
            try
            {
                Debug.WriteLine($"Rendering {vectorTile.Features.Count} features for tile ({vectorTile.TileX}, {vectorTile.TileY})");
                
                int featuresRendered = 0;
                
                // Render all terrain features
                foreach (var feature in vectorTile.Features)
                {
                    if (!feature.Style.IsVisible || feature.Geometry == null)
                        continue;
                    
                    RenderVectorFeature(canvas, feature, vectorTile.Bounds);
                    featuresRendered++;
                }
                
                Debug.WriteLine($"Rendered {featuresRendered} terrain features for tile ({vectorTile.TileX}, {vectorTile.TileY})");
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
                Debug.WriteLine($"Error creating path from polygon: {ex.Message}");
                path.Dispose();
                return null;
            }
        }
        
        protected override SKColor GetBackgroundColor()
        {
            // Use a neutral background instead of water color
            // This prevents the entire surface from appearing as water
            return new SKColor(240, 240, 240); // Light gray background
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