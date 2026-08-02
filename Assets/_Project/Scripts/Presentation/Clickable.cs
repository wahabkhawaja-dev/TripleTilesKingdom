using System;
using Core.Services;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Presentation
{
    /// <summary>
    /// Minimal click target: raises OnClick via the UI event system, plus a built-in
    /// press-down/release scale bounce. Used in place of UnityEngine.UI.Button
    /// everywhere in this project — Button inherits Selectable, which by default
    /// applies its own ColorTint transition (tinting the target Graphic on
    /// hover/press/disabled) and a distinct "disabledColor" when Interactable is false.
    /// Those run independently of and fight with our own PrimeTween-driven visuals
    /// (covered-tile darkening, punch scale, fly animations) — the two color/scale
    /// systems stomp on each other every frame, which is what made tiles and buttons
    /// feel stiff/inconsistent. Clickable only ever does what we explicitly tell it to.
    ///
    /// The press-juice lives here, in the component itself, rather than being wired
    /// externally (e.g. by whatever code constructs the button) — C# event
    /// subscriptions aren't part of Unity's scene serialization, so a subscription
    /// added only at scene-build time would silently vanish the moment the saved scene
    /// loads fresh in Play mode. Baking it into Awake/OnPointerDown/OnPointerUp means it
    /// always works, regardless of whether this GameObject was scene-authored or built
    /// at runtime.
    /// </summary>
    public sealed class Clickable : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public bool Interactable = true;

        /// <summary>
        /// True for every generic UI button; false on TileView's Clickable, which plays
        /// its own distinct click animation and sound instead — gates both the built-in
        /// press-scale juice and the generic menu click sfx below, since both are things
        /// a tile deliberately handles itself rather than inheriting.
        /// </summary>
        [SerializeField] private bool _pressJuice = true;

        public event Action Clicked;
        public event Action PressedDown;
        public event Action PressedUp;

        private Vector3 _restScale;

        private void Awake()
        {
            _restScale = transform.localScale;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable)
            {
                Clicked?.Invoke();
                if (_pressJuice && GameServices.IsRegistered)
                {
                    GameServices.Audio.PlaySfx("MenuButtonClick");
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable)
            {
                PressedDown?.Invoke();
                if (_pressJuice)
                {
                    Tween.Scale(transform, _restScale * 0.9f, 0.08f, Ease.OutQuad, useUnscaledTime: true);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            PressedUp?.Invoke();
            if (_pressJuice)
            {
                Tween.Scale(transform, _restScale, 0.22f, Ease.OutBack, useUnscaledTime: true);
            }
        }
    }
}
