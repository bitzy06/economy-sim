using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using StrategyGame;
using economy_sim;

namespace economy_sim
{    public partial class TradeManagementForm : Form
    {
        private StrategyGame.Country playerCountry;
        private List<StrategyGame.Country> allCountries;
        private List<StrategyGame.City> allCities;
        private TradeRouteManager routeManager;
        private EnhancedTradeManager tradeManager;
        private StrategyGame.GlobalMarket globalMarket;
        private System.Windows.Forms.Timer refreshTimer;
        private Button buttonRefresh;
        
        private TabControl tabControlTrade;
        private TabPage tabPageTradeRoutes;
        private TabPage tabPageTradeAgreements;
        private TabPage tabPageGlobalMarket;
        
        // Trade Routes controls
        private ListView listViewTradeRoutes;
        private Button buttonCreateRoute;
        private Button buttonUpgradeRoute;
        private Button buttonDeleteRoute;
        private ComboBox comboBoxRouteType;
        
        // Trade Agreements controls
        private ListView listViewTradeAgreements;
        private Button buttonNewAgreement;
        private Button buttonRenewAgreement;
        private Button buttonCancelAgreement;
        
        // Global Market controls
        private ListView listViewGlobalPrices;
        private ListView listViewTradingPartners;
        private Label labelGlobalTradeValue;
        private ComboBox comboBoxCommodityFilter;
        
        // Trade Route creation panel
        private Panel panelCreateRoute;
        private ComboBox comboBoxStartCity;
        private ComboBox comboBoxEndCity;
        private ComboBox comboBoxNewRouteType;
        private Button buttonConfirmNewRoute;
        private Button buttonCancelNewRoute;
        
        public TradeManagementForm(StrategyGame.Country playerCountry, List<StrategyGame.Country> allCountries, List<StrategyGame.City> allCities, 
                                 TradeRouteManager routeManager, EnhancedTradeManager tradeManager, 
                                 StrategyGame.GlobalMarket globalMarket)        {
            this.playerCountry = playerCountry;
            this.allCountries = allCountries;
            this.allCities = allCities;
            this.routeManager = routeManager;
            this.tradeManager = tradeManager;
            this.globalMarket = globalMarket;
            
            InitializeComponent();

            // Initialize refresh button
            buttonRefresh = new Button();
            buttonRefresh.Text = "🔄 Refresh";
            buttonRefresh.Location = new Point(10, 530);
            buttonRefresh.Size = new Size(100, 30);
            buttonRefresh.Click += ButtonRefresh_Click;
            this.Controls.Add(buttonRefresh);

            // Initialize refresh timer
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 5000; // 5 seconds
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            PopulateTradeRoutes();
            PopulateTradeAgreements();
            PopulateGlobalMarket();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Trade Management";
            this.Size = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Tab Control
            tabControlTrade = new TabControl();
            tabControlTrade.Dock = DockStyle.Fill;
            
            // Trade Routes Tab
            tabPageTradeRoutes = new TabPage("Trade Routes");
            InitializeTradeRoutesTab();
            tabControlTrade.TabPages.Add(tabPageTradeRoutes);
            
            // Trade Agreements Tab
            tabPageTradeAgreements = new TabPage("Trade Agreements");
            InitializeTradeAgreementsTab();
            tabControlTrade.TabPages.Add(tabPageTradeAgreements);
            
            // Global Market Tab
            tabPageGlobalMarket = new TabPage("Global Market");
            InitializeGlobalMarketTab();
            tabControlTrade.TabPages.Add(tabPageGlobalMarket);
            
            this.Controls.Add(tabControlTrade);
            
            // Initialize route creation panel (initially hidden)
            InitializeRouteCreationPanel();
            tabPageTradeRoutes.Controls.Add(panelCreateRoute);
            panelCreateRoute.Visible = false;
        }
        
        private void InitializeTradeRoutesTab()
        {
            // ListView for trade routes
            listViewTradeRoutes = new ListView();
            listViewTradeRoutes.View = View.Details;
            listViewTradeRoutes.FullRowSelect = true;
            listViewTradeRoutes.GridLines = true;
            listViewTradeRoutes.Location = new Point(10, 10);
            listViewTradeRoutes.Size = new Size(765, 400);
            
            listViewTradeRoutes.Columns.Add("Route Name", 150);
            listViewTradeRoutes.Columns.Add("Type", 80);
            listViewTradeRoutes.Columns.Add("From", 100);
            listViewTradeRoutes.Columns.Add("To", 100);
            listViewTradeRoutes.Columns.Add("Distance", 70);
            listViewTradeRoutes.Columns.Add("Capacity", 70);
            listViewTradeRoutes.Columns.Add("Usage", 70);
            listViewTradeRoutes.Columns.Add("Status", 80);
            
            // Route type filter
            Label labelRouteFilter = new Label();
            labelRouteFilter.Text = "Filter by Type:";
            labelRouteFilter.Location = new Point(10, 420);
            labelRouteFilter.Size = new Size(100, 20);
            
            comboBoxRouteType = new ComboBox();
            comboBoxRouteType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRouteType.Location = new Point(110, 420);
            comboBoxRouteType.Size = new Size(120, 20);
            comboBoxRouteType.Items.AddRange(new object[] { "All", "Land", "Sea", "River", "Air" });
            comboBoxRouteType.SelectedIndex = 0;
            comboBoxRouteType.SelectedIndexChanged += ComboBoxRouteType_SelectedIndexChanged;
            
            // Buttons
            buttonCreateRoute = new Button();
            buttonCreateRoute.Text = "Create New Route";
            buttonCreateRoute.Location = new Point(10, 460);
            buttonCreateRoute.Size = new Size(120, 30);
            buttonCreateRoute.Click += ButtonCreateRoute_Click;
            
            buttonUpgradeRoute = new Button();
            buttonUpgradeRoute.Text = "Upgrade Route";
            buttonUpgradeRoute.Location = new Point(140, 460);
            buttonUpgradeRoute.Size = new Size(120, 30);
            buttonUpgradeRoute.Click += ButtonUpgradeRoute_Click;
            
            buttonDeleteRoute = new Button();
            buttonDeleteRoute.Text = "Delete Route";
            buttonDeleteRoute.Location = new Point(270, 460);
            buttonDeleteRoute.Size = new Size(120, 30);
            buttonDeleteRoute.Click += ButtonDeleteRoute_Click;
            
            // Add controls to tab page
            tabPageTradeRoutes.Controls.Add(listViewTradeRoutes);
            tabPageTradeRoutes.Controls.Add(labelRouteFilter);
            tabPageTradeRoutes.Controls.Add(comboBoxRouteType);
            tabPageTradeRoutes.Controls.Add(buttonCreateRoute);
            tabPageTradeRoutes.Controls.Add(buttonUpgradeRoute);
            tabPageTradeRoutes.Controls.Add(buttonDeleteRoute);
        }
        
        private void InitializeTradeAgreementsTab()
        {
            // ListView for trade agreements
            listViewTradeAgreements = new ListView();
            listViewTradeAgreements.View = View.Details;
            listViewTradeAgreements.FullRowSelect = true;
            listViewTradeAgreements.GridLines = true;
            listViewTradeAgreements.Location = new Point(10, 10);
            listViewTradeAgreements.Size = new Size(765, 400);
            
            listViewTradeAgreements.Columns.Add("Agreement", 150);
            listViewTradeAgreements.Columns.Add("Type", 80);
            listViewTradeAgreements.Columns.Add("With", 100);
            listViewTradeAgreements.Columns.Add("Goods", 120);
            listViewTradeAgreements.Columns.Add("Tariff Rate", 80);
            listViewTradeAgreements.Columns.Add("Duration", 70);
            listViewTradeAgreements.Columns.Add("Status", 80);
            
            // Buttons for trade agreements
            buttonNewAgreement = new Button();
            buttonNewAgreement.Text = "New Agreement";
            buttonNewAgreement.Location = new Point(10, 430);
            buttonNewAgreement.Size = new Size(120, 30);
            buttonNewAgreement.Click += ButtonNewAgreement_Click;
            
            buttonRenewAgreement = new Button();
            buttonRenewAgreement.Text = "Renew Agreement";
            buttonRenewAgreement.Location = new Point(140, 430);
            buttonRenewAgreement.Size = new Size(120, 30);
            buttonRenewAgreement.Click += ButtonRenewAgreement_Click;
            
            buttonCancelAgreement = new Button();
            buttonCancelAgreement.Text = "Cancel Agreement";
            buttonCancelAgreement.Location = new Point(270, 430);
            buttonCancelAgreement.Size = new Size(120, 30);
            buttonCancelAgreement.Click += ButtonCancelAgreement_Click;
            
            // Add controls to tab page
            tabPageTradeAgreements.Controls.Add(listViewTradeAgreements);
            tabPageTradeAgreements.Controls.Add(buttonNewAgreement);
            tabPageTradeAgreements.Controls.Add(buttonRenewAgreement);
            tabPageTradeAgreements.Controls.Add(buttonCancelAgreement);
        }
        
        private void InitializeGlobalMarketTab()
        {
            // Global trade statistics            // Player country status header
            Label playerCountryHeader = new Label();
            playerCountryHeader.Text = $"Managing Trade for: {(playerCountry != null ? playerCountry.Name : "N/A")}";
            playerCountryHeader.Location = new Point(10, 10);
            playerCountryHeader.Size = new Size(400, 20);
            playerCountryHeader.Font = new Font(playerCountryHeader.Font.FontFamily, 12, FontStyle.Bold);
            playerCountryHeader.ForeColor = Color.DarkGreen;
            
            // Global trade value
            labelGlobalTradeValue = new Label();
            labelGlobalTradeValue.Text = "Global Trade Value: $0";
            labelGlobalTradeValue.Location = new Point(410, 10);
            labelGlobalTradeValue.Size = new Size(300, 20);
            labelGlobalTradeValue.Font = new Font(labelGlobalTradeValue.Font, FontStyle.Bold);
            
            // Commodity filter
            Label labelCommodityFilter = new Label();
            labelCommodityFilter.Text = "Select Commodity:";
            labelCommodityFilter.Location = new Point(10, 40);
            labelCommodityFilter.Size = new Size(120, 20);
            
            comboBoxCommodityFilter = new ComboBox();
            comboBoxCommodityFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCommodityFilter.Location = new Point(130, 40);
            comboBoxCommodityFilter.Size = new Size(150, 20);
            comboBoxCommodityFilter.SelectedIndexChanged += ComboBoxCommodityFilter_SelectedIndexChanged;
            
            // Global price trends
            Label labelPriceHeader = new Label();
            labelPriceHeader.Text = "Global Price Index:";
            labelPriceHeader.Location = new Point(10, 70);
            labelPriceHeader.Size = new Size(150, 20);
            
            listViewGlobalPrices = new ListView();
            listViewGlobalPrices.View = View.Details;
            listViewGlobalPrices.FullRowSelect = true;
            listViewGlobalPrices.GridLines = true;
            listViewGlobalPrices.Location = new Point(10, 100);
            listViewGlobalPrices.Size = new Size(380, 400);
            
            listViewGlobalPrices.Columns.Add("Commodity", 120);
            listViewGlobalPrices.Columns.Add("Price", 80);
            listViewGlobalPrices.Columns.Add("Change", 80);
            listViewGlobalPrices.Columns.Add("Volatility", 80);
            
            // Trading partners
            Label labelPartnersHeader = new Label();
            labelPartnersHeader.Text = "Top Trading Partners:";
            labelPartnersHeader.Location = new Point(400, 70);
            labelPartnersHeader.Size = new Size(150, 20);
            
            listViewTradingPartners = new ListView();
            listViewTradingPartners.View = View.Details;
            listViewTradingPartners.FullRowSelect = true;
            listViewTradingPartners.GridLines = true;
            listViewTradingPartners.Location = new Point(400, 100);
            listViewTradingPartners.Size = new Size(370, 400);
            
            listViewTradingPartners.Columns.Add("Country", 120);
            listViewTradingPartners.Columns.Add("Export Volume", 100);
            listViewTradingPartners.Columns.Add("Import Volume", 100);
            
            // Add controls to tab page
            tabPageGlobalMarket.Controls.Add(labelGlobalTradeValue);
            tabPageGlobalMarket.Controls.Add(labelCommodityFilter);
            tabPageGlobalMarket.Controls.Add(comboBoxCommodityFilter);
            tabPageGlobalMarket.Controls.Add(labelPriceHeader);
            tabPageGlobalMarket.Controls.Add(listViewGlobalPrices);
            tabPageGlobalMarket.Controls.Add(labelPartnersHeader);
            tabPageGlobalMarket.Controls.Add(listViewTradingPartners);
            
            // Populate commodity filter
            if (globalMarket != null && globalMarket.GlobalPrices != null)
            {
                comboBoxCommodityFilter.Items.Add("All Commodities");
                foreach (var goodName in globalMarket.GlobalPrices.Keys)
                {
                    comboBoxCommodityFilter.Items.Add(goodName);
                }
                comboBoxCommodityFilter.SelectedIndex = 0;
            }
        }
        
        private void InitializeRouteCreationPanel()
        {
            panelCreateRoute = new Panel();
            panelCreateRoute.BorderStyle = BorderStyle.FixedSingle;
            panelCreateRoute.BackColor = Color.AntiqueWhite;
            panelCreateRoute.Location = new Point(200, 100);
            panelCreateRoute.Size = new Size(400, 300);
            
            Label labelPanelTitle = new Label();
            labelPanelTitle.Text = "Create New Trade Route";
            labelPanelTitle.Location = new Point(10, 10);
            labelPanelTitle.Size = new Size(380, 25);
            labelPanelTitle.TextAlign = ContentAlignment.MiddleCenter;
            labelPanelTitle.Font = new Font(labelPanelTitle.Font, FontStyle.Bold);
            
            Label labelStartCity = new Label();
            labelStartCity.Text = "Start City:";
            labelStartCity.Location = new Point(10, 50);
            labelStartCity.Size = new Size(100, 20);
            
            comboBoxStartCity = new ComboBox();
            comboBoxStartCity.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStartCity.Location = new Point(120, 50);
            comboBoxStartCity.Size = new Size(250, 20);
            
            Label labelEndCity = new Label();
            labelEndCity.Text = "End City:";
            labelEndCity.Location = new Point(10, 90);
            labelEndCity.Size = new Size(100, 20);
            
            comboBoxEndCity = new ComboBox();
            comboBoxEndCity.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxEndCity.Location = new Point(120, 90);
            comboBoxEndCity.Size = new Size(250, 20);
            
            Label labelRouteType = new Label();
            labelRouteType.Text = "Route Type:";
            labelRouteType.Location = new Point(10, 130);
            labelRouteType.Size = new Size(100, 20);
            
            comboBoxNewRouteType = new ComboBox();
            comboBoxNewRouteType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxNewRouteType.Location = new Point(120, 130);
            comboBoxNewRouteType.Size = new Size(250, 20);
            comboBoxNewRouteType.Items.AddRange(new object[] { "Land", "Sea", "River", "Air" });
            comboBoxNewRouteType.SelectedIndex = 0;
            
            Label labelInfo = new Label();
            labelInfo.Text = "Note: Route costs will vary based on type and distance.";
            labelInfo.Location = new Point(10, 170);
            labelInfo.Size = new Size(380, 40);
            
            buttonConfirmNewRoute = new Button();
            buttonConfirmNewRoute.Text = "Create";
            buttonConfirmNewRoute.Location = new Point(100, 230);
            buttonConfirmNewRoute.Size = new Size(80, 30);
            buttonConfirmNewRoute.Click += ButtonConfirmNewRoute_Click;
            
            buttonCancelNewRoute = new Button();
            buttonCancelNewRoute.Text = "Cancel";
            buttonCancelNewRoute.Location = new Point(220, 230);
            buttonCancelNewRoute.Size = new Size(80, 30);
            buttonCancelNewRoute.Click += ButtonCancelNewRoute_Click;
            
            // Add controls to panel
            panelCreateRoute.Controls.Add(labelPanelTitle);
            panelCreateRoute.Controls.Add(labelStartCity);
            panelCreateRoute.Controls.Add(comboBoxStartCity);
            panelCreateRoute.Controls.Add(labelEndCity);
            panelCreateRoute.Controls.Add(comboBoxEndCity);
            panelCreateRoute.Controls.Add(labelRouteType);
            panelCreateRoute.Controls.Add(comboBoxNewRouteType);
            panelCreateRoute.Controls.Add(labelInfo);
            panelCreateRoute.Controls.Add(buttonConfirmNewRoute);
            panelCreateRoute.Controls.Add(buttonCancelNewRoute);
            
            // Populate city dropdowns
            if (allCities != null)
            {
                foreach (var city in allCities.Where(c => PlayerControlsCity(c)))
                {
                    comboBoxStartCity.Items.Add(city.Name);
                }
                
                foreach (var city in allCities)
                {
                    comboBoxEndCity.Items.Add(city.Name);
                }
                
                if (comboBoxStartCity.Items.Count > 0)
                    comboBoxStartCity.SelectedIndex = 0;
                    
                if (comboBoxEndCity.Items.Count > 0)
                    comboBoxEndCity.SelectedIndex = 0;
            }
        }
        
        private void PopulateTradeRoutes()
        {
            listViewTradeRoutes.Items.Clear();
            
            if (routeManager == null || routeManager.AllTradeRoutes == null)
                return;
                
            string typeFilter = comboBoxRouteType.SelectedItem.ToString();
            
            foreach (var route in routeManager.AllTradeRoutes)
            {
                if (route.StartCity == null || route.EndCity == null) continue;

                if (typeFilter != "All" && route.Type.ToString() != typeFilter)
                    continue;
                    
                if (!PlayerControlsCity(route.StartCity) && !PlayerControlsCity(route.EndCity) && !route.IsPlayerOwned)
                    continue; // Only show routes relevant to player
                
                ListViewItem item = new ListViewItem(route.Name);
                item.SubItems.Add(route.Type.ToString());
                item.SubItems.Add(route.StartCity.Name);
                item.SubItems.Add(route.EndCity.Name);
                item.SubItems.Add(route.Distance.ToString("N0"));
                item.SubItems.Add(route.Capacity.ToString("N0"));
                item.SubItems.Add(route.CurrentUsage.ToString("N0"));
                item.SubItems.Add(route.IsBlocked ? "Blocked" : "Active");
                
                // Store route ID for reference
                item.Tag = route.Id;
                
                // Set color based on status
                if (route.IsBlocked)
                    item.ForeColor = Color.Red;
                else if (route.CurrentUsage / route.Capacity > 0.8)
                    item.ForeColor = Color.DarkOrange; // Near capacity
                
                listViewTradeRoutes.Items.Add(item);
            }
        }
        
        private void PopulateTradeAgreements()
        {
            listViewTradeAgreements.Items.Clear();
            
            if (tradeManager == null || playerCountry == null)
                return;
                
            // Get enhanced agreements if available
            var enhancedAgreements = tradeManager.EnhancedTradeAgreements ?? new List<EnhancedTradeAgreement>();
            
            // Include standard agreements too
            var standardAgreements = (tradeManager.GetTradeAgreementsForCountry(playerCountry.Name) ?? new List<TradeAgreement>())
                .Where(a => a.Status == TradeStatus.Active || a.Status == TradeStatus.Proposed);
                
            // Process enhanced agreements
            foreach (var agreement in enhancedAgreements)
            {
                if (!agreement.ParticipatingCountries.Contains(playerCountry.Name))
                    continue;
                    
                string partnerName;
                if (agreement.FromCountryName == playerCountry.Name)
                    partnerName = agreement.ToCountryName;
                else if (agreement.ToCountryName == playerCountry.Name)
                    partnerName = agreement.FromCountryName;
                else
                    partnerName = agreement.ParticipatingCountries.Where(c => c != playerCountry.Name).FirstOrDefault() ?? "Multiple";
                
                string goodsList = agreement.ResourceName;
                if (string.IsNullOrEmpty(goodsList))
                    goodsList = agreement.GoodSpecificTariffs.Any() ? 
                        string.Join(", ", agreement.GoodSpecificTariffs.Keys.Take(2)) + "..." : 
                        "All Goods";
                
                string agreementType = agreement.IsEmbargo ? "Embargo" : 
                    (agreement.ParticipatingCountries.Count > 2 ? "Multilateral" : "Bilateral");
                
                string tariffDetails = agreement.TariffType != TariffType.None ? 
                    $"{agreement.TariffType}: {agreement.TariffRate}%" : "None";
                
                ListViewItem item = new ListViewItem(agreement.TreatyName);
                item.SubItems.Add(agreementType);
                item.SubItems.Add(partnerName);
                item.SubItems.Add(goodsList);
                item.SubItems.Add(tariffDetails);
                item.SubItems.Add(agreement.TurnsRemaining.ToString());
                item.SubItems.Add(agreement.Status.ToString());
                
                item.Tag = agreement.Id;
                
                if (agreement.IsEmbargo)
                    item.BackColor = Color.Salmon;
                else if (agreement.Status == TradeStatus.Proposed)
                    item.BackColor = Color.LightYellow;
                
                listViewTradeAgreements.Items.Add(item);
            }
            
            // Process standard agreements
            foreach (var agreement in standardAgreements)
            {
                // Skip if already processed as enhanced
                if (enhancedAgreements.Any(ea => ea.Id == agreement.Id))
                    continue;
                    
                string partnerName = agreement.FromCountryName == playerCountry.Name ? 
                    agreement.ToCountryName : agreement.FromCountryName;
                
                ListViewItem item = new ListViewItem($"Trade: {agreement.ResourceName}");
                item.SubItems.Add("Standard");
                item.SubItems.Add(partnerName);
                item.SubItems.Add(agreement.ResourceName);
                item.SubItems.Add("N/A");
                item.SubItems.Add(agreement.TurnsRemaining.ToString());
                item.SubItems.Add(agreement.Status.ToString());
                
                item.Tag = agreement.Id;
                
                if (agreement.Status == TradeStatus.Proposed)
                    item.BackColor = Color.LightYellow;
                
                listViewTradeAgreements.Items.Add(item);
            }
        }        private void PopulateGlobalMarket()
        {
            listViewGlobalPrices.Items.Clear();
            listViewTradingPartners.Items.Clear();

            if (globalMarket == null) return;

            labelGlobalTradeValue.Text = $"Global Trade Value: {globalMarket.GlobalTradeValue:C}";

            // Populate Global Prices
            if (globalMarket.GlobalPrices != null)
            {
                string selectedCommodity = comboBoxCommodityFilter.SelectedItem?.ToString() ?? "All Commodities";

                foreach (var goodEntry in globalMarket.GlobalPrices)
                {
                    if (selectedCommodity == "All Commodities" || goodEntry.Key == selectedCommodity)
                    {
                        string commodityName = goodEntry.Key;
                        double currentPrice = goodEntry.Value;
                        string trend = "Stable";
                        if (globalMarket.PriceTrends.ContainsKey(commodityName) && globalMarket.PriceTrends[commodityName].Any())
                        {
                            var latestTrend = globalMarket.PriceTrends[commodityName].Last();
                            if (latestTrend.ChangePercent > 0.01) trend = "Rising";
                            else if (latestTrend.ChangePercent < -0.01) trend = "Falling";
                        }
                        double volatility = globalMarket.GetPriceVolatilityScore(commodityName); // This method exists in GlobalMarket.cs

                        ListViewItem item = new ListViewItem(commodityName);
                        item.SubItems.Add(currentPrice.ToString("F2"));
                        item.SubItems.Add(trend);
                        item.SubItems.Add(volatility.ToString("F2"));
                        listViewGlobalPrices.Items.Add(item);
                    }
                }
            }

            // Populate Trading Partners (Example: Top 10 by total volume with player country)
            if (allCountries != null && playerCountry != null)
            {
                var partnerVolumes = new Dictionary<string, (double exportVol, double importVol)>();

                // Aggregate trade volumes from globalMarket.TradeHistory
                if (globalMarket.TradeHistory != null) // Changed from TradeRecords to TradeHistory
                {
                    foreach (var record in globalMarket.TradeHistory) // Changed from TradeRecords to TradeHistory
                    {
                        if (record.ExportingCountry == playerCountry.Name) // No change here, was correct
                        {
                            if (!partnerVolumes.ContainsKey(record.ImportingCountry)) partnerVolumes[record.ImportingCountry] = (0,0);
                            partnerVolumes[record.ImportingCountry] = (partnerVolumes[record.ImportingCountry].exportVol + record.TotalValue, partnerVolumes[record.ImportingCountry].importVol);
                        }
                        else if (record.ImportingCountry == playerCountry.Name) // No change here, was correct
                        {
                            // This was the line with the error, record.ExporterName should be record.ExportingCountry
                            if (!partnerVolumes.ContainsKey(record.ExportingCountry)) partnerVolumes[record.ExportingCountry] = (0,0);
                            partnerVolumes[record.ExportingCountry] = (partnerVolumes[record.ExportingCountry].exportVol, partnerVolumes[record.ExportingCountry].importVol + record.TotalValue);
                        }
                    }
                }

                var sortedPartners = partnerVolumes.OrderByDescending(kv => kv.Value.exportVol + kv.Value.importVol).Take(10);
                foreach (var partnerEntry in sortedPartners)
                {
                    ListViewItem item = new ListViewItem(partnerEntry.Key); // Partner Country Name
                    item.SubItems.Add(partnerEntry.Value.exportVol.ToString("C")); // Export from player to partner
                    item.SubItems.Add(partnerEntry.Value.importVol.ToString("C")); // Import to player from partner
                    listViewTradingPartners.Items.Add(item);
                }
            }
        }
        
        private bool PlayerControlsCity(StrategyGame.City city)
        {
            if (playerCountry == null || city == null)
                return false;

            foreach (var state in playerCountry.States)
            {
                if (state.Cities.Contains(city))
                {
                    return true;
                }
            }
            return false;
        }
        
        private void ComboBoxRouteType_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateTradeRoutes();
        }
        
