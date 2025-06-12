using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame // Reverted from EconomySim
{
    // Class to track trade volume with directional information
    public class TradeFlow
    {
        public int Exports { get; set; } = 0;
        public int Imports { get; set; } = 0;
        
        public int TotalVolume => Exports + Imports;
    }

    public class GlobalMarket
    {
        public Dictionary<string, double> GlobalPrices { get; private set; } // Global price index for each good
        public Dictionary<string, int> GlobalDemand { get; private set; } // Global demand for each good
        public Dictionary<string, int> GlobalSupply { get; private set; } // Global supply of each good
        public Dictionary<string, List<MarketTrend>> PriceTrends { get; private set; } // Historical price data
        public Dictionary<string, double> PriceVolatility { get; private set; } // How much prices fluctuate
        public Dictionary<string, double> SpeculationMultiplier { get; private set; } // Speculation's effect on prices
        public Dictionary<string, CommodityExchange> CommodityExchanges { get; private set; } // Organized markets
        public Dictionary<string, Dictionary<string, int>> TradeVolumes { get; private set; } // {GoodName, {CountryName, Volume}}
        public Dictionary<string, Dictionary<string, TradeFlow>> CountryTradeFlows { get; set; } // {CountryName, {GoodName, TradeFlow}}
        public List<GlobalTradeEvent> RecentTradeEvents { get; private set; } // Major trade events
        public List<DetailedTradeRecord> TradeHistory { get; private set; } // Detailed history of all trades
        public double GlobalTradeValue { get; private set; } // Total value of all trade this turn
          public static GlobalMarket Instance { get; private set; }
        
        public GlobalMarket()
        {
            // Set the static instance
            Instance = this;
              GlobalPrices = new Dictionary<string, double>();
            GlobalDemand = new Dictionary<string, int>();
            GlobalSupply = new Dictionary<string, int>();
            PriceTrends = new Dictionary<string, List<MarketTrend>>();
            PriceVolatility = new Dictionary<string, double>();
            SpeculationMultiplier = new Dictionary<string, double>();
            CommodityExchanges = new Dictionary<string, CommodityExchange>();
            TradeVolumes = new Dictionary<string, Dictionary<string, int>>();
            CountryTradeFlows = new Dictionary<string, Dictionary<string, TradeFlow>>();
            RecentTradeEvents = new List<GlobalTradeEvent>();
            TradeHistory = new List<DetailedTradeRecord>();
            GlobalTradeValue = 0;
            
            // Initialize with goods from the Market class
            foreach (var goodDef in Market.GoodDefinitions.Values)
            {
                GlobalPrices[goodDef.Name] = goodDef.BasePrice;
                GlobalDemand[goodDef.Name] = 0;
                GlobalSupply[goodDef.Name] = 0;
                PriceTrends[goodDef.Name] = new List<MarketTrend>();
                PriceVolatility[goodDef.Name] = 0.05; // 5% default volatility
                SpeculationMultiplier[goodDef.Name] = 1.0; // No default speculation
                TradeVolumes[goodDef.Name] = new Dictionary<string, int>();
            }
        }
        
        // Method signature changed to reflect that it's part of EconomySim and might not need all these specific StrategyGame types directly if they are wrapped or accessed via a common interface.
        public void UpdateGlobalMarket(List<StrategyGame.City> allCities, List<StrategyGame.Country> allCountries, 
                                     TradeRouteManager routeManager, EnhancedTradeManager tradeManager)        
        {
            // Reset demand and supply
            foreach (var goodName in GlobalDemand.Keys.ToList())
            {
                GlobalDemand[goodName] = 0;
                GlobalSupply[goodName] = 0;
            }
            
            // Aggregate demand and supply from all cities
            foreach (var city in allCities)
            {
                foreach (var kvp in city.LocalDemand)
                {
                    if (GlobalDemand.ContainsKey(kvp.Key))
                    {
                        GlobalDemand[kvp.Key] += kvp.Value;
                    }
                }
                
                foreach (var kvp in city.LocalSupply)
                {
                    if (GlobalSupply.ContainsKey(kvp.Key))
                    {
                        GlobalSupply[kvp.Key] += kvp.Value;
                    }
                }
            }
            
            // Calculate new global prices based on supply and demand
            foreach (var goodName in GlobalPrices.Keys.ToList())
            {
                // Record the old price for trend calculation
                double oldPrice = GlobalPrices[goodName];
                
                // Calculate new price based on supply/demand balance
                if (GlobalDemand.ContainsKey(goodName) && GlobalSupply.ContainsKey(goodName))
                {
                    int demand = GlobalDemand[goodName];
                    int supply = GlobalSupply[goodName];
                    
                    if (demand > 0 && supply > 0)
                    {
                        double ratio = (double)demand / supply;
                        double basePrice = Market.GoodDefinitions[goodName].BasePrice;
                        
                        // Adjust price based on supply/demand ratio
                        double newPrice = basePrice * Math.Pow(ratio, 0.5);
                        
                        // Apply speculation multiplier
                        newPrice *= SpeculationMultiplier[goodName];
                        
                        // Apply random noise based on volatility
                        Random rand = new Random();
                        double noise = 1.0 + ((rand.NextDouble() * 2.0 - 1.0) * PriceVolatility[goodName]);
                        newPrice *= noise;
                        
                        // Cap extreme price changes
                        double maxChange = 0.25; // 25% max change per turn
                        double lowerBound = oldPrice * (1.0 - maxChange);
                        double upperBound = oldPrice * (1.0 + maxChange);
                        newPrice = Math.Max(lowerBound, Math.Min(upperBound, newPrice));
                        
                        // Ensure minimum price
                        newPrice = Math.Max(basePrice * 0.1, newPrice);
                        
                        GlobalPrices[goodName] = newPrice;
                    }
                }
                
                // Record price trend
                if (PriceTrends.ContainsKey(goodName))
                {
                    PriceTrends[goodName].Add(new MarketTrend 
                    { 
                        Price = GlobalPrices[goodName],
                        Change = GlobalPrices[goodName] - oldPrice,
                        ChangePercent = (GlobalPrices[goodName] / oldPrice) - 1.0,
                        Timestamp = DateTime.Now // Or game turn timestamp
                    });
                    
                    // Keep only the most recent trends (e.g., last 20 turns)
                    if (PriceTrends[goodName].Count > 20)
                    {
                        PriceTrends[goodName].RemoveAt(0);
                    }
                }
            }
            
            // Apply influence from commodity exchanges
            foreach (var exchange in CommodityExchanges.Values)
            {
                exchange.InfluenceGlobalPrice(this);
            }
            
            // Apply effects of trade agreements and embargoes
            if (tradeManager != null)
            {
                foreach (var goodName in GlobalPrices.Keys.ToList())
                {
                    // Simple approach: decreased trade = increased prices
                    int embargoes = tradeManager.EnhancedTradeAgreements
                        .Count(a => a.IsEmbargo && a.Status == TradeStatus.Active);
                    
                    if (embargoes > 0)
                    {
                        // Increase price volatility for goods affected by embargoes
                        PriceVolatility[goodName] = Math.Min(0.3, PriceVolatility[goodName] * (1.0 + (embargoes * 0.05)));
                        
                        // Increase price if the good is under embargo
                        GlobalPrices[goodName] *= (1.0 + (embargoes * 0.02));
                    }
                }
            }
              // Reset trade volumes and flows for new turn
            foreach (var goodName in TradeVolumes.Keys)
            {
                TradeVolumes[goodName].Clear();
            }
            foreach (var country in CountryTradeFlows.Values)
            {
                foreach (var flow in country.Values)
                {
                    flow.Exports = 0;
                    flow.Imports = 0;
                }
            }
            GlobalTradeValue = 0;
            
            // Clear old trade events but keep very recent ones
            RecentTradeEvents.RemoveAll(e => e.TurnsAgo > 5);
            foreach (var evt in RecentTradeEvents)
            {
                evt.TurnsAgo++;
            }
        }
          public void RecordTrade(string goodName, string exportingCountry, string importingCountry, 
                               int quantity, double totalValue)
        {
            // Record trade volume by good and country
            if (!TradeVolumes.ContainsKey(goodName))
            {
                TradeVolumes[goodName] = new Dictionary<string, int>();
            }
            
            var countryVolumes = TradeVolumes[goodName];
            
            // Add countries if they don't exist in this good's volume tracking
            if (!countryVolumes.ContainsKey(exportingCountry))
            {
                countryVolumes[exportingCountry] = 0;
            }
            if (!countryVolumes.ContainsKey(importingCountry))
            {
                countryVolumes[importingCountry] = 0;
            }            // Update general trade volumes (accumulate rather than overwrite)
            countryVolumes[exportingCountry] += quantity; // Add to exporter's trading volume
            countryVolumes[importingCountry] += quantity; // Add to importer's trading volume
            
            // Track directional trade flows by country
            // For exporting country
            EnsureCountryInTradeFlows(exportingCountry);
            var exporterGoods = CountryTradeFlows[exportingCountry];
            
            if (!exporterGoods.ContainsKey(goodName))
            {
                exporterGoods[goodName] = new TradeFlow();
            }
            
            // Increment exports for the exporting country
            exporterGoods[goodName].Exports += quantity;
            
            // For importing country
            EnsureCountryInTradeFlows(importingCountry);
            var importerGoods = CountryTradeFlows[importingCountry];
            
            if (!importerGoods.ContainsKey(goodName))
            {
                importerGoods[goodName] = new TradeFlow();
            }
            
            // Increment imports for the importing country
            importerGoods[goodName].Imports += quantity;
            
            // Record to detailed trade history
            TradeHistory.Add(new DetailedTradeRecord
            {
                GoodName = goodName,
                ExportingCountry = exportingCountry,
                ImportingCountry = importingCountry,
                Quantity = quantity,
                PricePerUnit = quantity > 0 ? totalValue / quantity : 0,
                TotalValue = totalValue,
            });
            
            // Accumulate global trade value
            GlobalTradeValue += totalValue;
            
            // Record significant trade events
            if (quantity > 100 || totalValue > 10000)
            {
                RecentTradeEvents.Add(new GlobalTradeEvent
                {
                    GoodName = goodName,
                    ExportingCountry = exportingCountry,
                    ImportingCountry = importingCountry,
                    Quantity = quantity,
                    TotalValue = totalValue,
                    TurnsAgo = 0
                });
            }
        }
        
        private void EnsureCountryInTradeFlows(string countryName)
        {
            if (!CountryTradeFlows.ContainsKey(countryName))
            {
                CountryTradeFlows[countryName] = new Dictionary<string, TradeFlow>();
            }
        }
        
        public List<string> GetTopTradingCountries(string goodName, int count = 5)
        {
            if (TradeVolumes.ContainsKey(goodName))
            {
                return TradeVolumes[goodName]
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
            return new List<string>();
        }
        
        public double GetPriceVolatilityScore(string goodName)
        {
            if (PriceTrends.ContainsKey(goodName) && PriceTrends[goodName].Count >= 5)
            {
                // Calculate standard deviation of recent price changes
                var listForTrends = PriceTrends[goodName];
                // Replaced TakeLast with Skip to ensure compatibility and address CS1061
                var recentTrends = listForTrends.Skip(Math.Max(0, listForTrends.Count - 5)).ToList(); 
                
                double avgChange = recentTrends.Average(t => Math.Abs(t.ChangePercent));
                double sumSquareDiff = recentTrends.Sum(t => Math.Pow(Math.Abs(t.ChangePercent) - avgChange, 2));
                
                // Safety check, though PriceTrends[goodName].Count >= 5 should ensure recentTrends.Count is 5.
                if (recentTrends.Count == 0)
                {
                    return PriceVolatility[goodName]; // Default to base volatility if count is unexpectedly zero
                }
                
                double stdDev = Math.Sqrt(sumSquareDiff / recentTrends.Count); // CS0019 was here
                
                return stdDev;
            }
            return PriceVolatility[goodName]; // Default to the base volatility if not enough data
        }
        
        public void CreateCommodityExchange(string goodName, string hostCountry)
        {
            if (!CommodityExchanges.ContainsKey(goodName))
            {
                CommodityExchanges[goodName] = new CommodityExchange
                {
                    GoodName = goodName,
                    HostCountry = hostCountry,
                    TradingVolume = 0,
                    MinimumLotSize = 10,
                    TransactionFee = 0.02 // 2% fee
                };
                
                // Having an exchange tends to reduce volatility
                PriceVolatility[goodName] *= 0.8;
            }
        }
        
        // Helper methods to retrieve trade information

        /// <summary>
        /// Gets a list of countries that trade with the specified country, sorted by total trade volume
        /// </summary>
        public List<KeyValuePair<string, int>> GetTopTradingPartners(string countryName, int maxResults = 10)
        {
            Dictionary<string, int> tradeVolumeByCountry = new Dictionary<string, int>();
            
            // Process all trade records to find partners
            foreach (var record in TradeHistory)
            {
                // Skip trades not involving this country
                if (record.ExportingCountry != countryName && record.ImportingCountry != countryName)
                    continue;
                
                string partnerName = record.ExportingCountry == countryName 
                    ? record.ImportingCountry 
                    : record.ExportingCountry;
                
                if (!tradeVolumeByCountry.ContainsKey(partnerName))
                    tradeVolumeByCountry[partnerName] = 0;
                
                tradeVolumeByCountry[partnerName] += record.Quantity;
            }
            
            // Sort by volume and return top results
            return tradeVolumeByCountry
                .OrderByDescending(kvp => kvp.Value)
                .Take(maxResults)
                .ToList();
        }
        
        /// <summary>
        /// Gets the export volume for a specific country and good
        /// </summary>
        public int GetExportVolume(string countryName, string goodName)
        {
            if (!CountryTradeFlows.ContainsKey(countryName))
                return 0;
                
            var goods = CountryTradeFlows[countryName];
            if (!goods.ContainsKey(goodName))
                return 0;
                
            return goods[goodName].Exports;
        }
        
        /// <summary>
        /// Gets the import volume for a specific country and good
        /// </summary>
        public int GetImportVolume(string countryName, string goodName)
        {
            if (!CountryTradeFlows.ContainsKey(countryName))
                return 0;
                
            var goods = CountryTradeFlows[countryName];
            if (!goods.ContainsKey(goodName))
                return 0;
                
            return goods[goodName].Imports;
        }
        
        /// <summary>
        /// Gets the top exported goods for a country
        /// </summary>
        public List<KeyValuePair<string, int>> GetTopExports(string countryName, int maxResults = 5)
        {
            if (!CountryTradeFlows.ContainsKey(countryName))
                return new List<KeyValuePair<string, int>>();
                
            return CountryTradeFlows[countryName]
                .OrderByDescending(kvp => kvp.Value.Exports)
                .Take(maxResults)
                .Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value.Exports))
                .ToList();
        }
        
        /// <summary>
        /// Gets the top imported goods for a country
        /// </summary>
        public List<KeyValuePair<string, int>> GetTopImports(string countryName, int maxResults = 5)
        {
            if (!CountryTradeFlows.ContainsKey(countryName))
                return new List<KeyValuePair<string, int>>();
                
            return CountryTradeFlows[countryName]
                .OrderByDescending(kvp => kvp.Value.Imports)
                .Take(maxResults)
                .Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value.Imports))
                .ToList();
        }
    }
    
    public class MarketTrend
    {
        public double Price { get; set; }
        public double Change { get; set; }
        public double ChangePercent { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class GlobalTradeEvent
    {
        public string GoodName { get; set; }
        public string ExportingCountry { get; set; }
        public string ImportingCountry { get; set; }
        public int Quantity { get; set; }
        public double TotalValue { get; set; }
        public int TurnsAgo { get; set; }
    }
    
    public class CommodityExchange
    {
        public string GoodName { get; set; }
        public string HostCountry { get; set; }
        public double TradingVolume { get; set; }
        public int MinimumLotSize { get; set; }
        public double TransactionFee { get; set; }
        public List<FuturesContract> OpenContracts { get; set; } = new List<FuturesContract>();
        
        public CommodityExchange()
        {
            OpenContracts = new List<FuturesContract>();
        }
        
        public void InfluenceGlobalPrice(GlobalMarket market)
        {
            // The more trading volume on the exchange, the more it stabilizes prices
            if (market.GlobalPrices.ContainsKey(GoodName))
            {
                // Higher trading volume leads to more stable prices
                double stabilizationFactor = Math.Min(0.9, 0.5 + (TradingVolume / 10000.0));
                market.PriceVolatility[GoodName] *= stabilizationFactor;
                
                // Process futures contracts
                foreach (var contract in OpenContracts.ToList())
                {
                    contract.TurnsRemaining--;
                    if (contract.TurnsRemaining <= 0)
                    {
                        // Contract is due for settlement
                        double marketPrice = market.GlobalPrices[GoodName];
                        double priceDifference = marketPrice - contract.AgreedPrice;
                        double settlementAmount = priceDifference * contract.Quantity;
                        
                        // Record the settlement in some way (depends on game mechanics)
                        Console.WriteLine($"Futures contract for {contract.Quantity} {GoodName} settled with difference of {settlementAmount:C}");
                        
                        // Record the transaction
                        TradingVolume += contract.Quantity * marketPrice;
                        
                        // Remove settled contract
                        OpenContracts.Remove(contract);
                    }
                }
            }
        }
        
        public FuturesContract CreateFuturesContract(string buyerName, string sellerName, int quantity, double agreedPrice, int duration)
        {
            var contract = new FuturesContract
            {
                GoodName = GoodName,
                BuyerName = buyerName,
                SellerName = sellerName,
                Quantity = quantity,
                AgreedPrice = agreedPrice,
                TurnsRemaining = duration
            };
            
            OpenContracts.Add(contract);
            return contract;
        }
    }
    
    public class FuturesContract
    {
        public string GoodName { get; set; }
        public string BuyerName { get; set; }
        public string SellerName { get; set; }
        public int Quantity { get; set; }
        public double AgreedPrice { get; set; }
        public int TurnsRemaining { get; set; }
    }
      // Enhanced version of GlobalTradeEvent with more detailed information
    public class DetailedTradeRecord
    {
        public string GoodName { get; set; }
        public string ExportingCountry { get; set; }
        public string ImportingCountry { get; set; }
        public int Quantity { get; set; }
        public double PricePerUnit { get; set; }
        public double TotalValue { get; set; }
        public DateTime Timestamp { get; set; }
        
        public DetailedTradeRecord()
        {
            Timestamp = DateTime.Now;
        }
    }
} // Reverted from EconomySim
