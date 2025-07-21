using System.Collections.Generic;
using System;

namespace StrategyGame
{
    /// <summary>
    /// Simplified world simulator that tracks cities and reacts to events.
    /// </summary>
    public static class WorldSim
    {
        private static readonly Dictionary<string, City> cities = new();
        private static readonly HashSet<string> focusedCities = new();
        private static int nextIndex = 0;

        public static void Initialize()
        {
            MessageBus.Instance.Subscribe<DistrictDestroyedEventData>(OnDistrictDestroyed);
            MessageBus.Instance.Subscribe<MajorInfrastructureBuiltEventData>(OnMajorInfrastructureBuilt);
        }

        public static void RegisterCity(City city)
        {
            if (city != null && !string.IsNullOrEmpty(city.Name))
            {
                city.Index = nextIndex++;
                cities[city.Name] = city;
            }
        }

        public static void AddNarrativeFocus(string cityName)
        {
            if (!string.IsNullOrEmpty(cityName))
                focusedCities.Add(cityName);
        }

        public static void RemoveNarrativeFocus(string cityName)
        {
            if (!string.IsNullOrEmpty(cityName))
                focusedCities.Remove(cityName);
        }

        private static void OnDistrictDestroyed(DistrictDestroyedEventData data)
        {
            if (cities.TryGetValue(data.CityName, out var city))
            {
                city.Population = Math.Max(0, city.Population - data.LostPopulation);
            }
        }

        private static void OnMajorInfrastructureBuilt(MajorInfrastructureBuiltEventData data)
        {
            if (cities.TryGetValue(data.CityName, out var city))
            {
                city.Budget += data.Value * 100;
            }
        }

        public static void UpdateCityLODs(City playerCity)
        {
            foreach (var city in cities.Values)
            {
                var previous = city.LOD;
                if (city == playerCity || focusedCities.Contains(city.Name))
                {
                    city.LOD = CityLOD.Full;
                }
                else
                {
                    double distance = ComputeDistance(playerCity, city);
                    if (distance < LODSettings.SimplifiedDistanceThreshold)
                        city.LOD = CityLOD.Simplified;
                    else
                        city.LOD = CityLOD.Dormant;
                }

                if (previous != city.LOD)
                    MessageBus.Instance.Publish(new CityLODChangedEventData(city.Name, city.LOD));

                MessageBus.Instance.Publish(new CityStatusEventData(city.Name, city.Population, city.Budget, city.LOD));
            }
        }

        private static double ComputeDistance(City a, City b)
        {
            if (a == null || b == null) return double.MaxValue;
            return Math.Abs(a.Index - b.Index) * 10.0;
        }
    }
}
