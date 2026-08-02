using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Maps AudioService's string clip/track keys to actual AudioClip assets — the same
    /// "logical key, Resources-loaded data asset" pattern as TileThemeSO/UIThemeSO, kept
    /// here (rather than in Presentation) since it's just clip data with no
    /// Presentation-layer dependency, and AudioService (Core) is what resolves it.
    /// </summary>
    [CreateAssetMenu(menuName = "TripleTilesKingdom/Audio Theme", fileName = "AudioTheme")]
    public sealed class AudioThemeSO : ScriptableObject
    {
        public AudioClip BackgroundBGM;
        public AudioClip MenuButtonClick;
        public AudioClip TileSelectSound;
        public AudioClip TilePopSound;
    }
}
