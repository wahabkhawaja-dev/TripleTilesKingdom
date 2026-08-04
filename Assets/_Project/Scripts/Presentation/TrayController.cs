using System;
using System.Collections.Generic;
using Core.Services;
using Domain.Board;
using Domain.Matching;
using Domain.Tray;
using PrimeTween;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// World-space sprite tray. Each slot is a child transform with a background
    /// and an icon <see cref="SpriteRenderer"/>. Draw order is fixed by
    /// per-role sortingOrder — tray bar 500, slot bg 501, slot icon 502 — so
    /// nothing on the tray ever fights a board tile for visibility.
    /// The tray no longer handles the insert-pop animation itself: that's the
    /// job of the flying tile from the board (see BoardController.FlyTileToSlot).
    /// GameFlowController calls <see cref="ShowSlotIcon"/> the frame the flying
    /// tile lands, and the transition reads as the tile becoming the slot icon.
    /// </summary>
    public sealed class TrayController : MonoBehaviour
    {
        [Header("Layout (world units)")]
        [SerializeField] private float _slotSize = 1f;
        [SerializeField] private float _slotSpacing = 1.08f;
        [Tooltip("Widest tray width in world units. High-capacity trays scale down uniformly to stay inside this budget.")]
        [SerializeField] private float _maxTrayWidth = 8.5f;
        [SerializeField] private float _iconYOffset = 0.05f;

        /// <summary>
        /// Uniform scale applied to the icon transform — deliberately matches the
        /// tile prefab's Fruit child scale (see <c>TilePrefabBuilder</c>) so a
        /// fruit shows at the exact same visual size on the tile and in the tray.
        /// Previously the icon was force-scaled to fill the whole slot (0.65× or
        /// 1×), which made the tray fruit look ~3× bigger than on the board.
        /// </summary>
        [SerializeField] private float _iconScale = 0.3f;

        [Header("Theme")]
        [SerializeField] private TileThemeSO _theme;

        // Tray sits above every board tile (board bands top out at layer × 100 +
        // grid sub-order — <1000 for any realistic pyramid).
        private const int TrayBarSortingOrder = 2000;
        private const int SlotBgSortingOrder = 2001;
        private const int SlotIconSortingOrder = 2002;

        private float _effectiveSlotSize;
        private float _effectiveSlotSpacing;

        private TrayModel _trayModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private readonly List<Transform> _slotRoots = new();
        private readonly List<SpriteRenderer> _slotBackgrounds = new();
        private readonly List<SpriteRenderer> _slotIcons = new();
        private readonly List<Vector3> _slotIconRestScales = new();
        private readonly List<TileTypeId?> _lastKnownSlots = new();
        private Sequence[] _popSequences = Array.Empty<Sequence>();
        private Transform _trayBar;

        public int Capacity => _trayModel != null ? _trayModel.Capacity : 0;

        public void Initialize(TrayModel trayModel, TileThemeSO theme, IReadOnlyList<Sprite> fruitSpritesByType)
        {
            _trayModel = trayModel;
            if (theme != null) _theme = theme;
            _fruitSpritesByType = fruitSpritesByType;
            CreateSlots();
            SnapToCurrentState();
        }

        public Vector3 GetSlotWorldPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotRoots.Count) return transform.position;
            return _slotRoots[slotIndex].position + new Vector3(0f, _iconYOffset, 0f);
        }

        public Vector3 GetSlotIconWorldScale(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotIcons.Count) return Vector3.one;
            return _slotIcons[slotIndex].transform.lossyScale;
        }

        /// <summary>
        /// Called by GameFlowController the frame a flying tile lands. Snaps the
        /// slot's icon to the right sprite + enabled, plays a small OutBack pulse
        /// so the moment is felt, and records the occupant.
        /// </summary>
        public void ShowSlotIcon(int slotIndex, TileTypeId type)
        {
            if (slotIndex < 0 || slotIndex >= _slotIcons.Count) return;
            var icon = _slotIcons[slotIndex];
            StopStalePop(slotIndex, icon);

            icon.sprite = _fruitSpritesByType[type.Value % _fruitSpritesByType.Count];
            icon.enabled = true;
            var rest = _slotIconRestScales[slotIndex];
            icon.transform.localScale = rest * 1.15f;
            Tween.Scale(icon.transform, rest, 0.18f, Ease.OutBack);

            _lastKnownSlots[slotIndex] = type;
        }

        private void CreateSlots()
        {
            foreach (var s in _popSequences)
            {
                if (s.isAlive) s.Stop();
            }

            var existingBar = transform.Find("TrayBar");
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == existingBar) continue;
                foreach (var descendant in child.GetComponentsInChildren<Transform>(true))
                {
                    Tween.StopAll(descendant);
                }
                Destroy(child.gameObject);
            }
            _slotRoots.Clear();
            _slotBackgrounds.Clear();
            _slotIcons.Clear();
            _slotIconRestScales.Clear();
            _lastKnownSlots.Clear();
            _popSequences = new Sequence[_trayModel.Capacity];

            ComputeSlotMetrics();
            var totalWidth = (_trayModel.Capacity - 1) * _effectiveSlotSpacing;
            var barWidth = totalWidth + _effectiveSlotSpacing * 1.4f;

            EnsureTrayBar(existingBar, barWidth);

            var startX = -totalWidth / 2f;
            for (var i = 0; i < _trayModel.Capacity; i++)
            {
                var slotGO = new GameObject($"Slot_{i}");
                slotGO.transform.SetParent(transform, false);
                slotGO.transform.localPosition = new Vector3(startX + i * _effectiveSlotSpacing, 0f, 0f);
                slotGO.transform.localScale = Vector3.one;

                var bgGO = new GameObject("Bg");
                bgGO.transform.SetParent(slotGO.transform, false);
                var bg = bgGO.AddComponent<SpriteRenderer>();
                bg.sprite = _theme != null ? _theme.BaseTileSprite : null;
                bg.color = new Color(1f, 1f, 1f, 0.55f);
                bg.sortingOrder = SlotBgSortingOrder;
                ScaleRendererToSize(bg, _effectiveSlotSize);

                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(slotGO.transform, false);
                iconGO.transform.localPosition = new Vector3(0f, _iconYOffset, 0f);
                // Match the tile prefab's Fruit-child scale exactly so a fruit
                // sprite is the same visual size on the tile and in the tray.
                // No per-sprite bounds normalization — that would inflate a small
                // sprite to fill the slot regardless of its real dimensions.
                iconGO.transform.localScale = Vector3.one * _iconScale;
                var icon = iconGO.AddComponent<SpriteRenderer>();
                icon.sortingOrder = SlotIconSortingOrder;
                icon.enabled = false;

                _slotRoots.Add(slotGO.transform);
                _slotBackgrounds.Add(bg);
                _slotIcons.Add(icon);
                _slotIconRestScales.Add(icon.transform.localScale);
                _lastKnownSlots.Add(null);
            }
        }

        private void EnsureTrayBar(Transform existing, float barWidth)
        {
            if (existing != null)
            {
                var sr = existing.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(barWidth, _effectiveSlotSize * 1.3f);
                    sr.sortingOrder = TrayBarSortingOrder;
                }
                _trayBar = existing;
                return;
            }

            if (_theme == null || _theme.TrayBarSprite == null) return;

            var go = new GameObject("TrayBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _theme.TrayBarSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(barWidth, _effectiveSlotSize * 1.3f);
            renderer.sortingOrder = TrayBarSortingOrder;
            _trayBar = go.transform;
        }

        private static void ScaleRendererToSize(SpriteRenderer renderer, float worldSize)
        {
            if (renderer.sprite == null)
            {
                renderer.transform.localScale = new Vector3(worldSize, worldSize, 1f);
                return;
            }
            var bounds = renderer.sprite.bounds.size;
            var maxDim = Mathf.Max(bounds.x, bounds.y);
            if (maxDim <= 0.0001f)
            {
                renderer.transform.localScale = new Vector3(worldSize, worldSize, 1f);
                return;
            }
            var scale = worldSize / maxDim;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ComputeSlotMetrics()
        {
            var idealWidth = (_trayModel.Capacity - 1) * _slotSpacing + _slotSize;
            var scale = idealWidth > _maxTrayWidth ? _maxTrayWidth / idealWidth : 1f;
            _effectiveSlotSize = _slotSize * scale;
            _effectiveSlotSpacing = _slotSpacing * scale;
        }

        private void SnapToCurrentState()
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
                _lastKnownSlots[i] = slotContent;
            }
        }

        /// <summary>
        /// Pops matched slots. Only matched slots pop — a slot going empty
        /// without a match will never happen in this game, so the old
        /// "content-null but previous had a value → pop" diff was double-
        /// popping intermediate tiles when a later tile completed the match.
        /// Inserts are handled up-front by <see cref="ShowSlotIcon"/>.
        /// </summary>
        public void Refresh(MatchResult? completedMatch = null)
        {
            if (!completedMatch.HasValue) return;

            var matched = completedMatch.Value.SlotIndices;
            var poppingIndices = new List<int>(matched.Count);
            var matchType = completedMatch.Value.TileType;

            for (var k = 0; k < matched.Count; k++)
            {
                var i = matched[k];
                if (i < 0 || i >= _slotIcons.Count) continue;

                var icon = _slotIcons[i];
                StopStalePop(i, icon);

                // A matched slot the player never saw filled (rare — the flying
                // tile hasn't landed there yet) still needs an icon to pop.
                if (!icon.enabled)
                {
                    icon.sprite = _fruitSpritesByType[matchType.Value % _fruitSpritesByType.Count];
                    icon.enabled = true;
                    icon.transform.localScale = _slotIconRestScales[i];
                }

                poppingIndices.Add(i);
                _lastKnownSlots[i] = null;
            }

            // Repair the type-change edge case (rare — can happen if RemoveSlots
            // compaction moved a different type into an already-known slot).
            for (var i = 0; i < _trayModel.Capacity; i++)
            {
                var slotContent = _trayModel.Slots[i];
                var previous = _lastKnownSlots[i];
                if (slotContent.HasValue && previous.HasValue && slotContent.Value.Value != previous.Value.Value)
                {
                    _slotIcons[i].sprite = _fruitSpritesByType[slotContent.Value.Value % _fruitSpritesByType.Count];
                    _lastKnownSlots[i] = slotContent;
                }
            }

            if (poppingIndices.Count > 0) PlayMatchPopSequence(poppingIndices);
        }

        private void StopStalePop(int index, SpriteRenderer icon)
        {
            if (_popSequences[index].isAlive) _popSequences[index].Stop();
            // Also kill the ShowSlotIcon land-pulse tween if it's still running —
            // otherwise a rapid land-then-pop on the same slot leaves two tweens
            // fighting for the icon's scale (that's what caused the flicker).
            Tween.StopAll(icon.transform);
            icon.transform.localScale = _slotIconRestScales[index];
        }

        private const float PopStagger = 0.05f;
        private const float PopPunchDuration = 0.09f;
        private const float PopShrinkDuration = 0.16f;
        private const float PopPitchStep = 0.06f;
        private const float PopPitchMax = 1.35f;

        private void PlayMatchPopSequence(List<int> indices)
        {
            for (var k = 0; k < indices.Count; k++)
            {
                var popOrder = k;
                var index = indices[k];
                var icon = _slotIcons[index];
                var iconTransform = icon.transform;
                var restScale = _slotIconRestScales[index];
                var startDelay = k * PopStagger;

                _popSequences[index] = Sequence.Create()
                    .ChainDelay(startDelay)
                    .Chain(Tween.Scale(iconTransform, restScale * 1.2f, PopPunchDuration, Ease.OutQuad))
                    .Chain(Tween.Scale(iconTransform, restScale * 0.001f, PopShrinkDuration, Ease.InBack))
                    .OnComplete(() =>
                    {
                        icon.enabled = false;
                        iconTransform.localScale = restScale;
                        WorldParticleBurst.BurstSparkle(iconTransform.position, count: 14, duration: 0.5f, distance: 0.9f);
                        if (GameServices.IsRegistered)
                        {
                            GameServices.Haptics.Play(HapticStrength.Heavy);
                            var pitch = Mathf.Min(1f + popOrder * PopPitchStep, PopPitchMax);
                            GameServices.Audio.PlaySfx("TilePopSound", pitch: pitch);
                        }
                    });
            }
        }
    }
}
