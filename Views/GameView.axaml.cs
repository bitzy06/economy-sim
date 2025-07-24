using Avalonia; // <--- ADD THIS LINE
using Avalonia.Controls;
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
        private readonly MultiResolutionMapManager _mapManager;
        private float _currentZoom = 1.5f;
        private SKPointI _viewOffset = new SKPointI(0, 0);

        public GameView()
        {
            InitializeComponent();

            _mapManager = new MultiResolutionMapManager(baseWidth: 256, baseHeight: 256);

            this.Opened += (s, e) => RenderMap();

            this.EffectiveViewportChanged += OnViewportChanged;
        }

        // This method's signature is now valid because 'EffectiveViewportChangedEventArgs' is recognized.
        private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            RenderMap();
        }

        private void RenderMap()
        {
            if (this.ClientSize.Width < 1 || this.ClientSize.Height < 1)
                return;

            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + (int)this.ClientSize.Width,
                _viewOffset.Y + (int)this.ClientSize.Height
            );

            SKBitmap skBitmap = _mapManager.AssembleView(
                _currentZoom,
                viewArea,
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