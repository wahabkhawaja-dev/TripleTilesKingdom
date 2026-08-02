using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Shared helpers for building uGUI elements procedurally at runtime, themed against
    /// UIThemeSO where a sprite is available and falling back to solid-color placeholders
    /// otherwise. Every Presentation UI script builds its own hierarchy in code rather
    /// than relying on scene-authored references — this keeps every scene one component
    /// away from working regardless of how (or whether) an external scene-setup tool
    /// wired things. All elements are anchored to their parent's center by default so
    /// anchoredPosition behaves predictably as a simple offset from center.
    ///
    /// Uses Clickable (not UnityEngine.UI.Button) throughout — see Clickable.cs for why.
    /// </summary>
    internal static class UIFactory
    {
        private static UIThemeSO _theme;

        public static UIThemeSO Theme
        {
            get
            {
                if (_theme == null)
                {
                    _theme = Resources.Load<UIThemeSO>("UITheme_Default");
                }
                return _theme;
            }
        }

        public static Canvas CreateScreenCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Portrait reference (matches this game's actual orientation and every
            // anchoredPosition value used throughout the UI code, which all assume a
            // ~1080-wide, ~1920-tall canvas) — the previous 1920x1080 reference was a
            // landscape resolution swapped in backwards, which is why everything read
            // noticeably smaller than intended on an actual portrait phone screen.
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            CenterAnchor(rect);
            return rect;
        }

        public static RectTransform CreateFullScreenContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image CreateFullScreenPanel(Transform parent, Color color)
        {
            var rect = CreateFullScreenContainer(parent, "Panel");
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>Full-screen background image (e.g. a themed scenic panel), stretched to fill and drawn behind everything else via SetAsFirstSibling.</summary>
        public static Image CreateFullScreenSprite(Transform parent, Sprite sprite)
        {
            var rect = CreateFullScreenContainer(parent, "Background");
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            rect.SetAsFirstSibling();
            return image;
        }

        /// <summary>
        /// White text with a dark outline by default — readable on any background
        /// (dark navy, cream panel, colored button) without having to hand-pick a color
        /// per context. White-on-cream with no outline was reading as nearly invisible
        /// on panel/button sprites with light fills.
        /// </summary>
        public static TextMeshProUGUI CreateText(Transform parent, string text, Vector2 anchoredPos, Vector2 size, int fontSize = 36, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            CenterAnchor(rect);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;
            ApplyOutline(tmp);
            return tmp;
        }

        private static void ApplyOutline(TextMeshProUGUI tmp)
        {
            // fontMaterial (not font.material) instantiates a private copy for this
            // instance, so this never bleeds into the shared default material.
            var mat = tmp.fontMaterial;
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.15f, 0.1f, 0.08f, 1f));
            tmp.fontMaterial = mat;
            tmp.fontSharedMaterial = mat;
        }

        /// <summary>Solid-color pill button. Kept as a fallback for contexts with no matching themed sprite.</summary>
        public static Clickable CreateButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, Color color, Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            CenterAnchor(rect);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;

            var clickable = go.AddComponent<Clickable>();
            if (onClick != null)
            {
                clickable.Clicked += onClick;
            }

            CreateText(go.transform, label, Vector2.zero, size, fontSize: 28);

            return clickable;
        }

        /// <summary>Themed pill button (9-sliced sprite background) with a label — the mockup's Play/Next/Retry style.</summary>
        public static Clickable CreateSpriteButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, Sprite sprite, Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            CenterAnchor(rect);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;

            var clickable = go.AddComponent<Clickable>();
            if (onClick != null)
            {
                clickable.Clicked += onClick;
            }

            if (!string.IsNullOrEmpty(label))
            {
                CreateText(go.transform, label, Vector2.zero, size, fontSize: 30);
            }

            return clickable;
        }

        /// <summary>Circular icon-only button (pause, settings, back) — no label, just a themed sprite.</summary>
        public static Clickable CreateIconButton(Transform parent, string name, Vector2 anchoredPos, float diameter, Sprite sprite, Action onClick)
        {
            var go = new GameObject($"IconButton_{name}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            CenterAnchor(rect);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(diameter, diameter);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            var clickable = go.AddComponent<Clickable>();
            if (onClick != null)
            {
                clickable.Clicked += onClick;
            }

            return clickable;
        }

        private static void CenterAnchor(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
