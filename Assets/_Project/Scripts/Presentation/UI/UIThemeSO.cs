using UnityEngine;

namespace Presentation.UI
{
    /// <summary>
    /// Sprite set for chrome UI (buttons, bars, decoration) sourced from the mockup's
    /// UI sprite sheet. Mirrors TileThemeSO's role for gameplay tiles — a single asset
    /// every UI script pulls from via Resources.Load, so reskinning is a data change,
    /// not a code change.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/UI Theme", fileName = "UITheme")]
    public sealed class UIThemeSO : ScriptableObject
    {
        public Sprite ButtonGreen;
        public Sprite ButtonOrange;
        public Sprite StatusBar;
        public Sprite IconPlay;
        public Sprite IconPause;
        public Sprite ArrowBack;
        public Sprite ArrowForward;
        public Sprite IconSettings;
        public Sprite IconShuffle;
        public Sprite IconHint;
        public Sprite IconUndo;
        public Sprite Ribbon;
        public Sprite Plaque;
        public Sprite NavHome;
        public Sprite NavShop;
        public Sprite NavPeople;
        public Sprite NavSettings;

        // Sheet 2 — clearly labeled icon buttons and panels, preferred over Sheet 1
        // equivalents where both exist.
        public Sprite PauseIcon2;
        public Sprite ResumeIcon2;
        public Sprite ReplayIcon2;
        public Sprite ShuffleIcon2;
        public Sprite HintIcon2;
        public Sprite HammerIcon2;
        public Sprite UndoIcon2;
        public Sprite AddSlotIcon2;
        public Sprite CancelIcon2;
        public Sprite ConfirmIcon2;
        public Sprite PanelCream;
        public Sprite PanelPurple;
        public Sprite RibbonA;
        public Sprite RibbonB;
        public Sprite BarHeart;
        public Sprite BarStar;
        public Sprite ChestBronze;
        public Sprite ChestSilver;
        public Sprite ChestGold;
        public Sprite ChestPurple;
    }
}
