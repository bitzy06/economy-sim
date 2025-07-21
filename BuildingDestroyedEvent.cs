using System;

namespace StrategyGame
{
    public record BuildingDestroyedEvent(Guid CityId, Guid BuildingId, LandUseType LandUse);
}
