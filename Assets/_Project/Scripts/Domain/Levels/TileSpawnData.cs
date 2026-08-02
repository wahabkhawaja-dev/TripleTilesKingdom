using Domain.Board;

namespace Domain.Levels
{
    /// <summary>
    /// One tile's worth of baked level layout: where it sits and what type it is, plus
    /// an optional obstacle to attach. Immutable — a LevelModel's TileLayout is a
    /// flat list of these, produced by whichever board-layout source built it (manual
    /// authoring or image-based generation, ARCHITECTURE.md §7-§8), independent of how
    /// it was authored.
    /// </summary>
    public readonly struct TileSpawnData
    {
        public readonly BoardCoordinate Coordinate;
        public readonly TileTypeId TileType;

        /// <summary>Null when this tile has no obstacle. Resolved to a concrete IObstacleState by whoever spawns the tile (build order step 11).</summary>
        public readonly int? ObstacleTypeId;

        public TileSpawnData(BoardCoordinate coordinate, TileTypeId tileType, int? obstacleTypeId = null)
        {
            Coordinate = coordinate;
            TileType = tileType;
            ObstacleTypeId = obstacleTypeId;
        }
    }
}
