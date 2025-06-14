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
        public int CompletedProjects { get; private set; }

        public ConstructionCompany(string name, int workers, decimal initialBudget)
            : base(name, CorporationSpecialization.Diversified)
        {
            Workers = workers;
            Budget = (double)initialBudget; // base Budget is double
            Projects = new List<ConstructionProject>();
        }

        // Convenience method for starting a new project. Returns true if the
        // company successfully took on the job.
        public bool StartProject(ProjectType type, decimal budget, int duration,
                                 double output, string requiredResource,
                                 int resourcePerDay, int workersRequired,
                                 decimal workerWagePerDay, City city,
                                 bool governmentSponsored = false)
        {
            var project = new ConstructionProject(type, budget, duration, output,
                requiredResource, resourcePerDay, workersRequired, workerWagePerDay,
                governmentSponsored);
            return TakeProject(project, city);
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
                    CompletedProjects++;
                    continue;
                }

                decimal dailyBaseCost = project.Budget / project.Duration;
                decimal resourceCost = 0m;
                
                if (!string.IsNullOrEmpty(project.RequiredResource) && project.ResourcePerDay > 0)
                {
                    double price = city.LocalPrices.ContainsKey(project.RequiredResource)
                        ? city.LocalPrices[project.RequiredResource]
                        : (Market.GoodDefinitions.ContainsKey(project.RequiredResource)
                            ? Market.GoodDefinitions[project.RequiredResource].BasePrice
                            : 10.0);

                    resourceCost = (decimal)(price * project.ResourcePerDay);

                    if (Budget >= (double)resourceCost && project.TrySpendBudget(resourceCost))
                    {
                        // Attempt to purchase locally, but allow progress even if not available
                        Market.BuyFromCityMarket(city, project.RequiredResource, project.ResourcePerDay, buyerCorp: this);
                        Budget -= (double)resourceCost;
                    }
                    else
                    {
                        if (project.BudgetRemaining <= 0 || Budget <= 0)
                        {
                            Projects.Remove(project);
                            // TODO: if project.GovernmentSponsored continue work using state funds
                        }
                        continue;
                    }
                }

                decimal workerCost = project.WorkerCostPerDay;
                if (workerCost > 0)
                {
                    if (Budget >= (double)workerCost && project.TrySpendBudget(workerCost))
                    {
                        Budget -= (double)workerCost;
                    }
                    else
                    {
                        if (project.BudgetRemaining <= 0 || Budget <= 0)
                        {
                            Projects.Remove(project);
                            // TODO: if project.GovernmentSponsored continue work using state funds
                        }
                        continue;
                    }
                }

                decimal totalDailyCost = dailyBaseCost;

                if (Budget < (double)totalDailyCost || project.BudgetRemaining < totalDailyCost)
                {
                    if (project.BudgetRemaining <= 0 || Budget <= 0)
                    {
                        Projects.Remove(project);
                        // TODO: if project.GovernmentSponsored continue work using state funds
                    }
                    continue;
                }

                if (project.ProgressProject(1, dailyBaseCost))
                {
                    Budget -= (double)dailyBaseCost;
                }

                if (project.IsComplete())
                {
                    Projects.Remove(project);
                    CompletedProjects++;
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
