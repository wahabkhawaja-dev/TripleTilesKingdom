using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Baseline haptics via Unity's built-in Handheld.Vibrate, which is a single,
    /// fixed-intensity pulse on both platforms with no amplitude control — so a
    /// "softer" feel can't come from a quieter buzz, only from firing less often. Two
    /// steps toward that: Light-strength calls (routine button taps) are dropped
    /// entirely, reserving physical vibration for tile selection and match completion;
    /// and a cooldown prevents back-to-back calls (e.g. the tray's staggered 3-tile pop
    /// sequence) from stacking into one continuous harsh buzz instead of a few distinct
    /// gentle taps. The strength parameter exists now so call sites never need to
    /// change when this is upgraded to a native plugin with real amplitude control
    /// (Android VibrationEffect / iOS Core Haptics). See DECISIONS.md.
    /// </summary>
    public sealed class HapticsService : IHapticsService
    {
        private const float MinIntervalSeconds = 0.15f;

        public bool IsEnabled { get; set; } = true;

        private float _lastPlayTimeUnscaled = float.NegativeInfinity;

        public void Play(HapticStrength strength)
        {
            if (!IsEnabled || strength == HapticStrength.Light)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now - _lastPlayTimeUnscaled < MinIntervalSeconds)
            {
                return;
            }
            _lastPlayTimeUnscaled = now;

#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
