using System.Collections.Generic;

namespace Domain.Board
{
    /// <summary>
    /// Wires up Blocker/Covers relationships and initial Exposed/Covered state for a
    /// freshly constructed set of tiles, using the default stacking rule: any tile
    /// shares a "column" (same X/Y) with another tile at a strictly greater Layer is
    /// blocked by it (ARCHITECTURE.md §8.2). This is a level-construction concern, not
    /// a BoardModel runtime concern — BoardModel assumes relationships are already
    /// correct by the time it's constructed. Called today by tests and by whatever
    /// constructs a board in-Editor; will be called by BoardGenerator once the Level
    /// System (build order step 3+) exists.
    ///
    /// Cost is O(n * k) where k is the largest column's tile count — fine at level-load
    /// time (dozens to low hundreds of tiles), never called per-frame or per-tap.
    /// </summary>
    public static class StackingResolver
    {
        public static void ResolveStacking(IReadOnlyList<TileModel> tiles)
        {
            var byColumn = new Dictionary<(int X, int Y), List<TileModel>>();

            foreach (var tile in tiles)
            {
                var key = (tile.Coordinate.X, tile.Coordinate.Y);
                if (!byColumn.TryGetValue(key, out var column))
                {
                    column = new List<TileModel>(4);
                    byColumn[key] = column;
                }

                column.Add(tile);
            }

            foreach (var column in byColumn.Values)
            {
                for (var i = 0; i < column.Count; i++)
                {
                    var lower = column[i];
                    for (var j = 0; j < column.Count; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        var candidate = column[j];
                        if (candidate.Coordinate.Layer > lower.Coordinate.Layer)
                        {
                            lower.AddBlocker(candidate);
                            candidate.AddCovers(lower);
                        }
                    }
                }
            }

            foreach (var tile in tiles)
            {
                tile.State = tile.Blockers.Count == 0 ? TileState.Exposed : TileState.Covered;
            }
        }
    }
}
