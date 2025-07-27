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

        private void OnOpened(object? sender, EventArgs e)
        {
            // Choose an initial zoom so the world roughly fits the window
            int cw = (int)ClientSize.Width;
            int ch = (int)ClientSize.Height;

            float zoom = _currentZoom;
            for (float z = 1f; z <= 10f; z += 0.25f)
            {
                int cs = _mapManager.GetCellSize(z);
                if (256 * cs >= cw && 256 * cs >= ch)
                {
                    zoom = z;
                    break;
                }
            }

            _currentZoom = zoom;
            int cellSize = _mapManager.GetCellSize(_currentZoom);
            int worldW = 256 * cellSize;
            int worldH = 256 * cellSize;

            // start centered even when the map is smaller than the window
            _viewOffset = new SKPointI(
                (worldW - cw) / 2,
                (worldH - ch) / 2
            );

            UpdateOffset(new SKPointI(0, 0));
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

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.Delta.Y == 0)
                return;

            // Compute zoom factor
            float factor = e.Delta.Y > 0 ? 1.2f : 0.8f;
            float newZoom = Math.Clamp(_currentZoom * factor, 0.2f, 10f);

            var mouse = e.GetPosition(MapContainer);

            float oldCell = _mapManager.GetCellSize(_currentZoom);
            float newCell = _mapManager.GetCellSize(newZoom);

            float worldX = (_viewOffset.X + (float)mouse.X) / oldCell;
            float worldY = (_viewOffset.Y + (float)mouse.Y) / oldCell;

            _currentZoom = newZoom;

            _viewOffset = new SKPointI(
                (int)(worldX * newCell - mouse.X),
                (int)(worldY * newCell - mouse.Y)
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

            int minX = worldW <= cw ? -(cw - worldW) / 2 : 0;
            int maxX = worldW <= cw ? minX : worldW - cw;
            int minY = worldH <= ch ? -(ch - worldH) / 2 : 0;
            int maxY = worldH <= ch ? minY : worldH - ch;

            _viewOffset = new SKPointI(
                Math.Clamp(_viewOffset.X + delta.X, minX, maxX),
                Math.Clamp(_viewOffset.Y + delta.Y, minY, maxY)
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
