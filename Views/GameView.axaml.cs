using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame;   // your namespace for MultiResolutionMapManager

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly MultiResolutionMapManager _mapManager;
        private float _currentZoom = 1.5f;

        // Top?left corner of the viewport in *map pixels*
        private SKPointI _viewOffset;

        // Drag state
        private bool _isDragging;
        private SKPointI _lastPointer;

        public GameView()
        {
            InitializeComponent();

            _mapManager = new MultiResolutionMapManager(baseWidth: 256, baseHeight: 256);

            // 1) Center on open
            this.Opened += OnOpened;

            // 2) Re?render when the window is resized
            this.SizeChanged += (_, __) => RenderMap();

            // 3) Drag?to?pan
            MapContainer.PointerPressed += OnPointerPressed;
            MapContainer.PointerMoved += OnPointerMoved;
            MapContainer.PointerReleased += OnPointerReleased;

            // 4) Scroll?to?zoom
            MapContainer.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnOpened(object sender, EventArgs e)
        {
            // Center the view so you start in the middle of the world
            int cellSize = _mapManager.GetCellSize(_currentZoom);
            int worldW = 256 * cellSize;
            int worldH = 256 * cellSize;
            int cw = (int)ClientSize.Width;
            int ch = (int)ClientSize.Height;

            _viewOffset = new SKPointI(
                Math.Max(0, (worldW - cw) / 2),
                Math.Max(0, (worldH - ch) / 2)
            );

            RenderMap();
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            _isDragging = true;
            var p = e.GetPosition(MapContainer);
            _lastPointer = new SKPointI((int)p.X, (int)p.Y);
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDragging)
                return;

            var p = e.GetPosition(MapContainer);
            var now = new SKPointI((int)p.X, (int)p.Y);
            var delta = new SKPointI(_lastPointer.X - now.X, _lastPointer.Y - now.Y);

            _lastPointer = now;
            UpdateOffset(delta);
            RenderMap();
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
        }

        private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (e.Delta.Y == 0)
                return;

            // Compute zoom factor
            float factor = e.Delta.Y > 0 ? 1.2f : 0.8f;
            float newZoom = Math.Clamp(_currentZoom * factor, 0.2f, 10f);

            // Zoom around the mouse position
            var mouse = e.GetPosition(MapContainer);
            float mouseMapX = _viewOffset.X + (float)mouse.X;
            float mouseMapY = _viewOffset.Y + (float)mouse.Y;

            _currentZoom = newZoom;

            // After zoom, keep the same map?pixel under the cursor
            _viewOffset = new SKPointI(
                (int)(mouseMapX - (float)mouse.X),
                (int)(mouseMapY - (float)mouse.Y)
            );

            UpdateOffset(new SKPointI(0, 0));
            RenderMap();
        }

        private void UpdateOffset(SKPointI delta)
        {
            // Clamp so you never pan beyond the map edges
            int cellSize = _mapManager.GetCellSize(_currentZoom);
            int worldW = 256 * cellSize;
            int worldH = 256 * cellSize;
            int cw = (int)ClientSize.Width;
            int ch = (int)ClientSize.Height;

            _viewOffset = new SKPointI(
                Math.Clamp(_viewOffset.X + delta.X, 0, Math.Max(0, worldW - cw)),
                Math.Clamp(_viewOffset.Y + delta.Y, 0, Math.Max(0, worldH - ch))
            );
        }

        private void RenderMap()
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1)
                return;

            // Define our “window” into the world in map?pixel coords
            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + (int)ClientSize.Width,
                _viewOffset.Y + (int)ClientSize.Height
            );

            using var bmp = _mapManager.AssembleView(
                _currentZoom,
                viewArea,
                () => Dispatcher.UIThread.Post(RenderMap, DispatcherPriority.Background)
            );

            using var img = SKImage.FromBitmap(bmp);
            using var ms = new MemoryStream();
            img.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;

            MapImage.Source = new Bitmap(ms);
        }
    }
}
