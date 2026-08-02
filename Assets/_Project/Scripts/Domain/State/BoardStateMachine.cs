using System;
using System.Collections.Generic;

namespace Domain.State
{
    /// <summary>
    /// Guarded state machine for <see cref="BoardState"/>. A bare enum with an
    /// unguarded setter lets any system push the board into an illegal state (e.g.
    /// Won straight to Animating) with no signal until something downstream breaks in
    /// a confusing way. This machine makes illegal transitions fail loudly and
    /// immediately instead.
    /// </summary>
    public sealed class BoardStateMachine
    {
        private static readonly Dictionary<BoardState, BoardState[]> AllowedTransitions = new Dictionary<BoardState, BoardState[]>
        {
            [BoardState.Loading] = new[] { BoardState.Ready },
            [BoardState.Ready] = new[] { BoardState.Animating, BoardState.Paused },
            [BoardState.Animating] = new[] { BoardState.Ready, BoardState.Won, BoardState.Lost },
            [BoardState.Paused] = new[] { BoardState.Ready },
            [BoardState.Won] = new[] { BoardState.Loading },
            [BoardState.Lost] = new[] { BoardState.Loading },
        };

        public BoardState Current { get; private set; }

        /// <summary>Raised after a successful transition with (previous, current).</summary>
        public event Action<BoardState, BoardState> StateChanged;

        public BoardStateMachine(BoardState initial = BoardState.Loading)
        {
            Current = initial;
        }

        public bool CanTransitionTo(BoardState target)
        {
            return AllowedTransitions.TryGetValue(Current, out var targets) && Array.IndexOf(targets, target) >= 0;
        }

        /// <summary>
        /// Attempts the transition. Returns false and leaves state unchanged if the
        /// transition isn't legal from the current state — callers in dev builds
        /// should treat a false return as a bug, not a normal outcome.
        /// </summary>
        public bool TrySetState(BoardState target)
        {
            if (!CanTransitionTo(target))
            {
                return false;
            }

            var previous = Current;
            Current = target;
            StateChanged?.Invoke(previous, target);
            return true;
        }
    }
}
