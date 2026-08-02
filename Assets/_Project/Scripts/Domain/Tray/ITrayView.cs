using System.Collections.Generic;
using Domain.Board;

namespace Domain.Tray
{
    /// <summary>
    /// Read-only view of a tray, consumed by matching logic. Deliberately excludes
    /// mutation methods (Interface Segregation) so an IMatchRule can query the tray but
    /// can never accidentally insert or remove a tile itself — TrayModel/MatchSystem
    /// own that.
    /// </summary>
    public interface ITrayView
    {
        int Capacity { get; }
        int OccupiedCount { get; }
        bool IsFull { get; }
        bool IsEmpty { get; }

        /// <summary>Slot contents by index; null means the slot is empty.</summary>
        IReadOnlyList<TileTypeId?> Slots { get; }

        /// <summary>O(1) count of how many slots currently hold the given type.</summary>
        int CountOf(TileTypeId type);
    }
}
