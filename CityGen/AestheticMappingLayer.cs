using System;
using EconomySim.Messaging;

namespace EconomySim.CityGen
{
    /// <summary>
    /// Maps high level economic and cultural metrics into parameters
    /// that drive procedural city generation. Consumes events from the
    /// global message bus and exposes the latest parameters.
    /// </summary>
    public static class AestheticMappingLayer
    {
        /// <summary>
        /// Event carrying metrics used for mapping.
        /// </summary>
        public record CityMetricsEvent(decimal Gdp, float PopulationDensity, float CultureIndex);

        /// <summary>
        /// Parameter set influencing generation subsystems.
        /// </summary>
        public class CityGenerationParameters
        {
            public double RoadDensity { get; set; } = 1.0;
            public double BuildingHeightScale { get; set; } = 1.0;
            public double GreenSpaceRatio { get; set; } = 0.1;
        }

        public static CityGenerationParameters Parameters { get; } = new();

        /// <summary>
        /// Subscribe to the message bus for incoming metric events.
        /// </summary>
        public static void Initialize(MessageBus bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            bus.Subscribe<CityMetricsEvent>(HandleMetricsEvent);
        }

        private static void HandleMetricsEvent(CityMetricsEvent evt)
        {
            // Simple rule mapping GDP, population and culture to parameters.
            Parameters.RoadDensity = Math.Clamp((double)evt.PopulationDensity * 2 + (double)(evt.Gdp / 10_000_000m), 0.5, 3.0);
            Parameters.BuildingHeightScale = Math.Clamp((double)(evt.Gdp / 5_000_000m) + evt.CultureIndex, 0.5, 5.0);
            Parameters.GreenSpaceRatio = Math.Clamp(1 - evt.PopulationDensity + evt.CultureIndex * 0.1, 0.05, 0.5);
        }
    }
}

