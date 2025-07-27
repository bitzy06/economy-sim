using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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

        // --- Ported from WinForms Logic ---
        private int _currentZoomLevel = 1; // Integer-based zoom level
        private SKPointI _viewOffset = SKPointI.Empty; // Integer-based view offset
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _isInitialized = false;

        // --- Timer for smooth panning ---
        private DispatcherTimer _mapUpdateTimer;
        private bool _pendingMapUpdate = false;

        // --- Thread-safe rendering flags ---
        private readonly object _renderLock = new object();
        private bool _renderInProgress = false;


        public GameView()
        {
            InitializeComponent();

            // The baseWidth and baseHeight should correspond to the full, unscaled
            // dimensions of your map source data in cells.
            _mapManager = new MultiResolutionMapManager(baseWidth: 4096, baseHeight: 2048);

            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            // Initialize the timer for handling map updates during panning
            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30) // Render at ~30fps during pan
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();
        }

        private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("OnWindowLoaded called");

            if (this.MapImage != null)
            {
                // Attach event handlers
                this.MapImage.PointerPressed += OnPointerPressed;
                this.MapImage.PointerMoved += OnPointerMoved;
                this.MapImage.PointerReleased += OnPointerReleased;
                this.MapImage.PointerWheelChanged += OnPointerWheelChanged;

                _isInitialized = true;
                Debug.WriteLine("Window loaded, triggering initial render");

                QueueRender();
            }
            else
            {
                Debug.WriteLine("ERROR: MapImage is null after window loaded!");
            }
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (_isInitialized && this.MapImage != null)
            {
                Debug.WriteLine($"Size changed to {e.NewSize}, re-rendering");
                QueueRender();
            }
        }

        /// <summary>
        /// Timer tick handler to render the map if an update is pending (e.g., during a pan).
        /// </summary>
        private void MapUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_pendingMapUpdate)
            {
                _pendingMapUpdate = false;
                QueueRender();
            }
        }

        #region Pointer Event Handlers

        /// <summary>
        /// Handles zooming the map using the mouse wheel, ported from WinForms logic.
        /// </summary>
        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (this.MapImage == null) return;

            var mousePos = e.GetPosition(this.MapImage);
            int oldZoomLevel = _currentZoomLevel;

            // Update zoom level
            _currentZoomLevel = Math.Clamp(_currentZoomLevel + Math.Sign(e.Delta.Y), 1, MultiResolutionMapManager.PixelsPerCellLevels.Length);
            if (_currentZoomLevel == oldZoomLevel) return;

            // Get cell sizes for old and new zoom levels
            int oldCellSize = GetCellSizeForZoom(oldZoomLevel);
            int newCellSize = GetCellSizeForZoom(_currentZoomLevel);

            // Calculate new view offset to keep mouse position stationary ("zoom to cursor")
            int newOffsetX = (int)Math.Round((_viewOffset.X + mousePos.X) * (double)newCellSize / oldCellSize) - (int)mousePos.X;
            int newOffsetY = (int)Math.Round((_viewOffset.Y + mousePos.Y) * (double)newCellSize / oldCellSize) - (int)mousePos.Y;

            _viewOffset = new SKPointI(newOffsetX, newOffsetY);

            QueueRender();
            e.Handled = true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (this.MapImage == null) return;

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this.MapImage);
                this.Cursor = new Cursor(StandardCursorType.Hand);
            }
        }

        /// <summary>
        /// Updates the map's view offset while panning but defers rendering to the timer.
        /// </summary>
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (this.MapImage == null || !_isPanning) return;

            var currentPoint = e.GetPosition(this.MapImage);
            var delta = _panStartPoint - currentPoint;
            _panStartPoint = currentPoint;

            // Update view offset based on mouse movement
            _viewOffset.X += (int)delta.X;
            _viewOffset.Y += (int)delta.Y;

            // Set a flag to render on the next timer tick instead of immediately
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
        /// Queues a render operation, ensuring it runs on a background thread without blocking the UI.
        /// </summary>
        private void QueueRender()
        {
            lock (_renderLock)
            {
                if (_renderInProgress) return; // Don't start a new render if one is already running
                _renderInProgress = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    var bitmap = RenderMap();
                    if (bitmap != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (this.MapImage != null)
                            {
                                (this.MapImage.Source as IDisposable)?.Dispose();
                                this.MapImage.Source = bitmap;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in background render task: {ex}");
                }
                finally
                {
                    lock (_renderLock)
                    {
                        _renderInProgress = false;
                    }
                }
            });
        }

        /// <summary>
        /// Assembles and renders the current map view. This method is designed to be called from a background thread.
        /// </summary>
        private Bitmap? RenderMap()
        {
            var effectiveSize = GetEffectiveRenderSize();

            if (!_isInitialized || effectiveSize.Width < 1 || effectiveSize.Height < 1 || _mapManager == null)
            {
                Debug.WriteLine($"RenderMap: Not ready - Init:{_isInitialized}, EffectiveSize: {effectiveSize}");
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

            // The callback will trigger a re-render when a new tile is loaded asynchronously.
            using SKBitmap skBitmap = _mapManager.AssembleView(
                _currentZoomLevel, // Use integer zoom level
                viewArea,
                () => Dispatcher.UIThread.Post(QueueRender, DispatcherPriority.Background)
            );

            if (skBitmap == null || skBitmap.Width <= 1 || skBitmap.Height <= 1)
            {
                Debug.WriteLine($"RenderMap: AssembleView returned null or tiny bitmap.");
                return null;
            }

            // Convert the SkiaSharp bitmap to an Avalonia bitmap
            using var skImage = SKImage.FromBitmap(skBitmap);
            using var stream = new MemoryStream();
            skImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
            stream.Position = 0;

            return new Bitmap(stream);
        }

        /// <summary>
        /// Gets the rendering size of the map control.
        /// </summary>
        private Size GetEffectiveRenderSize()
        {
            if (this.MapImage?.Bounds.Width > 1 && this.MapImage?.Bounds.Height > 1)
            {
                return this.MapImage.Bounds.Size;
            }
            return this.ClientSize; // Fallback to window client size
        }

        /// <summary>
        /// Ensures the view offset does not go beyond the map's boundaries.
        /// </summary>
        private void ClampViewOffset()
        {
            if (_mapManager == null) return;

            var effectiveSize = GetEffectiveRenderSize();
            if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;

            var mapSize = _mapManager.GetMapSize(_currentZoomLevel);

            // Clamp X offset
            if (mapSize.Width < effectiveSize.Width)
                _viewOffset.X = (mapSize.Width - (int)effectiveSize.Width) / 2; // Center
            else
                _viewOffset.X = Math.Clamp(_viewOffset.X, 0, mapSize.Width - (int)effectiveSize.Width);

            // Clamp Y offset
            if (mapSize.Height < effectiveSize.Height)
                _viewOffset.Y = (mapSize.Height - (int)effectiveSize.Height) / 2; // Center
            else
                _viewOffset.Y = Math.Clamp(_viewOffset.Y, 0, mapSize.Height - (int)effectiveSize.Height);
        }

        /// <summary>
        /// Gets the cell size for a given integer zoom level.
        /// </summary>
        private int GetCellSizeForZoom(int zoomLevel)
        {
            // `zoomLevel` is 1-based, array is 0-based
            int index = Math.Clamp(zoomLevel - 1, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1);
            return MultiResolutionMapManager.PixelsPerCellLevels[index];
        }

        #endregion
    }
}
