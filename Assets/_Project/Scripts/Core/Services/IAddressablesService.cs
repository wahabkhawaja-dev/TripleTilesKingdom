using System.Threading.Tasks;
using UnityEngine;

namespace Core.Services
{
    /// <summary>
    /// Thin wrapper over Unity Addressables. Deliberately game-agnostic — level/theme
    /// loading orchestration (which keys to load, in what order) belongs in
    /// LevelLoader / ThemeService, not here.
    /// </summary>
    public interface IAddressablesService
    {
        bool IsInitialized { get; }
        Task InitializeAsync();
        Task<T> LoadAssetAsync<T>(string key) where T : Object;
        void Release(Object asset);
    }
}
