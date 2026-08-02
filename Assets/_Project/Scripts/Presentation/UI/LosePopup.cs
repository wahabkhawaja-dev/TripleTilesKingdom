using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// A persistent, pre-existing scene child (see GameplayHUD) that's shown/hidden via
    /// SetActive — never Instantiated or Destroyed — so Initialize can run more than
    /// once on the same instance across a play session.
    /// </summary>
    public sealed class LosePopup : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Clickable _retryButton;
        [SerializeField] private Clickable _menuButton;

        private GameFlowController _gameFlowController;

        public void Initialize(GameFlowController gameFlowController)
        {
            _gameFlowController = gameFlowController;

            if (_panel == null)
            {
                BuildFallback();
            }

            // Unsubscribe-then-subscribe: this instance is reused every time the popup
            // shows (not re-Instantiated), so a plain += here would stack a duplicate
            // handler on every loss.
            _retryButton.Clicked -= OnRetryClicked;
            _retryButton.Clicked += OnRetryClicked;
            _menuButton.Clicked -= OnMenuClicked;
            _menuButton.Clicked += OnMenuClicked;

            PlayEntranceAnimation(_panel);
            UIParticleBurst.BurstSparkle(transform, _panel.position, count: 8, duration: 0.4f, distance: 100f);
        }

        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));

            _panel = UIFactory.CreateContainer(transform, "Panel");
            _panel.sizeDelta = new Vector2(540, 400);
            var panelBg = _panel.gameObject.AddComponent<Image>();
            panelBg.sprite = theme.PanelPurple;
            panelBg.type = Image.Type.Sliced;

            UIFactory.CreateText(_panel, "Level Failed", new Vector2(0, 130), new Vector2(400, 100), fontSize: 48);
            _retryButton = UIFactory.CreateSpriteButton(_panel, "Retry", new Vector2(0, 0), new Vector2(320, 100), theme.ButtonGreen, null);
            _menuButton = UIFactory.CreateSpriteButton(_panel, "Menu", new Vector2(0, -120), new Vector2(320, 100), theme.ButtonOrange, null);
        }

        private static void PlayEntranceAnimation(RectTransform panel)
        {
            panel.localScale = Vector3.zero;
            Tween.Scale(panel, 1f, 0.3f, Ease.OutBack, useUnscaledTime: true);
        }

        private void OnRetryClicked()
        {
            _gameFlowController.Restart();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }
    }
}
