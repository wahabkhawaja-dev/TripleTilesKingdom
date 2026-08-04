using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Board;
using UnityEngine;

namespace Presentation
{
    /// <summary>
    /// World-space controller for the sprite tile board. Positions tiles by
    /// (X, Y, Layer) in world units on plain <see cref="Transform"/>s; draw order
    /// is deterministic per-layer <c>sortingOrder</c> on
    /// <see cref="SpriteRenderer"/> (see <see cref="TileView.ApplySortingOrder"/>).
    /// The fly-to-tray handoff is initiated from here by <see cref="FlyTileToSlot"/>
    /// — GameFlowController supplies the tray slot's world position and icon scale,
    /// so the tile lands at the exact pixel where the tray icon will take over.
    /// </summary>
    public sealed class BoardController : MonoBehaviour
    {
        [Header("Layout (world units)")]
        [SerializeField] private float _tileWorldSize = 1f;
        [SerializeField] private float _tileSpacing = 1f;
        [Tooltip("Purely cosmetic per-layer Y lift so the pyramid tapers visibly. Sorting is by sortingOrder band, not Y or Z.")]
        [SerializeField] private float _layerLift = 0.14f;

        [Header("Fly layer (auto-created if empty)")]
        [SerializeField] private Transform _flyLayer;

        /// <summary>Well above every board layer band (max ≈ layer × 100 + 99)
        /// AND above the tray bar (2000). Anything mid-fly always draws on top.</summary>
        private const int FlyingSortingOrder = 3000;

        private static GameObject _tilePrefab;

        private BoardModel _boardModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private Sprite _baseTileSprite;
        private Dictionary<TileId, TileView> _tileViews = new();
        private HashSet<TileId> _animatingOut = new();
        private Action<TileId> _onTileSelected;
        private Vector3 _boardOrigin;

        public void Initialize(
            BoardModel boardModel,
            Sprite baseTileSprite,
            IReadOnlyList<Sprite> fruitSpritesByType,
            Action<TileId> onTileSelected)
        {
            foreach (var view in _tileViews.Values)
            {
                if (view != null)
                {
                    view.CancelAllAnimations();
                    Destroy(view.gameObject);
                }
            }
            _tileViews.Clear();
            _animatingOut.Clear();

            _boardModel = boardModel;
            _baseTileSprite = baseTileSprite;
            _fruitSpritesByType = fruitSpritesByType;
            _onTileSelected = onTileSelected;
            _boardOrigin = ComputeBoardOrigin();

            EnsureFlyLayer();

            foreach (var tile in _boardModel.AllTiles.OrderBy(t => t.Coordinate.Layer))
            {
                CreateTileView(tile);
            }
        }

        private void EnsureFlyLayer()
        {
            if (_flyLayer != null) return;
            var go = new GameObject("FlyLayer");
            var parent = transform.parent != null ? transform.parent : transform;
            go.transform.SetParent(parent, false);
            _flyLayer = go.transform;
        }

        private Vector3 ComputeBoardOrigin()
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var tile in _boardModel.AllTiles)
            {
                var c = tile.Coordinate;
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }
            if (minX > maxX) return Vector3.zero;

            var width = (maxX - minX) * _tileSpacing;
            var height = (maxY - minY) * _tileSpacing;
            return new Vector3(-width / 2f, height / 2f, 0f);
        }

        /// <summary>
        /// Half-cell-per-layer shift so a layer-(L+1) tile visually nests over the
        /// four layer-L tiles it covers. Extra per-layer Y lift is purely cosmetic
        /// peek-through — draw order is handled by sortingOrder, not Y.
        /// </summary>
        private Vector3 GetLocalPositionFor(BoardCoordinate c)
        {
            var half = c.Layer * 0.5f;
            var lift = c.Layer * _layerLift;
            return new Vector3(
                (c.X + half) * _tileSpacing + _boardOrigin.x,
                -(c.Y + half) * _tileSpacing + _boardOrigin.y + lift,
                0f);
        }

        private void CreateTileView(TileModel tile)
        {
            var prefab = GetTilePrefab();
            var instance = Instantiate(prefab, transform);
            instance.transform.localPosition = GetLocalPositionFor(tile.Coordinate);
            instance.transform.localScale = Vector3.one * _tileWorldSize;

            var view = instance.GetComponent<TileView>();
            var fruitSprite = _fruitSpritesByType[tile.TileType.Value % _fruitSpritesByType.Count];
            view.Initialize(tile, _baseTileSprite, fruitSprite, OnTileViewClicked);

            _tileViews[tile.Id] = view;
        }

        private void OnTileViewClicked(TileId tileId) => _onTileSelected?.Invoke(tileId);

        public bool TryGetTileView(TileId id, out TileView view) => _tileViews.TryGetValue(id, out view);

        public Vector3 GetTileWorldPosition(TileId id) =>
            _tileViews.TryGetValue(id, out var view) ? view.transform.position : transform.position;

        /// <summary>
        /// Fly the tile with <paramref name="id"/> to <paramref name="targetWorldPos"/>
        /// at <paramref name="targetWorldScale"/>. On landing, calls
        /// <paramref name="onLanded"/> then destroys the flying tile.
        /// </summary>
        public void FlyTileToSlot(TileId id, Vector3 targetWorldPos, Vector3 targetWorldScale, Action onLanded)
        {
            if (!_tileViews.TryGetValue(id, out var view))
            {
                onLanded?.Invoke();
                return;
            }

            _animatingOut.Add(id);
            view.FlyToSlot(targetWorldPos, targetWorldScale, _flyLayer, FlyingSortingOrder, () =>
            {
                onLanded?.Invoke();
                _animatingOut.Remove(id);
                _tileViews.Remove(id);
                if (view != null) Destroy(view.gameObject);
            });
        }

        /// <summary>
        /// Refresh only updates covered/exposed visuals on tiles still standing;
        /// removals are handled by <see cref="FlyTileToSlot"/>, not here.
        /// </summary>
        public void Refresh()
        {
            foreach (var tile in _boardModel.AllTiles.OrderBy(t => t.Coordinate.Layer))
            {
                if (tile.State == TileState.Removed || _animatingOut.Contains(tile.Id)) continue;
                if (_tileViews.TryGetValue(tile.Id, out var view)) view.UpdateVisuals();
            }
        }

        public void ShowWinAnimation()
        {
            foreach (var view in _tileViews.Values) if (view != null) view.PlayWinAnimation();
        }

        public void ShowLoseAnimation()
        {
            foreach (var view in _tileViews.Values) if (view != null) view.PlayLoseAnimation();
        }

        public void PulseTile(TileId id)
        {
            if (_tileViews.TryGetValue(id, out var view)) view.PlayHintPulse();
        }

        private static GameObject GetTilePrefab()
        {
            if (_tilePrefab == null) _tilePrefab = Resources.Load<GameObject>("Prefabs/TileView");
            return _tilePrefab;
        }
    }
}
