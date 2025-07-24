using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly MultiResolutionMapManager _mapManager;
        private float _currentZoom = 1.5f;
        private SKPointI _viewOffset = SKPointI.Empty;

        public GameView()
        {
            InitializeComponent();

            _mapManager = new MultiResolutionMapManager(baseWidth: 256, baseHeight: 256);

            // Kick off the first render on open, attach, or resize:
            Opened += async (_, __) => await RenderMapAsync();
            MapImage.AttachedToVisualTree += async (_, __) => await RenderMapAsync();
            MapImage.SizeChanged += async (_, __) => await RenderMapAsync();
        }

        private async Task RenderMapAsync()
        {
            // 1) Measure the control
            int w = (int)MapImage.Bounds.Width;
            int h = (int)MapImage.Bounds.Height;
            if (w < 1 || h < 1)
                return;

            // 2) Compute world?coords area
            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + w,
                _viewOffset.Y + h);

            // 3) Ensure all nearby tiles exist (on disk/in memory)
            try
            {
                await _mapManager.PreloadTilesAsync(
                    zoom: _currentZoom,
                    view: viewArea,
                    radius: 1,
                    token: CancellationToken.None);
            }
            catch (Exception ex)
            {
                // If generation blows up, at least we’ll see why:
                Console.WriteLine($"[RenderMap] PreloadTiles failed: {ex}");
            }

            // 4) Stitch them together
            using var skBmp = _mapManager.AssembleView(
                zoom: _currentZoom,
                viewArea: viewArea,
                triggerRefresh: null);    // no extra refresh needed, we awaited preload

            if (skBmp == null || skBmp.Width <= 1 || skBmp.Height <= 1)
                return;

            // 5) Encode to PNG bytes
            using var skImg = SKImage.FromBitmap(skBmp);
            using var data = skImg.Encode(SKEncodedImageFormat.Png, 100);
            var pngBytes = data.ToArray();   // keep bytes alive

            // 6) Dispatch to UI thread and assign
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var ms = new MemoryStream(pngBytes);
                MapImage.Source = new Bitmap(ms);
            });
        }
    }
}
