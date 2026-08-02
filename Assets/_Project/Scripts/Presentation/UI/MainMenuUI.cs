using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Main Menu UI controller. Prefers a scene-authored Canvas/panels/buttons (built
    /// once by Tools > Build Game Scenes and saved into MainMenu.unity — hand-editable
    /// in the Inspector like any other scene content) — falls back to procedural
    /// construction via UIFactory only if those references are missing, so the scene
    /// never breaks if it hasn't been (re)built yet.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private Clickable _playButton;
        [SerializeField] private Clickable _settingsButton;
        [SerializeField] private Clickable _quitButton;
        [SerializeField] private TextMeshProUGUI _coinText;

        private void Awake()
        {
            if (_playButton == null)
            {
                BuildFallback();
            }

            _playButton.Clicked += OnPlayClicked;
            _settingsButton.Clicked += OnSettingsClicked;
            _quitButton.Clicked += OnQuitClicked;

            UpdateCoinDisplay();
        }

        /// <summary>Original runtime-construction path, used only when scene-authored refs are missing.</summary>
        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.10f, 0.12f, 0.22f));

            if (theme.RibbonB != null)
            {
                var ribbon = UIFactory.CreateContainer(canvas.transform, "TitleRibbon");
                ribbon.anchoredPosition = new Vector2(0, 480);
                ribbon.sizeDelta = new Vector2(760, 230);
                var ribbonImg = ribbon.gameObject.AddComponent<Image>();
                ribbonImg.sprite = theme.RibbonB;
                ribbonImg.type = Image.Type.Sliced;
                UIFactory.CreateText(ribbon, "Triple Tiles\nKingdom", Vector2.zero, new Vector2(680, 190), fontSize: 52);
            }
            else
            {
                UIFactory.CreateText(canvas.transform, "Triple Tiles\nKingdom", new Vector2(0, 480), new Vector2(760, 200), fontSize: 56);
            }

            if (theme.PanelPurple != null)
            {
                var buttonBacking = UIFactory.CreateContainer(canvas.transform, "ButtonBacking");
                buttonBacking.anchoredPosition = new Vector2(0, -50);
                buttonBacking.sizeDelta = new Vector2(480, 460);
                var backingImg = buttonBacking.gameObject.AddComponent<Image>();
                backingImg.sprite = theme.PanelPurple;
                backingImg.type = Image.Type.Sliced;
            }

            _playButton = UIFactory.CreateSpriteButton(canvas.transform, "Play", new Vector2(0, 80), new Vector2(370, 110), theme.ButtonGreen, null);
            _settingsButton = UIFactory.CreateSpriteButton(canvas.transform, "Settings", new Vector2(0, -50), new Vector2(370, 110), theme.ButtonOrange, null);
            _quitButton = UIFactory.CreateSpriteButton(canvas.transform, "Quit", new Vector2(0, -180), new Vector2(370, 110), theme.ButtonOrange, null);

            var coinPanel = UIFactory.CreateContainer(canvas.transform, "CoinPanel");
            coinPanel.anchoredPosition = new Vector2(-420, 800);
            coinPanel.sizeDelta = new Vector2(220, 90);
            var coinBg = coinPanel.gameObject.AddComponent<Image>();
            coinBg.sprite = theme.ButtonOrange;
            coinBg.type = Image.Type.Sliced;
            _coinText = UIFactory.CreateText(coinPanel, "0", Vector2.zero, new Vector2(200, 70), fontSize: 36);

            if (theme.ChestGold != null)
            {
                var chest = UIFactory.CreateContainer(canvas.transform, "Chest");
                chest.anchoredPosition = new Vector2(420, 800);
                chest.sizeDelta = new Vector2(110, 110);
                var chestImg = chest.gameObject.AddComponent<Image>();
                chestImg.sprite = theme.ChestGold;
                chestImg.preserveAspect = true;
            }

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private void OnPlayClicked()
        {
            SceneManager.LoadScene("LevelSelect");
        }

        private void OnSettingsClicked()
        {
            // Placeholder — settings panel not yet implemented.
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdateCoinDisplay()
        {
            if (_coinText != null)
            {
                _coinText.text = PlayerPrefs.GetInt("Coins", 0).ToString();
            }
        }
    }
}
