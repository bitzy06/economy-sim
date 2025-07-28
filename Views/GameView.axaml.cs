using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using StrategyGame; // Assuming HybridMapManager is in this namespace
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Economy_sim
{
    public partial class GameView : Window
    {
        private readonly HybridMapManager _mapManager;

        // --- Optimized Rendering Fields ---
        private WriteableBitmap _writeableBitmap; // Use a WriteableBitmap for high-performance updates.
        private int _currentZoomLevel = 1; // Start at the lowest zoom level so user doesn't have to zoom out
        private SKPointI _viewOffset = SKPointI.Empty;
        private bool _isPanning = false;
        private Point _panStartPoint;
        private bool _isInitialized = false;

        private readonly DispatcherTimer _mapUpdateTimer;
        private DispatcherTimer _initialRenderTimer; // Timer to poll for initial size.
        private bool _pendingMapUpdate = false;
        private readonly object _renderLock = new object();
        private bool _renderInProgress = false;

        // Country selection functionality
        private Country? _selectedCountry;
        private Point _lastMousePosition;
        
        // Click vs drag detection
        private Point _mouseDownPoint;
        private bool _hasMouseMoved;
        private const double ClickTolerance = 5.0; // Pixels


        public GameView()
        {
            InitializeComponent();
            _mapManager = new HybridMapManager(baseWidth: 4096, baseHeight: 2048);
            this.Loaded += OnWindowLoaded;
            this.SizeChanged += OnSizeChanged;

            _mapUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // Render at ~60fps during pan
            };
            _mapUpdateTimer.Tick += MapUpdateTimer_Tick;
            _mapUpdateTimer.Start();

            // Initialize HUD after component initialization
            InitializeHUD();

            // Initialize country selection functionality
            InitializeCountrySelection();

            // Subscribe to map manager events
            _mapManager.ViewTypeChanged += OnMapViewTypeChanged;
            UpdateMapViewButtons();
            
            // Run basic integration test for political borders (commented out for production)
            // Economy_sim.Testing.PoliticalBorderIntegrationTest.RunBasicTests();
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
                
                // Center the view to ensure both map types start at the same position
                CenterView();
                
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
                var currentPoint = e.GetPosition(this.MapImage);
                
                Debug.WriteLine($"OnPointerPressed: Mouse click at ({currentPoint.X:F0}, {currentPoint.Y:F0})");
                Debug.WriteLine($"OnPointerPressed: Current view type: {_mapManager.CurrentViewType}");
                
                // Store the initial click point for click vs drag detection
                _mouseDownPoint = currentPoint;
                _hasMouseMoved = false;
                
                // Don't handle country selection here - wait for OnPointerReleased to distinguish click from drag
                // This prevents triggering map re-rendering during potential drag operations
                
                _isPanning = true;
                _panStartPoint = currentPoint;
                this.Cursor = new Cursor(StandardCursorType.Hand);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var currentPoint = e.GetPosition(this.MapImage);
            _lastMousePosition = currentPoint;

            // Track if the mouse has moved significantly from the initial click point
            if (!_hasMouseMoved)
            {
                var distance = Math.Sqrt(Math.Pow(currentPoint.X - _mouseDownPoint.X, 2) + Math.Pow(currentPoint.Y - _mouseDownPoint.Y, 2));
                if (distance > ClickTolerance)
                {
                    _hasMouseMoved = true;
                    Debug.WriteLine($"OnPointerMoved: Mouse movement detected ({distance:F1} pixels) - this is a drag operation");
                }
            }

            if (_isPanning && _hasMouseMoved)
            {
                var delta = _panStartPoint - currentPoint;
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
                Debug.WriteLine($"OnPointerReleased: _hasMouseMoved: {_hasMouseMoved}, View type: {_mapManager.CurrentViewType}");
                
                // Only handle country selection if this was a click (not a drag) and we're in political view
                if (!_hasMouseMoved && _mapManager.CurrentViewType == MapViewType.Political)
                {
                    Debug.WriteLine("OnPointerReleased: Handling country click (pure click, no drag)");
                    HandleCountryClick(_mouseDownPoint);
                }
                else if (_hasMouseMoved)
                {
                    Debug.WriteLine("OnPointerReleased: Was a drag operation - no country selection");
                }
                else
                {
                    Debug.WriteLine($"OnPointerReleased: Not in political view - current view: {_mapManager.CurrentViewType}");
                }
                
                _isPanning = false;
                _hasMouseMoved = false;
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

        #region HUD Management

        // Sample game state for HUD demonstration
        private PlayerRoleManager _playerRoleManager;
        private Country _currentCountry;

        private void InitializeHUD()
        {
            // Initialize sample game state for demonstration
            _playerRoleManager = new PlayerRoleManager();
            _currentCountry = new Country("United States");

            // Add some sample states and cities for demonstration
            var california = new State("California");
            california.Cities.Add(new City("Los Angeles"));
            california.Cities.Add(new City("San Francisco"));

            var texas = new State("Texas");
            texas.Cities.Add(new City("Houston"));
            texas.Cities.Add(new City("Dallas"));

            _currentCountry.States.Add(california);
            _currentCountry.States.Add(texas);

            // Set up player as Prime Minister by default
            _playerRoleManager.AssumeRolePrimeMinister(_currentCountry);

            // Set up HUD update timer
            var hudTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Update every second
            };
            hudTimer.Tick += UpdateHUDDisplay;
            hudTimer.Start();

            // Initialize HUD button event handlers
            SetupHUDEventHandlers();

            // Initial HUD update
            UpdateHUDDisplay(null, null);
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

            // Map view toggle buttons
            if (this.FindControl<Button>("TerrainViewButton") is Button terrainBtn)
                terrainBtn.Click += OnTerrainViewClicked;

            if (this.FindControl<Button>("PoliticalViewButton") is Button politicalBtn)
                politicalBtn.Click += OnPoliticalViewClicked;

            if (this.FindControl<Button>("MenuButton") is Button menuBtn)
                menuBtn.Click += OnMenuClicked;

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

            if (this.FindControl<Border>("CountryInfoMenuOverlay") is Border countryInfoOverlay)
                countryInfoOverlay.PointerPressed += OnOverlayClicked;

            // Setup popup menu content
            InitializePopupMenus();
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

        #region Country Selection Management

        private void InitializeCountrySelection()
        {
            // Setup close button handler for country info menu
            if (this.FindControl<Button>("CountryInfoCloseButton") is Button closeBtn)
                closeBtn.Click += (s, e) => HideAllPopups();
        }

        private void HandleCountryClick(Point mousePosition)
        {
            try
            {
                Debug.WriteLine($"HandleCountryClick: Mouse position ({mousePosition.X:F0}, {mousePosition.Y:F0})");
                Debug.WriteLine($"HandleCountryClick: Zoom level {_currentZoomLevel}, View offset ({_viewOffset.X}, {_viewOffset.Y})");
                Debug.WriteLine($"HandleCountryClick: Current view type {_mapManager.CurrentViewType}");
                
                // Debug: Print effective render size and map image bounds
                var effectiveSize = GetEffectiveRenderSize();
                Debug.WriteLine($"HandleCountryClick: Effective render size: {effectiveSize.Width}x{effectiveSize.Height}");
                if (this.MapImage != null)
                {
                    Debug.WriteLine($"HandleCountryClick: MapImage bounds: {this.MapImage.Bounds}");
                }
                
                // Get country at clicked position
                var country = _mapManager.GetCountryAtScreenCoordinate(
                    (int)mousePosition.X, 
                    (int)mousePosition.Y, 
                    _currentZoomLevel, 
                    _viewOffset);

                Debug.WriteLine($"HandleCountryClick: Found country: {country?.Name ?? "null"}");

                if (country != null)
                {
                    _selectedCountry = country;
                    
                    Debug.WriteLine($"HandleCountryClick: Setting highlighted country to {country.Name}");
                    
                    // Set highlighting for visual feedback
                    _mapManager.SetHighlightedCountry(country);
                    
                    // Show selection indicator on map
                    ShowCountrySelectionIndicator(country.Name, mousePosition);
                    
                    ShowCountryInfoMenu(country);
                    
                    // Refresh the map to show highlighting
                    QueueRender();
                }
                else
                {
                    Debug.WriteLine("HandleCountryClick: No country found at clicked position");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in country click detection: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void ShowCountryInfoMenu(Country country)
        {
            // Update country information title with selection indicator
            if (this.FindControl<TextBlock>("CountryInfoTitle") is TextBlock titleText)
            {
                titleText.Text = $"🗺️ {country.Name} (Selected)";
            }

            // Update country overview
            if (this.FindControl<TextBlock>("CountryPopulationText") is TextBlock popText)
            {
                popText.Text = $"{country.Population:N0}";
            }

            if (this.FindControl<TextBlock>("CountryPopulationGrowth") is TextBlock growthText)
            {
                growthText.Text = "Growth: +1.2%"; // Placeholder
            }

            if (this.FindControl<TextBlock>("CountryBudgetText") is TextBlock budgetText)
            {
                budgetText.Text = $"${country.Budget:N0}";
            }

            if (this.FindControl<TextBlock>("CountryRelationsText") is TextBlock relationsText)
            {
                relationsText.Text = "Neutral"; // Placeholder
            }

            // Update resources list
            if (this.FindControl<ListBox>("CountryResourcesList") is ListBox resourcesList)
            {
                resourcesList.Items.Clear();
                if (country.Resources.Any())
                {
                    foreach (var resource in country.Resources)
                    {
                        resourcesList.Items.Add($"🏭 {resource.Key}: {resource.Value:N0}");
                    }
                }
                else
                {
                    resourcesList.Items.Add("No resource data available");
                }
            }

            // Update additional information
            if (this.FindControl<TextBlock>("CountryCapitalText") is TextBlock capitalText)
            {
                capitalText.Text = $"Capital: {GetCountryCapital(country.Name)}";
            }

            if (this.FindControl<TextBlock>("CountryAreaText") is TextBlock areaText)
            {
                areaText.Text = $"Area: {GetCountryArea(country.Name)}";
            }

            if (this.FindControl<TextBlock>("CountryLanguageText") is TextBlock languageText)
            {
                languageText.Text = $"Language: {GetCountryLanguage(country.Name)}";
            }

            if (this.FindControl<TextBlock>("CountryGovernmentText") is TextBlock govText)
            {
                govText.Text = $"Government: {GetCountryGovernment(country.Name)}";
            }

            if (this.FindControl<TextBlock>("CountryFoundedText") is TextBlock foundedText)
            {
                foundedText.Text = $"Founded: {GetCountryFounded(country.Name)}";
            }

            // Show the country info menu
            ShowPopup("CountryInfoMenuOverlay");
        }

        // Helper methods for country information
        private string GetCountryCapital(string countryName)
        {
            return countryName switch
            {
                "United States" => "Washington, D.C.",
                "Canada" => "Ottawa",
                "Mexico" => "Mexico City",
                "United Kingdom" => "London",
                _ => "Unknown"
            };
        }

        private string GetCountryArea(string countryName)
        {
            return countryName switch
            {
                "United States" => "9.8 million km²",
                "Canada" => "10.0 million km²",
                "Mexico" => "2.0 million km²",
                "United Kingdom" => "243,610 km²",
                _ => "Unknown"
            };
        }

        private string GetCountryLanguage(string countryName)
        {
            return countryName switch
            {
                "United States" => "English",
                "Canada" => "English, French",
                "Mexico" => "Spanish",
                "United Kingdom" => "English",
                _ => "Unknown"
            };
        }

        private string GetCountryGovernment(string countryName)
        {
            return countryName switch
            {
                "United States" => "Federal Republic",
                "Canada" => "Federal Parliamentary Democracy",
                "Mexico" => "Federal Republic",
                "United Kingdom" => "Constitutional Monarchy",
                _ => "Unknown"
            };
        }

        private void ShowCountrySelectionIndicator(string countryName, Point position)
        {
            if (this.FindControl<Border>("CountrySelectionIndicator") is Border indicator &&
                this.FindControl<TextBlock>("CountrySelectionText") is TextBlock text)
            {
                text.Text = $"{countryName} Selected";
                
                // Position the indicator near the click point but offset to avoid overlap
                Canvas.SetLeft(indicator, position.X + 10);
                Canvas.SetTop(indicator, position.Y - 30);
                
                indicator.IsVisible = true;
            }
        }

        private void HideCountrySelectionIndicator()
        {
            if (this.FindControl<Border>("CountrySelectionIndicator") is Border indicator)
            {
                indicator.IsVisible = false;
            }
        }

        private string GetCountryFounded(string countryName)
        {
            return countryName switch
            {
                "United States" => "1776",
                "Canada" => "1867",
                "Mexico" => "1810",
                "United Kingdom" => "1707",
                _ => "Unknown"
            };
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

            if (this.FindControl<Border>("CountryInfoMenuOverlay") is Border countryOverlay)
            {
                countryOverlay.IsVisible = false;
                
                // Clear country highlighting and selection indicator when menu is closed
                _mapManager.SetHighlightedCountry(null);
                HideCountrySelectionIndicator();
                QueueRender();
            }
        }

        private void ShowPopup(string popupName)
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

            // Initialize Trade menu content
            if (this.FindControl<ListBox>("ExportsList") is ListBox exportsList)
            {
                var exports = new[]
                {
                    "💼 Manufactured Goods → UK ($2.5B)",
                    "🌾 Agricultural Products → Japan ($1.8B)",
                    "⚙️ Technology → Germany ($3.2B)",
                    "🛢️ Oil Products → Various ($4.1B)"
                };
                foreach (var export in exports)
                {
                    exportsList.Items.Add(export);
                }
            }

            if (this.FindControl<ListBox>("ImportsList") is ListBox importsList)
            {
                var imports = new[]
                {
                    "📱 Electronics ← China ($2.8B)",
                    "☕ Coffee ← Brazil ($0.9B)",
                    "💎 Rare Metals ← Africa ($1.5B)",
                    "🏭 Machinery ← Germany ($2.2B)"
                };
                foreach (var import in imports)
                {
                    importsList.Items.Add(import);
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
        }

        #endregion

        #region HUD Event Handlers

        private void OnDiplomacyClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Diplomacy button clicked - showing diplomacy menu");
            ShowPopup("DiplomacyMenuOverlay");
        }

        private void OnTradeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Trade button clicked - showing trade menu");
            ShowPopup("TradeMenuOverlay");
        }

        private void OnConstructionClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Construction button clicked - showing construction menu");
            ShowPopup("ConstructionMenuOverlay");
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
            Debug.WriteLine("Economy view button clicked - showing economy menu");
            ShowPopup("EconomyMenuOverlay");
        }

        private void OnStatsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Stats button clicked - showing statistics menu");
            ShowPopup("StatsMenuOverlay");
        }

        private void OnMenuClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Menu button clicked - returning to main menu");

            // Create and show the main menu window
            var mainWindow = new MainWindow();
            mainWindow.Show();

            // Close the current game window
            this.Close();
        }

        private void OnTerrainViewClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Terrain view button clicked");
            _mapManager.SetViewType(MapViewType.Terrain);
            QueueRender();
        }

        private void OnPoliticalViewClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Debug.WriteLine("Political view button clicked");
            _mapManager.SetViewType(MapViewType.Political);
            QueueRender();
        }

        private void OnMapViewTypeChanged(object? sender, MapViewType viewType)
        {
            Debug.WriteLine($"Map view type changed to: {viewType}");
            
            // Hide country info menu, selection indicator, and clear highlighting when switching away from political view
            if (viewType != MapViewType.Political)
            {
                if (this.FindControl<Border>("CountryInfoMenuOverlay") is Border countryOverlay)
                    countryOverlay.IsVisible = false;
                
                HideCountrySelectionIndicator();
                _mapManager.SetHighlightedCountry(null);
            }
            
            // Do not recenter view when switching map types - maintain current position
            // CenterView(); // Removed to prevent annoying recentering
            
            Dispatcher.UIThread.Post(UpdateMapViewButtons);
            Dispatcher.UIThread.Post(QueueRender);
        }
        
        private void CenterView()
        {
            if (_mapManager == null) return;
            
            var effectiveSize = GetEffectiveRenderSize();
            if (effectiveSize.Width < 1 || effectiveSize.Height < 1) return;
            
            var mapSize = _mapManager.GetMapSize(_currentZoomLevel);
            
            // Center the view on the map
            _viewOffset.X = Math.Max(0, (mapSize.Width - (int)effectiveSize.Width) / 2);
            _viewOffset.Y = Math.Max(0, (mapSize.Height - (int)effectiveSize.Height) / 2);
            
            Debug.WriteLine($"Centered view at offset: {_viewOffset}, Map size: {mapSize}, View size: {effectiveSize}");
        }

        private void UpdateMapViewButtons()
        {
            if (this.FindControl<Button>("TerrainViewButton") is Button terrainBtn)
            {
                terrainBtn.Background = _mapManager.CurrentViewType == MapViewType.Terrain 
                    ? Avalonia.Media.Brushes.DarkBlue 
                    : Avalonia.Media.Brushes.DarkSlateGray;
            }

            if (this.FindControl<Button>("PoliticalViewButton") is Button politicalBtn)
            {
                politicalBtn.Background = _mapManager.CurrentViewType == MapViewType.Political 
                    ? Avalonia.Media.Brushes.DarkRed 
                    : Avalonia.Media.Brushes.DarkSlateGray;
            }
        }

        #endregion
    }
}