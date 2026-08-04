using System.Collections.Generic;
using UnityEngine;

namespace LevelSystem.Data
{
    /// <summary>
    /// Reusable preset that stamps a set of <see cref="LayerDefinition"/>s onto a
    /// <see cref="LevelDefinitionSO"/>. Designers create these once ("Small pyramid",
    /// "Big tower", "Diamond") and reuse them across many levels via the Level
    /// Designer window. Levels can override individual layers after applying a preset.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/Level Preset", fileName = "LevelPreset")]
    public sealed class LevelPresetSO : ScriptableObject
    {
        public enum PresetShape
        {
            /// <summary>Classic pyramid — each layer shrinks by <see cref="ShrinkPerLayer"/> on width and height, up to <see cref="LayerCount"/> layers.</summary>
            PyramidShrink,
            /// <summary>Flat tower — every layer is the same Base size. Rare, but useful for pure-stack levels.</summary>
            TowerFlat,
            /// <summary>Two pyramids side-by-side sharing a base. Base = BaseWidth × BaseHeight, each half shrinks independently.</summary>
            DoublePyramid,
            /// <summary>Fully custom — <see cref="CustomLayers"/> is used verbatim.</summary>
            Custom,
        }

        public PresetShape Shape = PresetShape.PyramidShrink;

        [Header("Shape parameters (used by non-Custom shapes)")]
        [Min(1)] public int BaseWidth = 5;
        [Min(1)] public int BaseHeight = 5;
        [Min(1)] public int LayerCount = 3;
        [Min(1)] public int ShrinkPerLayer = 1;

        [Header("Custom shape")]
        public List<LayerDefinition> CustomLayers = new List<LayerDefinition>();

        /// <summary>Materializes this preset into a flat list of layer definitions.</summary>
        public List<LayerDefinition> BuildLayers()
        {
            var layers = new List<LayerDefinition>();
            switch (Shape)
            {
                case PresetShape.PyramidShrink:
                {
                    var w = BaseWidth;
                    var h = BaseHeight;
                    for (var l = 0; l < LayerCount; l++)
                    {
                        if (w < 1 || h < 1) break;
                        layers.Add(LayerDefinition.Rect(w, h));
                        w -= ShrinkPerLayer;
                        h -= ShrinkPerLayer;
                    }
                    break;
                }
                case PresetShape.TowerFlat:
                {
                    for (var l = 0; l < LayerCount; l++)
                    {
                        layers.Add(LayerDefinition.Rect(BaseWidth, BaseHeight));
                    }
                    break;
                }
                case PresetShape.DoublePyramid:
                {
                    // Base is one big row; each subsequent layer splits into two pyramids
                    // shrinking independently. Handled by placing two layer entries per
                    // level with explicit offsets — AutoCenter off.
                    layers.Add(LayerDefinition.Rect(BaseWidth, BaseHeight));
                    var halfW = Mathf.Max(1, BaseWidth / 2 - 1);
                    var h = BaseHeight - ShrinkPerLayer;
                    var xLeft = 0;
                    var xRight = BaseWidth - halfW;
                    for (var l = 1; l < LayerCount && halfW >= 1 && h >= 1; l++)
                    {
                        layers.Add(new LayerDefinition { Width = halfW, Height = h, Offset = new Vector2Int(xLeft, (BaseHeight - h) / 2), AutoCenter = false });
                        layers.Add(new LayerDefinition { Width = halfW, Height = h, Offset = new Vector2Int(xRight, (BaseHeight - h) / 2), AutoCenter = false });
                        halfW -= ShrinkPerLayer;
                        h -= ShrinkPerLayer;
                        xLeft += ShrinkPerLayer;
                        xRight -= ShrinkPerLayer;
                    }
                    break;
                }
                case PresetShape.Custom:
                {
                    layers.AddRange(CustomLayers);
                    break;
                }
            }
            return layers;
        }
    }
}
