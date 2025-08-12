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
            var currentPoint = e.GetCurrentPoint(this);
            
            if (currentPoint.Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this.MapImage);
                this.Cursor = new Cursor(StandardCursorType.Hand);
            }
            else if (currentPoint.Properties.IsRightButtonPressed)
            {
                // Right-click for country detection
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

        #region Country Detection

        /// <summary>
        /// Detects which country is at the specified screen position
        /// </summary>
        private void DetectCountryAtPosition(int screenX, int screenY)
        {
            try
            {
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
            }
        }
        
        /// <summary>
        /// Shows visual feedback for country detection (placeholder implementation)
        /// </summary>
        private void ShowCountryDetectionFeedback(IndexedCountryFeature? country, int screenX, int screenY)
        {
            // For now, just update a text display or create a simple notification
            // In a full implementation, this could:
            // 1. Highlight the country borders
            // 2. Show a tooltip near the mouse cursor
            // 3. Update a country information panel
            // 4. Play a sound effect
            
            string message = country != null 
                ? $"Selected: {country.CountryName}" 
                : "No country selected";
                
            // Update the HUD or show temporary feedback
            Dispatcher.UIThread.Post(() =>
            {
                // You could update a label in the UI here
                Debug.WriteLine($"[UI FEEDBACK] {message}");
                
                // Example: Update window title to show selected country (temporary solution)
                this.Title = country != null 
                    ? $"Economy Sim - {country.CountryName} ({country.CountryCode})"
                    : "Economy Sim";
            });
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