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
        [SerializeField] private float _tileSize = 100f;
        [SerializeField] private float _tileSpacing = 108f;

        /// <summary>Per-layer pixel offset so stacked tiles read visually as a stack (matches the reference mockup) instead of perfectly overlapping.</summary>
        [SerializeField] private float _layerStackOffset = 14f;

        private static GameObject _tileViewPrefab;

        private BoardModel _boardModel;
        private IReadOnlyList<Sprite> _fruitSpritesByType;
        private Dictionary<TileId, TileView> _tileViews = new();
        private Action<TileId> _onTileSelected;
        private Vector2 _centerOffset;

        public void Initialize(BoardModel boardModel, IReadOnlyList<Sprite> fruitSpritesByType, Action<TileId> onTileSelected)
        {
            _boardModel = boardModel;
            _fruitSpritesByType = fruitSpritesByType;
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
        /// Column/row extents span [0, N) with no negative coordinates, so raw
        /// coord*spacing places the whole board to the lower-right of the anchor
        /// instead of centered on it. Shift everything back by half the board's extent.
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

        private Vector2 GetAnchoredPositionForTile(BoardCoordinate coord)
        {
            var stack = coord.Layer * _layerStackOffset;
            return new Vector2(
                coord.X * _tileSpacing + _centerOffset.x + stack,
                -coord.Y * _tileSpacing + _centerOffset.y + stack);
        }

        private void OnTileViewClicked(TileId tileId)
        {
            _onTileSelected?.Invoke(tileId);
        }

        /// <summary>
        /// Reconciles views with model state: destroys views for tiles that left the
        /// board, creates views for any not yet represented, and refreshes visuals
        /// (exposure/selectability) on everything still standing — a tile can become
        /// selectable this turn without itself being newly created or removed.
        /// </summary>
        public void Refresh()
        {
            var toRemove = new List<TileId>();
            foreach (var kvp in _tileViews)
            {
                if (!_boardModel.TryGetTile(kvp.Key, out var tile) || tile.State == TileState.Removed)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                Destroy(_tileViews[id].gameObject);
                _tileViews.Remove(id);
            }

            foreach (var tile in _boardModel.AllTiles)
            {
                if (tile.State == TileState.Removed)
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
