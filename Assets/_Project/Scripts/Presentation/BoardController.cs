using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Board;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// Manages the visual representation of the board. Must be attached to a
    /// RectTransform under a Canvas — tiles are uGUI elements positioned with
    /// anchoredPosition, not world-space transforms. Instantiates the TileView prefab
    /// per tile, handles visual updates, and forwards selection events to
    /// GameFlowController.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardController : MonoBehaviour
    {
        [SerializeField] private float _tileSize = 175f;

        /// <summary>
        /// Equal to tile size (tiles touch edge-to-edge, no gap) — a visible gap between
        /// same-layer tiles read as a bug, not a design choice.
        /// </summary>
        [SerializeField] private float _tileSpacing = 175f;

        /// <summary>
        /// Uniform per-layer upward nudge. Each higher layer sits a fixed amount above
        /// the layer below it — not the geometrically "correct" half-cell shift that
        /// would center a layer-(L+1) tile over the four layer-L tiles it covers — the
        /// reference feel is a tight peek-out stack (each layer visible by only a few
        /// pixels below the one above), not tiles spread half a cell apart. Combined
        /// with per-layer centering (see ComputeLayerCenterOffsets), this reads as a
        /// pyramid rising to a peak rather than a stack fanned off to one side.
        /// </summary>
        [SerializeField] private float _layerStackOffset = 20f;

        private static GameObject _tileViewPrefab;

        private BoardModel _boardModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private Dictionary<TileId, TileView> _tileViews = new();
        private HashSet<TileId> _animatingOut = new();
        private Action<TileId> _onTileSelected;
        private Vector2 _centerOffset;
        private Vector2 _flyToTrayTarget;

        /// <summary>
        /// <paramref name="flyToTrayTargetLocalPos"/> is TrayRoot's anchoredPosition
        /// converted into this container's local space (both are direct siblings under
        /// the same Canvas, so it's just a coordinate delta) — the point tiles animate
        /// toward when tapped, before their TileView is destroyed. Safe to call again on
        /// an already-initialized board (used by Undo/Shuffle) — clears any existing
        /// views first rather than assuming a clean slate.
        /// </summary>
        public void Initialize(BoardModel boardModel, IReadOnlyList<Sprite> fruitSpritesByType, Vector2 flyToTrayTargetLocalPos, Action<TileId> onTileSelected)
        {
            foreach (var view in _tileViews.Values)
            {
                if (view != null)
                {
                    // Stop in-flight tweens (click punch, fly-to-tray, hint pulse) before
                    // destroying — an interrupted tween logs a PrimeTween "OnComplete
                    // callback ignored" error otherwise, e.g. when Undo/Shuffle rebuilds
                    // mid-animation.
                    view.CancelAllAnimations();
                    Destroy(view.gameObject);
                }
            }
            _tileViews.Clear();
            _animatingOut.Clear();

            _boardModel = boardModel;
            _fruitSpritesByType = fruitSpritesByType;
            _flyToTrayTarget = flyToTrayTargetLocalPos;
            _onTileSelected = onTileSelected;
            _centerOffset = ComputeCenterOffset();

            // Ascending layer order so later (higher-layer / stacked-on-top) tiles are
            // instantiated last and therefore render in front, matching sibling-order
            // draw order in uGUI — no separate sorting pass needed.
            foreach (var tile in _boardModel.AllTiles.OrderBy(t => t.Coordinate.Layer))
            {
                CreateTileView(tile);
            }
        }

        /// <summary>
        /// A single shared offset (based on the full board's bounds — effectively layer
        /// 0's, since it's always the widest) applied to every layer alike. This has to
        /// stay a SINGLE shared mapping rather than centering each layer independently —
        /// StackingResolver.ResolvePyramidStacking computes which layer-L tiles a
        /// layer-(L+1) tile covers from raw shared (x, y) coordinates (that tile covers
        /// (x,y), (x+1,y), (x,y+1), (x+1,y+1) one layer down); giving each layer its own
        /// recentered offset breaks that correspondence, so a tile's rendered position no
        /// longer lines up with what it's actually stacked over — tiles read as randomly
        /// scattered relative to each other instead of nesting into a pyramid. The
        /// tradeoff is that odd-width layers can land up to half a cell off the exact
        /// center (integer coordinates can't split a shrink-by-one span evenly) — a much
        /// smaller cosmetic issue than tiles not visually nesting over what they cover.
        /// </summary>
        private Vector2 ComputeCenterOffset()
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            foreach (var tile in _boardModel.AllTiles)
            {
                var c = tile.Coordinate;
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            if (minX > maxX)
            {
                return Vector2.zero;
            }

            var width = (maxX - minX) * _tileSpacing;
            var height = (maxY - minY) * _tileSpacing;
            return new Vector2(-width / 2f, height / 2f);
        }

        private void CreateTileView(TileModel tile)
        {
            var prefab = GetTileViewPrefab();
            var instance = Instantiate(prefab, transform);
            var view = instance.GetComponent<TileView>();

            var rect = (RectTransform)instance.transform;
            rect.sizeDelta = new Vector2(_tileSize, _tileSize);
            rect.anchoredPosition = GetAnchoredPositionForTile(tile.Coordinate);

            var fruitSprite = _fruitSpritesByType[tile.TileType.Value % _fruitSpritesByType.Count];
            view.Initialize(tile, fruitSprite, OnTileViewClicked);

            _tileViews[tile.Id] = view;
        }

        /// <summary>
        /// Every layer shares the same coordinate-to-pixel mapping (see
        /// ComputeCenterOffset) — a layer-(L+1) tile at (x, y) always renders directly
        /// over the layer-L tiles at (x,y), (x+1,y), (x,y+1), (x+1,y+1) it actually
        /// covers. The only per-layer difference is a uniform upward lift, purely a
        /// rendering peek-through effect (doesn't shift X, so it can't affect which
        /// tiles visually nest over which) — higher layers rise toward a peak while the
        /// base they're centered on stays put underneath.
        ///
        /// LevelGenerator's minX/minY = layer/2 (integer division) truncates the .5
        /// remainder for odd layer numbers, under-insetting each such layer's left and
        /// top edges by half a cell relative to true center. On Y that reads as the
        /// intended "peeking from below" look (more base exposed at the bottom, not the
        /// top — the desired -Y view). On X it reads as a spurious sideways lean (more
        /// base exposed on the right, as if viewed from that side) — xCorrection cancels
        /// exactly that half-cell, and only that, re-centering X without touching Y or
        /// the underlying domain coordinates StackingResolver depends on.
        /// </summary>
        private Vector2 GetAnchoredPositionForTile(BoardCoordinate coord)
        {
            var lift = coord.Layer * _layerStackOffset;
            var xCorrection = (coord.Layer / 2f - coord.Layer / 2) * _tileSpacing;
            return new Vector2(
                coord.X * _tileSpacing + _centerOffset.x + xCorrection,
                -coord.Y * _tileSpacing + _centerOffset.y + lift);
        }

        private void OnTileViewClicked(TileId tileId)
        {
            _onTileSelected?.Invoke(tileId);
        }

        /// <summary>
        /// Reconciles views with model state: tiles that left the board fly to the tray
        /// and destroy themselves on arrival (see TileView.PlayFlyToTrayAndDestroy),
        /// creates views for any not yet represented, and refreshes visuals (exposure/
        /// selectability) on everything still standing — a tile can become selectable
        /// this turn without itself being newly created or removed.
        /// </summary>
        public void Refresh()
        {
            var toAnimateOut = new List<TileId>();
            foreach (var kvp in _tileViews)
            {
                if (_animatingOut.Contains(kvp.Key))
                {
                    continue;
                }

                if (!_boardModel.TryGetTile(kvp.Key, out var tile) || tile.State == TileState.Removed)
                {
                    toAnimateOut.Add(kvp.Key);
                }
            }

            foreach (var id in toAnimateOut)
            {
                _animatingOut.Add(id);
                var view = _tileViews[id];
                view.PlayFlyToTrayAndDestroy(_flyToTrayTarget, () =>
                {
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                    }
                    _tileViews.Remove(id);
                    _animatingOut.Remove(id);
                });
            }

            // Ascending layer order here too — matches Initialize. Any tile still
            // needing its view created for the first time (defensive; every tile
            // normally gets one at Initialize) must still land in the correct
            // front-to-back draw order, not just get appended after whatever already
            // exists regardless of its actual layer.
            foreach (var tile in _boardModel.AllTiles.OrderBy(t => t.Coordinate.Layer))
            {
                if (tile.State == TileState.Removed || _animatingOut.Contains(tile.Id))
                    continue;

                if (_tileViews.TryGetValue(tile.Id, out var view))
                {
                    view.UpdateVisuals();
                }
                else
                {
                    CreateTileView(tile);
                }
            }
        }

        public void ShowWinAnimation()
        {
            foreach (var view in _tileViews.Values)
            {
                view.PlayWinAnimation();
            }
        }

        public void ShowLoseAnimation()
        {
            foreach (var view in _tileViews.Values)
            {
                view.PlayLoseAnimation();
            }
        }

        /// <summary>Used by the Hint power-up to draw attention to a specific tile.</summary>
        public void PulseTile(TileId id)
        {
            if (_tileViews.TryGetValue(id, out var view))
            {
                view.PlayHintPulse();
            }
        }

        private static GameObject GetTileViewPrefab()
        {
            if (_tileViewPrefab == null)
            {
                _tileViewPrefab = Resources.Load<GameObject>("Prefabs/TileView");
            }
            return _tileViewPrefab;
        }
    }
}
