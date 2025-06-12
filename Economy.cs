using System.Collections.Generic;
using System;
using System.Linq;

namespace StrategyGame
{
    public enum GoodCategory
    {
        RawMaterial,      // Grain, Coal, Iron
        IndustrialInput,  // Steel, some Tools
        ProcessedFood,    // Bread
        ConsumerProduct,  // Cloth, Furniture, Tools (for pops), Books, Luxury Clothes
        CapitalGood       // Tools (for factories - can overlap with ConsumerProduct)
    }

    public enum CorporationSpecialization
    {
        None, 
        Agriculture, 
        Mining,      
        HeavyIndustry, 
        LightIndustry, 
        Diversified    
    }

    public static class Economy
    {
        public static void UpdateCountryEconomy(Country country)
        {
            // === New Financial System Integration ===
            NationalFinancialSystem fs = country.FinancialSystem;

            // 1. Calculate Tax Revenue using the new system
            // Placeholder values for assessable income, corporate profits, etc.
            // These would eventually be derived from detailed economic activity (POPs, corporations)
            decimal totalAssessablePopIncome = (decimal)country.Population * 500m; // Example: average income per capita
            decimal totalCorporateProfits = (decimal)country.States.Sum(s => s.Cities.Sum(c => c.Factories.Sum(f => f.OwnerCorporation?.Budget * 0.1 ?? 0.0))); // Highly simplified
            decimal totalLandValue = (decimal)country.States.Count * 1000000m; // Example
            decimal totalConsumptionValue = totalAssessablePopIncome * 0.6m; // Example: 60% of income is consumed

            decimal taxRevenue = fs.CalculateTaxRevenue(totalAssessablePopIncome, totalCorporateProfits, totalLandValue, totalConsumptionValue);
            country.Budget += (double)taxRevenue;
            // Channel a portion of revenue into reserves
            fs.AdjustReserves(taxRevenue * 0.05m);

            // 2. Account for National Expenses from the Financial System
            decimal debtInterestPayment = fs.GetAnnualDebtInterestPayment();
            country.Budget -= (double)debtInterestPayment;
            fs.AdjustReserves(-(debtInterestPayment * 0.05m));

            // 3. Account for other general National Expenses
            country.Budget -= country.NationalExpenses;
            fs.AdjustMoneySupply((decimal)country.NationalExpenses * 0.01m);

            // 4. Bond system removed; no maturities to process

            // 5. Update other financial indicators
            decimal currentGdp = totalAssessablePopIncome + totalCorporateProfits; // Highly simplified GDP
            fs.UpdateFinancialIndicators(currentGdp);

            // The old fund distribution to states is now in Country.DistributeFunds(), which can be called separately if needed.
            // country.DistributeFunds(); // This call can be made here or as part of a different game phase.

            // Ensure country budget doesn't go unrealistically negative without consequence (e.g., debt, default)
            if (country.Budget < 0)
            {
                // Trigger events like increasing debt, credit rating hit, etc.
                // For now, just log or cap it.
                // fs.IssueBond("CentralBank", (decimal)-country.Budget, 0.05f, 5); // Auto-issue debt to cover deficit (simplification)
            }
        }

        public static void UpdateStateEconomy(State state)
        {
            // Collect taxes from cities
            double totalTax = 0;
            foreach (var city in state.Cities)
            {
                double tax = city.Budget * state.TaxRate;
                city.Budget -= tax;
                totalTax += tax;
            }
            state.Budget += totalTax;

            // Pay state expenses
            state.Budget -= state.StateExpenses;

            // Optionally, distribute funds to cities (not implemented here)
            if (state.Cities.Any())
            {
                double fundsToDistribute = state.Budget * 0.10; // Distribute 10% of remaining budget
                double fundsPerCity = fundsToDistribute / state.Cities.Count;
                foreach (var city in state.Cities)
                {
                    city.Budget += fundsPerCity;
                }
                state.Budget -= fundsToDistribute;
            }
        }

        public static void UpdateCityEconomy(City city)
        {
            // Collect taxes from population
            double tax = city.Population * 10 * city.TaxRate; // Example: $10 per person * tax rate
            city.Budget += tax;

            // Pay city expenses
            city.Budget -= city.CityExpenses;

            // Simulate growth
            city.SimulateGrowth();

            // Process detailed city economy including buy/sell order generation
            CityEconomy.ProcessCityEconomy(city);
        }

        public static void UpdatePopGrowth(PopClass pop)
        {
            // Base growth rate (e.g., 2% per year)
            double baseGrowthRate = 0.02;

            // Adjust growth rate based on Quality of Life and Happiness
            double qolFactor = pop.QualityOfLife > 0.5 ? 1 : pop.QualityOfLife; // Penalize low QoL
            double happinessFactor = pop.Happiness > 50 ? 1 : pop.Happiness / 100.0; // Penalize low happiness

            double adjustedGrowthRate = baseGrowthRate * qolFactor * happinessFactor;

            // Calculate new population size
            int newSize = (int)(pop.Size * (1 + adjustedGrowthRate));

            // Update the population size
            pop.Size = newSize;

            // Optionally, update other properties like Happiness based on growth
            pop.Happiness = (int)(pop.QualityOfLife * 100); // Example: scale QoL to 0-100 for Happiness
        }

        public static void UpdateCityPopulation(City city)
        {
            foreach (var pop in city.PopClasses)
            {
                UpdatePopGrowth(pop);
            }
        }

        public static void UpdateCountryPopulation(Country country)
        {
            foreach (var state in country.States)
            {
                foreach (var city in state.Cities)
                {
                    UpdateCityPopulation(city);
                }
            }
        }
    }

    public class Good
    {
        public string Name { get; set; }
        public double BasePrice { get; set; }
        public int Quantity { get; set; }
        public GoodCategory Category { get; set; }

        public Good(string name, double basePrice, GoodCategory category, int quantity = 0)
        {
            Name = name;
            BasePrice = basePrice;
            Category = category;
            Quantity = quantity;
        }
    }

    public class Factory
    {
        public string Name { get; set; }
        public Corporation OwnerCorporation { get; set; }
        public List<Good> InputGoods { get; set; }
        public List<Good> OutputGoods { get; set; }
        public int ProductionCapacity { get; set; }
        public int WorkersEmployed { get; set; } // This might become a sum of employed from JobSlots or represent total workforce
        public Dictionary<string, int> JobSlots { get; set; }
        public Dictionary<string, int> ActualEmployed { get; set; } // Tracks actual number employed in each slot type

        public Factory(string name, int productionCapacity)
        {
            Name = name;
            ProductionCapacity = productionCapacity;
            InputGoods = new List<Good>();
            OutputGoods = new List<Good>();
            JobSlots = new Dictionary<string, int>();
            ActualEmployed = new Dictionary<string, int>();
            // WorkersEmployed will be calculated or set based on ActualEmployed
        }

        // Simulate production: consume inputs, produce outputs
        public void Produce(Dictionary<string, Good> cityStockpile, City city)
        {
            if (this.OwnerCorporation == null) 
            {
                return; 
            }

            double totalInputCost = 0;
            bool allInputsAvailableInStockpile = true;
            foreach (var input in InputGoods)
            {
                if (!cityStockpile.ContainsKey(input.Name) || cityStockpile[input.Name].Quantity < input.Quantity * ProductionCapacity)
                {
                    allInputsAvailableInStockpile = false;
                    break;
                }
                // Use city.LocalPrices for input cost calculation
                totalInputCost += (input.Quantity * ProductionCapacity) * (city.LocalPrices.ContainsKey(input.Name) ? city.LocalPrices[input.Name] : input.BasePrice); 
            }

            if (!allInputsAvailableInStockpile) return;
            if (this.OwnerCorporation.Budget < totalInputCost) return; 

            this.OwnerCorporation.Budget -= totalInputCost;
            city.Budget += totalInputCost;

            foreach (var input in InputGoods)
            {
                cityStockpile[input.Name].Quantity -= input.Quantity * ProductionCapacity;
                // Update city.LocalDemand
                if (city.LocalDemand.ContainsKey(input.Name))
                    city.LocalDemand[input.Name] += input.Quantity * ProductionCapacity;
                else
                    city.LocalDemand[input.Name] = input.Quantity * ProductionCapacity;
            }

            double totalOutputValue = 0;
            foreach (var output in OutputGoods)
            {
                if (!cityStockpile.ContainsKey(output.Name))
                    cityStockpile[output.Name] = new Good(output.Name, output.BasePrice, output.Category); 
                
                cityStockpile[output.Name].Quantity += output.Quantity * ProductionCapacity;
                // Use city.LocalPrices for output value calculation
                double currentMarketPrice = city.LocalPrices.ContainsKey(output.Name) ? city.LocalPrices[output.Name] : output.BasePrice;
                totalOutputValue += (output.Quantity * ProductionCapacity) * currentMarketPrice;
                
                // Update city.LocalSupply
                if (city.LocalSupply.ContainsKey(output.Name))
                    city.LocalSupply[output.Name] += output.Quantity * ProductionCapacity;
                else
                    city.LocalSupply[output.Name] = output.Quantity * ProductionCapacity;
            }

            this.OwnerCorporation.Budget += totalOutputValue;
            city.Budget -= totalOutputValue;
        }
    }

