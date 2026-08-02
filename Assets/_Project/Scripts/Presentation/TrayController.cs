using System.Collections.Generic;
using Core.Services;
using Domain.Board;
using Domain.Matching;
using Domain.Tray;
using PrimeTween;
using Presentation.UI;
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
        /// <summary>
        /// Ideal (unscaled) slot size/spacing, used as-is for typical tray capacities
        /// (6-9 slots). Capacity can go up to 12, though, and 12 slots at full ideal
        /// spacing would overflow a 1080-wide portrait canvas — see ComputeSlotMetrics,
        /// which scales both down together only when the ideal width wouldn't fit, so a
        /// small everyday tray reads as big as it can while a maxed-out one still fits.
        /// </summary>
        [SerializeField] private float _slotSize = 130f;
        [SerializeField] private float _slotSpacing = 140f;
        [SerializeField] private float _maxTrayWidth = 980f;

        /// <summary>Icon is nudged up from the slot's visual center so it doesn't sit flush on the socket's bottom edge — scales with the effective slot size.</summary>
        [SerializeField] private float _iconYOffset = 10f;

        private float _effectiveSlotSize;
        private float _effectiveSlotSpacing;
        private float _effectiveIconYOffset;

        private TrayModel _trayModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private TileThemeSO _theme;
        private readonly List<Image> _slotBackgrounds = new();
        private readonly List<Image> _slotIcons = new();
        private readonly List<TileTypeId?> _lastKnownSlots = new();
        private Sequence[] _popSequences = System.Array.Empty<Sequence>();

        public void Initialize(TrayModel trayModel, TileThemeSO theme, IReadOnlyList<Sprite> fruitSpritesByType)
        {
            _trayModel = trayModel;
            _theme = theme;
            _fruitSpritesByType = fruitSpritesByType;
            CreateSlots();
            SnapToCurrentState();
        }

        /// <summary>
        /// Slots (count varies 6-12 per level) are always dynamic — there's no static
        /// "correct" count to hand-author. The TrayBar background, however, is left
        /// alone if a scene-authored one already exists (named "TrayBar", built once by
        /// Tools > Build Game Scenes and editable in the Inspector) so re-Initializing
        /// (Shuffle/Undo) doesn't destroy and rebuild it every time; only falls back to
        /// building one procedurally if it's missing.
        /// </summary>
        private void CreateSlots()
        {
            // PrimeTween disallows stopping one of a Sequence's nested tweens directly
            // (see TileView.CancelAllAnimations) — the per-slot pop Sequence must be
            // stopped as a Sequence before the plain Tween.StopAll sweep below, or an
            // interrupted pop (e.g. a Shuffle/Undo rebuild mid-animation) logs a
            // PrimeTween "OnComplete callback ignored" error.
            foreach (var sequence in _popSequences)
            {
                if (sequence.isAlive)
                {
                    sequence.Stop();
                }
            }

            var existingBar = transform.Find("TrayBar");

            foreach (Transform child in transform)
            {
                if (child == existingBar)
                {
                    continue;
                }

                // Stop any in-flight tween on this subtree before destroying it —
                // otherwise an interrupted tween (e.g. a slot icon's pop-in animation cut
                // short by a Shuffle/Undo rebuild) logs a PrimeTween "OnComplete callback
                // ignored" error when its target disappears out from under it. The
                // animated object is the nested Icon child, not the slot itself, so this
                // has to walk the whole subtree, not just the top-level child.
                foreach (var descendant in child.GetComponentsInChildren<Transform>(true))
                {
                    PrimeTween.Tween.StopAll(descendant);
                }
                Destroy(child.gameObject);
            }
            _slotBackgrounds.Clear();
            _slotIcons.Clear();
            _lastKnownSlots.Clear();
            _popSequences = new Sequence[_trayModel.Capacity];

            ComputeSlotMetrics();

            var totalWidth = (_trayModel.Capacity - 1) * _effectiveSlotSpacing;
            var barWidth = totalWidth + _effectiveSlotSpacing * 1.4f;

            if (existingBar != null)
            {
                var existingBarRect = (RectTransform)existingBar;
                existingBarRect.sizeDelta = new Vector2(barWidth, _effectiveSlotSize * 1.3f);
                existingBarRect.SetAsFirstSibling();
            }
            else if (_theme.TrayBarSprite != null)
            {
                var barGO = new GameObject("TrayBar", typeof(RectTransform));
                barGO.transform.SetParent(transform, false);
                var barRect = barGO.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(barWidth, _effectiveSlotSize * 1.3f);
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
                rectTransform.sizeDelta = new Vector2(_effectiveSlotSize, _effectiveSlotSize);
                rectTransform.anchoredPosition = new Vector2(startX + i * _effectiveSlotSpacing, 0f);

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
                iconRect.sizeDelta = new Vector2(_effectiveSlotSize * 0.65f, _effectiveSlotSize * 0.65f);
                iconRect.anchoredPosition = new Vector2(0f, _effectiveIconYOffset);
                var iconImage = iconGO.AddComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.enabled = false;

                _slotBackgrounds.Add(bgImage);
                _slotIcons.Add(iconImage);
                _lastKnownSlots.Add(null);
            }
        }

        /// <summary>
        /// Scales slot size/spacing/icon-offset down together, uniformly, only when the
        /// ideal (unscaled) width for this level's tray capacity would exceed
        /// _maxTrayWidth — everyday capacities (6-9) get the full-size tray, only a
        /// maxed-out 12-slot tray gets shrunk to still fit a 1080-wide portrait canvas.
        /// </summary>
        private void ComputeSlotMetrics()
        {
            var idealWidth = (_trayModel.Capacity - 1) * _slotSpacing + _slotSize;
            var scale = idealWidth > _maxTrayWidth ? _maxTrayWidth / idealWidth : 1f;

            _effectiveSlotSize = _slotSize * scale;
            _effectiveSlotSpacing = _slotSpacing * scale;
            _effectiveIconYOffset = _iconYOffset * scale;
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
        /// Diffs against the last known slot contents so newly-filled slots pop in and
        /// newly-cleared (matched) slots pop out, instead of just snapping visibility.
        /// <paramref name="completedMatch"/>, when present, marks exactly which slots
        /// just finished a run — including the slot that completed it this turn, whose
        /// insert and removal both happen (in TrayModel) before this Refresh call ever
        /// sees it, so a plain previous/current diff would never render it at all. Those
        /// slots get queued into a short staggered pop instead of vanishing instantly,
        /// so a multi-tile match reads as a quick beat rather than one flicker.
        /// </summary>
        public void Refresh(MatchResult? completedMatch = null)
        {
            HashSet<int> matchedSlots = null;
            if (completedMatch.HasValue)
            {
                matchedSlots = new HashSet<int>(completedMatch.Value.SlotIndices);
            }

            var poppingIndices = new List<int>();

            for (var i = 0; i < _trayModel.Capacity; i++)
            {
                var slotContent = _trayModel.Slots[i];
                var previous = _lastKnownSlots[i];
                var icon = _slotIcons[i];

                if (matchedSlots != null && matchedSlots.Contains(i))
                {
                    // This index is starting a brand new pop — if it's still mid-way
                    // through an earlier one (a fast follow-up match can complete again
                    // at the same compacted index before the previous pop finished
                    // animating), stop that one first so its later steps can't run
                    // against this new occupant.
                    StopStalePop(i, icon);

                    if (!icon.enabled)
                    {
                        icon.sprite = _fruitSpritesByType[completedMatch.Value.TileType.Value % _fruitSpritesByType.Count];
                        icon.enabled = true;
                        icon.transform.localScale = Vector3.one;
                    }
                    poppingIndices.Add(i);
                    _lastKnownSlots[i] = null;
                    continue;
                }

                if (slotContent.HasValue && !previous.HasValue)
                {
                    // Same reasoning as above: this index is about to show a freshly
                    // inserted tile, so any leftover pop animation on it must be
                    // stopped first, or its delayed "shrink to 0" would fire after this
                    // insert's "grow to 1" and silently hide the new tile.
                    StopStalePop(i, icon);

                    icon.sprite = _fruitSpritesByType[slotContent.Value.Value % _fruitSpritesByType.Count];
                    icon.enabled = true;
                    icon.transform.localScale = Vector3.zero;
                    Tween.Scale(icon.transform, 1f, 0.25f, Ease.OutBack);
                }
                else if (!slotContent.HasValue && previous.HasValue)
                {
                    poppingIndices.Add(i);
                }
                else if (slotContent.HasValue && previous.HasValue && slotContent.Value.Value != previous.Value.Value)
                {
                    // Defensive: shouldn't happen given compaction semantics, but keep visuals correct if it ever does.
                    icon.sprite = _fruitSpritesByType[slotContent.Value.Value % _fruitSpritesByType.Count];
                }

                _lastKnownSlots[i] = slotContent;
            }

            if (poppingIndices.Count > 0)
            {
                PlayMatchPopSequence(poppingIndices);
            }
        }

        /// <summary>
        /// Stops and resets a slot's in-flight pop sequence, if any — only called at the
        /// point an index is about to be reused for a new pop or a new insert. Calling
        /// this unconditionally for every index on every Refresh (an earlier version of
        /// this code did) forcibly cut off OTHER slots' still-legitimately-running pop
        /// animations the moment any unrelated click happened — Stop() doesn't invoke
        /// OnComplete, so those interrupted tiles never got their icon.enabled = false,
        /// leaving already-matched tiles stuck visible indefinitely.
        /// </summary>
        private void StopStalePop(int index, Image icon)
        {
            if (!_popSequences[index].isAlive)
            {
                return;
            }

            _popSequences[index].Stop();
            icon.transform.localScale = Vector3.one;
        }

        private const float PopStagger = 0.05f;
        private const float PopPunchDuration = 0.09f;
        private const float PopShrinkDuration = 0.16f;

        /// <summary>
        /// Semitone-ish step applied per pop within one match, so a multi-tile match
        /// plays as a short ascending run (like a xylophone glissando) instead of the
        /// same recording firing back to back — a rising cascade reads as "building
        /// toward a payoff," matching the match-3 genre's usual combo escalation.
        /// Capped further down so a big 5-tile match doesn't end up chipmunk-pitched.
        /// </summary>
        private const float PopPitchStep = 0.06f;
        private const float PopPitchMax = 1.35f;

        /// <summary>
        /// Each slot in <paramref name="indices"/> punches up then shrinks away,
        /// staggered by a short fixed delay per slot so a multi-tile match reads as a
        /// quick beat ("...and, and, and!") rather than everything vanishing on the same
        /// frame the last tile lands — short enough (well under half a second total)
        /// that it never feels like it's making the player wait.
        /// </summary>
        private void PlayMatchPopSequence(List<int> indices)
        {
            for (var k = 0; k < indices.Count; k++)
            {
                // Captured into its own per-iteration local — a `for` loop's own control
                // variable is shared across all iterations, so a closure referencing `k`
                // directly would see whatever it ends at (indices.Count) by the time
                // these delayed callbacks actually fire, not the value from its own turn.
                var popOrder = k;
                var index = indices[k];
                var icon = _slotIcons[index];
                var iconRect = (RectTransform)icon.transform;
                var startDelay = k * PopStagger;

                _popSequences[index] = Sequence.Create()
                    .ChainDelay(startDelay)
                    .Chain(Tween.Scale(iconRect, 1.2f, PopPunchDuration, Ease.OutQuad))
                    .Chain(Tween.Scale(iconRect, 0f, PopShrinkDuration, Ease.InBack))
                    .OnComplete(() =>
                    {
                        icon.enabled = false;
                        iconRect.localScale = Vector3.one;
                        UIParticleBurst.BurstSparkle(transform, iconRect.position, count: 12, duration: 0.45f, distance: 80f);
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
