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
        /// <summary>
        /// <paramref name="pitch"/> lets call sites vary otherwise-identical repeated
        /// sounds (e.g. a run of tile-pop sfx firing in quick succession) so they don't
        /// sound like the exact same recording stamped out N times in a row.
        /// </summary>
        void PlaySfx(string clipKey, float volumeScale = 1f, float pitch = 1f);
        void PlayMusic(string trackKey, bool crossfade = true);
        void SetMuted(AudioCategory category, bool muted);
    }
}
