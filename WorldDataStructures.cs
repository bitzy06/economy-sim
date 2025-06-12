using System.Collections.Generic;

namespace StrategyGame
{
    // DTO for initial factory setup in a city
    public class InitialFactoryData
    {
        public string FactoryTypeName { get; set; } // Matches a FactoryBlueprint typeName
        public int Capacity { get; set; }
        // public string OwnerCorpName { get; set; } // Optional: for pre-assigning ownership
    }

    // DTO for City data from JSON
    public class CityData
    {
        public string Name { get; set; }
        public int InitialPopulation { get; set; }
        public double InitialBudget { get; set; }
        public double TaxRate { get; set; }
        public double CityExpenses { get; set; }
        public List<InitialFactoryData> InitialFactories { get; set; }
        // Add other city-specific initial properties if needed, e.g., starting stockpile
    }

    // DTO for State data from JSON
    public class StateData
    {
        public string Name { get; set; }
        public double TaxRate { get; set; }
        public double StateExpenses { get; set; }
        // Population and Budget for states could be calculated from cities or set explicitly
        public int InitialPopulation { get; set; } // Optional: if not summing from cities
        public double InitialBudget { get; set; }   // Optional: if not summing/distributing
        public List<CityData> Cities { get; set; }
    }

    // DTO for Country data from JSON
    public class CountryData
    {
        public string Name { get; set; }
        public double TaxRate { get; set; }
        public double NationalExpenses { get; set; }
        // Population and Budget for countries could be calculated from states or set explicitly
        public int InitialPopulation { get; set; } // Optional: if not summing from states
        public double InitialBudget { get; set; }   // Optional: if not summing/distributing
        public List<StateData> States { get; set; }
        public bool IsPlayerControlled { get; set; } // To identify the player's starting nation
    }

    // Root object for the JSON file
    public class WorldSetupData
    {
        public List<CountryData> Countries { get; set; }
    }
} 