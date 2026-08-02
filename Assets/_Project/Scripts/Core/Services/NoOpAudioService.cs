namespace Core.Services
{
    /// <summary>
    /// Placeholder IAudioService wired by GameRoot until the real pooled-AudioSource
    /// implementation lands as part of the Juice pass (build order step 6,
    /// ARCHITECTURE.md §11). Silent no-op so gameplay/UI code can call PlaySfx/PlayMusic
    /// today without null checks, and get real sound the moment the real service is
    /// swapped in — no call-site changes required.
    /// </summary>
    public sealed class NoOpAudioService : IAudioService
    {
        public void PlaySfx(string clipKey, float volumeScale = 1f, float pitch = 1f)
        {
        }

        public void PlayMusic(string trackKey, bool crossfade = true)
        {
        }

        public void SetMuted(AudioCategory category, bool muted)
        {
        }
    }
}
