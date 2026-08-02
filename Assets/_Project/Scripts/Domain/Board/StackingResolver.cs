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

        /// <summary>
        /// Alternative stacking rule for a "Mahjong tower" pyramid board: a tile at
        /// (x, y, layer L+1) sits centered over — and blocks — the four layer-L tiles at
        /// (x, y), (x+1, y), (x, y+1), (x+1, y+1). This is a many-to-one relationship
        /// (one upper tile can block up to four lower tiles, and a lower tile can have up
        /// to four blockers), unlike <see cref="ResolveStacking"/>'s direct 1:1 same-column
        /// rule. BoardModel's existing "exposed only once Blockers.Count == 0" cascade
        /// logic already gives the correct "a lower tile stays covered until every tile
        /// blocking it is gone" behaviour for free — only the relationship-wiring differs.
        /// Only adjacent layers interact (L+1 blocks L; L+2 never blocks L directly),
        /// matching standard Mahjong Solitaire tower rules.
        /// </summary>
        public static void ResolvePyramidStacking(IReadOnlyList<TileModel> tiles)
        {
            var byLayer = new Dictionary<int, Dictionary<(int X, int Y), TileModel>>();

            foreach (var tile in tiles)
            {
                var layer = tile.Coordinate.Layer;
                if (!byLayer.TryGetValue(layer, out var positions))
                {
                    positions = new Dictionary<(int X, int Y), TileModel>();
                    byLayer[layer] = positions;
                }

                positions[(tile.Coordinate.X, tile.Coordinate.Y)] = tile;
            }

            var maxLayer = -1;
            foreach (var layer in byLayer.Keys)
            {
                if (layer > maxLayer) maxLayer = layer;
            }

            for (var layer = 0; layer < maxLayer; layer++)
            {
                if (!byLayer.TryGetValue(layer, out var lowerPositions) ||
                    !byLayer.TryGetValue(layer + 1, out var upperPositions))
                {
                    continue;
                }

                foreach (var kvp in upperPositions)
                {
                    var upper = kvp.Value;
                    var bx = kvp.Key.X;
                    var by = kvp.Key.Y;

                    TryBlock(lowerPositions, bx, by, upper);
                    TryBlock(lowerPositions, bx + 1, by, upper);
                    TryBlock(lowerPositions, bx, by + 1, upper);
                    TryBlock(lowerPositions, bx + 1, by + 1, upper);
                }
            }

            foreach (var tile in tiles)
            {
                tile.State = tile.Blockers.Count == 0 ? TileState.Exposed : TileState.Covered;
            }
        }

        private static void TryBlock(Dictionary<(int X, int Y), TileModel> lowerPositions, int x, int y, TileModel upper)
        {
            if (lowerPositions.TryGetValue((x, y), out var lower))
            {
                lower.AddBlocker(upper);
                upper.AddCovers(lower);
            }
        }
    }
}