        private void ComboBoxCommodityFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateGlobalMarket();
        }
        
        private void ButtonCreateRoute_Click(object sender, EventArgs e)
        {
            panelCreateRoute.Visible = true;
        }
        
        private void ButtonUpgradeRoute_Click(object sender, EventArgs e)
        {
            if (listViewTradeRoutes.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a route to upgrade.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var selectedItem = listViewTradeRoutes.SelectedItems[0];
            Guid routeId = (Guid)selectedItem.Tag;
            
            // In a full implementation, show a dialog with available upgrades
            MessageBox.Show("This feature would show available upgrades for the selected route.", 
                          "Upgrade Route", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ButtonDeleteRoute_Click(object sender, EventArgs e)
        {
            if (listViewTradeRoutes.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a route to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            var selectedItem = listViewTradeRoutes.SelectedItems[0];
            Guid routeId = (Guid)selectedItem.Tag;
            
            DialogResult result = MessageBox.Show("Are you sure you want to delete this route?", 
                                               "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                               
            if (result == DialogResult.Yes)
            {
                if (routeManager.DestroyRoute(routeId))
                {
                    PopulateTradeRoutes();
                    MessageBox.Show("Route deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Failed to delete route.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        private void ButtonNewAgreement_Click(object sender, EventArgs e)
        {
            // In a full implementation, show a new trade agreement dialog
            MessageBox.Show("This feature would open a dialog to create a new trade agreement.", 
                          "New Agreement", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ButtonRenewAgreement_Click(object sender, EventArgs e)
        {
            if (listViewTradeAgreements.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an agreement to renew.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // In a full implementation, renew the selected agreement
            MessageBox.Show("This feature would renew the selected trade agreement.", 
                          "Renew Agreement", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ButtonCancelAgreement_Click(object sender, EventArgs e)
        {
            if (listViewTradeAgreements.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an agreement to cancel.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            DialogResult result = MessageBox.Show("Are you sure you want to cancel this agreement? This may have diplomatic consequences.", 
                                               "Confirm Cancellation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                               
            if (result == DialogResult.Yes)
            {
                // In a full implementation, cancel the agreement
                MessageBox.Show("This feature would cancel the selected trade agreement.", 
                              "Cancel Agreement", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void ButtonConfirmNewRoute_Click(object sender, EventArgs e)
        {
            if (comboBoxStartCity.SelectedItem == null || comboBoxEndCity.SelectedItem == null)
            {
                MessageBox.Show("Please select both start and end cities.", "Incomplete Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            string startCityName = comboBoxStartCity.SelectedItem.ToString();
            string endCityName = comboBoxEndCity.SelectedItem.ToString();
            string routeTypeStr = comboBoxNewRouteType.SelectedItem.ToString();
            
            RouteType routeType = (RouteType)Enum.Parse(typeof(RouteType), routeTypeStr);
            
            City startCity = allCities.FirstOrDefault(c => c.Name == startCityName);
            City endCity = allCities.FirstOrDefault(c => c.Name == endCityName);
            
            if (startCity == null || endCity == null)
            {
                MessageBox.Show("Could not find selected cities.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (startCity == endCity)
            {
                MessageBox.Show("Start and end cities cannot be the same.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Check if player has enough budget
            double routeCost = CalculateRouteEstablishmentCost(startCity, endCity, routeType);
            if (playerCountry.Budget < routeCost)
            {
                MessageBox.Show($"Insufficient funds to establish route. Cost: {routeCost:C}, Available: {playerCountry.Budget:C}", 
                              "Insufficient Funds", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Create the route
            TradeRoute newRoute = routeManager.CreateNewRoute(startCity, endCity, routeType);
            newRoute.IsPlayerOwned = true;
            
            // Deduct cost from player budget
            playerCountry.Budget -= routeCost;
            
            PopulateTradeRoutes();
            panelCreateRoute.Visible = false;
            
            MessageBox.Show($"Trade route established successfully! Cost: {routeCost:C}", 
                          "Route Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void ButtonCancelNewRoute_Click(object sender, EventArgs e)
        {
            panelCreateRoute.Visible = false;
        }
        
        private double CalculateRouteEstablishmentCost(City start, City end, RouteType type)
        {
            // In a full implementation, this would be based on distance, terrain, technology level, etc.
            double baseCost = 5000;
            
            switch (type)
            {
                case RouteType.Land:
                    baseCost = 5000;
                    break;
                case RouteType.Sea:
                    baseCost = 10000;
                    break;
                case RouteType.River:
                    baseCost = 7500;
                    break;
                case RouteType.Air:
                    baseCost = 15000;
                    break;
            }
            
            // In a real implementation, distance would be calculated based on coordinates
            double distance = new Random().Next(100, 1000);
            double distanceFactor = distance / 100.0;
            
            return baseCost * distanceFactor;
        }        // Helper method to improve the display of trading partners
        private void EnhanceTradingPartnersDisplay()
        {
            TradeDisplayHelper.EnhanceTradingPartnersDisplay(listViewTradingPartners, playerCountry);
        }

        private void RefreshData()
        {
            PopulateTradeRoutes();
            PopulateTradeAgreements();
            PopulateGlobalMarket();
        }

        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            RefreshData();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            refreshTimer.Stop();
            base.OnFormClosing(e);
        }
    }
}
