using System;
using Domain.Board;
using Domain.Tray;

namespace Domain.Matching
{
    /// <summary>
    /// Thin facade over an <see cref="IMatchRule"/>. Exists so callers depend on
    /// "the game's match system" rather than a specific rule implementation, and so a
    /// future mode/level could swap rules (e.g. wildcard tiles) by constructing
    /// MatchSystem with a different IMatchRule — no other call site changes.
    /// Read-only: never mutates the tray itself. The caller (a future
    /// GameFlowController) is responsible for calling TrayModel.RemoveSlots with the
    /// result's SlotIndices once a match is confirmed.
    /// </summary>
    public sealed class MatchSystem
    {
        private readonly IMatchRule _rule;

        public MatchSystem(IMatchRule rule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public bool TryEvaluate(ITrayView tray, TileTypeId justInserted, out MatchResult result) =>
            _rule.TryFindMatch(tray, justInserted, out result);
    }
}
