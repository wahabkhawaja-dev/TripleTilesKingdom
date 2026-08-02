using System;

namespace Domain.Board
{
    /// <summary>
    /// Strongly-typed tile instance identifier. Wraps an int (not a Guid) — cheap to
    /// generate, cheap to hash, deterministic ordering, and sufficient uniqueness within
    /// a single loaded level's lifetime. Wrapping it in a struct (instead of passing raw
    /// ints around) prevents a tile id from ever being accidentally passed where a
    /// TileTypeId or a slot index was expected.
    /// </summary>
    public readonly struct TileId : IEquatable<TileId>
    {
        public readonly int Value;

        public TileId(int value) => Value = value;

        public bool Equals(TileId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TileId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(TileId left, TileId right) => left.Equals(right);
        public static bool operator !=(TileId left, TileId right) => !left.Equals(right);

        public override string ToString() => $"Tile#{Value}";
    }

    /// <summary>
    /// Generates sequential, unique TileId values for a single board's lifetime.
    /// Owned by whoever constructs a BoardModel (e.g. a future BoardGenerator) — not a
    /// static/global counter, so multiple boards (tests, future multi-board scenarios)
    /// never collide or leak state between runs.
    /// </summary>
    public sealed class TileIdFactory
    {
        private int _next;

        public TileId Next() => new TileId(_next++);
    }
}
