using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Lightweight, low-end-mobile-friendly particle bursts built from pooled plain
    /// uGUI Images instead of Unity's ParticleSystem — no shaders, no extra draw calls
    /// beyond ordinary UI batching, no GPU particle overhead. A handful of small sparkle
    /// sprites are tweened outward from a point with PrimeTween (scale + fade + move +
    /// spin), then returned to the pool.
    ///
    /// Works entirely in world position (RectTransform.position, not anchoredPosition)
    /// so any caller anywhere in the gameplay canvas can request a burst at its own
    /// transform.position without needing to know or convert into some other object's
    /// local coordinate space — the only requirement is that everything lives under the
    /// same ScreenSpaceOverlay canvas, which is true for the whole Gameplay scene.
    /// </summary>
    public sealed class UIParticleBurst : MonoBehaviour
    {
        private static UIParticleBurst _instance;
        private static Sprite[] _sparkleSprites;
        private readonly Stack<Image> _pool = new();
        private RectTransform _root;

        private static UIParticleBurst GetOrCreate(Transform anyTransformUnderCanvas)
        {
            if (_instance != null)
            {
                return _instance;
            }

            var canvas = anyTransformUnderCanvas.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform : anyTransformUnderCanvas;

            var go = new GameObject("UIParticleBurst", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Draw above the board/tray but BELOW the HUD — popups (win/lose/pause) live
            // inside HUD, and a naive "always last sibling" placement would draw a heavy
            // burst on top of them, visually hiding the popup entirely. Insert just before
            // HUD if it exists; otherwise fall back to last (e.g. non-gameplay canvases).
            var hud = parent.Find("HUD");
            if (hud != null)
            {
                rect.SetSiblingIndex(hud.GetSiblingIndex());
            }
            else
            {
                rect.SetAsLastSibling();
            }

            _instance = go.AddComponent<UIParticleBurst>();
            _instance._root = rect;
            return _instance;
        }

        private static Sprite[] GetSparkleSprites()
        {
            if (_sparkleSprites == null)
            {
                var theme = Resources.Load<TileThemeSO>("TileTheme_Default");
                _sparkleSprites = theme != null && theme.ParticleSprites != null && theme.ParticleSprites.Length > 0
                    ? theme.ParticleSprites
                    : new Sprite[0];
            }
            return _sparkleSprites;
        }

        /// <summary>Themed sparkle burst — the sprite-based particles used for tile click/move/pop/win/lose.</summary>
        public static void BurstSparkle(Transform anyTransformUnderCanvas, Vector3 originWorldPos, int count = 8, float duration = 0.45f, float distance = 90f, Color? tint = null)
        {
            var sprites = GetSparkleSprites();
            var instance = GetOrCreate(anyTransformUnderCanvas);
            var color = tint ?? Color.white;

            for (var i = 0; i < count; i++)
            {
                var sprite = sprites.Length > 0 ? sprites[Random.Range(0, sprites.Length)] : null;
                instance.SpawnOne(originWorldPos, color, sprite, duration, distance, i, count);
            }
        }

        private void SpawnOne(Vector3 originWorldPos, Color color, Sprite sprite, float duration, float distance, int index, int total)
        {
            var particle = RentParticle();
            particle.transform.position = originWorldPos;
            particle.transform.localScale = Vector3.one * Random.Range(0.75f, 1.15f);
            particle.transform.localEulerAngles = Vector3.zero;
            particle.color = color;
            particle.sprite = sprite;

            var angle = (360f / total) * index + Random.Range(-18f, 18f);
            var radians = angle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            var target = originWorldPos + dir * distance * Random.Range(0.7f, 1f);

            Tween.Position(particle.transform, target, duration, Ease.OutQuad);
            Tween.Scale(particle.transform, 0f, duration, Ease.InQuad);
            Tween.LocalEulerAngles(particle.transform, Vector3.zero, new Vector3(0f, 0f, Random.Range(-180f, 180f)), duration, Ease.OutQuad);
            Tween.Alpha(particle, 0f, duration, Ease.InQuad)
                .OnComplete(() => ReturnParticle(particle));
        }

        private Image RentParticle()
        {
            if (_pool.Count > 0)
            {
                var reused = _pool.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var go = new GameObject("Particle", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(34f, 34f);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private void ReturnParticle(Image particle)
        {
            if (particle == null)
            {
                return;
            }
            particle.gameObject.SetActive(false);
            _pool.Push(particle);
        }
    }
}
