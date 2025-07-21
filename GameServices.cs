using Messaging;

namespace StrategyGame
{
    /// <summary>
    /// Provides access to shared services like the global message bus.
    /// </summary>
    public static class GameServices
    {
        public static MessageBus Bus { get; } = new MessageBus();

        static GameServices()
        {
            Bus.MessagePublished += EventLogger.Log;
            CityGen.AestheticMappingLayer.Initialize(Bus);
        }
    }
}
