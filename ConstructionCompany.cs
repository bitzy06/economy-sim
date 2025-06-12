// ConstructionCompany.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    // Construction companies are corporations that specialise in building
    // projects for cities. They maintain their own budgets and workers and
    // purchase materials from the market when progressing a project.
    public class ConstructionCompany : Corporation
    {
        public List<ConstructionProject> Projects { get; private set; }
        public City HomeCity { get; set; }
        public int Workers { get; set; }

        public ConstructionCompany(string name, int workers, decimal initialBudget)
            : base(name, CorporationSpecialization.Diversified)
        {
            Workers = workers;
            Budget = (double)initialBudget; // base Budget is double
            Projects = new List<ConstructionProject>();
        }

        // City pays the project budget to the company when starting a job
        public bool TakeProject(ConstructionProject project, City city)
        {
            if (city.Budget < (double)project.Budget)
            {
                return false;
            }

            city.Budget -= (double)project.Budget;
            Budget += (double)project.Budget;
            project.AssignedCompany = this;
            Projects.Add(project);
            return true;
        }

        // Called each day by the city hosting the project
        public void WorkOnProjects(City city)
        {
            foreach (var project in Projects.ToList())
            {
                if (project.IsComplete())
                {
                    Projects.Remove(project);
                    continue;
                }

                decimal dailyBaseCost = project.Budget / project.Duration;
                decimal resourceCost = 0m;

                if (!string.IsNullOrEmpty(project.RequiredResource) && project.ResourcePerDay > 0)
                {
                    if (!city.LocalPrices.ContainsKey(project.RequiredResource))
                        continue;

                    double price = city.LocalPrices[project.RequiredResource];
                    resourceCost = (decimal)(price * project.ResourcePerDay);

                    if (Budget < (double)resourceCost || project.BudgetRemaining < resourceCost)

                        continue;

                    bool bought = Market.BuyFromCityMarket(city, project.RequiredResource, project.ResourcePerDay, buyerCorp: this);
                    if (!bought) continue;
                    
                    project.BudgetRemaining -= resourceCost;
                }

                if (Budget < (double)dailyBaseCost || project.BudgetRemaining < dailyBaseCost)
                    continue;

                if (project.ProgressProject(1, dailyBaseCost))
                {
                    Budget -= (double)dailyBaseCost;
                }

                if (project.IsComplete())
                {
                    Projects.Remove(project);
                }
            }
        }

        private void CompleteProject(ConstructionProject project)
        {
            Projects.Remove(project);
            Console.WriteLine($"Project {project.Type} completed with output: {project.Output}");
            // TODO: Deliver the output to the city or state that issued the contract
        }
    }
}
