using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Messaging
{
    /// <summary>
    /// Simple in-memory message bus with publish/subscribe semantics.
    /// Messages are queued and dispatched on a background worker thread.
    /// </summary>
    public sealed class MessageBus : IDisposable
    {
        private readonly ConcurrentDictionary<Type, List<Action<object>>> _subscribers = new();
        private readonly BlockingCollection<object> _queue = new();
        private readonly Thread _worker;

        /// <summary>
        /// Invoked whenever a message is published.
        /// </summary>
        public event Action<object>? MessagePublished;

        public MessageBus()
        {
            _worker = new Thread(EventLoop) { IsBackground = true };
            _worker.Start();
        }

        /// <summary>
        /// Subscribe to messages of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="handler">Callback invoked for each published message.</param>
        public void Subscribe<T>(Action<T> handler)
        {
            var list = _subscribers.GetOrAdd(typeof(T), _ => new List<Action<object>>());
            lock (list)
            {
                list.Add(o => handler((T)o));
            }
        }

        /// <summary>
        /// Publish a message instance.
        /// </summary>
        /// <param name="evt">Event object to publish.</param>
        public void Publish<T>(T evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            MessagePublished?.Invoke(evt!);
            _queue.Add(evt!);
        }

        private void EventLoop()
        {
            foreach (var evt in _queue.GetConsumingEnumerable())
            {
                Dispatch(evt);
            }
        }

        private void Dispatch(object evt)
        {
            if (_subscribers.TryGetValue(evt.GetType(), out var list))
            {
                Action<object>[] handlers;
                lock (list)
                {
                    handlers = list.ToArray();
                }

                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch
                    {
                        // Swallow exceptions from handlers to avoid stopping dispatch loop
                    }
                }
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _worker.Join();
            _queue.Dispose();
        }
    }
}
