using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StrategyGame
{
    /// <summary>
    /// Simple thread-safe message bus for publishing and subscribing to game events.
    /// </summary>
    public class MessageBus
    {
        private static readonly Lazy<MessageBus> lazyInstance = new Lazy<MessageBus>(() => new MessageBus());
        public static MessageBus Instance => lazyInstance.Value;

        private readonly ConcurrentDictionary<Type, List<Delegate>> handlers = new ConcurrentDictionary<Type, List<Delegate>>();

        private MessageBus() { }

        /// <summary>
        /// Register a handler for the specified event type.
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            var list = handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// Publish a message to all subscribed handlers. Handlers are executed asynchronously.
        /// </summary>
        public void Publish<T>(T message)
        {
            EventLogger.Record(message);
            if (!handlers.TryGetValue(typeof(T), out var list))
                return;

            List<Delegate> copy;
            lock (list)
            {
                copy = list.ToList();
            }

            foreach (var handler in copy.Cast<Action<T>>())
            {
                Task.Run(() => handler(message));
            }
        }
    }
}
