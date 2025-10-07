using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Generates a complete world with countries, states, and cities using templates
    /// </summary>
    public static class ProceduralWorldGenerator
    {
        /// <summary>
        /// Generate a complete world based on Natural Earth data and city templates
        /// </summary>
        public static (List<Country> countries, List<Corporation> corporations) GenerateWorld(
            WorldSetupData worldData, 
            Random random = null)
        {
            random ??= new Random();
            
            var allCountries = new List<Country>();
            var allCorporations = new List<Corporation>();

            Console.WriteLine("[World Gen] Starting procedural world generation...");

            foreach (var countryData in worldData.Countries)
            {
                var country = GenerateCountry(countryData, random, allCorporations);
                allCountries.Add(country);
            }

            int totalCities = allCountries.Sum(c => c.States.Sum(s => s.Cities.Count));
            int totalFactories = allCountries.Sum(c => c.States.Sum(s => s.Cities.Sum(city => city.Factories.Count)));
            int totalPopClasses = allCountries.Sum(c => c.States.Sum(s => s.Cities.Sum(city => city.PopClasses.Count)));

            Console.WriteLine($"[World Gen] ========================================");
            Console.WriteLine($"[World Gen] World generation complete!");
            Console.WriteLine($"[World Gen] - {allCountries.Count} countries");
            Console.WriteLine($"[World Gen] - {allCountries.Sum(c => c.States.Count)} states");
            Console.WriteLine($"[World Gen] - {totalCities} cities");
            Console.WriteLine($"[World Gen] - {totalPopClasses} population classes");
            Console.WriteLine($"[World Gen] - {totalFactories} factories");
            Console.WriteLine($"[World Gen] - {allCorporations.Count} corporations");
            Console.WriteLine($"[World Gen] ========================================");

            return (allCountries, allCorporations);
        }

        private static Country GenerateCountry(CountryData data, Random random, List<Corporation> corporationPool)
        {
            var country = new Country(data.Name)
            {
                Budget = data.InitialBudget,
                NationalExpenses = data.NationalExpenses,
                Population = 0
            };

            // Set up tax policies
            var incomeTax = new TaxPolicy(TaxType.IncomeTax, (decimal)data.TaxRate, TaxProgressivity.Progressive);
            incomeTax.ProgressiveBrackets[20000m] = (decimal)(data.TaxRate * 0.6);
            incomeTax.ProgressiveBrackets[50000m] = (decimal)data.TaxRate;
            incomeTax.ProgressiveBrackets[100000m] = (decimal)(data.TaxRate * 1.5);
            country.FinancialSystem.AddTaxPolicy(incomeTax);
            country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.CorporateTax, (decimal)(data.TaxRate * 1.2)));
            country.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.ConsumptionTax, (decimal)(data.TaxRate * 0.4)));

            Console.WriteLine($"[World Gen] Generating country: {country.Name} with {data.States.Count} states");
            
            bool isFirstState = true;
            foreach (var stateData in data.States)
            {
                var state = GenerateState(stateData, country, random, corporationPool, isFirstState);
                country.States.Add(state);
                country.Population += state.Population;
                isFirstState = false;
            }

            Console.WriteLine($"[World Gen] ✓ Completed country: {country.Name} - {country.States.Count} states, {country.States.Sum(s => s.Cities.Count)} cities, population: {country.Population:N0}");
            
            return country;
        }

        private static State GenerateState(StateData data, Country country, Random random, List<Corporation> corporationPool, bool isFirstState)
        {
            var state = new State(data.Name)
            {
                Budget = data.InitialBudget,
                TaxRate = data.TaxRate,
                StateExpenses = data.StateExpenses,
                Population = 0
            };

            Console.WriteLine($"[World Gen]  Generating state: {state.Name} with {data.Cities.Count} cities");

            // Determine city types for this state
            var cityTypes = CityTemplateManager.DetermineStateCityTypes(
                data.Cities.Count, 
                random, 
                hasCapital: isFirstState);

            for (int i = 0; i < data.Cities.Count; i++)
            {
                var cityData = data.Cities[i];
                var cityType = i < cityTypes.Count ? cityTypes[i] : CityType.MixedIndustrial;
                
                var city = GenerateCity(cityData, state, country, cityType, random, corporationPool);
                state.Cities.Add(city);
                state.Population += city.Population;
            }

            Console.WriteLine($"[World Gen]  ✓ Completed state: {state.Name} - {state.Cities.Count} cities, population: {state.Population:N0}");

            return state;
        }

        private static City GenerateCity(
            CityData data, 
            State state, 
            Country country, 
            CityType cityType, 
            Random random, 
            List<Corporation> corporationPool)
        {
            var template = CityTemplateManager.GetTemplate(cityType);
            
            // Apply template multipliers to base data
            double adjustedBudget = data.InitialBudget * template.BudgetMultiplier;
            double adjustedExpenses = data.CityExpenses * template.ExpenseMultiplier;
            
            var city = new City(data.Name)
            {
                Budget = adjustedBudget,
                TaxRate = data.TaxRate,
                CityExpenses = adjustedExpenses,
                Population = data.InitialPopulation,
                Happiness = 50
            };

            Console.WriteLine($"[World Gen]   Generating {cityType} city: {city.Name} (initial pop: {city.Population:N0})");

            // Generate population classes based on template
            GeneratePopulationClasses(city, data.InitialPopulation, template, random);
            city.Population = city.PopClasses.Sum(p => p.Size);

            // Generate factories based on template
            GenerateFactories(city, data, template, random, corporationPool);

            // Initialize stockpile with template bias
            InitializeStockpile(city, template, random);

            // Initialize local prices
            InitializeLocalPrices(city);

            // Link procedural city data to the economic simulation
            ProceduralCityBuilder.InitializeCityData(city, template, random);

            Console.WriteLine($"[World Gen]   ✓ Completed city: {city.Name} - {city.PopClasses.Count} pop classes, {city.Factories.Count} factories, final pop: {city.Population:N0}");

            return city;
        }

        private static void GeneratePopulationClasses(City city, int totalPopulation, CityTemplate template, Random random)
        {
            city.PopClasses.Clear();

            foreach (var popDist in template.PopulationDistribution)
            {
                string className = popDist.Key;
                double percentage = popDist.Value;
                
                int classSize = (int)(totalPopulation * percentage);
                if (classSize < 1) classSize = 1;

                // Determine base income based on class type
                double baseIncome = className switch
                {
                    "Laborers" => random.Next(12, 20) * template.IncomeMultiplier,
                    "Craftsmen" => random.Next(20, 35) * template.IncomeMultiplier,
                    "Engineers" => random.Next(40, 70) * template.IncomeMultiplier,
                    "Managers" => random.Next(60, 100) * template.IncomeMultiplier,
                    "Clerks" => random.Next(25, 45) * template.IncomeMultiplier,
                    _ => 20.0
                };

                var popClass = new PopClass(className, classSize, baseIncome);
                
                // Set needs based on class
                SetPopulationNeeds(popClass, className);
                
                city.PopClasses.Add(popClass);
            }
        }

        private static void SetPopulationNeeds(PopClass pop, string className)
        {
            // Basic needs for all classes
            pop.Needs["Bread"] = 2.0;
            pop.Needs["Cloth"] = 1.0;

            // Class-specific needs
            switch (className)
            {
                case "Laborers":
                    // Minimal needs
                    break;
                    
                case "Craftsmen":
                    pop.Needs["Furniture"] = 0.3;
                    break;
                    
                case "Engineers":
                    pop.Needs["Furniture"] = 0.8;
                    pop.Needs["Books"] = 0.5;
                    pop.Needs["Tea"] = 0.3;
                    break;
                    
                case "Managers":
                    pop.Needs["Furniture"] = 1.2;
                    pop.Needs["Books"] = 1.0;
                    pop.Needs["Luxury Clothes"] = 0.5;
                    pop.Needs["Coffee"] = 0.5;
                    break;
                    
                case "Clerks":
                    pop.Needs["Furniture"] = 0.5;
                    pop.Needs["Books"] = 0.3;
                    break;
            }
        }

        private static void GenerateFactories(
            City city, 
            CityData data, 
            CityTemplate template, 
            Random random, 
            List<Corporation> corporationPool)
        {
            // Use factory data from WorldSetupData but filter by template weights
            var factoriesToBuild = new List<(string type, int capacity)>();

            if (data.InitialFactories != null && data.InitialFactories.Any())
            {
                // Use provided factories but weight them according to template
                foreach (var factoryData in data.InitialFactories)
                {
                    // Check if this factory type matches the city template
                    double weight = template.FactoryWeights.ContainsKey(factoryData.FactoryTypeName) 
                        ? template.FactoryWeights[factoryData.FactoryTypeName] 
                        : 0.1;

                    // Random chance based on weight
                    if (random.NextDouble() < Math.Min(1.0, weight / 3.0))
                    {
                        factoriesToBuild.Add((factoryData.FactoryTypeName, factoryData.Capacity));
                    }
                }
            }
            
            // If no factories or too few, generate based on template
            int targetFactoryCount = random.Next(3, 8);
            while (factoriesToBuild.Count < targetFactoryCount && template.FactoryWeights.Any())
            {
                var factoryType = SelectWeightedFactoryType(template.FactoryWeights, random);
                int capacity = random.Next(2, 6);
                factoriesToBuild.Add((factoryType, capacity));
            }

            // Create factories and assign to corporations
            foreach (var (type, capacity) in factoriesToBuild)
            {
                var blueprint = FactoryBlueprints.GetBlueprintForGood(type);
                if (blueprint == null)
                {
                    blueprint = FactoryBlueprints.AllBlueprints.FirstOrDefault(b => b.FactoryTypeName == type);
                }
                
                if (blueprint == null) continue;

                // Find or create a corporation to own this factory
                var corporation = FindOrCreateCorporation(blueprint, city, corporationPool, random);
                
                var factory = CreateFactoryFromBlueprint(blueprint, corporation, capacity, city);
                
                city.Factories.Add(factory);
                corporation.AddFactory(factory);
            }
        }

        public static string SelectWeightedFactoryType(Dictionary<string, double> weights, Random random)
        {
            double totalWeight = weights.Values.Sum();
            double randomValue = random.NextDouble() * totalWeight;
            double cumulativeWeight = 0;

            foreach (var kvp in weights)
            {
                cumulativeWeight += kvp.Value;
                if (randomValue <= cumulativeWeight)
                {
                    return kvp.Key;
                }
            }

            return weights.Keys.FirstOrDefault() ?? "Grain Farm";
        }

        public static Corporation FindOrCreateCorporation(
            FactoryBlueprint blueprint, 
            City city, 
            List<Corporation> corporationPool, 
            Random random)
        {
            // Determine specialization from blueprint
            var specialization = DetermineSpecialization(blueprint);

            // Try to find existing corporation with matching specialization
            var existingCorp = corporationPool
                .Where(c => c.Specialization == specialization || c.Specialization == CorporationSpecialization.Diversified)
                .Where(c => c.OwnedFactories.Count < 10) // Don't let corporations get too large initially
                .OrderBy(_ => random.Next())
                .FirstOrDefault();

            if (existingCorp != null)
            {
                return existingCorp;
            }

            // Create new corporation
            string corpName = GenerateCorporationName(blueprint, city, random);
            var newCorp = new Corporation(corpName, specialization)
            {
                Budget = random.Next(500000, 2000000)
            };

            corporationPool.Add(newCorp);
            
            Console.WriteLine($"[World Gen]     Created corporation: {corpName} ({specialization})");
            
            return newCorp;
        }

        private static CorporationSpecialization DetermineSpecialization(FactoryBlueprint blueprint)
        {
            return blueprint.ProducedGoodCategory switch
            {
                GoodCategory.RawMaterial when blueprint.OutputGood.Name.Contains("Grain") || 
                                               blueprint.OutputGood.Name.Contains("Livestock") ||
                                               blueprint.OutputGood.Name.Contains("Cotton") => CorporationSpecialization.Agriculture,
                GoodCategory.RawMaterial when blueprint.OutputGood.Name.Contains("Mine") ||
                                               blueprint.OutputGood.Name.Contains("Coal") ||
                                               blueprint.OutputGood.Name.Contains("Iron") => CorporationSpecialization.Mining,
                GoodCategory.IndustrialInput => CorporationSpecialization.HeavyIndustry,
                GoodCategory.CapitalGood => CorporationSpecialization.HeavyIndustry,
                GoodCategory.ConsumerProduct => CorporationSpecialization.LightIndustry,
                GoodCategory.ProcessedFood => CorporationSpecialization.Agriculture,
                _ => CorporationSpecialization.Diversified
            };
        }

        private static string GenerateCorporationName(FactoryBlueprint blueprint, City city, Random random)
        {
            string[] prefixes = { "Global", "National", "United", "Imperial", "Royal", "Continental", "Federal" };
            string[] suffixes = { "Industries", "Corporation", "Company", "Holdings", "Enterprises", "Group", "Co." };

            string industry = blueprint.ProducedGoodCategory switch
            {
                GoodCategory.RawMaterial => "Resources",
                GoodCategory.IndustrialInput => "Industrial",
                GoodCategory.ConsumerProduct => "Consumer",
                GoodCategory.ProcessedFood => "Foods",
                GoodCategory.CapitalGood => "Manufacturing",
                _ => "Trading"
            };

            return $"{prefixes[random.Next(prefixes.Length)]} {industry} {suffixes[random.Next(suffixes.Length)]}";
        }

        public static Factory CreateFactoryFromBlueprint(
            FactoryBlueprint blueprint, 
            Corporation owner, 
            int capacity, 
            City city)
        {
            string factoryName = $"{owner.Name}'s {blueprint.FactoryTypeName} #{owner.OwnedFactories.Count(f => f.Name.Contains(blueprint.FactoryTypeName)) + 1}";
            
            var factory = new Factory(factoryName, capacity)
            {
                OwnerCorporation = owner
            };

            // Set up job slots
            int totalJobSlots = capacity * 5;
            foreach (var jobSlot in blueprint.DefaultJobSlotDistribution)
            {
                int slots = (int)Math.Ceiling(totalJobSlots * jobSlot.Value);
                factory.JobSlots[jobSlot.Key] = slots;
            }

            // Set up input goods
            foreach (var inputGood in blueprint.InputGoods)
            {
                factory.InputGoods.Add(new Good(inputGood.Name, inputGood.BasePrice, inputGood.Category, inputGood.Quantity));
            }

            // Set up output goods
            factory.OutputGoods.Add(new Good(
                blueprint.OutputGood.Name, 
                blueprint.OutputGood.BasePrice, 
                blueprint.OutputGood.Category, 
                blueprint.OutputGood.Quantity));

            return factory;
        }

        public static void InitializeStockpile(City city, CityTemplate template, Random random)
        {
            // Start with template biases
            foreach (var bias in template.StockpileBias)
            {
                if (Market.GoodDefinitions.ContainsKey(bias.Key))
                {
                    city.Stockpile[bias.Key] = new Good(
                        bias.Key,
                        Market.GoodDefinitions[bias.Key].BasePrice,
                        Market.GoodDefinitions[bias.Key].Category,
                        bias.Value + random.Next(-1000, 2000));
                }
            }

            // Add basic goods for all cities
            var basicGoods = new[] { "Grain", "Coal", "Iron", "Bread", "Cloth" };
            foreach (var goodName in basicGoods)
            {
                if (!city.Stockpile.ContainsKey(goodName) && Market.GoodDefinitions.ContainsKey(goodName))
                {
                    city.Stockpile[goodName] = new Good(
                        goodName,
                        Market.GoodDefinitions[goodName].BasePrice,
                        Market.GoodDefinitions[goodName].Category,
                        random.Next(2000, 8000));
                }
            }
        }

        public static void InitializeLocalPrices(City city)
        {
            foreach (var good in Market.GoodDefinitions.Values)
            {
                if (!city.LocalPrices.ContainsKey(good.Name))
                {
                    city.LocalPrices[good.Name] = good.BasePrice;
                }
                
                if (!city.LocalSupply.ContainsKey(good.Name))
                {
                    city.LocalSupply[good.Name] = 0;
                }
                
                if (!city.LocalDemand.ContainsKey(good.Name))
                {
                    city.LocalDemand[good.Name] = 0;
                }
            }
        }
    }
}
