using System.Collections.Generic;
using System.Linq;
using System; // Added for Console

namespace Economy_sim
{
    public class City
    {
        public enum TradeOrderKind
        {
            Buy,
            Sell
        }

        public sealed record TradeOrderRecord(
            DateTime Timestamp,
            TradeOrderKind Kind,
            string Good,
            int Quantity,
            double Price,
            string? Counterparty);

        private readonly List<Factory> factories = new();
        private readonly object factoriesLock = new object();

        private readonly List<ResourceExtractionBuilding> resourceExtractionBuildings = new();
        private readonly object rebLock = new object();

        public string Name { get; set; }
        public double Budget { get; set; }
        public int Population { get; set; }
        public double TaxRate { get; set; } // Percentage (e.g., 0.1 for 10%)
        public double CityExpenses { get; set; }
        
        /// <summary>
        /// Thread-safe access to factories. Returns a snapshot of the current factories list.
        /// </summary>
        public List<Factory> Factories
        {
            get
            {
                lock (factoriesLock)
                {
                    return new List<Factory>(factories);
                }
            }
        }

        // Lightweight in-memory order history for UI (per building ref). Bounded to avoid unbounded growth.
        private readonly Dictionary<object, List<TradeOrderRecord>> _orderHistory = new();
        private readonly object _orderHistoryLock = new();
        private const int MaxOrderHistoryPerBuilding = 50;

        public IReadOnlyList<TradeOrderRecord> GetOrderHistoryForBuilding(object building)
        {
            if (building == null) return Array.Empty<TradeOrderRecord>();

            lock (_orderHistoryLock)
            {
                if (_orderHistory.TryGetValue(building, out var list))
                {
                    return list.ToList();
                }

                return Array.Empty<TradeOrderRecord>();
            }
        }

        public void RecordBuildingOrder(object building, TradeOrderKind kind, string good, int quantity, double price, string? counterparty = null)
        {
            if (building == null) return;
            if (string.IsNullOrWhiteSpace(good)) return;
            if (quantity <= 0) return;

            lock (_orderHistoryLock)
            {
                if (!_orderHistory.TryGetValue(building, out var list))
                {
                    list = new List<TradeOrderRecord>();
                    _orderHistory[building] = list;
                }

                list.Insert(0, new TradeOrderRecord(DateTime.UtcNow, kind, good, quantity, price, counterparty));
                if (list.Count > MaxOrderHistoryPerBuilding)
                {
                    list.RemoveRange(MaxOrderHistoryPerBuilding, list.Count - MaxOrderHistoryPerBuilding);
                }
            }
        }
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

            foreach (var factory in city.Factories)
            {
                foreach (var input in factory.InputGoods)
                {
                    if (!city.Stockpile.ContainsKey(input.Name))
                    {
                        city.Stockpile[input.Name] = new Good(input.Name, input.BasePrice, input.Category, 0);
                    }

                    // Calculate import needs - what factories need to produce
                    int inputNeeded = input.Quantity * factory.ProductionCapacity;
                    int currentStock = city.Stockpile.ContainsKey(input.Name) ? city.Stockpile[input.Name].Quantity : 0;
                    int shortage = Math.Max(0, inputNeeded - currentStock);

                    if (shortage > 0)
                    {
                        if (city.ImportNeeds.ContainsKey(input.Name))
                            city.ImportNeeds[input.Name] += shortage;
                        else
                            city.ImportNeeds[input.Name] = shortage;
                    }
                }

                foreach (var output in factory.OutputGoods)
                {
                    if (!city.Stockpile.ContainsKey(output.Name))
                    {
                        city.Stockpile[output.Name] = new Good(output.Name, output.BasePrice, output.Category, 0);
                    }
                }
            }

            foreach (var factory in city.Factories)
            {
                factory.Produce(city.Stockpile, city);
            }

            var baseIncomeByPop = new Dictionary<PopClass, double>();

