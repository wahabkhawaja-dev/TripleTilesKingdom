using System.Collections.Generic;
using UnityEngine;

namespace LevelSystem.Data
{
    /// <summary>
    /// Data-only definition of a single level. One asset per level, dropped into a
    /// <see cref="LevelCollectionSO"/> that the game loads at boot. Every field is
    /// designer-tunable in the Inspector or via the Level Designer window
    /// (Tools ▸ Level Designer). This is the concrete implementation of the
    /// <c>LevelDefinitionSO</c> called for in ARCHITECTURE.md §7.1.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/Level Definition", fileName = "Level_")]
    public sealed class LevelDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public int LevelId = 1;
        public string DisplayName;

        [Header("Rules")]
        [Min(2)] public int MatchCount = 3;
        [Min(1)] public int TraySize = 7;

        [Header("Content")]
        [Tooltip("Number of distinct tile faces on the board. 0 = auto (derived from tray & match count).")]
        [Min(0)] public int TileTypeCount = 0;

        [Tooltip("Deterministic seed for tile shuffle. 0 = derive from LevelId.")]
        public int Seed = 0;

        public string ThemeId = "default";

        [Header("Layers (base first, top last)")]
        [Tooltip("Each layer is a full Width x Height grid. Higher layers must fit inside the layer below (w <= W-1, h <= H-1). Only the topmost tiles are initially playable.")]
        public List<LayerDefinition> Layers = new List<LayerDefinition>
        {
            LayerDefinition.Rect(5, 5),
            LayerDefinition.Rect(4, 4),
            LayerDefinition.Rect(3, 3),
        };

        [Header("Editor helpers")]
        [Tooltip("Optional preset. Apply from the inspector's 'Apply Preset' button to stamp its layers into this level.")]
        public LevelPresetSO PresetTemplate;

        public int TotalTileCount
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Layers.Count; i++) total += Layers[i].TileCount;
                return total;
            }
        }
    }
}