    // The following should be outside the Economy class
    public static class Market
    {
        public static Dictionary<string, Good> GoodDefinitions { get; private set; } = new Dictionary<string, Good>(); // Keep this for base prices/categories
        public static List<Corporation> AllCorporations { get; private set; } = new List<Corporation>(); 

        // Call this at the start of each turn for each city to reset its local supply/demand
        public static void ResetCitySupplyDemand(City city)
        {
            if (city == null || city.LocalSupply == null || city.LocalDemand == null) return;

            List<string> goodKeys = city.LocalSupply.Keys.ToList(); // Iterate over a copy of keys
            foreach (var key in goodKeys)
            {
                city.LocalSupply[key] = 0;
            }
            goodKeys = city.LocalDemand.Keys.ToList();
            foreach (var key in goodKeys)
            {
                city.LocalDemand[key] = 0;
            }
            // Ensure all defined goods have an entry, even if 0
            foreach (var goodDefKey in GoodDefinitions.Keys)
            {
                if (!city.LocalSupply.ContainsKey(goodDefKey)) city.LocalSupply[goodDefKey] = 0;
                if (!city.LocalDemand.ContainsKey(goodDefKey)) city.LocalDemand[goodDefKey] = 0;
            }
        }

        // Call this for each city after all its local buy/sell actions to update its local prices
        public static void UpdateCityPrices(City city)
        {
            if (city == null || city.LocalPrices == null || city.LocalSupply == null || city.LocalDemand == null) return;

            foreach (var goodName in new List<string>(city.LocalPrices.Keys)) // Iterate over goods present in the city's price list
            {
                int supply = city.LocalSupply.ContainsKey(goodName) ? city.LocalSupply[goodName] : 0;
                int demand = city.LocalDemand.ContainsKey(goodName) ? city.LocalDemand[goodName] : 0;
                
                double basePrice = 10.0; // Default base price if not in GoodDefinitions
                if (GoodDefinitions.ContainsKey(goodName))
                {
                    basePrice = GoodDefinitions[goodName].BasePrice;
                }
                else
                {
                    // This case should ideally not happen if all goods are defined
                    Console.WriteLine($"Warning: Good '{goodName}' not found in GoodDefinitions during price update for city {city.Name}.");
                }

                if (supply == 0 && demand == 0) 
                {
                    // city.LocalPrices[goodName] = basePrice; // Option 1: Reset to base if no activity
                                                       // Option 2: Keep previous price (current behavior if no change)
                    continue; // Or let it drift based on previous state / small random factor if desired
                }
                
                // Prevent extreme swings if supply or demand is zero but the other is not.
                int effectiveSupply = Math.Max(1, supply); // Avoid division by zero, ensure some base for calculation
                int effectiveDemand = Math.Max(1, demand);
                double denominator = effectiveSupply + effectiveDemand; // Simplified, can add +1 to soften further

                double priceAdjustmentFactor = 0.5; // How much prices react
                double newPrice = basePrice * (1 + priceAdjustmentFactor * (demand - supply) / denominator);
                
                city.LocalPrices[goodName] = Math.Max(0.1 * basePrice, Math.Min(5.0 * basePrice, newPrice)); // Clamp price to avoid extremes
            }
        }

        // Method for entities (pops, factories via corps) to buy from the city market
        // Buyer pays, goods move from city stockpile to buyer (implicit for pops, or could be explicit for factories)
        public static bool BuyFromCityMarket(City city, string goodName, int quantity, PopClass buyerPop = null, Corporation buyerCorp = null)
        {
            if (city == null || !city.LocalPrices.ContainsKey(goodName) || !city.Stockpile.ContainsKey(goodName) || city.Stockpile[goodName].Quantity < quantity)
            {
                return false; // Good not available or not enough in stock
            }
            if (quantity <= 0) return true; // Buying nothing is always successful

            double pricePerUnit = city.LocalPrices[goodName];
            double totalCost = pricePerUnit * quantity;
            bool transactionMade = false;

            if (buyerPop != null)
            {
                // For PopClass, we need a way to access their individual budget or assume it's handled by city.PopBudget or similar
                // This part needs more detailed thought on Pop budgets if they directly transact.
                // For now, let's assume the city's main budget is a proxy or that pop needs are met abstractly without direct pop budget deduction here.
                // Let's assume the cost is covered by the pop's general spending power, not deducting from a specific pop budget field here.
                // The important part for the market is that the demand is registered and goods are removed.
                transactionMade = true; // For pops, assume they can afford their needs for this simplified step
            }
            else if (buyerCorp != null)
            {
                if (buyerCorp.Budget >= totalCost)
                {
                    buyerCorp.Budget -= totalCost;
                    city.Budget += totalCost; // City receives payment from corporation
                    transactionMade = true;
                }
            }
            else // If no specific buyer, maybe it's the city itself procuring (e.g. for construction - future use)
            {
                 if (city.Budget >= totalCost) {
                    city.Budget -= totalCost; // City pays itself, effectively writing off the cost for internal use
                    transactionMade = true;
                 }
            }

            if (transactionMade)
            {
                city.Stockpile[goodName].Quantity -= quantity;
                if (city.LocalDemand.ContainsKey(goodName))
                    city.LocalDemand[goodName] += quantity; // Record demand fulfilled
                else
                    city.LocalDemand[goodName] = quantity;
                return true;
            }
            return false;
        }

        // Method for entities (factories via corps) to sell to the city market
        // Seller gets paid, goods move from seller (implicit) to city stockpile
        public static void SellToCityMarket(City city, string goodName, int quantity, Corporation sellerCorp = null)
        {
            if (city == null || quantity <= 0) return;

            if (!city.LocalPrices.ContainsKey(goodName))
            {
                // If good has no price yet in city, use base price from definitions
                if (GoodDefinitions.ContainsKey(goodName))
                {
                    city.LocalPrices[goodName] = GoodDefinitions[goodName].BasePrice;
                }
                else
                {
                    Console.WriteLine($"Warning: Good '{goodName}' has no price definition. Cannot sell to city {city.Name}.");
                    return; // Cannot determine price
                }
            }
            double pricePerUnit = city.LocalPrices[goodName];
            double totalValue = pricePerUnit * quantity;

            if (sellerCorp != null)
            {
                 // Corporation sells to the city. City must be able to afford it.
                if (city.Budget < totalValue)
                {
                    // Console.WriteLine($"Warning: City {city.Name} cannot afford to buy {quantity} of {goodName} from {sellerCorp.Name}. Needs {totalValue:C}, Has {city.Budget:C}.");
                    // Decide if partial sale is allowed or fail. For now, fail the whole sale.
                    return; 
                }
                sellerCorp.Budget += totalValue;
                city.Budget -= totalValue;
            }
            // Else: If no sellerCorp, implies goods are appearing in city stockpile from non-corporate source (e.g. player spawning, aid - future)
            // In this case, city budget isn't directly affected by paying a corp, but goods still increase supply.

            if (!city.Stockpile.ContainsKey(goodName))
            {
                GoodCategory category = GoodCategory.ConsumerProduct; // Default, should ideally come from the good being sold
                if (GoodDefinitions.ContainsKey(goodName)) category = GoodDefinitions[goodName].Category;
                city.Stockpile[goodName] = new Good(goodName, city.LocalPrices[goodName], category, 0);
            }
            city.Stockpile[goodName].Quantity += quantity;
            
            if (city.LocalSupply.ContainsKey(goodName))
                city.LocalSupply[goodName] += quantity; // Record supply provided
            else
                city.LocalSupply[goodName] = quantity;
        }

