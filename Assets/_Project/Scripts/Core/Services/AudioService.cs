using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Pooled-AudioSource playback. Uses a single looping music source plus a small
    /// round-robin pool of one-shot sfx sources (so overlapping sounds — e.g. a
    /// staggered multi-tile pop — each get their own AudioSource and don't have their
    /// pitch/volume stomped on by whichever sfx fires next). Takes those AudioSources
    /// in rather than creating its own hidden GameObjects — GameRoot owns them as real,
    /// scene-authored objects (visible and editable in Bootstrap.unity, same as every
    /// other piece of UI in this project), not something conjured invisibly at runtime.
    /// Clip keys resolve against a single Resources-loaded AudioThemeSO; unknown keys or
    /// a missing theme silently no-op rather than throw, since a missing sound should
    /// never be able to break gameplay.
    /// </summary>
    public sealed class AudioService : IAudioService
    {
        private const float MusicVolume = 0.55f;
        private const float SfxVolume = 0.9f;

        private readonly AudioThemeSO _theme;
        private readonly AudioSource _musicSource;
        private readonly AudioSource[] _sfxSources;
        private int _nextSfxSource;

        private bool _masterMuted;
        private bool _musicMuted;
        private bool _sfxMuted;

        public AudioService(AudioSource musicSource, AudioSource[] sfxSources)
        {
            _theme = Resources.Load<AudioThemeSO>("AudioTheme_Default");

            _musicSource = musicSource;
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = MusicVolume;

            _sfxSources = sfxSources;
            foreach (var source in _sfxSources)
            {
                source.playOnAwake = false;
                source.loop = false;
            }
        }

        public void PlaySfx(string clipKey, float volumeScale = 1f, float pitch = 1f)
        {
            if (_masterMuted || _sfxMuted)
            {
                return;
            }

            var clip = ResolveClip(clipKey);
            if (clip == null)
            {
                return;
            }

            var source = _sfxSources[_nextSfxSource];
            _nextSfxSource = (_nextSfxSource + 1) % _sfxSources.Length;

            source.pitch = pitch;
            source.volume = SfxVolume * volumeScale;
            source.clip = clip;
            source.Play();
        }

        public void PlayMusic(string trackKey, bool crossfade = true)
        {
            var clip = ResolveClip(trackKey);
            if (clip == null || _musicSource.clip == clip)
            {
                return;
            }

            // Only one music track exists today, so there's nothing to actually
            // crossfade between yet — a straight swap, but kept behind the same
            // parameter so a real crossfade can drop in later without call-site changes.
            _musicSource.clip = clip;
            _musicSource.volume = _masterMuted || _musicMuted ? 0f : MusicVolume;
            _musicSource.Play();
        }

        public void SetMuted(AudioCategory category, bool muted)
        {
            switch (category)
            {
                case AudioCategory.Master:
                    _masterMuted = muted;
                    _musicSource.mute = muted || _musicMuted;
                    foreach (var source in _sfxSources)
                    {
                        source.mute = muted || _sfxMuted;
                    }
                    break;

                case AudioCategory.Music:
                    _musicMuted = muted;
                    _musicSource.mute = muted || _masterMuted;
                    break;

                case AudioCategory.Sfx:
                case AudioCategory.Ui:
                    _sfxMuted = muted;
                    foreach (var source in _sfxSources)
                    {
                        source.mute = muted || _masterMuted;
                    }
                    break;
            }
        }

        private AudioClip ResolveClip(string key)
        {
            if (_theme == null)
            {
                return null;
            }

            switch (key)
            {
                case "BackgroundBGM": return _theme.BackgroundBGM;
                case "MenuButtonClick": return _theme.MenuButtonClick;
                case "TileSelectSound": return _theme.TileSelectSound;
                case "TilePopSound": return _theme.TilePopSound;
                default: return null;
            }
        }
    }
}
