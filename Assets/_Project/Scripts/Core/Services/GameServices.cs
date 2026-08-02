using Core.EventBus;

namespace Core.Services
{
    /// <summary>
    /// Static accessor for app-lifetime infrastructure singletons ONLY: the event bus,
    /// save, audio, addressables, analytics, and haptics. This is intentionally not a
    /// general-purpose service container or a DI replacement.
    ///
    /// Gameplay-scoped objects (BoardController, TileController, MatchService,
    /// PoolService, ...) are constructed explicitly by SceneRoot and handed their
    /// dependencies directly — they must never be resolved through GameServices. If
    /// you're about to add gameplay state here (e.g. "current board"), stop — see
    /// ARCHITECTURE.md §5.1 and §16 risk #2.
    /// </summary>
    public static class GameServices
    {
        public static IEventBus EventBus { get; private set; }
        public static ISaveService Save { get; private set; }
        public static IAudioService Audio { get; private set; }
        public static IAddressablesService Content { get; private set; }
        public static IAnalyticsService Analytics { get; private set; }
        public static IHapticsService Haptics { get; private set; }

        public static bool IsRegistered { get; private set; }

        /// <summary>
        /// Called exactly once by GameRoot during boot, in a fixed order. Never call
        /// this from gameplay or UI code.
        /// </summary>
        internal static void Register(
            IEventBus eventBus,
            ISaveService save,
            IAudioService audio,
            IAddressablesService content,
            IAnalyticsService analytics,
            IHapticsService haptics)
        {
            EventBus = eventBus;
            Save = save;
            Audio = audio;
            Content = content;
            Analytics = analytics;
            Haptics = haptics;
            IsRegistered = true;
        }

        /// <summary>
        /// Test-only reset hook so EditMode/PlayMode tests don't leak state between
        /// runs. Not for use outside the Tests assembly.
        /// </summary>
        internal static void ResetForTests()
        {
            EventBus = null;
            Save = null;
            Audio = null;
            Content = null;
            Analytics = null;
            Haptics = null;
            IsRegistered = false;
        }
    }
}
