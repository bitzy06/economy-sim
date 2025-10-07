using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using Economy_sim;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.ObjectModel;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly HybridMapManager _mapManager;

        // --- Optimized Rendering Fields ---
        private WriteableBitmap? _writeableBitmap; // Use a WriteableBitmap for high-performance updates.
        private WriteableBitmap? _backBufferBitmap; // Back buffer for double buffering
        private SKBitmap? _currentFrameBuffer; // Current frame in SkBitmap format
        private SKBitmap? _nextFrameBuffer; // Next frame being rendered
        private int _currentZoomLevel = 1; // Start at the lowest zoom level so user doesn't have to zoom out
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _hasPanned = false; // Track if user actually moved during pan
        private const double PAN_THRESHOLD = 5.0; // Minimum distance to consider as panning
        private bool _isInitialized = false;

        private readonly DispatcherTimer _mapUpdateTimer;
        private DispatcherTimer? _initialRenderTimer; // Timer to poll for initial size.
        private DispatcherTimer? _continuousRenderTimer; // Timer for continuous refreshing
        private DispatcherTimer? _hudTimer;
        private bool _pendingMapUpdate = false;
        private readonly object _renderLock = new object();
        private readonly object _bufferSwapLock = new object();
        private bool _renderInProgress = false;
        private bool _frameReady = false;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(100); // 10 FPS continuous refresh
        public Point mousepoint;

        private bool _isCullingStates;
        private CancellationTokenSource? _cullStatesCts;

        // Track baseline base size to compute normalization if env changes
        private readonly int _baselineWidth = 4096 * 4;
        private readonly int _baselineHeight = 2048 * 4;

        // === NEW: Real economy data tracking ===
        private List<Country> _allCountries = new();
        private List<Corporation> _allCorporations = new();
        private Country? _playerCountry;
        private DispatcherTimer? _economyUpdateTimer;
        private bool _economyInitialized = false;
        private bool _usedMapEconomy = false; // track if map based economy built
        private TradeRouteManager? _tradeRouteManager;
        private EnhancedTradeManager? _enhancedTradeManager;
        private readonly TradeMenuViewModel _tradeMenuViewModel;

        private static readonly IReadOnlyDictionary<string, string> _needRemapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Food"] = "Bread",
            ["Housing"] = "Furniture",
            ["Clothing"] = "Cloth",
            ["Luxury"] = "Luxury Clothes",
            ["Education"] = "Books"
        };

        public GameView()
        {
            InitializeComponent();

            _tradeMenuViewModel = new TradeMenuViewModel(HandleCreateTradeAsync);
            if (TradeMenuOverlay != null)
            {
                TradeMenuOverlay.DataContext = _tradeMenuViewModel;
            }
            if (SideTradePanel != null)
            {
                SideTradePanel.DataContext = _tradeMenuViewModel;
            }

            int baseW = ParseEnvOrDefault("ES_BASE_WIDTH", _baselineWidth);
            int baseH = ParseEnvOrDefault("ES_BASE_HEIGHT", _baselineHeight);
            int defaultPolW = checked(baseW * 2);
            int defaultPolH = checked(baseH * 2);
            int polW = ParseEnvOrDefault("ES_POL_BASE_WIDTH", defaultPolW);
            int polH = ParseEnvOrDefault("ES_POL_BASE_HEIGHT", defaultPolH);
            _mapManager = new HybridMapManager(baseWidth: baseW, baseHeight: baseH, politicalBaseWidth: polW, politicalBaseHeight: polH);

            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // Render at ~60fps during pan
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();

            // Create continuous render timer
            _continuousRenderTimer = new DispatcherTimer
            {
                Interval = _refreshInterval
            };
            _continuousRenderTimer.Tick += ContinuousRenderTimer_Tick;

            // Initialize HUD after component initialization
            InitializeHUD();

            // Subscribe to map manager events
            _mapManager.ViewTypeChanged += OnMapViewTypeChanged;
            UpdateMapViewButtons();

            // Maintain perceived zoom if base sizes differ from baseline
            NormalizeInitialViewOffset(baseW, baseH);

            // Run basic integration test for political borders (commented out for production)
            // Economy_sim.Testing.PoliticalBorderIntegrationTest.RunBasicTests();

            RefreshTradeViewModel();
        }

        private void NormalizeInitialViewOffset(int baseW, int baseH)
        {
            // If base size differs from baseline, scale the view offset so FOV stays roughly the same
            if (baseW != _baselineWidth || baseH != _baselineHeight)
            {
                double sx = (double)baseW / Math.Max(1, _baselineWidth);
                double sy = (double)baseH / Math.Max(1, _baselineHeight);
                _viewOffset = new SKPointI((int)Math.Round(_viewOffset.X * sx), (int)Math.Round(_viewOffset.Y * sy));
            }
        }

        private static int ParseEnvOrDefault(string key, int def)
        {
            var s = Environment.GetEnvironmentVariable(key);
            return int.TryParse(s, out var v) && v > 0 ? v : def;
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

                // Also react to layout changes so buffers resize when side menu opens/closes
                this.MapImage.PropertyChanged += MapImage_PropertyChanged;

                // Flag that the view is ready
                _isInitialized = true;

                // Use a timer to poll for a valid size, as Loaded/SizeChanged can be unreliable at startup.
                _initialRenderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, InitialRenderTimer_Tick);
                _initialRenderTimer.Start();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                this.Loaded -= OnWindowLoaded;
                this.SizeChanged -= OnSizeChanged;

                CancelStateCulling();

                if (_mapUpdateTimer != null)
                {
                    _mapUpdateTimer.Stop();
                    _mapUpdateTimer.Tick -= MapUpdateTimer_Tick;
                }

                if (_continuousRenderTimer != null)
                {
                    _continuousRenderTimer.Stop();
                    _continuousRenderTimer.Tick -= ContinuousRenderTimer_Tick;
                    _continuousRenderTimer = null;
                }

                if (_initialRenderTimer != null)
                {
                    _initialRenderTimer.Stop();
                    _initialRenderTimer.Tick -= InitialRenderTimer_Tick;
                    _initialRenderTimer = null;
                }

                if (_hudTimer != null)
                {
                    _hudTimer.Stop();
                    _hudTimer.Tick -= UpdateHUDDisplay;
                    _hudTimer = null;
                }

                DetachMapImageHandlers();
                DisposeRenderResources();

                _mapManager.ViewTypeChanged -= OnMapViewTypeChanged;
                _mapManager.Dispose();
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        private void MapImage_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
            {
                OnMapBoundsChanged();
            }
        }

        private void CancelStateCulling()
        {
            var cts = Interlocked.Exchange(ref _cullStatesCts, null);
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore; the CTS was already disposed after completion.
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        private void DetachMapImageHandlers()
        {
            if (this.MapImage != null)
            {
                this.MapImage.PointerPressed -= OnPointerPressed;
                this.MapImage.PointerMoved -= OnPointerMoved;
                this.MapImage.PointerReleased -= OnPointerReleased;
                this.MapImage.PointerWheelChanged -= OnPointerWheelChanged;
                this.MapImage.PropertyChanged -= MapImage_PropertyChanged;
            }
        }

        private void DisposeRenderResources()
        {
            lock (_bufferSwapLock)
            {
                _writeableBitmap?.Dispose();
                _writeableBitmap = null;

                _backBufferBitmap?.Dispose();
                _backBufferBitmap = null;
            }

            lock (_renderLock)
            {
                _currentFrameBuffer?.Dispose();
                _currentFrameBuffer = null;

                _nextFrameBuffer?.Dispose();
                _nextFrameBuffer = null;

                _frameReady = false;
                _renderInProgress = false;
            }

            if (this.MapImage != null)
            {
                this.MapImage.Source = null;
            }
        }

        private void OnMapBoundsChanged()
        {
            if (!_isInitialized || this.MapImage == null) return;
            var size = this.MapImage.Bounds.Size;
            if (size.Width <= 0 || size.Height <= 0) return;

            var pixelSize = PixelSize.FromSize(size, 1.0);
            if (_writeableBitmap == null || _writeableBitmap.PixelSize != pixelSize)
            {
                Debug.WriteLine($"MapImage bounds changed to {size}, recreating buffers.");
                UpdateBitmapSource(pixelSize);
                QueueRender(immediate: true);
            }
        }

        /// <summary>
        /// Recreate front/back buffers sized to the current effective render size (e.g., after UI layout changes)
        /// </summary>
        private void RecreateBuffersToCurrentSize()
        {
            var size = GetEffectiveRenderSize();
            if (size.Width > 0 && size.Height > 0)
            {
                UpdateBitmapSource(PixelSize.FromSize(size, 1.0));
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
                var pixelSize = PixelSize.FromSize(this.ClientSize, 1.0);
                UpdateBitmapSource(pixelSize);

                // Center the view to ensure both map types start at the same position
                CenterView();

                // Initial render
                QueueRender(immediate: true);

                // Start the continuous refresh timer
                _continuousRenderTimer.Start();
                Debug.WriteLine($"Started continuous refresh timer at {_refreshInterval.TotalMilliseconds}ms interval");
            }
        }

        /// <summary>
        /// This timer continuously refreshes the map at a fixed interval
        /// </summary>
        private void ContinuousRenderTimer_Tick(object? sender, EventArgs e)
        {
            // Queue a new render if one is not already in progress
            if (!_renderInProgress && _isInitialized)
            {
                // Debug.WriteLine("Continuous refresh tick - queueing new render");
                QueueRender(immediate: false);
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
                Debug.WriteLine($"Window size changed to {e.NewSize}, updating bitmap and re-rendering.");
                UpdateBitmapSource(PixelSize.FromSize(e.NewSize, 1.0));
                QueueRender(immediate: true);
            }
        }

        private void MapUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_pendingMapUpdate)
            {
                _pendingMapUpdate = false;
                QueueRender(immediate: true);
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

            QueueRender(immediate: true);
            e.Handled = true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var currentPoint = e.GetCurrentPoint(this);

            if (currentPoint.Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _hasPanned = false;
                _panStartPoint = e.GetPosition(this.MapImage);
                this.Cursor = new Cursor(StandardCursorType.Hand);
                mousepoint = _panStartPoint; // Store initial mouse position for panning
                Debug.WriteLine($"Pointer pressed at {_panStartPoint}, starting pan.");
            }
            else if (currentPoint.Properties.IsRightButtonPressed)
            {
                // Right-click for country detection (existing behavior)
                var mousePos = e.GetPosition(this.MapImage);
                DetectCountryAtPosition((int)mousePos.X, (int)mousePos.Y);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetPosition(this.MapImage);
            var delta = _panStartPoint - currentPoint;

            // Check if movement is significant enough to be considered panning
            var distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            if (distance > PAN_THRESHOLD)
            {
                _hasPanned = true;

                _panStartPoint = currentPoint;

                _viewOffset.X += (int)delta.X;
                _viewOffset.Y += (int)delta.Y;

                _pendingMapUpdate = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _isPanning = false;
                this.Cursor = new Cursor(StandardCursorType.Arrow);

                // If user didn't pan (just clicked), select country at click position
                if (!_hasPanned && _mapManager.CurrentViewType == MapViewType.Political)
                {
                    var mousePos = e.GetPosition(this.MapImage);
                    HandleSelectionAtPosition((int)mousePos.X, (int)mousePos.Y);
                }

                _hasPanned = false;
            }
        }

        #endregion

        #region Country Detection

        /// <summary>
        /// Detects which country is at the specified screen position
        /// </summary>
        private void DetectCountryAtPosition(int screenX, int screenY)
        {
            try
            {
                // Validate inputs
                if (screenX < 0 || screenY < 0 || _mapManager == null)
                {
                    Debug.WriteLine($"[COUNTRY DETECTION] Invalid input: screenX={screenX}, screenY={screenY}, mapManager={_mapManager != null}");
                    return;
                }

                // Only detect countries when in political view mode
                if (_mapManager.CurrentViewType != MapViewType.Political)
                {
                    Debug.WriteLine($"[COUNTRY DETECTION] Country detection only available in political view mode (current: {_mapManager.CurrentViewType})");
                    ShowCountryDetectionFeedback(null, screenX, screenY, "Switch to Political View to detect countries");
                    return;
                }

                var country = _mapManager.GetCountryAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);

                if (country != null)
                {
                    // Show country information
                    string message = $"Country: {country.CountryName} ({country.CountryCode})";
                    Debug.WriteLine($"[COUNTRY DETECTED] {message}");

                    // You could add visual feedback here, such as:
                    // - Highlighting the country border
                    // - Showing a tooltip
                    // - Opening a country information panel
                    ShowCountryDetectionFeedback(country, screenX, screenY);
                }
                else
                {
                    Debug.WriteLine($"[COUNTRY DETECTED] No country found at position ({screenX}, {screenY})");
                    // Could show "Ocean" or "No country" message
                    ShowCountryDetectionFeedback(null, screenX, screenY);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[COUNTRY DETECTION ERROR] {ex.Message}");
                Debug.WriteLine($"[COUNTRY DETECTION ERROR] Stack trace: {ex.StackTrace}");
                ShowCountryDetectionFeedback(null, screenX, screenY, "Error detecting country");
            }
        }

        /// <summary>
        /// Shows visual feedback for country detection (placeholder implementation)
        /// </summary>
        private void ShowCountryDetectionFeedback(IndexedCountryFeature? country, int screenX, int screenY, string? customMessage = null)
        {
            // For now, just update a text display or create a simple notification
            // In a full implementation, this could:
            // 1. Highlight the country borders
            // 2. Show a tooltip near the mouse cursor
            // 3. Update a country information panel
            // 4. Play a sound effect

            string message = customMessage ?? (country != null
                ? $"Selected: {country.CountryName}"
                : "No country selected (ocean or outside map bounds)");

            // Update the HUD or show temporary feedback
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // You could update a label in the UI here
                    Debug.WriteLine($"[UI FEEDBACK] {message}");

                    // Example: Update window title to show selected country (temporary solution)
                    this.Title = country != null
                        ? $"Economy Sim - {country.CountryName} ({country.CountryCode})"
                        : customMessage != null
                        ? $"Economy Sim - {customMessage}"
                        : "Economy Sim";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UI FEEDBACK ERROR] {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Shows instructions for the country detection feature
        /// </summary>
        private void ShowCountryDetectionInstructions()
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Update the window title to show instructions
                    this.Title = "Economy Sim - Political View - LEFT-CLICK to select, RIGHT-CLICK to identify countries";

                    Debug.WriteLine("[INSTRUCTIONS] Country detection and selection are now active!");
                    Debug.WriteLine("[INSTRUCTIONS] LEFT-CLICK on any country to select it (shows white borders).");
                    Debug.WriteLine("[INSTRUCTIONS] RIGHT-CLICK on any country to see its name and code.");
                    Debug.WriteLine("[INSTRUCTIONS] The country name will appear in the window title.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[INSTRUCTION ERROR] {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Handles selection at the specified screen position (left-click)
        /// Selects countries or states based on current selection state
        /// </summary>
        private void HandleSelectionAtPosition(int screenX, int screenY)
        {
            try
            {
                // Validate inputs
                if (screenX < 0 || screenY < 0 || _mapManager == null)
                {
                    Debug.WriteLine($"[SELECTION] Invalid input: screenX={screenX}, screenY={screenY}, mapManager={_mapManager != null}");
                    return;
                }

                // Only handle selection when in political view mode
                if (_mapManager.CurrentViewType != MapViewType.Political)
                {
                    Debug.WriteLine($"[SELECTION] Selection only available in political view mode (current: {_mapManager.CurrentViewType})");
                    return;
                }

                var country = _mapManager.GetCountryAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);
                var currentSelectedCountry = _mapManager.SelectedCountry;

                if (country != null)
                {
                    // If no country is currently selected, or clicking on a different country
                    if (currentSelectedCountry == null || currentSelectedCountry.CountryCode != country.CountryCode)
                    {
                        // Select the new country (this will clear any state selection)
                        _mapManager.SelectCountry(country);

                        string message = $"Selected country: {country.CountryName} ({country.CountryCode})";
                        Debug.WriteLine($"[COUNTRY SELECTED] {message}");

                        // Update UI feedback
                        ShowCountrySelectionFeedback(country, screenX, screenY);
                    }
                    else
                    {
                        // Same country is selected, try to select a state within it
                        var state = _mapManager.GetStateAtPixel(screenX, screenY, _currentZoomLevel, _viewOffset);

                        if (state != null && state.CountryCode.Equals(country.CountryCode, StringComparison.OrdinalIgnoreCase))
                        {
                            _mapManager.SelectState(state);

                            string message = $"Selected state: {state.StateName} in {state.CountryName}";
                            Debug.WriteLine($"[STATE SELECTED] {message}");

                            // Update UI feedback for state selection
                            ShowStateSelectionFeedback(state, screenX, screenY);
                        }
                        else
                        {
                            // Clear state selection if clicking on a different state or no state found
                            _mapManager.ClearStateSelection();
                            Debug.WriteLine($"[STATE SELECTION] No valid state found at position ({screenX}, {screenY}) - cleared state selection");

                            // Show country feedback since country is still selected
                            ShowCountrySelectionFeedback(country, screenX, screenY);
                        }
                    }

                    // Force immediate re-render to display borders
                    QueueRender(immediate: true);
                }
                else
                {
                    // Clear all selections if clicking on water/empty area
                    _mapManager.ClearCountrySelection();
                    _mapManager.ClearStateSelection();
                    Debug.WriteLine($"[SELECTION] No country found at position ({screenX}, {screenY}) - cleared all selections");

                    ShowCountrySelectionFeedback(null, screenX, screenY);

                    // Force immediate re-render to remove any previous highlights
                    QueueRender(immediate: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SELECTION ERROR] {ex.Message}");
                Debug.WriteLine($"[SELECTION ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Shows visual feedback for country selection
        /// </summary>
        private void ShowCountrySelectionFeedback(IndexedCountryFeature? country, int screenX, int screenY)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (country != null)
                    {
                        this.Title = $"Economy Sim - SELECTED: {country.CountryName} ({country.CountryCode})";
                        Debug.WriteLine($"[SELECTION FEEDBACK] Country selected: {country.CountryName}");
                    }
                    else
                    {
                        this.Title = "Economy Sim - No country selected";
                        Debug.WriteLine("[SELECTION FEEDBACK] No country selected");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SELECTION FEEDBACK ERROR] {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Shows visual feedback for state selection
        /// </summary>
        private void ShowStateSelectionFeedback(StateBorderManager.StateFeature? state, int screenX, int screenY)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (state != null)
                    {
                        this.Title = $"Economy Sim - STATE: {state.StateName}, {state.CountryName} ({state.CountryCode})";
                        Debug.WriteLine($"[SELECTION FEEDBACK] State selected: {state.StateName} in {state.CountryName}");
                    }
                    else
                    {
                        this.Title = "Economy Sim - No state selected";
                        Debug.WriteLine("[SELECTION FEEDBACK] No state selected");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SELECTION FEEDBACK ERROR] {ex.Message}");
                }
            });
        }

        #endregion

        #region Rendering Logic

        /// <summary>
        /// Creates or resizes the WriteableBitmap used as the target for rendering.
        /// Sets up double-buffering for smooth rendering.
        /// </summary>
        private void UpdateBitmapSource(PixelSize size)
        {
            if (size.Width <= 0 || size.Height <= 0) return;

            lock (_bufferSwapLock)
            {
                // Dispose old bitmaps if they exist
                if (_writeableBitmap != null && _writeableBitmap.PixelSize != size)
                {
                    _writeableBitmap.Dispose();
                    _writeableBitmap = null;
                }

                if (_backBufferBitmap != null && _backBufferBitmap.PixelSize != size)
                {
                    _backBufferBitmap.Dispose();
                    _backBufferBitmap = null;
                }

                // Dispose old frame buffers
                _currentFrameBuffer?.Dispose();
                _nextFrameBuffer?.Dispose();

                // Create front buffer (displayed to user)
                if (_writeableBitmap == null)
                {
                    _writeableBitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
                    this.MapImage.Source = _writeableBitmap;
                }

                // Create back buffer (for rendering next frame)
                if (_backBufferBitmap == null)
                {
                    _backBufferBitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
                }

                // Create frame buffers for rendering
                _currentFrameBuffer = new SKBitmap(size.Width, size.Height);
                _nextFrameBuffer = new SKBitmap(size.Width, size.Height);

                // Initialize with a clear background
                _currentFrameBuffer.Erase(SKColors.LightGray);
                _nextFrameBuffer.Erase(SKColors.LightGray);

                Debug.WriteLine($"Created double-buffered bitmaps at size: {size.Width}x{size.Height}");
            }
        }

        /// <summary>
        /// Queues a render operation, ensuring it runs on a background thread without blocking the UI.
        /// Uses double-buffering to prevent flickering.
        /// </summary>
        private void QueueRender(bool immediate = false)
        {
            lock (_renderLock)
            {
                if (_renderInProgress)
                {
                    // If immediate mode and a render is already in progress, we need to ensure 
                    // another render happens after the current one completes
                    if (immediate)
                    {
                        _pendingMapUpdate = true;
                    }
                    return;
                }
                _renderInProgress = true;
            }

            // Get the effective render size on the UI thread before starting background task
            var effectiveSize = GetEffectiveRenderSize();

            // Fire and forget the async task.
            _ = Task.Run(async () =>
            {
                SKBitmap resultBitmap = null;
                try
                {
                    // This runs on a background thread.
                    resultBitmap = RenderMapOnWorkerThread(effectiveSize);

                    if (resultBitmap != null)
                    {
                        lock (_bufferSwapLock)
                        {
                            // Copy the result to the next frame buffer
                            if (_nextFrameBuffer != null && !_nextFrameBuffer.IsEmpty)
                            {
                                // Copy pixels from result to next frame buffer
                                resultBitmap.CopyTo(_nextFrameBuffer);
                                _frameReady = true;
                            }
                        }

                        // Dispatch the buffer swap to the UI thread
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            SwapBuffers();
                        }, immediate ? DispatcherPriority.Render : DispatcherPriority.Background);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during map rendering: {ex.Message}");
                }
                finally
                {
                    resultBitmap?.Dispose(); // Dispose the temporary Skia bitmap after we're done with it.

                    lock (_renderLock)
                    {
                        _renderInProgress = false;

                        // If there are pending updates and we're allowed to immediately render again
                        if (_pendingMapUpdate)
                        {
                            _pendingMapUpdate = false;
                            // Use the dispatcher to avoid potential stack overflow
                            Dispatcher.UIThread.Post(() => QueueRender(immediate));
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Swaps the front and back buffers to display the new frame
        /// </summary>
        private void SwapBuffers()
        {
            lock (_bufferSwapLock)
            {
                try
                {
                    if (!_frameReady || _nextFrameBuffer == null || _writeableBitmap == null)
                        return;

                    // Validate size compatibility to avoid overruns
                    var ps = _writeableBitmap.PixelSize;
                    if (_nextFrameBuffer.Width != ps.Width || _nextFrameBuffer.Height != ps.Height)
                    {
                        Debug.WriteLine($"SwapBuffers skipped due to size mismatch. NextFrame: {_nextFrameBuffer.Width}x{_nextFrameBuffer.Height}, Front: {ps.Width}x{ps.Height}");
                        _frameReady = false; // Drop this frame safely
                        return;
                    }

                    // Copy with stride awareness to avoid overruns
                    using (var frameBuffer = _writeableBitmap.Lock())
                    {
                        int dstRowBytes = frameBuffer.RowBytes;
                        int srcRowBytes = _nextFrameBuffer.RowBytes;
                        int rows = Math.Min(frameBuffer.Size.Height, _nextFrameBuffer.Height);
                        int copyBytesPerRow = Math.Min(dstRowBytes, srcRowBytes);

                        unsafe
                        {
                            byte* src = (byte*)_nextFrameBuffer.GetPixels().ToPointer();
                            byte* dst = (byte*)frameBuffer.Address.ToPointer();

                            for (int y = 0; y < rows; y++)
                            {
                                Buffer.MemoryCopy(src + (long)y * srcRowBytes,
                                                  dst + (long)y * dstRowBytes,
                                                  dstRowBytes,
                                                  copyBytesPerRow);
                            }
                        }
                    }

                    // Explicitly tell the UI to redraw the updated area
                    MapImage.InvalidateVisual();

                    // Reset the frame ready flag
                    _frameReady = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error swapping buffers: {ex}");
                    _frameReady = false; // Ensure we don't loop on a bad frame
                }
            }
        }

        private SKBitmap RenderMapOnWorkerThread(Size effectiveSize)
        {
            if (!_isInitialized || effectiveSize.Width < 1 || effectiveSize.Height < 1 || _mapManager == null)
            {
                return null;
            }

            ClampViewOffset(effectiveSize);

            var viewArea = new SKRectI(
                _viewOffset.X,
                _viewOffset.Y,
                _viewOffset.X + (int)effectiveSize.Width,
                _viewOffset.Y + (int)effectiveSize.Height
            );

            // Debug.WriteLine($"RenderMap: ZoomLevel={_currentZoomLevel}, ViewArea={viewArea}, Offset={_viewOffset}");

            return _mapManager.AssembleView(
                _currentZoomLevel,
                viewArea,
                () => Dispatcher.UIThread.Post(() => QueueRender(), DispatcherPriority.Background)
            );
        }

        private Size GetEffectiveRenderSize()
        {
            // First check if we have a proper MapContainer with valid bounds
            if (this.FindControl<Border>("MapContainer") is Border mapContainer &&
                mapContainer.Bounds.Width > 1 && mapContainer.Bounds.Height > 1)
            {
                Debug.WriteLine($"Using MapContainer bounds: {mapContainer.Bounds.Size}");
                return mapContainer.Bounds.Size;
            }

            // Fallback to MapImage bounds if available
            if (this.MapImage?.Bounds.Width > 1 && this.MapImage?.Bounds.Height > 1)
            {
                Debug.WriteLine($"Using MapImage bounds: {this.MapImage.Bounds.Size}");
                return this.MapImage.Bounds.Size;
            }

            // Calculate available space based on Grid column layout
            if (this.FindControl<Grid>("RootGrid") is Grid rootGrid &&
                rootGrid.ColumnDefinitions.Count > 1)
            {
                var sideMenuColumnWidth = rootGrid.ColumnDefinitions[1].Width.Value;
                var availableWidth = this.ClientSize.Width - sideMenuColumnWidth;
                var calculatedSize = new Size(availableWidth, this.ClientSize.Height);
                Debug.WriteLine($"Calculated size based on Grid layout: {calculatedSize}");
                return calculatedSize;
            }

            // Final fallback to ClientSize
            Debug.WriteLine($"Using ClientSize: {this.ClientSize}");
            return this.ClientSize;
        }

        private void ClampViewOffset(Size effectiveSize)
        {
            if (_mapManager == null) return;
            if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;
            var mapSize = _mapManager.GetMapSize(_currentZoomLevel);

            _viewOffset.X = mapSize.Width < effectiveSize.Width
                ? (mapSize.Width - (int)effectiveSize.Width) / 2
                : Math.Clamp(_viewOffset.X, 0, mapSize.Width - (int)effectiveSize.Width);

            _viewOffset.Y = mapSize.Height < effectiveSize.Height
                ? (mapSize.Height - (int)effectiveSize.Height) / 2
                : Math.Clamp(_viewOffset.Y, 0, mapSize.Height - (int)effectiveSize.Height);
        }

        private void ClampViewOffset()
        {
            var effectiveSize = GetEffectiveRenderSize();
            ClampViewOffset(effectiveSize);
        }

        #endregion

        #region HUD Management

        // Sample game state for HUD demonstration
        private PlayerRoleManager _playerRoleManager;
        private Country _currentCountry;

        // Unified political entity renderer
        private PoliticalEntityRenderer _politicalEntityRenderer;

        private void InitializeHUD()
        {
            if (GlobalMarket.Instance == null)
            {
                _ = new GlobalMarket();
            }

            // Initialize real economy state
            if (!_economyInitialized)
            {
                // Try map based generation first
                try
                {
                    if (InitializeEconomyFromMap())
                    {
                        _usedMapEconomy = true;
                        Debug.WriteLine("[Economy Init] Initialized economy from map data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Economy Init] Map-based initialization failed: {ex.Message}");
                }
                if (!_economyInitialized)
                {
                    InitializeEconomyData(); // fallback sample
                    Debug.WriteLine("[Economy Init] Fallback sample economy initialized.");
                }
            }

            // Initialize the unified political entity renderer
            var dataCache = _mapManager.GetPoliticalDataCache();
            _politicalEntityRenderer = new PoliticalEntityRenderer(dataCache);

            // Set up HUD update timer
            _hudTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Update every second
            };

            _hudTimer.Tick -= UpdateHUDDisplay;
            _hudTimer.Tick += UpdateHUDDisplay;
            if (!_hudTimer.IsEnabled)
            {
                _hudTimer.Start();
            }

            // Set up economy simulation timer (updates every 5 seconds)
            _economyUpdateTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _economyUpdateTimer.Tick -= OnEconomyUpdateTick;
            _economyUpdateTimer.Tick += OnEconomyUpdateTick;
            if (!_economyUpdateTimer.IsEnabled)
            {
                _economyUpdateTimer.Start();
            }

            // Initialize HUD button event handlers
            SetupHUDEventHandlers();

            // Initial HUD update
            UpdateHUDDisplay(null, null);
            UpdateEconomyDisplay();
        }

        private bool InitializeEconomyFromMap()
        {
            // Require political map data and city listings
            if (_mapManager == null) return false;
            var countryData = _mapManager.GetAllCountryData();
            if (countryData == null || countryData.Count == 0) return false;
            List<HybridMapManager.MapCityInfo> cities;
            try { cities = _mapManager.GetAllCities(includeUnassigned: false); } catch { return false; }
            if (cities == null || cities.Count == 0) return false;

            // Group cities by country code
            var citiesByCountry = cities.GroupBy(c => c.CountryCode.ToUpperInvariant()).ToList();
            if (citiesByCountry.Count == 0) return false;

            _allCountries = new List<Country>();
            foreach (var countryGroup in citiesByCountry)
            {
                // Resolve country display name
                string iso = countryGroup.Key;
                string displayName = countryData.FirstOrDefault(c => string.Equals(c.CountryCode, iso, StringComparison.OrdinalIgnoreCase))?.CountryName ?? iso;
                var country = new Country(displayName) { Name = displayName };

                // Progressive base budgets scaled by number of cities
                int totalCities = countryGroup.Count();
                country.Budget = 250000 + totalCities * 25000;
                country.NationalExpenses = Math.Max(50000, totalCities * 1500);

                // Basic tax policies
                if (!Market.GoodDefinitions.Any()) FactoryBlueprints.InitializeBlueprints();
                if (!country.FinancialSystem.TaxPolicies.Any())
                {
                    country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.IncomeTax, 0.15m));
                    country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.CorporateTax, 0.20m));
                    country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.ConsumptionTax, 0.05m));
                }

                // Group by state (if null group into a synthetic "Federal District")
                var statesByCode = countryGroup.GroupBy(c => string.IsNullOrWhiteSpace(c.StateCode) ? "__NO_STATE__" : c.StateCode!);
                foreach (var stateGroup in statesByCode)
                {
                    string stateCode = stateGroup.Key;
                    string stateName = stateCode == "__NO_STATE__" ? "Federal District" : stateGroup.First().StateName ?? stateCode;
                    var state = new State(stateName)
                    {
                        TaxRate = 0.04,
                        StateExpenses = 20000 + stateGroup.Count() * 500
                    };

                    foreach (var cityInfo in stateGroup)
                    {
                        // Derive starting population from POP_MAX with floor and scaling
                        int pop = cityInfo.Population;
                        if (pop <= 0) pop = 5000;
                        // Clamp to avoid extreme numbers for prototype
                        pop = Math.Clamp(pop, 5000, 15_000_000);

                        var city = new City(cityInfo.Name)
                        {
                            Population = pop,
                            Budget = 20000 + pop * 0.01,
                            TaxRate = 0.02,
                            CityExpenses = 5000 + pop * 0.002
                        };

                        // Adjust default pop classes to reflect actual population distribution
                        if (city.PopClasses.Any())
                        {
                            int totalOriginal = city.PopClasses.Sum(pc => pc.Size);
                            if (totalOriginal > 0)
                            {
                                double scale = pop / (double)totalOriginal;
                                foreach (var pc in city.PopClasses) pc.Size = (int)Math.Max(1, Math.Round(pc.Size * scale));
                            }
                        }

                        // Seed stockpile with core goods proportionally to population
                        SeedCityStockpile(city);
                        NormalizeCityPopulationNeeds(city);

                        state.Cities.Add(city);
                    }

                    state.Population = state.Cities.Sum(c => c.Population);
                    state.Budget = 50000 + state.Population * 0.02;
                    country.States.Add(state);
                }

                country.Population = country.States.Sum(s => s.Population);
                _allCountries.Add(country);
            }

            // Select the largest population country as player
            _playerCountry = _allCountries.OrderByDescending(c => c.Population).FirstOrDefault();
            if (_playerCountry == null) return false;
            _currentCountry = _playerCountry;
            _playerRoleManager = new PlayerRoleManager();
            _playerRoleManager.AssumeRolePrimeMinister(_playerCountry);

            // Minimal corporations seeded per large city cluster
            _allCorporations = new List<Corporation>();
            CreateInitialCorporationsFromCities();

            Market.AllCorporations.Clear();
            Market.AllCorporations.AddRange(_allCorporations);

            InitializeTradeSystemsForCurrentWorld();

            _economyInitialized = true;
            return true;
        }

        private void SeedCityStockpile(City city)
        {
            string[] coreGoods = { "Grain", "Coal", "Iron", "Bread", "Cloth" };
            foreach (var g in coreGoods)
            {
                if (!Market.GoodDefinitions.ContainsKey(g)) continue;
                int qty = g switch
                {
                    "Grain" => city.Population / 5,
                    "Coal" => city.Population / 20,
                    "Iron" => city.Population / 30,
                    "Bread" => city.Population / 8,
                    "Cloth" => city.Population / 15,
                    _ => city.Population / 50
                };
                city.Stockpile[g] = new Good(g, Market.GoodDefinitions[g].BasePrice, Market.GoodDefinitions[g].Category, qty);
            }
        }

        private void NormalizeCityPopulationNeeds(City city)
        {
            if (city?.PopClasses == null || !city.PopClasses.Any()) return;
            if (!Market.GoodDefinitions.Any()) return;

            foreach (var popClass in city.PopClasses)
            {
                var needsToReview = popClass.Needs.Keys.ToList();
                foreach (var needName in needsToReview)
                {
                    if (Market.GoodDefinitions.ContainsKey(needName)) continue;

                    if (_needRemapping.TryGetValue(needName, out var mappedNeed) && Market.GoodDefinitions.ContainsKey(mappedNeed))
                    {
                        double amount = popClass.Needs[needName];
                        popClass.Needs.Remove(needName);
                        if (popClass.Needs.TryGetValue(mappedNeed, out var existing))
                        {
                            popClass.Needs[mappedNeed] = existing + amount;
                        }
                        else
                        {
                            popClass.Needs[mappedNeed] = amount;
                        }
                        Debug.WriteLine($"[Economy Init] Remapped need '{needName}' to '{mappedNeed}' for {popClass.Name} in {city.Name}.");
                    }
                    else
                    {
                        popClass.Needs.Remove(needName);
                        Debug.WriteLine($"[Economy Init] Removed unsupported need '{needName}' for {popClass.Name} in {city.Name}.");
                    }
                }
            }
        }

        private void CreateInitialCorporationsFromCities()
        {
            if (_playerCountry == null) return;
            var allCities = _playerCountry.States.SelectMany(s => s.Cities).ToList();
            if (allCities.Count == 0) return;

            var rnd = new Random();
            int corpCounter = 1;
            foreach (var city in allCities.OrderByDescending(c => c.Population).Take(8))
            {
                var spec = (CorporationSpecialization)(corpCounter % 5);
                if (spec == 0) spec = CorporationSpecialization.Diversified;
                var corp = new Corporation($"{city.Name} Holdings", spec)
                {
                    Budget = 250000 + city.Population * 0.05
                };

                // Pick a blueprint biased by specialization
                var blueprint = FactoryBlueprints.GetBlueprintBySpecialization(corp.Specialization, GoodCategory.RawMaterial, rnd) ?? FactoryBlueprints.AllBlueprints.First();
                var factory = new Factory($"{blueprint.FactoryTypeName} #{corpCounter}", productionCapacity: Math.Clamp(city.Population / 200_000, 1, 10));
                foreach (var input in blueprint.InputGoods)
                    factory.InputGoods.Add(new Good(input.Name, input.BasePrice, input.Category, input.Quantity));
                factory.OutputGoods.Add(new Good(blueprint.OutputGood.Name, blueprint.OutputGood.BasePrice, blueprint.OutputGood.Category, blueprint.OutputGood.Quantity));
                foreach (var kvp in blueprint.DefaultJobSlotDistribution)
                {
                    factory.JobSlots[kvp.Key] = Math.Max(1, (int)Math.Round(kvp.Value * factory.ProductionCapacity * 10));
                }
                factory.OwnerCorporation = corp;
                corp.AddFactory(factory);
                city.Factories.Add(factory);
                _allCorporations.Add(corp);
                corpCounter++;
            }
        }

        private void InitializeTradeSystemsForCurrentWorld()
        {
            if (_allCountries == null || _allCountries.Count == 0)
            {
                return;
            }

            _tradeRouteManager ??= new TradeRouteManager();
            _enhancedTradeManager ??= new EnhancedTradeManager(_allCountries);
            RefreshTradeViewModel();
        }

        private void RefreshTradeViewModel()
        {
            var focusCountry = _currentCountry?.Name ?? _playerCountry?.Name ?? string.Empty;
            _tradeMenuViewModel.Refresh(GlobalMarket.Instance, _tradeRouteManager, _enhancedTradeManager, focusCountry);
        }

        private async Task HandleCreateTradeAsync(bool isExport)
        {
            InitializeTradeSystemsForCurrentWorld();

            if (_enhancedTradeManager == null)
            {
                _enhancedTradeManager = new EnhancedTradeManager(_allCountries);
            }

            var countryNames = (_allCountries != null && _allCountries.Count > 0)
                ? _allCountries.Select(c => c.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();

            var focusCountry = _currentCountry?.Name ?? _playerCountry?.Name;
            if (!string.IsNullOrWhiteSpace(focusCountry) && !countryNames.Any(n => string.Equals(n, focusCountry, StringComparison.OrdinalIgnoreCase)))
            {
                countryNames.Add(focusCountry!);
            }

            if (countryNames.Count == 0)
            {
                countryNames.Add("Player Nation");
            }

            var goodsNames = Market.GoodDefinitions.Any()
                ? Market.GoodDefinitions.Keys.ToList()
                : new List<string> { "Generic Goods" };

            string? partner = countryNames.FirstOrDefault(n => !string.Equals(n, focusCountry, StringComparison.OrdinalIgnoreCase));
            string? defaultFrom = isExport ? focusCountry ?? partner : partner ?? focusCountry;
            string? defaultTo = isExport ? partner ?? focusCountry : focusCountry ?? partner;

            var proposalWindow = new TradeProposalWindow();
            proposalWindow.Configure(isExport, countryNames, goodsNames, defaultFrom, defaultTo);
            var result = await proposalWindow.ShowDialog<TradeDealParameters?>(this);

            if (result == null)
            {
                return;
            }

            var agreement = _enhancedTradeManager!.CreateEnhancedTradeAgreement(
                result.FromCountry,
                result.ToCountry,
                result.Resource,
                result.Quantity,
                result.Price,
                result.Duration,
                result.TariffType,
                result.TariffRate);

            agreement.Status = TradeStatus.Active;
            if (!_enhancedTradeManager.AllTradeAgreements.Contains(agreement))
            {
                _enhancedTradeManager.AllTradeAgreements.Add(agreement);
            }

            RefreshTradeViewModel();
        }
        private void InitializeEconomyData()
        {
            if (_economyInitialized) return;

            Debug.WriteLine("[Economy Init] Initializing economy system...");

            // Initialize Market and goods definitions
            if (!Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints();
                Debug.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods and {FactoryBlueprints.AllBlueprints.Count} factory blueprints");
            }

            // Create the main country (player's country)
            _currentCountry = new Country("United States");
            _currentCountry.Budget = 5000000; // $5M starting budget
            _currentCountry.NationalExpenses = 100000; // $100k expenses per turn
            _currentCountry.Population = 10000000;

            // Set up initial tax policies
            var incomeTax = new TaxPolicy(TaxType.IncomeTax, 0.15m, TaxProgressivity.Progressive);
            incomeTax.ProgressiveBrackets[20000m] = 0.10m;  // First $20k taxed at 10%
            incomeTax.ProgressiveBrackets[50000m] = 0.15m;  // $20k-50k taxed at 15%
            incomeTax.ProgressiveBrackets[100000m] = 0.25m; // $50k-100k taxed at 25%
            _currentCountry.FinancialSystem.AddTaxPolicy(incomeTax);

            _currentCountry.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.CorporateTax, 0.21m));
            _currentCountry.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.ConsumptionTax, 0.08m));

            // Add some states with cities
            var california = new State("California");
            california.Budget = 100000;
            california.TaxRate = 0.05;
            california.StateExpenses = 10000;

            var losAngeles = new City("Los Angeles");
            losAngeles.Budget = 50000;
            losAngeles.Population = 4000000;
            losAngeles.TaxRate = 0.02;
            losAngeles.CityExpenses = 5000;

            losAngeles.PopClasses.Clear();

            // Add some population classes with needs
            var laborers = new PopClass("Laborers", 1000000, 15.0);
            laborers.Needs["Bread"] = 2.0;   // 2 units per 1000 people
            laborers.Needs["Furniture"] = 1.0;
            laborers.Needs["Cloth"] = 1.0;   // 1 unit per 1000 people

            var craftsmen = new PopClass("Craftsmen", 500000, 25.0);
            craftsmen.Needs["Bread"] = 2.0;
            craftsmen.Needs["Cloth"] = 1.5;
            craftsmen.Needs["Furniture"] = 0.5;
            craftsmen.Needs["Luxury Clothes"] = 0.2;

            var engineers = new PopClass("Engineers", 200000, 50.0);
            engineers.Needs["Bread"] = 2.0;
            engineers.Needs["Cloth"] = 2.0;
            engineers.Needs["Furniture"] = 1.0;
            engineers.Needs["Books"] = 1.0;
            engineers.Needs["Luxury Clothes"] = 0.5;

            losAngeles.PopClasses.Add(laborers);
            losAngeles.PopClasses.Add(craftsmen);
            losAngeles.PopClasses.Add(engineers);

            NormalizeCityPopulationNeeds(losAngeles);

            california.Cities.Add(losAngeles);

            // Add San Francisco
            var sanFrancisco = new City("San Francisco");
            sanFrancisco.Budget = 40000;
            sanFrancisco.Population = 900000;
            sanFrancisco.TaxRate = 0.02;
            sanFrancisco.CityExpenses = 4000;

            sanFrancisco.PopClasses.Clear();

            var sfLaborers = new PopClass("Laborers", 300000, 18.0);
            sfLaborers.Needs["Bread"] = 2.0;
            sfLaborers.Needs["Cloth"] = 1.0;
            sfLaborers.Needs["Furniture"] = 1.0;

            var sfCraftsmen = new PopClass("Craftsmen", 200000, 28.0);
            sfCraftsmen.Needs["Bread"] = 2.0;
            sfCraftsmen.Needs["Cloth"] = 1.5;
            sfCraftsmen.Needs["Furniture"] = 0.5;
            sfCraftsmen.Needs["Luxury Clothes"] = 0.2;

            sanFrancisco.PopClasses.Add(sfLaborers);
            sanFrancisco.PopClasses.Add(sfCraftsmen);

            NormalizeCityPopulationNeeds(sanFrancisco);

            california.Cities.Add(sanFrancisco);
            _currentCountry.States.Add(california);

            // Add Texas
            var texas = new State("Texas");
            texas.Budget = 80000;
            texas.TaxRate = 0.04;
            texas.StateExpenses = 8000;

            var houston = new City("Houston");
            houston.Budget = 45000;
            houston.Population = 2300000;
            houston.TaxRate = 0.02;
            houston.CityExpenses = 4500;

            houston.PopClasses.Clear();

            var houstonLaborers = new PopClass("Laborers", 800000, 16.0);
            houstonLaborers.Needs["Bread"] = 2.0;
            houstonLaborers.Needs["Cloth"] = 1.0;
            houstonLaborers.Needs["Furniture"] = 1.0;

            houston.PopClasses.Add(houstonLaborers);
            NormalizeCityPopulationNeeds(houston);
            texas.Cities.Add(houston);

            var dallas = new City("Dallas");
            dallas.Budget = 35000;
            dallas.Population = 1300000;
            dallas.TaxRate = 0.02;
            dallas.CityExpenses = 3500;

            dallas.PopClasses.Clear();

            var dallasLaborers = new PopClass("Laborers", 500000, 17.0);
            dallasLaborers.Needs["Bread"] = 2.0;
            dallasLaborers.Needs["Cloth"] = 1.0;
            dallasLaborers.Needs["Furniture"] = 1.0;

            dallas.PopClasses.Add(dallasLaborers);
            NormalizeCityPopulationNeeds(dallas);
            texas.Cities.Add(dallas);

            _currentCountry.States.Add(texas);

            // Create some corporations and factories
            _allCorporations = new List<Corporation>();

            var steelCorp = new Corporation("US Steel Corporation", CorporationSpecialization.HeavyIndustry);
            steelCorp.Budget = 500000;

            // Create a steel mill in LA
            var steelMill = new Factory("US Steel Mill #1", 5);
            steelMill.InputGoods.Add(new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 2));
            steelMill.InputGoods.Add(new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1));
            steelMill.OutputGoods.Add(new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1));
            steelMill.OwnerCorporation = steelCorp;
            steelMill.JobSlots["Laborers"] = 15;
            steelMill.JobSlots["Craftsmen"] = 8;
            steelMill.JobSlots["Engineers"] = 2;

            losAngeles.Factories.Add(steelMill);
            steelCorp.AddFactory(steelMill);
            _allCorporations.Add(steelCorp);

            var foodCorp = new Corporation("American Food Co", CorporationSpecialization.Agriculture);
            foodCorp.Budget = 200000;

            // Create a bakery in Houston
            var bakery = new Factory("American Bakery #1", 8);
            bakery.InputGoods.Add(new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 2));
            bakery.OutputGoods.Add(new Good("Bread", Market.GoodDefinitions["Bread"].BasePrice, GoodCategory.ProcessedFood, 3));
            bakery.OwnerCorporation = foodCorp;
            bakery.JobSlots["Laborers"] = 20;
            bakery.JobSlots["Craftsmen"] = 4;

            houston.Factories.Add(bakery);
            foodCorp.AddFactory(bakery);
            _allCorporations.Add(foodCorp);

            // Add starting stockpile to cities
            foreach (var state in _currentCountry.States)
            {
                foreach (var city in state.Cities)
                {
                    // Add basic goods to stockpile
                    city.Stockpile["Grain"] = new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 10000);
                    city.Stockpile["Coal"] = new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 5000);
                    city.Stockpile["Iron"] = new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 3000);
                    city.Stockpile["Bread"] = new Good("Bread", Market.GoodDefinitions["Bread"].BasePrice, GoodCategory.ProcessedFood, 8000);
                    city.Stockpile["Cloth"] = new Good("Cloth", Market.GoodDefinitions["Cloth"].BasePrice, GoodCategory.ConsumerProduct, 4000);
                }
            }

            Market.AllCorporations.Clear();
            Market.AllCorporations.AddRange(_allCorporations);

            _allCountries = new List<Country> { _currentCountry };

            InitializeTradeSystemsForCurrentWorld();

            // Set up player as Prime Minister BEFORE the HUD tries to access it
            _playerRoleManager = new PlayerRoleManager();
            _playerRoleManager.AssumeRolePrimeMinister(_currentCountry);

            // Set _playerCountry to the current country
            _playerCountry = _currentCountry;

            _economyInitialized = true;
            Debug.WriteLine($"[Economy Init] Economy initialized with {_currentCountry.States.Count} states, {_currentCountry.States.Sum(s => s.Cities.Count)} cities, and {_allCorporations.Count} corporations");
        }
        private void OnEconomyUpdateTick(object? sender, EventArgs e)
        {
            if (!_economyInitialized || _currentCountry == null) return;

            try
            {
                Debug.WriteLine("[Economy Update] Running economy simulation tick...");

                var activeCountries = (_allCountries != null && _allCountries.Count > 0)
                    ? _allCountries
                    : new List<Country> { _currentCountry };

                var playerCities = _currentCountry.States.SelectMany(s => s.Cities).ToList();
                var allCities = activeCountries.SelectMany(c => c.States).SelectMany(s => s.Cities).ToList();

                _tradeRouteManager ??= new TradeRouteManager();
                if (_enhancedTradeManager == null)
                {
                    _enhancedTradeManager = new EnhancedTradeManager(activeCountries);
                }

                GlobalMarket.Instance?.PrepareForNewTurn();

                // Run the economy update cycle for city-level data
                foreach (var city in playerCities)
                {
                    Economy.UpdateCityEconomy(city);
                }

                // Resolve inter-city trade before aggregating state/country metrics
                Economy.ResolveInterCityTrade(allCities, activeCountries);

                _tradeRouteManager?.UpdateAllRoutes();

                if (GlobalMarket.Instance != null)
                {
                    GlobalMarket.Instance.UpdateGlobalMarket(allCities, activeCountries, _tradeRouteManager, _enhancedTradeManager);

                    if (_enhancedTradeManager != null)
                    {
                        InternationalTrade.ExecuteTradeTurn(activeCountries, GlobalMarket.Instance, _enhancedTradeManager);
                    }
                }

                RefreshTradeViewModel();

                foreach (var state in _currentCountry.States)
                {
                    Economy.UpdateStateEconomy(state);
                }

                Economy.UpdateCountryEconomy(_currentCountry);

                // Update population growth
                Economy.UpdateCountryPopulation(_currentCountry);

                // Run AI for corporations
                var random = new Random();
                foreach (var corp in _allCorporations)
                {
                    corp.UpdateAI(playerCities, Market.GoodDefinitions.Values.ToList(), random);
                }

                // Simulate monetary effects
                _currentCountry.FinancialSystem.SimulateMonetaryEffects();

                // Update displays
                UpdateEconomyDisplay();
                UpdateHUDDisplay(null, null);

                Debug.WriteLine($"[Economy Update] Country budget: ${_currentCountry.Budget:N0}, GDP estimate: ${CalculateGDP():N0}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Economy Update Error] {ex.Message}");
                Debug.WriteLine($"[Economy Update Error] Stack trace: {ex.StackTrace}");
            }
        }

        private decimal CalculateGDP()
        {
            if (_currentCountry == null) return 0;

            decimal totalGDP = 0;

            // Sum all economic activity: population income + corporate profits
            foreach (var state in _currentCountry.States)
            {
                foreach (var city in state.Cities)
                {
                    foreach (var pop in city.PopClasses)
                    {
                        totalGDP += (decimal)(pop.Size * pop.IncomePerPerson);
                    }
                }
            }

            // Add corporate output value
            foreach (var corp in _allCorporations)
            {
                totalGDP += (decimal)(corp.Budget * 0.1); // Approximate 10% of corp budget as profit contribution
            }

            return totalGDP;
        }

        private void UpdateEconomyDisplay()
        {
            if (!_economyInitialized || _currentCountry == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Update GDP display
                    decimal gdp = CalculateGDP();
                    if (this.FindControl<TextBlock>("GDPText") is TextBlock gdpText)
                    {
                        gdpText.Text = $"${gdp / 1000000000m:F2}T";
                    }
                    if (this.FindControl<TextBlock>("SideGDPText") is TextBlock sideGdpText)
                    {
                        sideGdpText.Text = $"${gdp / 1000000000m:F2}T";
                    }

                    // Calculate unemployment rate
                    int totalPop = _currentCountry.States.SelectMany(s => s.Cities).SelectMany(c => c.PopClasses).Sum(p => p.Size);
                    int employed = _currentCountry.States.SelectMany(s => s.Cities).SelectMany(c => c.PopClasses).Sum(p => p.Employed);
                    double unemploymentRate = totalPop > 0 ? ((totalPop - employed) / (double)totalPop) * 100 : 0;

                    if (this.FindControl<TextBlock>("UnemploymentText") is TextBlock unempText)
                    {
                        unempText.Text = $"{unemploymentRate:F1}%";
                    }
                    if (this.FindControl<TextBlock>("SideUnemploymentText") is TextBlock sideUnempText)
                    {
                        sideUnempText.Text = $"{unemploymentRate:F1}%";
                    }

                    // Update inflation (from financial system)
                    decimal inflationRate = _currentCountry.FinancialSystem.InflationRate * 100;
                    if (this.FindControl<TextBlock>("InflationText") is TextBlock inflText)
                    {
                        inflText.Text = $"{inflationRate:F1}%";
                    }
                    if (this.FindControl<TextBlock>("SideInflationText") is TextBlock sideInflText)
                    {
                        sideInflText.Text = $"{inflationRate:F1}%";
                    }

                    double globalTradeValue = GlobalMarket.Instance?.GlobalTradeValue ?? 0;
                    if (this.FindControl<TextBlock>("GlobalTradeText") is TextBlock globalTradeText)
                    {
                        globalTradeText.Text = $"${FormatCurrency(globalTradeValue)}";
                    }

                    // Update industries list with real data
                    if (this.FindControl<ListBox>("IndustriesList") is ListBox industriesList)
                    {
                        industriesList.Items.Clear();

                        var factoriesByType = _currentCountry.States
                            .SelectMany(s => s.Cities)
                            .SelectMany(c => c.Factories)
                            .GroupBy(f => f.OutputGoods.FirstOrDefault()?.Name ?? "Unknown")
                            .OrderByDescending(g => g.Count())
                            .Take(6);

                        foreach (var group in factoriesByType)
                        {
                            int count = group.Count();
                            double totalOutput = group.Sum(f => f.ProductionCapacity * f.OutputGoods.Sum(o => o.Quantity));
                            string goodName = group.Key;
                            industriesList.Items.Add($"🏭 {goodName} - {count} factories (Output: {totalOutput:N0} units)");
                        }
                    }

                    if (this.FindControl<ListBox>("SideIndustriesList") is ListBox sideIndList)
                    {
                        sideIndList.Items.Clear();

                        var factoriesByType = _currentCountry.States
                            .SelectMany(s => s.Cities)
                            .SelectMany(c => c.Factories)
                            .GroupBy(f => f.OutputGoods.FirstOrDefault()?.Name ?? "Unknown")
                            .OrderByDescending(g => g.Count())
                            .Take(3);

                        foreach (var group in factoriesByType)
                        {
                            int count = group.Count();
                            sideIndList.Items.Add($"🏭 {group.Key} - {count} factories");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Economy Display Error] {ex.Message}");
                }
            });
        }

        private void SetupHUDEventHandlers()
        {
            // Get references to HUD elements and add event handlers
            if (this.FindControl<Button>("DiplomacyButton") is Button diplomacyBtn)
                diplomacyBtn.Click += OnDiplomacyClicked;

            if (this.FindControl<Button>("TradeButton") is Button tradeBtn)
                tradeBtn.Click += OnTradeClicked;

            if (this.FindControl<Button>("ConstructionButton") is Button constructionBtn)
                constructionBtn.Click += OnConstructionClicked;

            if (this.FindControl<Button>("RoleActionButton") is Button roleActionBtn)
                roleActionBtn.Click += OnRoleActionClicked;

            if (this.FindControl<Button>("EconomyViewButton") is Button economyBtn)
                economyBtn.Click += OnEconomyViewClicked;

            if (this.FindControl<Button>("StatsButton") is Button statsBtn)
                statsBtn.Click += OnStatsClicked;

            if (this.FindControl<Button>("DebugButton") is Button debugBtn)
                debugBtn.Click += OnDebugClicked;

            if (this.FindControl<Button>("MenuButton") is Button menuBtn)
                menuBtn.Click += OnMenuClicked;

            // Map view toggle buttons
            if (this.FindControl<Button>("TerrainViewButton") is Button terrainBtn)
                terrainBtn.Click += OnTerrainViewClicked;

            if (this.FindControl<Button>("PoliticalViewButton") is Button politicalBtn)
                politicalBtn.Click += OnPoliticalViewClicked;

            if (this.FindControl<Button>("PlaceHolder1Button") is Button populationBtn)
                populationBtn.Click += OnPopulationDensityViewClicked;

            if (this.FindControl<Button>("StatesViewButton") is Button statesBtn)
                statesBtn.Click += OnStatesViewClicked;

            // Setup close button handlers for popup menus
            if (this.FindControl<Button>("DiplomacyCloseButton") is Button diplomacyCloseBtn)
                diplomacyCloseBtn.Click += (s, e) => HideAllPopups();

            if (this.FindControl<Button>("TradeCloseButton") is Button tradeCloseBtn)
                tradeCloseBtn.Click += (s, e) => HideAllPopups();

            if (this.FindControl<Button>("ConstructionCloseButton") is Button constructionCloseBtn)
                constructionCloseBtn.Click += (s, e) => HideAllPopups();

            if (this.FindControl<Button>("EconomyCloseButton") is Button economyCloseBtn)
                economyCloseBtn.Click += (s, e) => HideAllPopups();

            if (this.FindControl<Button>("StatsCloseButton") is Button statsCloseBtn)
                statsCloseBtn.Click += (s, e) => HideAllPopups();

            if (this.FindControl<Button>("DebugCloseButton") is Button debugCloseBtn)
                debugCloseBtn.Click += (s, e) => HideAllPopups();

            // Setup overlay click handlers to close popups when clicking outside
            if (this.FindControl<Border>("DiplomacyMenuOverlay") is Border diplomacyOverlay)
                diplomacyOverlay.PointerPressed += OnOverlayClicked;

            if (this.FindControl<Border>("TradeMenuOverlay") is Border tradeOverlay)
                tradeOverlay.PointerPressed += OnOverlayClicked;

            if (this.FindControl<Border>("ConstructionMenuOverlay") is Border constructionOverlay)
                constructionOverlay.PointerPressed += OnOverlayClicked;

            if (this.FindControl<Border>("EconomyMenuOverlay") is Border economyOverlay)
                economyOverlay.PointerPressed += OnOverlayClicked;

            if (this.FindControl<Border>("StatsMenuOverlay") is Border statsOverlay)
                statsOverlay.PointerPressed += OnOverlayClicked;

            if (this.FindControl<Border>("DebugMenuOverlay") is Border debugOverlay)
                debugOverlay.PointerPressed += OnOverlayClicked;

            // Side menu close button
            if (this.FindControl<Button>("SideMenuCloseButton") is Button sideCloseBtn)
                sideCloseBtn.Click += (s, e) => HideRightSideMenu();

            // Setup popup and side menu content
            InitializePopupMenus();
            InitializeSideMenus();
        }

        private void UpdateHUDDisplay(object? sender, EventArgs? e)
        {
            try
            {
                // Update player role and controlled entity
                if (this.FindControl<TextBlock>("PlayerRoleText") is TextBlock roleText)
                {
                    roleText.Text = _playerRoleManager.CurrentRole.ToString().Replace("PrimeMinister", "Prime Minister");
                }

                if (this.FindControl<TextBlock>("ControlledEntityText") is TextBlock entityText)
                {
                    string entityName = _playerRoleManager.CurrentRole switch
                    {
                        PlayerRoleType.PrimeMinister => _playerRoleManager.ControlledCountry?.Name ?? "N/A",
                        PlayerRoleType.Governor => _playerRoleManager.ControlledState?.Name ?? "N/A",
                        PlayerRoleType.CEO => _playerRoleManager.ControlledCorporation?.Name ?? "N/A",
                        _ => "None"
                    };
                    entityText.Text = entityName;
                }

                // Update treasury information
                if (this.FindControl<TextBlock>("TreasuryText") is TextBlock treasuryText)
                {
                    double budget = _playerRoleManager.CurrentRole switch
                    {
                        PlayerRoleType.PrimeMinister => _playerRoleManager.ControlledCountry?.Budget ?? 0,
                        PlayerRoleType.Governor => _playerRoleManager.ControlledState?.Budget ?? 0,
                        PlayerRoleType.CEO => _playerRoleManager.ControlledCorporation?.Budget ?? 0,
                        _ => 0
                    };
                    treasuryText.Text = $"${budget:N0}";
                }

                // Update population
                if (this.FindControl<TextBlock>("PopulationText") is TextBlock popText)
                {
                    int population = _playerRoleManager.CurrentRole switch
                    {
                        PlayerRoleType.PrimeMinister => _playerRoleManager.ControlledCountry?.Population ?? 0,
                        PlayerRoleType.Governor => _playerRoleManager.ControlledState?.Population ?? 0,
                        _ => 0
                    };
                    popText.Text = $"{population:N0}";
                }

                // Update date/time (placeholder)
                if (this.FindControl<TextBlock>("DateTimeText") is TextBlock dateText)
                {
                    dateText.Text = DateTime.Now.ToString("MMMM yyyy");
                }

                // Update role-specific action button
                if (this.FindControl<Button>("RoleActionButton") is Button roleBtn)
                {
                    roleBtn.Content = _playerRoleManager.CurrentRole switch
                    {
                        PlayerRoleType.PrimeMinister => "Set National Policy",
                        PlayerRoleType.Governor => "Set State Policy",
                        PlayerRoleType.CEO => "Build Factory",
                        _ => "No Action"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating HUD: {ex.Message}");
            }
        }

        #endregion

        #region Popup Menu Management

        private void HideAllPopups()
        {
            if (this.FindControl<Border>("DiplomacyMenuOverlay") is Border diplomacyOverlay)
                diplomacyOverlay.IsVisible = false;

            if (this.FindControl<Border>("TradeMenuOverlay") is Border tradeOverlay)
                tradeOverlay.IsVisible = false;

            if (this.FindControl<Border>("ConstructionMenuOverlay") is Border constructionOverlay)
                constructionOverlay.IsVisible = false;

            if (this.FindControl<Border>("EconomyMenuOverlay") is Border economyOverlay)
                economyOverlay.IsVisible = false;

            if (this.FindControl<Border>("StatsMenuOverlay") is Border statsOverlay)
                statsOverlay.IsVisible = false;

            if (this.FindControl<Border>("DebugMenuOverlay") is Border debugOverlay)
                debugOverlay.IsVisible = false;
        }

        private void ShowPopup(String popupName)
        {
            HideAllPopups();
            if (this.FindControl<Border>(popupName) is Border popup)
            {
                popup.IsVisible = true;
            }
        }

        private void OnOverlayClicked(object? sender, PointerPressedEventArgs e)
        {
            // Only close if clicking directly on the overlay (not on the inner content)
            if (sender == e.Source)
            {
                HideAllPopups();
            }
        }

        private void InitializePopupMenus()
        {
            // Initialize Diplomacy menu content
            if (this.FindControl<ListBox>("DiplomacyRelationsList") is ListBox diplomacyList)
            {
                var relations = new[]
                {
                    "🇬🇧 United Kingdom - Allied (+85)",
                    "🇷🇺 Russia - Cold War (-45)",
                    "🇨🇳 China - Neutral (0)",
                    "🇫🇷 France - Friendly (+60)",
                    "🇩🇪 Germany - Allied (+75)",
                    "🇯🇵 Japan - Trade Partner (+40)"
                };
                foreach (var relation in relations)
                {
                    diplomacyList.Items.Add(relation);
                }
            }

            // Initialize Construction menu content
            if (this.FindControl<ListBox>("ActiveProjectsList") is ListBox projectsList)
            {
                var projects = new[]
                {
                    "🏭 Steel Factory - Los Angeles (Progress: 75%)",
                    "🛣️ Interstate Highway - Texas (Progress: 45%)",
                    "🌉 Golden Gate Bridge Maintenance (Progress: 20%)",
                    "✈️ Airport Expansion - New York (Progress: 90%)"
                };
                foreach (var project in projects)
                {
                    projectsList.Items.Add(project);
                }
            }

            // Initialize Economy menu content
            if (this.FindControl<ListBox>("IndustriesList") is ListBox industriesList)
            {
                var industries = new[]
                {
                    "🏭 Manufacturing - Output: $850B (↗️ +2.8%)",
                    "💻 Technology - Output: $620B (↗️ +8.1%)",
                    "🌾 Agriculture - Output: $180B (↗️ +1.2%)",
                    "⚡ Energy - Output: $290B (↗️ +3.5%)",
                    "🏗️ Construction - Output: $240B (↗️ +4.2%)",
                    "🚗 Automotive - Output: $320B (↗️ +1.8%)"
                };
                foreach (var industry in industries)
                {
                    industriesList.Items.Add(industry);
                }
            }

            // Initialize Statistics menu content
            if (this.FindControl<ListBox>("DetailedStatsList") is ListBox statsList)
            {
                var stats = new[]
                {
                    "👥 Total Cities: 125",
                    "🏭 Active Factories: 2,847",
                    "🛣️ Roads Built: 45,230 km",
                    "🌉 Bridges: 8,954",
                    "✈️ Airports: 342",
                    "🏛️ Government Buildings: 1,205",
                    "💰 Tax Revenue: $1.2T/year",
                    "📈 Economic Growth: +3.2%",
                    "🎯 Approval Rating: 67%"
                };
                foreach (var stat in stats)
                {
                    statsList.Items.Add(stat);
                }
            }

            if (this.FindControl<Button>("CullEmptyStatesButton") is Button cullButton)
                cullButton.Click += OnCullEmptyStatesClicked;
        }

        private void InitializeSideMenus()
        {
            // Header defaults
            if (this.FindControl<TextBlock>("SideMenuTitleText") is TextBlock title)
                title.Text = "Details";

            // Diplomacy
            if (this.FindControl<ListBox>("SideDiplomacyRelationsList") is ListBox sideDip)
            {
                var relations = new[]
                {
                    "🇬🇧 United Kingdom - Allied (+85)",
                    "🇷🇺 Russia - Cold War (-45)",
                    "🇨🇳 China - Neutral (0)",
                    "🇫🇷 France - Friendly (+60)",
                    "🇩🇪 Germany - Allied (+75)",
                    "🇯🇵 Japan - Trade Partner (+40)"
                };
                foreach (var relation in relations)
                    sideDip.Items.Add(relation);
            }

            // Construction
            if (this.FindControl<ListBox>("SideActiveProjectsList") is ListBox sideProjects)
            {
                var projects = new[]
                {
                    "🏭 Steel Factory - Los Angeles (Progress: 75%)",
                    "🛣️ Interstate Highway - Texas (Progress: 45%)",
                    "🌉 Golden Gate Bridge Maintenance (Progress: 20%)",
                    "✈️ Airport Expansion - New York (Progress: 90%)"
                };
                foreach (var project in projects)
                    sideProjects.Items.Add(project);
            }

            // Economy
            if (this.FindControl<TextBlock>("SideGDPText") is TextBlock gdp)
                gdp.Text = "$2.5T";
            if (this.FindControl<TextBlock>("SideUnemploymentText") is TextBlock unemp)
                unemp.Text = "4.2%";
            if (this.FindControl<TextBlock>("SideInflationText") is TextBlock infl)
                infl.Text = "2.1%";
            if (this.FindControl<ListBox>("SideIndustriesList") is ListBox sideIndustries)
            {
                var industries = new[]
                {
                    "🏭 Manufacturing - Output: $850B (↗️ +2.8%)",
                    "💻 Technology - Output: $620B (↗️ +8.1%)",
                    "🌾 Agriculture - Output: $180B (↗️ +1.2%)"
                };
                foreach (var ind in industries)
                    sideIndustries.Items.Add(ind);
            }

            // Stats
            if (this.FindControl<TextBlock>("SideTotalPopulationText") is TextBlock totPop)
                totPop.Text = "328,000,000";
            if (this.FindControl<TextBlock>("SidePopGrowthText") is TextBlock popG)
                popG.Text = "+0.7%";
            if (this.FindControl<ListBox>("SideDetailedStatsList") is ListBox sideStats)
            {
                var stats = new[]
                {
                    "👥 Total Cities: 125",
                    "🏭 Active Factories: 2,847",
                    "🛣️ Roads Built: 45,230 km"
                };
                foreach (var s in stats)
                    sideStats.Items.Add(s);
            }
        }

        #endregion

        #region Side Menu Helpers

        private void HideAllSidePanels()
        {
            void Hide(string name)
            {
                if (this.FindControl<Control>(name) is Control c)
                    c.IsVisible = false;
            }
            Hide("SideDiplomacyPanel");
            Hide("SideTradePanel");
            Hide("SideConstructionPanel");
            Hide("SideEconomyPanel");
            Hide("SideStatsPanel");
        }

        private void HideRightSideMenu()
        {
            Debug.WriteLine("HideRightSideMenu called");

            // Ensure we're on the UI thread
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(HideRightSideMenu);
                return;
            }

            HideAllSidePanels();

            // Find and hide the RightSideMenu
            if (this.FindControl<Border>("RightSideMenu") is Border panel)
            {
                Debug.WriteLine($"Setting RightSideMenu IsVisible to false. Was: {panel.IsVisible}");
                panel.IsVisible = false;
            }
            else
            {
                Debug.WriteLine("RightSideMenu Border not found!");
            }

            // Collapse the side menu column by setting its width to 0
            if (this.FindControl<Grid>("RootGrid") is Grid rootGrid &&
                rootGrid.ColumnDefinitions.Count > 1)
            {
                Debug.WriteLine("Collapsing side menu column");
                rootGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }

            // Force immediate layout update with a small delay to allow layout to settle
            Dispatcher.UIThread.Post(() =>
            {
                Debug.WriteLine("HideRightSideMenu: Forcing layout update and buffer recreation");

                // Force layout updates
                this.InvalidateArrange();
                this.InvalidateMeasure();

                // Small delay before recreating buffers to ensure layout is complete
                Dispatcher.UIThread.Post(() =>
                {
                    // Recreate buffers to new size and re-render
                    RecreateBuffersToCurrentSize();
                    QueueRender(immediate: true);
                }, DispatcherPriority.Background);

            }, DispatcherPriority.Normal);
        }

        private void ShowRightSidePanel(string title, string panelName)
        {
            Debug.WriteLine($"ShowRightSidePanel called: {title}, {panelName}");

            // Ensure we're on the UI thread
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => ShowRightSidePanel(title, panelName));
                return;
            }

            HideAllPopups();

            // Expand the side menu column to show the panel
            if (this.FindControl<Grid>("RootGrid") is Grid rootGrid &&
                rootGrid.ColumnDefinitions.Count > 1)
            {
                Debug.WriteLine("Expanding side menu column to 420 pixels");
                rootGrid.ColumnDefinitions[1].Width = new GridLength(420);
            }

            // Show the RightSideMenu
            if (this.FindControl<Border>("RightSideMenu") is Border panel)
            {
                Debug.WriteLine($"Setting RightSideMenu IsVisible to true. Was: {panel.IsVisible}");
                panel.IsVisible = true;
            }
            else
            {
                Debug.WriteLine("RightSideMenu Border not found!");
            }

            // Set title
            if (this.FindControl<TextBlock>("SideMenuTitleText") is TextBlock titleText)
                titleText.Text = title;

            HideAllSidePanels();
            if (this.FindControl<Control>(panelName) is Control content)
            {
                Debug.WriteLine($"Setting {panelName} IsVisible to true");
                content.IsVisible = true;
            }
            else
            {
                Debug.WriteLine($"Panel {panelName} not found!");
            }

            // Force layout update to ensure proper map resize when side menu appears with a small delay
            Dispatcher.UIThread.Post(() =>
            {
                Debug.WriteLine("ShowRightSidePanel: Forcing layout update and buffer recreation");

                // Force layout updates
                this.InvalidateArrange();
                this.InvalidateMeasure();

                // Small delay before recreating buffers to ensure layout is complete
                Dispatcher.UIThread.Post(() =>
                {
                    // Recreate buffers to new size and re-render
                    RecreateBuffersToCurrentSize();
                    QueueRender(immediate: true);
                }, DispatcherPriority.Background);

            }, DispatcherPriority.Normal);
        }

        #endregion

        #region HUD Event Handlers

        private void OnDiplomacyClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Diplomacy button clicked - showing right side diplomacy panel");
            ShowRightSidePanel("Diplomatic Relations", "SideDiplomacyPanel");
        }

        private void OnTradeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Trade button clicked - showing right side trade panel");
            ShowRightSidePanel("Trade Management", "SideTradePanel");
        }

        private void OnConstructionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Construction button clicked - showing right side construction panel");
            ShowRightSidePanel("Construction Projects", "SideConstructionPanel");
        }

        private void OnRoleActionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine($"Role action clicked for {_playerRoleManager.CurrentRole}");

            switch (_playerRoleManager.CurrentRole)
            {
                case PlayerRoleType.PrimeMinister:
                    // Example: Set a national policy
                    bool success = _playerRoleManager.SetNationalPolicy("TaxRate", "25%");
                    Debug.WriteLine($"National policy set: {success}");
                    break;
                case PlayerRoleType.Governor:
                    // Example: Set a state policy
                    _playerRoleManager.SetStatePolicy("LocalTax", "5%");
                    break;
                case PlayerRoleType.CEO:
                    // Example: Build a factory (need a city reference)
                    if (_currentCountry.States.Count > 0 && _currentCountry.States[0].Cities.Count > 0)
                    {
                        var city = _currentCountry.States[0].Cities[0];
                        _playerRoleManager.BuildFactoryAsCEO("Steel", city);
                    }
                    break;
            }
        }

        private void OnEconomyViewClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Economy view button clicked - showing economy overlay");
            ShowEconomyOverlay();
        }

        private void OnStatsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Stats button clicked - showing right side statistics panel");
            ShowRightSidePanel("Game Statistics", "SideStatsPanel");
        }

        // ===== ECONOMY OVERLAY (CLEAN IMPLEMENTATION) =====
        private List<(string name, double amount)> _fullRevenueData = new();
        private List<(string name, double amount)> _fullExpenseData = new();
        private Corporation? _selectedCorporation;

        private void ShowEconomyOverlay()
        {
            if (this.FindControl<Border>("EconomyMenuOverlay") is Border overlay)
            {
                overlay.IsVisible = true;
                SwitchEconomyTab("PrivateSector");
            }
            // Attach tab handlers (idempotent – we remove before adding)
            if (this.FindControl<Button>("PrivateSectorTabButton") is Button privBtn)
            {
                privBtn.Click -= PrivateSectorTabButton_Click;
                privBtn.Click += PrivateSectorTabButton_Click;
            }
            if (this.FindControl<Button>("BudgetTabButton") is Button budBtn)
            {
                budBtn.Click -= BudgetTabButton_Click;
                budBtn.Click += BudgetTabButton_Click;
            }
            if (this.FindControl<Button>("PopulationTabButton") is Button popBtn)
            {
                popBtn.Click -= PopulationTabButton_Click;
                popBtn.Click += PopulationTabButton_Click;
            }
            if (this.FindControl<Button>("EconomyCloseButton") is Button closeBtn)
            {
                closeBtn.Click -= EconomyCloseButton_Click;
                closeBtn.Click += EconomyCloseButton_Click;
            }
        }

        private void EconomyCloseButton_Click(object? sender, RoutedEventArgs e) => HideEconomyOverlay();
        private void PrivateSectorTabButton_Click(object? sender, RoutedEventArgs e) => SwitchEconomyTab("PrivateSector");
        private void BudgetTabButton_Click(object? sender, RoutedEventArgs e) => SwitchEconomyTab("Budget");
        private void PopulationTabButton_Click(object? sender, RoutedEventArgs e) => SwitchEconomyTab("Population");

        private void HideEconomyOverlay()
        {
            if (this.FindControl<Border>("EconomyMenuOverlay") is Border overlay)
                overlay.IsVisible = false;
        }

        private void SwitchEconomyTab(string tab)
        {
            var privatePanel = this.FindControl<Grid>("PrivateSectorPanel");
            var budgetPanel = this.FindControl<Grid>("BudgetPanel");
            var populationPanel = this.FindControl<Grid>("PopulationPanel");
            var privateBtn = this.FindControl<Button>("PrivateSectorTabButton");
            var budgetBtn = this.FindControl<Button>("BudgetTabButton");
            var popBtn = this.FindControl<Button>("PopulationTabButton");

            void HideAll()
            {
                if (privatePanel != null) privatePanel.IsVisible = false;
                if (budgetPanel != null) budgetPanel.IsVisible = false;
                if (populationPanel != null) populationPanel.IsVisible = false;
                if (privateBtn != null) privateBtn.Background = new SolidColorBrush(Color.Parse("#555555"));
                if (budgetBtn != null) budgetBtn.Background = new SolidColorBrush(Color.Parse("#555555"));
                if (popBtn != null) popBtn.Background = new SolidColorBrush(Color.Parse("#555555"));
            }
            HideAll();

            switch (tab)
            {
                case "PrivateSector":
                    if (privatePanel != null) privatePanel.IsVisible = true;
                    if (privateBtn != null) privateBtn.Background = new SolidColorBrush(Color.Parse("#1A4A1A"));
                    UpdatePrivateSectorTab();
                    break;
                case "Budget":
                    if (budgetPanel != null) budgetPanel.IsVisible = true;
                    if (budgetBtn != null) budgetBtn.Background = new SolidColorBrush(Color.Parse("#1A4A1A"));
                    UpdateBudgetTab();
                    break;
                case "Population":
                    if (populationPanel != null) populationPanel.IsVisible = true;
                    if (popBtn != null) popBtn.Background = new SolidColorBrush(Color.Parse("#1A4A1A"));
                    UpdatePopulationTab();
                    break;
            }
        }

        private void UpdatePrivateSectorTab()
        {
            if (!_economyInitialized) return;
            var corpList = this.FindControl<ListBox>("CorporationsList");
            var industriesList = this.FindControl<ListBox>("IndustriesList");
            if (corpList != null)
            {
                corpList.Items.Clear();
                foreach (var corp in _allCorporations.OrderByDescending(c => c.Budget))
                {
                    var factories = corp.OwnedFactories;
                    double revenue = factories.Sum(f => f.ProductionCapacity * 1000.0);
                    double costs = factories.Sum(f => f.ProductionCapacity * 500.0);
                    double profit = revenue - costs;
                    double stockPrice = 50 + profit / 100000.0;
                    corpList.Items.Add($"🏢 {corp.Name}\n   Revenue: ${FormatCurrency(revenue)} | Profit: ${FormatCurrency(profit)}\n   Stock: ${stockPrice:F2} | Factories: {factories.Count}");
                }
            }
            if (industriesList != null && _currentCountry != null)
            {
                industriesList.Items.Clear();
                var groups = _currentCountry.States.SelectMany(s => s.Cities).SelectMany(c => c.Factories)
                    .GroupBy(f => f.OutputGoods.FirstOrDefault()?.Name ?? "Unknown")
                    .OrderByDescending(g => g.Count()).Take(6);
                foreach (var g in groups)
                {
                    int count = g.Count();
                    double output = g.Sum(f => f.ProductionCapacity * f.OutputGoods.Sum(o => o.Quantity));
                    industriesList.Items.Add($"🏭 {g.Key} - {count} factories (Output: {output:N0})");
                }
            }
            if (corpList != null)
            {
                corpList.SelectionChanged -= CorporationsList_SelectionChanged;
                corpList.SelectionChanged += CorporationsList_SelectionChanged;
            }
        }

        private void UpdateBudgetTab()
        {
            if (_playerCountry == null) return;
            double totalRevenue = 0; double totalExpenses = 0;
            var revenueList = new List<(string name, double amount)>();
            var expenseList = new List<(string name, double amount)>();
            foreach (var state in _playerCountry.States)
            {
                foreach (var city in state.Cities)
                {
                    var incomeTax = city.PopClasses.Sum(p => p.Size * p.IncomePerPerson * 0.15);
                    revenueList.Add(($"Income Tax - {city.Name}", incomeTax));
                    totalRevenue += incomeTax;
                    var corpTax = city.Factories.Sum(f => f.ProductionCapacity * 100 * 0.21);
                    revenueList.Add(($"Corporate Tax - {city.Name}", corpTax));
                    totalRevenue += corpTax;
                }
                var admin = state.Cities.Count * 50000.0;
                expenseList.Add(($"Administration - {state.Name}", admin)); totalExpenses += admin;
                var infra = state.Cities.Count * 100000.0;
                expenseList.Add(($"Infrastructure - {state.Name}", infra)); totalExpenses += infra;
            }
            var military = (double)_playerCountry.Population * 0.05; expenseList.Add(("Military", military)); totalExpenses += military;

            if (this.FindControl<TextBlock>("TotalRevenueText") is TextBlock tr) tr.Text = $"${FormatCurrency(totalRevenue)}";
            if (this.FindControl<TextBlock>("TotalExpensesText") is TextBlock te) te.Text = $"${FormatCurrency(totalExpenses)}";
            double balance = totalRevenue - totalExpenses;
            if (this.FindControl<TextBlock>("BudgetBalanceText") is TextBlock bb)
            {
                bb.Text = $"{(balance >= 0 ? "+" : "-")}${FormatCurrency(Math.Abs(balance))}";
                bb.Foreground = new SolidColorBrush(Color.Parse(balance >= 0 ? "#90EE90" : "#F08080"));
            }
            if (this.FindControl<ListBox>("RevenueSourcesList") is ListBox revList)
            {
                revList.Items.Clear();
                foreach (var r in revenueList.OrderByDescending(r => r.amount).Take(5))
                    revList.Items.Add($"{r.name}: ${FormatCurrency(r.amount)}");
            }
            if (this.FindControl<ListBox>("ExpenseCategoriesList") is ListBox expList)
            {
                expList.Items.Clear();
                foreach (var ex in expenseList.OrderByDescending(r => r.amount).Take(5))
                    expList.Items.Add($"{ex.name}: ${FormatCurrency(ex.amount)}");
            }
            _fullRevenueData = revenueList; _fullExpenseData = expenseList;
        }

        private void UpdatePopulationTab()
        {
            if (_playerCountry == null) return;
            var allCities = _playerCountry.States.SelectMany(s => s.Cities).ToList();
            int totalPop = allCities.Sum(c => c.Population);
            double avgQoL = allCities.Average(c => c.PopClasses.Any() ? c.PopClasses.Average(p => p.QualityOfLife) : 50);
            double avgHappiness = allCities.Average(c => c.PopClasses.Any() ? c.PopClasses.Average(p => p.Happiness) : 50);
            double popGrowth = 0.7; // placeholder
            if (this.FindControl<TextBlock>("AvgQoLText") is TextBlock q) q.Text = $"{avgQoL:F0}%";
            if (this.FindControl<TextBlock>("AvgHappinessText") is TextBlock h) h.Text = $"{avgHappiness:F0}%";
            if (this.FindControl<TextBlock>("PopGrowthRateText") is TextBlock g) g.Text = $"+{popGrowth:F1}%";
            if (this.FindControl<ListBox>("PopulationClassesList") is ListBox pcl)
            {
                pcl.Items.Clear();
                var groups = allCities.SelectMany(c => c.PopClasses).GroupBy(p => p.Name).OrderByDescending(gp => gp.Sum(p => p.Size));
                foreach (var gp in groups)
                {
                    int size = gp.Sum(p => p.Size);
                    double pct = totalPop > 0 ? (size / (double)totalPop) * 100 : 0;
                    double income = gp.Average(p => p.IncomePerPerson);
                    double happiness = gp.Average(p => p.Happiness);
                    pcl.Items.Add($"{gp.Key} ({pct:F1}%)\n   Pop: {FormatCurrency(size)} | Avg Income: ${income:F0} | Happiness: {happiness:F0}%");
                }
            }
        }

        private void CorporationsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this.FindControl<ListBox>("CorporationsList") is not ListBox list) return;
            var ordered = _allCorporations.OrderByDescending(c => c.Budget).ToList();
            if (list.SelectedIndex < 0 || list.SelectedIndex >= ordered.Count) return;
            _selectedCorporation = ordered[list.SelectedIndex];
            ShowCompanyDetails(_selectedCorporation);
        }

        private void ShowCompanyDetails(Corporation? corp)
        {
            if (corp == null) return;
            if (this.FindControl<Border>("CompanyDetailsOverlay") is not Border overlay) return;
            (this.FindControl<TextBlock>("CompanyNameText"))?.Let(t => t.Text = corp.Name);
            (this.FindControl<TextBlock>("CompanyTypeText"))?.Let(t => t.Text = $" ({corp.Specialization})");
            double revenue = corp.OwnedFactories.Sum(f => f.ProductionCapacity * 1000.0);
            double costs = corp.OwnedFactories.Sum(f => f.ProductionCapacity * 500.0);
            double profit = revenue - costs;
            (this.FindControl<TextBlock>("CompanyRevenueText"))?.Let(t => t.Text = $"${FormatCurrency(revenue)}");
            (this.FindControl<TextBlock>("CompanyProfitText"))?.Let(t => t.Text = $"${FormatCurrency(profit)}");
            (this.FindControl<TextBlock>("CompanyBudgetText"))?.Let(t => t.Text = $"${FormatCurrency(corp.Budget)}");
            (this.FindControl<TextBlock>("CompanyStockPriceText"))?.Let(t => t.Text = $"${(50 + profit / 100000.0):F2}");
            if (this.FindControl<ListBox>("CompanyBuildingsList") is ListBox buildings)
            {
                buildings.Items.Clear();
                foreach (var f in corp.OwnedFactories)
                {
                    double fProfit = f.ProductionCapacity * 500.0;
                    buildings.Items.Add($"🏭 {f.Name}\n   Capacity: {f.ProductionCapacity}\n   Profit: ${FormatCurrency(fProfit)} | Workers: {f.WorkersEmployed}");
                }
                buildings.SelectionChanged -= CompanyBuildingsList_SelectionChanged;
                buildings.SelectionChanged += CompanyBuildingsList_SelectionChanged;
            }
            overlay.IsVisible = true;
            if (this.FindControl<Button>("CompanyDetailsCloseButton") is Button closeBtn)
            {
                closeBtn.Click -= (_, __) => HideCompanyDetails();
                closeBtn.Click += (s, e) => HideCompanyDetails();
            }
        }

        private void HideCompanyDetails()
        {
            if (this.FindControl<Border>("CompanyDetailsOverlay") is Border b) b.IsVisible = false;
        }

        private void CompanyBuildingsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_selectedCorporation == null) return;
            var list = this.FindControl<ListBox>("CompanyBuildingsList");
            var details = this.FindControl<StackPanel>("BuildingDetailsPanel");
            if (list == null || details == null) return;
            if (list.SelectedIndex < 0 || list.SelectedIndex >= _selectedCorporation.OwnedFactories.Count) return;
            var factory = _selectedCorporation.OwnedFactories[list.SelectedIndex];
            details.Children.Clear();
            details.Children.Add(new TextBlock { Text = factory.Name, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#FFD700")) });
            double revenue = factory.ProductionCapacity * 1000.0;
            double costs = factory.ProductionCapacity * 500.0;
            double profit = revenue - costs;
            details.Children.Add(new TextBlock { Text = $"Revenue: ${FormatCurrency(revenue)}" });
            details.Children.Add(new TextBlock { Text = $"Costs: ${FormatCurrency(costs)}" });
            details.Children.Add(new TextBlock { Text = $"Profit: ${FormatCurrency(profit)}", Foreground = new SolidColorBrush(Color.Parse(profit >= 0 ? "#90EE90" : "#F08080")) });
        }

        private string FormatCurrency(double amount)
        {
            if (amount >= 1_000_000_000) return $"{amount / 1_000_000_000d:F1}B";
            if (amount >= 1_000_000) return $"{amount / 1_000_000d:F1}M";
            if (amount >= 1_000) return $"{amount / 1_000d:F1}K";
            return amount.ToString("F0");
        }
        // ================================================

        // ===== Added missing handler implementations (map & menu) =====
        private void OnDebugClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Debug button clicked - showing debug menu");
            ShowPopup("DebugMenuOverlay");
        }
        private void OnMenuClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Menu button clicked - returning to main menu");
            _continuousRenderTimer?.Stop();
            var main = new MainWindow();
            main.Show();
            Close();
        }
        private void OnTerrainViewClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Terrain view button clicked");
            _mapManager.SetViewType(MapViewType.Terrain);
            QueueRender(immediate: true);
        }
        private void OnPoliticalViewClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Political view button clicked");
            _mapManager.SetViewType(MapViewType.Political);
            ShowCountryDetectionInstructions();
            QueueRender(immediate: true);
        }
        private void OnPopulationDensityViewClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Population density view button clicked");
            _mapManager.SetViewType(MapViewType.PopulationDensity);
            QueueRender(immediate: true);
        }
        private void OnStatesViewClicked(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("States view button clicked");
            _mapManager.SetViewType(MapViewType.States);
            QueueRender(immediate: true);
        }
        private async void OnCullEmptyStatesClicked(object? sender, RoutedEventArgs e)
        {
            if (_isCullingStates) { Debug.WriteLine("[DEBUG MENU] Cull already running"); return; }
            if (_mapManager == null) return;
            var cullButton = this.FindControl<Button>("CullEmptyStatesButton");
            var progressPanel = this.FindControl<StackPanel>("CullProgressPanel");
            var progressBar = this.FindControl<ProgressBar>("CullProgressBar");
            var progressLabel = this.FindControl<TextBlock>("CullProgressLabel");
            try
            {
                _isCullingStates = true;
                cullButton?.Let(b => b.IsEnabled = false);
                progressPanel?.Let(p => p.IsVisible = true);
                progressLabel?.Let(l => l.Text = "Culling states… 0%");
                var progress = new Progress<double>(v => Dispatcher.UIThread.Post(() =>
                {
                    if (progressBar != null) progressBar.Value = v * 100;
                    if (progressLabel != null) progressLabel.Text = $"Culling states… {v * 100:F0}%";
                }));
                int culled = await _mapManager.CullStatesWithoutCitiesAsync(progress);
                Debug.WriteLine($"[DEBUG MENU] Culled {culled} empty states");
                QueueRender(immediate: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG MENU] Cull error: {ex.Message}");
            }
            finally
            {
                _isCullingStates = false;
                if (cullButton != null) cullButton.IsEnabled = true;
                if (progressPanel != null) progressPanel.IsVisible = false;
            }
        }
        private void OnMapViewTypeChanged(object? sender, MapViewType type)
        {
            Dispatcher.UIThread.Post(UpdateMapViewButtons);
            QueueRender(immediate: true);
        }
        private void UpdateMapViewButtons()
        {
            if (this.FindControl<Button>("TerrainViewButton") is Button terrainBtn)
                terrainBtn.Background = _mapManager.CurrentViewType == MapViewType.Terrain ? Brushes.DarkBlue : Brushes.DarkSlateGray;
            if (this.FindControl<Button>("PoliticalViewButton") is Button polBtn)
                polBtn.Background = _mapManager.CurrentViewType == MapViewType.Political ? Brushes.DarkRed : Brushes.DarkSlateGray;
            if (this.FindControl<Button>("PlaceHolder1Button") is Button popBtn)
                popBtn.Background = _mapManager.CurrentViewType == MapViewType.PopulationDensity ? Brushes.DarkGreen : Brushes.DarkSlateGray;
            if (this.FindControl<Button>("StatesViewButton") is Button statesBtn)
                statesBtn.Background = _mapManager.CurrentViewType == MapViewType.States ? Brushes.DarkOrange : Brushes.DarkSlateGray;
        }
        private void CenterView()
        {
            var size = GetEffectiveRenderSize();
            var mapSize = _mapManager.GetMapSize(_currentZoomLevel);
            _viewOffset = new SKPointI(Math.Max(0, (mapSize.Width - (int)size.Width) / 2), Math.Max(0, (mapSize.Height - (int)size.Height) / 2));
        }
        // ===== End added handlers =====

        // Ensure all regions closed
        #endregion
    }

    internal static class ControlExtensions
    {
        public static void Let<T>(this T? obj, Action<T> act) where T : class { if (obj != null) act(obj); }
    }
}