using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame;
using System;
using System.IO;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly MultiResolutionMapManager _mapManager;
        private float _currentZoom = 1.5f;
        private SKPointI _viewOffset = new SKPointI(0, 0);

        // This will store the size of the last render to prevent unnecessary updates.
        private Size _lastRenderedSize = new Size(0, 0);

        // Add constants to control zoom behavior
        private const float ZoomIncrement = 0.2f;
        private const float MinZoom = 0.5f;
        private const float MaxZoom = 5.0f;

        // Panning state tracking
        private bool _isPanning = false;
        private Point _lastPanPosition;

        public GameView()
        {
            InitializeComponent();
            _mapManager = new MultiResolutionMapManager(baseWidth: 256, baseHeight: 256);

            // Use the 'Loaded' event, which fires once when the window is initialized and shown.
            this.Loaded += OnLoaded;

            // Add pointer wheel event handler
            this.PointerWheelChanged += OnPointerWheelChanged;

            // Add pointer events for panning
            this.PointerPressed += OnPointerPressed;
            this.PointerMoved += OnPointerMoved;
            this.PointerReleased += OnPointerReleased;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Only start panning with primary button (usually left mouse button)
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(MapImage);

                // Set the cursor to indicate grabbing/panning
                this.Cursor = new Cursor(StandardCursorType.Hand);

                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isPanning)
            {
                var currentPosition = e.GetPosition(MapImage);

                // Calculate the distance moved
                int deltaX = (int)(_lastPanPosition.X - currentPosition.X);
                int deltaY = (int)(_lastPanPosition.Y - currentPosition.Y);

                // Only update if the movement is significant
                if (Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1)
                {
                    // Update view offset based on the movement
                    _viewOffset = new SKPointI(_viewOffset.X + deltaX, _viewOffset.Y + deltaY);

                    // Update the last position
                    _lastPanPosition = currentPosition;

                    // Re-render the map
                    RenderMap(this.ClientSize);
                }

                e.Handled = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning && e.InitialPressMouseButton == MouseButton.Left)
            {
                _isPanning = false;

                // Reset the cursor
                this.Cursor = new Cursor(StandardCursorType.Arrow);

                e.Handled = true;
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // Determine zoom direction based on wheel delta
            float zoomDelta = e.Delta.Y > 0 ? ZoomIncrement : -ZoomIncrement;

            // Calculate new zoom level
            float newZoom = _currentZoom + zoomDelta;

            // Apply zoom limits
            newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);

            // Only update if zoom actually changed
            if (Math.Abs(newZoom - _currentZoom) > 0.01f)
            {
                // Store cursor position for zooming toward cursor point
                var position = e.GetPosition(MapImage);

                // Calculate view offset adjustment to zoom toward cursor
                int centerX = (int)position.X;
                int centerY = (int)position.Y;

                // Adjust view offset to keep the point under cursor fixed during zoom
                float zoomFactor = newZoom / _currentZoom;
                int newOffsetX = _viewOffset.X + (int)(centerX * (1 - zoomFactor));
                int newOffsetY = _viewOffset.Y + (int)(centerY * (1 - zoomFactor));

                // Update zoom and view offset
                _currentZoom = newZoom;
                _viewOffset = new SKPointI(newOffsetX, newOffsetY);

                // Re-render the map with new zoom level
                RenderMap(this.ClientSize);

                // Mark the event as handled
                e.Handled = true;
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Unsubscribe so this logic only runs once at startup.
            this.Loaded -= OnLoaded;

            // --- Initial render based on the primary screen's full size ---
            if (Screens.Primary != null)
            {
                // 1. Get the primary screen's full pixel bounds (includes areas covered by the taskbar).
                var pixelSize = Screens.Primary.Bounds.Size;
                // 2. Get the screen's DPI scaling factor (e.g., 1.0 for 100%, 1.5 for 150%).
                var scaling = Screens.Primary.Scaling;
                // 3. Convert the raw pixel size to device-independent "logical" units.
                var logicalSize = new Size(pixelSize.Width / scaling, pixelSize.Height / scaling);

                // 4. Force the very first map render to use this exact screen size.
                RenderMap(logicalSize);
            }

            // After the initial render, use the robust LayoutUpdated for any future resizes.
            this.LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // Only re-render if the window's client size has actually changed.
            if (this.ClientSize.Width > 1 && this.ClientSize != _lastRenderedSize)
            {
                RenderMap(this.ClientSize);
            }
        }

        // MODIFIED: RenderMap now takes the target size as a parameter.
        private void RenderMap(Size mapSize)
        {
            // Store the size we're rendering at to prevent redundant updates.
            _lastRenderedSize = mapSize;

            if (mapSize.Width < 1 || mapSize.Height < 1)
                return;

            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + (int)mapSize.Width,
                _viewOffset.Y + (int)mapSize.Height
            );

            SKBitmap skBitmap = _mapManager.AssembleView(
                _currentZoom,
                viewArea,
                // The refresh callback for when tiles load asynchronously.
                () => Dispatcher.UIThread.Post(() => { if (this.IsVisible) RenderMap(this.ClientSize); })
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