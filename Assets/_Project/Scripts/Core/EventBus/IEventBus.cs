using System;

namespace Core.EventBus
{
    /// <summary>
    /// Central publish/subscribe bus for decoupled, one-way gameplay notifications.
    /// Events are value types implementing <see cref="IGameEvent"/> to avoid per-publish
    /// heap allocation.
    ///
    /// Use this for fan-out notifications where the publisher does not need a response
    /// from subscribers (e.g. "a match happened, react however you like"). Prefer a
    /// direct method call instead when control flow / return values matter (e.g. "can
    /// this tile be inserted into the tray right now?"). See ARCHITECTURE.md §6.3 and
    /// §16 risk #1 for the reasoning behind this split.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;
        void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent;
        void Publish<T>(T gameEvent) where T : struct, IGameEvent;
    }
}
