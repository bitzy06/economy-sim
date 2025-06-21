using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    /// <summary>
    /// Basic international trade processor. Aggregates country level supply and
    /// demand and executes trade transactions using the global market. This is a
    /// simplified implementation focusing on dynamic global trade flows.
    /// </summary>
    public static class InternationalTrade
    {
        /// <summary>
        /// Executes a trade turn matching countries with surpluses to those with
        /// deficits. Trade agreements are consulted for tariffs.
        /// </summary>
        public static void ExecuteTradeTurn(List<Country> countries,
                                            GlobalMarket market,
                                            EnhancedTradeManager tradeManager)
        {
            if (countries == null || market == null)
                return;

            // Aggregate supply and demand for each country
            var supplyByCountry = new Dictionary<string, Dictionary<string, int>>();
            var demandByCountry = new Dictionary<string, Dictionary<string, int>>();

            foreach (var c in countries)
            {
                var supply = new Dictionary<string, int>();
                var demand = new Dictionary<string, int>();

                foreach (var state in c.States)
                {
                    foreach (var city in state.Cities)
                    {
                        foreach (var kvp in city.ExportableSurplus)
                        {
                            if (!supply.ContainsKey(kvp.Key)) supply[kvp.Key] = 0;
                            supply[kvp.Key] += kvp.Value;
                        }
                        foreach (var kvp in city.ImportNeeds)
                        {
                            if (!demand.ContainsKey(kvp.Key)) demand[kvp.Key] = 0;
                            demand[kvp.Key] += kvp.Value;
                        }
                    }
                }
                supplyByCountry[c.Name] = supply;
                demandByCountry[c.Name] = demand;
            }

            // Process each good individually
            foreach (var good in Market.GoodDefinitions.Keys)
            {
                var exporters = new List<(string name, int qty)>();
                var importers = new List<(string name, int qty)>();

                foreach (var c in countries)
                {
                    int supply = supplyByCountry[c.Name].GetValueOrDefault(good);
                    int demand = demandByCountry[c.Name].GetValueOrDefault(good);
                    int net = supply - demand;
                    if (net > 0)
                        exporters.Add((c.Name, net));
                    else if (net < 0)
                        importers.Add((c.Name, -net));
                }

                foreach (var importer in importers)
                {
                    int remainingNeed = importer.qty;
                    foreach (var exporter in exporters.ToList())
                    {
                        if (remainingNeed <= 0)
                            break;
                        if (exporter.qty <= 0)
                            continue;

                        int quantity = Math.Min(exporter.qty, remainingNeed);
                        double basePrice = market.GlobalPrices[good];
                        double tariff = 0;
                        if (tradeManager != null)
                        {
                            tariff = tradeManager.CalculateEffectiveTariff(exporter.name,
                                                                        importer.name,
                                                                        good,
                                                                        quantity,
                                                                        basePrice);
                        }
                        double unitPrice = basePrice + (tariff / Math.Max(1, quantity));
                        double totalValue = unitPrice * quantity;

                        market.RecordTrade(good, exporter.name, importer.name, quantity, totalValue);

                        // Update local trackers
                        int newQty = exporter.qty - quantity;
                        exporters[exporters.IndexOf(exporter)] = (exporter.name, newQty);
                        remainingNeed -= quantity;
                    }
                }
            }
        }
    }
}
