using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// A persistent, pre-existing scene child (see GameplayHUD) that's shown/hidden via
    /// SetActive — never Instantiated or Destroyed — so Initialize can run more than
    /// once on the same instance across a play session.
    /// </summary>
    public sealed class PauseMenuPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Clickable _resumeButton;
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
            // handler on every pause.
            _resumeButton.Clicked -= OnResumeClicked;
            _resumeButton.Clicked += OnResumeClicked;
            _restartButton.Clicked -= OnRestartClicked;
            _restartButton.Clicked += OnRestartClicked;
            _menuButton.Clicked -= OnMenuClicked;
            _menuButton.Clicked += OnMenuClicked;

            PlayEntranceAnimation(_panel);
        }

        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));

            _panel = UIFactory.CreateContainer(transform, "Panel");
            _panel.sizeDelta = new Vector2(520, 500);
            var panelBg = _panel.gameObject.AddComponent<Image>();
            panelBg.sprite = theme.PanelPurple;
            panelBg.type = Image.Type.Sliced;

            UIFactory.CreateText(_panel, "Paused", new Vector2(0, 180), new Vector2(400, 100), fontSize: 48);
            _resumeButton = UIFactory.CreateSpriteButton(_panel, "Resume", new Vector2(0, 40), new Vector2(320, 100), theme.ButtonGreen, null);
            _restartButton = UIFactory.CreateSpriteButton(_panel, "Restart", new Vector2(0, -80), new Vector2(320, 100), theme.ButtonOrange, null);
            _menuButton = UIFactory.CreateSpriteButton(_panel, "Menu", new Vector2(0, -200), new Vector2(320, 100), theme.ButtonOrange, null);
        }

        private static void PlayEntranceAnimation(RectTransform panel)
        {
            // Unscaled time: this popup shows while Time.timeScale == 0 (paused), so a
            // scaled-time tween would freeze at scale 0 and the popup would never appear.
            panel.localScale = Vector3.zero;
            Tween.Scale(panel, 1f, 0.3f, Ease.OutBack, useUnscaledTime: true);
        }

        private void OnResumeClicked()
        {
            _gameFlowController.Resume();
            gameObject.SetActive(false);
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }
    }
}
