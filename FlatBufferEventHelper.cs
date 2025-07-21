using Google.FlatBuffers;
using System;

namespace StrategyGame
{
    /// <summary>
    /// Utility methods for serializing and deserializing game events using FlatBuffers.
    /// </summary>
    public static class FlatBufferEventHelper
    {
        public static byte[] Serialize(EconomyUpdatedEventData data)
        {
            var builder = new FlatBufferBuilder(64);
            var countryOffset = builder.CreateString(data.Country);
            StrategyGame.EconomyUpdatedEvent.StartEconomyUpdatedEvent(builder);
            StrategyGame.EconomyUpdatedEvent.AddVersion(builder, 1);
            StrategyGame.EconomyUpdatedEvent.AddCountry(builder, countryOffset);
            StrategyGame.EconomyUpdatedEvent.AddNewBudget(builder, data.NewBudget);
            StrategyGame.EconomyUpdatedEvent.AddGdp(builder, data.Gdp);
            var evt = StrategyGame.EconomyUpdatedEvent.EndEconomyUpdatedEvent(builder);
            StrategyGame.EconomyUpdatedEvent.FinishEconomyUpdatedEventBuffer(builder, evt);
            return builder.SizedByteArray();
        }

        public static EconomyUpdatedEventData DeserializeEconomyUpdatedEvent(byte[] bytes)
        {
            var evt = StrategyGame.EconomyUpdatedEvent.GetRootAsEconomyUpdatedEvent(new ByteBuffer(bytes));
            return new EconomyUpdatedEventData(evt.Country, evt.NewBudget, evt.Gdp);
        }

        public static byte[] Serialize(PopulationUpdatedEventData data)
        {
            var builder = new FlatBufferBuilder(32);
            StrategyGame.PopulationUpdatedEvent.StartPopulationUpdatedEvent(builder);
            StrategyGame.PopulationUpdatedEvent.AddVersion(builder, 1);
            StrategyGame.PopulationUpdatedEvent.AddEntityId(builder, data.EntityId);
            StrategyGame.PopulationUpdatedEvent.AddNewPopulation(builder, data.NewPopulation);
            var evt = StrategyGame.PopulationUpdatedEvent.EndPopulationUpdatedEvent(builder);
            StrategyGame.PopulationUpdatedEvent.FinishPopulationUpdatedEventBuffer(builder, evt);
            return builder.SizedByteArray();
        }

        public static PopulationUpdatedEventData DeserializePopulationUpdatedEvent(byte[] bytes)
        {
            var evt = StrategyGame.PopulationUpdatedEvent.GetRootAsPopulationUpdatedEvent(new ByteBuffer(bytes));
            return new PopulationUpdatedEventData(evt.EntityId, evt.NewPopulation);
        }
    }
}
