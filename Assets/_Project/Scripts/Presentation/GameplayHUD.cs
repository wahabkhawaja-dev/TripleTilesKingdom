using Domain.Levels;
using PrimeTween;
using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Manages the gameplay HUD. Prefers scene-authored elements (built once by
    /// Tools > Build Game Scenes and saved into Gameplay.unity, so the Canvas/panels/
    /// button sprites are hand-editable in the Inspector like any other scene content)
    /// — falls back to procedural construction via UIFactory only if those references
    /// are missing, so the scene never breaks if it hasn't been (re)built yet.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameplayHUD : MonoBehaviour
    {
        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Clickable _pauseButton;
        [SerializeField] private Clickable _restartButton;
        [SerializeField] private Clickable _shuffleButton;
        [SerializeField] private Clickable _hintButton;
        [SerializeField] private Clickable _hammerButton;
        [SerializeField] private Clickable _undoButton;
        [SerializeField] private RectTransform _popupContainer;
        [SerializeField] private PauseMenuPopup _pausePopup;
        [SerializeField] private WinPopup _winPopup;
        [SerializeField] private LosePopup _losePopup;

        [Header("Level-start announcement (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private RectTransform _announcementBanner;
        [SerializeField] private TextMeshProUGUI _announcementText;

        private GameFlowController _gameFlowController;
        private GameObject _currentPopup;
        private Sequence _announcementSequence;

        public void Initialize(GameFlowController gameFlowController, LevelModel level, int levelIndex)
        {
            _gameFlowController = gameFlowController;

            if (_pauseButton == null)
            {
                BuildFallback();
            }

            if (_announcementBanner == null)
            {
                BuildAnnouncementFallback();
            }

            // Popups default to hidden at the start of every level (including restarts)
            // — ShowPauseMenu/ShowGameOverPopup activate whichever one is needed.
            _pausePopup.gameObject.SetActive(false);
            _winPopup.gameObject.SetActive(false);
            _losePopup.gameObject.SetActive(false);
            _currentPopup = null;

            if (_statusText != null)
            {
                _statusText.text = $"Level {levelIndex + 1}   Match {level.MatchCount}   Tray {level.TraySize}";
            }

            _pauseButton.Clicked += OnPauseClicked;
            _restartButton.Clicked += OnRestartClicked;
            if (_shuffleButton != null) _shuffleButton.Clicked += () => _gameFlowController.Shuffle();
            if (_hintButton != null) _hintButton.Clicked += () => _gameFlowController.Hint();
            if (_hammerButton != null) _hammerButton.Clicked += () => _gameFlowController.Hammer();
            if (_undoButton != null) _undoButton.Clicked += () => _gameFlowController.Undo();

            ShowMatchAnnouncement(level.MatchCount);
        }

        /// <summary>Original runtime-construction path, used only when scene-authored refs are missing.</summary>
        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            var statusBar = UIFactory.CreateContainer(transform, "StatusBar");
            statusBar.anchoredPosition = new Vector2(0, 860);
            statusBar.sizeDelta = new Vector2(780, 100);
            var statusImage = statusBar.gameObject.AddComponent<Image>();
            statusImage.color = new Color(0.15f, 0.1f, 0.08f, 0.85f);
            _statusText = UIFactory.CreateText(statusBar, string.Empty, Vector2.zero, new Vector2(740, 76), fontSize: 34);

            _pauseButton = UIFactory.CreateIconButton(transform, "Pause", new Vector2(-460, 860), 120f, theme.PauseIcon2, null);
            _restartButton = UIFactory.CreateIconButton(transform, "Restart", new Vector2(460, 860), 120f, theme.ReplayIcon2, null);

            var powerupY = -780f;
            _shuffleButton = UIFactory.CreateIconButton(transform, "Shuffle", new Vector2(-345, powerupY), 150f, theme.ShuffleIcon2, null);
            _hintButton = UIFactory.CreateIconButton(transform, "Hint", new Vector2(-115, powerupY), 150f, theme.HintIcon2, null);
            _hammerButton = UIFactory.CreateIconButton(transform, "Hammer", new Vector2(115, powerupY), 150f, theme.HammerIcon2, null);
            _undoButton = UIFactory.CreateIconButton(transform, "Undo", new Vector2(345, powerupY), 150f, theme.UndoIcon2, null);

            _popupContainer = UIFactory.CreateFullScreenContainer(transform, "PopupContainer");

            // Popups are pre-existing children — never Instantiated/Destroyed at runtime
            // — so they show up as real, editable objects in the scene Hierarchy instead
            // of only existing while visible during Play. Left active here (their own
            // BuildFallback, invoked next, needs OnEnable to have run for TextMeshPro's
            // font/material to initialize — TMP silently leaves it null on inactive
            // objects); Initialize hides whichever popup isn't currently showing.
            _pausePopup = new GameObject("PausePopup", typeof(RectTransform)).AddComponent<PauseMenuPopup>();
            _pausePopup.transform.SetParent(_popupContainer, false);

            _winPopup = new GameObject("WinPopup", typeof(RectTransform)).AddComponent<WinPopup>();
            _winPopup.transform.SetParent(_popupContainer, false);

            _losePopup = new GameObject("LosePopup", typeof(RectTransform)).AddComponent<LosePopup>();
            _losePopup.transform.SetParent(_popupContainer, false);
        }

        /// <summary>Original runtime-construction path, used only when the scene-authored announcement banner is missing.</summary>
        private void BuildAnnouncementFallback()
        {
            var theme = UIFactory.Theme;

            var banner = UIFactory.CreateContainer(transform, "AnnouncementBanner");
            banner.anchoredPosition = new Vector2(0, 380);
            banner.sizeDelta = new Vector2(680, 170);

            if (theme.RibbonA != null)
            {
                var img = banner.gameObject.AddComponent<Image>();
                img.sprite = theme.RibbonA;
                img.type = Image.Type.Sliced;
            }

            _announcementText = UIFactory.CreateText(banner, string.Empty, Vector2.zero, new Vector2(600, 130), fontSize: 36);
            _announcementBanner = banner;
        }

        /// <summary>
        /// Briefly surfaces the level's match rule as a ribbon banner when the level
        /// starts — levels with a higher match count (4, 5) aren't self-explanatory from
        /// the board alone, and the always-visible status bar's "Match N" text is easy to
        /// miss under time pressure, so a transient, hard-to-miss callout fills that gap
        /// without permanently cluttering the HUD.
        /// </summary>
        private void ShowMatchAnnouncement(int matchCount)
        {
            if (_announcementBanner == null)
            {
                return;
            }

            if (_announcementSequence.isAlive)
            {
                _announcementSequence.Stop();
            }

            _announcementText.text = $"Match {matchCount} Tiles!";

            _announcementBanner.gameObject.SetActive(true);
            _announcementBanner.localScale = Vector3.zero;

            var appear = Tween.Scale(_announcementBanner, 1f, 0.4f, Ease.OutBack);
            _announcementSequence = Sequence.Create(appear)
                .ChainDelay(1.4f)
                .Chain(Tween.Scale(_announcementBanner, 0f, 0.25f, Ease.InBack))
                .OnComplete(() => _announcementBanner.gameObject.SetActive(false));
        }

        private void OnPauseClicked()
        {
            _gameFlowController.Pause();
            ShowPauseMenu();
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }

        private void ShowPauseMenu()
        {
            ClearCurrentPopup();
            _currentPopup = _pausePopup.gameObject;
            _currentPopup.SetActive(true);
            _pausePopup.Initialize(_gameFlowController);
        }

        public void ShowGameOverPopup(bool won)
        {
            ClearCurrentPopup();

            if (won)
            {
                _currentPopup = _winPopup.gameObject;
                _currentPopup.SetActive(true);
                _winPopup.Initialize(_gameFlowController);
            }
            else
            {
                _currentPopup = _losePopup.gameObject;
                _currentPopup.SetActive(true);
                _losePopup.Initialize(_gameFlowController);
            }
        }

        /// <summary>Hides (never destroys) the active popup — every popup is a persistent scene child, reused across shows.</summary>
        private void ClearCurrentPopup()
        {
            if (_currentPopup != null)
            {
                _currentPopup.SetActive(false);
                _currentPopup = null;
            }
        }
    }
}
