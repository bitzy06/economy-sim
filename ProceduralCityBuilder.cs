using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Helper responsible for seeding the procedural city layer and linking it with the economic simulation.
    /// </summary>
    public static class ProceduralCityBuilder
    {
        public static void InitializeCityData(City city, CityTemplate template, Random random)
        {
            if (city == null)
            {
                return;
            }

            var seed = random.Next();
            var proceduralData = new CityProceduralData(template.Type, seed);
            city.ProceduralData = proceduralData;

            CreateBaseParcels(city, template, random, proceduralData);
            AssignFactoriesToBuildings(city, proceduralData);

            // First pass land values to inform the bidding simulation.
            var initialLandValues = LandValueCalculator.Calculate(city);
            LandUseSimulator.RunDeveloperAgentSimulation(city, initialLandValues, new Random(seed));

            // Recalculate after land use adjustments to capture the latest configuration.
            var refinedLandValues = LandValueCalculator.Calculate(city);
            BuildingRefiner.RefineBuildings(city, refinedLandValues);
        }

        private static void CreateBaseParcels(City city, CityTemplate template, Random random, CityProceduralData proceduralData)
        {
            int population = Math.Max(1, city.PopClasses.Sum(p => p.Size));
            int residentialParcels = Math.Max(6, population / 12000);
            int industrialParcels = Math.Max(city.Factories.Count, residentialParcels / 3);
            int commercialParcels = Math.Max(3, residentialParcels / 2);
            int parkParcels = Math.Max(2, residentialParcels / 4);
            int flexibleParcels = Math.Max(2, residentialParcels / 5);

            foreach (var parcel in CreateParcels(residentialParcels, LandUseType.Residential))
            {
                var building = new Building { LandUse = LandUseType.Residential };
                proceduralData.AddParcel(parcel);
                proceduralData.SetBuilding(parcel, building);
            }

            foreach (var parcel in CreateParcels(commercialParcels, LandUseType.Commercial))
            {
                var building = new Building { LandUse = LandUseType.Commercial };
                proceduralData.AddParcel(parcel);
                proceduralData.SetBuilding(parcel, building);
            }

            foreach (var parcel in CreateParcels(industrialParcels, LandUseType.Industrial))
            {
                var building = new Building { LandUse = LandUseType.Industrial };
                proceduralData.AddParcel(parcel);
                proceduralData.SetBuilding(parcel, building);
            }

            foreach (var parcel in CreateParcels(parkParcels, LandUseType.Park))
            {
                var building = new Building { LandUse = LandUseType.Park };
                proceduralData.AddParcel(parcel);
                proceduralData.SetBuilding(parcel, building);
            }

            foreach (var parcel in CreateParcels(flexibleParcels, random.NextDouble() > 0.5 ? LandUseType.Commercial : LandUseType.Park))
            {
                proceduralData.AddParcel(parcel);
                // Leave these parcels without buildings initially so the simulator can claim them.
            }
        }

        private static IEnumerable<Parcel> CreateParcels(int count, LandUseType landUse)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new Parcel
                {
                    LandUse = landUse
                };
            }
        }

        private static void AssignFactoriesToBuildings(City city, CityProceduralData data)
        {
            var industrialBuildings = data.ParcelBuildingPairs
                .Where(pair => pair.building.LandUse == LandUseType.Industrial)
                .Select(pair => pair.building)
                .ToList();

            int index = 0;
            foreach (var factory in city.Factories)
            {
                if (factory.BuildingData != null)
                {
                    continue;
                }

                if (index >= industrialBuildings.Count)
                {
                    var extraParcel = new Parcel { LandUse = LandUseType.Industrial };
                    var extraBuilding = new Building { LandUse = LandUseType.Industrial };
                    data.AddParcel(extraParcel);
                    data.SetBuilding(extraParcel, extraBuilding);
                    industrialBuildings.Add(extraBuilding);
                }

                var building = industrialBuildings[index++];
                factory.BuildingData = building;
            }
        }
    }
}
