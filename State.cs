using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    public class State
    {
        public string Name { get; set; }
        public List<City> Cities { get; set; }
        public double Budget { get; set; }
        public int Population { get; set; }
        public double TaxRate { get; set; } // Percentage (e.g., 0.1 for 10%)
        public double StateExpenses { get; set; }

        public State(string name)
        {
            Name = name;
            Cities = new List<City>();
            Budget = 0; // Will be calculated from cities
            Population = 0; // Will be calculated from cities
        }

        public void DistributeFunds()
        {
            if (Cities.Count == 0) return;
            double perCity = Budget * 0.1 / Cities.Count; // Example: distribute 10% of budget equally
            foreach (var city in Cities)
            {
                city.Budget += perCity;
                Budget -= perCity;
            }
        }

        /// <summary>
        /// Update state population and budget based on cities
        /// </summary>
        public void UpdatePopulationFromCities()
        {
            Population = Cities?.Sum(c => c.Population) ?? 0;
        }

        /// <summary>
        /// Update state budget based on cities
        /// </summary>
        public void UpdateBudgetFromCities()
        {
            Budget = Cities?.Sum(c => c.Budget) ?? 0;
        }

        /// <summary>
        /// Update all state aggregates from cities (population, budget, etc.)
        /// </summary>
        public void UpdateAggregatesFromCities()
        {
            Population = Cities?.Sum(c => c.Population) ?? 0;
            Budget = Cities?.Sum(c => c.Budget) ?? 0;
        }
    }
} 