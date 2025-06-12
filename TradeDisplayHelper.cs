using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using StrategyGame;

namespace economy_sim
{
    /// <summary>
    /// Helper class for enhancing trade displays
    /// </summary>
    public static class TradeDisplayHelper
    {
        /// <summary>
        /// Enhances the trading partners display with visual indicators
        /// </summary>
        public static void EnhanceTradingPartnersDisplay(ListView listView, Country playerCountry)
        {
            if (listView == null || playerCountry == null)
                return;
                
            foreach (ListViewItem item in listView.Items)
            {
                string countryName = item.Text;
                
                // Highlight player's country
                if (countryName == playerCountry.Name)
                {
                    item.BackColor = Color.LightGreen;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                    item.ToolTipText = "Your Country";
                }
                else if (item.SubItems.Count >= 3)
                {
                    // Process trade volumes for AI countries
                    int exports = 0;
                    int imports = 0;
                    
                    // Try to parse the values
                    int.TryParse(item.SubItems[1].Text.Replace(",", ""), out exports);
                    int.TryParse(item.SubItems[2].Text.Replace(",", ""), out imports);
                    
                    // Calculate trade balance
                    int tradeBalance = exports - imports;
                    item.ToolTipText = $"Trade Balance: {(tradeBalance >= 0 ? "+" : "")}{tradeBalance:N0}";
                    
                    // Color based on balance
                    if (tradeBalance > 5000)
                        item.ForeColor = Color.Green; // Trade surplus
                    else if (tradeBalance < -5000)
                        item.ForeColor = Color.Red; // Trade deficit
                    
                    // Bold for major partners
                    if (exports + imports > 20000)
                    {
                        item.Font = new Font(item.Font, FontStyle.Bold);
                        item.ToolTipText += "\n(Major Trading Partner)";
                    }
                }
            }
        }
        
        /// <summary>
        /// Populates trading partners list from global market data
        /// </summary>
        public static void PopulateTradingPartners(ListView listView, GlobalMarket globalMarket, 
                                                  Country playerCountry, string selectedCommodity = null)
        {
            if (listView == null || globalMarket == null || playerCountry == null)
                return;
                
            // Clear existing items
            listView.Items.Clear();
            
            // Check if we should show all commodities
            bool showAllCommodities = selectedCommodity == "All Commodities" || string.IsNullOrEmpty(selectedCommodity);
            
            // Track totals for each country
            Dictionary<string, int> exportsByCountry = new Dictionary<string, int>();
            Dictionary<string, int> importsByCountry = new Dictionary<string, int>();
            
            // Process country trade flows which contain directional data
            foreach (var countryFlow in globalMarket.CountryTradeFlows)
            {
                string countryName = countryFlow.Key;
                
                // Skip entries without a proper country name
                if (string.IsNullOrEmpty(countryName) || countryName == "Unknown")
                    continue;
                
                // Process each good's trade flow
                foreach (var goodFlow in countryFlow.Value)
                {
                    string goodName = goodFlow.Key;
                    
                    // Skip if filtering and this isn't the selected commodity
                    if (!showAllCommodities && goodName != selectedCommodity)
                        continue;
                        
                    // Initialize dictionaries if needed
                    if (!exportsByCountry.ContainsKey(countryName))
                        exportsByCountry[countryName] = 0;
                    if (!importsByCountry.ContainsKey(countryName))
                        importsByCountry[countryName] = 0;
                    
                    // Add the actual exports and imports
                    exportsByCountry[countryName] += goodFlow.Value.Exports;
                    importsByCountry[countryName] += goodFlow.Value.Imports;
                }
            }
            
            // Sort countries by total volume
            Dictionary<string, int> totalVolumeByCountry = new Dictionary<string, int>();
            foreach (var country in exportsByCountry.Keys.Union(importsByCountry.Keys))
            {
                int exports = exportsByCountry.ContainsKey(country) ? exportsByCountry[country] : 0;
                int imports = importsByCountry.ContainsKey(country) ? importsByCountry[country] : 0;
                totalVolumeByCountry[country] = exports + imports;
            }
            
            // Display top trading partners
            foreach (var country in totalVolumeByCountry.OrderByDescending(kvp => kvp.Value).Take(10))
            {
                string countryName = country.Key;
                int exports = exportsByCountry.ContainsKey(countryName) ? exportsByCountry[countryName] : 0;
                int imports = importsByCountry.ContainsKey(countryName) ? importsByCountry[countryName] : 0;
                
                ListViewItem item = new ListViewItem(countryName);
                item.SubItems.Add(exports.ToString("N0"));
                item.SubItems.Add(imports.ToString("N0"));
                
                listView.Items.Add(item);
            }
            
            // Now enhance the display with visual indicators
            EnhanceTradingPartnersDisplay(listView, playerCountry);
        }
    }
}
