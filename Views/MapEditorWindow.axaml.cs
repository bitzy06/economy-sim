using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SDPoint = System.Drawing.Point;

namespace Economy_sim
{
    public partial class MapEditorWindow : Window
    {
        private readonly HybridMapManager _mapManager;
        private readonly EnhancedMapEditor _enhancedEditor;
        
        private WriteableBitmap? _writeableBitmap;
        private int _currentZoomLevel = 1;
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Avalonia.Point _panStartPoint;
        private bool _isInitialized = false;
        
        private MapViewLevel _currentLevel = MapViewLevel.Countries;
        private bool _isDrawingMode = false;
        private bool _allowWaterPaint = false;
        private IndexedCountryFeature? _selectedCountry = null;
        private StateBorderManager.StateFeature? _selectedState = null;
        
        // Enhanced editing features
        private bool _useEnhancedEditor = true;
        private EditPolicy _currentEditPolicy = EditPolicy.FillAllSubcells;
        
        // Brush size for drawing
        private int _brushSize = 2;

        private readonly DispatcherTimer _mapUpdateTimer;
        private bool _pendingMapUpdate = false;
        private readonly object _renderLock = new object();
        private bool _renderInProgress = false;

        private readonly LinkedList<EditorAction> _undoStack = new();
        private const int MaxUndo = 50;

        private bool _isDrawingStrokeActive = false;
        private List<(SDPoint cell, int previousId)> _currentStrokeChanges = new();

        private record EditorAction(EditorActionType Type, MapViewLevel Level, int TargetId, List<(SDPoint cell, int previousId)> Changes);
        private enum EditorActionType { ZeroSumAssign, DirectAssign }

        public MapEditorWindow()
        {
            InitializeComponent();
            
            int baseW = ParseEnvOrDefault("ES_BASE_WIDTH", 4096);
            int baseH = ParseEnvOrDefault("ES_BASE_HEIGHT", 2048);
            // Default political grid to a higher resolution than terrain unless explicitly overridden
            int defaultPolW = checked(baseW * 2);
            int defaultPolH = checked(baseH * 2);
            int polW = ParseEnvOrDefault("ES_POL_BASE_WIDTH", defaultPolW);
            int polH = ParseEnvOrDefault("ES_POL_BASE_HEIGHT", defaultPolH);
            _mapManager = new HybridMapManager(baseWidth: baseW, baseHeight: baseH, politicalBaseWidth: polW, politicalBaseHeight: polH);
            _mapManager.SetViewType(MapViewType.Political);
            
            // Initialize enhanced editor with data directory
            string dataDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "data");
            _enhancedEditor = new EnhancedMapEditor(dataDirectory, _mapManager);
            
            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();

            SetupUIEventHandlers();
        }

        private static int ParseEnvOrDefault(string key, int def)
        {
            var s = Environment.GetEnvironmentVariable(key);
            return int.TryParse(s, out var v) && v > 0 ? v : def;
        }

        private void SetupUIEventHandlers()
        {
            var exitButton = this.FindControl<Button>("ExitButton");
            if (exitButton != null)
            {
                exitButton.Click += ExitButton_Click;
            }

            var levelSelector = this.FindControl<ComboBox>("LevelSelector");
            if (levelSelector != null)
            {
                levelSelector.SelectionChanged += LevelSelector_SelectionChanged;
            }

            var entitySelector = this.FindControl<ComboBox>("EntitySelector");
            if (entitySelector != null)
            {
                entitySelector.SelectionChanged += EntitySelector_SelectionChanged;
            }

            var drawToolButton = this.FindControl<Button>("DrawToolButton");
            if (drawToolButton != null)
            {
                drawToolButton.Click += DrawToolButton_Click;
            }

            var drawModeToggle = this.FindControl<ToggleButton>("DrawModeToggle");
            if (drawModeToggle != null)
            {
                drawModeToggle.Click += DrawModeToggle_Click;
            }

            var waterToggle = this.FindControl<ToggleButton>("AllowWaterToggle");
            if (waterToggle != null)
            {
                waterToggle.IsChecked = false;
                waterToggle.Click += (s, e) =>
                {
                    _allowWaterPaint = waterToggle.IsChecked == true;
                };
            }

            if (this.FindControl<Button>("EquilibrateStatesButton") is Button equilibrateButton)
            {
                equilibrateButton.Click += EquilibrateStatesButton_Click;
            }

            // Setup brush size slider
            var brushSizeSlider = this.FindControl<Slider>("BrushSizeSlider");
            var brushSizeLabel = this.FindControl<TextBlock>("BrushSizeLabel");
            if (brushSizeSlider != null && brushSizeLabel != null)
            {
                brushSizeSlider.Value = _brushSize;
                brushSizeLabel.Text = _brushSize.ToString();
                
                brushSizeSlider.ValueChanged += (s, e) =>
                {
                    _brushSize = (int)Math.Round(e.NewValue);
                    brushSizeLabel.Text = _brushSize.ToString();
                    Debug.WriteLine($"[MAP EDITOR] Brush size changed to: {_brushSize}");
                };
            }

            UpdateEntitySelector();
            UpdateTitle();
        }

