namespace Domain.Board
{
    /// <summary>
    /// Board lifecycle state of a single tile. Deliberately does NOT encode
    /// "currently in the tray" — that's tracked independently via
    /// <see cref="TileModel.IsSelected"/>, since selection is an orthogonal concern
    /// (a tile leaves the board the instant it's tapped, but stays logically
    /// "selected" until it's matched and disposed of by the tray).
    /// </summary>
    public enum TileState
    {
        /// <summary>Blocked by one or more tiles above it. Not selectable.</summary>
        Covered,

        /// <summary>No blockers. Selectable (subject to IObstacleState.BlocksSelection).</summary>
        Exposed,

        /// <summary>Left the board — tapped and (about to be, or already) resolved by the tray. Terminal state.</summary>
        Removed
    }
}
