using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Holds the procedurally generated spatial data for a city and exposes
    /// helper methods for the economy integration layer.
    /// </summary>
    public class CityProceduralData
    {
        private readonly Dictionary<Parcel, Building> parcelBuildings = new();

        public CityProceduralData(CityType templateType, int seed)
        {
            TemplateType = templateType;
            Seed = seed;
        }

        /// <summary>
        /// Type of template that produced the city. Used to derive heuristics
        /// when calculating land values or refining buildings.
        /// </summary>
        public CityType TemplateType { get; }

        /// <summary>
        /// Seed used for deterministic calculations that depend on randomness.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// All parcels that make up the simulated city layout.
        /// </summary>
        public List<Parcel> Parcels { get; } = new();

        /// <summary>
        /// Returns the current building assigned to a parcel, if any.
        /// </summary>
        public Building? GetBuilding(Parcel parcel)
        {
            return parcelBuildings.TryGetValue(parcel, out var building) ? building : null;
        }

        /// <summary>
        /// Assigns a building to the provided parcel, ensuring the land-use
        /// information remains consistent between the two data structures.
        /// </summary>
        public void SetBuilding(Parcel parcel, Building building)
        {
            if (parcel == null || building == null)
            {
                return;
            }

            building.LandUse = parcel.LandUse;
            parcelBuildings[parcel] = building;
        }

        /// <summary>
        /// Removes a building assignment from a parcel.
        /// </summary>
        public void RemoveBuilding(Parcel parcel)
        {
            if (parcel == null)
            {
                return;
            }

            parcelBuildings.Remove(parcel);
        }

        /// <summary>
        /// Enumerates parcel/building pairs in the city.
        /// </summary>
        public IEnumerable<(Parcel parcel, Building building)> ParcelBuildingPairs =>
            parcelBuildings.Select(kvp => (kvp.Key, kvp.Value));

        /// <summary>
        /// Returns all buildings currently placed within the city.
        /// </summary>
        public IEnumerable<Building> Buildings => parcelBuildings.Values;

        /// <summary>
        /// Counts buildings by the provided land-use category.
        /// </summary>
        public int CountBuildingsByLandUse(LandUseType landUse) =>
            parcelBuildings.Values.Count(b => b.LandUse == landUse);

        /// <summary>
        /// Calculates the total population capacity of residential buildings.
        /// </summary>
        public int TotalResidentialCapacity =>
            parcelBuildings.Values
                .Where(b => b.LandUse == LandUseType.Residential)
                .Sum(b => b.PopulationCapacity);

        /// <summary>
        /// Retrieves parcels matching a land-use category.
        /// </summary>
        public IEnumerable<Parcel> GetParcelsByLandUse(LandUseType landUse) =>
            Parcels.Where(p => p.LandUse == landUse);
    }
}
