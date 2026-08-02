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
    public sealed class WinPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Clickable _nextButton;
        [SerializeField] private Clickable _restartButton;
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
            // handler on every win.
            _nextButton.Clicked -= OnNextClicked;
            _nextButton.Clicked += OnNextClicked;
            _restartButton.Clicked -= OnRestartClicked;
            _restartButton.Clicked += OnRestartClicked;
            _menuButton.Clicked -= OnMenuClicked;
            _menuButton.Clicked += OnMenuClicked;

            PlayEntranceAnimation(_panel);
            UIParticleBurst.BurstSparkle(transform, _panel.position, count: 20, duration: 0.8f, distance: 280f);
        }

        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));

            _panel = UIFactory.CreateContainer(transform, "Panel");
            _panel.sizeDelta = new Vector2(560, 520);
            var panelBg = _panel.gameObject.AddComponent<Image>();
            panelBg.sprite = theme.PanelPurple;
            panelBg.type = Image.Type.Sliced;

            if (theme.Plaque != null)
            {
                var plaque = UIFactory.CreateContainer(_panel, "Plaque");
                plaque.anchoredPosition = new Vector2(0, 220);
                plaque.sizeDelta = new Vector2(130, 130);
                var img = plaque.gameObject.AddComponent<Image>();
                img.sprite = theme.Plaque;
                img.preserveAspect = true;
            }

            UIFactory.CreateText(_panel, "You Win!", new Vector2(0, 170), new Vector2(400, 100), fontSize: 48);
            _nextButton = UIFactory.CreateSpriteButton(_panel, "Next", new Vector2(0, 20), new Vector2(320, 100), theme.ButtonGreen, null);
            _restartButton = UIFactory.CreateSpriteButton(_panel, "Restart", new Vector2(0, -100), new Vector2(320, 100), theme.ButtonOrange, null);
            _menuButton = UIFactory.CreateSpriteButton(_panel, "Menu", new Vector2(0, -220), new Vector2(320, 100), theme.ButtonOrange, null);
        }

        private static void PlayEntranceAnimation(RectTransform panel)
        {
            panel.localScale = Vector3.zero;
            Tween.Scale(panel, 1f, 0.35f, Ease.OutBack, useUnscaledTime: true);
        }

        private void OnNextClicked()
        {
            _gameFlowController.NextLevel();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }
    }
}