        public static void ResolveInterCityTrade(List<City> allCities, double baseTradeCostPerUnit = 0.1)
        {
            if (allCities == null || allCities.Count < 2) return; // Need at least two cities for trade

            foreach (var goodName in GoodDefinitions.Keys) // Iterate over all defined goods
            {
                List<City> potentialExporters = allCities
                    .Where(c => c.ExportableSurplus.ContainsKey(goodName) && c.ExportableSurplus[goodName] > 0 && c.LocalPrices.ContainsKey(goodName))
                    .OrderBy(c => c.LocalPrices[goodName]) // Cheapest sellers first
                    .ToList();

                List<City> potentialImporters = allCities
                    .Where(c => c.ImportNeeds.ContainsKey(goodName) && c.ImportNeeds[goodName] > 0 && c.LocalPrices.ContainsKey(goodName))
                    .OrderByDescending(c => c.LocalPrices[goodName]) // Buyers willing to pay most first
                    .ToList();

                if (!potentialExporters.Any() || !potentialImporters.Any()) continue; // No one to trade this good

                foreach (var exporter in potentialExporters)
                {
                    if (exporter.ExportableSurplus[goodName] <= 0) continue; // No more of this good to export from this city

                    foreach (var importer in potentialImporters)
                    {
                        if (importer.ImportNeeds[goodName] <= 0) continue; // This city no longer needs this good
                        if (exporter == importer) continue; // Cannot trade with oneself

                        double priceAtExporter = exporter.LocalPrices[goodName];
                        double priceAtImporter = importer.LocalPrices[goodName];
                        double effectiveExportPrice = priceAtExporter + baseTradeCostPerUnit;

                        if (effectiveExportPrice < priceAtImporter) // Trade is profitable for the system / importer is willing
                        {
                            int maxCanTrade = Math.Min(exporter.ExportableSurplus[goodName], importer.ImportNeeds[goodName]);
                            if (maxCanTrade <= 0) continue;

                            // Check importer budget
                            double costForImporter = maxCanTrade * effectiveExportPrice;
                            if (importer.Budget < costForImporter)
                            {
                                // Importer cannot afford the full amount, calculate how much they can afford
                                if (effectiveExportPrice <= 0) continue; // Avoid division by zero if price is weird
                                maxCanTrade = (int)Math.Floor(importer.Budget / effectiveExportPrice);
                                if (maxCanTrade <= 0) continue; // Cannot afford any
                                costForImporter = maxCanTrade * effectiveExportPrice; // Recalculate cost
                            }
                            
                            int quantityTraded = maxCanTrade;
                            if (quantityTraded <= 0) continue;

                            // --- Perform Transaction ---
                            // Exporter side
                            exporter.Stockpile[goodName].Quantity -= quantityTraded;
                            exporter.ExportableSurplus[goodName] -= quantityTraded;
                            exporter.Budget += quantityTraded * priceAtExporter;
                            if (exporter.LocalDemand.ContainsKey(goodName)) exporter.LocalDemand[goodName] += quantityTraded;
                            else exporter.LocalDemand[goodName] = quantityTraded;

                            // Importer side
                            if (!importer.Stockpile.ContainsKey(goodName)) 
                            {
                                GoodCategory category = GoodDefinitions.ContainsKey(goodName) ? GoodDefinitions[goodName].Category : GoodCategory.ConsumerProduct;
                                importer.Stockpile[goodName] = new Good(goodName, priceAtImporter, category, 0);
                            }
                            importer.Stockpile[goodName].Quantity += quantityTraded;
                            importer.ImportNeeds[goodName] -= quantityTraded;
                            importer.Budget -= costForImporter;
                            if (importer.LocalSupply.ContainsKey(goodName)) importer.LocalSupply[goodName] += quantityTraded;
                            else importer.LocalSupply[goodName] = quantityTraded;
                            
                            // Record the trade in the global market if available
                            // Record the trade in the global market if available
                            if (StrategyGame.GlobalMarket.Instance != null)
                            {
                                // Use default "Unknown" for country names - the global market will handle this
                                StrategyGame.GlobalMarket.Instance.RecordTrade(
                                    goodName, 
                                    "Unknown", // Exporter country - this should be set by the GlobalMarket based on city relationships
                                    "Unknown", // Importer country - this should be set by the GlobalMarket based on city relationships
                                    quantityTraded, 
                                    quantityTraded * effectiveExportPrice);
                            }
                            
                            // Console.WriteLine($"TRADE: {exporter.Name} exported {quantityTraded} of {goodName} to {importer.Name} at effective price {effectiveExportPrice:F2} (Exporter got {priceAtExporter:F2})");
                        }
                        else
                        {
                            // If this importer won't pay enough for this exporter's goods, 
                            // they likely won't for subsequent (more expensive) exporters of this good either, so break inner loop.
                            break; 
                        }
                        if (exporter.ExportableSurplus[goodName] <= 0) break; // Exporter has run out
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to get country name from a city by looking up its ownership in the world state
        /// </summary>
        private static string GetCountryNameForCity(City city, List<Country> countries)
        {
            if (city == null || countries == null)
                return "Unknown";
                
            // Check each country to find which one owns this city
            foreach (var country in countries)
            {
                foreach (var state in country.States)
                {
                    if (state.Cities.Contains(city))
                    {
                        return country.Name;
                    }
                }
            }
            
            return "Unknown";
        }

        public static void SimulateCityEconomy(City city)
        {
            if (city == null) return;

            // Simulate production of goods
            foreach (var good in Market.GoodDefinitions.Keys)
            {
                if (!city.LocalSupply.ContainsKey(good)) city.LocalSupply[good] = 0;
                city.LocalSupply[good] += 100; // Example: produce 100 units of each good
            }

            // Simulate consumption of goods by population
            foreach (var pop in city.PopClasses)
            {
                foreach (var need in pop.Needs)
                {
                    string good = need.Key;
                    double perCapitaConsumption = need.Value;
                    int totalConsumption = (int)(pop.Size * perCapitaConsumption);

                    if (!city.LocalDemand.ContainsKey(good)) city.LocalDemand[good] = 0;
                    city.LocalDemand[good] += totalConsumption;

                    if (city.LocalSupply.ContainsKey(good))
                    {
                        int available = city.LocalSupply[good];
                        int consumed = Math.Min(available, totalConsumption);
                        city.LocalSupply[good] -= consumed;
                    }
                }
            }
        }
    }

    // Define Corporation class if it doesn't exist, or add to it
    public class Corporation 
    {
        public string Name { get; set; }
        public List<Factory> OwnedFactories { get; set; }
        public double Budget { get; set; }
        public bool IsPlayerControlled { get; set; }
        public CorporationSpecialization Specialization { get; set; }

        public Corporation(string name, CorporationSpecialization spec = CorporationSpecialization.Diversified)
        {
            Name = name;
            OwnedFactories = new List<Factory>();
            Budget = 1000000; // Example starting budget
            IsPlayerControlled = false;
            Specialization = spec;
        }

        public void AddFactory(Factory factory)
        {
            if (!OwnedFactories.Contains(factory))
            {
                OwnedFactories.Add(factory);
                // factory.OwnerCorporation = this; // This link should be set when factory is assigned
            }
        }

        public void UpdateAI(List<City> allCities, List<Good> goodPrototypes, Random randomizer)
        {
            if (IsPlayerControlled) return; 

            double investmentThreshold = 200000; 
            double factoryBuildCost = 100000;
            int newFactoryBaseCapacity = 2; 
            int maxFactoriesOfSameTypeInCityForCorp = 1; // AI Corp won't build more than this of the same type in one city
            int maxFactoriesOfSameTypeInCityTotal = 3;   // AI Corp hesitant if city already has this many of same type total

            if (Budget > investmentThreshold && randomizer.Next(100) < 10 && FactoryBlueprints.AllBlueprints.Any()) 
            {
                // 1. Select a FactoryBlueprint based on Specialization
                // For diversified, we might not pass a category hint, or pick one randomly
                GoodCategory hintForDiversified = (GoodCategory)randomizer.Next(Enum.GetValues(typeof(GoodCategory)).Length);
                FactoryBlueprint chosenBlueprint = FactoryBlueprints.GetBlueprintBySpecialization(this.Specialization, hintForDiversified, randomizer);

                if (chosenBlueprint == null) 
                {
                    // Console.WriteLine($"AI Corp '{Name}': Could not find a suitable factory blueprint for specialization {Specialization}.");
                    return; 
                }

                if (!allCities.Any()) return;
                City targetCity = allCities[randomizer.Next(allCities.Count)];

                // 2. Check for existing factories / oversupply
                int corpOwnedOfTypeInCity = this.OwnedFactories.Count(f => f.OutputGoods.Any(og => og.Name == chosenBlueprint.OutputGood.Name) && 
                                                                       targetCity.Factories.Contains(f)); // A bit simplistic, assumes factory is in city's list if owned by corp and in that city
                int totalOfTypeInCity = targetCity.Factories.Count(f => f.OutputGoods.Any(og => og.Name == chosenBlueprint.OutputGood.Name));

                if (corpOwnedOfTypeInCity >= maxFactoriesOfSameTypeInCityForCorp)
                {
                    // Console.WriteLine($"AI Corp '{Name}': Already owns {corpOwnedOfTypeInCity} of {chosenBlueprint.FactoryTypeName} in {targetCity.Name}. Skipping build.");
                    return;
                }
                if (totalOfTypeInCity >= maxFactoriesOfSameTypeInCityTotal && randomizer.Next(100) < 75) // High chance to skip if city is saturated
                {
                    // Console.WriteLine($"AI Corp '{Name}': City {targetCity.Name} already has {totalOfTypeInCity} of {chosenBlueprint.FactoryTypeName}. High chance of skipping build.");
                    return;
                }

                // 3. Build factory using the blueprint
                if (Budget >= factoryBuildCost)
                {
                    Console.WriteLine($"AI Corp '{Name}' ({Specialization}): Attempting to build {chosenBlueprint.FactoryTypeName} (produces {chosenBlueprint.OutputGood.Name}) in {targetCity.Name}. Budget: {Budget:C}");
                    Budget -= factoryBuildCost;

                    string newFactoryName = $"{this.Name}'s {chosenBlueprint.FactoryTypeName} #{OwnedFactories.Count(f => f.Name.StartsWith(this.Name + "'s " + chosenBlueprint.FactoryTypeName)) + 1}";
                    Factory newFactory = new Factory(newFactoryName, newFactoryBaseCapacity);
                    
                    // Calculate actual job slots based on production capacity
                    int totalJobSlots = newFactoryBaseCapacity * 5; // Base number of workers needed
                    foreach (var jobSlot in chosenBlueprint.DefaultJobSlotDistribution)
                    {
                        int slots = (int)Math.Ceiling(totalJobSlots * jobSlot.Value);
                        newFactory.JobSlots[jobSlot.Key] = slots;
                        DebugLogger.Log($"[Factory Creation] {newFactoryName} - Added {slots} slots for {jobSlot.Key}");
                    }

                    // Set up input/output goods
                    foreach (var inputGood in chosenBlueprint.InputGoods)
                    {
                        newFactory.InputGoods.Add(new Good(inputGood.Name, inputGood.BasePrice, inputGood.Category));
                    }
                    newFactory.OutputGoods.Add(new Good(chosenBlueprint.OutputGood.Name, chosenBlueprint.OutputGood.BasePrice, chosenBlueprint.OutputGood.Category));

                    newFactory.OwnerCorporation = this;
                    
                    targetCity.Factories.Add(newFactory);
                    this.AddFactory(newFactory);
                    Console.WriteLine($"AI Corp '{Name}': SUCCESSFULLY BUILT {newFactory.Name} in {targetCity.Name}. New Budget: {Budget:C}");
                }
            }
        }

        // TODO: Add methods for corporate actions: CollectProfits, Invest, etc.
    }

    public static class CityEconomy
    {
        public static void ProcessCityEconomy(City city)
        {
            city.BuyOrders.Clear();
            city.SellOrders.Clear();
            
            // Clear/Reset trade-related dictionaries for the current turn
            if (Market.GoodDefinitions != null)
            {
                foreach (var goodKey in Market.GoodDefinitions.Keys)
                {
                    city.ExportableSurplus[goodKey] = 0;
                    city.ImportNeeds[goodKey] = 0;
                }
            }
            // Also ensure any goods in stockpile but not in GoodDefinitions (should not happen) are reset
            foreach (var goodKey in city.ExportableSurplus.Keys.ToList()) city.ExportableSurplus[goodKey] = 0;
            foreach (var goodKey in city.ImportNeeds.Keys.ToList()) city.ImportNeeds[goodKey] = 0;

            foreach (var pop in city.PopClasses)
            {
                DebugLogger.Log($"[Employment Debug] Population Class: {pop.Name}, Size: {pop.Size}, Initial Employed: {pop.Employed}");
                pop.Size = Math.Max(1, pop.Size); 
                pop.IncomePerPerson = Math.Max(0.01, pop.IncomePerPerson); 
           //     pop.Employed = 0; 
            }

            foreach (var factory in city.Factories)
            {
                if (factory.JobSlots == null || factory.JobSlots.Count == 0)
                {
                    DebugLogger.Log($"[Warning] Factory '{factory.Name}' has no job slots defined.");
                    continue;
                }

                DebugLogger.Log($"[Employment Debug] Factory: {factory.Name}, Job Slots: {string.Join(", ", factory.JobSlots.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
                factory.ActualEmployed.Clear();
                factory.WorkersEmployed = 0; 
            }

            Dictionary<string, int> totalAvailableSlots = new Dictionary<string, int>();
            foreach (var factory in city.Factories)
            {
                foreach (var slotEntry in factory.JobSlots)
                {
                    if (!totalAvailableSlots.ContainsKey(slotEntry.Key))
                        totalAvailableSlots[slotEntry.Key] = 0;
                    totalAvailableSlots[slotEntry.Key] += slotEntry.Value;
                }
            }

            foreach (var jobType in totalAvailableSlots.Keys)
            {
                DebugLogger.Log($"[Employment Debug] Job Type: {jobType}, Total Available Slots: {totalAvailableSlots[jobType]}");
            }

            foreach (string jobType in new List<string>(totalAvailableSlots.Keys)) 
            {
                if (!totalAvailableSlots.ContainsKey(jobType) || totalAvailableSlots[jobType] == 0) continue;
                int remainingSlotsForJobType = totalAvailableSlots[jobType];
                foreach (var pop in city.PopClasses.Where(p => p.Name == jobType)) 
                {
                    if (remainingSlotsForJobType == 0) break; 
                    int canBeEmployed = Math.Min(pop.Size - pop.Employed, remainingSlotsForJobType); 
                    pop.Employed += canBeEmployed;
                    DebugLogger.Log($"[Employment Debug] Population Class: {pop.Name}, Newly Employed: {canBeEmployed}, Total Employed: {pop.Employed}");
                    remainingSlotsForJobType -= canBeEmployed;
                    int assignedToFactories = canBeEmployed;
                    foreach (var factory in city.Factories.Where(f => f.JobSlots.ContainsKey(jobType)))
                    {
                        if (assignedToFactories == 0) break;
                        int factoryActualEmployedForType = factory.ActualEmployed.ContainsKey(jobType) ? factory.ActualEmployed[jobType] : 0;
                        int slotsInFactoryForType = factory.JobSlots[jobType];
                        int canAssignToFactory = Math.Min(assignedToFactories, slotsInFactoryForType - factoryActualEmployedForType);
                        if (canAssignToFactory > 0)
                        {
                            if (!factory.ActualEmployed.ContainsKey(jobType))
                                factory.ActualEmployed[jobType] = 0;
                            factory.ActualEmployed[jobType] += canAssignToFactory;
                            factory.WorkersEmployed += canAssignToFactory; 
                            DebugLogger.Log($"[Employment Debug] Factory: {factory.Name}, Job Type: {jobType}, Newly Assigned: {canAssignToFactory}, Total Workers Employed: {factory.WorkersEmployed}");
                            assignedToFactories -= canAssignToFactory;
                        }
                    }
                }

                if (!city.PopClasses.Any(p => p.Name == jobType))
                {
                    DebugLogger.Log($"[Warning] No population class matches job type '{jobType}'.");
                }
            }

            foreach (var pop in city.PopClasses)
            {
                double popBudget = pop.Size * pop.IncomePerPerson; // This is spending power for the turn
                int unmet = 0;
                foreach (var needEntry in pop.Needs) 
                {
                    string good = needEntry.Key;
                    double needPer1000 = needEntry.Value; 
                    int needed = (int)(Math.Max(1, pop.Size) * needPer1000 / 1000.0);
                    needed = Math.Max(0, needed); 
                    int available = city.Stockpile.ContainsKey(good) ? city.Stockpile[good].Quantity : 0;
                    int shortfall = Math.Max(0, needed - available);
                    
                    if (shortfall > 0)
                    {
                        // Use city.LocalPrices for buy order max price
                        double currentPrice = city.LocalPrices.ContainsKey(good) ? city.LocalPrices[good] : (Market.GoodDefinitions.ContainsKey(good) ? Market.GoodDefinitions[good].BasePrice : 10.0);
                        double maxPrice = currentPrice * 1.2; 
                        city.BuyOrders.Add(new BuyOrder(pop, good, shortfall, maxPrice));
                    }
                    
                    int consumed = Math.Min(needed, available);
                    if (consumed > 0)
                    {
                        // Market.BuyFromCityMarket handles stockpile reduction and demand recording.
                        // For pop consumption, who is the buyer for budget purposes?
                        // Let's assume for now that direct pop consumption affects city.LocalDemand but not city/corp budgets directly, handled by general pop spending power.
                        // If we were to model pop budgets, this would change.
                        city.Stockpile[good].Quantity -= consumed; // Manually reduce stockpile here for direct consumption
                        if (city.LocalDemand.ContainsKey(good))
                            city.LocalDemand[good] += consumed;
                        else
                            city.LocalDemand[good] = consumed;
                    }

                    // Try to buy the shortfall from the market (simulates pops trying to fulfill remaining needs)
                    // This is a simplified representation. A more complex system might use the BuyOrders list.
                    if (shortfall > 0 && city.LocalPrices.ContainsKey(good)) 
                    {
                        double price = city.LocalPrices[good];
                        if (price > 0) // Ensure price is not zero to avoid division by zero or infinite affordable quantity
                        {
                            int affordable = (int)(popBudget / price);
                            int toBuy = Math.Min(shortfall, affordable);
                            if (toBuy > 0)
                            {
                                // Here, we simulate pops buying. We need to decide if this affects the city budget or a pop-specific budget.
                                // For now, let's assume pops are buying from the city stockpile. The city budget isn't directly credited here for simplicity,
                                // as the goods are already in its stockpile. The demand is the key signal.
                                // This would be a Market.BuyFromCityMarket(city, good, toBuy, buyerPop: pop) if pops had distinct budgets.
                                
                                // If we assume the city is the seller, and the pop is the buyer with abstract budget:
                                if(city.Stockpile.ContainsKey(good) && city.Stockpile[good].Quantity >= toBuy)
                                {
                                     city.Stockpile[good].Quantity -= toBuy;
                                     popBudget -= toBuy * price; // Pop's spending power reduced
                                     if (city.LocalDemand.ContainsKey(good))
                                         city.LocalDemand[good] += toBuy;
                                     else
                                         city.LocalDemand[good] = toBuy;
                                     shortfall -= toBuy;
                                }
                            }
                        }
                    }
                    unmet += shortfall;
                }
                pop.UnmetNeeds = unmet;
                // Adjust happiness for this class
                if (unmet == 0)
                    pop.Happiness = Math.Min(100, pop.Happiness + 2);
                else if (unmet < pop.Size / 10)
                    pop.Happiness = Math.Max(0, pop.Happiness - 1);
                else
                    pop.Happiness = Math.Max(0, pop.Happiness - 4);

                // Adjust income for unemployment
                double employedIncome = pop.IncomePerPerson;
                double unemployedIncome = pop.IncomePerPerson * 0.3; // 30% of normal income
                double avgIncome = (pop.Employed * employedIncome + pop.Unemployed * unemployedIncome) / Math.Max(1, pop.Size);
                pop.IncomePerPerson = avgIncome;

                // Adjust happiness for unemployment
                if (pop.Unemployed > 0)
                    pop.Happiness = Math.Max(0, pop.Happiness - pop.Unemployed * 2 / Math.Max(1, pop.Size));
            }

            // Pop class mobility
            for (int i = 0; i < city.PopClasses.Count; i++)
            {
                var pop = city.PopClasses[i];
                // Move up if happy and needs met
                if (pop.Happiness > 80 && pop.UnmetNeeds == 0 && pop.Size > 100)
                {
                    if (i < city.PopClasses.Count - 1)
                    {
                        int move = pop.Size / 50;
                        pop.Size -= move;
                        city.PopClasses[i + 1].Size += move;
                    }
                }
                // Move down if unhappy and many needs unmet
                if (pop.Happiness < 30 && pop.UnmetNeeds > pop.Size / 10 && pop.Size > 100)
                {
                    if (i > 0)
                    {
                        int move = pop.Size / 50;
                        pop.Size -= move;
                        city.PopClasses[i - 1].Size += move;
                    }
                }
            }

            // Sell surplus from city stockpile (now becomes ExportableSurplus)
            foreach (var good in new List<string>(city.Stockpile.Keys))
            {
                int buffer = 0;
                foreach (var pop in city.PopClasses)
                {
                    if (pop.Needs.ContainsKey(good))
                    {
                        double needPer1000 = pop.Needs[good];
                        buffer += (int)(Math.Max(1, pop.Size) * needPer1000 / 1000.0 * 2.0); // 2x needs as buffer
                        buffer = Math.Max(0, buffer);
                    }
                }
                if (city.Stockpile[good].Quantity > buffer)
                {
                    int surplusAmount = city.Stockpile[good].Quantity - buffer;
                    if (city.ExportableSurplus.ContainsKey(good))
                        city.ExportableSurplus[good] += surplusAmount;
                    else
                        city.ExportableSurplus[good] = surplusAmount;
                    
                    // REMOVED: Market.SellToCityMarket(city, good, surplusAmount, null); 
                    // The surplus is now earmarked for export, not sold back to local market immediately.
                }
            }

            // Generate sell orders for factories (for intended output, not just stockpile)
            foreach (var factory in city.Factories)
            {
                foreach (var output in factory.OutputGoods)
                {
                    string good = output.Name;
                    int possible = factory.ProductionCapacity; // Simplified: assume full capacity can be offered
                    // More complex: check inputs available to the factory owner corp (not city stockpile for this offer)

                    int offeredQuantity = output.Quantity * possible; 
                    if (offeredQuantity > 0) 
                    {
                        double currentPrice = city.LocalPrices.ContainsKey(good) ? city.LocalPrices[good] : (Market.GoodDefinitions.ContainsKey(good) ? Market.GoodDefinitions[good].BasePrice : 5.0);
                        double minPrice = currentPrice * 0.8; 
                        city.SellOrders.Add(new SellOrder(factory, good, offeredQuantity, minPrice));
                    }
                }
            }

            // Adjust happiness and growth based on unmet needs
            foreach (var pop in city.PopClasses)
            {
                if (pop.UnmetNeeds == 0 && pop.Happiness > 60)
                {
                    pop.Size += (int)(pop.Size * 0.002); // 0.2% growth
                }
                else if (pop.UnmetNeeds < pop.Size / 10 && pop.Happiness > 40)
                {
                    pop.Size += (int)(pop.Size * 0.0005); // 0.05% growth
                }
                else if (pop.Happiness < 30 && pop.UnmetNeeds > pop.Size / 10)
                {
                    pop.Size -= (int)(pop.Size * 0.001); // 0.1% decline
                    if (pop.Size < 0) pop.Size = 0;
                }
            }
        }
    }

    public class PopClass
    {
        public string Name { get; set; }
        public int Size { get; set; }
        public double IncomePerPerson { get; set; }
        public Dictionary<string, double> Needs { get; set; }
        public int UnmetNeeds { get; set; }
        public int Happiness { get; set; } // 0-100
        public int Employed { get; set; }
        public int Unemployed { get { return Size - Employed; } }

        // New QualityOfLife property
        public double QualityOfLife { get; private set; }

        public PopClass(string name, int size, double incomePerPerson)
        {
            Name = name;
            Size = size;
            IncomePerPerson = incomePerPerson;
            Needs = new Dictionary<string, double>();
            UnmetNeeds = 0;
            Happiness = 50;
            QualityOfLife = CalculateQualityOfLife();
        }

        // Method to calculate Quality of Life based on constant factors
        private double CalculateQualityOfLife()
        {
            double healthcare = 0.8; // Example constant value (0-1 scale)
            double education = 0.7;
            double housing = 1.0 - (UnmetNeeds / (Needs.Count > 0 ? Needs.Count : 1)); // Penalize unmet needs
            double employment = Size > 0 ? Employed / (double)Size : 0; // Employment rate, avoid division by zero

            DebugLogger.Log($"[CalculateQualityOfLife] Healthcare: {healthcare}, Education: {education}, Housing: {housing}, Employment: {employment}");

            // Weighted average of factors
            double qualityOfLife = (healthcare * 0.3) + (education * 0.3) + (housing * 0.2) + (employment * 0.2);
            DebugLogger.Log($"[CalculateQualityOfLife] Calculated QoL: {qualityOfLife}");

            return qualityOfLife;
        }

        // Method to update Quality of Life dynamically
        public void UpdateQualityOfLife()
        {
            QualityOfLife = CalculateQualityOfLife();
        }
    }

    public class BuyOrder
    {
        public PopClass Buyer { get; set; }
        public string Good { get; set; }
        public int Quantity { get; set; }
        public double MaxPrice { get; set; }
        public BuyOrder(PopClass buyer, string good, int quantity, double maxPrice)
        {
            Buyer = buyer;
            Good = good;
            Quantity = quantity;
            MaxPrice = maxPrice;
        }
    }

    public class SellOrder
    {
        public Factory Seller { get; set; }
        public string Good { get; set; }
        public int Quantity { get; set; }
        public double MinPrice { get; set; }
        public SellOrder(Factory seller, string good, int quantity, double minPrice)
        {
            Seller = seller;
            Good = good;
            Quantity = quantity;
            MinPrice = minPrice;
        }
    }

    public class FactoryBlueprint
    {
        public string FactoryTypeName { get; set; } 
        public Good OutputGood { get; set; } 
        public List<Good> InputGoods { get; set; }
        public Dictionary<string, double> DefaultJobSlotDistribution { get; set; } 
        public GoodCategory ProducedGoodCategory { get; set; } 

        public FactoryBlueprint(string typeName, Good output, List<Good> inputs, GoodCategory producedGoodCategory, Dictionary<string, double> jobSlots = null)
        {
            FactoryTypeName = typeName;
            OutputGood = output;
            InputGoods = inputs ?? new List<Good>();
            ProducedGoodCategory = producedGoodCategory;
            
            // Updated default job slots with a more comprehensive distribution
            DefaultJobSlotDistribution = jobSlots ?? new Dictionary<string, double> 
            { 
                { "Laborers", 0.5 },
                { "Craftsmen", 0.2 },
                { "Engineers", 0.15 },
                { "Managers", 0.1 },
                { "Clerks", 0.05 }
            };
        }
    }

    public static class FactoryBlueprints
    {
        public static List<FactoryBlueprint> AllBlueprints { get; private set; } = new List<FactoryBlueprint>();

        public static void InitializeBlueprints() 
        {
            AllBlueprints.Clear();

            // Define standard job distributions
            var basicResourceJobSlots = new Dictionary<string, double> 
            { 
                { "Laborers", 0.7 },
                { "Craftsmen", 0.15 },
                { "Engineers", 0.1 },
                { "Managers", 0.05 }
            };

            var advancedResourceJobSlots = new Dictionary<string, double> 
            { 
                { "Laborers", 0.5 },
                { "Craftsmen", 0.25 },
                { "Engineers", 0.15 },
                { "Managers", 0.1 }
            };

            var industrialJobSlots = new Dictionary<string, double> 
            { 
                { "Laborers", 0.4 },
                { "Craftsmen", 0.3 },
                { "Engineers", 0.2 },
                { "Managers", 0.1 }
            };

            // == 1. Define ALL Goods in Market.GoodDefinitions First ==
            // Existing Raw Materials (assuming these are already here from previous steps)
            Market.GoodDefinitions["Grain"] = new Good("Grain", 2.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Coal"] = new Good("Coal", 5.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Iron"] = new Good("Iron", 8.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Cotton"] = new Good("Cotton", 7.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Timber"] = new Good("Timber", 4.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Dyes"] = new Good("Dyes", 10.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Sulphur"] = new Good("Sulphur", 9.0, GoodCategory.RawMaterial);

            // Chunk 1: New Raw Materials Definitions
            Market.GoodDefinitions["Crude Oil"] = new Good("Crude Oil", 15.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Raw Rubber"] = new Good("Raw Rubber", 12.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Fish"] = new Good("Fish", 3.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Livestock"] = new Good("Livestock", 20.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Tea Leaves"] = new Good("Tea Leaves", 6.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Coffee Beans"] = new Good("Coffee Beans", 7.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Tobacco Leaf"] = new Good("Tobacco Leaf", 8.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Sugar Cane"] = new Good("Sugar Cane", 3.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Copper Ore"] = new Good("Copper Ore", 10.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Tin Ore"] = new Good("Tin Ore", 11.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Lead Ore"] = new Good("Lead Ore", 9.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Zinc Ore"] = new Good("Zinc Ore", 9.5, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Limestone"] = new Good("Limestone", 2.0, GoodCategory.RawMaterial);
            Market.GoodDefinitions["Salt"] = new Good("Salt", 2.5, GoodCategory.RawMaterial); 

            // Existing Industrial Inputs (assuming these are already here)
            Market.GoodDefinitions["Steel"] = new Good("Steel", 25.0, GoodCategory.IndustrialInput); 
            Market.GoodDefinitions["Fabric"] = new Good("Fabric", 18.0, GoodCategory.IndustrialInput); 
            Market.GoodDefinitions["Lumber"] = new Good("Lumber", 10.0, GoodCategory.IndustrialInput); 
            Market.GoodDefinitions["Paper"] = new Good("Paper", 12.0, GoodCategory.IndustrialInput); 

            // Chunk 2: New Industrial Inputs Definitions
            Market.GoodDefinitions["Refined Oil"] = new Good("Refined Oil", 40.0, GoodCategory.IndustrialInput); // Fuel, Kerosene
            Market.GoodDefinitions["Processed Rubber"] = new Good("Processed Rubber", 30.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Copper Ingots"] = new Good("Copper Ingots", 28.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Tin Ingots"] = new Good("Tin Ingots", 32.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Lead Ingots"] = new Good("Lead Ingots", 25.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Zinc Ingots"] = new Good("Zinc Ingots", 26.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Bronze Ingots"] = new Good("Bronze Ingots", 35.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Brass Ingots"] = new Good("Brass Ingots", 36.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Explosives"] = new Good("Explosives", 50.0, GoodCategory.IndustrialInput); // Also Military
            Market.GoodDefinitions["Fertilizer"] = new Good("Fertilizer", 22.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Cement"] = new Good("Cement", 15.0, GoodCategory.IndustrialInput);
            Market.GoodDefinitions["Basic Chemicals"] = new Good("Basic Chemicals", 20.0, GoodCategory.IndustrialInput);

            // Existing Capital Goods (assuming these are already here)
            Market.GoodDefinitions["Tools"] = new Good("Tools", 40.0, GoodCategory.CapitalGood); 
            Market.GoodDefinitions["Machine Parts"] = new Good("Machine Parts", 70.0, GoodCategory.CapitalGood);

            // Existing Processed Food (assuming this is already here)
            Market.GoodDefinitions["Bread"] = new Good("Bread", 6.0, GoodCategory.ProcessedFood);

            // Chunk 3: New Processed Food Definitions
            Market.GoodDefinitions["Canned Goods"] = new Good("Canned Goods", 15.0, GoodCategory.ProcessedFood); // Generic canned food (fish, meat, fruit)
            Market.GoodDefinitions["Processed Meat"] = new Good("Processed Meat", 28.0, GoodCategory.ProcessedFood);
            Market.GoodDefinitions["Tea"] = new Good("Tea", 15.0, GoodCategory.ConsumerProduct); // Categorized as Consumer by preference
            Market.GoodDefinitions["Coffee"] = new Good("Coffee", 16.0, GoodCategory.ConsumerProduct); // Categorized as Consumer
            Market.GoodDefinitions["Refined Sugar"] = new Good("Refined Sugar", 10.0, GoodCategory.ProcessedFood); // Also IndustrialInput

            // Existing Consumer Products (assuming these are already here)
            Market.GoodDefinitions["Cloth"] = new Good("Cloth", 25.0, GoodCategory.ConsumerProduct); 
            Market.GoodDefinitions["Furniture"] = new Good("Furniture", 70.0, GoodCategory.ConsumerProduct);
            Market.GoodDefinitions["Books"] = new Good("Books", 30.0, GoodCategory.ConsumerProduct);
            Market.GoodDefinitions["Luxury Clothes"] = new Good("Luxury Clothes", 100.0, GoodCategory.ConsumerProduct);

            // Chunk 3: New Consumer Product Definitions
            Market.GoodDefinitions["Cigars"] = new Good("Cigars", 22.0, GoodCategory.ConsumerProduct); // Or Cigarettes
            Market.GoodDefinitions["Automobiles"] = new Good("Automobiles", 1500.0, GoodCategory.ConsumerProduct); // Late game
            Market.GoodDefinitions["Telephones"] = new Good("Telephones", 200.0, GoodCategory.ConsumerProduct); // Late game

            // Chunk 3: Military Goods Definitions
            Market.GoodDefinitions["Small Arms"] = new Good("Small Arms", 120.0, GoodCategory.CapitalGood); // For military units
            Market.GoodDefinitions["Ammunition"] = new Good("Ammunition", 80.0, GoodCategory.IndustrialInput); // Consumed by military
            Market.GoodDefinitions["Artillery"] = new Good("Artillery", 700.0, GoodCategory.CapitalGood); // For military units

            // == 2. Define Factory Blueprints ==

            // --- Existing/Refined Basic Resource Extraction (from previous refactoring) ---
            AllBlueprints.Add(new FactoryBlueprint("Grain Farm", new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 3), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Coal Mine", new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Iron Mine", new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Cotton Plantation", new Good("Cotton", Market.GoodDefinitions["Cotton"].BasePrice, GoodCategory.RawMaterial, 3), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Logging Camp", new Good("Timber", Market.GoodDefinitions["Timber"].BasePrice, GoodCategory.RawMaterial, 4), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Dye Collection Post", new Good("Dyes", Market.GoodDefinitions["Dyes"].BasePrice, GoodCategory.RawMaterial, 1), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sulphur Mine", new Good("Sulphur", Market.GoodDefinitions["Sulphur"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));

            // Chunk 1: New Basic Resource Extraction Facility Blueprints
            AllBlueprints.Add(new FactoryBlueprint("Limestone Quarry", new Good("Limestone", Market.GoodDefinitions["Limestone"].BasePrice, GoodCategory.RawMaterial, 3), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Salt Mine", new Good("Salt", Market.GoodDefinitions["Salt"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Oil Derrick", new Good("Crude Oil", Market.GoodDefinitions["Crude Oil"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, advancedResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Rubber Plantation", new Good("Raw Rubber", Market.GoodDefinitions["Raw Rubber"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Fishing Wharf", new Good("Fish", Market.GoodDefinitions["Fish"].BasePrice, GoodCategory.RawMaterial, 4), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Cattle Ranch", new Good("Livestock", Market.GoodDefinitions["Livestock"].BasePrice, GoodCategory.RawMaterial, 1), new List<Good> {new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice,GoodCategory.RawMaterial, 2)}, GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tea Plantation", new Good("Tea Leaves", Market.GoodDefinitions["Tea Leaves"].BasePrice, GoodCategory.RawMaterial, 3), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Coffee Plantation", new Good("Coffee Beans", Market.GoodDefinitions["Coffee Beans"].BasePrice, GoodCategory.RawMaterial, 3), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tobacco Plantation", new Good("Tobacco Leaf", Market.GoodDefinitions["Tobacco Leaf"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sugar Plantation", new Good("Sugar Cane", Market.GoodDefinitions["Sugar Cane"].BasePrice, GoodCategory.RawMaterial, 4), new List<Good>(), GoodCategory.RawMaterial, basicResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Copper Mine", new Good("Copper Ore", Market.GoodDefinitions["Copper Ore"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, advancedResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tin Mine", new Good("Tin Ore", Market.GoodDefinitions["Tin Ore"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, advancedResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Lead Mine", new Good("Lead Ore", Market.GoodDefinitions["Lead Ore"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, advancedResourceJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Zinc Mine", new Good("Zinc Ore", Market.GoodDefinitions["Zinc Ore"].BasePrice, GoodCategory.RawMaterial, 2), new List<Good>(), GoodCategory.RawMaterial, advancedResourceJobSlots));

            // --- Existing/Refined Intermediate Goods (from previous refactoring) --- 
            AllBlueprints.Add(new FactoryBlueprint("Steel Mill", new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1)}, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sawmill", new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> {new Good("Timber", Market.GoodDefinitions["Timber"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Textile Mill", new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Cotton", Market.GoodDefinitions["Cotton"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Paper Mill", new Good("Paper", Market.GoodDefinitions["Paper"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1)}, GoodCategory.IndustrialInput, industrialJobSlots));

            // --- More goods and blueprints will be added in subsequent chunks ---
            // Placeholder for the rest of the existing/refined blueprints from previous step
            AllBlueprints.Add(new FactoryBlueprint("Tool Factory", new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 2), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Machine Parts Factory", new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Bakery", new Good("Bread", Market.GoodDefinitions["Bread"].BasePrice, GoodCategory.ProcessedFood, 3), new List<Good> { new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 2) }, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Clothing Factory", new Good("Cloth", Market.GoodDefinitions["Cloth"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2)}, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Furniture Factory", new Good("Furniture", Market.GoodDefinitions["Furniture"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 3), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Printing Press", new Good("Books", Market.GoodDefinitions["Books"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Paper", Market.GoodDefinitions["Paper"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Luxury Tailor", new Good("Luxury Clothes", Market.GoodDefinitions["Luxury Clothes"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 3), new Good("Dyes", Market.GoodDefinitions["Dyes"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));

            // Chunk 2: New Intermediate Goods Factory Blueprints
            AllBlueprints.Add(new FactoryBlueprint("Oil Refinery", new Good("Refined Oil", Market.GoodDefinitions["Refined Oil"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Crude Oil", Market.GoodDefinitions["Crude Oil"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Rubber Processor", new Good("Processed Rubber", Market.GoodDefinitions["Processed Rubber"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Raw Rubber", Market.GoodDefinitions["Raw Rubber"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Sulphur", Market.GoodDefinitions["Sulphur"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Copper Smelter", new Good("Copper Ingots", Market.GoodDefinitions["Copper Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Copper Ore", Market.GoodDefinitions["Copper Ore"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tin Smelter", new Good("Tin Ingots", Market.GoodDefinitions["Tin Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Tin Ore", Market.GoodDefinitions["Tin Ore"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Lead Smelter", new Good("Lead Ingots", Market.GoodDefinitions["Lead Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Lead Ore", Market.GoodDefinitions["Lead Ore"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Zinc Smelter", new Good("Zinc Ingots", Market.GoodDefinitions["Zinc Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Zinc Ore", Market.GoodDefinitions["Zinc Ore"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Bronze Foundry", new Good("Bronze Ingots", Market.GoodDefinitions["Bronze Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Copper Ingots", Market.GoodDefinitions["Copper Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Tin Ingots", Market.GoodDefinitions["Tin Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Brass Foundry", new Good("Brass Ingots", Market.GoodDefinitions["Brass Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Copper Ingots", Market.GoodDefinitions["Copper Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Zinc Ingots", Market.GoodDefinitions["Zinc Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Chemicals Plant", new Good("Basic Chemicals", Market.GoodDefinitions["Basic Chemicals"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Sulphur", Market.GoodDefinitions["Sulphur"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Salt", Market.GoodDefinitions["Salt"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Explosives Factory", new Good("Explosives", Market.GoodDefinitions["Explosives"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Basic Chemicals", Market.GoodDefinitions["Basic Chemicals"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Fertilizer Plant", new Good("Fertilizer", Market.GoodDefinitions["Fertilizer"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Basic Chemicals", Market.GoodDefinitions["Basic Chemicals"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Sulphur", Market.GoodDefinitions["Sulphur"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Cement Plant", new Good("Cement", Market.GoodDefinitions["Cement"].BasePrice, GoodCategory.IndustrialInput, 3), new List<Good> { new Good("Limestone", Market.GoodDefinitions["Limestone"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));

            // --- Existing/Refined Capital Goods (from previous refactoring) ---
            AllBlueprints.Add(new FactoryBlueprint("Tool Factory", new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 2), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Machine Parts Factory", new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.CapitalGood, industrialJobSlots));

            // --- Existing/Refined Processed Food (from previous refactoring) ---
            AllBlueprints.Add(new FactoryBlueprint("Bakery", new Good("Bread", Market.GoodDefinitions["Bread"].BasePrice, GoodCategory.ProcessedFood, 3), new List<Good> { new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 2) }, GoodCategory.ProcessedFood, industrialJobSlots));

            // Chunk 3: New Processed Food Factory Blueprints
            AllBlueprints.Add(new FactoryBlueprint("Cannery", new Good("Canned Goods", Market.GoodDefinitions["Canned Goods"].BasePrice, GoodCategory.ProcessedFood, 2), new List<Good> { new Good("Fish", Market.GoodDefinitions["Fish"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Tin Ingots", Market.GoodDefinitions["Tin Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1)}, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Meat Packing Plant", new Good("Processed Meat", Market.GoodDefinitions["Processed Meat"].BasePrice, GoodCategory.ProcessedFood, 1), new List<Good> { new Good("Livestock", Market.GoodDefinitions["Livestock"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Salt", Market.GoodDefinitions["Salt"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tea Factory", new Good("Tea", Market.GoodDefinitions["Tea"].BasePrice, GoodCategory.ConsumerProduct, 2), new List<Good> { new Good("Tea Leaves", Market.GoodDefinitions["Tea Leaves"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Coffee Roastery", new Good("Coffee", Market.GoodDefinitions["Coffee"].BasePrice, GoodCategory.ConsumerProduct, 2), new List<Good> { new Good("Coffee Beans", Market.GoodDefinitions["Coffee Beans"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sugar Mill", new Good("Refined Sugar", Market.GoodDefinitions["Refined Sugar"].BasePrice, GoodCategory.ProcessedFood, 3), new List<Good> { new Good("Sugar Cane", Market.GoodDefinitions["Sugar Cane"].BasePrice, GoodCategory.RawMaterial, 4), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ProcessedFood, industrialJobSlots));

            // --- Existing/Refined Consumer Goods (from previous refactoring) ---
            AllBlueprints.Add(new FactoryBlueprint("Clothing Factory", new Good("Cloth", Market.GoodDefinitions["Cloth"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2)}, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Furniture Factory", new Good("Furniture", Market.GoodDefinitions["Furniture"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 3), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Printing Press", new Good("Books", Market.GoodDefinitions["Books"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Paper", Market.GoodDefinitions["Paper"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Luxury Tailor", new Good("Luxury Clothes", Market.GoodDefinitions["Luxury Clothes"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 3), new Good("Dyes", Market.GoodDefinitions["Dyes"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));

            // Chunk 3: New Consumer Goods Factory Blueprints
            AllBlueprints.Add(new FactoryBlueprint("Tobacco Factory", new Good("Cigars", Market.GoodDefinitions["Cigars"].BasePrice, GoodCategory.ConsumerProduct, 2), new List<Good> { new Good("Tobacco Leaf", Market.GoodDefinitions["Tobacco Leaf"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Paper", Market.GoodDefinitions["Paper"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Automobile Plant", new Good("Automobiles", Market.GoodDefinitions["Automobiles"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 5), new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 3), new Good("Processed Rubber", Market.GoodDefinitions["Processed Rubber"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Refined Oil", Market.GoodDefinitions["Refined Oil"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Electronics Plant", new Good("Telephones", Market.GoodDefinitions["Telephones"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Copper Ingots", Market.GoodDefinitions["Copper Ingots"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Processed Rubber", Market.GoodDefinitions["Processed Rubber"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 1), new Good("Basic Chemicals", Market.GoodDefinitions["Basic Chemicals"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            
            // Chunk 3: Military Goods Factory Blueprints
            AllBlueprints.Add(new FactoryBlueprint("Arms Factory", new Good("Small Arms", Market.GoodDefinitions["Small Arms"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Munitions Plant", new Good("Ammunition", Market.GoodDefinitions["Ammunition"].BasePrice, GoodCategory.IndustrialInput, 5), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Explosives", Market.GoodDefinitions["Explosives"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Brass Ingots", Market.GoodDefinitions["Brass Ingots"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Artillery Plant", new Good("Artillery", Market.GoodDefinitions["Artillery"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 10), new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 5), new Good("Bronze Ingots", Market.GoodDefinitions["Bronze Ingots"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2)}, GoodCategory.CapitalGood, industrialJobSlots));
        }

        public static FactoryBlueprint GetBlueprintForGood(string goodName) // To build factory producing a specific good
        {
            return AllBlueprints.FirstOrDefault(bp => bp.OutputGood.Name == goodName);
        }

        public static FactoryBlueprint GetBlueprintBySpecialization(CorporationSpecialization spec, GoodCategory categoryHint, Random random) // For AI to pick a blueprint based on its specialty
        {
            List<FactoryBlueprint> suitableBlueprints = new List<FactoryBlueprint>();
            switch (spec)
            {
                case CorporationSpecialization.Agriculture:
                    suitableBlueprints = AllBlueprints.Where(bp => bp.ProducedGoodCategory == GoodCategory.RawMaterial && bp.OutputGood.Name == "Grain" || 
                                                                  bp.ProducedGoodCategory == GoodCategory.ProcessedFood).ToList();
                    break;
                case CorporationSpecialization.Mining:
                    suitableBlueprints = AllBlueprints.Where(bp => bp.ProducedGoodCategory == GoodCategory.RawMaterial && (bp.OutputGood.Name == "Coal" || bp.OutputGood.Name == "Iron")).ToList();
                    break;
                case CorporationSpecialization.HeavyIndustry:
                    suitableBlueprints = AllBlueprints.Where(bp => bp.ProducedGoodCategory == GoodCategory.IndustrialInput || 
                                                                  (bp.ProducedGoodCategory == GoodCategory.CapitalGood && bp.OutputGood.Name == "Tools")).ToList();
                    break;
                case CorporationSpecialization.LightIndustry:
                    suitableBlueprints = AllBlueprints.Where(bp => bp.ProducedGoodCategory == GoodCategory.ConsumerProduct && bp.OutputGood.Name != "Bread").ToList();
                    break;
                case CorporationSpecialization.Diversified:
                default:
                    // Diversified could pick based on a category hint or more broadly
                    if (categoryHint != default(GoodCategory)) // default(GoodCategory) is RawMaterial, so be careful if that's a valid hint
                    {
                         suitableBlueprints = AllBlueprints.Where(bp => bp.ProducedGoodCategory == categoryHint).ToList();
                    }
                    if (!suitableBlueprints.Any())
                    {
                        suitableBlueprints = AllBlueprints.ToList(); // Pick any
                    }
                    break;
            }
            if (suitableBlueprints.Any())
            {
                return suitableBlueprints[random.Next(suitableBlueprints.Count)];
            }
            return AllBlueprints.Any() ? AllBlueprints[random.Next(AllBlueprints.Count)] : null; // Absolute fallback
        }
    }
}