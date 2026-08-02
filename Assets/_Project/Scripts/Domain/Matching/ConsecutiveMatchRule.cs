using System;
using System.Collections.Generic;
using Domain.Board;
using Domain.Tray;

namespace Domain.Matching
{
    /// <summary>
    /// Match rule for the "Triple Tile" tray mechanic: a match fires only when the last
    /// <see cref="_requiredCount"/> tiles inserted into the tray — i.e. the trailing run
    /// ending at the most recently occupied slot — are all the same type. Two of type A,
    /// then a type B, then another type A does NOT match, even though the tray holds
    /// three A's in total; the B breaks the run.
    ///
    /// This relies on TrayModel compacting on removal (no gaps), so "the last occupied
    /// slot" is always well-defined as index OccupiedCount-1 and a newly inserted tile
    /// always lands there. Only the trailing run needs checking: every prior turn either
    /// resolved a matching trailing run or left a non-matching one, so a new tile can
    /// only ever complete a NEW run ending at the tray's tail.
    /// </summary>
    public sealed class ConsecutiveMatchRule : IMatchRule
    {
        private readonly int _requiredCount;
        private readonly List<int> _scratchIndices;

        public ConsecutiveMatchRule(int requiredCount)
        {
            if (requiredCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCount), "A match requires at least 2 tiles.");
            }

            _requiredCount = requiredCount;
            _scratchIndices = new List<int>(requiredCount);
        }

        public bool TryFindMatch(ITrayView tray, TileTypeId justInserted, out MatchResult result)
        {
            var lastIndex = tray.OccupiedCount - 1;
            if (lastIndex < 0)
            {
                result = default;
                return false;
            }

            var slots = tray.Slots;
            var runLength = 0;
            var i = lastIndex;
            while (i >= 0 && slots[i] == justInserted)
            {
                runLength++;
                i--;
            }

            if (runLength < _requiredCount)
            {
                result = default;
                return false;
            }

            _scratchIndices.Clear();
            for (var slot = lastIndex - _requiredCount + 1; slot <= lastIndex; slot++)
            {
                _scratchIndices.Add(slot);
            }

            result = new MatchResult(justInserted, _scratchIndices.ToArray());
            return true;
        }
    }
}
