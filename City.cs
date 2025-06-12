using System.Collections.Generic;
using System.Linq;
using System; // Added for Console
using StrategyGame; // Added to reference Suburb class
using StrategyGame; // Ensure namespace for ProjectType and ConstructionProject is included

namespace StrategyGame
{
    public class City
    {
        public string Name { get; set; }
        public double Budget { get; set; }
        public int Population { get; set; }
        public double TaxRate { get; set; } // Percentage (e.g., 0.1 for 10%)
        public double CityExpenses { get; set; }
        public List<Factory> Factories { get; set; }
        public Dictionary<string, Good> Stockpile { get; set; }
        
        // Local market data
        public Dictionary<string, double> LocalPrices { get; set; }
        public Dictionary<string, int> LocalSupply { get; set; } // Supply generated this turn in this city
        public Dictionary<string, int> LocalDemand { get; set; } // Demand generated this turn in this city

        // For Inter-City Trade
        public Dictionary<string, int> ExportableSurplus { get; set; }
        public Dictionary<string, int> ImportNeeds { get; set; }

        public int Happiness { get; set; }
        public double PopBudget { get; set; }
        public List<PopClass> PopClasses { get; set; }
        public List<BuyOrder> BuyOrders { get; set; }
        public List<SellOrder> SellOrders { get; set; }
        public List<Suburb> Suburbs { get; private set; } // Added Suburbs property
        public List<ConstructionProject> ActiveProjects { get; private set; } // Added to track active construction projects

        public City(string name)
        {
            Name = name;
            Budget = 10000; // Example starting budget
            Population = 100000; // Example starting population
            Factories = new List<Factory>();
            Stockpile = new Dictionary<string, Good>();
            Happiness = 50; // Out of 100
            PopBudget = Population * 0.05; // Example: $0.05 per person per turn
            PopClasses = new List<PopClass>();
            BuyOrders = new List<BuyOrder>();
            SellOrders = new List<SellOrder>();
            Suburbs = new List<Suburb>(); // Initialize suburbs
            ActiveProjects = new List<ConstructionProject>(); // Initialize active projects

            // Initialize local market data structures
            LocalPrices = new Dictionary<string, double>();
            LocalSupply = new Dictionary<string, int>();
            LocalDemand = new Dictionary<string, int>();
            ExportableSurplus = new Dictionary<string, int>();
            ImportNeeds = new Dictionary<string, int>();

            // Initialize local prices from global good definitions and supply/demand to 0
            // This requires Market.GoodDefinitions to be populated before cities are created.
            if (Market.GoodDefinitions != null && Market.GoodDefinitions.Any()) // Ensure definitions are loaded
            {
                foreach (var goodDefPair in Market.GoodDefinitions)
                {
                    LocalPrices[goodDefPair.Key] = goodDefPair.Value.BasePrice;
                    LocalSupply[goodDefPair.Key] = 0;
                    LocalDemand[goodDefPair.Key] = 0;
                    ExportableSurplus[goodDefPair.Key] = 0;
                    ImportNeeds[goodDefPair.Key] = 0;
                }
            }
            else
            {
                // Fallback or warning if Market.GoodDefinitions is not ready - this should not happen in normal flow
                Console.WriteLine($"Warning: Market.GoodDefinitions not populated when creating city {Name}. Local market data may be incomplete.");
            }

            // Create population classes with names matching job types
            DebugLogger.Log($"[City] Creating population classes for city: {Name}");

            var laborers = new PopClass("Laborers", (int)(Population * 0.5), 0.03);
            laborers.Needs["Food"] = 2.0;
            laborers.Needs["Housing"] = 1.0;
            laborers.Needs["Clothing"] = 0.5;
            PopClasses.Add(laborers);
            DebugLogger.Log($"[City] Created Laborers class - Size: {laborers.Size}, Income: {laborers.IncomePerPerson}");

            var craftsmen = new PopClass("Craftsmen", (int)(Population * 0.25), 0.06);
            craftsmen.Needs["Food"] = 3.0;
            craftsmen.Needs["Housing"] = 1.5;
            craftsmen.Needs["Clothing"] = 1.0;
            craftsmen.Needs["Luxury"] = 0.2;
            PopClasses.Add(craftsmen);
            DebugLogger.Log($"[City] Created Craftsmen class - Size: {craftsmen.Size}, Income: {craftsmen.IncomePerPerson}");

            var engineers = new PopClass("Engineers", (int)(Population * 0.15), 0.12);
            engineers.Needs["Food"] = 4.0;
            engineers.Needs["Housing"] = 2.0;
            engineers.Needs["Clothing"] = 1.5;
            engineers.Needs["Luxury"] = 0.5;
            engineers.Needs["Education"] = 1.0;
            PopClasses.Add(engineers);
            DebugLogger.Log($"[City] Created Engineers class - Size: {engineers.Size}, Income: {engineers.IncomePerPerson}");

            var managers = new PopClass("Managers", (int)(Population * 0.07), 0.15);
            managers.Needs["Food"] = 5.0;
            managers.Needs["Housing"] = 3.0;
            managers.Needs["Clothing"] = 2.0;
            managers.Needs["Luxury"] = 1.0;
            managers.Needs["Education"] = 1.5;
            PopClasses.Add(managers);
            DebugLogger.Log($"[City] Created Managers class - Size: {managers.Size}, Income: {managers.IncomePerPerson}");

            var clerks = new PopClass("Clerks", (int)(Population * 0.03), 0.08);
            clerks.Needs["Food"] = 3.5;
            clerks.Needs["Housing"] = 2.0;
            clerks.Needs["Clothing"] = 1.2;
            clerks.Needs["Luxury"] = 0.3;
            clerks.Needs["Education"] = 0.5;
            PopClasses.Add(clerks);
            DebugLogger.Log($"[City] Created Clerks class - Size: {clerks.Size}, Income: {clerks.IncomePerPerson}");
        }

