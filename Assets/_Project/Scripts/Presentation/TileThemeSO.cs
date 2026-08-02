using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Maps logical tile types to visual sprites for one theme. Mirrors the ThemeSO
    /// design in ARCHITECTURE.md §12 in miniature — a logical TileTypeId (Domain) has no
    /// idea what it looks like; this asset is what tells Presentation which fruit/nature
    /// icon to draw for type index N. Levels pick a subset of FruitSprites (by index)
    /// so the same theme can present different variety per level without new assets.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/Tile Theme", fileName = "TileTheme")]
    public sealed class TileThemeSO : ScriptableObject
    {
        public Sprite BaseTileSprite;
        public Sprite[] FruitSprites;
        public Sprite TrayBarSprite;
    }
}
