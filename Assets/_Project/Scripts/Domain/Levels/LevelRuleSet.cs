using System.Collections.Generic;

namespace Domain.Levels
{
    /// <summary>
    /// Extension point for future per-level mechanic configuration (time limits, move
    /// limits, special win conditions, wildcard tile behaviour, ...) without changing
    /// LevelModel's constructor every time a new mechanic needs a knob. Backed by a
    /// small flag bag rather than named properties on LevelModel itself, so adding a
    /// rule never requires touching LevelModel or anything that constructs one.
    /// Currently empty — no mechanic needs it yet.
    /// </summary>
    public sealed class LevelRuleSet
    {
        public static readonly LevelRuleSet Default = new LevelRuleSet();

        private readonly Dictionary<string, object> _flags;

        public LevelRuleSet(IReadOnlyDictionary<string, object> flags = null)
        {
            _flags = flags != null ? new Dictionary<string, object>(flags) : new Dictionary<string, object>();
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_flags.TryGetValue(key, out var boxed) && boxed is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }
    }
}
