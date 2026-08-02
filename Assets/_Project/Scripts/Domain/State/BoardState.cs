namespace Domain.State
{
    /// <summary>
    /// High-level gameplay flow state for a loaded level. Distinct from TileState
    /// (per-tile) and TrayModel's own bookkeeping — this is "what is the level as a
    /// whole doing right now".
    /// </summary>
    public enum BoardState
    {
        /// <summary>Level data is being loaded/board is being generated. Input disabled.</summary>
        Loading,

        /// <summary>Board generated, idle, accepting taps.</summary>
        Ready,

        /// <summary>A tap has been accepted and its animation/resolution is in flight. Input disabled.</summary>
        Animating,

        /// <summary>Player paused the game. Input disabled.</summary>
        Paused,

        /// <summary>Board fully cleared. Terminal.</summary>
        Won,

        /// <summary>Tray filled without completing a match. Terminal.</summary>
        Lost
    }
}
