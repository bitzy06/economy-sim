using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame; // Assuming MultiResolutionMapManager is in this namespace
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly MultiResolutionMapManager _mapManager;

        // --- Optimized Rendering Fields ---
        private WriteableBitmap _writeableBitmap; // Use a WriteableBitmap for high-performance updates.
        private int _currentZoomLevel = 1;
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _isInitialized = false;

        private readonly DispatcherTimer _mapUpdateTimer;
        private DispatcherTimer _initialRenderTimer; // Timer to poll for initial size.
        private bool _pendingMapUpdate = false;
        private readonly object _renderLock = new object();
        private bool _renderInProgress = false;


        public GameView()
        {
            InitializeComponent();
            _mapManager = new MultiResolutionMapManager(baseWidth: 4096, baseHeight: 2048);
            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // Render at ~60fps during pan
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();
        }

        private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("OnWindowLoaded called");
            if (this.MapImage != null)
            {
                // Attach input event handlers
                this.MapImage.PointerPressed += OnPointerPressed;
                this.MapImage.PointerMoved += OnPointerMoved;
                this.MapImage.PointerReleased += OnPointerReleased;
                this.MapImage.PointerWheelChanged += OnPointerWheelChanged;

                // Flag that the view is ready
                _isInitialized = true;

                // Use a timer to poll for a valid size, as Loaded/SizeChanged can be unreliable at startup.
                _initialRenderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, InitialRenderTimer_Tick);
                _initialRenderTimer.Start();
            }
        }

        /// <summary>
        /// This timer will tick until it finds a valid size for the MapImage control,
        /// ensuring the initial render happens correctly.
        /// </summary>
        private void InitialRenderTimer_Tick(object? sender, EventArgs e)
        {
            // This check will run repeatedly until the layout is ready.
            // We now check the Window's ClientSize directly, as the Image's bounds can be unreliable at startup.
            if (_isInitialized && this.ClientSize.Width > 1 && this.ClientSize.Height > 1)
            {
                // Stop the timer, we don't need it anymore.
                _initialRenderTimer?.Stop();
                _initialRenderTimer = null;

                Debug.WriteLine($"Initial size detected via timer using ClientSize: {this.ClientSize}. Triggering render.");
                UpdateBitmapSource(PixelSize.FromSize(this.ClientSize, 1.0));
                QueueRender();
            }
        }


        /// <summary>
        /// This is the most reliable place to create/resize the bitmap after startup,
        /// as it guarantees the control has a valid size.
        /// </summary>
        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isInitialized && this.MapImage != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                Debug.WriteLine($"Size changed to {e.NewSize}, updating bitmap and re-rendering.");
                UpdateBitmapSource(PixelSize.FromSize(e.NewSize, 1.0));
                QueueRender();
            }
        }

        private void MapUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_pendingMapUpdate)
            {
                _pendingMapUpdate = false;
                QueueRender();
            }
        }

        #region Pointer Event Handlers

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (this.MapImage == null) return;

            var mousePos = e.GetPosition(this.MapImage);
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

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this.MapImage);
                this.Cursor = new Cursor(StandardCursorType.Hand);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetPosition(this.MapImage);
            var delta = _panStartPoint - currentPoint;
            _panStartPoint = currentPoint;

            _viewOffset.X += (int)delta.X;
            _viewOffset.Y += (int)delta.Y;

            _pendingMapUpdate = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _isPanning = false;
                this.Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }

        #endregion

        #region Rendering Logic

        /// <summary>
        /// Creates or resizes the WriteableBitmap used as the target for rendering.
        /// </summary>
        private void UpdateBitmapSource(PixelSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) return;

            // Dispose the old bitmap if it exists and the size is different
            if (_writeableBitmap != null && _writeableBitmap.PixelSize != size)
            {
                _writeableBitmap.Dispose();
                _writeableBitmap = null;
            }

            if (_writeableBitmap == null)
            {
                _writeableBitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
                this.MapImage.Source = _writeableBitmap;
            }
        }

        /// <summary>
        /// Queues a render operation, ensuring it runs on a background thread without blocking the UI.
        /// This version is fully asynchronous to prevent deadlocks.
        /// </summary>
        private void QueueRender()
        {
            lock (_renderLock)
            {
                if (_renderInProgress) return;
                _renderInProgress = true;
            }

            // Fire and forget the async task.
            _ = Task.Run(async () =>
            {
                SKBitmap skBitmap = null;
                try
                {
                    // This runs on a background thread.
                    skBitmap = RenderMapOnWorkerThread();

                    if (skBitmap != null)
                    {
                        // Dispatch the pixel copy to the UI thread without blocking the background thread.
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_writeableBitmap != null && _writeableBitmap.PixelSize.Width == skBitmap.Width && _writeableBitmap.PixelSize.Height == skBitmap.Height)
                            {
                                using (var frameBuffer = _writeableBitmap.Lock())
                                {
                                    var size = frameBuffer.RowBytes * frameBuffer.Size.Height;
                                    unsafe
                                    {
                                        Buffer.MemoryCopy(skBitmap.GetPixels().ToPointer(), frameBuffer.Address.ToPointer(), size, size);
                                    }
                                }
                                // Explicitly tell the UI to redraw the updated area.
                                MapImage.InvalidateVisual();
                            }
                        });
                    }
                }
                finally
                {
                    skBitmap?.Dispose(); // Dispose the Skia bitmap after we're done with it.
                    lock (_renderLock)
                    {
                        _renderInProgress = false;
                    }
                }
            });
        }

        private SKBitmap RenderMapOnWorkerThread()
        {
            var effectiveSize = GetEffectiveRenderSize();
            if (!_isInitialized || effectiveSize.Width < 1 || effectiveSize.Height < 1 || _mapManager == null)
            {
                return null;
            }

            ClampViewOffset();

            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + (int)effectiveSize.Width,
                _viewOffset.Y + (int)effectiveSize.Height
            );

            Debug.WriteLine($"RenderMap: ZoomLevel={_currentZoomLevel}, ViewArea={viewArea}, Offset={_viewOffset}");

            return _mapManager.AssembleView(
                _currentZoomLevel,
                viewArea,
                () => Dispatcher.UIThread.Post(QueueRender, DispatcherPriority.Background)
            );
        }

        private Size GetEffectiveRenderSize()
        {
            if (this.MapImage?.Bounds.Width > 1 && this.MapImage?.Bounds.Height > 1)
            {
                return this.MapImage.Bounds.Size;
            }
            return this.ClientSize;
        }

        private void ClampViewOffset()
        {
            if (_mapManager == null) return;
            var effectiveSize = GetEffectiveRenderSize();
            if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;
            var mapSize = _mapManager.GetMapSize(_currentZoomLevel);

            _viewOffset.X = mapSize.Width < effectiveSize.Width
                ? (mapSize.Width - (int)effectiveSize.Width) / 2
                : Math.Clamp(_viewOffset.X, 0, mapSize.Width - (int)effectiveSize.Width);

            _viewOffset.Y = mapSize.Height < effectiveSize.Height
                ? (mapSize.Height - (int)effectiveSize.Height) / 2
                : Math.Clamp(_viewOffset.Y, 0, mapSize.Height - (int)effectiveSize.Height);
        }

        #endregion
    }
}
