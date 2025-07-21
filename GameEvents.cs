using System;
using NetTopologySuite.Geometries;

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

    /// <summary>
    /// Event raised when a city district is destroyed.
    /// </summary>
    public record DistrictDestroyedEventData(string CityName, string DistrictName, int LostPopulation);

    /// <summary>
    /// Event raised when major infrastructure is built in a city.
    /// </summary>
    public record MajorInfrastructureBuiltEventData(string CityName, string InfrastructureType, double Value);

    /// <summary>
    /// Event raised when a city's level of detail changes.
    /// </summary>
    public record CityLODChangedEventData(string CityName, CityLOD NewLOD);

    /// <summary>
    /// Summary event containing high level city data for low LOD rendering.
    /// </summary>
    public record CityStatusEventData(string CityName, int Population, double Budget, CityLOD LOD);

    /// <summary>
    /// Request to generate city data for the given urban area.
    /// </summary>
    public record CityGenerationRequestEventData(Polygon Area);

    /// <summary>
    /// Published when city generation for an area completes.
    /// </summary>
    public record CityGenerationCompletedEventData(Guid ModelId, Polygon Area);
}
