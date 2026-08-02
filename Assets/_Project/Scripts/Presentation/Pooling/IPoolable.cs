namespace Presentation.Pooling
{
    /// <summary>
    /// Lifecycle hooks for a component managed by an object pool. Called explicitly by
    /// the pool rather than relying on OnEnable/OnDisable, so pooled objects can tell
    /// "reused from pool" apart from an ordinary Unity enable/disable (e.g. re-parenting,
    /// Inspector toggling in the Editor).
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called right after the pool activates and hands out this instance.</summary>
        void OnSpawned();

        /// <summary>Called right before the pool deactivates and reclaims this instance.</summary>
        void OnDespawned();
    }
}
