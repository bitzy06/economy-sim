using System;
using System.Collections.Generic;
using System.Linq;
using EconomySim.Protocols;
using Messaging;

namespace StrategyGame
{
    /// <summary>
    /// Manages level-of-detail (LOD) for city simulation. Cities close to the player
    /// or otherwise flagged as important run full updates while distant cities are
    /// updated using cheaper logic and only emit summary events.
    /// </summary>
    public class SimulationLODManager
    {
        private class CityInfo
        {
            public float ActivityLevel;
            public bool HighPriority;
        }

        private readonly Dictionary<City, CityInfo> _cityInfo = new();
        private City? _playerCity;
        private readonly MessageBus _bus;

        public static SimulationLODManager Instance { get; } = new SimulationLODManager(GameServices.Bus);

        private SimulationLODManager(MessageBus bus)
        {
            _bus = bus;
        }

        /// <summary>
        /// Set the city that represents the player's current focus.
        /// </summary>
        public City? PlayerCity
        {
            get => _playerCity;
            set => _playerCity = value;
        }

        /// <summary>
        /// Register a city with the LOD manager.
        /// </summary>
        public void RegisterCity(City city)
        {
            if (!_cityInfo.ContainsKey(city))
                _cityInfo[city] = new CityInfo { ActivityLevel = 1f };
        }

        /// <summary>
        /// Mark a city as narratively important so it always runs at high fidelity.
        /// </summary>
        public void SetHighPriority(City city, bool value)
        {
            if (!_cityInfo.ContainsKey(city))
                RegisterCity(city);
            _cityInfo[city].HighPriority = value;
        }

        /// <summary>
        /// Determine if a city should run a high fidelity update this tick.
        /// </summary>
        public bool ShouldSimulateHighFidelity(City city)
        {
            if (!_cityInfo.TryGetValue(city, out var info))
                return true;
            return info.HighPriority || ReferenceEquals(city, _playerCity);
        }

        /// <summary>
        /// Perform a lightweight update and publish summary events.
        /// </summary>
        public void LowFidelityStep(City city)
        {
            city.SimulateGrowth();

            float gdp = (float)city.PopClasses.Sum(p => p.Size * p.IncomePerPerson);
            var builder = new FlatBuffers.FlatBufferBuilder(64);
            var idOffset = builder.CreateString(city.ProceduralData?.Id.ToString() ?? string.Empty);
            EconomyUpdatedEvent.StartEconomyUpdatedEvent(builder);
            EconomyUpdatedEvent.AddCityId(builder, idOffset);
            EconomyUpdatedEvent.AddTimestamp(builder, (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            EconomyUpdatedEvent.AddGdp(builder, gdp);
            EconomyUpdatedEvent.AddInflation(builder, 0f);
            var evtOffset = EconomyUpdatedEvent.EndEconomyUpdatedEvent(builder);
            EconomyUpdatedEvent.FinishSizePrefixedEconomyUpdatedEventBuffer(builder, evtOffset);
            var buffer = new FlatBuffers.ByteBuffer(builder.SizedByteArray());
            var evt = EconomyUpdatedEvent.GetRootAsEconomyUpdatedEvent(buffer);
            _bus.Publish(evt);
        }
    }
}
