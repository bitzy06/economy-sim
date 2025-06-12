// ConstructionProject.cs
using System;

namespace StrategyGame
{
    public enum ProjectType
    {
        Housing,
        Railway
    }

    public class ConstructionProject
    {
        public ProjectType Type { get; private set; }
        public decimal Cost { get; private set; } // Total cost in money
        public int Duration { get; private set; } // Duration in days
        public int Progress { get; private set; } // Progress in days completed
        public double Output { get; private set; } // Output (e.g., housing units or kilometers of railway)

        public ConstructionProject(ProjectType type, decimal cost, int duration, double output)
        {
            Type = type;
            Cost = cost;
            Duration = duration;
            Output = output;
            Progress = 0;
        }

        public bool ProgressProject(int days, decimal availableBudget)
        {
            if (availableBudget < Cost / Duration * days)
            {
                return false; // Not enough budget to progress
            }

            Progress += days;
            return true;
        }

        public bool IsComplete()
        {
            return Progress >= Duration;
        }
    }
}
