using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    /// <summary>
    /// Extension of DeveloperAgent that allows agents to compete for parcels.
    /// </summary>
    public abstract class AgentDeveloper : DeveloperAgent
    {
        public string Name { get; }
        public double Budget { get; set; }

        protected AgentDeveloper(string name, LandUseType type, double startingBudget = 1000)
            : base(type)
        {
            Name = name;
            Budget = startingBudget;
        }

        /// <summary>
        /// Returns the amount this developer is willing to bid for a parcel.
        /// The bid is capped by the remaining budget.
        /// </summary>
        public virtual double Bid(Parcel parcel, EconomicData data)
        {
            double score = Evaluate(parcel);
            return Math.Min(score, Budget);
        }
    }

    /// <summary>
    /// Coordinates parcel allocation among competing developers.
    /// </summary>
    public class AgentDevelopmentManager
    {
        private readonly List<AgentDeveloper> developers;

        public AgentDevelopmentManager(IEnumerable<AgentDeveloper> devs)
        {
            developers = devs.ToList();
        }

        public void AllocateParcels(IEnumerable<Parcel> parcels, EconomicData data)
        {
            foreach (var parcel in parcels)
            {
                AgentDeveloper? winner = null;
                double bestBid = double.MinValue;

                foreach (var dev in developers)
                {
                    double bid = dev.Bid(parcel, data);
                    if (bid > bestBid && dev.Budget >= bid)
                    {
                        winner = dev;
                        bestBid = bid;
                    }
                }

                if (winner != null)
                {
                    winner.Budget -= bestBid;
                    parcel.LandUse = winner.LandUse;
                }
            }
        }
    }
}
