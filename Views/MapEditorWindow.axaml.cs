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
        private readonly StateBorderManager _stateBorderManager;
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
            
            _mapManager = new HybridMapManager(baseWidth: 4096, baseHeight: 2048);
            _mapManager.SetViewType(MapViewType.Political);
            _stateBorderManager = new StateBorderManager();
            
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
                    _selectedState = _stateBorderManager.GetStateByName(selectedName);
                    _stateBorderManager.SetSelectedState(_selectedState);
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
                    
                    var states = _stateBorderManager.GetAllStates();
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
        }

        private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("MapEditorWindow loaded");
            
            var mapImage = this.FindControl<Image>("MapImage");
            if (mapImage != null)
            {
                mapImage.PointerPressed += OnPointerPressed;
                mapImage.PointerMoved += OnPointerMoved;
                mapImage.PointerReleased += OnPointerReleased;
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

                var viewArea = new SKRectI(_viewOffset.X, _viewOffset.Y, 
                    _viewOffset.X + (int)effectiveSize.Width, 
                    _viewOffset.Y + (int)effectiveSize.Height);

                SKBitmap? bitmap = null;

                if (_currentLevel == MapViewLevel.Countries)
                {
                    bitmap = _mapManager.AssembleView(_currentZoomLevel, viewArea);
                }
                else
                {
                    bitmap = RenderStatesWithCountryOverlay(viewArea, effectiveSize);
                }

                if (bitmap != null)
                {
                    var writeableBitmap = SKBitmapToWriteableBitmap(bitmap);
                    if (writeableBitmap != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            mapImage.Source = writeableBitmap;
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
                lock (_renderLock)
                {
                    _renderInProgress = false;
                }
            }
        }

        private SKBitmap? RenderStatesWithCountryOverlay(SKRectI viewArea, Avalonia.Size effectiveSize)
        {
            try
            {
                var bitmap = new SKBitmap((int)effectiveSize.Width, (int)effectiveSize.Height);
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.LightBlue);
                    var viewport = new SKRect(viewArea.Left, viewArea.Top, viewArea.Right, viewArea.Bottom);
                    var mapSize = _mapManager.GetMapSize(_currentZoomLevel);

                    _stateBorderManager.RenderStateFills(canvas, viewport, mapSize);
                    _stateBorderManager.RenderStateBorders(canvas, viewport, mapSize, 2.0f, SKColors.Black);
                    RenderCountryBordersOverlay(canvas, viewport, mapSize);
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering states with country overlay: {ex.Message}");
                return null;
            }
        }

        private void RenderCountryBordersOverlay(SKCanvas canvas, SKRect viewport, SKSizeI mapPixelSize)
        {
            try
            {
                using (var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    Color = SKColors.DarkGray,
                    IsAntialias = true
                })
                {
                    canvas.Save();
                    canvas.Translate(-viewport.Left, -viewport.Top);
                    canvas.DrawRect(viewport, paint);
                    canvas.Restore();
                    Debug.WriteLine("[MAP EDITOR] Country border overlay rendered (mock implementation)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering country borders overlay: {ex.Message}");
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
                var currentPoint = e.GetCurrentPoint(null).Position;
                var deltaX = currentPoint.X - _panStartPoint.X;
                var deltaY = currentPoint.Y - _panStartPoint.Y;

                _viewOffset.X = Math.Max(0, _viewOffset.X - (int)deltaX);
                _viewOffset.Y = Math.Max(0, _viewOffset.Y - (int)deltaY);

                _panStartPoint = currentPoint;

                QueueRender();
                e.Handled = true;
            }
        }

        private void HandleDrawing(int screenX, int screenY)
        {
            if (_useEnhancedEditor)
            {
                // Use the new enhanced editor
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Set brush value based on current selection
                        uint brushValue = 0;
                        if (_currentLevel == MapViewLevel.Countries && _selectedCountry != null)
                        {
                            brushValue = (uint)_selectedCountry.RasterCode;
                        }
                        else if (_currentLevel == MapViewLevel.States && _selectedState != null)
                        {
                            brushValue = (uint)_selectedState.RasterCode;
                        }
                        
                        _enhancedEditor.SetBrushValue(brushValue);
                        _enhancedEditor.SetBrushSize(_brushSize);
                        _enhancedEditor.SetEditPolicy(_currentEditPolicy);
                        
                        await _enhancedEditor.ApplyEditAsync(screenX, screenY, _currentZoomLevel, _viewOffset);
                        
                        // Trigger UI refresh
                        Dispatcher.UIThread.Post(() => QueueRender());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MAP EDITOR] Error in enhanced drawing: {ex.Message}");
                    }
                });
                return;
            }
            
            // Original drawing logic as fallback
            int cellSize = _mapManager.GetCellSizeForZoom(_currentZoomLevel);
            int mapX = screenX + _viewOffset.X;
            int mapY = screenY + _viewOffset.Y;
            int gridX = Math.Clamp(mapX / cellSize, 0, 4096 - 1);
            int gridY = Math.Clamp(mapY / cellSize, 0, 2048 - 1);

            var brushCells = GetBrushCells(gridX, gridY, _brushSize).ToList();

            List<(SDPoint cell, int previousId)> changes = new();
            if (_currentLevel == MapViewLevel.Countries && _selectedCountry != null)
            {
                int rasterCode = _selectedCountry.RasterCode;

                var zero = _mapManager.ChangeCountryControlZeroSum(rasterCode, brushCells);
                if (zero.Count > 0)
                    changes.AddRange(zero);
            }
            else if (_currentLevel == MapViewLevel.States && _selectedState != null)
            {
                // Border-aware state editing (works from inside selected state as well)
                var zero = _stateBorderManager.ChangeControlZeroSum(_selectedState.RasterCode, brushCells);
                if (zero.Count > 0)
                    changes.AddRange(zero);

                // Optional water paint
                if (_allowWaterPaint)
                {
                    var waterOnly = _stateBorderManager.ChangeControlWaterOnly(_selectedState.RasterCode, brushCells);
                    if (waterOnly.Count > 0) changes.AddRange(waterOnly);
                }
            }

            if (changes.Count > 0)
            {
                _currentStrokeChanges.AddRange(changes);
                QueueRender();
            }
        }

        private IEnumerable<SDPoint> GetBrushCells(int centerX, int centerY, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (x >= 0 && x < 4096 && y >= 0 && y < 2048)
                        yield return new SDPoint(x, y);
                }
            }
        }

        private void PushUndo(EditorAction action)
        {
            _undoStack.AddFirst(action);
            while (_undoStack.Count > MaxUndo)
                _undoStack.RemoveLast();
        }

        private void UndoLastAction()
        {
            if (_undoStack.First == null) return;
            var action = _undoStack.First.Value;
            _undoStack.RemoveFirst();

            switch (action.Level)
            {
                case MapViewLevel.Countries:
                    foreach (var (cell, prev) in action.Changes)
                    {
                        if (prev >= 0)
                            _mapManager.ChangeCountryControlAtGrid(prev, new[] { cell });
                    }
                    break;
                case MapViewLevel.States:
                    foreach (var (cell, prev) in action.Changes)
                    {
                        if (prev >= 0)
                            _stateBorderManager.ChangeControlAtGrid(prev, new[] { cell });
                    }
                    break;
            }
            QueueRender();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z)
            {
                if (_useEnhancedEditor)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            bool result = await _enhancedEditor.UndoAsync();
                            if (result)
                            {
                                Dispatcher.UIThread.Post(() => QueueRender());
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MAP EDITOR] Error during enhanced undo: {ex.Message}");
                        }
                    });
                }
                else
                {
                    UndoLastAction();
                }
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y)
            {
                if (_useEnhancedEditor)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            bool result = await _enhancedEditor.RedoAsync();
                            if (result)
                            {
                                Dispatcher.UIThread.Post(() => QueueRender());
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MAP EDITOR] Error during enhanced redo: {ex.Message}");
                        }
                    });
                }
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                // Toggle border-aware editing
                _currentEditPolicy = _currentEditPolicy == EditPolicy.FillAllSubcells 
                    ? EditPolicy.BorderAware 
                    : EditPolicy.FillAllSubcells;
                
                Debug.WriteLine($"[MAP EDITOR] Edit policy changed to: {_currentEditPolicy}");
                e.Handled = true;
            }
            else if (e.Key == Key.P)
            {
                // Toggle precision indicator
                _enhancedEditor.TogglePrecisionIndicator();
                e.Handled = true;
            }
            else if (e.Key == Key.G)
            {
                // Toggle dirty tile glow
                _enhancedEditor.ToggleDirtyTileGlow();
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                // Toggle enhanced editor
                _useEnhancedEditor = !_useEnhancedEditor;
                Debug.WriteLine($"[MAP EDITOR] Enhanced editor: {(_useEnhancedEditor ? "ON" : "OFF")}");
                e.Handled = true;
            }
        }

        private void UpdateEntitySelectorToSelection(string entityName)
        {
            var entitySelector = this.FindControl<ComboBox>("EntitySelector");
            if (entitySelector != null)
            {
                for (int i = 0; i < entitySelector.Items.Count; i++)
                {
                    if (entitySelector.Items[i] is ComboBoxItem item && 
                        item.Content?.ToString() == entityName)
                    {
                        entitySelector.SelectedIndex = i;
                        break;
                    }
                }
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
                var state = _stateBorderManager.GetStateAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);
                if (state != null)
                {
                    _selectedState = state;
                    _stateBorderManager.SetSelectedState(state);
                    UpdateEntitySelectorToSelection(state.StateName);
                    QueueRender();
                }
                else
                {
                    Debug.WriteLine("[MAP EDITOR] No state at clicked position");
                }
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDrawingStrokeActive)
            {
                // finalize stroke as a single undo action
                if (_currentStrokeChanges.Count > 0)
                {
                    var targetId = _currentLevel == MapViewLevel.Countries ? (_selectedCountry?.RasterCode ?? -1) : (_selectedState?.RasterCode ?? -1);
                    PushUndo(new EditorAction(EditorActionType.ZeroSumAssign, _currentLevel, targetId, new List<(SDPoint cell, int previousId)>(_currentStrokeChanges)));
                }
                _currentStrokeChanges.Clear();
                _isDrawingStrokeActive = false;
            }
            _isPanning = false;
            e.Handled = true;
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

            _viewOffset = new SKPointI(newOffsetX, newOffsetY);

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
            _stateBorderManager?.Dispose();
            _enhancedEditor?.Dispose();
            base.OnClosed(e);
        }
    }
}