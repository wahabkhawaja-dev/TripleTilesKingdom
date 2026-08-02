namespace Domain.Obstacles
{
    /// <summary>
    /// Extension point for future board obstacles (locked tiles, ice blocks, chains,
    /// spider webs — see ARCHITECTURE.md §15). BoardModel/TileModel consult this
    /// interface at well-defined hook points instead of branching on obstacle type, so
    /// adding a new obstacle is a new class, never an edit to existing gameplay logic.
    ///
    /// No concrete obstacle implements this yet (build order step 11) — only the
    /// interface and its Null Object default exist today.
    /// </summary>
    public interface IObstacleState
    {
        /// <summary>If true, an otherwise-exposed tile still cannot be tapped.</summary>
        bool BlocksSelection { get; }

        /// <summary>If true, a matched tile survives removal (e.g. needs a second hit to actually clear).</summary>
        bool BlocksMatchRemoval { get; }
    }
}
