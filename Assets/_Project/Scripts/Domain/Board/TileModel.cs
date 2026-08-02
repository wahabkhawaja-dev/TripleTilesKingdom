using System.Collections.Generic;
using Domain.Obstacles;

namespace Domain.Board
{
    /// <summary>
    /// A single logical tile. Pure data + relationship bookkeeping — no visuals, no
    /// animation, no Unity types. Relationship mutation (<see cref="AddBlocker"/> etc.)
    /// and state transitions are internal: only <see cref="BoardModel"/>, which owns the
    /// invariants around covering/covered relationships, is allowed to change them.
    /// Everything else reads this tile through its public, read-only surface.
    /// </summary>
    public sealed class TileModel
    {
        private readonly List<TileModel> _blockers = new List<TileModel>(2);
        private readonly List<TileModel> _covers = new List<TileModel>(2);

        public TileId Id { get; }
        public TileTypeId TileType { get; }
        public BoardCoordinate Coordinate { get; }
        public TileState State { get; internal set; }
        public IObstacleState Obstacle { get; internal set; }

        /// <summary>
        /// True once this tile has been tapped and handed to the tray. Independent of
        /// <see cref="State"/> — a tile is set Removed from the board's perspective the
        /// moment it's tapped, but stays IsSelected until the tray actually resolves it
        /// (matched and cleared, or the tray tracks it for other future rules).
        /// </summary>
        public bool IsSelected { get; internal set; }

        /// <summary>Tiles directly stacked on top of this one. Empty when this tile is exposed.</summary>
        public IReadOnlyList<TileModel> Blockers => _blockers;

        /// <summary>Tiles directly beneath this one that this tile is currently blocking.</summary>
        public IReadOnlyList<TileModel> Covers => _covers;

        /// <summary>The actual tap-ability check: exposed, not already selected, and not held back by an obstacle.</summary>
        public bool IsSelectable => State == TileState.Exposed && !IsSelected && !Obstacle.BlocksSelection;

        public TileModel(TileId id, TileTypeId tileType, BoardCoordinate coordinate, IObstacleState obstacle = null)
        {
            Id = id;
            TileType = tileType;
            Coordinate = coordinate;
            Obstacle = obstacle ?? NullObstacleState.Instance;
            State = TileState.Covered; // BoardModel promotes this to Exposed during setup if it has no blockers.
        }

        internal void AddBlocker(TileModel blocker)
        {
            _blockers.Add(blocker);
        }

        internal void RemoveBlocker(TileModel blocker)
        {
            _blockers.Remove(blocker);
        }

        internal void AddCovers(TileModel covered)
        {
            _covers.Add(covered);
        }

        public override string ToString() => $"{Id} [{TileType}] @ {Coordinate} ({State}{(IsSelected ? ", Selected" : string.Empty)})";
    }
}
