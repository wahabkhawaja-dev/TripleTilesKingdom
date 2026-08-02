using System;
using System.Collections.Generic;
using Domain.Board;
using Domain.Tray;

namespace Domain.Matching
{
    /// <summary>
    /// Default match rule: a match fires the moment the tray holds
    /// <see cref="_requiredCount"/> tiles of the same type (3, 4, 5, ... — configured
    /// per level via LevelModel.MatchCount). Reuses an internal scratch List&lt;int&gt;
    /// across calls to avoid a per-evaluation allocation; the returned MatchResult
    /// copies out of it, so the result itself remains valid after the next Evaluate
    /// call even though the scratch buffer is reused.
    /// </summary>
    public sealed class ExactCountMatchRule : IMatchRule
    {
        private readonly int _requiredCount;
        private readonly List<int> _scratchIndices;

        public ExactCountMatchRule(int requiredCount)
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
            if (tray.CountOf(justInserted) < _requiredCount)
            {
                result = default;
                return false;
            }

            _scratchIndices.Clear();
            var slots = tray.Slots;

            for (var i = 0; i < slots.Count && _scratchIndices.Count < _requiredCount; i++)
            {
                if (slots[i] == justInserted)
                {
                    _scratchIndices.Add(i);
                }
            }

            // Copy out of the scratch buffer so the caller's MatchResult stays valid
            // even after this rule is evaluated again later.
            result = new MatchResult(justInserted, _scratchIndices.ToArray());
            return true;
        }
    }
}
