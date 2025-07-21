using System;

namespace StrategyGame
{
    /// <summary>
    /// Simple container of economic indicators used when generating road networks.
    /// </summary>
    public class EconomicData
    {
        public double Gdp { get; set; }
        public double InfrastructureBudget { get; set; }
    }

    /// <summary>
    /// Interface for models that estimate road density from economic data.
    /// </summary>
    public interface IEconomicRoadModel
    {
        double PredictRoadDensity(EconomicData data);
    }

    /// <summary>
    /// Basic stub model. Returns a density factor based on GDP.
    /// Replace with ML.NET or other ML framework in the future.
    /// </summary>
    public sealed class SimpleEconomicRoadModel : IEconomicRoadModel
    {
        public double PredictRoadDensity(EconomicData data)
        {
            if (data == null) return 1.0;
            // Very rough scaling: more prosperous cities support denser roads.
            return 1.0 + Math.Clamp(data.Gdp / 1_000_000.0, 0, 2.0);
        }
    }
}
