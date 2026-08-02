using System;
using System.Collections.Generic;

namespace Core.EventBus
{
    /// <summary>
    /// Default <see cref="IEventBus"/> implementation. Handlers are stored per event
    /// Type in a dictionary resolved once at Subscribe time; Publish does a single
    /// dictionary lookup plus a delegate invoke — no LINQ, no allocation on the hot path.
    /// Subscribe/Unsubscribe do allocate (Delegate.Combine/Remove), which is acceptable
    /// since they only happen at scene setup/teardown, not per-frame or per-tap.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlersByEventType = new Dictionary<Type, Delegate>(32);

        public void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            var eventType = typeof(T);
            _handlersByEventType[eventType] = _handlersByEventType.TryGetValue(eventType, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            var eventType = typeof(T);
            if (!_handlersByEventType.TryGetValue(eventType, out var existing))
            {
                return;
            }

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
            {
                _handlersByEventType.Remove(eventType);
            }
            else
            {
                _handlersByEventType[eventType] = remaining;
            }
        }

        public void Publish<T>(T gameEvent) where T : struct, IGameEvent
        {
            var eventType = typeof(T);
            if (!_handlersByEventType.TryGetValue(eventType, out var existing))
            {
                return;
            }

            // Safe cast: Subscribe<T> only ever stores an Action<T> under this exact key.
            ((Action<T>)existing).Invoke(gameEvent);
        }

        /// <summary>
        /// Number of distinct event types with at least one live subscriber. Used by
        /// SceneRoot's dev-build teardown assertion to catch leaked subscriptions
        /// (expected to be 0 after a scene fully unsubscribes).
        /// </summary>
        public int ActiveSubscriptionTypeCount => _handlersByEventType.Count;
    }
}
