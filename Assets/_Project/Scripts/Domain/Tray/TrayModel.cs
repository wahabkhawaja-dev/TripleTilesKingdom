using System;
using System.Collections.Generic;
using Domain.Board;

namespace Domain.Tray
{
    /// <summary>
    /// Holds the player's collected tiles by type. Fixed-capacity slot array (matching
    /// the physical tray in the level config) plus a per-type running count so
    /// <see cref="CountOf"/> is O(1) instead of scanning slots. Knows nothing about
    /// TileModel, coordinates, or the board — a tray only ever deals in TileTypeId,
    /// keeping it decoupled from board internals (Single Responsibility).
    /// </summary>
    public sealed class TrayModel : ITrayView
    {
        private readonly TileTypeId?[] _slots;
        private readonly Dictionary<TileTypeId, int> _countsByType = new Dictionary<TileTypeId, int>();
        private readonly HashSet<int> _scratchRemovalSet = new HashSet<int>();

        public int Capacity { get; }
        public int OccupiedCount { get; private set; }
        public bool IsFull => OccupiedCount >= Capacity;
        public bool IsEmpty => OccupiedCount == 0;
        public IReadOnlyList<TileTypeId?> Slots => _slots;

        /// <summary>Raised after a tile is inserted, with the type and the slot index it landed in.</summary>
        public event Action<TileTypeId, int> TileInserted;

        /// <summary>Raised after a tile is removed, with the type and the slot index it vacated.</summary>
        public event Action<TileTypeId, int> TileRemoved;

        /// <summary>Raised once OccupiedCount reaches Capacity.</summary>
        public event Action TrayFull;

        /// <summary>Raised once OccupiedCount returns to zero after having been non-zero.</summary>
        public event Action TrayEmptied;

        public TrayModel(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Tray capacity must be positive.");
            }

            Capacity = capacity;
            _slots = new TileTypeId?[capacity];
        }

        public int CountOf(TileTypeId type) => _countsByType.TryGetValue(type, out var count) ? count : 0;

        /// <summary>
        /// Attempts to place a tile of the given type into the first free slot.
        /// Returns false (no mutation) if the tray is already full.
        /// </summary>
        public bool TryInsert(TileTypeId type, out int slotIndex)
        {
            if (IsFull)
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = FindFirstEmptySlot();
            _slots[slotIndex] = type;
            OccupiedCount++;
            _countsByType[type] = CountOf(type) + 1;

            TileInserted?.Invoke(type, slotIndex);

            if (IsFull)
            {
                TrayFull?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Removes and empties every slot in <paramref name="slotIndices"/> — used by
        /// MatchSystem once a match is confirmed. All-or-nothing: if any index is
        /// invalid or already empty, nothing is removed and the method returns false,
        /// since a partial removal would silently corrupt the match result.
        ///
        /// Remaining occupied slots are compacted to stay contiguous from index 0,
        /// preserving relative order. This is required for adjacency-based match rules
        /// (e.g. a rule that only fires on N consecutive same-type slots) to make sense —
        /// without compaction, "consecutive" would be undefined the moment any hole
        /// exists. It also keeps <see cref="TryInsert"/>'s "first empty slot" behaviour
        /// equivalent to "append at the end," which consecutive-match rules rely on.
        /// </summary>
        public bool RemoveSlots(IReadOnlyList<int> slotIndices)
        {
            if (slotIndices == null)
            {
                return false;
            }

            for (var i = 0; i < slotIndices.Count; i++)
            {
                var index = slotIndices[i];
                if (index < 0 || index >= _slots.Length || _slots[index] == null)
                {
                    return false;
                }
            }

            var wasEmpty = IsEmpty;

            _scratchRemovalSet.Clear();
            for (var i = 0; i < slotIndices.Count; i++)
            {
                _scratchRemovalSet.Add(slotIndices[i]);
            }

            var write = 0;
            for (var read = 0; read < _slots.Length; read++)
            {
                var value = _slots[read];
                if (value == null)
                {
                    continue;
                }

                if (_scratchRemovalSet.Contains(read))
                {
                    var type = value.Value;
                    OccupiedCount--;
                    _countsByType[type] = CountOf(type) - 1;
                    TileRemoved?.Invoke(type, read);
                    continue;
                }

                _slots[write] = value;
                write++;
            }

            // Every position from `write` onward is now stale (either a removed slot's
            // old value, or a kept value that already got copied earlier in the array) —
            // clear them all so FindFirstEmptySlot/IsFull stay in sync with OccupiedCount.
            for (var i = write; i < _slots.Length; i++)
            {
                _slots[i] = null;
            }

            if (!wasEmpty && IsEmpty)
            {
                TrayEmptied?.Invoke();
            }

            return true;
        }

        private int FindFirstEmptySlot()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            // Unreachable: callers must check IsFull before calling TryInsert's internals,
            // and TryInsert itself guards this — kept as a hard failure rather than -1 to
            // surface the invariant break immediately instead of corrupting tray state.
            throw new InvalidOperationException("FindFirstEmptySlot called on a full tray.");
        }
    }
}
