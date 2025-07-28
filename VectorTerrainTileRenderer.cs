using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using SkiaSharp;
using Nts = NetTopologySuite.Geometries;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;

namespace StrategyGame
{
    /// <summary>
    /// Vector-based terrain tile renderer with GPU acceleration and customizable styling
    /// Uses real GDAL terrain data from Natural Earth raster files
    /// </summary>
    public class VectorTerrainTileRenderer : VectorTileRenderer
    {
        // Terrain styling themes
        private readonly Dictionary<string, TerrainTheme> _themes = new();
        private string _currentTheme = "Default";
        
        // GDAL configuration
        private static readonly object GdalConfigLock = new object();
        private static bool _gdalConfigured = false;
        
        // Data file paths (following existing pattern from PixelMapGenerator)
        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        
        private static string GetDataFile(string name)
        {
            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;

            var matches = Directory.GetFiles(RepoDataDir, name, SearchOption.AllDirectories);
            if (matches.Length > 0)
                return matches[0];
                
            throw new FileNotFoundException($"Data file not found: {name}");
        }
        
        private static readonly string TerrainTifPath = GetDataFile("NE1_HR_LC.tif");
        
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
                Console.WriteLine($"ERROR: Terrain tile ({tileX}, {tileY}) failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        private void GenerateProceduralTerrain(VectorTile vectorTile, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            try
            {
                // Use real GDAL terrain data instead of procedural generation
                GenerateRealTerrainData(vectorTile, pixelX, pixelY, tileWidth, tileHeight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load real terrain data: {ex.Message}. Using fallback pattern.");
                // Fallback to simple pattern if real data fails
                GenerateFallbackTerrain(vectorTile, pixelX, pixelY, tileWidth, tileHeight);
            }
        }
        
        private void GenerateRealTerrainData(VectorTile vectorTile, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            // Configure GDAL
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }
            
            var theme = _themes[_currentTheme];
            var geometryFactory = new Nts.GeometryFactory();
            
            // Open the terrain GeoTIFF
            using var ds = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
            if (ds == null)
                throw new FileNotFoundException("Missing terrain GeoTIFF", TerrainTifPath);

            int srcW = ds.RasterXSize;
            int srcH = ds.RasterYSize;
            
            // Calculate scaling from world coordinates to raster coordinates
            int mapWidthPx = _baseWidth;
            int mapHeightPx = _baseHeight;
            
            double scaleX = (double)srcW / mapWidthPx;
            double scaleY = (double)srcH / mapHeightPx;
            
            // Calculate the region to read from the raster
            int srcX = (int)Math.Floor(pixelX * scaleX);
            int srcY = (int)Math.Floor(pixelY * scaleY);
            int readW = (int)Math.Ceiling(tileWidth * scaleX);
            int readH = (int)Math.Ceiling(tileHeight * scaleY);
            
            // Clamp to raster bounds
            srcX = Math.Max(0, Math.Min(srcX, srcW - 1));
            srcY = Math.Max(0, Math.Min(srcY, srcH - 1));
            readW = Math.Min(readW, srcW - srcX);
            readH = Math.Min(readH, srcH - srcY);
            
            if (readW <= 0 || readH <= 0)
            {
                GenerateFallbackTerrain(vectorTile, pixelX, pixelY, tileWidth, tileHeight);
                return;
            }
            
            // Read RGB data from the raster
            byte[] r = new byte[readW * readH];
            byte[] g = new byte[readW * readH];
            byte[] b = new byte[readW * readH];
            
            ds.GetRasterBand(1).ReadRaster(srcX, srcY, readW, readH, r, readW, readH, 0, 0);
            ds.GetRasterBand(2).ReadRaster(srcX, srcY, readW, readH, g, readW, readH, 0, 0);
            ds.GetRasterBand(3).ReadRaster(srcX, srcY, readW, readH, b, readW, readH, 0, 0);
            
            // Create vector regions from the raster data
            // Group similar colored regions together to create vector polygons
            var regions = CreateTerrainRegions(r, g, b, readW, readH, tileWidth, tileHeight);
            
            foreach (var region in regions)
            {
                var feature = new VectorFeature
                {
                    Geometry = region.Geometry,
                    Properties = new Dictionary<string, object>
                    {
                        ["terrainType"] = region.TerrainType,
                        ["color"] = region.Color.ToString()
                    },
                    Style = new VectorStyle
                    {
                        FillColor = region.Color,
                        StrokeColor = region.Color,
                        StrokeWidth = 0.5f,
                        Opacity = 1.0f
                    }
                };
                
                vectorTile.Features.Add(feature);
            }
        }
        
