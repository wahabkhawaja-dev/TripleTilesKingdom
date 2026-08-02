using System.Collections.Generic;
using System.Linq;
using Domain.Board;
using Domain.Levels;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Temporary procedural level generator. Generates playable levels for the
    /// consecutive-run matching rule (see ConsecutiveMatchRule): a match only fires when
    /// the last MatchCount tiles inserted into the tray are the same type, so solvability
    /// now depends on tap ORDER, not just total counts — a property that's genuinely hard
    /// to guarantee for free-order play without a real solver. This generator applies two
    /// heuristics that make getting stuck rare in practice, but it is NOT a proof of
    /// solvability under adversarial tap order:
    ///   1. Every tile type's total count on the board is an exact multiple of MatchCount
    ///      (no remainder that could get permanently stranded).
    ///   2. The number of distinct types used, multiplied by (MatchCount - 1) — the
    ///      worst-case simultaneous tray occupancy if every type's run gets interrupted —
    ///      never exceeds the level's tray capacity, bounding how badly interleaved taps
    ///      can fill the tray before some match is forced to complete.
    /// This will be replaced by image-based/hand-authored generation (with a real
    /// solvability checker) once the PNG level pipeline is complete — see ROADMAP.md.
    /// </summary>
    public static class LevelGenerator
    {
        private const int BoardWidth = 6;
        private const int BoardHeight = 6;
        private const int MaxTileTypes = 10;

        public static LevelModel GenerateLevel(int levelIndex)
        {
            var matchCount = Mathf.Min(3 + levelIndex / 10, 5);
            var trayCapacity = Mathf.Min(6 + levelIndex / 5, 12);

            var typeCount = Mathf.Clamp(trayCapacity / Mathf.Max(1, matchCount - 1), 2, MaxTileTypes);

            var tileLayout = GenerateBoardLayout(levelIndex, matchCount, typeCount);

            return new LevelModel(
                levelId: levelIndex,
                tileLayout: tileLayout,
                traySize: trayCapacity,
                matchCount: matchCount,
                themeId: "placeholder_theme_" + (levelIndex % 3),
                rules: LevelRuleSet.Default);
        }

        /// <summary>
        /// Deterministically picks <paramref name="typeCount"/> distinct sprites from the
        /// theme's fruit pool for this level, indexed 0..typeCount-1 to line up directly
        /// with TileTypeId.Value. Different levels draw a different subset/order from the
        /// same pool, so the same handful of sprites gives visual variety across many
        /// levels without new art per level.
        /// </summary>
        public static Sprite[] SelectFruitSprites(int levelIndex, int typeCount, IReadOnlyList<Sprite> pool)
        {
            var random = new System.Random(levelIndex * 7919 + 13); // distinct stream from the layout RNG
            var indices = Enumerable.Range(0, pool.Count).ToList();
            Shuffle(indices, random);

            var count = Mathf.Min(typeCount, pool.Count);
            var result = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = pool[indices[i]];
            }
            return result;
        }

        private static List<TileSpawnData> GenerateBoardLayout(int levelIndex, int matchCount, int typeCount)
        {
            var random = new System.Random(levelIndex); // Deterministic per level.

            var basePositions = new List<BoardCoordinate>(BoardWidth * BoardHeight);
            for (var y = 0; y < BoardHeight; y++)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    basePositions.Add(new BoardCoordinate(x, y, 0));
                }
            }

            Shuffle(basePositions, random);

            // A sparse subset of cells also get a layer-1 tile stacked on top, giving the
            // reference mockup's stacked/covered look without affecting the count math
            // below — StackingResolver treats layer only as a blocking relationship.
            var stackedCellCount = basePositions.Count / 6;
            var stackedCells = basePositions.GetRange(0, stackedCellCount);

            var totalSlots = basePositions.Count + stackedCells.Count;

            // Every type gets a count that's a clean multiple of matchCount.
            var usableSlots = totalSlots - (totalSlots % matchCount);
            var multiplesPerType = Mathf.Max(1, (usableSlots / matchCount) / typeCount);

            var typeQueue = new List<TileTypeId>(usableSlots);
            for (var t = 0; t < typeCount; t++)
            {
                var countForType = multiplesPerType * matchCount;
                for (var c = 0; c < countForType; c++)
                {
                    typeQueue.Add(new TileTypeId(t));
                }
            }

            Shuffle(typeQueue, random);

            var spawns = new List<TileSpawnData>(typeQueue.Count);
            var queueIndex = 0;

            for (var i = 0; i < basePositions.Count && queueIndex < typeQueue.Count; i++)
            {
                spawns.Add(new TileSpawnData(basePositions[i], typeQueue[queueIndex]));
                queueIndex++;
            }

            for (var i = 0; i < stackedCells.Count && queueIndex < typeQueue.Count; i++)
            {
                spawns.Add(new TileSpawnData(stackedCells[i].WithLayer(1), typeQueue[queueIndex]));
                queueIndex++;
            }

            return spawns;
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
