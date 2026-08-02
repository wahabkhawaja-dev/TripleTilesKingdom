using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation.Pooling
{
    /// <summary>
    /// Owns every named pool for a single gameplay scene. Constructed and disposed by
    /// SceneRoot — deliberately NOT registered in GameServices, since pools are
    /// scene-scoped by design (a fresh set per level, released on teardown). See
    /// ARCHITECTURE.md §5.3.
    ///
    /// Pools are keyed by an explicit string id (e.g. a tile skin's Addressables key,
    /// "MatchParticle", "FloatingScoreText") rather than by System.Type, since multiple
    /// visually distinct prefabs can share one component type — every themed tile skin
    /// is a TileController, but each theme needs its own pool of instances.
    /// </summary>
    public sealed class PoolService : IDisposable
    {
        private readonly Dictionary<string, object> _poolsById = new Dictionary<string, object>(16);
        private readonly Transform _root;

        public PoolService(Transform sceneRootTransform)
        {
            var container = new GameObject("PooledObjects_Inactive");
            container.transform.SetParent(sceneRootTransform, false);
            _root = container.transform;
        }

        /// <summary>
        /// Returns the existing pool for <paramref name="poolId"/>, or creates one
        /// using <paramref name="factory"/> and optionally prewarms it. Safe to call
        /// repeatedly with the same id — subsequent calls ignore factory/prewarmCount
        /// and just return the existing pool.
        /// </summary>
        public IObjectPool<T> GetOrCreatePool<T>(string poolId, Func<T> factory, int prewarmCount = 0) where T : Component
        {
            if (_poolsById.TryGetValue(poolId, out var existing))
            {
                return (IObjectPool<T>)existing;
            }

            var pool = new GenericObjectPool<T>(factory, _root, Mathf.Max(prewarmCount, 4));
            if (prewarmCount > 0)
            {
                pool.Prewarm(prewarmCount);
            }

            _poolsById[poolId] = pool;
            return pool;
        }

        /// <summary>
        /// Called once by SceneRoot on teardown. Destroys the container holding every
        /// currently-INACTIVE pooled instance and clears bookkeeping.
        ///
        /// Known limitation: instances that are still active (e.g. a tile mid-flight
        /// into the tray when the player quits mid-animation) are not tracked here —
        /// they're expected to be destroyed either by their owning controller
        /// (BoardController releasing everything it spawned) or by the scene unload
        /// itself. Documented in DECISIONS.md rather than silently assumed.
        /// </summary>
        public void Dispose()
        {
            _poolsById.Clear();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }
    }
}
