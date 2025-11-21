using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Calculates parcel land values based on the current economic state of the city.
    /// </summary>
    public static class LandValueCalculator
    {
        public static Dictionary<Parcel, double> Calculate(City city)
        {
            var results = new Dictionary<Parcel, double>();
            if (city?.ProceduralData == null)
            {
                return results;
            }

            var proceduralData = city.ProceduralData;
            var template = CityTemplateManager.GetTemplate(proceduralData.TemplateType);

            double populationPressure = Math.Log(Math.Max(1, city.PopClasses.Sum(p => p.Size) + 1)) * 8.0;
            double wealthFactor = Math.Clamp(city.Budget / Math.Max(1, city.Population + 1), -25, 45);
            double happinessFactor = city.Happiness * 0.35;

            // Materialize the collection to a list to avoid "collection was modified" exceptions
            // if the underlying list is modified during iteration
            foreach (var parcel in proceduralData.Parcels.ToList())
            {
                double baseValue = 25 + populationPressure + wealthFactor;

                switch (parcel.LandUse)
                {
                    case LandUseType.Residential:
                        baseValue += happinessFactor * 0.8;
                        baseValue += template.IncomeMultiplier * 6;
                        break;
                    case LandUseType.Commercial:
                        baseValue += template.IncomeMultiplier * 12;
                        baseValue += city.PopClasses.Count * 1.5;
                        break;
                    case LandUseType.Industrial:
                        baseValue += template.FactoryWeights.Values.DefaultIfEmpty(1).Average() * 5;
                        baseValue += city.Factories.Count * 1.2;
                        baseValue -= Math.Max(0, 40 - happinessFactor); // pollution penalty
                        break;
                    case LandUseType.Park:
                        baseValue += happinessFactor;
                        baseValue += template.ExpenseMultiplier * 4;
                        break;
                }

                if (proceduralData.GetBuilding(parcel) is { } existingBuilding)
                {
                    // Successful or high-level buildings push land value further up.
                    baseValue += existingBuilding.Level * 3;
                    baseValue += existingBuilding.EconomicOutput * 0.02;
                }

                baseValue = Math.Clamp(baseValue, 5, 120);
                parcel.LandValue = baseValue;
                results[parcel] = baseValue;
            }

            return results;
        }
    }
}
