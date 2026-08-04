using System.Collections.Generic;
using Domain.Board;
using Domain.Levels;
using LevelSystem.Data;
using UnityEngine;

namespace LevelSystem.Generation
{
    /// <summary>
    /// Converts a data-only <see cref="LevelDefinitionSO"/> into a fully-formed
    /// <see cref="LevelModel"/> ready for <c>BoardGenerator.CreateBoard</c>. Emits
    /// tiles in per-layer raster order, auto-centers each successive layer over the
    /// one below (when requested), and — crucially — never produces a board with
    /// partial rows or notches. If the requested layers would give a total tile
    /// count that isn't a multiple of MatchCount, the builder auto-adjusts by:
    ///   1. dropping the topmost layer entirely and retrying, until either the
    ///      count is divisible or only the base remains, THEN if still not,
    ///   2. shrinking the top remaining layer's dimensions (height first, then
    ///      width) until divisibility is reached.
    /// The output is always a set of complete rectangular layers — the game never
    /// renders a broken grid.
    ///
    /// Coordinates are compatible with <c>StackingResolver.ResolvePyramidStacking</c>:
    /// a layer-(L+1) tile at (x, y) covers the four layer-L tiles at (x, y),
    /// (x+1, y), (x, y+1), (x+1, y+1). Non-base layers that don't fit (w &gt; W-1
    /// or h &gt; H-1) are clamped down to a legal size at build time.
    /// </summary>
    public static class LevelDefinitionBuilder
    {
        public static LevelModel Build(LevelDefinitionSO def)
        {
            if (def == null) throw new System.ArgumentNullException(nameof(def));
            if (def.Layers == null || def.Layers.Count == 0)
                throw new System.ArgumentException($"LevelDefinition '{def.name}' has no layers.");

            var matchCount = Mathf.Max(2, def.MatchCount);
            var traySize = Mathf.Max(matchCount, def.TraySize);
            var seed = def.Seed != 0 ? def.Seed : def.LevelId * 7919 + 13;

            var effectiveLayers = SolveCompleteGridLayers(def.Layers, matchCount);
            var positions = GeneratePositionsFromLayers(effectiveLayers);

            var usableSlots = positions.Count;
            var maxTypesByTray = Mathf.Max(1, traySize / Mathf.Max(1, matchCount - 1));
            var typeCount = def.TileTypeCount > 0
                ? def.TileTypeCount
                : Mathf.Clamp(maxTypesByTray, 2, 10);
            typeCount = Mathf.Max(1, Mathf.Min(typeCount, usableSlots / matchCount));
            if (typeCount < 1) typeCount = 1;

            var totalGroups = usableSlots / matchCount;
            var baseGroupsPerType = totalGroups / typeCount;
            var extraGroups = totalGroups % typeCount;

            var typeQueue = new List<TileTypeId>(usableSlots);
            for (var t = 0; t < typeCount; t++)
            {
                var groups = baseGroupsPerType + (t < extraGroups ? 1 : 0);
                for (var c = 0; c < groups * matchCount; c++) typeQueue.Add(new TileTypeId(t));
            }

            var random = new System.Random(seed);
            Shuffle(typeQueue, random);
            Shuffle(positions, random);

            var spawns = new List<TileSpawnData>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                spawns.Add(new TileSpawnData(positions[i], typeQueue[i]));
            }

            return new LevelModel(
                levelId: def.LevelId,
                tileLayout: spawns,
                traySize: traySize,
                matchCount: matchCount,
                themeId: string.IsNullOrEmpty(def.ThemeId) ? "default" : def.ThemeId,
                rules: LevelRuleSet.Default);
        }

        /// <summary>
        /// Emits the coordinates <see cref="Build"/> would produce (without the tile
        /// assignments) — used by the editor preview so what you see in the inspector
        /// is exactly what gameplay will render, including any auto-adjustments.
        /// </summary>
        public static List<BoardCoordinate> GeneratePositions(LevelDefinitionSO def)
        {
            if (def == null || def.Layers == null || def.Layers.Count == 0)
            {
                return new List<BoardCoordinate>();
            }
            var matchCount = Mathf.Max(2, def.MatchCount);
            var layers = SolveCompleteGridLayers(def.Layers, matchCount);
            return GeneratePositionsFromLayers(layers);
        }

