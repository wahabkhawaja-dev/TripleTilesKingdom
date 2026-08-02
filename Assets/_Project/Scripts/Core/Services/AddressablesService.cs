using System.Threading.Tasks;
using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Placeholder IAddressablesService. The project has not integrated the
    /// Addressables package yet — nothing loads content through it, so pulling in that
    /// dependency now would be premature. This keeps the seam (interface) in place so
    /// swapping in a real Addressables-backed implementation later, once the Level
    /// System actually needs to load bundled/remote content (ARCHITECTURE.md §8, §12),
    /// is a one-line change in GameRoot with zero call-site impact. See DECISIONS.md.
    /// </summary>
    public sealed class AddressablesService : IAddressablesService
    {
        public bool IsInitialized { get; private set; }

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<T> LoadAssetAsync<T>(string key) where T : Object
        {
            Debug.LogWarning($"[AddressablesService] LoadAssetAsync<{typeof(T).Name}>(\"{key}\") called before a real content pipeline is wired — returning null.");
            return Task.FromResult<T>(null);
        }

        public void Release(Object asset)
        {
        }
    }
}
