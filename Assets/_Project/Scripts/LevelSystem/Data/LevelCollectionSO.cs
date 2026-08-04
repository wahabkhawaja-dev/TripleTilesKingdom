using System.Collections.Generic;
using UnityEngine;

namespace LevelSystem.Data
{
    /// <summary>
    /// Ordered list of <see cref="LevelDefinitionSO"/> assets loaded at boot. Place
    /// the built asset at <c>Assets/Resources/Levels/LevelCollection.asset</c> and the
    /// game will pick it up automatically (GameFlowController.LoadCollection). If the
    /// asset is missing, gameplay falls back to the procedural LevelGenerator so the
    /// project never breaks — but shipped builds should always ship a curated collection.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/Level Collection", fileName = "LevelCollection")]
    public sealed class LevelCollectionSO : ScriptableObject
    {
        public List<LevelDefinitionSO> Levels = new List<LevelDefinitionSO>();

        public int Count => Levels != null ? Levels.Count : 0;

        public LevelDefinitionSO Get(int index)
        {
            if (Levels == null || Levels.Count == 0) return null;
            var i = ((index % Levels.Count) + Levels.Count) % Levels.Count;
            return Levels[i];
        }
    }
}