            foreach (var pop in city.PopClasses)
            {
                DebugLogger.Log($"[Employment Debug] Population Class: {pop.Name}, Size: {pop.Size}, Initial Employed: {pop.Employed}", DebugLogger.LogCategory.Pop);
                pop.Size = Math.Max(1, pop.Size);
                pop.IncomePerPerson = Math.Max(0.01, pop.IncomePerPerson);
                baseIncomeByPop[pop] = pop.IncomePerPerson;
                pop.Employed = 0;
            }

            foreach (var factory in city.Factories)
            {
                if (factory.JobSlots == null || factory.JobSlots.Count == 0)
                {
                    DebugLogger.Log($"[Warning] Factory '{factory.Name}' has no job slots defined.", DebugLogger.LogCategory.Building);
                    continue;
                }

                DebugLogger.Log($"[Employment Debug] Factory: {factory.Name}, Job Slots: {string.Join(", ", factory.JobSlots.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}", DebugLogger.LogCategory.Building);
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
                DebugLogger.Log($"[Employment Debug] Job Type: {jobType}, Total Available Slots: {totalAvailableSlots[jobType]}", DebugLogger.LogCategory.Building);
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
                    DebugLogger.Log($"[Employment Debug] Population Class: {pop.Name}, Newly Employed: {canBeEmployed}, Total Employed: {pop.Employed}", DebugLogger.LogCategory.Pop);
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
                            DebugLogger.Log($"[Employment Debug] Factory: {factory.Name}, Job Type: {jobType}, Newly Assigned: {canAssignToFactory}, Total Workers Employed: {factory.WorkersEmployed}", DebugLogger.LogCategory.Building);
                            assignedToFactories -= canAssignToFactory;
                        }
                    }
                }

                if (!city.PopClasses.Any(p => p.Name == jobType))
                {
                    DebugLogger.Log($"[Warning] No population class matches job type '{jobType}'.", DebugLogger.LogCategory.Building);
                }
            }

            foreach (var pop in city.PopClasses)
            {
                double baseIncome = baseIncomeByPop.TryGetValue(pop, out double recordedIncome)
                    ? recordedIncome
                    : Math.Max(0.01, pop.IncomePerPerson);

                double employedIncome = baseIncome;
                double unemployedIncome = baseIncome * 0.3;
                double avgIncome = (pop.Employed * employedIncome + pop.Unemployed * unemployedIncome) / Math.Max(1, pop.Size);
                pop.IncomePerPerson = avgIncome;
                pop.UpdateQualityOfLife();
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
                                // If we were to model pop budgets, this would change.

                                // If we assume the city is the seller, and the pop is the buyer with abstract budget:
                                if (city.Stockpile.ContainsKey(good) && city.Stockpile[good].Quantity >= toBuy)
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

                // Adjust happiness for this class
                if (unmet == 0)
                    pop.Happiness = Math.Min(100, pop.Happiness + 2);
                else if (unmet < pop.Size / 10)
                    pop.Happiness = Math.Max(0, pop.Happiness - 1);
                else
                    pop.Happiness = Math.Max(0, pop.Happiness - 4);

                // Adjust happiness for unemployment
                if (pop.Unemployed > 0)
                    pop.Happiness = Math.Max(0, pop.Happiness - pop.Unemployed * 2 / Math.Max(1, pop.Size));
            }

            // Pop class mobility
            for (int i = 0; i < city.PopClasses.Count; i++)
            {
                var pop = city.PopClasses[i];
                // Move up if happy and needs met
                if (pop.Happiness > 80 && pop.Size > 100)
                {
                    if (i < city.PopClasses.Count - 1)
                    {
                        int move = pop.Size / 50;
                        pop.Size -= move;
                        city.PopClasses[i + 1].Size += move;
                    }
                }
                // Move down if unhappy and many needs unmet
                if (pop.Happiness < 30 && pop.Size > 100)
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
                        city.RecordBuildingOrder(factory, TradeOrderKind.Sell, good, offeredQuantity, minPrice);
                        Console.WriteLine($"[Sell Order] Factory '{factory.Name}' offers {offeredQuantity} of '{good}' at min price {minPrice:F2}.");
                    }
                }
            }

            // Adjust happiness and growth based on unmet needs
            foreach (var pop in city.PopClasses)
            {
                if (pop.Happiness > 60)
                {
                    pop.Size += (int)(pop.Size * 0.002); // 0.2% growth
                }
                else if (pop.Happiness > 40)
                {
                    pop.Size += (int)(pop.Size * 0.0005); // 0.05% growth
                }
                else if (pop.Happiness < 30)
                {
                    pop.Size -= (int)(pop.Size * 0.001); // 0.1% decline
                    if (pop.Size < 0) pop.Size = 0;
                }
            }
        }
        /// <summary>
        /// Adds a factory to the city in a thread-safe manner.
        /// </summary>
        public void AddFactory(Factory factory)
        {
            if (factory == null) return;

            lock (factoriesLock)
            {
                if (!factories.Contains(factory))
                {
                    factories.Add(factory);
                }
            }
        }

        /// <summary>
        /// Removes a factory from the city in a thread-safe manner.
        /// </summary>
        public bool RemoveFactory(Factory factory)
        {
            if (factory == null) return false;

            lock (factoriesLock)
            {
                return factories.Remove(factory);
            }
        }

        /// <summary>
        /// Gets the count of factories in a thread-safe manner.
        /// </summary>
        public int FactoryCount
        {
            get
            {
                lock (factoriesLock)
                {
                    return factories.Count;
                }
            }
        }

        /// <summary>
        /// Thread-safe access to resource extraction buildings. Returns a snapshot of the current list.
        /// </summary>
        public List<ResourceExtractionBuilding> ResourceExtractionBuildings
        {
            get
            {
                lock (rebLock)
                {
                    return new List<ResourceExtractionBuilding>(resourceExtractionBuildings);
                }
            }
        }

        /// <summary>
        /// Adds a resource extraction building to the city in a thread-safe manner.
        /// </summary>
        public void AddResourceExtractionBuilding(ResourceExtractionBuilding building)
        {
            if (building == null) return;

            lock (rebLock)
            {
                if (!resourceExtractionBuildings.Contains(building))
                {
                    resourceExtractionBuildings.Add(building);
                }
            }
        }

        /// <summary>
        /// Removes a resource extraction building from the city in a thread-safe manner.
        /// </summary>
        public bool RemoveResourceExtractionBuilding(ResourceExtractionBuilding building)
        {
            if (building == null) return false;

            lock (rebLock)
            {
                return resourceExtractionBuildings.Remove(building);
            }
        }

        /// <summary>
        /// Gets the count of resource extraction buildings in a thread-safe manner.
        /// </summary>
        public int ResourceExtractionBuildingCount
        {
            get
            {
                lock (rebLock)
                {
                    return resourceExtractionBuildings.Count;
                }
            }
        }

        public Dictionary<string, Good> Stockpile { get; set; }
        
        // Local market data
        public Dictionary<string, double> LocalPrices { get; set; }
        public Dictionary<string, int> LocalSupply { get; set; } // Supply generated this turn in this city
        public Dictionary<string, int> LocalDemand { get; set; } // Demand generated this turn in this city

        // For Inter-City Trade
        public Dictionary<string, int> ExportableSurplus { get; set; }
        public Dictionary<string, int> ImportNeeds { get; set; }

        public int Happiness { get; set; }
        public double PopBudget { get; set; }
        public List<PopClass> PopClasses { get; set; }
        public List<BuyOrder> BuyOrders { get; set; }
        public List<SellOrder> SellOrders { get; set; }
        public List<Suburb> Suburbs { get; private set; } // Added Suburbs property
        public List<ConstructionProject> ActiveProjects { get; private set; } // Added to track active construction projects
        public List<ConstructionCompany> ConstructionCompanies { get; } = new();
        public CityProceduralData ProceduralData { get; set; }

        public City(string name)
        {
            Name = name;
            Budget = 0; // Example starting budget
            Population = 0; // Example starting population
            Stockpile = new Dictionary<string, Good>();
            Happiness = 50; // Out of 100
            PopBudget = 0; // Example: $0.05 per person per turn
            PopClasses = InitializeDefaultPopClasses(Population);
            BuyOrders = new List<BuyOrder>();
            SellOrders = new List<SellOrder>();
            Suburbs = new List<Suburb>(); // Initialize suburbs
            ActiveProjects = new List<ConstructionProject>(); // Initialize active projects

            // Initialize local market data structures
            LocalPrices = new Dictionary<string, double>();
            LocalSupply = new Dictionary<string, int>();
            LocalDemand = new Dictionary<string, int>();
            ExportableSurplus = new Dictionary<string, int>();
            ImportNeeds = new Dictionary<string, int>();

            // Initialize local prices from global good definitions and supply/demand to 0
            // This requires Market.GoodDefinitions to be populated before cities are created.
            if (Market.GoodDefinitions != null && Market.GoodDefinitions.Any()) // Ensure definitions are loaded
            {
                foreach (var goodDefPair in Market.GoodDefinitions)
                {
                    LocalPrices[goodDefPair.Key] = goodDefPair.Value.BasePrice;
                    LocalSupply[goodDefPair.Key] = 0;
                    LocalDemand[goodDefPair.Key] = 0;
                    ExportableSurplus[goodDefPair.Key] = 0;
                    ImportNeeds[goodDefPair.Key] = 0;
                }
            }
            else
            {
                // Fallback or warning if Market.GoodDefinitions is not ready - this should not happen in normal flow
                Console.WriteLine($"Warning: Market.GoodDefinitions not populated when creating city {Name}. Local market data may be incomplete.");
            }

            // Create population classes with names matching job types
        }

        private List<PopClass> InitializeDefaultPopClasses(int basePopulation)
        {
            DebugLogger.Log($"[City] Creating population classes for city: {Name}", DebugLogger.LogCategory.Pop);

            var classes = new List<PopClass>();

            var laborers = new PopClass("Laborers", (int)(basePopulation * 0.5), 0.03);
            laborers.Needs["Bread"] = 2.0;
            laborers.Needs["Furniture"] = 1.0;
            laborers.Needs["Cloth"] = 0.5;
            classes.Add(laborers);
            DebugLogger.Log($"[City] Created Laborers class - Size: {laborers.Size}, Income: {laborers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var craftsmen = new PopClass("Craftsmen", (int)(basePopulation * 0.25), 0.06);
            craftsmen.Needs["Bread"] = 3.0;
            craftsmen.Needs["Furniture"] = 1.5;
            craftsmen.Needs["Cloth"] = 1.0;
            craftsmen.Needs["Luxury Clothes"] = 0.2;
            classes.Add(craftsmen);
            DebugLogger.Log($"[City] Created Craftsmen class - Size: {craftsmen.Size}, Income: {craftsmen.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var engineers = new PopClass("Engineers", (int)(basePopulation * 0.15), 0.12);
            engineers.Needs["Bread"] = 4.0;
            engineers.Needs["Furniture"] = 2.0;
            engineers.Needs["Cloth"] = 1.5;
            engineers.Needs["Luxury Clothes"] = 0.5;
            engineers.Needs["Books"] = 1.0;
            classes.Add(engineers);
            DebugLogger.Log($"[City] Created Engineers class - Size: {engineers.Size}, Income: {engineers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var managers = new PopClass("Managers", (int)(basePopulation * 0.07), 0.15);
            managers.Needs["Bread"] = 5.0;
            managers.Needs["Furniture"] = 3.0;
            managers.Needs["Cloth"] = 2.0;
            managers.Needs["Luxury Clothes"] = 1.0;
            managers.Needs["Books"] = 1.5;
            classes.Add(managers);
            DebugLogger.Log($"[City] Created Managers class - Size: {managers.Size}, Income: {managers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var clerks = new PopClass("Clerks", (int)(basePopulation * 0.03), 0.08);
            clerks.Needs["Bread"] = 3.5;
            clerks.Needs["Furniture"] = 2.0;
            clerks.Needs["Cloth"] = 1.2;
            clerks.Needs["Luxury Clothes"] = 0.3;
            clerks.Needs["Books"] = 0.5;
            classes.Add(clerks);
            DebugLogger.Log($"[City] Created Clerks class - Size: {clerks.Size}, Income: {clerks.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            return classes;
        }

        public void SimulateGrowth()
        {
            double surplus = Budget - CityExpenses;
            if (surplus <= 0)
            {
                HandleOvercrowding();
                return;
            }

            int baseGrowth = (int)(surplus / 1000);
            if (baseGrowth <= 0)
            {
                HandleOvercrowding();
                return;
            }

            // Use long to prevent overflow, and ensure all pop sizes are non-negative
            long totalPopSize = 0;
            try
            {
                foreach (var p in PopClasses)
                {
                    if (p.Size < 0)
                    {
                        p.Size = 0; // Clamp negative populations to 0
                    }
                    totalPopSize += p.Size;
                }
            }
            catch (OverflowException)
            {
                // If we still overflow, cap at max int
                totalPopSize = int.MaxValue;
            }

            int currentPopulation = Math.Max(1, (int)Math.Min(totalPopSize, int.MaxValue));

            int allowedGrowth = baseGrowth;

            if (ProceduralData != null)
            {
                int maxPopulation = ProceduralData.TotalResidentialCapacity;
                if (maxPopulation > 0)
                {
                    int availableCapacity = Math.Max(0, maxPopulation - currentPopulation);
                    allowedGrowth = Math.Min(baseGrowth, availableCapacity);

                    if (availableCapacity <= 0)
                    {
                        HandleOvercrowding();
                        return;
                    }
                }
            }

            if (allowedGrowth <= 0)
            {
                HandleOvercrowding();
                return;
            }

            Population = currentPopulation + allowedGrowth;

            foreach (var pop in PopClasses)
            {
                int popGrowth = (int)Math.Round(allowedGrowth * (pop.Size / (double)Math.Max(1, currentPopulation)));
                pop.Size += popGrowth;
            }

            Budget += surplus * 0.05;
        }

        private void HandleOvercrowding()
        {
            if (ProceduralData == null)
            {
                return;
            }

            int totalCapacity = ProceduralData.TotalResidentialCapacity;
            if (totalCapacity <= 0)
            {
                return;
            }

            // Recalculate current population safely
            long totalPopSize = 0;
            foreach (var p in PopClasses)
            {
                if (p.Size < 0) p.Size = 0;
                totalPopSize += p.Size;
            }
            int currentPopulation = (int)Math.Min(totalPopSize, int.MaxValue);

            if (currentPopulation <= totalCapacity)
            {
                return;
            }

            int overflow = currentPopulation - totalCapacity;
            int reduction = Math.Max(1, overflow / Math.Max(1, PopClasses.Count));

            foreach (var pop in PopClasses)
            {
                pop.Size = Math.Max(0, pop.Size - reduction);
            }

            totalPopSize = 0;
            foreach (var p in PopClasses)
            {
                totalPopSize += p.Size;
            }
            Population = (int)Math.Min(totalPopSize, int.MaxValue);
            Happiness = Math.Max(0, Happiness - 2);
        }

        public void AddSuburb(Suburb suburb)
        {
            Suburbs.Add(suburb);
        }

        public double CalculateCityQualityOfLife()
        {
            double suburbQoL = 0;
            if (Suburbs.Count > 0)
            {
                foreach (var suburb in Suburbs)
                {
                    suburbQoL += suburb.CalculateQualityOfLife();
                }
                suburbQoL /= Suburbs.Count; // Average QoL across suburbs
            }

            double popClassQoL = 0;
            if (PopClasses.Count > 0)
            {
                foreach (var pop in PopClasses)
                {
                    popClassQoL += pop.QualityOfLife;
                }
                popClassQoL /= PopClasses.Count; // Average QoL across population classes
            }

            DebugLogger.Log($"[CalculateCityQualityOfLife] Suburb QoL: {suburbQoL}, Population Class QoL: {popClassQoL}", DebugLogger.LogCategory.Economy);

            double cityQoL = (suburbQoL + popClassQoL) / 2; // Combine with equal weight
            DebugLogger.Log($"[CalculateCityQualityOfLife] Calculated City QoL: {cityQoL}", DebugLogger.LogCategory.Economy);

            return cityQoL;
        }

        public void RegisterConstructionCompany(ConstructionCompany company)
        {
            if (company == null)
            {
                return;
            }

            if (!ConstructionCompanies.Contains(company))
            {
                ConstructionCompanies.Add(company);
            }
        }

        public void StartConstructionProject(ConstructionProject project, ConstructionCompany company = null)
        {
            if (project == null)
            {
                return;
            }

            if (!ActiveProjects.Contains(project))
            {
                project.OwningCity = this;
                if (company != null)
                {
                    project.AssignedCompany = company;
                    RegisterConstructionCompany(company);
                    if (!company.Projects.Contains(project))
                    {
                        company.Projects.Add(project);
                    }
                }
                else
                {
                    project.AssignedCompany = null;
                }
                ActiveProjects.Add(project);
            }
        }

        public void ProgressConstruction()
        {
            if (ActiveProjects.Count == 0)
            {
                return;
            }

            bool progressMade = false;

            foreach (var project in ActiveProjects.ToList())
            {
                if (project.AssignedCompany != null)
                {
                    continue;
                }

                var dailyCost = CalculateDailyCost(project);
                if (dailyCost <= 0m)
                {
                    continue;
                }

                var cityBudget = (decimal)Budget;
                if (cityBudget < dailyCost || project.BudgetRemaining < dailyCost)
                {
                    continue;
                }

                if (project.ProgressProject(1, dailyCost))
                {
                    Budget -= (double)dailyCost;
                    progressMade = true;

                    if (project.IsComplete())
                    {
                        ApplyProjectEffects(project);
                        ActiveProjects.Remove(project);
                        progressMade = true;
                    }
                }
            }

            foreach (var project in ActiveProjects.ToList())
            {
                if (project.AssignedCompany != null && project.IsComplete())
                {
                    ApplyProjectEffects(project);
                    ActiveProjects.Remove(project);
                    progressMade = true;
                }
            }

            if (progressMade)
            {
                Economy.RaiseConstructionProgressed(this);
            }
        }

        private static decimal CalculateDailyCost(ConstructionProject project)
        {
            return project.Duration > 0 ? project.Budget / project.Duration : 0m;
        }

        private void ApplyProjectEffects(ConstructionProject project)
        {
            switch (project.Type)
            {
                case ProjectType.Housing:
                    IncreaseHousingCapacity((int)project.Output);
                    break;
                case ProjectType.Railway:
                    IncreaseRailwayKilometers(project.Output);
                    break;
                case ProjectType.Factory:
                    BoostIndustrialOutput(project.Output);
                    break;
                case ProjectType.Road:
                    ImproveTransportation(project.Output);
                    break;
                case ProjectType.Bridge:
                    ImproveTransportation(project.Output * 1.5);
                    break;
                case ProjectType.Port:
                    EnhanceTradeCapacity(project.Output);
                    break;
                case ProjectType.Airport:
                    EnhanceTradeCapacity(project.Output * 1.2);
                    break;
            }
        }

        private void IncreaseHousingCapacity(int value)
        {
            foreach (var suburb in Suburbs)
            {
                suburb.HousingCapacity += value / Suburbs.Count; // Distribute housing capacity
            }
        }

        private void IncreaseRailwayKilometers(double value)
        {
            foreach (var suburb in Suburbs)
            {
                suburb.RailwayKilometers += value / Suburbs.Count; // Distribute railway kilometers
            }
        }

        private void BoostIndustrialOutput(double factor)
        {
            Budget += factor * 5000;
        }

        private void ImproveTransportation(double factor)
        {
            CityExpenses = Math.Max(0, CityExpenses - factor * 10);
        }

        private void EnhanceTradeCapacity(double factor)
        {
            Budget += factor * 3500;
        }
    }
}