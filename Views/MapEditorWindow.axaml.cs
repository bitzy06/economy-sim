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

namespace Economy_sim
{
    public partial class MapEditorWindow : Window
    {
        private readonly HybridMapManager _mapManager;
        private readonly StateBorderManager _stateBorderManager;
        
        // Map rendering fields
        private WriteableBitmap? _writeableBitmap;
        private int _currentZoomLevel = 1;
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _isInitialized = false;
        
        // Map editor state
        private MapViewLevel _currentLevel = MapViewLevel.Countries;
        private bool _isDrawingMode = false;
        private IndexedCountryFeature? _selectedCountry = null;
        private StateBorderManager.StateFeature? _selectedState = null;
        
        private readonly DispatcherTimer _mapUpdateTimer;
        private bool _pendingMapUpdate = false;
        private readonly object _renderLock = new object();
        private bool _renderInProgress = false;

        public MapEditorWindow()
        {
            InitializeComponent();
            
            // Initialize map manager with political view
            _mapManager = new HybridMapManager(baseWidth: 4096, baseHeight: 2048);
            _mapManager.SetViewType(MapViewType.Political);
            
            // Initialize state border manager
            _stateBorderManager = new StateBorderManager();
            
            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            // Setup map update timer
            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();

            // Setup UI event handlers
            SetupUIEventHandlers();
        }

