// ConstructionCompany.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    public class ConstructionCompany
    {
        public string Name { get; private set; }
        public decimal Budget { get; private set; }
        public List<ConstructionProject> Projects { get; private set; }

        public ConstructionCompany(string name, decimal initialBudget)
        {
            Name = name;
            Budget = initialBudget;
            Projects = new List<ConstructionProject>();
        }

        public bool StartProject(ConstructionProject project)
        {
            if (Budget < project.Cost)
            {
                return false; // Not enough budget to start the project
            }

            Projects.Add(project);
            Budget -= project.Cost; // Deduct the cost upfront
            return true;
        }

        public void ProgressProjects(int days)
        {
            foreach (var project in Projects.ToList())
            {
                if (project.ProgressProject(days, Budget))
                {
                    Budget -= project.Cost / project.Duration * days; // Deduct cost for the progress made

                    if (project.IsComplete())
                    {
                        CompleteProject(project);
                    }
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
