namespace Domain.Obstacles
{
    /// <summary>
    /// Null Object default for TileModel.Obstacle. Every tile has a non-null
    /// IObstacleState, so gameplay code never needs a null check before reading
    /// BlocksSelection/BlocksMatchRemoval — an unobstructed tile just answers false to
    /// both via this shared, allocation-free singleton.
    /// </summary>
    public sealed class NullObstacleState : IObstacleState
    {
        public static readonly NullObstacleState Instance = new NullObstacleState();

        private NullObstacleState()
        {
        }

        public bool BlocksSelection => false;
        public bool BlocksMatchRemoval => false;
    }
}
