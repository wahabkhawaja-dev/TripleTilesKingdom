namespace Core.EventBus
{
    /// <summary>
    /// Marker interface for all event-bus payloads. Implementations must be structs
    /// (enforced by IEventBus's generic constraints) so publishing never allocates on
    /// the heap. See ARCHITECTURE.md §5.2.
    /// </summary>
    public interface IGameEvent
    {
    }
}
