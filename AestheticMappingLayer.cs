using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public enum BuildingStyle { Traditional, Modern }
    public enum RoadNetworkType { Organic, Grid }

    public class CityGenerationParameters
    {
        public BuildingStyle BuildingStyle { get; init; } = BuildingStyle.Traditional;
        public RoadNetworkType RoadNetworkType { get; init; } = RoadNetworkType.Organic;
    }

    /// <summary>
    /// Maps high level simulation metrics to city generation parameters.
    /// Listens to the message bus and updates parameters accordingly.
    /// </summary>
    public sealed class AestheticMappingLayer
    {
        private static readonly Lazy<AestheticMappingLayer> lazy = new(() => new AestheticMappingLayer());
        public static AestheticMappingLayer Instance => lazy.Value;

        private CityGenerationParameters currentParameters = new CityGenerationParameters();
        public CityGenerationParameters CurrentParameters => currentParameters;

        private AestheticMappingLayer()
        {
            MessageBus.Instance.Subscribe<EconomyUpdatedEventData>(OnEconomyUpdated);
        }

        private void OnEconomyUpdated(EconomyUpdatedEventData data)
        {
            var style = data.Gdp > 100000 ? BuildingStyle.Modern : BuildingStyle.Traditional;
            var road = data.NewBudget > 5000 ? RoadNetworkType.Grid : RoadNetworkType.Organic;
            currentParameters = new CityGenerationParameters
            {
                BuildingStyle = style,
                RoadNetworkType = road
            };
        }
    }
}
