using System;

namespace StrategyGame
{
    /// <summary>
    /// Event raised when a construction project is completed.
    /// </summary>
    public record ConstructionCompletedEvent(ConstructionProject Project, City City);

    /// <summary>
    /// Event raised whenever a trade is recorded on the global market.
    /// </summary>
    public record TradeRecordedEvent(string GoodName, string ExportingCountry, string ImportingCountry, int Quantity, double TotalValue);

    /// <summary>
    /// Event raised when a country's economy metrics are updated.
    /// </summary>
    public record EconomyUpdatedEventData(string Country, double NewBudget, double Gdp);

    /// <summary>
    /// Event raised when a population count changes for an entity.
    /// </summary>
    public record PopulationUpdatedEventData(int EntityId, int NewPopulation);
}