        private void LevelSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedIndex >= 0)
            {
                _currentLevel = comboBox.SelectedIndex == 0 ? MapViewLevel.Countries : MapViewLevel.States;
                _mapManager.SetViewType(_currentLevel == MapViewLevel.Countries ? MapViewType.Political : MapViewType.States);
                UpdateEntitySelector();
                UpdateTitle();
                QueueRender();
            }
        }

        private void EntitySelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is ComboBoxItem item && item.Content is string selectedName)
            {
                if (_currentLevel == MapViewLevel.Countries)
                {
                    var country = _mapManager.FindCountryByName(selectedName);
                    _selectedCountry = country;
                    _mapManager.SelectCountry(country);
                    Debug.WriteLine($"[MAP EDITOR] Selected country: {selectedName}");
                    QueueRender();
                }
                else
                {
                    _selectedState = _mapManager.GetAllStates().FirstOrDefault(s => string.Equals(s.StateName, selectedName, StringComparison.OrdinalIgnoreCase));
                    _mapManager.SetSelectedState(_selectedState);
                    Debug.WriteLine($"[MAP EDITOR] Selected state: {selectedName}");
                    QueueRender();
                }
            }
        }

        private void DrawToolButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("[MAP EDITOR] Draw tool clicked");
        }

        private void DrawModeToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            if (toggle != null)
            {
                _isDrawingMode = toggle.IsChecked == true;
                toggle.Content = _isDrawingMode ? "Drawing On" : "Drawing Off";
                toggle.Background = _isDrawingMode ?
                    new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(100, 150, 100)) :
                    new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(102, 102, 102));

                Debug.WriteLine($"[MAP EDITOR] Drawing mode: {(_isDrawingMode ? "ON" : "OFF")}");
            }
        }

        private void EquilibrateStatesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_currentLevel != MapViewLevel.States)
            {
                Debug.WriteLine("[MAP EDITOR] Equilibrate states command ignored when not in States level");
                return;
            }

            try
            {
                Debug.WriteLine("[MAP EDITOR] Equilibrating state borders for visual balance");
                _mapManager.EquilibrateStateBorders(3);
                QueueRender();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAP EDITOR] Failed to equilibrate state borders: {ex.Message}");
            }
        }

        private void UpdateEntitySelector()
        {
            var entitySelector = this.FindControl<ComboBox>("EntitySelector");
            var entitySelectorLabel = this.FindControl<TextBlock>("EntitySelectorLabel");
            
            if (entitySelector != null && entitySelectorLabel != null)
            {
                entitySelector.Items.Clear();
                
                if (_currentLevel == MapViewLevel.Countries)
                {
                    entitySelectorLabel.Text = "Country:";

                    try
                    {
                        var countries = _mapManager.GetAllCountryData();
                        if (countries == null || countries.Count == 0)
                        {
                            entitySelector.Items.Add(new ComboBoxItem { Content = "(No country data loaded)" });
                        }
                        else
                        {
                            foreach (var c in countries.OrderBy(c => c.CountryName))
                            {
                                entitySelector.Items.Add(new ComboBoxItem { Content = c.CountryName });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MAP EDITOR] Failed loading country list: {ex.Message}");
                        entitySelector.Items.Add(new ComboBoxItem { Content = "(Error loading countries)" });
                    }
                }
                else
                {
                    entitySelectorLabel.Text = "State:";
                    
                    var states = _mapManager.GetAllStates();
                    foreach (var state in states.OrderBy(s => s.StateName))
                    {
                        entitySelector.Items.Add(new ComboBoxItem { Content = state.StateName });
                    }
                }
                
                if (entitySelector.Items.Count > 0)
                {
                    entitySelector.SelectedIndex = 0;
                }
            }
        }

        private void UpdateTitle()
        {
            var titleText = this.FindControl<TextBlock>("TitleText");
            if (titleText != null)
            {
                titleText.Text = _currentLevel == MapViewLevel.Countries ?
                    "Map Editor - Countries View" :
                    "Map Editor - States View";
            }

            if (this.FindControl<Button>("EquilibrateStatesButton") is Button equilibrateButton)
            {
                equilibrateButton.IsEnabled = _currentLevel == MapViewLevel.States;
                equilibrateButton.Opacity = equilibrateButton.IsEnabled ? 1.0 : 0.5;
            }
        }

        private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("MapEditorWindow loaded");
            
            var mapImage = this.FindControl<Image>("MapImage");
            if (mapImage != null)
            {
                mapImage.PointerPressed += OnPointerPressed;
                mapImage.PointerMoved += OnPointerMoved;
                mapImage.PointerReleased += OnPointerReleased; // signature matches (object?, PointerReleasedEventArgs)
                mapImage.PointerWheelChanged += OnPointerWheelChanged;

                _isInitialized = true;
                
                QueueRender();
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isInitialized)
            {
                QueueRender();
            }
        }

        private void MapUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_pendingMapUpdate && !_renderInProgress)
            {
                _pendingMapUpdate = false;
                RenderMap();
            }
        }

        private void QueueRender()
        {
            _pendingMapUpdate = true;
        }

        private void RenderMap()
        {
            lock (_renderLock)
            {
                if (_renderInProgress) return;
                _renderInProgress = true;
            }

            try
            {
                var mapImage = this.FindControl<Image>("MapImage");
                if (mapImage == null) return;

                var effectiveSize = GetEffectiveRenderSize();
                if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;

                // viewOffset and size are in SCREEN (scaled) pixels
                var viewArea = new SKRectI(
                    _viewOffset.X,
                    _viewOffset.Y,
                    _viewOffset.X + (int)effectiveSize.Width,
                    _viewOffset.Y + (int)effectiveSize.Height);

                SKBitmap? bitmap = _mapManager.RenderAdminBitmap(_currentLevel, _currentZoomLevel, viewArea, new SKSizeI((int)effectiveSize.Width, (int)effectiveSize.Height));
                if (bitmap != null)
                {
                    var writeableBitmap = SKBitmapToWriteableBitmap(bitmap);
                    if (writeableBitmap != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            mapImage.Source = writeableBitmap;
                            _writeableBitmap = writeableBitmap;
                        });
                    }
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering map: {ex.Message}");
            }
            finally
            {
                lock (_renderLock) _renderInProgress = false;
            }
        }

        private WriteableBitmap? SKBitmapToWriteableBitmap(SKBitmap skBitmap)
        {
            try
            {
                var writeableBitmap = new WriteableBitmap(
                    new PixelSize(skBitmap.Width, skBitmap.Height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul);

                using (var lockedBitmap = writeableBitmap.Lock())
                {
                    var skPixelSpan = skBitmap.GetPixelSpan();
                    var destSpan = lockedBitmap.Address;
                    var destSize = lockedBitmap.Size.Width * lockedBitmap.Size.Height * 4;
                    
                    System.Runtime.InteropServices.Marshal.Copy(
                        skPixelSpan.ToArray(), 0, destSpan, Math.Min(skPixelSpan.Length, destSize));
                }

                return writeableBitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting SKBitmap to WriteableBitmap: {ex.Message}");
                return null;
            }
        }

        private Avalonia.Size GetEffectiveRenderSize()
        {
            var mapContainer = this.FindControl<Border>("MapContainer");
            if (mapContainer != null)
            {
                return new Avalonia.Size(Math.Max(1, mapContainer.Bounds.Width), 
                               Math.Max(1, mapContainer.Bounds.Height));
            }
            return new Avalonia.Size(800, 600);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                if (_isDrawingMode)
                {
                    _isDrawingStrokeActive = true;
                    _currentStrokeChanges.Clear();
                    var mousePos = e.GetCurrentPoint(this.FindControl<Image>("MapImage"));
                    HandleDrawing((int)mousePos.Position.X, (int)mousePos.Position.Y);
                    e.Handled = true;
                }
                else
                {
                    _isPanning = true;
                    _panStartPoint = e.GetCurrentPoint(null).Position;
                    e.Handled = true;
                }
            }
            else if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
            {
                var mousePos = e.GetCurrentPoint(this.FindControl<Image>("MapImage"));
                HandleSelection((int)mousePos.Position.X, (int)mousePos.Position.Y);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDrawingMode && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                var mousePos = e.GetCurrentPoint(this.FindControl<Image>("MapImage"));
                HandleDrawing((int)mousePos.Position.X, (int)mousePos.Position.Y);
                e.Handled = true;
            }
            else if (_isPanning)
            {
                // FIX: Previous logic inverted the direction (subtracting raw delta) which caused viewOffset to remain near 0
                // resulting in edits always mapping to the top-left of the underlying political grid.
                var mapImage = this.FindControl<Image>("MapImage");
                var currentPoint = e.GetPosition(mapImage);
                var previousPoint = _panStartPoint;
                var delta = previousPoint - currentPoint; // movement since last event
                _panStartPoint = currentPoint;

                _viewOffset.X += (int)delta.X;
                _viewOffset.Y += (int)delta.Y;

                ClampViewOffset();
                QueueRender();
                e.Handled = true;
            }
        }

        private void ClampViewOffset()
        {
            // Ensure we don't scroll outside the map bounds (terrain-space; political view scales internally)
            try
            {
                var effectiveSize = GetEffectiveRenderSize();
                if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;
                var mapSize = _mapManager.GetMapSize(_currentZoomLevel);

                if (mapSize.Width > (int)effectiveSize.Width)
                {
                    if (_viewOffset.X < 0) _viewOffset.X = 0;
                    else if (_viewOffset.X > mapSize.Width - (int)effectiveSize.Width)
                        _viewOffset.X = mapSize.Width - (int)effectiveSize.Width;
                }
                else
                {
                    _viewOffset.X = Math.Max(0, (mapSize.Width - (int)effectiveSize.Width) / 2);
                }

                if (mapSize.Height > (int)effectiveSize.Height)
                {
                    if (_viewOffset.Y < 0) _viewOffset.Y = 0;
                    else if (_viewOffset.Y > mapSize.Height - (int)effectiveSize.Height)
                        _viewOffset.Y = mapSize.Height - (int)effectiveSize.Height;
                }
                else
                {
                    _viewOffset.Y = Math.Max(0, (mapSize.Height - (int)effectiveSize.Height) / 2);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAP EDITOR] ClampViewOffset error: {ex.Message}");
            }
        }

        private void HandleDrawing(int screenX, int screenY)
        {
            if (_useEnhancedEditor)
            {
                var effectiveSize = GetEffectiveRenderSize();
                var outputSize = new SKSizeI((int)effectiveSize.Width, (int)effectiveSize.Height);
                var viewOffsetSnapshot = _viewOffset; // screen pixel offset
                var levelSnapshot = _currentLevel;
                var zoomSnapshot = _currentZoomLevel;

                uint brushValue = 0;
                if (levelSnapshot == MapViewLevel.Countries && _selectedCountry != null) brushValue = (uint)_selectedCountry.RasterCode;
                else if (levelSnapshot == MapViewLevel.States && _selectedState != null) brushValue = (uint)_selectedState.RasterCode;
                if (brushValue == 0) return;

                _enhancedEditor.SetBrushValue(brushValue);
                _enhancedEditor.SetBrushSize(_brushSize);
                _enhancedEditor.SetEditPolicy(_currentEditPolicy);

                int sx = screenX; int sy = screenY;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _enhancedEditor.ApplyEditAsync(levelSnapshot, sx, sy, zoomSnapshot, viewOffsetSnapshot, outputSize);
                        Dispatcher.UIThread.Post(QueueRender);
                    }
                    catch (Exception ex) { Debug.WriteLine($"[MAP EDITOR] Error in enhanced drawing: {ex.Message}"); }
                });
                return;
            }

            int limitW = _currentLevel == MapViewLevel.Countries || _currentLevel == MapViewLevel.States ? _mapManager.PoliticalBaseWidth : _mapManager.BaseWidth;
            int limitH = _currentLevel == MapViewLevel.Countries || _currentLevel == MapViewLevel.States ? _mapManager.PoliticalBaseHeight : _mapManager.BaseHeight;

            int gridX, gridY;
            if (_currentLevel == MapViewLevel.Countries || _currentLevel == MapViewLevel.States)
            {
                var (gx, gy) = _mapManager.ScreenToPoliticalGrid(screenX, screenY, _currentZoomLevel, _viewOffset);
                gridX = Math.Clamp(gx, 0, limitW - 1);
                gridY = Math.Clamp(gy, 0, limitH - 1);
            }
            else
            {
                int cellSize = _mapManager.GetCellSizeForZoom(_currentZoomLevel);
                int mapX = _viewOffset.X + screenX;
                int mapY = _viewOffset.Y + screenY;
                gridX = Math.Clamp(mapX / cellSize, 0, limitW - 1);
                gridY = Math.Clamp(mapY / cellSize, 0, limitH - 1);
            }

            var brushCells = GetBrushCells(gridX, gridY, _brushSize, limitW, limitH).ToList();

            List<(SDPoint cell, int previousId)> changes = new();
            if (_currentLevel == MapViewLevel.Countries && _selectedCountry != null)
            {
                var zero = _mapManager.ChangeAdminControlZeroSum(_currentLevel, _selectedCountry.RasterCode, brushCells);
                if (zero.Count > 0) changes.AddRange(zero);
            }
            else if (_currentLevel == MapViewLevel.States && _selectedState != null)
            {
                var zero = _mapManager.ChangeAdminControlZeroSum(_currentLevel, _selectedState.RasterCode, brushCells);
                if (zero.Count > 0) changes.AddRange(zero);
                if (_allowWaterPaint)
                {
                    var waterOnly = _mapManager.ChangeAdminControlWaterOnly(_currentLevel, _selectedState.RasterCode, brushCells);
                    if (waterOnly.Count > 0) changes.AddRange(waterOnly);
                }
            }

            if (changes.Count > 0)
            {
                _currentStrokeChanges.AddRange(changes);
                QueueRender();
            }
        }

        private void HandleSelection(int screenX, int screenY)
        {
            if (_currentLevel == MapViewLevel.Countries)
            {
                var country = _mapManager.GetCountryAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);
                if (country != null)
                {
                    _selectedCountry = country;
                    _mapManager.SelectCountry(country);
                    UpdateEntitySelectorToSelection(country.CountryName);
                    QueueRender();
                }
            }
            else
            {
                var state = _mapManager.GetStateAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);
                if (state != null)
                {
                    _selectedState = state;
                    _mapManager.SetSelectedState(state);
                    UpdateEntitySelectorToSelection(state.StateName);
                    QueueRender();
                }
                else Debug.WriteLine("[MAP EDITOR] No state at clicked position");
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var mapImage = this.FindControl<Image>("MapImage");
            if (mapImage == null) return;
            var mousePos = e.GetPosition(mapImage);
            int oldZoomLevel = _currentZoomLevel;
            _currentZoomLevel = Math.Clamp(_currentZoomLevel + Math.Sign(e.Delta.Y), 1, MultiResolutionMapManager.PixelsPerCellLevels.Length);
            if (_currentZoomLevel == oldZoomLevel) return;
            int oldCellSize = _mapManager.GetCellSizeForZoom(oldZoomLevel);
            int newCellSize = _mapManager.GetCellSizeForZoom(_currentZoomLevel);
            int newOffsetX = (int)Math.Round((_viewOffset.X + mousePos.X) * (double)newCellSize / oldCellSize) - (int)mousePos.X;
            int newOffsetY = (int)Math.Round((_viewOffset.Y + mousePos.Y) * (double)newCellSize / oldCellSize) - (int)mousePos.Y;
            _viewOffset = new SKPointI(Math.Max(0, newOffsetX), Math.Max(0, newOffsetY));
            QueueRender();
            e.Handled = true;
        }

        private void ExitButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var optionsWindow = new OptionsWindow();
            optionsWindow.Show();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _mapUpdateTimer?.Stop();
            _mapManager?.Dispose();
            _enhancedEditor?.Dispose();
            base.OnClosed(e);
        }

        // Re-introduced helper for brush cells (lost after refactor)
        private IEnumerable<SDPoint> GetBrushCells(int centerX, int centerY, int radius, int limitW, int limitH)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x >= 0 && x < limitW && y >= 0 && y < limitH)
                        yield return new SDPoint(x, y);
                }
            }
        }

        // Re-introduced selector update helper
        private void UpdateEntitySelectorToSelection(string entityName)
        {
            var entitySelector = this.FindControl<ComboBox>("EntitySelector");
            if (entitySelector != null)
            {
                for (int i = 0; i < entitySelector.Items.Count; i++)
                {
                    if (entitySelector.Items[i] is ComboBoxItem item && item.Content?.ToString() == entityName)
                    {
                        entitySelector.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        // Add pointer released handler (removed earlier by accident)
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDrawingStrokeActive)
            {
                if (_currentStrokeChanges.Count > 0)
                {
                    var targetId = _currentLevel == MapViewLevel.Countries ? (_selectedCountry?.RasterCode ?? -1) : (_selectedState?.RasterCode ?? -1);
                    _undoStack.AddFirst(new EditorAction(EditorActionType.ZeroSumAssign, _currentLevel, targetId, new List<(SDPoint cell, int previousId)>(_currentStrokeChanges)));
                    while (_undoStack.Count > MaxUndo) _undoStack.RemoveLast();
                }
                _currentStrokeChanges.Clear();
                _isDrawingStrokeActive = false;
            }
            _isPanning = false;
            e.Handled = true;
        }
    }
}