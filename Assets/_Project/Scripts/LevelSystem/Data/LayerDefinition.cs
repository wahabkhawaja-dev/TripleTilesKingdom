using System;
using UnityEngine;

namespace LevelSystem.Data
{
    /// <summary>
    /// One rectangular grid layer of a pyramid level. Each layer is a fully-populated
    /// Width x Height block placed at <see cref="Offset"/> (in cell coordinates,
    /// relative to the shared board origin at layer 0). AutoCenter tells the builder
    /// to center this layer over the previous one and ignore <see cref="Offset"/>.
    ///
    /// Pyramid stacking rule (see Domain.Board.StackingResolver.ResolvePyramidStacking):
    /// a tile at layer L+1 (x, y) covers the four layer-L tiles at (x, y), (x+1, y),
    /// (x, y+1), (x+1, y+1). Therefore for a layer with size (w, h) sitting on top of a
    /// layer with size (W, H), we need w &lt;= W - 1 AND h &lt;= H - 1, and its offset
    /// must fit inside the base. The builder validates and clamps this at build time.
    /// </summary>
    [Serializable]
    public struct LayerDefinition
    {
        [Min(1)] public int Width;
        [Min(1)] public int Height;

        [Tooltip("Cell offset from board origin. Ignored when AutoCenter is on.")]
        public Vector2Int Offset;

        [Tooltip("Center this layer over the previous layer automatically.")]
        public bool AutoCenter;

        public int TileCount => Width * Height;

        public static LayerDefinition Rect(int width, int height, bool autoCenter = true) =>
            new LayerDefinition { Width = width, Height = height, AutoCenter = autoCenter, Offset = Vector2Int.zero };
    }
}
