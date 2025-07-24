using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame;
using System;
using System.IO;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private MultiResolutionMapManager _mapManager;
        private float _currentZoom = 1.0f; // Start at 1:1 zoom
        private Point _viewOffset = new Point(0, 0); // High-precision world coordinates
        private Size _lastRenderedSize = new Size(0, 0);

        private bool _isPanning = false;
        private Point _panStartPoint;
        private Point _panStartOffset;

        // World size is defined in abstract units, not pixels.
        private const int WorldWidth = 4096;
        private const int WorldHeight = 2048;

        public GameView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;

            MapImage.PointerPressed += OnPointerPressed;
            MapImage.PointerMoved += OnPointerMoved;
            MapImage.PointerReleased += OnPointerReleased;
            // Add the PointerWheelChanged event handler for zooming
            MapImage.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            this.Loaded -= OnLoaded;
            _mapManager = new MultiResolutionMapManager(WorldWidth, WorldHeight);
            RenderMap();
            this.LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (this.ClientSize.Width > 1 && this.ClientSize != _lastRenderedSize)
            {
                RenderMap();
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this);
                _panStartOffset = _viewOffset;
                e.Pointer.Capture(MapImage);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetPosition(this);
            var totalDelta = currentPoint - _panStartPoint;

            // This panning logic is robust and correct.
            _viewOffset = new Point(
                _panStartOffset.X - (totalDelta.X / _currentZoom),
                _panStartOffset.Y - (totalDelta.Y / _currentZoom)
            );

            ClampViewOffset();
            RenderMap();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Adjust zoom based on scroll wheel delta
            float zoomDelta = (float)e.Delta.Y * 0.1f;
            _currentZoom += zoomDelta;

            // Clamp zoom level to reasonable values
            if (_currentZoom < 0.2f) _currentZoom = 0.2f;
            if (_currentZoom > 20f) _currentZoom = 20f;

            // After zooming, we need to re-clamp the offset and re-render
            ClampViewOffset();
            RenderMap();
        }

        private void ClampViewOffset()
        {
            if (_mapManager == null) return;

            var clientSize = this.ClientSize;
            var logicalWidth = (int)(clientSize.Width / _currentZoom);
            var logicalHeight = (int)(clientSize.Height / _currentZoom);

            double newX = _viewOffset.X;
            double newY = _viewOffset.Y;

            if (newX < 0) newX = 0;
            if (newY < 0) newY = 0;

            if (newX > WorldWidth - logicalWidth) newX = WorldWidth - logicalWidth;
            if (newY > WorldHeight - logicalHeight) newY = WorldHeight - logicalHeight;

            _viewOffset = new Point(newX, newY);
        }

        private void RenderMap()
        {
            var clientSize = this.ClientSize;
            if (clientSize.Width < 1 || clientSize.Height < 1 || _mapManager == null)
                return;

            _lastRenderedSize = clientSize;

            // --- THIS IS THE KEY FIX ---
            // The viewArea passed to the MapManager must be in PIXEL coordinates for the current zoom level.
            // It is NOT in abstract world coordinates.
            int cellSize = _mapManager.GetCellSize(_currentZoom);
            var viewAreaInPixels = new SKRectI(
                (int)(_viewOffset.X * cellSize),
                (int)(_viewOffset.Y * cellSize),
                (int)(_viewOffset.X * cellSize + clientSize.Width),
                (int)(_viewOffset.Y * cellSize + clientSize.Height)
            );

            // The MapManager will now return a bitmap that is exactly the size of our control.
            SKBitmap skBitmap = _mapManager.AssembleView(
                _currentZoom,
                viewAreaInPixels,
                () => Dispatcher.UIThread.Post(RenderMap, DispatcherPriority.Background)
            );

            if (skBitmap == null || skBitmap.Width <= 1 || skBitmap.Height <= 1)
            {
                skBitmap?.Dispose();
                return;
            }

            try
            {
                using var skImage = SKImage.FromBitmap(skBitmap);
                using var stream = new MemoryStream();
                skImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
                stream.Position = 0;

                var avaloniaBitmap = new Bitmap(stream);
                this.MapImage.Source = avaloniaBitmap;
            }
            finally
            {
                skBitmap.Dispose();
            }
        }
    }
}