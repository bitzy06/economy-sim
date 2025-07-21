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

        public static byte[] Serialize(DistrictDestroyedEventData data)
        {
            var builder = new FlatBufferBuilder(64);
            var cityOffset = builder.CreateString(data.CityName);
            var districtOffset = builder.CreateString(data.DistrictName);
            StrategyGame.DistrictDestroyedEvent.StartDistrictDestroyedEvent(builder);
            StrategyGame.DistrictDestroyedEvent.AddVersion(builder, 1);
            StrategyGame.DistrictDestroyedEvent.AddCity(builder, cityOffset);
            StrategyGame.DistrictDestroyedEvent.AddDistrict(builder, districtOffset);
            StrategyGame.DistrictDestroyedEvent.AddLostPopulation(builder, data.LostPopulation);
            var evt = StrategyGame.DistrictDestroyedEvent.EndDistrictDestroyedEvent(builder);
            StrategyGame.DistrictDestroyedEvent.FinishDistrictDestroyedEventBuffer(builder, evt);
            return builder.SizedByteArray();
        }

        public static DistrictDestroyedEventData DeserializeDistrictDestroyedEvent(byte[] bytes)
        {
            var evt = StrategyGame.DistrictDestroyedEvent.GetRootAsDistrictDestroyedEvent(new ByteBuffer(bytes));
            return new DistrictDestroyedEventData(evt.City, evt.District, evt.LostPopulation);
        }

        public static byte[] Serialize(MajorInfrastructureBuiltEventData data)
        {
            var builder = new FlatBufferBuilder(64);
            var cityOffset = builder.CreateString(data.CityName);
            var typeOffset = builder.CreateString(data.InfrastructureType);
            StrategyGame.MajorInfrastructureBuiltEvent.StartMajorInfrastructureBuiltEvent(builder);
            StrategyGame.MajorInfrastructureBuiltEvent.AddVersion(builder, 1);
            StrategyGame.MajorInfrastructureBuiltEvent.AddCity(builder, cityOffset);
            StrategyGame.MajorInfrastructureBuiltEvent.AddInfrastructureType(builder, typeOffset);
            StrategyGame.MajorInfrastructureBuiltEvent.AddValue(builder, data.Value);
            var evt = StrategyGame.MajorInfrastructureBuiltEvent.EndMajorInfrastructureBuiltEvent(builder);
            StrategyGame.MajorInfrastructureBuiltEvent.FinishMajorInfrastructureBuiltEventBuffer(builder, evt);
            return builder.SizedByteArray();
        }

        public static MajorInfrastructureBuiltEventData DeserializeMajorInfrastructureBuiltEvent(byte[] bytes)
        {
            var evt = StrategyGame.MajorInfrastructureBuiltEvent.GetRootAsMajorInfrastructureBuiltEvent(new ByteBuffer(bytes));
            return new MajorInfrastructureBuiltEventData(evt.City, evt.InfrastructureType, evt.Value);
        }
    }
}
