using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Unity's built-in Handheld.Vibrate is a single, fixed-intensity, fixed-duration
    /// pulse with zero amplitude control — every HapticStrength produced the exact same
    /// physical buzz through it, which is why tile taps and match pops felt identical
    /// and both read as "too strong." On Android, this bypasses Handheld.Vibrate
    /// entirely and drives android.os.Vibrator/VibrationEffect directly (via
    /// AndroidJavaObject — no native plugin needed), which DOES support real
    /// duration/amplitude — so Light (tile tap) is genuinely short and soft, Heavy
    /// (match pop) is longer and stronger, and they're actually distinguishable. iOS has
    /// no equivalent built-in API without a native plugin, so it falls back to
    /// Handheld.Vibrate — reserved for Heavy only, since that single fixed buzz is too
    /// strong to fire on every routine tile tap; Light/Medium are silently skipped there
    /// rather than spam the one blunt vibration Unity can give us on that platform.
    /// </summary>
    public sealed class HapticsService : IHapticsService
    {
        private const float MinIntervalSeconds = 0.12f;

        public bool IsEnabled { get; set; } = true;

        private float _lastPlayTimeUnscaled = float.NegativeInfinity;

        public void Play(HapticStrength strength)
        {
            if (!IsEnabled)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now - _lastPlayTimeUnscaled < MinIntervalSeconds)
            {
                return;
            }
            _lastPlayTimeUnscaled = now;

#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroidVibration(strength);
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength == HapticStrength.Heavy)
            {
                Handheld.Vibrate();
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Duration in ms, amplitude 1-255 (Android's own scale) — tuned so Light barely taps and Heavy is clearly the bigger event, not just "the same buzz again."</summary>
        private static void PlayAndroidVibration(HapticStrength strength)
        {
            int durationMs;
            int amplitude;
            switch (strength)
            {
                case HapticStrength.Light:
                    durationMs = 12;
                    amplitude = 30;
                    break;
                case HapticStrength.Heavy:
                    durationMs = 40;
                    amplitude = 120;
                    break;
                default: // Medium
                    durationMs = 20;
                    amplitude = 60;
                    break;
            }

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    return;
                }

                using var versionClass = new AndroidJavaClass("android.os.Build$VERSION");
                var sdkInt = versionClass.GetStatic<int>("SDK_INT");

                if (sdkInt >= 26)
                {
                    using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)durationMs, amplitude);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", (long)durationMs);
                }
            }
            catch
            {
                // A haptics failure (missing service, odd OEM vibrator implementation,
                // etc.) must never be able to break or crash gameplay.
            }
        }
#endif
    }
}
