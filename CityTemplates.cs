using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Defines different types of cities based on their primary economic focus
    /// </summary>
    public enum CityType
    {
        Farming,
        Mining,
        Manufacturing,
        Trading,
        Fishing,
        MixedIndustrial,
        Agricultural,
        TechHub,
        PortCity,
        CapitalCity
    }

    /// <summary>
    /// Template defining the economic characteristics of a city type
    /// </summary>
    public class CityTemplate
    {
        public CityType Type { get; set; }
        public string Description { get; set; }
        
        // Population distribution percentages (should sum to 1.0)
        public Dictionary<string, double> PopulationDistribution { get; set; }
        
        // Factory types that this city specializes in (with weight for selection)
        public Dictionary<string, double> FactoryWeights { get; set; }
        
        // Base population range
        public int MinPopulation { get; set; }
        public int MaxPopulation { get; set; }
        
        // Economic multipliers
        public double BudgetMultiplier { get; set; }
        public double ExpenseMultiplier { get; set; }
        public double IncomeMultiplier { get; set; }
        
        // Starting stockpile bias (goods this city produces more of)
        public Dictionary<string, int> StockpileBias { get; set; }

        public CityTemplate()
        {
            PopulationDistribution = new Dictionary<string, double>();
            FactoryWeights = new Dictionary<string, double>();
            StockpileBias = new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Manages city templates and provides methods to apply them
    /// </summary>
    public static class CityTemplateManager
    {
        private static Dictionary<CityType, CityTemplate> _templates;

        static CityTemplateManager()
        {
            InitializeTemplates();
        }

        private static void InitializeTemplates()
        {
            _templates = new Dictionary<CityType, CityTemplate>();

            // Farming City - Focus on agricultural production
            _templates[CityType.Farming] = new CityTemplate
            {
                Type = CityType.Farming,
                Description = "Agricultural center focused on food production",
                MinPopulation = 50000,
                MaxPopulation = 500000,
                BudgetMultiplier = 0.8,
                ExpenseMultiplier = 0.7,
                IncomeMultiplier = 0.9,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.60 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.08 },
                    { "Managers", 0.05 },
                    { "Clerks", 0.02 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Grain Farm", 3.0 },
                    { "Cattle Ranch", 2.5 },
                    { "Sugar Plantation", 2.0 },
                    { "Bakery", 2.0 },
                    { "Meat Packing Plant", 1.5 },
                    { "Cotton Plantation", 1.5 },
                    { "Coffee Plantation", 1.0 },
                    { "Tea Plantation", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Grain", 15000 },
                    { "Livestock", 5000 },
                    { "Bread", 10000 }
                }
            };

            // Mining City - Focus on raw resource extraction
            _templates[CityType.Mining] = new CityTemplate
            {
                Type = CityType.Mining,
                Description = "Mining center extracting raw materials",
                MinPopulation = 100000,
                MaxPopulation = 800000,
                BudgetMultiplier = 1.2,
                ExpenseMultiplier = 1.1,
                IncomeMultiplier = 1.3,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.55 },
                    { "Craftsmen", 0.20 },
                    { "Engineers", 0.15 },
                    { "Managers", 0.07 },
                    { "Clerks", 0.03 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Coal Mine", 3.0 },
                    { "Iron Mine", 3.0 },
                    { "Copper Mine", 2.5 },
                    { "Tin Mine", 2.0 },
                    { "Lead Mine", 2.0 },
                    { "Zinc Mine", 2.0 },
                    { "Limestone Quarry", 1.5 },
                    { "Salt Mine", 1.5 },
                    { "Sulphur Mine", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Coal", 10000 },
                    { "Iron", 8000 },
                    { "Copper Ore", 5000 }
                }
            };

            // Manufacturing City - Focus on processing and production
            _templates[CityType.Manufacturing] = new CityTemplate
            {
                Type = CityType.Manufacturing,
                Description = "Industrial center processing raw materials",
                MinPopulation = 200000,
                MaxPopulation = 2000000,
                BudgetMultiplier = 1.5,
                ExpenseMultiplier = 1.3,
                IncomeMultiplier = 1.6,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.40 },
                    { "Craftsmen", 0.30 },
                    { "Engineers", 0.18 },
                    { "Managers", 0.09 },
                    { "Clerks", 0.03 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Steel Mill", 3.0 },
                    { "Textile Mill", 2.5 },
                    { "Machine Parts Factory", 2.5 },
                    { "Tool Factory", 2.0 },
                    { "Sawmill", 2.0 },
                    { "Paper Mill", 1.5 },
                    { "Clothing Factory", 2.0 },
                    { "Furniture Factory", 1.5 },
                    { "Copper Smelter", 1.5 },
                    { "Cement Plant", 1.5 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Steel", 8000 },
                    { "Fabric", 6000 },
                    { "Tools", 5000 }
                }
            };

            // Trading City - Diverse economy, commercial focus
            _templates[CityType.Trading] = new CityTemplate
            {
                Type = CityType.Trading,
                Description = "Commercial hub with diverse industries",
                MinPopulation = 150000,
                MaxPopulation = 1500000,
                BudgetMultiplier = 1.8,
                ExpenseMultiplier = 1.5,
                IncomeMultiplier = 1.7,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.30 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.15 },
                    { "Managers", 0.15 },
                    { "Clerks", 0.15 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Printing Press", 2.0 },
                    { "Luxury Tailor", 2.0 },
                    { "Furniture Factory", 1.5 },
                    { "Clothing Factory", 1.5 },
                    { "Bakery", 1.5 },
                    { "Tea Factory", 1.0 },
                    { "Coffee Roastery", 1.0 },
                    { "Tobacco Factory", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Cloth", 7000 },
                    { "Books", 4000 },
                    { "Furniture", 5000 }
                }
            };

            // Fishing City - Coastal, food processing
            _templates[CityType.Fishing] = new CityTemplate
            {
                Type = CityType.Fishing,
                Description = "Coastal city focused on fishing and seafood",
                MinPopulation = 80000,
                MaxPopulation = 600000,
                BudgetMultiplier = 1.0,
                ExpenseMultiplier = 0.9,
                IncomeMultiplier = 1.1,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.55 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.10 },
                    { "Managers", 0.07 },
                    { "Clerks", 0.03 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Fishing Wharf", 4.0 },
                    { "Cannery", 3.0 },
                    { "Salt Mine", 2.0 },
                    { "Bakery", 1.5 },
                    { "Sawmill", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Fish", 12000 },
                    { "Canned Goods", 8000 },
                    { "Salt", 5000 }
                }
            };

            // Mixed Industrial - Balanced production
            _templates[CityType.MixedIndustrial] = new CityTemplate
            {
                Type = CityType.MixedIndustrial,
                Description = "Balanced industrial center with varied production",
                MinPopulation = 180000,
                MaxPopulation = 1200000,
                BudgetMultiplier = 1.3,
                ExpenseMultiplier = 1.2,
                IncomeMultiplier = 1.4,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.45 },
                    { "Craftsmen", 0.28 },
                    { "Engineers", 0.15 },
                    { "Managers", 0.09 },
                    { "Clerks", 0.03 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Steel Mill", 2.0 },
                    { "Textile Mill", 2.0 },
                    { "Bakery", 2.0 },
                    { "Sawmill", 1.5 },
                    { "Clothing Factory", 1.5 },
                    { "Tool Factory", 1.5 },
                    { "Grain Farm", 1.0 },
                    { "Coal Mine", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Steel", 5000 },
                    { "Bread", 6000 },
                    { "Cloth", 5000 }
                }
            };

            // Tech Hub - Advanced manufacturing
            _templates[CityType.TechHub] = new CityTemplate
            {
                Type = CityType.TechHub,
                Description = "Advanced manufacturing and innovation center",
                MinPopulation = 150000,
                MaxPopulation = 1800000,
                BudgetMultiplier = 2.0,
                ExpenseMultiplier = 1.7,
                IncomeMultiplier = 2.2,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.25 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.30 },
                    { "Managers", 0.12 },
                    { "Clerks", 0.08 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Machine Parts Factory", 3.0 },
                    { "Electronics Plant", 2.5 },
                    { "Tool Factory", 2.0 },
                    { "Chemicals Plant", 2.0 },
                    { "Oil Refinery", 1.5 },
                    { "Rubber Processor", 1.5 },
                    { "Printing Press", 1.5 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Machine Parts", 6000 },
                    { "Tools", 5000 },
                    { "Basic Chemicals", 4000 }
                }
            };

            // Port City - Trade and shipping
            _templates[CityType.PortCity] = new CityTemplate
            {
                Type = CityType.PortCity,
                Description = "Major port with shipping and trade focus",
                MinPopulation = 250000,
                MaxPopulation = 2500000,
                BudgetMultiplier = 2.2,
                ExpenseMultiplier = 1.8,
                IncomeMultiplier = 2.0,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.35 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.15 },
                    { "Managers", 0.15 },
                    { "Clerks", 0.10 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Fishing Wharf", 2.5 },
                    { "Cannery", 2.0 },
                    { "Sawmill", 2.0 },
                    { "Steel Mill", 1.5 },
                    { "Clothing Factory", 1.5 },
                    { "Furniture Factory", 1.5 },
                    { "Luxury Tailor", 1.5 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Fish", 8000 },
                    { "Lumber", 7000 },
                    { "Cloth", 6000 }
                }
            };

            // Capital City - Administrative center with diverse economy
            _templates[CityType.CapitalCity] = new CityTemplate
            {
                Type = CityType.CapitalCity,
                Description = "National capital with government and diverse industries",
                MinPopulation = 500000,
                MaxPopulation = 5000000,
                BudgetMultiplier = 2.5,
                ExpenseMultiplier = 2.0,
                IncomeMultiplier = 2.3,
                PopulationDistribution = new Dictionary<string, double>
                {
                    { "Laborers", 0.30 },
                    { "Craftsmen", 0.25 },
                    { "Engineers", 0.18 },
                    { "Managers", 0.17 },
                    { "Clerks", 0.10 }
                },
                FactoryWeights = new Dictionary<string, double>
                {
                    { "Printing Press", 3.0 },
                    { "Luxury Tailor", 2.5 },
                    { "Furniture Factory", 2.0 },
                    { "Bakery", 2.0 },
                    { "Steel Mill", 1.5 },
                    { "Machine Parts Factory", 1.5 },
                    { "Automobile Plant", 1.5 },
                    { "Arms Factory", 1.0 }
                },
                StockpileBias = new Dictionary<string, int>
                {
                    { "Books", 8000 },
                    { "Luxury Clothes", 5000 },
                    { "Furniture", 7000 }
                }
            };
        }

        public static CityTemplate GetTemplate(CityType type)
        {
            return _templates.ContainsKey(type) ? _templates[type] : _templates[CityType.MixedIndustrial];
        }

        public static CityType GetRandomCityType(Random random)
        {
            var types = Enum.GetValues(typeof(CityType)).Cast<CityType>().ToArray();
            return types[random.Next(types.Length)];
        }

        public static CityType GetWeightedRandomCityType(Random random, Dictionary<CityType, double> weights = null)
        {
            // Default weights if none provided
            weights ??= new Dictionary<CityType, double>
            {
                { CityType.Farming, 2.5 },
                { CityType.Mining, 2.0 },
                { CityType.Manufacturing, 2.5 },
                { CityType.Trading, 2.0 },
                { CityType.Fishing, 1.5 },
                { CityType.MixedIndustrial, 3.0 },
                { CityType.Agricultural, 1.5 },
                { CityType.TechHub, 1.0 },
                { CityType.PortCity, 1.5 },
                { CityType.CapitalCity, 0.5 }
            };

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

            return CityType.MixedIndustrial;
        }

        public static List<CityType> DetermineStateCityTypes(int cityCount, Random random, bool hasCapital = false)
        {
            var cityTypes = new List<CityType>();

            // First city in first state of a country should be capital
            if (hasCapital)
            {
                cityTypes.Add(CityType.CapitalCity);
                cityCount--;
            }

            // Determine types for remaining cities with weighted distribution
            for (int i = 0; i < cityCount; i++)
            {
                cityTypes.Add(GetWeightedRandomCityType(random));
            }

            return cityTypes;
        }
    }
}
