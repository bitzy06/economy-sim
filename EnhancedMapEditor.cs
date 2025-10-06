using System;
using System.Drawing;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Simplified map editor that directly edits the runtime grid via HybridMapManager/PoliticalTileManager/StateBorderManager.
    /// Works for both Countries and States using the unified admin API.
    /// </summary>
    public class EnhancedMapEditor : IDisposable
    {
        private readonly HybridMapManager _mapManager;
        private readonly string _dataDirectory;
        
        // Current edit state
        private EditPolicy _currentEditPolicy = EditPolicy.FillAllSubcells;
        private uint _currentBrushValue = 1;
        private int _currentBrushSize = 1;
        private bool _showPrecisionIndicator = true;
        
        // Edit preview
        private Rectangle? _previewRegion = null;
        private bool _isDirtyTileGlowEnabled = true;
        
        public EnhancedMapEditor(string dataDirectory, HybridMapManager mapManager)
        {
            _dataDirectory = dataDirectory;
            _mapManager = mapManager;
            
            // Keep directories optional for future persistence features
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "grids"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "borders"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "edits"));
        }
        
        /// <summary>
        /// Core implementation applying edit given a political grid coordinate.
        /// </summary>
        private Task ApplyEditAtGridInternalAsync(MapViewLevel level, int gridX, int gridY, int zoomLevel)
        {
            try
            {
                int limitW = (level == MapViewLevel.Countries || level == MapViewLevel.States)
                    ? _mapManager.PoliticalBaseWidth
                    : _mapManager.BaseWidth;
                int limitH = (level == MapViewLevel.Countries || level == MapViewLevel.States)
                    ? _mapManager.PoliticalBaseHeight
                    : _mapManager.BaseHeight;

                gridX = Math.Clamp(gridX, 0, limitW - 1);
                gridY = Math.Clamp(gridY, 0, limitH - 1);

                int halfBrush = _currentBrushSize / 2;
                int startX = Math.Max(0, gridX - halfBrush);
                int startY = Math.Max(0, gridY - halfBrush);
                int endX = Math.Min(limitW, gridX + halfBrush + 1);
                int endY = Math.Min(limitH, gridY + halfBrush + 1);
                var worldRegion = new Rectangle(startX, startY, endX - startX, endY - startY);

                if (!IsValidEditRegion(level, worldRegion))
                {
                    System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Invalid region (grid path): {worldRegion}");
                    return Task.CompletedTask;
                }

                if (_showPrecisionIndicator)
                {
                    int cellSizeForIndicator = _mapManager.GetCellSizeForZoom(zoomLevel);
                    ShowPrecisionIndicator(worldRegion, zoomLevel, cellSizeForIndicator);
                }

                int rasterCode = (int)_currentBrushValue;
                _mapManager.ChangeAdminControlRect(level, rasterCode, worldRegion);

                if (_isDirtyTileGlowEnabled)
                {
                    ShowDirtyTileGlow(worldRegion);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Error (grid) applying edit: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Apply edit using already computed political grid coordinates (preferred to avoid double scaling).
        /// </summary>
        public Task ApplyEditAtGridAsync(MapViewLevel level, int gridX, int gridY, int zoomLevel)
            => ApplyEditAtGridInternalAsync(level, gridX, gridY, zoomLevel);

        /// <summary>
        /// Applies an edit at the specified screen coordinates and zoom level for the given admin level.
        /// Coordinates are in DIPs and match the render path. outputSize is the current Image size in DIPs.
        /// </summary>
        public async Task ApplyEditAsync(MapViewLevel level, int screenX, int screenY, int zoomLevel, SKPointI viewOffset, SKSizeI outputSize)
        {
            try
            {
                if (screenX < 0 || screenY < 0 || screenX >= outputSize.Width || screenY >= outputSize.Height)
                {
                    return;
                }

                int gridX;
                int gridY;

                if (level == MapViewLevel.Countries || level == MapViewLevel.States)
                {
                    var (gx, gy) = _mapManager.ScreenToPoliticalGrid(screenX, screenY, zoomLevel, viewOffset);
                    gridX = gx; gridY = gy;
                }
                else
                {
                    int cellSize = _mapManager.GetCellSizeForZoom(zoomLevel);
                    int mapX = viewOffset.X + screenX;
                    int mapY = viewOffset.Y + screenY;
                    gridX = Math.Clamp(mapX / cellSize, 0, _mapManager.BaseWidth - 1);
                    gridY = Math.Clamp(mapY / cellSize, 0, _mapManager.BaseHeight - 1);
                }

                await ApplyEditAtGridInternalAsync(level, gridX, gridY, zoomLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Error applying edit: {ex.Message}");
            }
        }
        
        private bool IsValidEditRegion(MapViewLevel level, Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0) return false;
            if (region.Left < 0 || region.Top < 0) return false;
            int limitW = (level == MapViewLevel.Countries || level == MapViewLevel.States)
                ? _mapManager.PoliticalBaseWidth
                : _mapManager.BaseWidth;
            int limitH = (level == MapViewLevel.Countries || level == MapViewLevel.States)
                ? _mapManager.PoliticalBaseHeight
                : _mapManager.BaseHeight;
            if (region.Right > limitW || region.Bottom > limitH) return false;
            if (region.Width * region.Height > 100000) return false; // guardrail

            return true;
        }
        
        private void ShowPrecisionIndicator(Rectangle worldRegion, int zoomLevel, int cellSize)
        {
            int subcellCount = worldRegion.Width * worldRegion.Height;
            string message = $"Editing at zoom {zoomLevel} (cell size: {cellSize}px). This will affect {subcellCount} grid cells.";
            if (subcellCount > 100) message += " Consider zooming in for finer control.";
            if (_currentEditPolicy == EditPolicy.BorderAware) message += " Border-aware mode: respects coastlines and political boundaries.";
            System.Diagnostics.Debug.WriteLine($"[PRECISION INDICATOR] {message}");
        }
        
        private void ShowDirtyTileGlow(Rectangle worldRegion)
        {
            int tileStartX = worldRegion.Left / 512;
            int tileEndX = (worldRegion.Right + 511) / 512;
            int tileStartY = worldRegion.Top / 512;
            int tileEndY = (worldRegion.Bottom + 511) / 512;
            int affectedTiles = (tileEndX - tileStartX) * (tileEndY - tileStartY);
            System.Diagnostics.Debug.WriteLine($"[DIRTY TILE GLOW] {affectedTiles} tiles will be regenerated");
        }
        
        public void SetEditPolicy(EditPolicy policy)
        {
            _currentEditPolicy = policy;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Edit policy changed to: {policy}");
        }
        
        public void SetBrushValue(uint value)
        {
            _currentBrushValue = value;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Brush value changed to: {value}");
        }
        
        public void SetBrushSize(int size)
        {
            _currentBrushSize = Math.Max(1, Math.Min(50, size));
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Brush size changed to: {_currentBrushSize}");
        }
        
        public void TogglePrecisionIndicator()
        {
            _showPrecisionIndicator = !_showPrecisionIndicator;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Precision indicator: {(_showPrecisionIndicator ? "ON" : "OFF")}");
        }
        
        public void ToggleDirtyTileGlow()
        {
            _isDirtyTileGlowEnabled = !_isDirtyTileGlowEnabled;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Dirty tile glow: {(_isDirtyTileGlowEnabled ? "ON" : "OFF")}" );
        }
        
        public async Task<bool> UndoAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ENHANCED EDITOR] Undo not available without authoritative grid.");
            await Task.CompletedTask;
            return false;
        }
        
        public async Task<bool> RedoAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ENHANCED EDITOR] Redo not available without authoritative grid.");
            await Task.CompletedTask;
            return false;
        }
        
        public EditPolicy GetEditPolicy() => _currentEditPolicy;
        public uint GetBrushValue() => _currentBrushValue;
        public int GetBrushSize() => _currentBrushSize;
        public bool CanUndo() => false;
        public bool CanRedo() => false;
        
        public void Dispose()
        {
            // Nothing to dispose in simplified mode
        }
    }
}