#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Domain.Board;
using LevelSystem.Data;
using LevelSystem.Generation;
using UnityEditor;
using UnityEngine;

namespace LevelSystem.EditorTools
{
    /// <summary>
    /// Shared IMGUI renderer for a <see cref="LevelDefinitionSO"/>. Draws a
    /// top-down preview of the effective (auto-adjusted) layer set — the exact
    /// coordinates the runtime will produce — so what the designer sees in the
    /// inspector is what plays. Also reports whether the builder had to drop
    /// layers or shrink the top layer to keep the total divisible by MatchCount,
    /// so the change is never invisible.
    /// </summary>
    internal static class LevelPreviewDrawer
    {
        private const int MaxPreviewSize = 320;
        private const int MinCellPixels = 6;

        public static void Draw(LevelDefinitionSO def)
        {
            if (def == null || def.Layers == null || def.Layers.Count == 0)
            {
                EditorGUILayout.HelpBox("No layers to preview.", MessageType.Info);
                return;
            }

            var matchCount = Mathf.Max(2, def.MatchCount);
            var effective = LevelDefinitionBuilder.SolveCompleteGridLayers(def.Layers, matchCount);
            var positions = LevelDefinitionBuilder.GeneratePositions(def);
            if (positions.Count == 0)
            {
                EditorGUILayout.HelpBox("No positions produced.", MessageType.Warning);
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue, maxLayer = 0;
            foreach (var p in positions)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Layer > maxLayer) maxLayer = p.Layer;
            }

            int cols = maxX - minX + 1;
            int rows = maxY - minY + 1;
            int cell = Mathf.Max(MinCellPixels, Mathf.Min(MaxPreviewSize / Mathf.Max(cols, rows), 48));

            var rect = GUILayoutUtility.GetRect(cols * cell + 12, rows * cell + 12);
            var origin = new Vector2(rect.x + 6, rect.y + 6);

            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.16f));

            var byLayer = new Dictionary<int, List<BoardCoordinate>>();
            foreach (var p in positions)
            {
                if (!byLayer.TryGetValue(p.Layer, out var list))
                {
                    list = new List<BoardCoordinate>();
                    byLayer[p.Layer] = list;
                }
                list.Add(p);
            }

            for (var layer = 0; layer <= maxLayer; layer++)
            {
                if (!byLayer.TryGetValue(layer, out var list)) continue;
                var tint = LayerColor(layer, maxLayer);
                var inset = Mathf.Clamp(layer * (cell / 8f), 0f, cell * 0.4f);
                foreach (var p in list)
                {
                    var cx = origin.x + (p.X - minX) * cell + inset;
                    var cy = origin.y + (rows - 1 - (p.Y - minY)) * cell + inset;
                    var size = cell - inset * 2f - 1f;
                    EditorGUI.DrawRect(new Rect(cx, cy, size, size), tint);
                }
            }

            var effectiveTotal = LevelDefinitionBuilder.TotalTiles(effective);
            EditorGUILayout.LabelField(
                $"Effective: base {effective[0].Width}×{effective[0].Height} · {effective.Count} layers · {effectiveTotal} tiles (÷{matchCount} = {effectiveTotal / matchCount} groups)",
                EditorStyles.miniLabel);
        }

        public static void DrawValidation(LevelDefinitionSO def)
        {
            var messages = new List<(string text, MessageType level)>();
            var matchCount = Mathf.Max(2, def.MatchCount);

            if (def.Layers == null || def.Layers.Count == 0)
            {
                messages.Add(("No layers defined.", MessageType.Warning));
            }
            else
            {
                var rawTotal = LevelDefinitionBuilder.TotalTiles(def.Layers);
                var effective = LevelDefinitionBuilder.SolveCompleteGridLayers(def.Layers, matchCount);
                var effectiveTotal = LevelDefinitionBuilder.TotalTiles(effective);

                if (rawTotal != effectiveTotal || effective.Count != def.Layers.Count)
                {
                    messages.Add((
                        $"Auto-adjusted for complete grids: authored {rawTotal} tiles / {def.Layers.Count} layers → effective {effectiveTotal} tiles / {effective.Count} layers. Click 'Auto-fix authored layers' to bake this change into the asset.",
                        MessageType.Info));
                }
                else
                {
                    messages.Add(("Layers already divide evenly — nothing to fix.", MessageType.Info));
                }

                var raw = def.Layers;
                for (var i = 1; i < raw.Count; i++)
                {
                    var prev = raw[i - 1];
                    var cur = raw[i];
                    if (cur.Width > prev.Width - 1 || cur.Height > prev.Height - 1)
                    {
                        messages.Add((
                            $"Layer {i} ({cur.Width}×{cur.Height}) doesn't fit inside layer {i - 1} ({prev.Width}×{prev.Height}); it will be clamped at build time.",
                            MessageType.Warning));
                    }
                }
            }

            if (def.MatchCount < 2) messages.Add(("MatchCount must be ≥ 2.", MessageType.Error));
            if (def.TraySize < def.MatchCount) messages.Add(("TraySize should be ≥ MatchCount.", MessageType.Warning));

            foreach (var m in messages) EditorGUILayout.HelpBox(m.text, m.level);
        }

        public static string DescribeEffective(LevelDefinitionSO def)
        {
            var matchCount = Mathf.Max(2, def.MatchCount);
            var effective = LevelDefinitionBuilder.SolveCompleteGridLayers(def.Layers, matchCount);
            var sb = new StringBuilder();
            for (var i = 0; i < effective.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(effective[i].Width).Append('×').Append(effective[i].Height);
            }
            return sb.ToString();
        }

        private static Color LayerColor(int layer, int maxLayer)
        {
            var t = maxLayer == 0 ? 0f : (float)layer / maxLayer;
            return Color.Lerp(new Color(0.30f, 0.55f, 0.90f), new Color(0.98f, 0.76f, 0.25f), t);
        }
    }
}
#endif
