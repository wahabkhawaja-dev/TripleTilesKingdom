namespace Core.Services
{
    /// <summary>
    /// Local persistence for progress, settings, and unlocks. Gameplay/meta systems own
    /// their own serializable state and just call Save/Load — this service doesn't know
    /// what a "level" or "tile" is. See ARCHITECTURE.md §13.
    /// </summary>
    public interface ISaveService
    {
        T Load<T>(string key, T fallback);
        void Save<T>(string key, T value);

        /// <summary>Explicit, batched write-to-disk. Not called automatically per Save().</summary>
        void Flush();
    }
}
