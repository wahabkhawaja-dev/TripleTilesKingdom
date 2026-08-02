using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Baseline haptics via Unity's built-in Handheld.Vibrate, which is a single,
    /// fixed-intensity pulse on both platforms — so every HapticStrength currently
    /// produces the same physical vibration. The strength parameter exists now so call
    /// sites (JuiceDirector) never need to change when this is upgraded to a native
    /// plugin with real amplitude control (Android VibrationEffect / iOS Core Haptics).
    /// See DECISIONS.md.
    /// </summary>
    public sealed class HapticsService : IHapticsService
    {
        public bool IsEnabled { get; set; } = true;

        public void Play(HapticStrength strength)
        {
            if (!IsEnabled)
            {
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