        public void SimulateGrowth()
        {
            double surplus = Budget - CityExpenses;
            if (surplus > 0)
            {
                int growth = (int)(surplus / 1000); // Example: population grows with surplus
                Population += growth;

                // Distribute growth among PopClasses proportionally
                foreach (var pop in PopClasses)
                {
                    int popGrowth = (int)(growth * ((double)pop.Size / Population));
                    pop.Size += popGrowth;
                }

                Budget += surplus * 0.05; // Example: reinvest surplus
            }
        }

        public void AddSuburb(Suburb suburb)
        {
            Suburbs.Add(suburb);
        }

        public double CalculateCityQualityOfLife()
        {
            double suburbQoL = 0;
            if (Suburbs.Count > 0)
            {
                foreach (var suburb in Suburbs)
                {
                    suburbQoL += suburb.CalculateQualityOfLife();
                }
                suburbQoL /= Suburbs.Count; // Average QoL across suburbs
            }

            double popClassQoL = 0;
            if (PopClasses.Count > 0)
            {
                foreach (var pop in PopClasses)
                {
                    popClassQoL += pop.QualityOfLife;
                }
                popClassQoL /= PopClasses.Count; // Average QoL across population classes
            }

            DebugLogger.Log($"[CalculateCityQualityOfLife] Suburb QoL: {suburbQoL}, Population Class QoL: {popClassQoL}");

            double cityQoL = (suburbQoL + popClassQoL) / 2; // Combine with equal weight
            DebugLogger.Log($"[CalculateCityQualityOfLife] Calculated City QoL: {cityQoL}");

            return cityQoL;
        }

        public void StartConstructionProject(ConstructionProject project)
        {
            ActiveProjects.Add(project);
        }

        public void ProgressConstruction(decimal availableBudget)
        {
            foreach (var project in ActiveProjects)
            {
                if (project.ProgressProject(1, availableBudget)) // Progress by 1 day
                {
                    if (project.IsComplete())
                    {
                        ApplyProjectEffects(project);
                    }
                }
            }

            ActiveProjects.RemoveAll(p => p.IsComplete()); // Remove completed projects
        }

        private void ApplyProjectEffects(ConstructionProject project)
        {
            switch (project.Type)
            {
                case ProjectType.Housing:
                    IncreaseHousingCapacity((int)project.Output);
                    break;
                case ProjectType.Railway:
                    IncreaseRailwayKilometers(project.Output);
                    break;
            }
        }

        private void IncreaseHousingCapacity(int value)
        {
            foreach (var suburb in Suburbs)
            {
                suburb.HousingCapacity += value / Suburbs.Count; // Distribute housing capacity
            }
        }

        private void IncreaseRailwayKilometers(double value)
        {
            foreach (var suburb in Suburbs)
            {
                suburb.RailwayKilometers += value / Suburbs.Count; // Distribute railway kilometers
            }
        }
    }
}