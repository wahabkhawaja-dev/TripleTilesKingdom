using System;

namespace Domain.Board
{
    /// <summary>
    /// Strongly-typed logical tile "face" identifier used for matching. Deliberately not
    /// a reference to a visual asset (sprite, prefab, ScriptableObject) — the Domain
    /// layer has no concept of what a tile looks like, only whether two tiles are the
    /// same type for matching purposes. The Presentation/Level layers will map a
    /// TileTypeId to a themed visual (see ARCHITECTURE.md §12, ThemeSO).
    /// </summary>
    public readonly struct TileTypeId : IEquatable<TileTypeId>
    {
        public readonly int Value;

        public TileTypeId(int value) => Value = value;

        public bool Equals(TileTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TileTypeId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(TileTypeId left, TileTypeId right) => left.Equals(right);
        public static bool operator !=(TileTypeId left, TileTypeId right) => !left.Equals(right);

        public override string ToString() => $"TileType#{Value}";
    }
}
