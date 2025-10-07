using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Derives gameplay-relevant building statistics from land value and land use.
    /// </summary>
    public static class BuildingRefiner
    {
        public static void RefineBuildings(City city, Dictionary<Parcel, double> landValueMap)
        {
            if (city?.ProceduralData == null)
            {
                return;
            }

            var proceduralData = city.ProceduralData;
            var template = CityTemplateManager.GetTemplate(proceduralData.TemplateType);
            int residentialParcels = Math.Max(1, proceduralData.CountBuildingsByLandUse(LandUseType.Residential));
            int currentPopulation = Math.Max(1, city.PopClasses.Sum(p => p.Size));
            int baselineResidentialCapacity = Math.Max(1500, currentPopulation / residentialParcels);

            foreach (var (parcel, building) in proceduralData.ParcelBuildingPairs)
            {
                if (building == null)
                {
                    continue;
                }

                double landValue = landValueMap.TryGetValue(parcel, out var value)
                    ? value
                    : parcel.LandValue;

                building.LandUse = parcel.LandUse;
                building.Level = DetermineLevel(landValue);

                switch (parcel.LandUse)
                {
                    case LandUseType.Residential:
                        building.PopulationCapacity = CalculateResidentialCapacity(
                            baselineResidentialCapacity,
                            building.Level,
                            landValue);
                        building.EconomicOutput = Math.Round(
                            building.Level * template.IncomeMultiplier * 40 + landValue * 6,
                            MidpointRounding.AwayFromZero);
                        building.PollutionOutput = Math.Max(0, building.Level - 2);
                        break;
                    case LandUseType.Commercial:
                        building.PopulationCapacity = 0;
                        building.EconomicOutput = Math.Round(
                            (landValue * 8 + building.Level * 120) * template.IncomeMultiplier,
                            MidpointRounding.AwayFromZero);
                        building.PollutionOutput = Math.Max(0, building.Level * 0.5);
                        break;
                    case LandUseType.Industrial:
                        building.PopulationCapacity = (int)Math.Round(landValue * 3 + building.Level * 150);
                        building.EconomicOutput = Math.Round(
                            (landValue * 12 + building.Level * 200) * template.IncomeMultiplier,
                            MidpointRounding.AwayFromZero);
                        building.PollutionOutput = Math.Max(5, building.Level * 5 + landValue * 0.5);
                        break;
                    case LandUseType.Park:
                        building.PopulationCapacity = (int)Math.Round(landValue * 1.5);
                        building.EconomicOutput = Math.Round(landValue * 2, MidpointRounding.AwayFromZero);
                        building.PollutionOutput = -Math.Max(1, building.Level);
                        break;
                }
            }
        }

        private static int DetermineLevel(double landValue)
        {
            if (landValue < 25) return 1;
            if (landValue < 45) return 2;
            if (landValue < 65) return 3;
            if (landValue < 90) return 4;
            return 5;
        }

        private static int CalculateResidentialCapacity(int baseline, int level, double landValue)
        {
            double levelFactor = 0.6 + level * 0.45;
            double valueFactor = 0.8 + landValue / 120.0;
            return (int)Math.Round(baseline * levelFactor * valueFactor, MidpointRounding.AwayFromZero);
        }
    }
}
