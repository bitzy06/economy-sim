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
        private readonly List<Parcel> parcels = new();
        private readonly object parcelLock = new object();
        private readonly object buildingLock = new object();

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
        /// Returns a thread-safe snapshot.
        /// </summary>
        public List<Parcel> Parcels
        {
            get
            {
                lock (parcelLock)
                {
                    return new List<Parcel>(parcels);
                }
            }
        }

        /// <summary>
        /// Adds a parcel to the city in a thread-safe manner.
        /// </summary>
        public void AddParcel(Parcel parcel)
        {
            if (parcel == null) return;
            
            lock (parcelLock)
            {
                parcels.Add(parcel);
            }
        }

        /// <summary>
        /// Returns the current building assigned to a parcel, if any.
        /// Thread-safe read operation.
        /// </summary>
        public Building? GetBuilding(Parcel parcel)
        {
            lock (buildingLock)
            {
                return parcelBuildings.TryGetValue(parcel, out var building) ? building : null;
            }
        }

        /// <summary>
        /// Assigns a building to the provided parcel, ensuring the land-use
        /// information remains consistent between the two data structures.
        /// Thread-safe write operation.
        /// </summary>
        public void SetBuilding(Parcel parcel, Building building)
        {
            if (parcel == null || building == null)
            {
                return;
            }

            building.LandUse = parcel.LandUse;
            
            lock (buildingLock)
            {
                parcelBuildings[parcel] = building;
            }
        }

        /// <summary>
        /// Removes a building assignment from a parcel.
        /// Thread-safe write operation.
        /// </summary>
        public void RemoveBuilding(Parcel parcel)
        {
            if (parcel == null)
            {
                return;
            }

            lock (buildingLock)
            {
                parcelBuildings.Remove(parcel);
            }
        }

        /// <summary>
        /// Enumerates parcel/building pairs in the city.
        /// Returns a thread-safe snapshot to prevent collection modification exceptions.
        /// </summary>
        public IEnumerable<(Parcel parcel, Building building)> ParcelBuildingPairs
        {
            get
            {
                lock (buildingLock)
                {
                    return parcelBuildings.Select(kvp => (kvp.Key, kvp.Value)).ToList();
                }
            }
        }

        /// <summary>
        /// Returns all buildings currently placed within the city.
        /// Returns a thread-safe snapshot.
        /// </summary>
        public IEnumerable<Building> Buildings
        {
            get
            {
                lock (buildingLock)
                {
                    return parcelBuildings.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Counts buildings by the provided land-use category.
        /// Thread-safe operation.
        /// </summary>
        public int CountBuildingsByLandUse(LandUseType landUse)
        {
            lock (buildingLock)
            {
                return parcelBuildings.Values.Count(b => b.LandUse == landUse);
            }
        }

        /// <summary>
        /// Calculates the total population capacity of residential buildings.
        /// Thread-safe operation.
        /// </summary>
        public int TotalResidentialCapacity
        {
            get
            {
                lock (buildingLock)
                {
                    return parcelBuildings.Values
                        .Where(b => b.LandUse == LandUseType.Residential)
                        .Sum(b => b.PopulationCapacity);
                }
            }
        }

        /// <summary>
        /// Retrieves parcels matching a land-use category.
        /// Returns a thread-safe snapshot.
        /// </summary>
        public IEnumerable<Parcel> GetParcelsByLandUse(LandUseType landUse)
        {
            lock (parcelLock)
            {
                return parcels.Where(p => p.LandUse == landUse).ToList();
            }
        }
    }
}
