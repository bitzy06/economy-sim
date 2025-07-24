using System.Collections.Generic;

namespace StrategyGame
{
    public class Country
    {
        public string Name { get; set; }
        public List<State> States { get; set; }
        // Budget now reflects the treasury, managed more closely with FinancialSystem
        public double Budget { get; set; }
        public int Population { get; set; }
        // public double TaxRate { get; set; } // Replaced by FinancialSystem.TaxPolicies
        public double NationalExpenses { get; set; } // General national expenses
        public Dictionary<string, double> Resources { get; private set; }
        public NationalFinancialSystem FinancialSystem { get; private set; }
        public Government Government { get; private set; }

        public Country(string name)
        {
            Name = name;
            States = new List<State>();
            Budget = 1000000; // Initial treasury balance
            Population = 10000000;
            Resources = new Dictionary<string, double>();
            // Initialize the financial system for the country
            FinancialSystem = new NationalFinancialSystem(name, (decimal)Budget, 50000m, CurrencyStandard.Fiat);
            // Initialize basic government structure
            Government = new Government();
            Government.Parties.Add(new PoliticalParty { Name = $"{name} Conservative Party", ShareOfGovernment = 0.5 });
            Government.Parties.Add(new PoliticalParty { Name = $"{name} Liberal Party", ShareOfGovernment = 0.5 });
        }

        public void AddResource(string resourceName, double amount)
        {
            if (amount <= 0) return;
            if (Resources.ContainsKey(resourceName))
            {
                Resources[resourceName] += amount;
            }
            else
            {
                Resources[resourceName] = amount;
            }
        }

        public bool RemoveResource(string resourceName, double amount)
        {
            if (amount <= 0) return true; // Nothing to remove
            if (Resources.ContainsKey(resourceName) && Resources[resourceName] >= amount)
            {
                Resources[resourceName] -= amount;
                return true;
            }
            return false; // Not enough resource, or resource doesn't exist
        }

        public double GetResourceAmount(string resourceName)
        {
            if (Resources.ContainsKey(resourceName))
            {
                return Resources[resourceName];
            }
            return 0; // Resource not found
        }

        public void DistributeFunds()
        {
            if (States.Count == 0) return;
            // This distribution is a form of government spending.
            double totalToDistribute = Budget * 0.1; // Example: 10% of current liquid budget
            if (totalToDistribute <= 0) return;

            // Ensure the country has enough budget to distribute
            if (Budget < totalToDistribute)
            {
                // Optionally handle this case, e.g., log a warning or distribute less
                totalToDistribute = Budget; 
            }

            if (totalToDistribute <= 0) return;

            double perState = totalToDistribute / States.Count;
            foreach (var state in States)
            {
                state.Budget += perState;
            }
            
            // This spending reduces the central treasury (Budget).
            Budget -= totalToDistribute;
            // This could also be recorded as a specific type of government expenditure in the financial system if desired,
            // for now, direct adjustment to Budget which the FinancialSystem might read or be updated with.
        }

        public void SimulateTurn()
        {
            foreach (var state in States)
            {
                foreach (var city in state.Cities)
                {
                    Market.ResetCitySupplyDemand(city);
                    Market.SimulateCityEconomy(city);
                    Market.UpdateCityPrices(city);
                }
            }
        }
    }
}