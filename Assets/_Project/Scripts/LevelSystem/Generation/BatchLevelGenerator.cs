using System;
using LevelSystem.Data;
using UnityEngine;

namespace LevelSystem.Generation
{
    /// <summary>
    /// Serializable difficulty curve used by <see cref="BatchLevelGenerator"/>. Every
    /// axis is a pair (start value at level 0, end value at level Count-1) — the
    /// generator lerps between them per level. Non-integer lerps are rounded, and every
    /// output value is clamped to at least the value below which the game would break
    /// (e.g. MatchCount &gt;= 2, TraySize &gt;= MatchCount). Curve shape controls whether
    /// difficulty ramps evenly (Linear), gently at first then steeply (EaseIn), or the
    /// reverse (EaseOut).
    /// </summary>
    [Serializable]
    public sealed class DifficultyCurve
    {
        public enum Shape { Linear, EaseIn, EaseOut, EaseInOut }

        [Header("Match count (harder as it grows)")]
        [Min(2)] public int StartMatchCount = 3;
        [Min(2)] public int EndMatchCount = 3;

        [Header("Base grid size (layer 0)")]
        [Min(2)] public int StartBaseWidth = 4;
        [Min(2)] public int StartBaseHeight = 4;
        [Min(2)] public int EndBaseWidth = 7;
        [Min(2)] public int EndBaseHeight = 7;

        [Header("Layer count (pyramid depth)")]
        [Min(1)] public int StartLayerCount = 2;
        [Min(1)] public int EndLayerCount = 5;

        [Header("Tray")]
        [Min(1)] public int StartTraySize = 7;
        [Min(1)] public int EndTraySize = 7;

        [Header("Tile faces (variety)")]
        [Tooltip("0 = auto (builder derives from tray & match count)")]
        [Min(0)] public int StartTileTypes = 3;
        [Min(0)] public int EndTileTypes = 6;

        [Header("Curve")]
        public Shape CurveShape = Shape.Linear;
        public LevelPresetSO.PresetShape ShapeAcrossBatch = LevelPresetSO.PresetShape.PyramidShrink;
        [Min(1)] public int ShrinkPerLayer = 1;
        public string ThemeId = "default";
        public bool DeterministicSeed = true;

        public static float Ease(Shape shape, float t)
        {
            t = Mathf.Clamp01(t);
            switch (shape)
            {
                case Shape.EaseIn: return t * t;
                case Shape.EaseOut: return 1f - (1f - t) * (1f - t);
                case Shape.EaseInOut: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default: return t;
            }
        }
    }

    /// <summary>
    /// Fills a <see cref="LevelDefinitionSO"/> in place from a
    /// <see cref="DifficultyCurve"/> and a per-level index. Pure logic — no
    /// AssetDatabase, no Editor calls — so the same routine can be unit-tested or run
    /// at runtime for procedural difficulty tuning if we ever want that (analytics-driven
    /// hot re-tuning of curves without a client build).
    /// </summary>
    public static class BatchLevelGenerator
    {
        public static void Configure(LevelDefinitionSO def, DifficultyCurve curve, int levelIndex, int totalLevels)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            if (totalLevels <= 0) totalLevels = 1;

            var raw = totalLevels == 1 ? 0f : (float)levelIndex / (totalLevels - 1);
            var t = DifficultyCurve.Ease(curve.CurveShape, raw);

            def.LevelId = levelIndex + 1;
            def.DisplayName = $"Level {levelIndex + 1}";
            def.ThemeId = string.IsNullOrEmpty(curve.ThemeId) ? "default" : curve.ThemeId;
            def.Seed = curve.DeterministicSeed ? (levelIndex + 1) * 7919 + 13 : 0;

            def.MatchCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(curve.StartMatchCount, curve.EndMatchCount, t)));
            def.TraySize = Mathf.Max(def.MatchCount, Mathf.RoundToInt(Mathf.Lerp(curve.StartTraySize, curve.EndTraySize, t)));
            def.TileTypeCount = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(curve.StartTileTypes, curve.EndTileTypes, t)));

            var baseW = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(curve.StartBaseWidth, curve.EndBaseWidth, t)));
            var baseH = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(curve.StartBaseHeight, curve.EndBaseHeight, t)));
            var layers = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(curve.StartLayerCount, curve.EndLayerCount, t)));

            // Cap layer count so upper layers never shrink below a 1x1 tile — a pyramid
            // that runs out of room mid-generation just stops at the tallest layer that
            // still fits, so the batch never produces silently-degenerate levels.
            var maxLayersFit = 1 + Mathf.Min((baseW - 1) / curve.ShrinkPerLayer, (baseH - 1) / curve.ShrinkPerLayer);
            layers = Mathf.Clamp(layers, 1, Mathf.Max(1, maxLayersFit));

            // Nudge dimensions to a nearby pyramid whose total tile count is an exact
            // multiple of MatchCount — no partial rows, no notches, no runtime jank
            // (user rule: "complete game feel at any cost"). Prefers to GROW rather
            // than shrink where possible (25 → 27, not 25 → 24).
            if (PyramidSizeSolver.TrySolve(baseW, baseH, layers, curve.ShrinkPerLayer, def.MatchCount,
                    out var fixedW, out var fixedH, out var fixedLayers))
            {
                baseW = fixedW;
                baseH = fixedH;
                layers = fixedLayers;
            }

            var scratch = ScriptableObject.CreateInstance<LevelPresetSO>();
            try
            {
                scratch.Shape = curve.ShapeAcrossBatch;
                scratch.BaseWidth = baseW;
                scratch.BaseHeight = baseH;
                scratch.LayerCount = layers;
                scratch.ShrinkPerLayer = curve.ShrinkPerLayer;
                def.Layers = scratch.BuildLayers();
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(scratch);
                else UnityEngine.Object.DestroyImmediate(scratch);
            }
        }
    }
}
