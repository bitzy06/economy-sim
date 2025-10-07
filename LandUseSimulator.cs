using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Simplified developer-agent simulation that rebalances parcel land use based on economic pressures.
    /// </summary>
    public static class LandUseSimulator
    {
        public static void RunDeveloperAgentSimulation(City city, Dictionary<Parcel, double> landValueMap, Random? random = null)
        {
            if (city?.ProceduralData == null)
            {
                return;
            }

            random ??= new Random(city.ProceduralData.Seed);
            EnsureIndustrialCapacity(city, landValueMap, random);
            EnsureResidentialCapacity(city, landValueMap, random);
            EnsureCommercialPresence(city, landValueMap, random);
            EnsureCivicAmenities(city, landValueMap, random);
        }

        private static void EnsureIndustrialCapacity(City city, Dictionary<Parcel, double> landValueMap, Random random)
        {
            var data = city.ProceduralData;
            int desiredIndustrial = city.Factories.Count;
            int currentIndustrial = data.CountBuildingsByLandUse(LandUseType.Industrial);
            if (currentIndustrial >= desiredIndustrial)
            {
                return;
            }

            foreach (var parcel in SelectConvertibleParcels(city, landValueMap, random)
                .Where(p => data.GetBuilding(p)?.LandUse != LandUseType.Residential))
            {
                if (currentIndustrial >= desiredIndustrial)
                {
                    break;
                }

                parcel.LandUse = LandUseType.Industrial;
                var building = data.GetBuilding(parcel) ?? new Building();
                building.LandUse = LandUseType.Industrial;
                data.SetBuilding(parcel, building);

                var unassignedFactory = city.Factories.FirstOrDefault(f => f.BuildingData == null);
                if (unassignedFactory != null)
                {
                    unassignedFactory.BuildingData = building;
                }

                currentIndustrial++;
            }
        }

        private static void EnsureResidentialCapacity(City city, Dictionary<Parcel, double> landValueMap, Random random)
        {
            var data = city.ProceduralData;
            int desiredResidential = Math.Max(3, (int)Math.Ceiling(city.PopClasses.Sum(p => p.Size) / 20000.0));
            int currentResidential = data.CountBuildingsByLandUse(LandUseType.Residential);
            if (currentResidential >= desiredResidential)
            {
                return;
            }

            foreach (var parcel in SelectConvertibleParcels(city, landValueMap, random)
                .Where(p => !IsFactoryParcel(city, p)))
            {
                if (currentResidential >= desiredResidential)
                {
                    break;
                }

                parcel.LandUse = LandUseType.Residential;
                var building = data.GetBuilding(parcel) ?? new Building();
                building.LandUse = LandUseType.Residential;
                data.SetBuilding(parcel, building);
                currentResidential++;
            }
        }

        private static void EnsureCommercialPresence(City city, Dictionary<Parcel, double> landValueMap, Random random)
        {
            var data = city.ProceduralData;
            int desiredCommercial = Math.Max(2, data.CountBuildingsByLandUse(LandUseType.Residential) / 2);
            int currentCommercial = data.CountBuildingsByLandUse(LandUseType.Commercial);
            if (currentCommercial >= desiredCommercial)
            {
                return;
            }

            foreach (var parcel in SelectConvertibleParcels(city, landValueMap, random)
                .Where(p => !IsFactoryParcel(city, p)))
            {
                if (currentCommercial >= desiredCommercial)
                {
                    break;
                }

                parcel.LandUse = LandUseType.Commercial;
                var building = data.GetBuilding(parcel) ?? new Building();
                building.LandUse = LandUseType.Commercial;
                data.SetBuilding(parcel, building);
                currentCommercial++;
            }
        }

        private static void EnsureCivicAmenities(City city, Dictionary<Parcel, double> landValueMap, Random random)
        {
            var data = city.ProceduralData;
            int desiredParks = Math.Max(1, data.CountBuildingsByLandUse(LandUseType.Residential) / 4);
            int currentParks = data.CountBuildingsByLandUse(LandUseType.Park);
            if (currentParks >= desiredParks)
            {
                return;
            }

            foreach (var parcel in SelectConvertibleParcels(city, landValueMap, random)
                .Where(p => !IsFactoryParcel(city, p)))
            {
                if (currentParks >= desiredParks)
                {
                    break;
                }

                parcel.LandUse = LandUseType.Park;
                var building = data.GetBuilding(parcel) ?? new Building();
                building.LandUse = LandUseType.Park;
                data.SetBuilding(parcel, building);
                currentParks++;
            }
        }

        private static IEnumerable<Parcel> SelectConvertibleParcels(City city, Dictionary<Parcel, double> landValueMap, Random random)
        {
            return city.ProceduralData.Parcels
                .OrderByDescending(p => landValueMap.TryGetValue(p, out var value) ? value : p.LandValue)
                .ThenBy(_ => random.Next());
        }

        private static bool IsFactoryParcel(City city, Parcel parcel)
        {
            var building = city.ProceduralData.GetBuilding(parcel);
            if (building == null)
            {
                return false;
            }

            return city.Factories.Any(f => ReferenceEquals(f.BuildingData, building));
        }
    }
}
