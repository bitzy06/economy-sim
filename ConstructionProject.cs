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
        // Budget is the total amount allocated for the project. It is spent
        // gradually during construction rather than paid up front.
        public decimal Budget { get; private set; }
        // Allow other classes to update the remaining budget as costs are paid
        // over time. Previously the setter was private which caused a compiler
        // error when other classes attempted to deduct costs.
        public decimal BudgetRemaining { get; set; }
        public int Duration { get; private set; } // Duration in days
        public int Progress { get; private set; } // Progress in days completed
        public double Output { get; private set; } // Output (e.g., housing units or kilometers of railway)

        public string RequiredResource { get; private set; }
        public int ResourcePerDay { get; private set; }

        public ConstructionCompany AssignedCompany { get; set; }

        public const decimal MinimumDailyBudget = 100m;

        public ConstructionProject(ProjectType type, decimal budget, int duration, double output, string requiredResource, int resourcePerDay)
        {
            Type = type;
            Budget = budget;
            BudgetRemaining = budget;
            Duration = duration;
            Output = output;
            RequiredResource = requiredResource;
            ResourcePerDay = resourcePerDay;
            Progress = 0;
        }

        public bool ProgressProject(int days, decimal dailyCost)
        {
            if (BudgetRemaining < dailyCost * days)
            {
                return false; // Not enough project budget
            }

            BudgetRemaining -= dailyCost * days;
            Progress += days;
            return true;
        }

        // Spend project budget directly (e.g., on materials) without advancing
        // progress. Returns true if there was enough budget to cover the cost.
        public bool TrySpendBudget(decimal amount)
        {
            if (BudgetRemaining < amount)
                return false;

            BudgetRemaining -= amount;
            return true;
        }

        public bool IsComplete()
        {
            return Progress >= Duration;
        }
    }
}
