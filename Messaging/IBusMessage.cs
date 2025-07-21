namespace Messaging
{
    /// <summary>
    /// Marker interface for events published on the message bus.
    /// </summary>
    public interface IBusMessage
    {
        /// <summary>
        /// Identifier for the city that originated the message.
        /// </summary>
        string CityOrigin { get; }
    }
}
