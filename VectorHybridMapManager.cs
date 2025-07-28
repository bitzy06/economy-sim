using System;
using System.Collections.Generic;
using SkiaSharp;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace StrategyGame
{
    /// <summary>
    /// Vector-based hybrid map manager with GPU acceleration and customizable themes
    /// </summary>
    public class VectorHybridMapManager : IMapManager
    {
        private readonly VectorTerrainTileRenderer _terrainRenderer;
        private readonly VectorPoliticalTileRenderer _politicalRenderer;
        private readonly PoliticalBorderManager _politicalManager;
        
        private MapViewType _currentViewType = MapViewType.Terrain;
        private DateTime _politicalMapDate = new DateTime(1950, 1, 1);
        
        public MapViewType CurrentViewType => _currentViewType;
        public DateTime PoliticalMapDate => _politicalMapDate;
        
        public event EventHandler<MapViewType>? ViewTypeChanged;
        
        // Theme management
        private readonly Dictionary<MapViewType, string> _currentThemes = new()
        {
            { MapViewType.Terrain, "Default" },
            { MapViewType.Political, "Default" }
        };
        
        public VectorHybridMapManager(int baseWidth = 4096, int baseHeight = 2048)
        {
            _politicalManager = new PoliticalBorderManager();
            _terrainRenderer = new VectorTerrainTileRenderer(baseWidth, baseHeight);
            _politicalRenderer = new VectorPoliticalTileRenderer(_politicalManager, baseWidth, baseHeight);
            
            Debug.WriteLine("Vector hybrid map manager initialized with GPU acceleration support");
        }
        
        public void SetViewType(MapViewType viewType)
        {
            if (_currentViewType != viewType)
            {
                var oldViewType = _currentViewType;
                _currentViewType = viewType;
                
                Debug.WriteLine($"View type changed from {oldViewType} to {viewType}");
                ViewTypeChanged?.Invoke(this, viewType);
                
                // Force garbage collection when switching views to free memory
                if (viewType == MapViewType.Political)
                {
                    GC.Collect();
                }
            }
        }
        
        public void SetPoliticalMapDate(DateTime date)
        {
            if (_politicalMapDate != date)
            {
                _politicalMapDate = date;
                _politicalRenderer.SetPoliticalMapDate(date);
                Debug.WriteLine($"Political map date set to: {date:yyyy-MM-dd}");
            }
        }
        
        /// <summary>
        /// Set the theme for terrain rendering
        /// </summary>
        public void SetTerrainTheme(string themeName)
        {
            _terrainRenderer.SetTheme(themeName);
            _currentThemes[MapViewType.Terrain] = themeName;
            Debug.WriteLine($"Terrain theme set to: {themeName}");
        }
        
        /// <summary>
        /// Set the theme for political rendering
        /// </summary>
        public void SetPoliticalTheme(string themeName)
        {
            _politicalRenderer.SetTheme(themeName);
            _currentThemes[MapViewType.Political] = themeName;
            Debug.WriteLine($"Political theme set to: {themeName}");
        }
        
        /// <summary>
        /// Get available themes for the current view type
        /// </summary>
        public string[] GetAvailableThemes()
        {
            return _currentViewType switch
            {
                MapViewType.Terrain => _terrainRenderer.GetAvailableThemes(),
                MapViewType.Political => _politicalRenderer.GetAvailableThemes(),
                _ => Array.Empty<string>()
            };
        }
        
        /// <summary>
        /// Get the current theme name for the current view type
        /// </summary>
        public string GetCurrentTheme()
        {
            return _currentThemes.GetValueOrDefault(_currentViewType, "Default");
        }
        
        /// <summary>
        /// Assembles the current view using vector rendering with GPU acceleration
        /// </summary>
        public SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                SKBitmap? result = _currentViewType switch
                {
                    MapViewType.Terrain => _terrainRenderer.AssembleView(zoomLevel, viewArea, onTileReady),
                    MapViewType.Political => _politicalRenderer.AssembleView(zoomLevel, viewArea, onTileReady),
                    _ => null
                };
                
                if (result != null)
                {
                    Debug.WriteLine($"Vector {_currentViewType} view assembled in {sw.ElapsedMilliseconds}ms " +
                                  $"(size: {result.Width}x{result.Height})");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assembling vector view: {ex.Message}");
                return CreateErrorBitmap(viewArea.Width, viewArea.Height);
            }
        }
        
        /// <summary>
        /// Get map size for the specified zoom level
        /// </summary>
        public SKSizeI GetMapSize(int zoomLevel)
        {
            // Use the same calculation as the original map managers for compatibility
            int cellSize = GetCellSizeForZoom(zoomLevel);
            return new SKSizeI(4096 * cellSize, 2048 * cellSize);
        }
        
        /// <summary>
        /// Get cell size for the specified zoom level
        /// </summary>
        public int GetCellSizeForZoom(int zoomLevel)
        {
            int index = zoomLevel - 1;
            index = Math.Clamp(index, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1);
            return MultiResolutionMapManager.PixelsPerCellLevels[index];
        }
        
        /// <summary>
        /// Check if GPU acceleration is available
        /// </summary>
        public bool IsGpuAccelerationAvailable()
        {
            return VectorTileRenderer.IsGpuAccelerationAvailable;
        }
        
        /// <summary>
        /// Get rendering performance statistics
        /// </summary>
        public VectorRenderingStats GetRenderingStats()
        {
            return new VectorRenderingStats
            {
                CurrentViewType = _currentViewType,
                CurrentTheme = GetCurrentTheme(),
                GpuAccelerated = IsGpuAccelerationAvailable(),
                PoliticalDate = _politicalMapDate
            };
        }
        
        /// <summary>
        /// Export current view as high-resolution vector graphics
        /// </summary>
        public async Task<bool> ExportHighResolutionAsync(string filePath, int width, int height, int zoomLevel)
        {
            try
            {
                var viewArea = new SKRectI(0, 0, width, height);
                var bitmap = AssembleView(zoomLevel, viewArea);
                
                if (bitmap == null)
                {
                    Debug.WriteLine("Failed to generate bitmap for export");
                    return false;
                }
                
                using (bitmap)
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite(filePath))
                {
                    await data.AsStream().CopyToAsync(stream);
                }
                
                Debug.WriteLine($"High-resolution vector export saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting high-resolution image: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Create a demonstration of customization capabilities
        /// </summary>
        public void DemonstrateCustomization()
        {
            Debug.WriteLine("=== Vector Graphics Customization Demo ===");
            
            // Show available themes
            var terrainThemes = _terrainRenderer.GetAvailableThemes();
            var politicalThemes = _politicalRenderer.GetAvailableThemes();
            
            Debug.WriteLine($"Available terrain themes: {string.Join(", ", terrainThemes)}");
            Debug.WriteLine($"Available political themes: {string.Join(", ", politicalThemes)}");
            
            // Demonstrate theme switching
            foreach (var theme in terrainThemes)
            {
                SetTerrainTheme(theme);
                Debug.WriteLine($"Applied terrain theme: {theme}");
            }
            
            foreach (var theme in politicalThemes)
            {
                SetPoliticalTheme(theme);
                Debug.WriteLine($"Applied political theme: {theme}");
            }
            
            // Reset to default themes
            SetTerrainTheme("Default");
            SetPoliticalTheme("Default");
            
            Debug.WriteLine($"GPU acceleration available: {IsGpuAccelerationAvailable()}");
            Debug.WriteLine("=== End Customization Demo ===");
        }
        
        private SKBitmap CreateErrorBitmap(int width, int height)
        {
            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            
            // Create a distinctive error pattern
            canvas.Clear(new SKColor(255, 200, 200, 255)); // Light red background
            
            using var paint = new SKPaint
            {
                Color = new SKColor(200, 0, 0, 255),
                TextSize = Math.Min(width, height) / 20f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            
            string message = $"Vector rendering error\n{_currentViewType} view";
            var lines = message.Split('\n');
            
            float startY = height / 2f - (lines.Length * paint.TextSize / 2f);
            for (int i = 0; i < lines.Length; i++)
            {
                float y = startY + (i * paint.TextSize * 1.2f);
                canvas.DrawText(lines[i], width / 2f, y, paint);
            }
            
            return bitmap;
        }
        
        public void Dispose()
        {
            Debug.WriteLine("Disposing vector hybrid map manager");
            _terrainRenderer?.Dispose();
            _politicalRenderer?.Dispose();
            // PoliticalBorderManager doesn't implement IDisposable
        }
    }
    
    /// <summary>
    /// Performance and configuration statistics for vector rendering
    /// </summary>
    public class VectorRenderingStats
    {
        public MapViewType CurrentViewType { get; set; }
        public string CurrentTheme { get; set; } = string.Empty;
        public bool GpuAccelerated { get; set; }
        public DateTime PoliticalDate { get; set; }
        public TimeSpan LastRenderTime { get; set; }
        public int CachedTileCount { get; set; }
    }
}