using System.Collections.Generic;
using Domain.Board;

namespace Domain.Matching
{
    /// <summary>
    /// Describes a confirmed match: which type matched, which tray slots it occupies,
    /// and how many tiles were involved. Immutable value snapshot — safe to hold onto
    /// after the rule that produced it evaluates again (unlike the rule's internal
    /// scratch buffer; see ExactCountMatchRule).
    /// </summary>
    public readonly struct MatchResult
    {
        public readonly TileTypeId TileType;
        public readonly IReadOnlyList<int> SlotIndices;
        public readonly int MatchCount;

        public MatchResult(TileTypeId tileType, IReadOnlyList<int> slotIndices)
        {
            TileType = tileType;
            SlotIndices = slotIndices;
            MatchCount = slotIndices.Count;
        }
    }
}
