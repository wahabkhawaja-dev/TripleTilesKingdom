using System;
using System.Collections;
using Domain.Board;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Visual representation of a single tile: a base tile sprite with a fruit/nature
    /// icon layered on top. Matching and popping are driven entirely by the fruit
    /// layer's tile type — the base is purely decorative framing. Lives as a saved
    /// prefab (Assets/Resources/Prefabs/TileView.prefab) so its child Image/Button/
    /// CanvasGroup references are wired once in the asset itself, not re-wired by a
    /// scene-setup script every time.
    /// </summary>
    [RequireComponent(typeof(Button), typeof(CanvasGroup))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private Image _baseImage;
        [SerializeField] private Image _fruitImage;
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _canvasGroup;

        private TileModel _tile;
        private Action<TileId> _onClicked;

        public void Initialize(TileModel tile, Sprite fruitSprite, Action<TileId> onClicked)
        {
            _tile = tile;
            _onClicked = onClicked;
            _fruitImage.sprite = fruitSprite;

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClicked);
            UpdateVisuals();
        }

        private void OnClicked()
        {
            _onClicked?.Invoke(_tile.Id);
            PlayClickAnimation();
        }

        public void UpdateVisuals()
        {
            var covered = _tile.State == TileState.Covered;
            _canvasGroup.alpha = covered ? 0.55f : 1f;
            _canvasGroup.blocksRaycasts = _tile.IsSelectable;
            _button.interactable = _tile.IsSelectable;

            // Covered tiles render in shadow (darker base) to read as "blocked" at a glance.
            _baseImage.color = covered ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        }

        public void PlayClickAnimation()
        {
            transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            _ = StartCoroutine(ScaleToAnimation(Vector3.one, 0.1f));
        }

        public void PlayWinAnimation()
        {
            transform.localScale = Vector3.one * 1.1f;
        }

        public void PlayLoseAnimation()
        {
            transform.localScale = new Vector3(0.9f, 0.9f, 1f);
        }

        private IEnumerator ScaleToAnimation(Vector3 target, float duration)
        {
            var startScale = transform.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, target, elapsed / duration);
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
