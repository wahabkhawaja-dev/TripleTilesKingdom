using System;
using System.Collections.Generic;

namespace Domain.Levels
{
    /// <summary>
    /// All logical data needed to simulate one level — no visuals, no asset references.
    /// This is the pure-data counterpart to the future LevelDefinitionSO
    /// (ARCHITECTURE.md §7.1); a presentation/level-system adapter (build order step 3)
    /// will construct a LevelModel from a LevelDefinitionSO, but nothing in Domain
    /// depends on that adapter existing.
    /// </summary>
    public sealed class LevelModel
    {
        public int LevelId { get; }
        public IReadOnlyList<TileSpawnData> TileLayout { get; }
        public int TraySize { get; }
        public int MatchCount { get; }
        public string ThemeId { get; }
        public LevelRuleSet Rules { get; }

        public LevelModel(
            int levelId,
            IReadOnlyList<TileSpawnData> tileLayout,
            int traySize,
            int matchCount,
            string themeId,
            LevelRuleSet rules = null)
        {
            if (tileLayout == null || tileLayout.Count == 0)
            {
                throw new ArgumentException("A level must have at least one tile.", nameof(tileLayout));
            }

            if (traySize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traySize), "Tray size must be positive.");
            }

            if (matchCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(matchCount), "Match count must be at least 2.");
            }

            LevelId = levelId;
            TileLayout = tileLayout;
            TraySize = traySize;
            MatchCount = matchCount;
            ThemeId = themeId ?? string.Empty;
            Rules = rules ?? LevelRuleSet.Default;
        }
    }
}
