using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Economy_sim
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
        public static event Action<City> ConstructionProgressed;

        internal static void RaiseConstructionProgressed(City city)
        {
            ConstructionProgressed?.Invoke(city);
        }

        /// <summary>
        /// Initialize the world economy using procedural generation with city templates
        /// </summary>
        /// <param name="numCountries">Number of countries to generate (default: uses all available country templates)</param>
        /// <param name="numStatesPerCountry">Number of states per country</param>
        /// <param name="numCitiesPerState">Base number of cities per state</param>
        /// <param name="seed">Random seed for reproducible generation (null for random)</param>
        /// <returns>Tuple of (countries, corporations) lists</returns>
        public static void InitializeWorldEconomy(int? seed = null)
        {
            Console.WriteLine("[Economy Init] Starting procedural world economy initialization...");
            

            // Initialize factory blueprints and goods
            if (!Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints();
                ResourceExtractionBuildingBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods, {FactoryBlueprints.AllBlueprints.Count} factory blueprints, and {ResourceExtractionBuildingBlueprints.AllBlueprints.Count} REB blueprints");
            }
            else if (ResourceExtractionBuildingBlueprints.AllBlueprints.Count == 0)
            {
                ResourceExtractionBuildingBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {ResourceExtractionBuildingBlueprints.AllBlueprints.Count} REB blueprints (goods already loaded)");
            }

            // Generate world structure data
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
        

           

            // Use procedural generation with templates to create the actual world
           

            // Set up construction companies
            var constructionCompanies = new List<ConstructionCompany>();
           

            // Register corporations in global market
            Market.AllCorporations.Clear();
           

            Market.AllConstructionCompanies.Clear();
            Market.AllConstructionCompanies.AddRange(constructionCompanies);

            Console.WriteLine($"[Economy Init] Economy initialization complete!");

           
           
        }


        /// <summary>
        /// Generate world economy from map data (countries and states from the political map rendering system)
        /// </summary>
        /// <param name="mapCountries">List of country features from the map</param>
        /// <param name="mapStates">List of state features from the map</param>
        /// <param name="seed">Random seed for reproducible generation (null for random)</param>
        /// <returns>Tuple of (countries, corporations) lists</returns>
        public static (List<Country> countries, List<Corporation> corporations) GenerateWorldEconomyFromMapData(
            IReadOnlyList<IndexedCountryFeature> mapCountries,
            List<StateBorderManager.StateFeature> mapStates,
            List<HybridMapManager.GeographicCityInfo>? geographicCities = null,
            int? seed = null)
        {
            Console.WriteLine($"[Economy Init] Generating world economy from map data...");
            Console.WriteLine($"[Economy Init] Map data: {mapCountries?.Count ?? 0} countries, {mapStates?.Count ?? 0} states");

            // Initialize factory blueprints and goods
            if (!Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints();
                ResourceExtractionBuildingBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods, {FactoryBlueprints.AllBlueprints.Count} factory blueprints, and {ResourceExtractionBuildingBlueprints.AllBlueprints.Count} REB blueprints");
            }
            else if (ResourceExtractionBuildingBlueprints.AllBlueprints.Count == 0)
            {
                // Goods already loaded but REB blueprints not initialized
                ResourceExtractionBuildingBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {ResourceExtractionBuildingBlueprints.AllBlueprints.Count} REB blueprints (goods already loaded)");
            }

            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            var allCountries = new List<Country>();
            var allCorporations = new List<Corporation>();

            // If no states, we can't generate anything
            if (mapStates == null || mapStates.Count == 0)
            {
                Console.WriteLine($"[Economy Init] ERROR: No states provided, cannot generate economy from map data");
                return (allCountries, allCorporations);
            }

            // Group states by country
            var statesByCountry = new Dictionary<string, List<StateBorderManager.StateFeature>>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in mapStates)
            {
                if (string.IsNullOrWhiteSpace(state.CountryName))
                {
                    Console.WriteLine($"[Economy Init] Skipping state with no country name: {state.StateName}");
                    continue;
                }

                if (!statesByCountry.ContainsKey(state.CountryName))
                {
                    statesByCountry[state.CountryName] = new List<StateBorderManager.StateFeature>();
                }
                statesByCountry[state.CountryName].Add(state);
            }

            Console.WriteLine($"[Economy Init] Grouped states into {statesByCountry.Count} countries");

            // If no valid countries in map data, use country names from states
            var countriesToProcess = new List<string>();
            if (mapCountries != null && mapCountries.Count > 0)
            {
                // Use provided countries
                countriesToProcess.AddRange(mapCountries.Select(c => c.CountryName).Where(n => !string.IsNullOrWhiteSpace(n)));
            }
            else
            {
                // Use countries derived from states
                countriesToProcess.AddRange(statesByCountry.Keys);
                Console.WriteLine($"[Economy Init] No explicit countries provided, using {countriesToProcess.Count} countries from states");
            }

            // Generate countries
            foreach (var countryName in countriesToProcess)
            {
                if (string.IsNullOrWhiteSpace(countryName)) continue;

                var country = new Country(countryName)
                {
                    Budget = 0,
                    NationalExpenses = 0,
                    Population = 0
                };

                // Set up tax policies
                //double baseTaxRate = 0.15 + random.NextDouble() * 0.15; // 15-30%
                //var incomeTax = new TaxPolicy(TaxType.IncomeTax, (decimal)baseTaxRate, TaxProgressivity.Progressive);
                //incomeTax.ProgressiveBrackets[20000m] = (decimal)(baseTaxRate * 0.6);
                //incomeTax.ProgressiveBrackets[50000m] = (decimal)baseTaxRate;
                //incomeTax.ProgressiveBrackets[100000m] = (decimal)(baseTaxRate * 1.5);
                //country.FinancialSystem.AddTaxPolicy(incomeTax);
                //country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.CorporateTax, (decimal)(baseTaxRate * 1.2)));
                //country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.ConsumptionTax, (decimal)(baseTaxRate * 0.4)));

                Console.WriteLine($"[Economy Init] Generating country: {country.Name}");

                // Get states for this country
                if (statesByCountry.TryGetValue(countryName, out var countryStates))
                {
                    Console.WriteLine($"[Economy Init]   Found {countryStates.Count} states for {country.Name}");

                    bool isFirstState = true;
                    int stateIndex = 0;
                    foreach (var mapState in countryStates)
                    {
                        var state = new State(mapState.StateName)
                        {
                            Budget = 0,
                            TaxRate = 0,
                            StateExpenses = 0,
                            Population = 0
                        };

                        Console.WriteLine($"[Economy Init]    Generating state: {state.Name}");

                        // Get cities for this state from geographic data
                        List<HybridMapManager.GeographicCityInfo> stateCities = new List<HybridMapManager.GeographicCityInfo>();
                        if (geographicCities != null && geographicCities.Count > 0)
                        {
                            // Find cities that belong to this state
                            stateCities = geographicCities
                                .Where(c => !string.IsNullOrWhiteSpace(c.StateName) &&
                                           string.Equals(c.StateName, mapState.StateName, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(c => c.Importance)  // Lower ScaleRank = more important cities first
                                .ToList();

                            Console.WriteLine($"[Economy Init]     Found {stateCities.Count} geographic cities for state {state.Name}");
                        }

                        // Determine number of cities to generate
                        int numCities;
                        if (stateCities.Count > 0)
                        {
                            // Generate economy data for all geographic cities (no cap)
                            // This ensures all cities on the map have economic stats
                            numCities = stateCities.Count;
                            Console.WriteLine($"[Economy Init]     Generating economy for all {numCities} geographic cities");
                        }
                        else
                        {
                            // Fallback: generate 3-8 cities if no geographic data
                            numCities = random.Next(3, 9);
                        }

                        var cityTypes = CityTemplateManager.DetermineStateCityTypes(numCities, random, hasCapital: isFirstState && stateIndex == 0);

                        for (int i = 0; i < numCities; i++)
                        {
                            var cityType = i < cityTypes.Count ? cityTypes[i] : CityType.MixedIndustrial;
                            var template = CityTemplateManager.GetTemplate(cityType);

                            // Use geographic city data if available
                            string cityName;
                            int population;

                            if (i < stateCities.Count)
                            {
                                var geoCity = stateCities[i];
                                cityName = geoCity.CityName;
                                // Use geographic population as base, with some variation
                                //population = geoCity.Population > 0
                                //    ? Math.Max(50000, geoCity.Population + random.Next(-10000, 10000))
                                //    : random.Next(50000, 2000000);
                                population = geoCity.Population;
                            }
                            else
                            {
                                // Fallback: generate procedural name
                                cityName = $"{state.Name} City {i + 1}";
                                population = 0;
                            }

                            var city = new City(cityName)
                            {
                                Budget = 0,
                                TaxRate = 0.02 + random.NextDouble() * 0.04, // 2-6%
                                CityExpenses = 0,
                                Population = population,
                                Happiness = 50
                            };

                            Console.WriteLine($"[Economy Init]     Generating {cityType} city: {city.Name} (pop: {population:N0})");

                            // Generate population classes
                            city.PopClasses.Clear();
                            foreach (var popDist in template.PopulationDistribution)
                            {
                                string className = popDist.Key;
                                double percentage = popDist.Value;

                                int classSize = (int)(population * percentage);
                                if (classSize < 1) classSize = 1;

                                double baseIncome = className switch
                                {
                                    "Laborers" => random.Next(12, 20) * template.IncomeMultiplier,
                                    "Craftsmen" => random.Next(20, 35) * template.IncomeMultiplier,
                                    "Engineers" => random.Next(40, 70) * template.IncomeMultiplier,
                                    "Managers" => random.Next(60, 100) * template.IncomeMultiplier,
                                    "Clerks" => random.Next(25, 45) * template.IncomeMultiplier,
                                    _ => 20.0
                                };

                                var pop = new PopClass(className, classSize, baseIncome)
                                {
                                    Employed = (int)(classSize * (0.85 + random.NextDouble() * 0.1)),
                                    Happiness = 50 + random.Next(-15, 15)
                                };

                                // Set basic needs
                                pop.Needs["Grain"] = 1.0;
                                pop.Needs["Bread"] = 0.8;
                                pop.Needs["Cloth"] = 0.3;

                                city.PopClasses.Add(pop);
                            }
                            city.Population = city.PopClasses.Sum(p => p.Size);

                            // Generate factories
                            int targetFactoryCount = random.Next(3, 8);
                            int targetResourceBuildings = random.Next(1, 4);

                            for (int f = 0; f < targetFactoryCount; f++)
                            {
                                var randomFactoryTypeIndex = random.Next(FactoryBlueprints.AllBlueprints.Count);
                                var blueprint = FactoryBlueprints.AllBlueprints[randomFactoryTypeIndex];

                                if (blueprint != null)
                                {
                                    var factory = blueprint.CreateFactory(city.Name, productionCapacity: random.Next(1, 4));
                                    city.AddFactory(factory);  // Use AddFactory() method, not Factories.Add()
                                    Console.WriteLine($"[Economy Init]       Added factory: {factory.Name} (Capacity: {factory.ProductionCapacity})");
                                }
                            }

                            for (int f = 0; f < targetResourceBuildings; f++)
                            {
                                if (ResourceExtractionBuildingBlueprints.AllBlueprints.Count == 0)
                                {
                                    Console.WriteLine($"[Economy Init]       WARNING: No REB blueprints available, skipping REB generation");
                                    break;
                                }
                                var randomREBTypeIndex = random.Next(ResourceExtractionBuildingBlueprints.AllBlueprints.Count);
                                var blueprint = ResourceExtractionBuildingBlueprints.AllBlueprints[randomREBTypeIndex];
                                if (blueprint != null)
                                {
                                    var reb = blueprint.CreateBuilding(city.Name, extractionCapacity: random.Next(1, 3));
                                    city.AddResourceExtractionBuilding(reb);
                                    Console.WriteLine($"[Economy Init]       Added REB: {reb.Name} (Capacity: {reb.ExtractionCapacity})");
                                }
                            }

                            // Initialize stockpile and prices

                            ProceduralCityBuilder.InitializeCityData(city, template, random);

                            Console.WriteLine($"[Economy Init]     ✓ Completed city: {city.Name} - {city.PopClasses.Count} pop classes, {city.Factories.Count} factories");

                            state.Cities.Add(city);
                            state.Population += city.Population;
                        }

                        Console.WriteLine($"[Economy Init]    ✓ Completed state: {state.Name} - {state.Cities.Count} cities, pop: {state.Population:N0}");

                        // Update state aggregates from cities (budget, population)
                        state.UpdateAggregatesFromCities();
                        Console.WriteLine($"[Economy Init]    ✓ State aggregates updated: Budget=${state.Budget:N0}, Pop={state.Population:N0}");

                        country.States.Add(state);
                        isFirstState = false;
                        stateIndex++;
                    }

                    // Update country aggregates from states (budget, population)
                    country.UpdateAggregatesFromStates();
                    Console.WriteLine($"[Economy Init]   ✓ Country aggregates updated: {country.Name} - Budget=${country.Budget:N0}, Pop={country.Population:N0}");
                }
                else
                {
                    Console.WriteLine($"[Economy Init]   WARNING: No states found for {country.Name} in map data, skipping this country");
                    continue; // Skip countries with no states instead of creating defaults
                }

                if (country.States.Count > 0) // Only add countries that have states
                {
                    Console.WriteLine($"[Economy Init] ✓ Completed country: {country.Name} - {country.States.Count} states, {country.States.Sum(s => s.Cities.Count)} cities, pop: {country.Population:N0}");
                    allCountries.Add(country);
                }
            }

            // Register corporations in global market
            Market.AllCorporations.Clear();
            Market.AllCorporations.AddRange(allCorporations);

            int totalCities = allCountries.Sum(c => c.States.Sum(s => s.Cities.Count));
            int totalFactories = allCountries.Sum(c => c.States.Sum(s => s.Cities.Sum(city => city.Factories.Count)));
            int totalPopClasses = allCountries.Sum(c => c.States.Sum(s => s.Cities.Sum(city => city.PopClasses.Count)));

            Console.WriteLine($"[Economy Init] ========================================");
            Console.WriteLine($"[Economy Init] World economy generation from map data complete!");
            Console.WriteLine($"[Economy Init] - {allCountries.Count} countries");
            Console.WriteLine($"[Economy Init] - {allCountries.Sum(c => c.States.Count)} states");
            Console.WriteLine($"[Economy Init] - {totalCities} cities");
            Console.WriteLine($"[Economy Init] - {totalPopClasses} population classes");
            Console.WriteLine($"[Economy Init] - {totalFactories} factories");
            Console.WriteLine($"[Economy Init] - {allCorporations.Count} corporations");
            Console.WriteLine($"[Economy Init] ========================================");

            return (allCountries, allCorporations);
        }

        public static void UpdateCountryEconomy(Country country)
        {
            // === New Financial System Integration ===
            NationalFinancialSystem fs = country.FinancialSystem;

            // 1. Calculate Tax Revenue using the new system
            // Derive assessable income from POP classes and corporations
            var popIncomeMap = new Dictionary<string, decimal>();
            decimal totalAssessablePopIncome = 0m;
            int totalPopulation = 0;
            foreach (var state in country.States)
            {
                foreach (var city in state.Cities)
                {
                    foreach (var pop in city.PopClasses)
                    {
                        decimal income = (decimal)pop.Size * (decimal)pop.IncomePerPerson;
                        totalAssessablePopIncome += income;
                        totalPopulation += pop.Size;
                        if (popIncomeMap.ContainsKey(pop.Name))
                            popIncomeMap[pop.Name] += income;
                        else
                            popIncomeMap[pop.Name] = income;
                    }
                }
            }

            decimal totalCorporateProfits = 0m;
            foreach (var corp in Market.AllCorporations)
            {
                totalCorporateProfits += (decimal)(corp.Budget * 0.1); // Simple profit approximation
            }

            decimal totalLandValue = (decimal)country.States
                .SelectMany(s => s.Cities)
                .SelectMany(c => c.ProceduralData?.Parcels ?? Enumerable.Empty<Parcel>())
                .Sum(p => p.LandValue);
            decimal totalConsumptionValue = totalAssessablePopIncome * 0.6m; // Assume 60% consumed

            decimal taxRevenue = fs.CalculateTaxRevenue(popIncomeMap, totalCorporateProfits, totalLandValue, totalConsumptionValue, totalPopulation);
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

            // 4. Process any bond maturities for this turn
            fs.ProcessMaturingBonds(DateTime.UtcNow);

            // 5. Update other financial indicators
            // decimal currentGdp = totalAssessablePopIncome + totalCorporateProfits; // Highly simplified GDP
            decimal currentGdp = 0;
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
            City.ProcessCityEconomy(city);

            var companiesToWork = new HashSet<ConstructionCompany>();

            foreach (var company in city.ConstructionCompanies)
            {
                if (company == null)
                {
                    continue;
                }

                if (company.Projects.Any(p => p.OwningCity == city))
                {
                    companiesToWork.Add(company);
                }
            }

            foreach (var project in city.ActiveProjects)
            {
                if (project.AssignedCompany != null)
                {
                    companiesToWork.Add(project.AssignedCompany);
                }
            }

            if (companiesToWork.Count == 0)
            {
                foreach (var company in Market.AllConstructionCompanies.Where(c => c.HomeCity == city))
                {
                    if (company != null)
                    {
                        city.RegisterConstructionCompany(company);
                        companiesToWork.Add(company);
                    }
                }
            }

            foreach (var company in companiesToWork)
            {
                company.WorkOnProjects(city);
            }

            if (companiesToWork.Count > 0)
            {
                RaiseConstructionProgressed(city);
            }
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

    public class ResourceExtractionBuilding // ResourceExtractionBuilding (REB)
    {
        public string Name { get; set; }
        public Corporation OwnerCorporation { get; set; }
        public List<Good> OutputGoods { get; set; }
        public int ExtractionCapacity { get; set; }
        public int BaseExtractionCapacity { get; }
        public Dictionary<string, int> JobSlots { get; set; }
        public Dictionary<string, int> ActualEmployed { get; set; }
        public Building BuildingData { get; set; }
        public string cityName { get; set; } // Name of the city where the resource extraction site is located
        public string RequiredResourceDeposit { get; set; } // Type of resource deposit required
        public double ExtractionMultiplier { get; set; } // Multiplier for extraction rate

        public ResourceExtractionBuilding(string name, int extractionCapacity, string cityName)
        {
            Name = name;
            ExtractionCapacity = extractionCapacity;
            BaseExtractionCapacity = extractionCapacity;
            OutputGoods = new List<Good>();
            JobSlots = new Dictionary<string, int>();
            ActualEmployed = new Dictionary<string, int>();
            this.cityName = cityName;
            ExtractionMultiplier = 1.0;
        }

        /// <summary>
        /// Creates a new ResourceExtractionBuilding instance from a ResourceExtractionBuildingBlueprint.
        /// </summary>
        /// <param name="blueprint">The blueprint to use for creating the building.</param>
        /// <param name="cityName">The name of the city where the building is located.</param>
        /// <param name="extractionCapacity">The extraction capacity of the building (default: 2).</param>
        /// <param name="owner">Optional corporation owner for the building.</param>
        /// <param name="customName">Optional custom name for the building. If null, uses blueprint's BuildingTypeName.</param>
        /// <returns>A new ResourceExtractionBuilding instance configured according to the blueprint.</returns>
        public static ResourceExtractionBuilding FromBlueprint(
            ResourceExtractionBuildingBlueprint blueprint,
            string cityName,
            int extractionCapacity = 2,
            Corporation owner = null,
            string customName = null)
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));

            string buildingName = customName ?? blueprint.BuildingTypeName;
            var building = new ResourceExtractionBuilding(buildingName, extractionCapacity, cityName);

            // Copy output good from blueprint
            building.OutputGoods.Add(new Good(
                blueprint.OutputGood.Name,
                blueprint.OutputGood.BasePrice,
                blueprint.OutputGood.Category,
                blueprint.OutputGood.Quantity));

            // Set resource deposit requirement and extraction multiplier
            building.RequiredResourceDeposit = blueprint.RequiredResourceDeposit;
            building.ExtractionMultiplier = blueprint.BaseExtractionMultiplier;

            // Calculate and set job slots based on extraction capacity and blueprint distribution
            int totalJobSlots = extractionCapacity * 5; // Base number of workers needed
            foreach (var jobSlot in blueprint.DefaultJobSlotDistribution)
            {
                int slots = (int)Math.Ceiling(totalJobSlots * jobSlot.Value);
                building.JobSlots[jobSlot.Key] = slots;
                building.ActualEmployed[jobSlot.Key] = 0; // Initialize actual employed to 0
            }

            // Set owner if provided
            if (owner != null)
            {
                building.OwnerCorporation = owner;
            }

            return building;
        }

        /// <summary>
        /// Simulate resource extraction: produce outputs without consuming inputs.
        /// </summary>
        public void Extract(Dictionary<string, Good> cityStockpile, City city)
        {
            if (this.OwnerCorporation == null)
            {
                return;
            }

            if (BuildingData == null)
            {
                return;
            }

            int currentExtractionCapacity = CalculateCurrentCapacity();

            double totalOutputValue = 0;
            foreach (var output in OutputGoods)
            {
                if (!cityStockpile.ContainsKey(output.Name))
                    cityStockpile[output.Name] = new Good(output.Name, output.BasePrice, output.Category);

                int extractedQuantity = (int)(output.Quantity * currentExtractionCapacity * ExtractionMultiplier);
                cityStockpile[output.Name].Quantity += extractedQuantity;

                // Use city.LocalPrices for output value calculation
                double currentMarketPrice = city.LocalPrices.ContainsKey(output.Name) ? city.LocalPrices[output.Name] : output.BasePrice;
                totalOutputValue += extractedQuantity * currentMarketPrice;

                // Update city.LocalSupply
                if (city.LocalSupply.ContainsKey(output.Name))
                    city.LocalSupply[output.Name] += extractedQuantity;
                else
                    city.LocalSupply[output.Name] = extractedQuantity;
            }

            this.OwnerCorporation.Budget += totalOutputValue;
            city.Budget -= totalOutputValue;
        }

        private int CalculateCurrentCapacity()
        {
            if (BuildingData == null)
            {
                return ExtractionCapacity;
            }

            double levelFactor = 0.6 + BuildingData.Level * 0.5;
            double outputFactor = 1.0 + BuildingData.EconomicOutput / 500.0;
            int capacity = (int)Math.Round(BaseExtractionCapacity * levelFactor * outputFactor);
            return Math.Max(1, capacity);
        }
    }


    public class Factory
    {
        public string Name { get; set; }
        public Corporation OwnerCorporation { get; set; }
        public List<Good> InputGoods { get; set; }
        public List<Good> OutputGoods { get; set; }
        public int ProductionCapacity { get; set; }
        public int BaseProductionCapacity { get; }
        public int WorkersEmployed { get; set; } // This might become a sum of employed from JobSlots or represent total workforce
        public Dictionary<string, int> JobSlots { get; set; }
        public Dictionary<string, int> ActualEmployed { get; set; } // Tracks actual number employed in each slot type
        public Building BuildingData { get; set; }
        public string cityName { get; set; } // Name of the city where the factory is located

        public Factory(string name, int productionCapacity, string cityName)
        {
            Name = name;
            ProductionCapacity = productionCapacity;
            BaseProductionCapacity = productionCapacity;
            InputGoods = new List<Good>();
            OutputGoods = new List<Good>();
            JobSlots = new Dictionary<string, int>();
            ActualEmployed = new Dictionary<string, int>();
            this.cityName = cityName;
            
            // WorkersEmployed will be calculated or set based on ActualEmployed
        }

        /// <summary>
        /// Creates a new Factory instance from a FactoryBlueprint.
        /// </summary>
        /// <param name="blueprint">The blueprint to use for creating the factory.</param>
        /// <param name="cityName">The name of the city where the factory is located.</param>
        /// <param name="productionCapacity">The production capacity of the factory (default: 2).</param>
        /// <param name="owner">Optional corporation owner for the factory.</param>
        /// <param name="customName">Optional custom name for the factory. If null, uses blueprint's FactoryTypeName.</param>
        /// <returns>A new Factory instance configured according to the blueprint.</returns>
        public static Factory FromBlueprint(FactoryBlueprint blueprint, string cityName, int productionCapacity = 2, Corporation owner = null, string customName = null)
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));

            string factoryName = customName ?? blueprint.FactoryTypeName;
            var factory = new Factory(factoryName, productionCapacity, cityName);

            // Copy input goods from blueprint
            foreach (var inputGood in blueprint.InputGoods)
            {
                factory.InputGoods.Add(new Good(inputGood.Name, inputGood.BasePrice, inputGood.Category, inputGood.Quantity));
            }

            // Copy output good from blueprint
            factory.OutputGoods.Add(new Good(
                blueprint.OutputGood.Name,
                blueprint.OutputGood.BasePrice,
                blueprint.OutputGood.Category,
                blueprint.OutputGood.Quantity));

            // Calculate and set job slots based on production capacity and blueprint distribution
            int totalJobSlots = productionCapacity * 5; // Base number of workers needed
            foreach (var jobSlot in blueprint.DefaultJobSlotDistribution)
            {
                int slots = (int)Math.Ceiling(totalJobSlots * jobSlot.Value);
                factory.JobSlots[jobSlot.Key] = slots;
                factory.ActualEmployed[jobSlot.Key] = 0; // Initialize actual employed to 0
            }

            // Set owner if provided
            if (owner != null)
            {
                factory.OwnerCorporation = owner;
            }

            return factory;
        }

        // Simulate production: consume inputs, produce outputs
        public void Produce(Dictionary<string, Good> cityStockpile, City city)
        {
            if (this.OwnerCorporation == null)
            {
                return;
            }

            if (BuildingData == null)
            {
                return;
            }

            int currentProductionCapacity = CalculateCurrentCapacity();

            double totalInputCost = 0;
            bool allInputsAvailableInStockpile = true;
            foreach (var input in InputGoods)
            {
                if (!cityStockpile.ContainsKey(input.Name) || cityStockpile[input.Name].Quantity < input.Quantity * currentProductionCapacity)
                {
                    allInputsAvailableInStockpile = false;
                    break;
                }
                // Use city.LocalPrices for input cost calculation
                totalInputCost += (input.Quantity * currentProductionCapacity) * (city.LocalPrices.ContainsKey(input.Name) ? city.LocalPrices[input.Name] : input.BasePrice);
            }

            if (!allInputsAvailableInStockpile) return;
            if (this.OwnerCorporation.Budget < totalInputCost) return;

            this.OwnerCorporation.Budget -= totalInputCost;
            city.Budget += totalInputCost;

            foreach (var input in InputGoods)
            {
                cityStockpile[input.Name].Quantity -= input.Quantity * currentProductionCapacity;
                // Update city.LocalDemand
                if (city.LocalDemand.ContainsKey(input.Name))
                    city.LocalDemand[input.Name] += input.Quantity * currentProductionCapacity;
                else
                    city.LocalDemand[input.Name] = input.Quantity * currentProductionCapacity;

                Console.WriteLine("");
            }
            
            double totalOutputValue = 0;
            foreach (var output in OutputGoods)
            {
                if (!cityStockpile.ContainsKey(output.Name))
                    cityStockpile[output.Name] = new Good(output.Name, output.BasePrice, output.Category);

                cityStockpile[output.Name].Quantity += output.Quantity * currentProductionCapacity;
                // Use city.LocalPrices for output value calculation
                double currentMarketPrice = city.LocalPrices.ContainsKey(output.Name) ? city.LocalPrices[output.Name] : output.BasePrice;
                totalOutputValue += (output.Quantity * currentProductionCapacity) * currentMarketPrice;

                // Update city.LocalSupply
                if (city.LocalSupply.ContainsKey(output.Name))
                    city.LocalSupply[output.Name] += output.Quantity * currentProductionCapacity;
                else
                    city.LocalSupply[output.Name] = output.Quantity * currentProductionCapacity;
            }

            this.OwnerCorporation.Budget += totalOutputValue;
            city.Budget -= totalOutputValue;
        }

        private int CalculateCurrentCapacity()
        {
            if (BuildingData == null)
            {
                return ProductionCapacity;
            }

            double levelFactor = 0.6 + BuildingData.Level * 0.5;
            double outputFactor = 1.0 + BuildingData.EconomicOutput / 500.0;
            int capacity = (int)Math.Round(BaseProductionCapacity * levelFactor * outputFactor);
            return Math.Max(1, capacity);
        }
    }

    // The following should be outside the Economy class
    public static class Market
    {
        public static Dictionary<string, Good> GoodDefinitions { get; private set; } = new Dictionary<string, Good>(); // Keep this for base prices/categories
        public static List<Corporation> AllCorporations { get; private set; } = new List<Corporation>();
        public static List<ConstructionCompany> AllConstructionCompanies { get; private set; } = new List<ConstructionCompany>();

        // Call this at the start of each turn for each city to reset its local supply/demand
  

        // Call this for each city after all its local buy/sell actions to update its local prices
   
        
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
                if (city.Budget >= totalCost)
                {
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
      

   

        /// <summary>
        /// Helper method to get country name from a city by looking up its ownership in the world state
        /// </summary>
       

      
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
                FactoryBlueprint chosenBlueprint = FactoryBlueprints.AllBlueprints
                    .Where(b =>
                    {
                        switch (this.Specialization)
                        {
                            case CorporationSpecialization.Agriculture:
                                return b.ProducedGoodCategory == GoodCategory.RawMaterial || b.ProducedGoodCategory == GoodCategory.ProcessedFood;
                            case CorporationSpecialization.Mining:
                                return b.ProducedGoodCategory == GoodCategory.RawMaterial && (b.OutputGood.Name.Contains("Mine") || b.OutputGood.Name.Contains("Coal") || b.OutputGood.Name.Contains("Iron"));
                            case CorporationSpecialization.HeavyIndustry:
                                return b.ProducedGoodCategory == GoodCategory.IndustrialInput || b.ProducedGoodCategory == GoodCategory.CapitalGood;
                            case CorporationSpecialization.LightIndustry:
                                return b.ProducedGoodCategory == GoodCategory.ConsumerProduct;
                            case CorporationSpecialization.Diversified:
                            default:
                                return b.ProducedGoodCategory == hintForDiversified;
                        }
                    })
                    .OrderBy(_ => randomizer.Next())
                    .FirstOrDefault();

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

                    Factory newFactory = new Factory(newFactoryName, newFactoryBaseCapacity,targetCity.Name);

                    // Calculate actual job slots based on production capacity
                    int totalJobSlots = newFactoryBaseCapacity * 5; // Base number of workers needed
                    foreach (var jobSlot in chosenBlueprint.DefaultJobSlotDistribution)
                    {
                        int slots = (int)Math.Ceiling(totalJobSlots * jobSlot.Value);
                        newFactory.JobSlots[jobSlot.Key] = slots;
                        DebugLogger.Log($"[Factory Creation] {newFactoryName} - Added {slots} slots for {jobSlot.Key}", DebugLogger.LogCategory.Building);
                    }

                    // Set up input/output goods
                    foreach (var inputGood in chosenBlueprint.InputGoods)
                    {
                        newFactory.InputGoods.Add(new Good(inputGood.Name, inputGood.BasePrice, inputGood.Category));
                    }
                    newFactory.OutputGoods.Add(new Good(chosenBlueprint.OutputGood.Name, chosenBlueprint.OutputGood.BasePrice, chosenBlueprint.OutputGood.Category));

                    newFactory.OwnerCorporation = this;

                    targetCity.AddFactory(newFactory);
                    this.AddFactory(newFactory);

                    if (targetCity.ProceduralData != null)
                    {
                        var newParcel = new Parcel { LandUse = LandUseType.Industrial };
                        var newBuilding = new Building { LandUse = LandUseType.Industrial };
                        targetCity.ProceduralData.AddParcel(newParcel);
                        targetCity.ProceduralData.SetBuilding(newParcel, newBuilding);
                        newFactory.BuildingData = newBuilding;

                        var updatedLandValues = LandValueCalculator.Calculate(targetCity);
                        LandUseSimulator.RunDeveloperAgentSimulation(targetCity, updatedLandValues);
                        var refinedValues = LandValueCalculator.Calculate(targetCity);
                        BuildingRefiner.RefineBuildings(targetCity, refinedValues);
                    }

                    Console.WriteLine($"AI Corp '{Name}': SUCCESSFULLY BUILT {newFactory.Name} in {targetCity.Name}. New Budget: {Budget:C}");
                }
            }
        }

        // TODO: Add methods for corporate actions: CollectProfits, Invest, etc.
    }

  

    public class PopClass
    {
        public string Name { get; set; }
        public int Size { get; set; }
        public double IncomePerPerson { get; set; }
        public Dictionary<string, double> Needs { get; set; }
        public string[] PreferredGoods { get; set; }
        public int Happiness { get; set; } // 0-100
        public int Employed { get; set; }
        public int Unemployed { get { return Size - Employed; } }
        public string Strata { get; set; }
        
        // New QualityOfLife property
        public double QualityOfLife { get; private set; }

        private static readonly Dictionary<string, double> DefaultNeeds = new Dictionary<string, double>
        {
            ["Grain"] = 1.0,
            ["Bread"] = 0.8,
            ["Cloth"] = 0.3
        };
        public PopClass(string name, int size, double incomePerPerson, string strata = "", Dictionary<string, double>? needs = null)
        {
            Name = name;
            Size = size;
            IncomePerPerson = incomePerPerson;
            Strata = strata;
            Needs = needs ?? new Dictionary<string, double>(DefaultNeeds);  // Use provided needs or default
            Happiness = 100;
            QualityOfLife = CalculateQualityOfLife();
        }

        // Method to calculate Quality of Life based on constant factors
        private double CalculateQualityOfLife()
        {
            /*double healthcare = 0.8; // Example constant value (0-1 scale)
            double education = 0.7;
            double housing = 1.0 - (UnmetNeeds / (Needs.Count > 0 ? Needs.Count : 1)); // Penalize unmet needs
            double employment = Size > 0 ? Employed / (double)Size : 0; // Employment rate, avoid division by zero

            DebugLogger.Log($"[CalculateQualityOfLife] Healthcare: {healthcare}, Education: {education}, Housing: {housing}, Employment: {employment}", DebugLogger.LogCategory.Pop);

            // Weighted average of factors
            double qualityOfLife = (healthcare * 0.3) + (education * 0.3) + (housing * 0.2) + (employment * 0.2);
            DebugLogger.Log($"[CalculateQualityOfLife] Calculated QoL: {qualityOfLife}", DebugLogger.LogCategory.Pop);
*/

            return 100;
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

    /// <summary>
    /// Base class for all building blueprints. Provides common properties and functionality
    /// for creating buildings from blueprints.
    /// </summary>
    public abstract class Blueprint
    {
        /// <summary>
        /// The name/type of the building this blueprint creates.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The primary output good produced by this building.
        /// </summary>
        public Good OutputGood { get; set; }

        /// <summary>
        /// The category of goods this building produces.
        /// </summary>
        public GoodCategory ProducedGoodCategory { get; set; }

        /// <summary>
        /// Default job slot distribution for workers in this building type.
        /// Key is job type name, value is percentage (0.0 to 1.0).
        /// </summary>
        public Dictionary<string, double> DefaultJobSlotDistribution { get; set; }

        /// <summary>
        /// Default job slots with a comprehensive distribution.
        /// </summary>
        protected static readonly Dictionary<string, double> StandardJobSlots = new Dictionary<string, double>
        {
            { "Laborers", 0.5 },
            { "Craftsmen", 0.2 },
            { "Engineers", 0.15 },
            { "Managers", 0.1 },
            { "Clerks", 0.05 }
        };

        protected Blueprint(string typeName, Good outputGood, GoodCategory producedGoodCategory, Dictionary<string, double> jobSlots = null)
        {
            TypeName = typeName;
            OutputGood = outputGood;
            ProducedGoodCategory = producedGoodCategory;
            DefaultJobSlotDistribution = jobSlots ?? new Dictionary<string, double>(StandardJobSlots);
        }
    }

    /// <summary>
    /// Blueprint for creating Factory instances. Factories transform input goods into output goods.
    /// </summary>
    public class FactoryBlueprint : Blueprint
    {
        /// <summary>
        /// Alias for TypeName for backwards compatibility.
        /// </summary>
        public string FactoryTypeName
        {
            get => TypeName;
            set => TypeName = value;
        }

        /// <summary>
        /// List of input goods required for production.
        /// </summary>
        public List<Good> InputGoods { get; set; }

        public FactoryBlueprint(string typeName, Good output, List<Good> inputs, GoodCategory producedGoodCategory, Dictionary<string, double> jobSlots = null)
            : base(typeName, output, producedGoodCategory, jobSlots)
        {
            InputGoods = inputs ?? new List<Good>();
        }

        /// <summary>
        /// Creates a new Factory instance from this blueprint.
        /// </summary>
        /// <param name="cityName">The name of the city where the factory is located.</param>
        /// <param name="productionCapacity">The production capacity of the factory (default: 2).</param>
        /// <param name="owner">Optional corporation owner for the factory.</param>
        /// <param name="customName">Optional custom name for the factory. If null, uses FactoryTypeName.</param>
        /// <returns>A new Factory instance configured according to this blueprint.</returns>
        public Factory CreateFactory(string cityName, int productionCapacity = 2, Corporation owner = null, string customName = null)
        {
            return Factory.FromBlueprint(this, cityName, productionCapacity, owner, customName);
        }
    }

    /// <summary>
    /// Blueprint for creating ResourceExtractionBuilding instances. REBs extract raw materials without requiring inputs.
    /// </summary>
    public class ResourceExtractionBuildingBlueprint : Blueprint
    {
        /// <summary>
        /// Alias for TypeName for consistency with FactoryBlueprint.
        /// </summary>
        public string BuildingTypeName
        {
            get => TypeName;
            set => TypeName = value;
        }

        /// <summary>
        /// The type of resource deposit required for this building (e.g., "Coal Deposit", "Iron Vein").
        /// Null if no specific deposit is required (e.g., farms, fishing wharves).
        /// </summary>
        public string RequiredResourceDeposit { get; set; }

        /// <summary>
        /// Base extraction rate multiplier for this building type.
        /// </summary>
        public double BaseExtractionMultiplier { get; set; }

        public ResourceExtractionBuildingBlueprint(
            string typeName,
            Good output,
            GoodCategory producedGoodCategory,
            Dictionary<string, double> jobSlots = null,
            string requiredResourceDeposit = null,
            double baseExtractionMultiplier = 1.0)
            : base(typeName, output, producedGoodCategory, jobSlots)
        {
            RequiredResourceDeposit = requiredResourceDeposit;
            BaseExtractionMultiplier = baseExtractionMultiplier;
        }

        /// <summary>
        /// Creates a new ResourceExtractionBuilding instance from this blueprint.
        /// </summary>
        /// <param name="cityName">The name of the city where the building is located.</param>
        /// <param name="extractionCapacity">The extraction capacity of the building (default: 2).</param>
        /// <param name="owner">Optional corporation owner for the building.</param>
        /// <param name="customName">Optional custom name for the building. If null, uses BuildingTypeName.</param>
        /// <returns>A new ResourceExtractionBuilding instance configured according to this blueprint.</returns>
        public ResourceExtractionBuilding CreateBuilding(string cityName, int extractionCapacity = 2, Corporation owner = null, string customName = null)
        {
            return ResourceExtractionBuilding.FromBlueprint(this, cityName, extractionCapacity, owner, customName);
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

            

            // --- Existing/Refined Intermediate Goods (from previous refactoring) --- 
            AllBlueprints.Add(new FactoryBlueprint("Steel Mill", new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new List<Good> { new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sawmill", new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Timber", Market.GoodDefinitions["Timber"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Textile Mill", new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Cotton", Market.GoodDefinitions["Cotton"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Paper Mill", new Good("Paper", Market.GoodDefinitions["Paper"].BasePrice, GoodCategory.IndustrialInput, 2), new List<Good> { new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.IndustrialInput, industrialJobSlots));

            // --- More goods and blueprints will be added in subsequent chunks ---
            // Placeholder for the rest of the existing/refined blueprints from previous step
            AllBlueprints.Add(new FactoryBlueprint("Tool Factory", new Good("Tools", Market.GoodDefinitions["Tools"].BasePrice, GoodCategory.CapitalGood, 2), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Machine Parts Factory", new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.CapitalGood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Bakery", new Good("Bread", Market.GoodDefinitions["Bread"].BasePrice, GoodCategory.ProcessedFood, 3), new List<Good> { new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 2) }, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Clothing Factory", new Good("Cloth", Market.GoodDefinitions["Cloth"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2) }, GoodCategory.ConsumerProduct, industrialJobSlots));
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
            AllBlueprints.Add(new FactoryBlueprint("Cannery", new Good("Canned Goods", Market.GoodDefinitions["Canned Goods"].BasePrice, GoodCategory.ProcessedFood, 2), new List<Good> { new Good("Fish", Market.GoodDefinitions["Fish"].BasePrice, GoodCategory.RawMaterial, 2), new Good("Tin Ingots", Market.GoodDefinitions["Tin Ingots"].BasePrice, GoodCategory.IndustrialInput, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Meat Packing Plant", new Good("Processed Meat", Market.GoodDefinitions["Processed Meat"].BasePrice, GoodCategory.ProcessedFood, 1), new List<Good> { new Good("Livestock", Market.GoodDefinitions["Livestock"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Salt", Market.GoodDefinitions["Salt"].BasePrice, GoodCategory.RawMaterial, 1), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ProcessedFood, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Tea Factory", new Good("Tea", Market.GoodDefinitions["Tea"].BasePrice, GoodCategory.ConsumerProduct, 2), new List<Good> { new Good("Tea Leaves", Market.GoodDefinitions["Tea Leaves"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Coffee Roastery", new Good("Coffee", Market.GoodDefinitions["Coffee"].BasePrice, GoodCategory.ConsumerProduct, 2), new List<Good> { new Good("Coffee Beans", Market.GoodDefinitions["Coffee Beans"].BasePrice, GoodCategory.RawMaterial, 3), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ConsumerProduct, industrialJobSlots));
            AllBlueprints.Add(new FactoryBlueprint("Sugar Mill", new Good("Refined Sugar", Market.GoodDefinitions["Refined Sugar"].BasePrice, GoodCategory.ProcessedFood, 3), new List<Good> { new Good("Sugar Cane", Market.GoodDefinitions["Sugar Cane"].BasePrice, GoodCategory.RawMaterial, 4), new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 1) }, GoodCategory.ProcessedFood, industrialJobSlots));

            // --- Existing/Refined Consumer Goods (from previous refactoring) ---
            AllBlueprints.Add(new FactoryBlueprint("Clothing Factory", new Good("Cloth", Market.GoodDefinitions["Cloth"].BasePrice, GoodCategory.ConsumerProduct, 1), new List<Good> { new Good("Fabric", Market.GoodDefinitions["Fabric"].BasePrice, GoodCategory.IndustrialInput, 2) }, GoodCategory.ConsumerProduct, industrialJobSlots));
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
            AllBlueprints.Add(new FactoryBlueprint("Artillery Plant", new Good("Artillery", Market.GoodDefinitions["Artillery"].BasePrice, GoodCategory.CapitalGood, 1), new List<Good> { new Good("Steel", Market.GoodDefinitions["Steel"].BasePrice, GoodCategory.IndustrialInput, 10), new Good("Machine Parts", Market.GoodDefinitions["Machine Parts"].BasePrice, GoodCategory.CapitalGood, 5), new Good("Bronze Ingots", Market.GoodDefinitions["Bronze Ingots"].BasePrice, GoodCategory.IndustrialInput, 2), new Good("Lumber", Market.GoodDefinitions["Lumber"].BasePrice, GoodCategory.IndustrialInput, 2) }, GoodCategory.CapitalGood, industrialJobSlots));
        }
    }

    /// <summary>
    /// Static class for managing ResourceExtractionBuilding blueprints.
    /// </summary>
    public static class ResourceExtractionBuildingBlueprints
    {
        public static List<ResourceExtractionBuildingBlueprint> AllBlueprints { get; private set; } = new List<ResourceExtractionBuildingBlueprint>();

        /// <summary>
        /// Initialize all REB blueprints. Should be called after Market.GoodDefinitions is populated.
        /// </summary>
        public static void InitializeBlueprints()
        {
            AllBlueprints.Clear();

            // Define standard job distributions for resource extraction
            var miningJobSlots = new Dictionary<string, double>
            {
                { "Laborers", 0.7 },
                { "Craftsmen", 0.15 },
                { "Engineers", 0.1 },
                { "Managers", 0.05 }
            };

            var farmingJobSlots = new Dictionary<string, double>
            {
                { "Laborers", 0.8 },
                { "Craftsmen", 0.1 },
                { "Engineers", 0.05 },
                { "Managers", 0.05 }
            };

            var advancedExtractionJobSlots = new Dictionary<string, double>
            {
                { "Laborers", 0.5 },
                { "Craftsmen", 0.25 },
                { "Engineers", 0.15 },
                { "Managers", 0.1 }
            };

            // === Mining Operations ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Coal Mine",
                new Good("Coal", Market.GoodDefinitions["Coal"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                miningJobSlots,
                "Coal Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Iron Mine",
                new Good("Iron", Market.GoodDefinitions["Iron"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                miningJobSlots,
                "Iron Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Copper Mine",
                new Good("Copper Ore", Market.GoodDefinitions["Copper Ore"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                advancedExtractionJobSlots,
                "Copper Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Tin Mine",
                new Good("Tin Ore", Market.GoodDefinitions["Tin Ore"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                advancedExtractionJobSlots,
                "Tin Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Lead Mine",
                new Good("Lead Ore", Market.GoodDefinitions["Lead Ore"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                advancedExtractionJobSlots,
                "Lead Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Zinc Mine",
                new Good("Zinc Ore", Market.GoodDefinitions["Zinc Ore"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                advancedExtractionJobSlots,
                "Zinc Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Sulphur Mine",
                new Good("Sulphur", Market.GoodDefinitions["Sulphur"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                miningJobSlots,
                "Sulphur Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Salt Mine",
                new Good("Salt", Market.GoodDefinitions["Salt"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                miningJobSlots,
                "Salt Deposit",
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Limestone Quarry",
                new Good("Limestone", Market.GoodDefinitions["Limestone"].BasePrice, GoodCategory.RawMaterial, 3),
                GoodCategory.RawMaterial,
                miningJobSlots,
                "Limestone Deposit",
                1.0));

            // === Oil Extraction ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Oil Derrick",
                new Good("Crude Oil", Market.GoodDefinitions["Crude Oil"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                advancedExtractionJobSlots,
                "Oil Field",
                1.0));

            // === Forestry ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Logging Camp",
                new Good("Timber", Market.GoodDefinitions["Timber"].BasePrice, GoodCategory.RawMaterial, 4),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null, // No specific deposit required - forests are common
                1.0));

            // === Agriculture (no deposit required) ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Grain Farm",
                new Good("Grain", Market.GoodDefinitions["Grain"].BasePrice, GoodCategory.RawMaterial, 3),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Cotton Plantation",
                new Good("Cotton", Market.GoodDefinitions["Cotton"].BasePrice, GoodCategory.RawMaterial, 3),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Rubber Plantation",
                new Good("Raw Rubber", Market.GoodDefinitions["Raw Rubber"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Tea Plantation",
                new Good("Tea Leaves", Market.GoodDefinitions["Tea Leaves"].BasePrice, GoodCategory.RawMaterial, 3),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Coffee Plantation",
                new Good("Coffee Beans", Market.GoodDefinitions["Coffee Beans"].BasePrice, GoodCategory.RawMaterial, 3),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Tobacco Plantation",
                new Good("Tobacco Leaf", Market.GoodDefinitions["Tobacco Leaf"].BasePrice, GoodCategory.RawMaterial, 2),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Sugar Plantation",
                new Good("Sugar Cane", Market.GoodDefinitions["Sugar Cane"].BasePrice, GoodCategory.RawMaterial, 4),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Dye Collection Post",
                new Good("Dyes", Market.GoodDefinitions["Dyes"].BasePrice, GoodCategory.RawMaterial, 1),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));

            // === Fishing ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Fishing Wharf",
                new Good("Fish", Market.GoodDefinitions["Fish"].BasePrice, GoodCategory.RawMaterial, 4),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null, // Requires coastal location but no deposit
                1.0));

            // === Ranching ===
            AllBlueprints.Add(new ResourceExtractionBuildingBlueprint(
                "Cattle Ranch",
                new Good("Livestock", Market.GoodDefinitions["Livestock"].BasePrice, GoodCategory.RawMaterial, 1),
                GoodCategory.RawMaterial,
                farmingJobSlots,
                null,
                1.0));
        }

        /// <summary>
        /// Gets a blueprint by its type name.
        /// </summary>
        public static ResourceExtractionBuildingBlueprint GetBlueprint(string typeName)
        {
            return AllBlueprints.FirstOrDefault(b => b.BuildingTypeName == typeName);
        }

        /// <summary>
        /// Gets all blueprints that produce a specific good.
        /// </summary>
        public static IEnumerable<ResourceExtractionBuildingBlueprint> GetBlueprintsByOutput(string goodName)
        {
            return AllBlueprints.Where(b => b.OutputGood.Name == goodName);
        }

        /// <summary>
        /// Gets all blueprints that require a specific resource deposit.
        /// </summary>
        public static IEnumerable<ResourceExtractionBuildingBlueprint> GetBlueprintsByDeposit(string depositType)
        {
            return AllBlueprints.Where(b => b.RequiredResourceDeposit == depositType);
        }

        /// <summary>
        /// Gets all blueprints that don't require a specific resource deposit (farms, plantations, etc.).
        /// </summary>
        public static IEnumerable<ResourceExtractionBuildingBlueprint> GetNonDepositBlueprints()
        {
            return AllBlueprints.Where(b => string.IsNullOrEmpty(b.RequiredResourceDeposit));
        }
    }
}