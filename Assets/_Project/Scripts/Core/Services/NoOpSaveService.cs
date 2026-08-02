using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Placeholder ISaveService wired by GameRoot until the real local-save system
    /// lands (build order step 10, ARCHITECTURE.md §13). Keeps the boot sequence fully
    /// wireable today; every consumer depends on ISaveService, so swapping this for the
    /// real implementation later is a one-line change in GameRoot.
    /// </summary>
    public sealed class NoOpSaveService : ISaveService
    {
        public T Load<T>(string key, T fallback)
        {
            Debug.LogWarning($"[NoOpSaveService] Load<{typeof(T).Name}>(\"{key}\") called before a real ISaveService is wired — returning fallback.");
            return fallback;
        }

        public void Save<T>(string key, T value)
        {
        }

        public void Flush()
        {
        }
    }
}
