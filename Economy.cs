using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
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
        public static (List<Country> countries, List<Corporation> corporations) InitializeWorldEconomy(
            int numCountries = 10,
            int numStatesPerCountry = 5,
            int numCitiesPerState = 3,
            int? seed = null)
        {
            Console.WriteLine("[Economy Init] Starting procedural world economy initialization...");
            Console.WriteLine($"[Economy Init] Parameters: {numCountries} countries, {numStatesPerCountry} states each, ~{numCitiesPerState} cities per state");

            // Initialize factory blueprints and goods
            if (!Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods and {FactoryBlueprints.AllBlueprints.Count} factory blueprints");
            }

            // Generate world structure data
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            var worldData = WorldDataGenerator.GenerateWorldData(numCountries, numStatesPerCountry, numCitiesPerState);
            
            Console.WriteLine($"[Economy Init] Generated world structure with {worldData.Countries.Count} countries");

            // Use procedural generation with templates to create the actual world
            var (countries, corporations) = ProceduralWorldGenerator.GenerateWorld(worldData, random);

            // Set up construction companies
            var constructionCompanies = new List<ConstructionCompany>();
            foreach (var companyData in worldData.ConstructionCompanies)
            {
                var homeCity = countries
                    .SelectMany(c => c.States)
                    .SelectMany(s => s.Cities)
                    .FirstOrDefault(city => city.Name == companyData.HomeCity);

                if (homeCity != null)
                {
                    var company = new ConstructionCompany(
                        companyData.Name,
                        companyData.Workers,
                        (decimal)companyData.InitialBudget)
                    {
                        HomeCity = homeCity
                    };
                    
                    constructionCompanies.Add(company);
                    homeCity.RegisterConstructionCompany(company);
                    corporations.Add(company);
                }
            }

            // Register corporations in global market
            Market.AllCorporations.Clear();
            Market.AllCorporations.AddRange(corporations);

            Market.AllConstructionCompanies.Clear();
            Market.AllConstructionCompanies.AddRange(constructionCompanies);

            Console.WriteLine($"[Economy Init] Economy initialization complete!");
            Console.WriteLine($"[Economy Init] Total: {countries.Count} countries, {countries.Sum(c => c.States.Count)} states, {countries.SelectMany(c => c.States).Sum(s => s.Cities.Count)} cities");
            Console.WriteLine($"[Economy Init] Total: {corporations.Count} corporations ({constructionCompanies.Count} construction companies)");
            Console.WriteLine($"[Economy Init] Total population: {countries.Sum(c => c.Population):N0}");

            return (countries, corporations);
        }

        /// <summary>
        /// Load the world economy from world_setup.json file
        /// </summary>
        /// <param name="filePath">Path to the world_setup.json file (defaults to world_setup.json in current directory)</param>
        /// <param name="seed">Random seed for reproducible generation (null for random)</param>
        /// <returns>Tuple of (countries, corporations) lists</returns>
        public static (List<Country> countries, List<Corporation> corporations) LoadWorldEconomyFromJson(
            string filePath = "world_setup.json",
            int? seed = null)
        {
            Console.WriteLine($"[Economy Init] Loading world economy from {filePath}...");

            // Initialize factory blueprints and goods
            if (!Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints();
                Console.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods and {FactoryBlueprints.AllBlueprints.Count} factory blueprints");
            }

            // Load world data from JSON
            WorldSetupData worldData;
            try
            {
                string jsonText = File.ReadAllText(filePath);
                worldData = JsonSerializer.Deserialize<WorldSetupData>(jsonText, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (worldData == null || worldData.Countries == null)
                {
                    Console.WriteLine($"[Economy Init] ERROR: Failed to deserialize world data from {filePath}");
                    // Fallback to procedural generation
                    return InitializeWorldEconomy();
                }
                
                Console.WriteLine($"[Economy Init] Loaded world structure with {worldData.Countries.Count} countries from JSON");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Economy Init] ERROR loading {filePath}: {ex.Message}");
                Console.WriteLine($"[Economy Init] Falling back to procedural generation");
                // Fallback to procedural generation
                return InitializeWorldEconomy();
            }

            // Use procedural generation with the loaded data
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            var (countries, corporations) = ProceduralWorldGenerator.GenerateWorld(worldData, random);

            // Set up construction companies
            var constructionCompanies = new List<ConstructionCompany>();
            if (worldData.ConstructionCompanies != null)
            {
                foreach (var companyData in worldData.ConstructionCompanies)
                {
                    var homeCity = countries
                        .SelectMany(c => c.States)
                        .SelectMany(s => s.Cities)
                        .FirstOrDefault(city => city.Name == companyData.HomeCity);

                    if (homeCity != null)
                    {
                        var company = new ConstructionCompany(
                            companyData.Name,
                            companyData.Workers,
                            (decimal)companyData.InitialBudget)
                        {
                            HomeCity = homeCity
                        };
                        
                        constructionCompanies.Add(company);
                        homeCity.RegisterConstructionCompany(company);
                        corporations.Add(company);
                    }
                }
            }

            // Register corporations in global market
            Market.AllCorporations.Clear();
            Market.AllCorporations.AddRange(corporations);

            Market.AllConstructionCompanies.Clear();
            Market.AllConstructionCompanies.AddRange(constructionCompanies);

            Console.WriteLine($"[Economy Init] Economy initialization complete from JSON!");
            Console.WriteLine($"[Economy Init] Total: {countries.Count} countries, {countries.Sum(c => c.States.Count)} states, {countries.SelectMany(c => c.States).Sum(s => s.Cities.Count)} cities");
            Console.WriteLine($"[Economy Init] Total: {corporations.Count} corporations ({constructionCompanies.Count} construction companies)");
            Console.WriteLine($"[Economy Init] Total population: {countries.Sum(c => c.Population):N0}");

            return (countries, corporations);
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
                Console.WriteLine($"[Economy Init] Initialized {Market.GoodDefinitions.Count} goods and {FactoryBlueprints.AllBlueprints.Count} factory blueprints");
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
                    Budget = random.Next(1000000, 100000000),
                    NationalExpenses = random.Next(500000, 10000000),
                    Population = 0
                };

                // Set up tax policies
                double baseTaxRate = 0.15 + random.NextDouble() * 0.15; // 15-30%
                var incomeTax = new TaxPolicy(TaxType.IncomeTax, (decimal)baseTaxRate, TaxProgressivity.Progressive);
                incomeTax.ProgressiveBrackets[20000m] = (decimal)(baseTaxRate * 0.6);
                incomeTax.ProgressiveBrackets[50000m] = (decimal)baseTaxRate;
                incomeTax.ProgressiveBrackets[100000m] = (decimal)(baseTaxRate * 1.5);
                country.FinancialSystem.AddTaxPolicy(incomeTax);
                country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.CorporateTax, (decimal)(baseTaxRate * 1.2)));
                country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.ConsumptionTax, (decimal)(baseTaxRate * 0.4)));

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
                            Budget = random.Next(100000, 10000000),
                            TaxRate = 0.03 + random.NextDouble() * 0.05, // 3-8%
                            StateExpenses = random.Next(50000, 1000000),
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
                                population = geoCity.Population > 0 
                                    ? Math.Max(50000, geoCity.Population + random.Next(-10000, 10000))
                                    : random.Next(50000, 2000000);
                            }
                            else
                            {
                                // Fallback: generate procedural name
                                cityName = $"{state.Name} City {i + 1}";
                                population = random.Next(50000, 2000000);
                            }
                            
                            var city = new City(cityName)
                            {
                                Budget = random.Next(100000, 5000000) * template.BudgetMultiplier,
                                TaxRate = 0.02 + random.NextDouble() * 0.04, // 2-6%
                                CityExpenses = random.Next(50000, 500000) * template.ExpenseMultiplier,
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
                            for (int f = 0; f < targetFactoryCount; f++)
                            {
                                var factoryType = ProceduralWorldGenerator.SelectWeightedFactoryType(template.FactoryWeights, random);
                                var blueprint = FactoryBlueprints.AllBlueprints.FirstOrDefault(b => b.OutputGood.Name == factoryType || b.FactoryTypeName == factoryType);
                                
                                if (blueprint != null)
                                {
                                    var corporation = ProceduralWorldGenerator.FindOrCreateCorporation(blueprint, city, allCorporations, random);
                                    var factory = ProceduralWorldGenerator.CreateFactoryFromBlueprint(blueprint, corporation, random.Next(2, 6), city);
                                    
                                    city.AddFactory(factory);
                                    corporation.AddFactory(factory);
                                }
                            }

                            // Initialize stockpile and prices
                            ProceduralWorldGenerator.InitializeStockpile(city, template, random);
                            ProceduralWorldGenerator.InitializeLocalPrices(city);
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
                        if (!popIncomeMap.TryGetValue(pop.Name, out var existingIncome))
                        {
                            popIncomeMap[pop.Name] = income;
                        }
                        else
                        {
                            popIncomeMap[pop.Name] = existingIncome + income;
                        }
                    }
                }
            }

            decimal totalCorporateProfits = 0m;
            foreach (var corp in Market.AllCorporations)
            {
                totalCorporateProfits += (decimal)(corp.Budget * 0.1); // Simple profit approximation
            }

            // Calculate total land value - optimized to avoid nested SelectMany
            decimal totalLandValue = 0m;
            foreach (var state in country.States)
            {
                foreach (var city in state.Cities)
                {
                    if (city.ProceduralData?.Parcels != null)
                    {
                        foreach (var parcel in city.ProceduralData.Parcels)
                        {
                            totalLandValue += (decimal)parcel.LandValue;
                        }
                    }
                }
            }
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

        /// <summary>
        /// Generate realistic city names for a state based on its name.
        /// </summary>
        /// <param name="stateName">The name of the state.</param>
        /// <param name="numCities">The number of cities to generate names for.</param>
        /// <param name="random">Random generator instance.</param>
        /// <returns>List of generated city names.</returns>
        private static List<string> GenerateCityNamesForState(string stateName, int numCities, Random random)
        {
            var cityNames = new List<string>();
            var suffixes = new[] { "ville", "burg", "ton", "mouth", "port", "land", "haven", "field", "wood", "shire" };

            for (int i = 0; i < numCities; i++)
            {
                // Randomly decide on a suffix or not
                bool hasSuffix = random.Next(2) == 0;
                string suffix = hasSuffix ? suffixes[random.Next(suffixes.Length)] : "";

                // Combine state name fragment with suffix
                string cityName = $"{stateName.Substring(0, Math.Min(3, stateName.Length)).ToLower()}-{i + 1}{suffix}";
                cityNames.Add(cityName);
            }

            return cityNames;
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
        public int BaseProductionCapacity { get; }
        public int WorkersEmployed { get; set; } // This might become a sum of employed from JobSlots or represent total workforce
        public Dictionary<string, int> JobSlots { get; set; }
        public Dictionary<string, int> ActualEmployed { get; set; } // Tracks actual number employed in each slot type
        public Building BuildingData { get; set; }

        public Factory(string name, int productionCapacity)
        {
            Name = name;
            ProductionCapacity = productionCapacity;
            BaseProductionCapacity = productionCapacity;
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

            if (BuildingData == null)
            {
                return;
            }

            int currentProductionCapacity = CalculateCurrentCapacity();

            double totalInputCost = 0;
            bool allInputsAvailableInStockpile = true;
            foreach (var input in InputGoods)
            {
                if (!cityStockpile.TryGetValue(input.Name, out var stockItem) || stockItem.Quantity < input.Quantity * currentProductionCapacity)
                {
                    allInputsAvailableInStockpile = false;
                    break;
                }
                // Use city.LocalPrices for input cost calculation
                double inputPrice = city.LocalPrices.TryGetValue(input.Name, out var localPrice) ? localPrice : input.BasePrice;
                totalInputCost += (input.Quantity * currentProductionCapacity) * inputPrice;
            }

            if (!allInputsAvailableInStockpile) return;
            if (this.OwnerCorporation.Budget < totalInputCost) return;

            this.OwnerCorporation.Budget -= totalInputCost;
            city.Budget += totalInputCost;

            foreach (var input in InputGoods)
            {
                cityStockpile[input.Name].Quantity -= input.Quantity * currentProductionCapacity;
                // Update city.LocalDemand
                if (!city.LocalDemand.TryGetValue(input.Name, out var currentDemand))
                {
                    city.LocalDemand[input.Name] = input.Quantity * currentProductionCapacity;
                }
                else
                {
                    city.LocalDemand[input.Name] = currentDemand + input.Quantity * currentProductionCapacity;
                }
            }

            double totalOutputValue = 0;
            foreach (var output in OutputGoods)
            {
                if (!cityStockpile.ContainsKey(output.Name))
                    cityStockpile[output.Name] = new Good(output.Name, output.BasePrice, output.Category);

                cityStockpile[output.Name].Quantity += output.Quantity * currentProductionCapacity;
                // Use city.LocalPrices for output value calculation
                double currentMarketPrice = city.LocalPrices.TryGetValue(output.Name, out var localPrice) ? localPrice : output.BasePrice;
                totalOutputValue += (output.Quantity * currentProductionCapacity) * currentMarketPrice;

                // Update city.LocalSupply
                if (!city.LocalSupply.TryGetValue(output.Name, out var currentSupply))
                {
                    city.LocalSupply[output.Name] = output.Quantity * currentProductionCapacity;
                }
                else
                {
                    city.LocalSupply[output.Name] = currentSupply + output.Quantity * currentProductionCapacity;
                }
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
        public static void ResetCitySupplyDemand(City city)
        {
            if (city == null || city.LocalSupply == null || city.LocalDemand == null) return;

            // Reset values without creating copies - we're only setting to 0, not modifying the collection
            foreach (var key in city.LocalSupply.Keys)
            {
                city.LocalSupply[key] = 0;
            }
            foreach (var key in city.LocalDemand.Keys)
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

        public static void ResolveInterCityTrade(List<City> allCities, List<Country> allCountries, double baseTradeCostPerUnit = 0.1)
        {
            if (allCities == null || allCities.Count < 2) return; // Need at least two cities for trade
            if (allCountries == null || allCountries.Count == 0) return;

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
                            if (Economy_sim.GlobalMarket.Instance != null)
                            {
                                string exporterCountry = GetCountryNameForCity(exporter, allCountries);
                                string importerCountry = GetCountryNameForCity(importer, allCountries);
                                Economy_sim.GlobalMarket.Instance.RecordTrade(
                                    goodName,
                                    exporterCountry,
                                    importerCountry,
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
                FactoryBlueprint chosenBlueprint = FactoryBlueprints.AllBlueprints
                    .Where(b => {
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

                    Factory newFactory = new Factory(newFactoryName, newFactoryBaseCapacity);
                    
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

    public static class CityEconomy
    {
        public static void ProcessCityEconomy(City city)
        {
            city.BuyOrders.Clear();
            city.SellOrders.Clear();

            // Cache factories list to avoid multiple copies from thread-safe property
            var cityFactories = city.Factories;

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
            foreach (var goodKey in city.ExportableSurplus.Keys) city.ExportableSurplus[goodKey] = 0;
            foreach (var goodKey in city.ImportNeeds.Keys) city.ImportNeeds[goodKey] = 0;

            foreach (var factory in cityFactories)
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

            foreach (var factory in cityFactories)
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

            foreach (var factory in cityFactories)
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
            foreach (var factory in cityFactories)
            {
                foreach (var slotEntry in factory.JobSlots)
                {
                    if (!totalAvailableSlots.TryGetValue(slotEntry.Key, out var currentSlots))
                    {
                        totalAvailableSlots[slotEntry.Key] = slotEntry.Value;
                    }
                    else
                    {
                        totalAvailableSlots[slotEntry.Key] = currentSlots + slotEntry.Value;
                    }
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
                    foreach (var factory in cityFactories.Where(f => f.JobSlots.ContainsKey(jobType)))
                    {
                        if (assignedToFactories == 0) break;
                        int factoryActualEmployedForType = factory.ActualEmployed.TryGetValue(jobType, out var actualEmployed) ? actualEmployed : 0;
                        int slotsInFactoryForType = factory.JobSlots[jobType];
                        int canAssignToFactory = Math.Min(assignedToFactories, slotsInFactoryForType - factoryActualEmployedForType);
                        if (canAssignToFactory > 0)
                        {
                            if (!factory.ActualEmployed.TryGetValue(jobType, out var currentActual))
                            {
                                factory.ActualEmployed[jobType] = canAssignToFactory;
                            }
                            else
                            {
                                factory.ActualEmployed[jobType] = currentActual + canAssignToFactory;
                            }
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
                    if (!city.ExportableSurplus.TryGetValue(good, out var currentSurplus))
                    {
                        city.ExportableSurplus[good] = surplusAmount;
                    }
                    else
                    {
                        city.ExportableSurplus[good] = currentSurplus + surplusAmount;
                    }
                    
                    // REMOVED: Market.SellToCityMarket(city, good, surplusAmount, null); 
                    // The surplus is now earmarked for export, not sold back to local market immediately.
                }
            }

            // Generate sell orders for factories (for intended output, not just stockpile)
            foreach (var factory in cityFactories)
            {
                foreach (var output in factory.OutputGoods)
                {
                    string good = output.Name;
                    int possible = factory.ProductionCapacity; // Simplified: assume full capacity can be offered
                    // More complex: check inputs available to the factory owner corp (not city stockpile for this offer)

                    int offeredQuantity = output.Quantity * possible; 
                    if (offeredQuantity > 0) 
                    {
                        double currentPrice = city.LocalPrices.TryGetValue(good, out var localPrice) 
                            ? localPrice 
                            : (Market.GoodDefinitions.TryGetValue(good, out var goodDef) ? goodDef.BasePrice : 5.0);
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

            DebugLogger.Log($"[CalculateQualityOfLife] Healthcare: {healthcare}, Education: {education}, Housing: {housing}, Employment: {employment}", DebugLogger.LogCategory.Pop);

            // Weighted average of factors
            double qualityOfLife = (healthcare * 0.3) + (education * 0.3) + (housing * 0.2) + (employment * 0.2);
            DebugLogger.Log($"[CalculateQualityOfLife] Calculated QoL: {qualityOfLife}", DebugLogger.LogCategory.Pop);

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
    }
}