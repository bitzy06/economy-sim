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
    /// Enhanced map editor that integrates with the AuthoritativeGridManager for precise editing
    /// </summary>
    public class EnhancedMapEditor : IDisposable
    {
        private readonly AuthoritativeGridManager _authoritativeGrid;
        private readonly HybridMapManager _mapManager;
        private readonly string _dataDirectory;
        
        // Current edit state
        private EditPolicy _currentEditPolicy = EditPolicy.FillAllSubcells;
        private uint _currentBrushValue = 1;
        private int _currentBrushSize = 1;
        private int _currentZoomLevel = 1;
        private bool _showPrecisionIndicator = true;
        
        // Edit preview
        private Rectangle? _previewRegion = null;
        private bool _isDirtyTileGlowEnabled = true;
        
        public EnhancedMapEditor(string dataDirectory, HybridMapManager mapManager)
        {
            _dataDirectory = dataDirectory;
            _mapManager = mapManager;
            _authoritativeGrid = new AuthoritativeGridManager(dataDirectory);
            
            // Ensure data directories exist
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "grids"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "borders"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dataDirectory, "edits"));
        }
        
        /// <summary>
        /// Applies an edit at the specified screen coordinates and zoom level
        /// </summary>
        public async Task ApplyEditAsync(int screenX, int screenY, int zoomLevel, SKPointI viewOffset)
        {
            try
            {
                // Convert screen coordinates to world space
                var worldRegion = ConvertScreenToWorldRegion(screenX, screenY, zoomLevel, viewOffset);
                
                // Validate edit region
                if (!IsValidEditRegion(worldRegion))
                {
                    System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Invalid edit region: {worldRegion}");
                    return;
                }
                
                // Show precision indicator before edit
                if (_showPrecisionIndicator)
                {
                    ShowPrecisionIndicator(worldRegion, zoomLevel);
                }
                
                // Apply the edit to the authoritative grid
                await _authoritativeGrid.ApplyEditAsync(zoomLevel, worldRegion, _currentBrushValue, _currentEditPolicy);
                
                // Show dirty tile glow if enabled
                if (_isDirtyTileGlowEnabled)
                {
                    ShowDirtyTileGlow(worldRegion);
                }
                
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Applied edit at zoom {zoomLevel}, region {worldRegion}, value {_currentBrushValue}, policy {_currentEditPolicy}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Error applying edit: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Converts screen coordinates to a world region for editing
        /// </summary>
        private Rectangle ConvertScreenToWorldRegion(int screenX, int screenY, int zoomLevel, SKPointI viewOffset)
        {
            // Get cell size for current zoom level
            int cellSize = _mapManager.GetCellSizeForZoom(zoomLevel);
            
            // Convert screen coordinates to world coordinates
            int worldX = screenX + viewOffset.X;
            int worldY = screenY + viewOffset.Y;
            
            // Convert to grid coordinates
            int gridX = worldX / cellSize;
            int gridY = worldY / cellSize;
            
            // Create brush region
            int halfBrush = _currentBrushSize / 2;
            int startX = Math.Max(0, gridX - halfBrush);
            int startY = Math.Max(0, gridY - halfBrush);
            int endX = Math.Min(4096, gridX + halfBrush + 1); // Base map is 4096x2048
            int endY = Math.Min(2048, gridY + halfBrush + 1);
            
            return new Rectangle(startX, startY, endX - startX, endY - startY);
        }
        
        /// <summary>
        /// Validates that an edit region is within bounds and reasonable
        /// </summary>
        private bool IsValidEditRegion(Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0) return false;
            if (region.Left < 0 || region.Top < 0) return false;
            if (region.Right > 4096 || region.Bottom > 2048) return false;
            if (region.Width * region.Height > 10000) return false; // Prevent massive edits
            
            return true;
        }
        
        /// <summary>
        /// Shows a precision indicator to inform the user about edit granularity
        /// </summary>
        private void ShowPrecisionIndicator(Rectangle worldRegion, int zoomLevel)
        {
            int cellSize = _mapManager.GetCellSizeForZoom(zoomLevel);
            int subcellCount = CalculateSubcellCount(worldRegion, zoomLevel);
            
            string message = $"Editing at zoom {zoomLevel} (cell size: {cellSize}px). This will affect {subcellCount} fine grid cells.";
            
            if (subcellCount > 100)
            {
                message += " Consider zooming in for finer control.";
            }
            
            if (_currentEditPolicy == EditPolicy.BorderAware)
            {
                message += " Border-aware mode: respects coastlines and political boundaries.";
            }
            
            System.Diagnostics.Debug.WriteLine($"[PRECISION INDICATOR] {message}");
        }
        
        /// <summary>
        /// Calculates how many fine grid subcells will be affected by an edit
        /// </summary>
        private int CalculateSubcellCount(Rectangle worldRegion, int zoomLevel)
        {
            // Calculate scaling factor from world region to authoritative grid
            double scaleFactor = AuthoritativeGridManager.AuthoritativeWidth / 4096.0;
            
            int fineWidth = (int)(worldRegion.Width * scaleFactor);
            int fineHeight = (int)(worldRegion.Height * scaleFactor);
            
            return fineWidth * fineHeight;
        }
        
        /// <summary>
        /// Shows a visual indication of tiles that will be regenerated
        /// </summary>
        private void ShowDirtyTileGlow(Rectangle worldRegion)
        {
            // TODO: Implement visual glow effect on UI
            // For now, just log the affected tiles
            int tileStartX = worldRegion.Left / 512;
            int tileEndX = (worldRegion.Right + 511) / 512;
            int tileStartY = worldRegion.Top / 512;
            int tileEndY = (worldRegion.Bottom + 511) / 512;
            
            int affectedTiles = (tileEndX - tileStartX) * (tileEndY - tileStartY);
            
            System.Diagnostics.Debug.WriteLine($"[DIRTY TILE GLOW] {affectedTiles} tiles will be regenerated");
        }
        
        /// <summary>
        /// Sets the edit policy for subsequent edits
        /// </summary>
        public void SetEditPolicy(EditPolicy policy)
        {
            _currentEditPolicy = policy;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Edit policy changed to: {policy}");
        }
        
        /// <summary>
        /// Sets the brush value (country ID, terrain type, etc.)
        /// </summary>
        public void SetBrushValue(uint value)
        {
            _currentBrushValue = value;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Brush value changed to: {value}");
        }
        
        /// <summary>
        /// Sets the brush size
        /// </summary>
        public void SetBrushSize(int size)
        {
            _currentBrushSize = Math.Max(1, Math.Min(50, size)); // Clamp to reasonable range
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Brush size changed to: {_currentBrushSize}");
        }
        
        /// <summary>
        /// Toggles the precision indicator
        /// </summary>
        public void TogglePrecisionIndicator()
        {
            _showPrecisionIndicator = !_showPrecisionIndicator;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Precision indicator: {(_showPrecisionIndicator ? "ON" : "OFF")}");
        }
        
        /// <summary>
        /// Toggles the dirty tile glow effect
        /// </summary>
        public void ToggleDirtyTileGlow()
        {
            _isDirtyTileGlowEnabled = !_isDirtyTileGlowEnabled;
            System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Dirty tile glow: {(_isDirtyTileGlowEnabled ? "ON" : "OFF")}");
        }
        
        /// <summary>
        /// Undoes the last edit operation
        /// </summary>
        public async Task<bool> UndoAsync()
        {
            try
            {
                bool result = await _authoritativeGrid.UndoAsync();
                if (result)
                {
                    System.Diagnostics.Debug.WriteLine("[ENHANCED EDITOR] Undo operation completed");
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Error during undo: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Redoes the next edit operation
        /// </summary>
        public async Task<bool> RedoAsync()
        {
            try
            {
                bool result = await _authoritativeGrid.RedoAsync();
                if (result)
                {
                    System.Diagnostics.Debug.WriteLine("[ENHANCED EDITOR] Redo operation completed");
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ENHANCED EDITOR] Error during redo: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets the current edit policy
        /// </summary>
        public EditPolicy GetEditPolicy() => _currentEditPolicy;
        
        /// <summary>
        /// Gets the current brush value
        /// </summary>
        public uint GetBrushValue() => _currentBrushValue;
        
        /// <summary>
        /// Gets the current brush size
        /// </summary>
        public int GetBrushSize() => _currentBrushSize;
        
        /// <summary>
        /// Checks if undo is available
        /// </summary>
        public bool CanUndo() => _authoritativeGrid != null;
        
        /// <summary>
        /// Checks if redo is available
        /// </summary>
        public bool CanRedo() => _authoritativeGrid != null;
        
        public void Dispose()
        {
            _authoritativeGrid?.Dispose();
        }
    }
}