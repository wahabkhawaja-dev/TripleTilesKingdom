using System;
using Core.Services;
using Domain.Board;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Visual representation of a single tile: a base tile sprite with a fruit/nature
    /// icon layered on top. Matching and popping are driven entirely by the fruit
    /// layer's tile type — the base is purely decorative framing. Lives as a saved
    /// prefab (Assets/Resources/Prefabs/TileView.prefab) so its child Image/Clickable/
    /// CanvasGroup references are wired once in the asset itself, not re-wired by a
    /// scene-setup script every time.
    /// </summary>
    [RequireComponent(typeof(Clickable), typeof(CanvasGroup))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private Image _baseImage;
        [SerializeField] private Image _fruitImage;
        [SerializeField] private Clickable _clickable;
        [SerializeField] private CanvasGroup _canvasGroup;

        private TileModel _tile;
        private Action<TileId> _onClicked;
        private RectTransform _rect;
        private Sequence _flySequence;
        private Tween _hintTween;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void Initialize(TileModel tile, Sprite fruitSprite, Action<TileId> onClicked)
        {
            _tile = tile;
            _onClicked = onClicked;
            _fruitImage.sprite = fruitSprite;

            _clickable.Clicked -= OnClicked;
            _clickable.Clicked += OnClicked;
            UpdateVisuals();
        }

        private void OnClicked()
        {
            _onClicked?.Invoke(_tile.Id);
            PlayClickAnimation();
            UIParticleBurst.BurstSparkle(transform, transform.position, count: 5, duration: 0.3f, distance: 40f);
            if (GameServices.IsRegistered)
            {
                GameServices.Haptics.Play(HapticStrength.Light);
                GameServices.Audio.PlaySfx("TileSelectSound");
            }
        }

        public void UpdateVisuals()
        {
            var covered = _tile.State == TileState.Covered;
            _clickable.Interactable = _tile.IsSelectable;
            _canvasGroup.blocksRaycasts = _tile.IsSelectable;

            // Covered tiles hide their fruit entirely and render as a plain dark socket —
            // a clean "you can't see what's under there yet" read instead of a washed-out
            // preview of the fruit, which reads as clutter on a busy pyramid board.
            _fruitImage.enabled = !covered;
            _baseImage.color = covered ? new Color(0.35f, 0.32f, 0.3f) : Color.white;
            _canvasGroup.alpha = 1f;
        }

        public void PlayClickAnimation()
        {
            // Squash-and-stretch punch reads as a physical "press" instead of a flat UI click.
            Tween.PunchScale(transform, new Vector3(0.22f, -0.18f, 0f), 0.25f, frequency: 10);
        }

        /// <summary>
        /// Animates this tile flying from its board position to the tray along a simple
        /// two-segment arc (up-and-over, then down into the tray) with a bit of spin and
        /// an in-back squash on arrival, then invokes <paramref name="onComplete"/> — the
        /// caller (BoardController) is responsible for destroying the GameObject there so
        /// the removal from bookkeeping and the visual disappearance stay in sync.
        /// </summary>
        public void PlayFlyToTrayAndDestroy(Vector2 targetAnchoredPosition, Action onComplete)
        {
            _clickable.Interactable = false;
            _canvasGroup.blocksRaycasts = false;

            UIParticleBurst.BurstSparkle(transform, transform.position, count: 4, duration: 0.25f, distance: 25f);

            var startPos = _rect.anchoredPosition;
            var arcPeak = Vector2.Lerp(startPos, targetAnchoredPosition, 0.5f) + Vector2.up * 110f;

            var rise = Tween.UIAnchoredPosition(_rect, arcPeak, 0.16f, Ease.OutSine);
            _flySequence = Sequence.Create(rise)
                .Chain(Tween.UIAnchoredPosition(_rect, targetAnchoredPosition, 0.2f, Ease.InSine));

            var spin = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            Tween.LocalEulerAngles(transform, Vector3.zero, new Vector3(0f, 0f, 30f * spin), 0.36f, Ease.OutQuad);
            Tween.Scale(transform, 0.3f, 0.36f, Ease.InBack);
            Tween.Alpha(_canvasGroup, 0f, 0.2f, Ease.InQuad, startDelay: 0.16f)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void PlayWinAnimation()
        {
            Tween.Scale(transform, 1.1f, 0.35f, Ease.OutBack, cycles: 6, cycleMode: CycleMode.Yoyo);
        }

        public void PlayLoseAnimation()
        {
            Tween.PunchScale(transform, new Vector3(0.08f, 0.08f, 0f), 0.4f);
        }

        public void PlayHintPulse()
        {
            // Repeated Hint taps often re-target the same tile (it's usually still the
            // first selectable one) — starting a second Yoyo scale tween on top of one
            // already mid-cycle left the two fighting over localScale, and whichever
            // stopped first (interrupted by yet another tap) abandoned the tile at
            // whatever scale it happened to be mid-pulse, not back at 1. Stopping the
            // old one and resetting to identity scale first guarantees every tap starts
            // from a clean, known state.
            if (_hintTween.isAlive)
            {
                _hintTween.Stop();
            }
            transform.localScale = Vector3.one;
            _hintTween = Tween.Scale(transform, 1.15f, 0.25f, Ease.InOutSine, cycles: 4, cycleMode: CycleMode.Yoyo);
        }

        /// <summary>
        /// Stops every animation currently running on this tile before it gets
        /// destroyed out from under them (e.g. a Shuffle/Undo rebuild interrupting a
        /// fly-to-tray mid-flight). The Sequence used by PlayFlyToTrayAndDestroy must be
        /// stopped as a Sequence — PrimeTween disallows stopping one of its nested
        /// tweens directly — everything else is stopped via a plain StopAll.
        /// </summary>
        public void CancelAllAnimations()
        {
            if (_flySequence.isAlive)
            {
                _flySequence.Stop();
            }
            Tween.StopAll(transform);
            Tween.StopAll(_canvasGroup);
        }
    }
}
