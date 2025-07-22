using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;

namespace StrategyGame
{
    public enum BuildingStyle { Traditional, Modern }
    public enum RoadNetworkType { Organic, Grid }

    public class CityGenerationParameters
    {
        public BuildingStyle BuildingStyle { get; init; } = BuildingStyle.Traditional;
        public RoadNetworkType RoadNetworkType { get; init; } = RoadNetworkType.Organic;

        public Dictionary<BuildingStyle, Dictionary<LandUseType, Rgba32>> BuildingPalettes { get; init; } = CreateDefaultPalettes();

        private static Dictionary<BuildingStyle, Dictionary<LandUseType, Rgba32>> CreateDefaultPalettes() => new()
        {
            [BuildingStyle.Traditional] = new Dictionary<LandUseType, Rgba32>
            {
                [LandUseType.Commercial] = new Rgba32(200, 50, 50, 180),
                [LandUseType.Residential] = new Rgba32(50, 50, 200, 180),
                [LandUseType.Industrial] = new Rgba32(120, 120, 120, 180),
                [LandUseType.Park] = new Rgba32(60, 160, 60, 180)
            },
            [BuildingStyle.Modern] = new Dictionary<LandUseType, Rgba32>
            {
                [LandUseType.Commercial] = new Rgba32(220, 80, 80, 180),
                [LandUseType.Residential] = new Rgba32(80, 80, 220, 180),
                [LandUseType.Industrial] = new Rgba32(150, 150, 150, 180),
                [LandUseType.Park] = new Rgba32(80, 180, 80, 180)
            }
        };
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
