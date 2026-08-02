using System.Threading.Tasks;
using Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventBusImpl = Core.EventBus.EventBus;

namespace Core.Bootstrap
{
    /// <summary>
    /// App-lifetime bootstrapper. Lives in Bootstrap.unity (the game's first scene),
    /// initializes every infrastructure service in a fixed order, registers them with
    /// GameServices, persists across scene loads via DontDestroyOnLoad, then hands off
    /// to the first real scene. See ARCHITECTURE.md §4 for the full boot flow and the
    /// reasoning behind a dedicated Bootstrap scene.
    ///
    /// This is the ONLY place infrastructure services are constructed. Gameplay scenes
    /// must never construct a new AudioService/SaveService/etc. themselves — they read
    /// GameServices instead.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private string _firstSceneName = "MainMenu";

        private static GameRoot _instance;

        private async void Awake()
        {
            if (_instance != null)
            {
                // Bootstrap scene was re-entered (e.g. Editor "play from any scene").
                // The persistent GameRoot already exists; this one is a duplicate.
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            await InitializeServicesAsync();

            SceneManager.LoadScene(_firstSceneName);
        }

        /// <summary>
        /// Construction order is deliberate:
        /// 1. Save — local/synchronous, no external dependency.
        /// 2. Content — placeholder today (no Addressables integration yet); kept first
        ///    since everything content-related will depend on it once it's real.
        /// 3. Audio / Analytics / Haptics / EventBus — no dependency on each other, so
        ///    they're safe to construct last.
        ///
        /// Save, Audio, and Content are currently no-op placeholders; see
        /// NoOpSaveService, NoOpAudioService, and AddressablesService for why, and
        /// ARCHITECTURE.md §19 / ROADMAP.md for when real implementations land.
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            var saveService = new NoOpSaveService();

            var addressablesService = new AddressablesService();
            await addressablesService.InitializeAsync();

            var audioService = new NoOpAudioService();
            var analyticsService = new NoOpAnalyticsService();
            var hapticsService = new HapticsService();
            var eventBus = new EventBusImpl();

            GameServices.Register(
                eventBus,
                saveService,
                audioService,
                addressablesService,
                analyticsService,
                hapticsService);
        }
    }
}
