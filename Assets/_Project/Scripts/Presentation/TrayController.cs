using System.Collections.Generic;
using Domain.Board;
using Domain.Tray;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Manages the visual representation of the tray. Must be attached to a
    /// RectTransform under a Canvas. Builds its own slot visuals procedurally against a
    /// themed tray-bar background — each slot shows the base tile socket plus the
    /// fruit icon currently occupying it (mirrors TileView's base+fruit layering).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TrayController : MonoBehaviour
    {
        [SerializeField] private float _slotSize = 84f;
        [SerializeField] private float _slotSpacing = 92f;

        private TrayModel _trayModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private TileThemeSO _theme;
        private readonly List<Image> _slotBackgrounds = new();
        private readonly List<Image> _slotIcons = new();

        public void Initialize(TrayModel trayModel, TileThemeSO theme, IReadOnlyList<Sprite> fruitSpritesByType)
        {
            _trayModel = trayModel;
            _theme = theme;
            _fruitSpritesByType = fruitSpritesByType;
            CreateSlots();
            Refresh();
        }

        private void CreateSlots()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            _slotBackgrounds.Clear();
            _slotIcons.Clear();

            var totalWidth = (_trayModel.Capacity - 1) * _slotSpacing;
            var barWidth = totalWidth + _slotSpacing * 1.4f;

            if (_theme.TrayBarSprite != null)
            {
                var barGO = new GameObject("TrayBar", typeof(RectTransform));
                barGO.transform.SetParent(transform, false);
                var barRect = barGO.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(barWidth, _slotSize * 1.3f);
                var barImage = barGO.AddComponent<Image>();
                barImage.sprite = _theme.TrayBarSprite;
                barImage.type = Image.Type.Sliced;
                barGO.transform.SetAsFirstSibling();
            }

            var startX = -totalWidth / 2f;

            for (var i = 0; i < _trayModel.Capacity; i++)
            {
                var slotGO = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGO.transform.SetParent(transform, false);

                var rectTransform = slotGO.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(_slotSize, _slotSize);
                rectTransform.anchoredPosition = new Vector2(startX + i * _slotSpacing, 0f);

                var bgImage = slotGO.AddComponent<Image>();
                bgImage.sprite = _theme.BaseTileSprite;
                bgImage.preserveAspect = true;
                bgImage.color = new Color(1f, 1f, 1f, 0.5f);

                var iconGO = new GameObject("Icon", typeof(RectTransform));
                iconGO.transform.SetParent(slotGO.transform, false);
                var iconRect = iconGO.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(_slotSize * 0.65f, _slotSize * 0.65f);
                var iconImage = iconGO.AddComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.enabled = false;

                _slotBackgrounds.Add(bgImage);
                _slotIcons.Add(iconImage);
            }
        }

        public void Refresh()
        {
            for (var i = 0; i < _trayModel.Capacity; i++)
            {
                var slotContent = _trayModel.Slots[i];
                var icon = _slotIcons[i];

                if (slotContent.HasValue)
                {
                    icon.sprite = _fruitSpritesByType[slotContent.Value.Value % _fruitSpritesByType.Count];
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }
        }
    }
}