        /// <summary>
        /// Returns the same layer list the builder would use — with clamping and
        /// divisibility auto-adjustments applied. Useful for editor tooling that
        /// wants to display the "effective" configuration side-by-side with the raw
        /// SO fields the designer authored.
        /// </summary>
        public static List<LayerDefinition> SolveCompleteGridLayers(
            IReadOnlyList<LayerDefinition> input, int matchCount)
        {
            var layers = new List<LayerDefinition>(input.Count);
            for (var i = 0; i < input.Count; i++)
            {
                var l = input[i];
                if (l.Width < 1) l.Width = 1;
                if (l.Height < 1) l.Height = 1;

                if (i > 0)
                {
                    var prev = layers[i - 1];
                    l.Width = Mathf.Min(l.Width, Mathf.Max(1, prev.Width - 1));
                    l.Height = Mathf.Min(l.Height, Mathf.Max(1, prev.Height - 1));
                }
                layers.Add(l);
            }

            // Strategy 1: drop the topmost layer(s) until the total is divisible or
            // only the base remains. Every remaining layer stays a complete grid,
            // which reads as "the pyramid isn't as tall as you asked for" rather
            // than "there's a hole in one of the layers".
            while (layers.Count > 1 && TotalTiles(layers) % matchCount != 0)
            {
                layers.RemoveAt(layers.Count - 1);
            }

            // Strategy 2: if we're down to the single base layer and it still isn't
            // divisible, shrink the top-remaining layer's height first, then width,
            // by whole rows/columns. Base stays visually intact if there was ever
            // more than one layer; if there's only ever been one, we shrink it —
            // still a complete rectangle, just a slightly smaller one.
            while (TotalTiles(layers) % matchCount != 0)
            {
                var idx = layers.Count - 1;
                var l = layers[idx];
                if (l.Height > 1) l.Height--;
                else if (l.Width > 1) l.Width--;
                else if (layers.Count > 1) { layers.RemoveAt(idx); continue; }
                else break;
                layers[idx] = l;
            }

            return layers;
        }

        /// <summary>Fast sum of all layers' tile counts — no allocation.</summary>
        public static int TotalTiles(IReadOnlyList<LayerDefinition> layers)
        {
            var total = 0;
            for (var i = 0; i < layers.Count; i++) total += layers[i].Width * layers[i].Height;
            return total;
        }

        private static List<BoardCoordinate> GeneratePositionsFromLayers(List<LayerDefinition> layers)
        {
            var positions = new List<BoardCoordinate>(64);
            var prevOffset = Vector2Int.zero;
            var prevW = 0;
            var prevH = 0;

            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (layer.Width < 1 || layer.Height < 1) continue;

                Vector2Int offset;
                int w = layer.Width;
                int h = layer.Height;

                if (layerIndex == 0)
                {
                    offset = layer.AutoCenter ? Vector2Int.zero : layer.Offset;
                }
                else
                {
                    var maxW = Mathf.Max(1, prevW - 1);
                    var maxH = Mathf.Max(1, prevH - 1);
                    w = Mathf.Min(w, maxW);
                    h = Mathf.Min(h, maxH);

                    if (layer.AutoCenter)
                    {
                        offset = new Vector2Int(
                            prevOffset.x + (prevW - 1 - w) / 2,
                            prevOffset.y + (prevH - 1 - h) / 2);
                    }
                    else
                    {
                        offset = new Vector2Int(
                            Mathf.Clamp(layer.Offset.x, prevOffset.x, prevOffset.x + prevW - 1 - w),
                            Mathf.Clamp(layer.Offset.y, prevOffset.y, prevOffset.y + prevH - 1 - h));
                    }
                }

                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        positions.Add(new BoardCoordinate(offset.x + x, offset.y + y, layerIndex));
                    }
                }

                prevOffset = offset;
                prevW = w;
                prevH = h;
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
