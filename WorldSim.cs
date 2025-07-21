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

        public static void Initialize()
        {
            MessageBus.Instance.Subscribe<DistrictDestroyedEventData>(OnDistrictDestroyed);
            MessageBus.Instance.Subscribe<MajorInfrastructureBuiltEventData>(OnMajorInfrastructureBuilt);
        }

        public static void RegisterCity(City city)
        {
            if (city != null && !string.IsNullOrEmpty(city.Name))
                cities[city.Name] = city;
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
    }
}
