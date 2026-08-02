using Domain.Board;
using Domain.Tray;

namespace Domain.Matching
{
    /// <summary>
    /// Strategy interface for "does the tray currently contain a match?". Pure query —
    /// implementations must never mutate the tray (enforced by taking ITrayView, not
    /// TrayModel). This is the extension point for future match rules (wildcards,
    /// "any N of a themed set", special tiles) without touching MatchSystem or
    /// TrayModel — a new rule is a new class implementing this interface.
    /// </summary>
    public interface IMatchRule
    {
        /// <summary>
        /// Checks whether inserting <paramref name="justInserted"/> completed a match.
        /// Only the just-inserted type needs checking — a match can only become true
        /// on the tile that was just added.
        /// </summary>
        bool TryFindMatch(ITrayView tray, TileTypeId justInserted, out MatchResult result);
    }
}
