// Suburb.cs
using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public class Suburb
    {
        public string Name { get; private set; }
        public int Population { get; set; }
        public double HousingCapacity { get; set; } // Maximum population the suburb can support
        public double HousingQuality { get; set; } // 0-1 scale representing housing quality
        public double RailwayKilometers { get; set; } // Total kilometers of railway in the suburb

        public Suburb(string name, int initialPopulation, double housingCapacity, double housingQuality)
        {
            Name = name;
            Population = initialPopulation;
            HousingCapacity = housingCapacity;
            HousingQuality = housingQuality;
            RailwayKilometers = 0; // Default to no railways initially
        }

        public double CalculateQualityOfLife()
        {
            double housingFactor = HousingQuality * (Population / HousingCapacity);
            double railwayFactor = RailwayKilometers > 0 ? 1.0 : 0.5; // Bonus if railways exist

            return (housingFactor + railwayFactor) / 2; // Average QoL factors
        }

        public void AddRailway(double kilometers)
        {
            RailwayKilometers += kilometers;
        }
    }
}