        private List<TerrainRegion> CreateTerrainRegions(byte[] r, byte[] g, byte[] b, int dataWidth, int dataHeight, int tileWidth, int tileHeight)
        {
            var regions = new List<TerrainRegion>();
            var geometryFactory = new Nts.GeometryFactory();
            
            // Create regions by sampling the raster data at regular intervals
            // This gives us vector-like regions while preserving the terrain patterns
            int regionSize = 32; // Size of each region in pixels
            
            for (int y = 0; y < tileHeight; y += regionSize)
            {
                for (int x = 0; x < tileWidth; x += regionSize)
                {
                    int regionWidth = Math.Min(regionSize, tileWidth - x);
                    int regionHeight = Math.Min(regionSize, tileHeight - y);
                    
                    // Sample the center of this region from the raster data
                    int centerX = x + regionWidth / 2;
                    int centerY = y + regionHeight / 2;
                    
                    // Convert to raster coordinates
                    int rasterX = (int)((double)centerX / tileWidth * dataWidth);
                    int rasterY = (int)((double)centerY / tileHeight * dataHeight);
                    
                    if (rasterX >= 0 && rasterX < dataWidth && rasterY >= 0 && rasterY < dataHeight)
                    {
                        int idx = rasterY * dataWidth + rasterX;
                        var color = new SKColor(r[idx], g[idx], b[idx]);
                        var terrainType = ClassifyTerrainFromColor(color);
                        
                        // Create polygon for this region
                        var coords = new[]
                        {
                            new Nts.Coordinate(x, y),
                            new Nts.Coordinate(x + regionWidth, y),
                            new Nts.Coordinate(x + regionWidth, y + regionHeight),
                            new Nts.Coordinate(x, y + regionHeight),
                            new Nts.Coordinate(x, y)
                        };
                        
                        var polygon = geometryFactory.CreatePolygon(coords);
                        
                        regions.Add(new TerrainRegion
                        {
                            Geometry = polygon,
                            TerrainType = terrainType,
                            Color = color
                        });
                    }
                }
            }
            
            return regions;
        }
        
        private TerrainType ClassifyTerrainFromColor(SKColor color)
        {
            // Classify terrain based on Natural Earth color schemes
            // This is a simplified classification - could be enhanced with more sophisticated logic
            
            var r = color.Red;
            var g = color.Green;
            var b = color.Blue;
            
            // Water/Ocean (blue-ish)
            if (b > r && b > g && b > 150)
                return TerrainType.Water;
                
            // Ice/Snow (very light, high in all channels)
            if (r > 200 && g > 200 && b > 200)
                return TerrainType.Ice;
                
            // Desert (yellowish, sandy)
            if (r > g && r > 150 && g > 100 && b < 100)
                return TerrainType.Desert;
                
            // Forest (green-ish)
            if (g > r && g > b && g > 80)
                return TerrainType.Forest;
                
            // Mountains (gray-ish)
            if (Math.Abs(r - g) < 30 && Math.Abs(r - b) < 30 && r < 150)
                return TerrainType.Mountain;
                
            // Tundra (light gray/blue)
            if (b > 100 && g > 100 && r > 100 && Math.Max(Math.Max(r, g), b) < 200)
                return TerrainType.Tundra;
                
            // Default to plains
            return TerrainType.Plains;
        }
        
        private void GenerateFallbackTerrain(VectorTile vectorTile, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            var theme = _themes[_currentTheme];
            var geometryFactory = new Nts.GeometryFactory();
            
            // Create a simple, reliable pattern that guarantees multiple terrain types in each tile
            var terrainColors = new Dictionary<TerrainType, SKColor>
            {
                { TerrainType.Forest, new SKColor(0, 128, 0) },       // Green
                { TerrainType.Desert, new SKColor(255, 255, 0) },     // Yellow  
                { TerrainType.Mountain, new SKColor(128, 128, 128) }, // Gray
                { TerrainType.Plains, new SKColor(0, 255, 0) },       // Bright green
                { TerrainType.Water, new SKColor(0, 0, 255) },        // Blue
                { TerrainType.Tundra, new SKColor(255, 255, 255) },   // White
                { TerrainType.Ice, new SKColor(192, 192, 192) }       // Silver
            };
            
            int cellSize = 64;
            
            for (int y = 0; y < tileHeight; y += cellSize)
            {
                for (int x = 0; x < tileWidth; x += cellSize)
                {
                    int cellWidth = Math.Min(cellSize, tileWidth - x);
                    int cellHeight = Math.Min(cellSize, tileHeight - y);
                    
                    int gridX = x / cellSize;
                    int gridY = y / cellSize;
                    var terrainType = GetTerrainTypeFromPattern(gridX, gridY);
                    
                    var cellCoords = new[]
                    {
                        new Nts.Coordinate(x, y),
                        new Nts.Coordinate(x + cellWidth, y),
                        new Nts.Coordinate(x + cellWidth, y + cellHeight),
                        new Nts.Coordinate(x, y + cellHeight),
                        new Nts.Coordinate(x, y)
                    };
                    
                    var polygon = geometryFactory.CreatePolygon(cellCoords);
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
                            StrokeColor = color,
                            StrokeWidth = 0.5f,
                            Opacity = 1.0f
                        }
                    };
                    
                    vectorTile.Features.Add(feature);
                }
            }
        }
        
        private class TerrainRegion
        {
            public Nts.Polygon Geometry { get; set; } = null!;
            public TerrainType TerrainType { get; set; }
            public SKColor Color { get; set; }
        }
        
        private TerrainType GetTerrainTypeFromPattern(int gridX, int gridY)
        {
            // Create a simple, deterministic pattern that ensures variety
            int pattern = (gridX + gridY * 3) % 7;
            
            return pattern switch
            {
                0 => TerrainType.Forest,
                1 => TerrainType.Desert,
                2 => TerrainType.Mountain,
                3 => TerrainType.Plains,
                4 => TerrainType.Water,
                5 => TerrainType.Tundra,
                6 => TerrainType.Ice,
                _ => TerrainType.Plains
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
            if (feature.Geometry is Nts.Polygon polygon)
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
        
        private SKPath? CreatePathFromPolygon(Nts.Polygon polygon, SKRect tileBounds)
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