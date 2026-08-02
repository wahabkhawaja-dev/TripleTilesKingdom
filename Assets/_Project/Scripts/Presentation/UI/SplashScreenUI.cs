using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Splash Screen. Prefers a scene-authored Canvas/background/label (built once by
    /// Tools > Build Game Scenes and saved into Splash.unity — hand-editable in the
    /// Inspector) — falls back to procedural construction via UIFactory only if those
    /// references are missing. Fades in, then transitions to Bootstrap after a delay.
    /// </summary>
    public sealed class SplashScreenUI : MonoBehaviour
    {
        private const int TraySlotCount = 6;

        [SerializeField] private float _displayDuration = 3f;

        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private TMPro.TextMeshProUGUI _loadingText;
        [SerializeField] private RectTransform _traySlotsContainer;

        private readonly List<Image> _traySlotIcons = new();

        private void Awake()
        {
            if (_rootGroup == null)
            {
                BuildFallback();
            }

            _rootGroup.alpha = 0f;
            Tween.Alpha(_rootGroup, 1f, 0.4f, Ease.OutQuad, useUnscaledTime: true);

            if (_loadingText != null)
            {
                Tween.Alpha(_loadingText, 0.3f, 0.6f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
            }

            if (_traySlotsContainer != null)
            {
                if (_traySlotIcons.Count == 0)
                {
                    // Scene-authored path: BuildFallback didn't run this session, so the
                    // icon list was never populated — find them from the saved hierarchy.
                    foreach (Transform slot in _traySlotsContainer)
                    {
                        var iconTransform = slot.Find("Icon");
                        var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                        if (icon != null)
                        {
                            _traySlotIcons.Add(icon);
                        }
                    }
                }

                PlayTrayLoadingAnimation();
            }
        }

        /// <summary>
        /// Pops each tray slot's icon in one after another, evenly spaced across the
        /// display duration — reads as the game's own tray filling up with tiles rather
        /// than a generic progress bar, on the theory that reusing gameplay's own visual
        /// language for "things are happening" is more on-brand than a borrowed UI
        /// widget.
        /// </summary>
        private void PlayTrayLoadingAnimation()
        {
            if (_traySlotIcons.Count == 0)
            {
                return;
            }

            var interval = _displayDuration / _traySlotIcons.Count;
            for (var i = 0; i < _traySlotIcons.Count; i++)
            {
                var icon = _traySlotIcons[i];
                icon.enabled = true;
                icon.transform.localScale = Vector3.zero;
                Tween.Scale(icon.transform, 1f, interval * 0.6f, Ease.OutBack, startDelay: i * interval, useUnscaledTime: true);
            }
        }

        /// <summary>Original runtime-construction path, used only when scene-authored refs are missing.</summary>
        private void BuildFallback()
        {
            var theme = UIFactory.Theme;

            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.10f, 0.12f, 0.22f));

            if (theme.Plaque != null)
            {
                var plaque = UIFactory.CreateContainer(canvas.transform, "Plaque");
                plaque.anchoredPosition = new Vector2(0, 160);
                plaque.sizeDelta = new Vector2(180, 180);
                var img = plaque.gameObject.AddComponent<Image>();
                img.sprite = theme.Plaque;
                img.preserveAspect = true;
            }

            RectTransform titleParent = canvas.transform as RectTransform;
            if (theme.RibbonB != null)
            {
                var ribbon = UIFactory.CreateContainer(canvas.transform, "TitleRibbon");
                ribbon.anchoredPosition = new Vector2(0, -30);
                ribbon.sizeDelta = new Vector2(760, 200);
                var ribbonImg = ribbon.gameObject.AddComponent<Image>();
                ribbonImg.sprite = theme.RibbonB;
                ribbonImg.type = Image.Type.Sliced;
                titleParent = ribbon;
            }

            UIFactory.CreateText(titleParent, "Triple Tiles\nKingdom", Vector2.zero, new Vector2(700, 180), fontSize: 54);

            _loadingText = UIFactory.CreateText(canvas.transform, "Loading...", new Vector2(0, -350), new Vector2(400, 60), fontSize: 30);

            BuildLoadingTray(canvas.transform);

            _rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// A row of tray slot sockets (mirrors TrayController's own base+fruit slot
        /// layering) that pop full one by one during the loading wait — see
        /// PlayTrayLoadingAnimation.
        /// </summary>
        private void BuildLoadingTray(Transform canvasTransform)
        {
            var tileTheme = Resources.Load<TileThemeSO>("TileTheme_Default");

            const float slotSize = 74f;
            const float slotSpacing = 82f;
            var totalWidth = (TraySlotCount - 1) * slotSpacing;

            var tray = UIFactory.CreateContainer(canvasTransform, "LoadingTray");
            tray.anchoredPosition = new Vector2(0, -420);
            tray.sizeDelta = new Vector2(totalWidth + slotSpacing * 1.4f, slotSize * 1.3f);

            if (tileTheme != null && tileTheme.TrayBarSprite != null)
            {
                var barImage = tray.gameObject.AddComponent<Image>();
                barImage.sprite = tileTheme.TrayBarSprite;
                barImage.type = Image.Type.Sliced;
            }

            _traySlotsContainer = tray;
            _traySlotIcons.Clear();

            var startX = -totalWidth / 2f;
            for (var i = 0; i < TraySlotCount; i++)
            {
                var slotGO = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGO.transform.SetParent(tray, false);
                var slotRect = slotGO.GetComponent<RectTransform>();
                slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0.5f, 0.5f);
                slotRect.sizeDelta = new Vector2(slotSize, slotSize);
                slotRect.anchoredPosition = new Vector2(startX + i * slotSpacing, 0f);

                if (tileTheme != null && tileTheme.BaseTileSprite != null)
                {
                    var bg = slotGO.AddComponent<Image>();
                    bg.sprite = tileTheme.BaseTileSprite;
                    bg.preserveAspect = true;
                    bg.color = new Color(1f, 1f, 1f, 0.5f);
                }

                var iconGO = new GameObject("Icon", typeof(RectTransform));
                iconGO.transform.SetParent(slotGO.transform, false);
                var iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(slotSize * 0.65f, slotSize * 0.65f);
                var icon = iconGO.AddComponent<Image>();
                icon.preserveAspect = true;
                if (tileTheme != null && tileTheme.FruitSprites != null && tileTheme.FruitSprites.Length > 0)
                {
                    icon.sprite = tileTheme.FruitSprites[i % tileTheme.FruitSprites.Length];
                }
                icon.enabled = false;

                _traySlotIcons.Add(icon);
            }
        }

        private void Start()
        {
            Invoke(nameof(TransitionToBootstrap), _displayDuration);
        }

        private void TransitionToBootstrap()
        {
            SceneManager.LoadScene("Bootstrap");
        }
    }
}
