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

            // Initialize refresh timer
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 5000; // 5 seconds
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            PopulateTradeRoutes();
            PopulateTradeAgreements();
            PopulateGlobalMarket();
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
