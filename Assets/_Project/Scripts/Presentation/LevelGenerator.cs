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
    ///
    /// Board layout is a pyramid: layer 0 covers the full board, each layer above it
    /// shrinks by one cell of inset on every side (centered), so the board reads as a
    /// tapering stack — many tiles at the base, fewer as you climb — matching the
    /// reference mockup's stacked board shape, and giving StackingResolver's default
    /// "higher layer blocks lower layer at the same column" rule real covering depth.
    /// </summary>
    public static class LevelGenerator
    {
        // Compact board (reference target is a small, tightly-packed cluster of ~25-50
        // tiles, not a full 6x6 grid spread across the whole screen).
        private const int BoardWidth = 5;
        private const int BoardHeight = 5;
        private const int MaxTileTypes = 10;

        /// <summary>Caps the pyramid's depth so it reads as a clean 3-layer stack rather than tapering all the way to a single-tile apex.</summary>
        private const int MaxLayers = 3;

        public static LevelModel GenerateLevel(int levelIndex)
        {
            var matchCount = Mathf.Min(3 + levelIndex / 10, 5);
            var trayCapacity = Mathf.Min(6 + levelIndex / 5, 12);

            var typeCount = Mathf.Clamp(trayCapacity / Mathf.Max(1, matchCount - 1), 2, MaxTileTypes);

            var tileLayout = GenerateBoardLayout(levelIndex, matchCount, typeCount, MaxLayers);

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

        private static List<TileSpawnData> GenerateBoardLayout(int levelIndex, int matchCount, int typeCount, int layerCount)
        {
            var random = new System.Random(levelIndex); // Deterministic per level.

            // Base layer first, apex last, each layer in plain raster order — no
            // per-layer shuffle here; geometry (which cells exist) and type assignment
            // (which fruit goes where) are handled as separate steps below so a
            // shortfall trim can target specific cells deterministically instead of
            // whichever ones a shuffle happened to put first.
            var positions = GeneratePyramidPositions(layerCount, out var baseLayerCount);

            var totalSlots = positions.Count;
            var shortfall = totalSlots % matchCount;
            var usableSlots = totalSlots - shortfall;

            if (shortfall > 0)
            {
                // Every type's count must stay an exact multiple of matchCount (so no
                // tile is ever permanently unmatchable), which means the board can't
                // always use every pyramid cell. Trimming a few cells off the tail of
                // the base layer's raster block (its bottom-right corner) reads as a
                // small, consistent notch in the board's edge — not a bug — unlike the
                // scattered mid-layer holes a random trim produces.
                positions.RemoveRange(baseLayerCount - shortfall, shortfall);
            }

            // Distribute whole match-count groups across types as evenly as possible
            // (remainder groups go to the first few types) rather than floor-dividing
            // twice (groups-per-type, then group size) — the double floor was silently
            // discarding whole extra groups' worth of tiles (e.g. 48 usable slots / 3
            // match-count / 3 types floored to 5 groups/type = 45 tiles used, 3 short,
            // instead of the 16 groups actually available), which is what left visible
            // holes in the board.
            var totalGroups = usableSlots / matchCount;
            var baseGroupsPerType = totalGroups / typeCount;
            var extraGroups = totalGroups % typeCount;

            var typeQueue = new List<TileTypeId>(usableSlots);
            for (var t = 0; t < typeCount; t++)
            {
                var groupsForType = baseGroupsPerType + (t < extraGroups ? 1 : 0);
                var countForType = groupsForType * matchCount;
                for (var c = 0; c < countForType; c++)
                {
                    typeQueue.Add(new TileTypeId(t));
                }
            }

            Shuffle(typeQueue, random);
            Shuffle(positions, random); // random fruit placement across the (now gap-free) board

            var spawns = new List<TileSpawnData>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                spawns.Add(new TileSpawnData(positions[i], typeQueue[i]));
            }

            return spawns;
        }

        /// <summary>
        /// Layer 0 spans the full board. Layer L+1's span shrinks by exactly one cell in
        /// each dimension relative to layer L's span (not two) — a layer-(L+1) tile at
        /// logical (x, y) must have layer-L tiles at (x,y), (x+1,y), (x,y+1), (x+1,y+1)
        /// all present to be a valid "sits centered over four tiles" placement (see
        /// StackingResolver.ResolvePyramidStacking), so its x/y range can only run up to
        /// one less than the layer below's max. Centered (approximately — integer
        /// coordinates can't split a shrink-by-one exactly evenly, so the inset alternates
        /// which side absorbs the extra cell). Stops once a layer would shrink to nothing.
        /// </summary>
        private static List<BoardCoordinate> GeneratePyramidPositions(int layerCount, out int baseLayerCount)
        {
            var positions = new List<BoardCoordinate>();
            baseLayerCount = 0;
            var layer = 0;

            while (layer < layerCount)
            {
                var span = Mathf.Min(BoardWidth, BoardHeight) - layer;
                if (span < 1)
                {
                    break;
                }

                var minX = layer / 2;
                var maxX = minX + (BoardWidth - layer) - 1;
                var minY = layer / 2;
                var maxY = minY + (BoardHeight - layer) - 1;

                if (minX > maxX || minY > maxY)
                {
                    break;
                }

                var countBefore = positions.Count;
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        positions.Add(new BoardCoordinate(x, y, layer));
                    }
                }

                if (layer == 0)
                {
                    baseLayerCount = positions.Count - countBefore;
                }

                layer++;
            }

            return positions;
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
