using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using NetTopologySuite.Geometries;
using OSGeo.GDAL;
using MaxRev.Gdal.Core;

namespace StrategyGame
{
    /// <summary>
    /// Vector-based terrain tile renderer with GPU acceleration and customizable styling
    /// </summary>
    public class VectorTerrainTileRenderer : VectorTileRenderer
    {
        private readonly object _gdalConfigLock = new object();
        private bool _gdalConfigured = false;
        
        // Terrain styling themes
        private readonly Dictionary<string, TerrainTheme> _themes = new();
        private string _currentTheme = "Default";
        
        // Data file paths
        private readonly string _terrainTifPath;
        private readonly string _shapefilePath;
        
        public VectorTerrainTileRenderer(int baseWidth, int baseHeight) : base(baseWidth, baseHeight)
        {
            _terrainTifPath = GetDataFile("NE1_HR_LC.tif");
            _shapefilePath = GetDataFile("ne_10m_admin_0_countries.shp");
            
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
            
            lock (_gdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }
            
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
                
                // Load terrain data and convert to vector features
                LoadTerrainVectorData(vectorTile, cellSize, pixelX, pixelY, tileWidth, tileHeight);
                
                return vectorTile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading vector data for terrain tile ({tileX}, {tileY}): {ex.Message}");
                return null;
            }
        }
        
        private void LoadTerrainVectorData(VectorTile vectorTile, int cellSize, int pixelX, int pixelY, int tileWidth, int tileHeight)
        {
            if (!File.Exists(_terrainTifPath))
            {
                Debug.WriteLine($"Terrain file not found: {_terrainTifPath}");
                return;
            }
            
            try
            {
                using var ds = Gdal.Open(_terrainTifPath, Access.GA_ReadOnly);
                if (ds == null)
                {
                    Debug.WriteLine($"Failed to open terrain file: {_terrainTifPath}");
                    return;
                }
                
                int srcW = ds.RasterXSize;
                int srcH = ds.RasterYSize;
                
                // Calculate sampling parameters
                int startCellX = pixelX / cellSize;
                int startCellY = pixelY / cellSize;
                int cellsX = (tileWidth + cellSize - 1) / cellSize;
                int cellsY = (tileHeight + cellSize - 1) / cellSize;
                
                double scaleX = (double)srcW / _baseWidth;
                double scaleY = (double)srcH / _baseHeight;
                
                int srcX = (int)Math.Floor(startCellX * scaleX);
                int srcY = (int)Math.Floor(startCellY * scaleY);
                int readW = (int)Math.Ceiling(cellsX * scaleX);
                int readH = (int)Math.Ceiling(cellsY * scaleY);
                
                // Read terrain color data
                byte[] r = new byte[cellsX * cellsY];
                byte[] g = new byte[cellsX * cellsY];
                byte[] b = new byte[cellsX * cellsY];
                
                ds.GetRasterBand(1).ReadRaster(srcX, srcY, readW, readH, r, cellsX, cellsY, 0, 0);
                ds.GetRasterBand(2).ReadRaster(srcX, srcY, readW, readH, g, cellsX, cellsY, 0, 0);
                ds.GetRasterBand(3).ReadRaster(srcX, srcY, readW, readH, b, cellsX, cellsY, 0, 0);
                
                // Create vector features from terrain data
                CreateTerrainVectorFeatures(vectorTile, r, g, b, cellsX, cellsY, cellSize, pixelX, pixelY);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading terrain vector data: {ex.Message}");
            }
        }
        
        private void CreateTerrainVectorFeatures(VectorTile vectorTile, byte[] r, byte[] g, byte[] b, 
            int cellsX, int cellsY, int cellSize, int offsetX, int offsetY)
        {
            var theme = _themes[_currentTheme];
            var geometryFactory = new GeometryFactory();
            
            // Group adjacent cells of similar terrain type into polygons
            var terrainRegions = new Dictionary<TerrainType, List<Coordinate>>();
            
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    int idx = y * cellsX + x;
                    var color = new SKColor(r[idx], g[idx], b[idx]);
                    var terrainType = ClassifyTerrain(color);
                    
                    // Create cell coordinates in world space
                    var cellCoords = new[]
                    {
                        new Coordinate(offsetX + x * cellSize, offsetY + y * cellSize),
                        new Coordinate(offsetX + (x + 1) * cellSize, offsetY + y * cellSize),
                        new Coordinate(offsetX + (x + 1) * cellSize, offsetY + (y + 1) * cellSize),
                        new Coordinate(offsetX + x * cellSize, offsetY + (y + 1) * cellSize),
                        new Coordinate(offsetX + x * cellSize, offsetY + y * cellSize)
                    };
                    
                    // Create polygon for this cell
                    var polygon = geometryFactory.CreatePolygon(cellCoords);
                    
                    var feature = new VectorFeature
                    {
                        Geometry = polygon,
                        Properties = new Dictionary<string, object>
                        {
                            ["terrainType"] = terrainType,
                            ["originalColor"] = color
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
        
        private TerrainType ClassifyTerrain(SKColor color)
        {
            // Simple terrain classification based on color values
            // This could be made more sophisticated with machine learning or lookup tables
            
            int r = color.Red;
            int g = color.Green;
            int b = color.Blue;
            
            // Water detection - blue dominant
            if (b > r && b > g && b > 150)
            {
                return TerrainType.Water;
            }
            
            // Ice/snow - high luminance, cool colors
            if (r > 200 && g > 200 && b > 200)
            {
                return TerrainType.Ice;
            }
            
            // Desert - sandy colors, red/yellow dominant
            if (r > g && r > 160 && g > 120 && b < 150)
            {
                return TerrainType.Desert;
            }
            
            // Forest - green dominant
            if (g > r && g > b && g > 100)
            {
                return TerrainType.Forest;
            }
            
            // Mountain - gray colors, low saturation
            if (Math.Abs(r - g) < 30 && Math.Abs(g - b) < 30 && Math.Abs(r - b) < 30 && r < 150)
            {
                return TerrainType.Mountain;
            }
            
            // Tundra - cool, desaturated colors
            if (b > r && g > 100 && r < 150)
            {
                return TerrainType.Tundra;
            }
            
            // Default to plains
            return TerrainType.Plains;
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
        
        private string GetDataFile(string fileName)
        {
            // Use the same data file resolution as PixelMapGenerator
            string userPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "data", "terrain", fileName);
            
            if (File.Exists(userPath))
                return userPath;
            
            // Fall back to repository data directory
            string repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string repoPath = Path.Combine(repoRoot, "data", fileName);
            
            return File.Exists(repoPath) ? repoPath : userPath;
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