        private void SetupUIEventHandlers()
        {
            // Exit button
            var exitButton = this.FindControl<Button>("ExitButton");
            if (exitButton != null)
            {
                exitButton.Click += ExitButton_Click;
            }

            // Level selector
            var levelSelector = this.FindControl<ComboBox>("LevelSelector");
            if (levelSelector != null)
            {
                levelSelector.SelectionChanged += LevelSelector_SelectionChanged;
            }

            // Entity selector
            var entitySelector = this.FindControl<ComboBox>("EntitySelector");
            if (entitySelector != null)
            {
                entitySelector.SelectionChanged += EntitySelector_SelectionChanged;
            }

            // Draw tool button
            var drawToolButton = this.FindControl<Button>("DrawToolButton");
            if (drawToolButton != null)
            {
                drawToolButton.Click += DrawToolButton_Click;
            }

            // Draw mode toggle
            var drawModeToggle = this.FindControl<ToggleButton>("DrawModeToggle");
            if (drawModeToggle != null)
            {
                drawModeToggle.Click += DrawModeToggle_Click;
            }

            // Initialize UI state
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
                    // Find and select country
                    // For now, just store the name - in a full implementation this would
                    // interface with the country data
                    Debug.WriteLine($"[MAP EDITOR] Selected country: {selectedName}");
                }
                else
                {
                    // Find and select state
                    _selectedState = _stateBorderManager.GetStateByName(selectedName);
                    Debug.WriteLine($"[MAP EDITOR] Selected state: {selectedName}");
                }
            }
        }

        private void DrawToolButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("[MAP EDITOR] Draw tool clicked");
            // For now, just log - in a full implementation this would activate specific drawing tools
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
                    
                    // Add mock countries for now
                    var countries = new[] { "United States", "Canada", "Mexico", "Germany", "France", "United Kingdom", "Australia" };
                    foreach (var country in countries)
                    {
                        entitySelector.Items.Add(new ComboBoxItem { Content = country });
                    }
                }
                else
                {
                    entitySelectorLabel.Text = "State:";
                    
                    // Add states from state manager
                    var states = _stateBorderManager.GetAllStates();
                    foreach (var state in states)
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
                // Attach input event handlers for map interaction
                mapImage.PointerPressed += OnPointerPressed;
                mapImage.PointerMoved += OnPointerMoved;
                mapImage.PointerReleased += OnPointerReleased;
                mapImage.PointerWheelChanged += OnPointerWheelChanged;

                _isInitialized = true;
                
                // Queue initial render
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

                // Create view area for rendering
                var viewArea = new SKRectI(_viewOffset.X, _viewOffset.Y, 
                    _viewOffset.X + (int)effectiveSize.Width, 
                    _viewOffset.Y + (int)effectiveSize.Height);

                SKBitmap? bitmap = null;

                if (_currentLevel == MapViewLevel.Countries)
                {
                    // Render countries using existing political map
                    bitmap = _mapManager.AssembleView(_currentZoomLevel, viewArea);
                }
                else
                {
                    // Render states with country overlay
                    bitmap = RenderStatesWithCountryOverlay(viewArea, effectiveSize);
                }

                if (bitmap != null)
                {
                    // Convert SKBitmap to WriteableBitmap for display
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

        private SKBitmap? RenderStatesWithCountryOverlay(SKRectI viewArea, Size effectiveSize)
        {
            try
            {
                // Create a bitmap for state rendering
                var bitmap = new SKBitmap((int)effectiveSize.Width, (int)effectiveSize.Height);
                using (var canvas = new SKCanvas(bitmap))
                {
                    // Clear with a background color
                    canvas.Clear(SKColors.LightBlue);

                    // Calculate the viewport for rendering
                    var viewport = new SKRect(viewArea.Left, viewArea.Top, viewArea.Right, viewArea.Bottom);

                    // First, render state fills
                    _stateBorderManager.RenderStateFills(canvas, viewport);

                    // Then, render state borders (thicker)
                    _stateBorderManager.RenderStateBorders(canvas, viewport, 2.0f, SKColors.Black);

                    // Finally, overlay country borders (thinner)
                    RenderCountryBordersOverlay(canvas, viewport);
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error rendering states with country overlay: {ex.Message}");
                return null;
            }
        }

        private void RenderCountryBordersOverlay(SKCanvas canvas, SKRect viewport)
        {
            try
            {
                // Get country borders from the political map manager and render them as thin overlays
                // For now, we'll create a simple mock implementation
                using (var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f,
                    Color = SKColors.DarkGray,
                    IsAntialias = true
                })
                {
                    // Mock country borders - in a real implementation, this would get actual country geometry
                    // and render it as thin lines over the states
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
                    // Copy pixel data safely
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

        private Size GetEffectiveRenderSize()
        {
            var mapContainer = this.FindControl<Border>("MapContainer");
            if (mapContainer != null)
            {
                return new Size(Math.Max(1, mapContainer.Bounds.Width), 
                               Math.Max(1, mapContainer.Bounds.Height));
            }
            return new Size(800, 600); // Fallback size
        }

        // Input event handlers for map interaction
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                if (_isDrawingMode)
                {
                    // Handle drawing
                    var mousePos = e.GetCurrentPoint(this.FindControl<Image>("MapImage"));
                    HandleDrawing((int)mousePos.Position.X, (int)mousePos.Position.Y);
                    e.Handled = true;
                }
                else
                {
                    // Handle panning
                    _isPanning = true;
                    _panStartPoint = e.GetCurrentPoint(null).Position;
                    e.Handled = true;
                }
            }
            else if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
            {
                // Right-click for selection
                var mousePos = e.GetCurrentPoint(this.FindControl<Image>("MapImage"));
                HandleSelection((int)mousePos.Position.X, (int)mousePos.Position.Y);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDrawingMode && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                // Continue drawing
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
            Debug.WriteLine($"[MAP EDITOR] Drawing at ({screenX}, {screenY})");
            
            if (_currentLevel == MapViewLevel.Countries && _selectedCountry != null)
            {
                Debug.WriteLine($"[MAP EDITOR] Drawing with country: {_selectedCountry.CountryName}");
                // In a full implementation, this would modify the underlying political map data
                // to assign the tile at this position to the selected country
            }
            else if (_currentLevel == MapViewLevel.States && _selectedState != null)
            {
                Debug.WriteLine($"[MAP EDITOR] Drawing with state: {_selectedState.StateName}");
                // In a full implementation, this would modify the underlying state map data
                // to assign the tile at this position to the selected state
            }
            
            // Force re-render to show changes
            QueueRender();
        }

        private void HandleSelection(int screenX, int screenY)
        {
            Debug.WriteLine($"[MAP EDITOR] Selection at ({screenX}, {screenY})");
            
            if (_currentLevel == MapViewLevel.Countries)
            {
                // Use existing country detection from map manager
                var country = _mapManager.GetCountryAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);
                if (country != null)
                {
                    _selectedCountry = country;
                    Debug.WriteLine($"[MAP EDITOR] Selected country: {country.CountryName}");
                    
                    // Update the entity selector to reflect the selection
                    UpdateEntitySelectorToSelection(country.CountryName);
                }
            }
            else
            {
                // For states, we'd need to implement state detection similar to country detection
                // For now, just log the attempt
                Debug.WriteLine("[MAP EDITOR] State selection not yet implemented - would detect state at position");
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

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
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
            // Return to options window
            var optionsWindow = new OptionsWindow();
            optionsWindow.Show();
            
            // Close this map editor window
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up resources
            _mapUpdateTimer?.Stop();
            _mapManager?.Dispose();
            _stateBorderManager?.Dispose();
            base.OnClosed(e);
        }
    }
}