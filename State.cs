using System.Collections.Generic;

namespace StrategyGame
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
            Budget = 100000; // Example starting budget
            Population = 1000000; // Example starting population
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
    }
} 