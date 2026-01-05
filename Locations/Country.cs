using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    public class Country
    {
        public string Name { get; set; }
        public List<State> States { get; set; }
        // Budget now reflects the treasury, managed more closely with FinancialSystem
        public double Budget { get; set; }
        public int Population { get; set; }
        public decimal GDP { get; set; } // Gross Domestic Product calculated from factory output
        // public double TaxRate { get; set; } // Replaced by FinancialSystem.TaxPolicies
        public double NationalExpenses { get; set; } // General national expenses
        public Dictionary<string, double> Resources { get; private set; }
        public NationalFinancialSystem FinancialSystem { get; private set; }
        public Government Government { get; private set; }

        public Country(string name)
        {
            Name = name;
            States = new List<State>();
            Budget = 0; // Will be calculated from states after generation
            Population = 0; // Will be calculated from states after generation
            Resources = new Dictionary<string, double>();
            // Initialize the financial system for the country
            FinancialSystem = new NationalFinancialSystem(name, 0m, 50000m, CurrencyStandard.Fiat);
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



        /// <summary>
        /// Update country population, budget, and GDP based on states
        /// GDP is aggregated from state GDPs which are calculated from factory output
        /// </summary>
        public void UpdateAggregatesFromStates()
        {
            // First update all state populations, budgets, and GDPs from their cities
            foreach (var state in States)
            {
                state.UpdateAggregatesFromCities();
            }
            // Then sum up all state populations, budgets, and GDPs
            Population = States?.Sum(s => s.Population) ?? 0;
            Budget = States?.Sum(s => s.Budget) ?? 0;
            GDP = States?.Sum(s => s.GDP) ?? 0m;
            
            // Update financial system with new budget
            if (FinancialSystem != null)
            {
                FinancialSystem = new NationalFinancialSystem(Name, (decimal)Budget, 50000m, CurrencyStandard.Fiat);
            }
        }
        
        /// <summary>
        /// Update country population based on states
        /// </summary>
        public void UpdatePopulationFromStates()
        {
            // First update all state populations from their cities
            foreach (var state in States)
            {
                state.UpdatePopulationFromCities();
            }
            // Then sum up all state populations
            Population = States?.Sum(s => s.Population) ?? 0;
        }
    }
}