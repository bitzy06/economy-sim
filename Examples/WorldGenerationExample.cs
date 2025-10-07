using System;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Example demonstrating the procedural world generation system
    /// </summary>
    public static class WorldGenerationExample
    {
        public static void RunExample()
        {
            Console.WriteLine("=== Procedural World Generation Example ===\n");

            // Example 1: Basic world generation
            Example1_BasicGeneration();

            // Example 2: Custom parameters
            Example2_CustomParameters();

            // Example 3: Deterministic generation with seed
            Example3_DeterministicGeneration();

            // Example 4: Analyzing generated world
            Example4_AnalyzeWorld();
        }

        /// <summary>
        /// Example 1: Generate a world with default parameters
        /// </summary>
        private static void Example1_BasicGeneration()
        {
            Console.WriteLine("--- Example 1: Basic Generation ---");

            var (countries, corporations) = Economy.InitializeWorldEconomy();

            Console.WriteLine($"Generated world:");
            Console.WriteLine($"  Countries: {countries.Count}");
            Console.WriteLine($"  States: {countries.Sum(c => c.States.Count)}");
            Console.WriteLine($"  Cities: {countries.SelectMany(c => c.States).Sum(s => s.Cities.Count)}");
            Console.WriteLine($"  Corporations: {corporations.Count}");
            Console.WriteLine();
        }

        /// <summary>
        /// Example 2: Generate a world with custom parameters
        /// </summary>
        private static void Example2_CustomParameters()
        {
            Console.WriteLine("--- Example 2: Custom Parameters ---");

            // Create a larger world
            var (countries, corporations) = Economy.InitializeWorldEconomy(
                numCountries: 15,           // More countries
                numStatesPerCountry: 4,     // Fewer states per country
                numCitiesPerState: 5        // More cities per state
            );

            Console.WriteLine($"Custom world:");
            Console.WriteLine($"  Countries: {countries.Count}");
            Console.WriteLine($"  Average cities per country: {countries.Average(c => c.States.Sum(s => s.Cities.Count)):F1}");
            Console.WriteLine();
        }

        /// <summary>
        /// Example 3: Generate reproducible worlds using a seed
        /// </summary>
        private static void Example3_DeterministicGeneration()
        {
            Console.WriteLine("--- Example 3: Deterministic Generation ---");

            int seed = 12345;

            // Generate first world
            var (countries1, corps1) = Economy.InitializeWorldEconomy(seed: seed);
            var firstCityName = countries1.First().States.First().Cities.First().Name;
            var firstCityPop = countries1.First().States.First().Cities.First().Population;

            Console.WriteLine($"First generation (seed {seed}):");
            Console.WriteLine($"  First city: {firstCityName} (pop: {firstCityPop:N0})");

            // Generate second world with same seed
            var (countries2, corps2) = Economy.InitializeWorldEconomy(seed: seed);
            var secondCityName = countries2.First().States.First().Cities.First().Name;
            var secondCityPop = countries2.First().States.First().Cities.First().Population;

            Console.WriteLine($"Second generation (seed {seed}):");
            Console.WriteLine($"  First city: {secondCityName} (pop: {secondCityPop:N0})");

            Console.WriteLine($"  Identical: {firstCityName == secondCityName && firstCityPop == secondCityPop}");
            Console.WriteLine();
        }

        /// <summary>
        /// Example 4: Analyze the generated world structure
        /// </summary>
        private static void Example4_AnalyzeWorld()
        {
            Console.WriteLine("--- Example 4: World Analysis ---");

            var (countries, corporations) = Economy.InitializeWorldEconomy(
                numCountries: 5,
                numStatesPerCountry: 3,
                numCitiesPerState: 4
            );

            // Analyze first country
            var country = countries.First();
            Console.WriteLine($"Country: {country.Name}");
            Console.WriteLine($"  Budget: ${country.Budget:N0}");
            Console.WriteLine($"  Population: {country.Population:N0}");
            Console.WriteLine($"  States: {country.States.Count}");

            // Analyze first state
            var state = country.States.First();
            Console.WriteLine($"\nState: {state.Name}");
            Console.WriteLine($"  Budget: ${state.Budget:N0}");
            Console.WriteLine($"  Tax Rate: {state.TaxRate:P1}");
            Console.WriteLine($"  Cities: {state.Cities.Count}");

            // Analyze each city in the state
            Console.WriteLine($"\nCities in {state.Name}:");
            foreach (var city in state.Cities)
            {
                Console.WriteLine($"  - {city.Name}");
                Console.WriteLine($"      Population: {city.Population:N0}");
                Console.WriteLine($"      Factories: {city.Factories.Count}");
                Console.WriteLine($"      Population Classes: {city.PopClasses.Count}");

                // Show factory types
                var factoryTypes = city.Factories
                    .GroupBy(f => f.OutputGoods.FirstOrDefault()?.Name ?? "Unknown")
                    .Select(g => $"{g.Key} ({g.Count()})")
                    .Take(3);
                Console.WriteLine($"      Top Industries: {string.Join(", ", factoryTypes)}");

                // Show population distribution
                var popDist = city.PopClasses
                    .OrderByDescending(p => p.Size)
                    .Take(3)
                    .Select(p => $"{p.Name} ({p.Size:N0})");
                Console.WriteLine($"      Top Classes: {string.Join(", ", popDist)}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Example showing how to find specific city types
        /// </summary>
        public static void Example5_FindCityTypes()
        {
            Console.WriteLine("--- Example 5: Find City Types ---");

            var (countries, corporations) = Economy.InitializeWorldEconomy();

            // Find all mining cities (cities with many mines)
            var miningCities = countries
                .SelectMany(c => c.States)
                .SelectMany(s => s.Cities)
                .Where(city => city.Factories.Count(f => 
                    f.Name.Contains("Mine") || f.Name.Contains("Quarry")) >= 2)
                .ToList();

            Console.WriteLine($"Mining Cities: {miningCities.Count}");
            foreach (var city in miningCities.Take(3))
            {
                Console.WriteLine($"  - {city.Name}: {city.Factories.Count} factories");
            }

            // Find all farming cities
            var farmingCities = countries
                .SelectMany(c => c.States)
                .SelectMany(s => s.Cities)
                .Where(city => city.Factories.Count(f => 
                    f.Name.Contains("Farm") || f.Name.Contains("Plantation")) >= 2)
                .ToList();

            Console.WriteLine($"\nFarming Cities: {farmingCities.Count}");
            foreach (var city in farmingCities.Take(3))
            {
                Console.WriteLine($"  - {city.Name}: {city.Factories.Count} factories");
            }

            // Find largest cities
            var largestCities = countries
                .SelectMany(c => c.States)
                .SelectMany(s => s.Cities)
                .OrderByDescending(city => city.Population)
                .Take(5);

            Console.WriteLine($"\nLargest Cities:");
            foreach (var city in largestCities)
            {
                Console.WriteLine($"  - {city.Name}: {city.Population:N0} people");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Example showing corporation distribution
        /// </summary>
        public static void Example6_CorporationAnalysis()
        {
            Console.WriteLine("--- Example 6: Corporation Analysis ---");

            var (countries, corporations) = Economy.InitializeWorldEconomy();

            // Group by specialization
            var corpsBySpec = corporations
                .GroupBy(c => c.Specialization)
                .OrderByDescending(g => g.Count());

            Console.WriteLine("Corporations by Specialization:");
            foreach (var group in corpsBySpec)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} corporations");
            }

            // Find largest corporations
            var largestCorps = corporations
                .OrderByDescending(c => c.OwnedFactories.Count)
                .Take(5);

            Console.WriteLine($"\nLargest Corporations:");
            foreach (var corp in largestCorps)
            {
                Console.WriteLine($"  - {corp.Name} ({corp.Specialization})");
                Console.WriteLine($"      Factories: {corp.OwnedFactories.Count}");
                Console.WriteLine($"      Budget: ${corp.Budget:N0}");
            }
            Console.WriteLine();
        }
    }
}
