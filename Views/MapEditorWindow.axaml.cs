using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace Economy_sim
{
    public partial class MapEditorWindow : Window
    {
        private readonly HybridMapManager _mapManager;
        
        // Map rendering fields
        private WriteableBitmap? _writeableBitmap;
        private int _currentZoomLevel = 1;
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _isInitialized = false;
        
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
            
            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            // Setup map update timer
            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();

            // Setup exit button handler
            var exitButton = this.FindControl<Button>("ExitButton");
            if (exitButton != null)
            {
                exitButton.Click += ExitButton_Click;
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

                // Get the political map bitmap
                var bitmap = _mapManager.AssembleView(_currentZoomLevel, viewArea);
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
                _isPanning = true;
                _panStartPoint = e.GetCurrentPoint(null).Position;
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isPanning)
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
            base.OnClosed(e);
        }
    }
}