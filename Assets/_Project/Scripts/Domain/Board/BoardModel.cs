using System;
using System.Collections.Generic;

namespace Domain.Board
{
    /// <summary>
    /// Owns every tile for one loaded level and is the single source of truth for board
    /// state. Maintains incrementally-updated indexes so every public query and
    /// mutation below is O(1) or O(k) (k = tiles directly affected by a single removal)
    /// — never an O(n) scan across the whole board. Assumes tiles handed to it already
    /// have correct Blocker/Covers relationships (see <see cref="StackingResolver"/>).
    /// </summary>
    public sealed class BoardModel
    {
        private readonly List<TileModel> _allTiles;
        private readonly Dictionary<TileId, TileModel> _tilesById;
        private readonly Dictionary<BoardCoordinate, TileModel> _tilesByCoordinate;
        private readonly Dictionary<TileTypeId, HashSet<TileModel>> _tilesByType;
        private readonly HashSet<TileModel> _selectableTiles;

        private int _tilesRemainingOnBoard;

        /// <summary>Raised when a tile transitions from Covered to Exposed because its last blocker was removed.</summary>
        public event Action<TileModel> TileExposed;

        /// <summary>Raised when a tile is tapped/removed from the board (State becomes Removed).</summary>
        public event Action<TileModel> TileRemoved;

        /// <summary>Raised once, when the last tile leaves the board.</summary>
        public event Action BoardCleared;

        public IReadOnlyList<TileModel> AllTiles => _allTiles;

        /// <summary>Currently exposed AND selectable tiles. O(1) to read — never recomputed by scanning AllTiles.</summary>
        public IReadOnlyCollection<TileModel> SelectableTiles => _selectableTiles;

        public int TilesRemainingOnBoard => _tilesRemainingOnBoard;

        public bool IsCleared => _tilesRemainingOnBoard == 0;

        public BoardModel(IReadOnlyList<TileModel> tiles)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            _allTiles = new List<TileModel>(tiles);
            _tilesById = new Dictionary<TileId, TileModel>(tiles.Count);
            _tilesByCoordinate = new Dictionary<BoardCoordinate, TileModel>(tiles.Count);
            _tilesByType = new Dictionary<TileTypeId, HashSet<TileModel>>();
            _selectableTiles = new HashSet<TileModel>();

            foreach (var tile in _allTiles)
            {
                _tilesById[tile.Id] = tile;
                _tilesByCoordinate[tile.Coordinate] = tile;
                IndexByType(tile);

                if (tile.IsSelectable)
                {
                    _selectableTiles.Add(tile);
                }
            }

            _tilesRemainingOnBoard = _allTiles.Count;
        }

        public bool TryGetTile(TileId id, out TileModel tile) => _tilesById.TryGetValue(id, out tile);

        public bool TryGetTile(BoardCoordinate coordinate, out TileModel tile) => _tilesByCoordinate.TryGetValue(coordinate, out tile);

        /// <summary>
        /// Tiles of a given type still on the board (not yet removed). Returns the live
        /// backing set, not a copy — treat as read-only and don't mutate while iterating.
        /// </summary>
        public IReadOnlyCollection<TileModel> GetTilesOfType(TileTypeId type) =>
            _tilesByType.TryGetValue(type, out var set) ? set : Array.Empty<TileModel>();

        /// <summary>
        /// Taps a tile: marks it Removed + Selected, unindexes it from the board, and
        /// cascades exposure to any tile it was covering. Idempotent — calling this
        /// twice on the same tile, or on a tile that isn't selectable, does nothing on
        /// the second call rather than throwing, since input races (e.g. a duplicate
        /// tap event) should never corrupt board state.
        /// </summary>
        public bool TrySelectTile(TileModel tile)
        {
            if (tile == null || !tile.IsSelectable)
            {
                return false;
            }

            tile.State = TileState.Removed;
            tile.IsSelected = true;

            _selectableTiles.Remove(tile);
            _tilesByType[tile.TileType].Remove(tile);
            _tilesRemainingOnBoard--;

            CascadeExposure(tile);

            TileRemoved?.Invoke(tile);

            if (_tilesRemainingOnBoard == 0)
            {
                BoardCleared?.Invoke();
            }

            return true;
        }

        private void CascadeExposure(TileModel removedTile)
        {
            var covers = removedTile.Covers;
            for (var i = 0; i < covers.Count; i++)
            {
                var covered = covers[i];
                covered.RemoveBlocker(removedTile);

                if (covered.Blockers.Count == 0 && covered.State == TileState.Covered)
                {
                    covered.State = TileState.Exposed;

                    if (covered.IsSelectable)
                    {
                        _selectableTiles.Add(covered);
                    }

                    TileExposed?.Invoke(covered);
                }
            }
        }

        private void IndexByType(TileModel tile)
        {
            if (!_tilesByType.TryGetValue(tile.TileType, out var set))
            {
                set = new HashSet<TileModel>();
                _tilesByType[tile.TileType] = set;
            }

            set.Add(tile);
        }
    }
}
