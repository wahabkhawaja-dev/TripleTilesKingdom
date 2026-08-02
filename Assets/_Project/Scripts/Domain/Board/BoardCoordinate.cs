using System;

namespace Domain.Board
{
    /// <summary>
    /// Immutable logical position of a tile on the board: column (X), row (Y), and
    /// stacking layer. Two tiles at the same X/Y but different Layer are distinct
    /// positions (e.g. a tile stacked on top of another). Pure value type — no Unity
    /// dependency, safe to use in save data, hashing, and unit tests.
    /// </summary>
    public readonly struct BoardCoordinate : IEquatable<BoardCoordinate>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Layer;

        public BoardCoordinate(int x, int y, int layer)
        {
            X = x;
            Y = y;
            Layer = layer;
        }

        /// <summary>Same X/Y, one layer above this one. Does not imply a tile exists there.</summary>
        public BoardCoordinate WithLayer(int layer) => new BoardCoordinate(X, Y, layer);

        /// <summary>True if <paramref name="other"/> shares this coordinate's X/Y regardless of layer.</summary>
        public bool IsSameColumn(in BoardCoordinate other) => X == other.X && Y == other.Y;

        // Cardinal-neighbor helpers reserved for future adjacency-based mechanics
        // (chain reactions, spider webs) — same layer, no diagonal by default.
        public BoardCoordinate North => new BoardCoordinate(X, Y + 1, Layer);
        public BoardCoordinate South => new BoardCoordinate(X, Y - 1, Layer);
        public BoardCoordinate East => new BoardCoordinate(X + 1, Y, Layer);
        public BoardCoordinate West => new BoardCoordinate(X - 1, Y, Layer);

        public bool Equals(BoardCoordinate other) => X == other.X && Y == other.Y && Layer == other.Layer;

        public override bool Equals(object obj) => obj is BoardCoordinate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                hash = hash * 31 + Layer;
                return hash;
            }
        }

        public static bool operator ==(BoardCoordinate left, BoardCoordinate right) => left.Equals(right);
        public static bool operator !=(BoardCoordinate left, BoardCoordinate right) => !left.Equals(right);

        public override string ToString() => $"({X}, {Y}, L{Layer})";
    }
}
