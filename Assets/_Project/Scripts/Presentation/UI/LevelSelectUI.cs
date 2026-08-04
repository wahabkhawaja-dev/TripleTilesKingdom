using LevelSystem.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Level Select UI. Prefers a scene-authored Canvas/background/ribbon/back button and
    /// scroll structure (built once by Tools > Build Game Scenes and saved into
    /// LevelSelect.unity — hand-editable in the Inspector) — falls back to procedural
    /// construction via UIFactory only if those references are missing. The level
    /// buttons themselves are always instantiated at runtime because their unlock
    /// state is per-save-file data, but their count comes from the authored
    /// LevelCollectionSO — one button per level asset, not a hardcoded number.
    /// </summary>
    public sealed class LevelSelectUI : MonoBehaviour
    {
        // Fallback used only for scene-layout sizing when no collection has been
        // authored yet (shows the shell so the level-select screen doesn't render
        // empty in that failure mode). The runtime button count is always
        // collection.Count when a collection is present.
        private const int FallbackButtonCount = 50;
        private const int Columns = 5;
        private const float CellWidth = 200f;
        private const float CellHeight = 100f;
        private const float Spacing = 18f;

        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private Clickable _backButton;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;

        private void Awake()
        {
            if (_backButton == null || _scrollRect == null || _content == null)
            {
                BuildFallback();
            }

            _backButton.Clicked += OnBackClicked;

            // The 50 level buttons are the one thing still built here rather than
            // hand-authored — their unlock state is per-save-file data, so there's no
            // static "correct" set of buttons to bake into the scene. The grid that
            // holds them (GridLayoutGroup, cell size, content size) is scene-authored
            // (see BuildFallback) and never touched at runtime.
            CreateLevelButtons(_content, UIFactory.Theme);
            EnsureEventSystem();
        }

        /// <summary>Original runtime-construction path, used only when scene-authored refs are missing.</summary>
        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.10f, 0.10f, 0.17f));

            if (theme.RibbonA != null)
            {
                var ribbon = UIFactory.CreateContainer(canvas.transform, "TitleRibbon");
                ribbon.anchoredPosition = new Vector2(0, 800);
                ribbon.sizeDelta = new Vector2(620, 170);
                var ribbonImg = ribbon.gameObject.AddComponent<Image>();
                ribbonImg.sprite = theme.RibbonA;
                ribbonImg.type = Image.Type.Sliced;
            }
            UIFactory.CreateText(canvas.transform, "Select Level", new Vector2(0, 790), new Vector2(700, 100), fontSize: 44);

            _backButton = UIFactory.CreateIconButton(canvas.transform, "Back", new Vector2(-420, -790), 100f, theme.ArrowBack, null);

            // Scroll viewport: everything between the title and the safe bottom margin.
            var viewportArea = UIFactory.CreateContainer(canvas.transform, "ScrollViewport");
            viewportArea.anchoredPosition = new Vector2(0, -30);
            var viewportWidth = Columns * CellWidth + (Columns - 1) * Spacing + 60f;
            viewportArea.sizeDelta = new Vector2(viewportWidth, 1400f);

            if (theme.PanelPurple != null)
            {
                var backingImg = viewportArea.gameObject.AddComponent<Image>();
                backingImg.sprite = theme.PanelPurple;
                backingImg.type = Image.Type.Sliced;
                backingImg.color = new Color(1f, 1f, 1f, 0.75f);
            }

            var scrollRectGO = new GameObject("ScrollRect", typeof(RectTransform));
            scrollRectGO.transform.SetParent(viewportArea, false);
            var scrollRectTransform = scrollRectGO.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(20, 20);
            scrollRectTransform.offsetMax = new Vector2(-20, -20);

            var mask = scrollRectGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var maskImage = scrollRectGO.AddComponent<Image>();
            maskImage.color = Color.white;

            _scrollRect = scrollRectGO.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(scrollRectGO.transform, false);
            _content = contentGO.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0.5f, 1f);
            _content.anchorMax = new Vector2(0.5f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;

            _scrollRect.content = _content;
            _scrollRect.viewport = scrollRectTransform;

            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellWidth, CellHeight);
            grid.spacing = new Vector2(Spacing, Spacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;

            var rows = Mathf.CeilToInt(FallbackButtonCount / (float)Columns);
            var contentHeight = rows * CellHeight + (rows - 1) * Spacing + 20f;
            _content.sizeDelta = new Vector2(Columns * CellWidth + (Columns - 1) * Spacing, contentHeight);
        }

        private void CreateLevelButtons(Transform container, UIThemeSO theme)
        {
            var highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 0);
            var collection = Resources.Load<LevelCollectionSO>("Levels/LevelCollection");
            var levelCount = collection != null && collection.Count > 0 ? collection.Count : 0;

            if (levelCount == 0)
            {
                Debug.LogWarning("[LevelSelectUI] No LevelCollection at Resources/Levels/LevelCollection.asset — level select is empty. Open Tools ▸ Level Designer to author levels.");
                return;
            }

            // Content height also depends on the actual level count now — resize so
            // the scroll extent matches, otherwise the last row can hide behind the
            // viewport mask.
            var rows = Mathf.CeilToInt(levelCount / (float)Columns);
            var contentHeight = rows * CellHeight + (rows - 1) * Spacing + 20f;
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, contentHeight);

            for (var i = 0; i < levelCount; i++)
            {
                var levelIndex = i;
                var isUnlocked = i <= highestUnlocked;

                var button = UIFactory.CreateSpriteButton(
                    container,
                    (i + 1).ToString(),
                    Vector2.zero,
                    new Vector2(CellWidth - 20f, CellHeight - 20f),
                    isUnlocked ? theme.ButtonGreen : theme.ButtonOrange,
                    () => OnLevelSelected(levelIndex));

                button.Interactable = isUnlocked;
                if (!isUnlocked)
                {
                    var img = button.GetComponent<Image>();
                    img.color = new Color(0.5f, 0.5f, 0.5f);
                }
            }
        }

        private void OnLevelSelected(int levelIndex)
        {
            PlayerPrefs.SetInt("LevelToPlay", levelIndex);
            SceneManager.LoadScene("Gameplay");
        }

        private void OnBackClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
