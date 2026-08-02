namespace Core.Services
{
    public enum AudioCategory
    {
        Master,
        Sfx,
        Music,
        Ui
    }

    /// <summary>
    /// Pooled-AudioSource playback service, keyed by Addressables key rather than a
    /// direct AudioClip reference so themes can swap SFX/music independently of code.
    /// See ARCHITECTURE.md §11.
    /// </summary>
    public interface IAudioService
    {
        void PlaySfx(string clipKey, float volumeScale = 1f);
        void PlayMusic(string trackKey, bool crossfade = true);
        void SetMuted(AudioCategory category, bool muted);
    }
}
