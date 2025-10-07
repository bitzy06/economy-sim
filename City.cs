using System.Collections.Generic;
using System.Linq;
using System; // Added for Console

namespace Economy_sim
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
        public List<ConstructionCompany> ConstructionCompanies { get; } = new();
        public CityProceduralData ProceduralData { get; set; }

        public City(string name)
        {
            Name = name;
            Budget = 10000; // Example starting budget
            Population = 100000; // Example starting population
            Factories = new List<Factory>();
            Stockpile = new Dictionary<string, Good>();
            Happiness = 50; // Out of 100
            PopBudget = Population * 0.05; // Example: $0.05 per person per turn
            PopClasses = InitializeDefaultPopClasses(Population);
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
        }

        private List<PopClass> InitializeDefaultPopClasses(int basePopulation)
        {
            DebugLogger.Log($"[City] Creating population classes for city: {Name}", DebugLogger.LogCategory.Pop);

            var classes = new List<PopClass>();

            var laborers = new PopClass("Laborers", (int)(basePopulation * 0.5), 0.03);
            laborers.Needs["Bread"] = 2.0;
            laborers.Needs["Furniture"] = 1.0;
            laborers.Needs["Cloth"] = 0.5;
            classes.Add(laborers);
            DebugLogger.Log($"[City] Created Laborers class - Size: {laborers.Size}, Income: {laborers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var craftsmen = new PopClass("Craftsmen", (int)(basePopulation * 0.25), 0.06);
            craftsmen.Needs["Bread"] = 3.0;
            craftsmen.Needs["Furniture"] = 1.5;
            craftsmen.Needs["Cloth"] = 1.0;
            craftsmen.Needs["Luxury Clothes"] = 0.2;
            classes.Add(craftsmen);
            DebugLogger.Log($"[City] Created Craftsmen class - Size: {craftsmen.Size}, Income: {craftsmen.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var engineers = new PopClass("Engineers", (int)(basePopulation * 0.15), 0.12);
            engineers.Needs["Bread"] = 4.0;
            engineers.Needs["Furniture"] = 2.0;
            engineers.Needs["Cloth"] = 1.5;
            engineers.Needs["Luxury Clothes"] = 0.5;
            engineers.Needs["Books"] = 1.0;
            classes.Add(engineers);
            DebugLogger.Log($"[City] Created Engineers class - Size: {engineers.Size}, Income: {engineers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var managers = new PopClass("Managers", (int)(basePopulation * 0.07), 0.15);
            managers.Needs["Bread"] = 5.0;
            managers.Needs["Furniture"] = 3.0;
            managers.Needs["Cloth"] = 2.0;
            managers.Needs["Luxury Clothes"] = 1.0;
            managers.Needs["Books"] = 1.5;
            classes.Add(managers);
            DebugLogger.Log($"[City] Created Managers class - Size: {managers.Size}, Income: {managers.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            var clerks = new PopClass("Clerks", (int)(basePopulation * 0.03), 0.08);
            clerks.Needs["Bread"] = 3.5;
            clerks.Needs["Furniture"] = 2.0;
            clerks.Needs["Cloth"] = 1.2;
            clerks.Needs["Luxury Clothes"] = 0.3;
            clerks.Needs["Books"] = 0.5;
            classes.Add(clerks);
            DebugLogger.Log($"[City] Created Clerks class - Size: {clerks.Size}, Income: {clerks.IncomePerPerson}", DebugLogger.LogCategory.Pop);

            return classes;
        }

        public void SimulateGrowth()
        {
            double surplus = Budget - CityExpenses;
            if (surplus <= 0)
            {
                HandleOvercrowding();
                return;
            }

            int baseGrowth = (int)(surplus / 1000);
            if (baseGrowth <= 0)
            {
                HandleOvercrowding();
                return;
            }

            int currentPopulation = Math.Max(1, PopClasses.Sum(p => p.Size));
            int allowedGrowth = baseGrowth;

            if (ProceduralData != null)
            {
                int maxPopulation = ProceduralData.TotalResidentialCapacity;
                if (maxPopulation > 0)
                {
                    int availableCapacity = Math.Max(0, maxPopulation - currentPopulation);
                    allowedGrowth = Math.Min(baseGrowth, availableCapacity);

                    if (availableCapacity <= 0)
                    {
                        HandleOvercrowding();
                        return;
                    }
                }
            }

            if (allowedGrowth <= 0)
            {
                HandleOvercrowding();
                return;
            }

            Population = currentPopulation + allowedGrowth;

            foreach (var pop in PopClasses)
            {
                int popGrowth = (int)Math.Round(allowedGrowth * (pop.Size / (double)currentPopulation));
                pop.Size += popGrowth;
            }

            Budget += surplus * 0.05;
        }

        private void HandleOvercrowding()
        {
            if (ProceduralData == null)
            {
                return;
            }

            int totalCapacity = ProceduralData.TotalResidentialCapacity;
            if (totalCapacity <= 0)
            {
                return;
            }

            int currentPopulation = PopClasses.Sum(p => p.Size);
            if (currentPopulation <= totalCapacity)
            {
                return;
            }

            int overflow = currentPopulation - totalCapacity;
            int reduction = Math.Max(1, overflow / Math.Max(1, PopClasses.Count));

            foreach (var pop in PopClasses)
            {
                pop.Size = Math.Max(0, pop.Size - reduction);
            }

            Population = PopClasses.Sum(p => p.Size);
            Happiness = Math.Max(0, Happiness - 2);
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

            DebugLogger.Log($"[CalculateCityQualityOfLife] Suburb QoL: {suburbQoL}, Population Class QoL: {popClassQoL}", DebugLogger.LogCategory.Economy);

            double cityQoL = (suburbQoL + popClassQoL) / 2; // Combine with equal weight
            DebugLogger.Log($"[CalculateCityQualityOfLife] Calculated City QoL: {cityQoL}", DebugLogger.LogCategory.Economy);

            return cityQoL;
        }

        public void RegisterConstructionCompany(ConstructionCompany company)
        {
            if (company == null)
            {
                return;
            }

            if (!ConstructionCompanies.Contains(company))
            {
                ConstructionCompanies.Add(company);
            }
        }

        public void StartConstructionProject(ConstructionProject project, ConstructionCompany company = null)
        {
            if (project == null)
            {
                return;
            }

            if (!ActiveProjects.Contains(project))
            {
                project.OwningCity = this;
                if (company != null)
                {
                    project.AssignedCompany = company;
                    RegisterConstructionCompany(company);
                    if (!company.Projects.Contains(project))
                    {
                        company.Projects.Add(project);
                    }
                }
                else
                {
                    project.AssignedCompany = null;
                }
                ActiveProjects.Add(project);
            }
        }

        public void ProgressConstruction()
        {
            if (ActiveProjects.Count == 0)
            {
                return;
            }

            bool progressMade = false;

            foreach (var project in ActiveProjects.ToList())
            {
                if (project.AssignedCompany != null)
                {
                    continue;
                }

                var dailyCost = CalculateDailyCost(project);
                if (dailyCost <= 0m)
                {
                    continue;
                }

                var cityBudget = (decimal)Budget;
                if (cityBudget < dailyCost || project.BudgetRemaining < dailyCost)
                {
                    continue;
                }

                if (project.ProgressProject(1, dailyCost))
                {
                    Budget -= (double)dailyCost;
                    progressMade = true;

                    if (project.IsComplete())
                    {
                        ApplyProjectEffects(project);
                        ActiveProjects.Remove(project);
                        progressMade = true;
                    }
                }
            }

            foreach (var project in ActiveProjects.ToList())
            {
                if (project.AssignedCompany != null && project.IsComplete())
                {
                    ApplyProjectEffects(project);
                    ActiveProjects.Remove(project);
                    progressMade = true;
                }
            }

            if (progressMade)
            {
                Economy.RaiseConstructionProgressed(this);
            }
        }

        private static decimal CalculateDailyCost(ConstructionProject project)
        {
            return project.Duration > 0 ? project.Budget / project.Duration : 0m;
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
                case ProjectType.Factory:
                    BoostIndustrialOutput(project.Output);
                    break;
                case ProjectType.Road:
                    ImproveTransportation(project.Output);
                    break;
                case ProjectType.Bridge:
                    ImproveTransportation(project.Output * 1.5);
                    break;
                case ProjectType.Port:
                    EnhanceTradeCapacity(project.Output);
                    break;
                case ProjectType.Airport:
                    EnhanceTradeCapacity(project.Output * 1.2);
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

        private void BoostIndustrialOutput(double factor)
        {
            Budget += factor * 5000;
        }

        private void ImproveTransportation(double factor)
        {
            CityExpenses = Math.Max(0, CityExpenses - factor * 10);
        }

        private void EnhanceTradeCapacity(double factor)
        {
            Budget += factor * 3500;
        }
    }
}