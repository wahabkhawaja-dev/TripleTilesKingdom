using System.Collections.Generic;
using Domain.Board;
using Domain.Levels;

namespace Presentation
{
    /// <summary>
    /// Converts a LevelModel's flat TileSpawnData layout into a fully-wired BoardModel:
    /// assigns TileIds, resolves blocker/covers relationships via StackingResolver, then
    /// constructs the BoardModel. This is the level-loading adapter referenced by
    /// StackingResolver's own doc comment ("will be called by BoardGenerator once the
    /// Level System exists") — Domain intentionally stops at TileSpawnData and leaves
    /// this wiring to whoever owns level loading, which today is Presentation.
    /// </summary>
    public static class BoardGenerator
    {
        public static BoardModel CreateBoard(LevelModel level)
        {
            var idFactory = new TileIdFactory();
            var tiles = new List<TileModel>(level.TileLayout.Count);

            foreach (var spawn in level.TileLayout)
            {
                tiles.Add(new TileModel(idFactory.Next(), spawn.TileType, spawn.Coordinate));
            }

            StackingResolver.ResolveStacking(tiles);

            return new BoardModel(tiles);
        }
    }
}
