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
}
