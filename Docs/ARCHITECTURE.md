# ARCHITECTURE.md

**Project:** Triple Tile Match (working title)
**Engine:** Unity 6 (URP, Portrait, Mobile: Android & iOS)
**Status:** Foundational architecture — pending your approval before any implementation begins
**Last updated:** 2026-08-01

This document is the single source of truth for how the game is built. It is meant to be read by both humans and future AI sessions, so it stays explicit about *why*, not just *what*.

---

## 1. Guiding Principles

Before the diagrams, the rules we're optimizing for, in priority order:

1. **Clean Architecture / SOLID** — every class has one reason to change.
2. **Performance on low-end Android** — this genre lives and dies on devices from 2018–2020. Zero per-frame allocations in hot paths, no `FindObjectOfType`, no runtime reflection in gameplay code.
3. **Scalability** — the systems below must work identically whether the game has 20 levels or 5,000.
4. **Maintainability & Extensibility** — new tile types, new board shapes, new meta systems must be addable without touching existing, shipped code (Open/Closed Principle in practice).
5. **Game feel** — juice is a first-class system, not an afterthought bolted onto gameplay code.
6. **Readability** — a new engineer should understand a system by reading its folder, not by asking someone.

**On Dependency Injection:** per your direction, we are *not* using a DI container (no Zenject/VContext/Reflex). Instead we use **explicit composition**: a small number of well-defined root objects (`GameRoot`, `SceneRoot`) construct systems in a deterministic order and hand out references directly through constructors or `Initialize()` calls. This keeps the object graph visible and debuggable — you can `Ctrl+Click` from a manager to see exactly who constructed it, with no container magic. We use a lightweight **Service Locator variant** (`GameServices`, detailed in §5) only for a small set of true cross-cutting singletons (Audio, Save, Analytics, Event Bus), never for gameplay objects.

---

## 2. High-Level System Map

```
┌─────────────────────────────────────────────────────────────────────┐
│                              GameRoot                                 │
│  (persistent, DontDestroyOnLoad — boots once, owns app-lifetime svcs) │
└───────────────┬─────────────────────────────────────────────────────┘
                │ constructs & registers
                ▼
        ┌───────────────────┐
        │   GameServices      │  (static accessor, NOT a container)
        │  - EventBus          │
        │  - SaveService       │
        │  - AudioService       │
        │  - AddressablesService│
        │  - AnalyticsService   │
        │  - HapticsService      │
        └───────────────────┘
                │
                ▼
        ┌────────────────────────────────────────┐
        │              Scene: Gameplay              │
        │                                             │
        │   SceneRoot (per-scene composition point)   │
        │        │                                     │
        │        ├─► LevelLoader ──► LevelDefinitionSO  │
        │        ├─► BoardGenerator ──► BoardModel        │
        │        ├─► BoardController ──► TileController[]  │
        │        ├─► TrayController ──► TraySlotModel[]     │
        │        ├─► MatchService                             │
        │        ├─► ObjectPoolService (tiles, VFX, popups)     │
        │        ├─► GameFlowController (win/lose/pause)          │
        │        ├─► UIRoot (HUD, Popups, Pause)                    │
        │        └─► JuiceDirector (camera fx, screenshake, combo fx)│
        └────────────────────────────────────────┘
```

Two composition tiers:
- **`GameRoot`** — lives for the app's lifetime, boots infrastructure services once (audio, save, addressables, analytics), persists across scene loads.
- **`SceneRoot`** — lives per gameplay scene, wires up everything gameplay-specific fresh each level, and is fully torn down (unsubscribed, pooled objects released) on scene exit. This is what prevents the classic "stale event subscription" memory leak class of bugs.

---

## 3. Folder Structure

```
Assets/
├── _Project/
│   ├── Art/
│   │   ├── Tiles/                     # per-theme tile sprite sets
│   │   ├── UI/
│   │   ├── VFX/
│   │   └── Backgrounds/
│   ├── Audio/
│   │   ├── SFX/
│   │   └── Music/
│   ├── Fonts/
│   ├── Prefabs/
│   │   ├── Gameplay/                  # Tile, TraySlot, Board
│   │   ├── UI/
│   │   └── VFX/
│   ├── ScriptableObjects/
│   │   ├── Levels/                    # LevelDefinitionSO assets
│   │   ├── Themes/                    # ThemeSO assets
│   │   ├── TileTypes/                 # TileTypeSO assets
│   │   ├── ObstacleTypes/             # ObstacleDefinitionSO assets
│   │   └── Config/                    # GameplayConfigSO, PoolConfigSO, etc.
│   ├── Scenes/
│   │   ├── Bootstrap.unity            # loads GameRoot, then routes to MainMenu
│   │   ├── MainMenu.unity
│   │   └── Gameplay.unity
│   └── Scripts/
│       ├── Core/                      # GameRoot, GameServices, app lifecycle
│       │   ├── Bootstrap/
│       │   ├── Services/
│       │   └── EventBus/
│       ├── Domain/                    # Pure C#, zero UnityEngine dependency — see §6a
│       │   ├── Board/                 # BoardCoordinate, TileId/TileTypeId, TileModel, BoardModel, StackingResolver
│       │   ├── Tray/                  # TrayModel, ITrayView
│       │   ├── Matching/              # MatchSystem, IMatchRule, ExactCountMatchRule, MatchResult
│       │   ├── Obstacles/             # IObstacleState, NullObstacleState (concrete obstacles: build order step 11)
│       │   ├── Levels/                # LevelModel, TileSpawnData, LevelRuleSet
│       │   └── State/                 # BoardState, BoardStateMachine
│       ├── Gameplay/
│       │   ├── Board/                 # BoardController — MonoBehaviour view/controller only, wraps a Domain.Board.BoardModel
│       │   ├── Tiles/                 # TileController, TileView
│       │   ├── Tray/                  # TrayController, TrayView — wraps a Domain.Tray.TrayModel
│       │   ├── Obstacles/             # IObstacleBehaviour view-side counterparts + implementations
│       │   ├── Flow/                  # GameFlowController, WinLoseEvaluator — drives Domain.State.BoardStateMachine
│       │   └── Input/                 # TileInputController (New Input System)
│       ├── LevelSystem/
│       │   ├── Data/                  # LevelDefinitionSO and sub-assets
│       │   ├── Generation/            # ImageToBoardGenerator, ManualLayoutGenerator
│       │   └── Validation/            # LevelValidator (editor + runtime)
│       ├── Presentation/
│       │   ├── Animation/             # DOTween wrapper services, motion presets
│       │   ├── Juice/                 # JuiceDirector, CameraShake, ComboFX
│       │   ├── Pooling/               # GenericObjectPool<T>, PoolService
│       │   └── Theming/               # ThemeService, IThemeable
│       ├── UI/
│       │   ├── HUD/
│       │   ├── Popups/
│       │   ├── Core/                  # UIRoot, UIScreen base, UIManager
│       │   └── MVVM/                  # base ViewModel, binding helpers
│       ├── Audio/
│       ├── Save/
│       ├── Analytics/
│       └── Utilities/
├── Editor/
│   ├── LevelEditorTools/               # image import pipeline, level inspector
│   └── ValidationTools/
├── Addressables/
│   └── (Addressable Groups per theme / level-pack)
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

**Why this shape:** systems are grouped by *feature*, not by *type* (i.e., not one giant `Scripts/` with `Managers/`, `Models/`, `Views/` mixed together). Each feature folder is close to a bounded context — `Board`, `Tray`, `Matching`, `Obstacles` — so a future engineer adding "ice blocks" opens `Obstacles/` and knows exactly where to work without touching `Board/` or `Matching/` internals.

---

## 4. Scene Structure & Project Initialization

**Boot flow:**

```
Bootstrap.unity (first scene, tiny, never seen by player)
   → GameRoot.Awake()
       → Initialize infrastructure services in fixed order:
         1. SaveService (loads local save synchronously — small file)
         2. AddressablesService (Addressables.InitializeAsync)
         3. AudioService (loads mixer, pools AudioSources)
         4. AnalyticsService
         5. HapticsService
         6. EventBus (pure C#, no init cost)
       → DontDestroyOnLoad(gameRoot)
   → LoadSceneAsync("MainMenu")
```

Why a dedicated Bootstrap scene rather than initializing in MainMenu: it guarantees `GameRoot` exists exactly once regardless of which scene the player (or the Unity Editor "Play from any scene" workflow) starts from, and it gives us a single, testable entry point for cold-start performance profiling.

**Gameplay scene composition:**

```
Gameplay.unity
   → SceneRoot.Awake()
       → resolves which level to load (from GameServices.LevelSelection)
       → LevelLoader.LoadAsync(levelId)
             → Addressables load of LevelDefinitionSO + theme assets
       → BoardGenerator.Generate(levelDefinition) → BoardModel
       → BoardController.Bind(boardModel)  → spawns TileControllers from pool
       → TrayController.Bind(trayCapacity)
       → MatchService.Bind(boardModel, trayModel, levelDefinition.matchCount)
       → GameFlowController.Bind(boardModel, trayModel) → starts listening for win/lose
       → UIRoot.Bind(gameFlowController, trayModel)
       → JuiceDirector.Bind(EventBus)
   → SceneRoot.OnDestroy()
       → explicit Unsubscribe on every event subscription
       → PoolService.ReleaseAll(sceneScope)
```

`SceneRoot` is the only MonoBehaviour allowed to `new` up gameplay-scoped controllers. This is the explicit-composition rule in practice — there is no hidden auto-wiring; every dependency is visible in one file (`SceneRoot.cs`), which effectively serves as the "wiring diagram" for the level.

---

## 5. Core Systems

### 5.1 GameServices (Service Locator, deliberately minimal)

```csharp
public static class GameServices
{
    public static IEventBus EventBus { get; private set; }
    public static ISaveService Save { get; private set; }
    public static IAudioService Audio { get; private set; }
    public static IAddressablesService Content { get; private set; }
    public static IAnalyticsService Analytics { get; private set; }
    public static IHapticsService Haptics { get; private set; }

    internal static void Register(IEventBus bus, ISaveService save, ...) { ... }
}
```

- Registration happens **once**, from `GameRoot`, in a fixed order — not from arbitrary call sites.
- Every field is an **interface**, so PlayMode tests can swap in fakes (`FakeSaveService`, `NullAudioService`) without touching production code.
- This is intentionally *not* a general-purpose container: gameplay objects (`BoardController`, `TileController`, etc.) are never pulled from `GameServices`. They're constructed explicitly by `SceneRoot` and handed their dependencies directly. `GameServices` only holds true app-lifetime singletons where a container would be overkill and DI would add indirection without benefit.

### 5.2 Event Bus (Event-Driven Architecture backbone)

A typed, allocation-free pub/sub bus is the primary way decoupled systems talk to each other (Board doesn't know UI exists; UI doesn't know Board exists — both know the Bus).

```csharp
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;
    void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent;
    void Publish<T>(T evt) where T : struct, IGameEvent;
}
```

Design choices and why:
- **Events are `struct`s implementing a marker interface `IGameEvent`** — value types avoid GC allocation on every publish, unlike a naive `UnityEvent`/`C# event` per message type.
- **Strongly typed, not string-keyed** (`"OnTileMatched"` string events are a classic footgun — no compile-time safety, easy to typo). Each event is its own struct: `TileMatchedEvent`, `TraySlotFilledEvent`, `TrayFullEvent`, `BoardClearedEvent`, `LevelFailedEvent`, `ComboIncreasedEvent`.
- **Internal dictionary keyed by `typeof(T)`** mapping to a `List<Delegate>`, resolved once and cached — no LINQ, no per-publish allocation once warmed up.
- Every subscriber (especially UI and Juice systems) **must** unsubscribe in `OnDestroy`/scene teardown. `SceneRoot` audits this in dev builds via a subscription-count assertion.

This is the mechanism that lets `MatchService`, `JuiceDirector`, `UIRoot`, `AudioService`, and `AnalyticsService` all react to "3 tiles matched" independently, with zero direct references between them.

### 5.3 Object Pooling

```
IObjectPool<T>            — generic interface (Get, Release, Prewarm)
GenericObjectPool<T>      — Stack<T>-backed, no allocations after prewarm
PoolService                — owns named pools (tiles, particles, floating text, tray slots)
IPoolable                  — OnSpawned() / OnDespawned() lifecycle hooks
```

- Tiles, particle VFX, floating score/combo text, and tray slot visuals are **all pooled** — never `Instantiate`/`Destroy` at runtime during gameplay.
- Pools are **prewarmed** at level load based on `LevelDefinitionSO` tile counts, so the first tile reveal never hitches.
- Pools are **scene-scoped**: `SceneRoot` owns a `PoolService` instance and releases every pooled object back (or destroys the pool entirely) on scene teardown — pools do not leak across levels.
- Addressables-loaded prefabs (theme-specific tile skins) are pooled *per theme key*, since swapping themes means different visual prefabs but identical logical `TileController` behaviour.

### 5.4 New Input System

- A single `TileInputController` owns a `PlayerInput`/Input Action Asset with one action map (`Gameplay`) and one action (`TapPoint`, `Vector2` binding to touch/mouse position).
- On tap, it does a single `Physics2D.Raycast` (or `OverlapPoint` on a `NonAlloc` variant) against a dedicated `Tile` layer — never a scene-wide raycast against everything.
- `TileInputController` does not know about matching, trays, or scoring. It resolves "which `TileController` was tapped" and publishes a `TileTappedEvent` (or, more precisely, calls a direct method on `BoardController` — see §6.3 for why tap resolution is a direct call rather than an event, unlike gameplay *outcomes*).
- Input is **disabled** during board-generation, level-intro animation, win/lose sequences, and one tile's own travel-to-tray animation window (handled via a simple `IsAcceptingInput` flag on the controller, not by disabling GameObjects, to avoid Editor/Addressables activation overhead).

---

## 6. Board, Tile, and Tray Architecture

This is the gameplay core, so it gets the strictest separation of **Model** (pure C#, no MonoBehaviour, fully unit-testable) from **View/Controller** (MonoBehaviour, owns transforms, animation, pooling).

### 6.1 Data/Model Layer (pure C#, zero UnityEngine dependency where possible)

```csharp
// Identifies a tile's logical position and stacking.
public readonly struct BoardCoordinate { public int X, Y, Layer; }

public sealed class TileModel
{
    public TileModel Id;                 // stable identity for pooling/animation continuation
    public BoardCoordinate Coordinate;
    public TileTypeSO TileType;           // which "face" — configured via SO, not enum, for content scalability
    public bool IsRemoved;
    public IReadOnlyList<TileModel> Blockers; // tiles directly on top of this one
    public bool IsExposed => Blockers.Count == 0 && !IsRemoved;
    public IObstacleState ObstacleState;   // null if no obstacle; see §12
}

public sealed class BoardModel
{
    public IReadOnlyList<TileModel> AllTiles { get; }
    public event Action<TileModel> TileExposed;   // raised when its last blocker is removed
    public void RemoveTile(TileModel tile) { ... }
}

public sealed class TraySlotModel { public TileTypeSO Occupant; }

public sealed class TrayModel
{
    public int Capacity;
    public IReadOnlyList<TraySlotModel> Slots;
    public bool IsFull => OccupiedCount >= Capacity;
    public bool TryInsert(TileTypeSO type, out int slotIndex) { ... }
    public void RemoveMatched(TileTypeSO type, int matchCount) { ... }
}
```

Why a pure-C# model layer: `BoardModel`, `TileModel`, `TrayModel`, and `MatchService` (below) contain **zero MonoBehaviour references** and can be constructed and unit tested in EditMode tests without loading a scene. This is what lets us verify "does a 4x4 grid with a 3-match rule solve correctly" in milliseconds in CI, long before touching a Unity scene.

### 6.2 View/Controller Layer (MonoBehaviour)

```csharp
public sealed class BoardController : MonoBehaviour
{
    // Owns the mapping from TileModel -> TileController instance.
    // Subscribes to BoardModel.TileExposed to enable input / play "reveal" juice.
    // Delegates actual movement/animation to TileController + JuiceDirector.
}

public sealed class TileController : MonoBehaviour, IPoolable
{
    // Owns its DOTween sequence for: idle, tap-response, travel-to-tray, match-pop, removal.
    // Exposes TileModel (read-only) it's currently representing.
    // No gameplay logic — purely "given a model state, render + animate it."
}

public sealed class TrayController : MonoBehaviour
{
    // Lays out TraySlot views, animates insertion, shuffles remaining slots when tiles are removed.
}
```

**Separation of gameplay logic from visuals** (explicit requirement): `BoardModel`/`TrayModel`/`MatchService` decide *what happened*. `BoardController`/`TileController`/`TrayController`/`JuiceDirector` decide *how it looks*. The model layer never references DOTween, never references `Transform`, never waits on animation completion to make a gameplay decision — animations are always "fire and forget, then notify" from the controller's perspective, keeping gameplay logic frame-rate- and animation-duration-independent.

### 6.3 Interaction flow (tap → match), step by step

```
1. TileInputController raycasts tap → resolves TileController → calls
   BoardController.TryHandleTap(tileController)
2. BoardController checks tileController.Model.IsExposed
   (guard: unexposed tiles are not raycast-able at all — they're on a non-interactive
   layer/sub-position — this is a defense-in-depth double-check)
3. BoardController calls BoardModel.RemoveTile(model) → this:
     - marks IsRemoved
     - decrements Blockers on tiles above it → may raise TileExposed for newly revealed tiles
4. BoardController tells the TileController to play its "fly to tray" DOTween animation
   (visual only — the *logical* removal from the board already happened in step 3)
5. On animation completion (callback), BoardController calls
   TrayModel.TryInsert(tileModel.TileType)
6. If insert fails (tray full) → publish TrayFullEvaluationEvent → GameFlowController checks loss
7. If insert succeeds → TrayController plays slot-fill juice, and MatchService is asked to
   evaluate: "does this tile type now have >= level.matchCount occurrences in the tray?"
8. If yes: MatchService calls TrayModel.RemoveMatched(...), publishes TileMatchedEvent
   (contains: tile type, matched slot indices, combo count) → TrayController animates
   removal, JuiceDirector plays combo FX/camera kick/haptics, AudioService plays match SFX,
   GameFlowController checks win condition (board fully cleared?)
9. If board is empty → publish LevelCompletedEvent → GameFlowController transitions to win UI
```

Note the deliberate mix of **direct calls** (steps 1–7, where ordering and return values matter — e.g., we need to know synchronously whether an insert succeeded to decide the animation) and **events** (steps 8–9, "broadcast, many independent listeners react"). This is intentional: events are for *fan-out notifications where the sender doesn't need a response*; direct calls are for *sequential logic where control flow matters*. Using events everywhere would make the core game loop hard to trace and debug; using direct calls everywhere would recouple Board/Tray/UI/Audio/Juice. §16 (Risks) elaborates on this trade-off.

### 6.4 Match Detection — `MatchService`

```csharp
public interface IMatchRule
{
    bool TryFindMatch(TrayModel tray, TileTypeSO justInserted, out MatchResult result);
}

public sealed class ExactCountMatchRule : IMatchRule { /* level.matchCount identical tiles */ }

public sealed class MatchService
{
    private readonly IMatchRule _rule;   // injected explicitly by SceneRoot, swappable per level/mode
    public MatchResult Evaluate(TrayModel tray, TileTypeSO justInserted) => ...;
}
```

`MatchService` depends on an `IMatchRule` abstraction rather than hardcoding "3 identical tiles" — the level config supplies `matchCount`, and `ExactCountMatchRule` is parameterized by it. This is the extension point for future modes (e.g., "match any 2 of a themed set" or "wildcard tiles") without touching `MatchService` itself — new rule, same interface (Open/Closed Principle).

---

## 6a. Domain Layer Separation (refinement, added 2026-08-01)

The original folder structure in §3 grouped model and controller together under `Gameplay/Board`, `Gameplay/Tray`, etc. Once actually implementing §6.1's model layer, we made this split explicit and physical: everything described in §6.1 (plus `LevelModel`, `MatchSystem`, and `BoardState`) now lives under `Domain/`, a folder — and eventually a distinct assembly — that takes **zero `UnityEngine` dependency**, not even value types like `Vector2Int`. `Gameplay/` now holds only MonoBehaviour view/controllers that wrap a `Domain` model instance.

Why the stricter split earns its keep: it makes "is this class engine-independent" a filesystem question, not a code-review judgment call, and it means the entire simulation — board generation, tapping, matching, win/loss — can be exercised by EditMode unit tests with no scene, no prefab, and sub-millisecond run time per test. See `DECISIONS.md` for the full rationale.

---

## 7. Level System — Data-Driven Design

### 7.1 `LevelDefinitionSO`

```csharp
[CreateAssetMenu(menuName = "Levels/Level Definition")]
public sealed class LevelDefinitionSO : ScriptableObject
{
    public int LevelId;
    public BoardLayoutSource LayoutSource;      // Manual or ImageBased — see §8
    public Texture2D ShapeMaskImage;             // optional, used if LayoutSource == ImageBased
    public List<LayerMaskImage> LayerMasks;      // optional, one per stacked layer
    public ManualLayoutSO ManualLayout;           // optional, used if LayoutSource == Manual
    public TileDistributionSO TileDistribution;    // how tile types are assigned/shuffled onto positions
    public int TraySize = 7;
    public int MatchCount = 3;
    public ThemeSO Theme;
    public List<ObstaclePlacementSO> Obstacles;
    public DifficultyProfileSO Difficulty;         // optional metadata, tuning/analytics only
}
```

Every field beyond `LevelId` is **optional or defaulted**, satisfying "each level can optionally contain X" — a level with no obstacles and no image just omits those fields and the pipeline degrades gracefully (see §7.3, Null Object pattern for obstacles).

### 7.2 Level Loading Pipeline

```
LevelLoader.LoadAsync(levelId)
  → Addressables.LoadAssetAsync<LevelDefinitionSO>(key)
  → LevelValidator.Validate(definition)     // fails loudly in dev builds if e.g. tile-count
                                              // isn't divisible by matchCount, tray too small, etc.
  → BoardGenerator.Generate(definition) → BoardModel
        (delegates to IBoardLayoutSource — see §8)
  → TileDistributor.Assign(boardModel.AllTiles, definition.TileDistribution)
        (weighted-random or curated assignment of TileTypeSO to each position,
         guaranteeing solvability — count-per-type is always a multiple of matchCount)
  → ObstacleApplier.Apply(boardModel, definition.Obstacles)
```

`LevelValidator` running in dev builds (and as an Editor tool, see §17) is the single most important safety net for a data-driven pipeline: a bad level asset should fail at import/load time with a clear error, never as a silent runtime soft-lock in front of a player.

### 7.3 Board Generation — `IBoardLayoutSource`

```csharp
public interface IBoardLayoutSource
{
    IReadOnlyList<BoardCoordinate> GenerateLayout(LevelDefinitionSO definition);
}

public sealed class ManualLayoutSource : IBoardLayoutSource { ... }
public sealed class ImageBasedLayoutSource : IBoardLayoutSource { ... }
```

`BoardGenerator` doesn't care which source produced the coordinate list — it just asks the `LevelDefinition.LayoutSource` for one via a small factory/switch, then builds `TileModel`s from the returned coordinates. Adding a third source later (e.g., **procedural generation**, listed under Future Expansion) means writing one new class implementing `IBoardLayoutSource` — zero changes to `BoardGenerator`, `TileDistributor`, or anything downstream. This is the primary Open/Closed extension point in the whole project.

---

## 8. Image-Based Level Generation

This is presented as its own pipeline stage since it's a headline feature.

### 8.1 Concept

A designer supplies a PNG per layer (shape mask + N layer masks). Each image is treated as a **stencil**: opaque/colored pixels above an alpha threshold indicate "place a tile here"; pixel position maps to a grid cell via a configurable sampling resolution (e.g., sample every N pixels, or downsample to a fixed grid resolution like 20×20 regardless of source image size, for consistent gameplay density across differently-sized source art).

### 8.2 Pipeline

```
ImageBasedLayoutSource.GenerateLayout(definition)
  → for each LayerMaskImage (ordered bottom-to-top):
        1. TextureSampler.Sample(image, gridResolution, alphaThreshold)
             → returns bool[,] occupancy grid for this layer
        2. GridToCoordinates.Convert(occupancyGrid, layerIndex)
             → List<BoardCoordinate> for this layer
  → merge all layers' coordinates → full BoardCoordinate list
  → StackingResolver assigns Blockers: any tile whose (X,Y) matches a tile in a
    lower layer index is a "blocker" of that lower tile (configurable stacking rule:
    default = same X,Y across layers means "on top of")
```

Key design decisions:
- **This runs as an Editor-time preprocessing step, not at runtime on device.** The `ImageBasedLayoutSource` logic is engine-agnostic-ish pure C#/Texture2D sampling, but for shipped levels we bake the *output* (`List<BoardCoordinate>`) into the `LevelDefinitionSO` (or a companion `BakedLayoutSO`) via an Editor tool (§17). Runtime never touches `Texture2D.GetPixels` on a mobile device — this is a performance/memory decision (reading large textures and doing CPU-side sampling on a low-end Android device, every level load, would be a real jank/battery risk). The *system* still supports fully runtime generation (useful for a future in-app level editor or UGC), it's just not the default shipped path.
- **`TextureSampler` and `GridToCoordinates` are separate, independently testable classes** (Single Responsibility) — sampling an image into a boolean grid is a distinct concern from converting that grid into game coordinates and stacking order.
- **Manual layouts and image-based layouts produce the exact same output type** (`IReadOnlyList<BoardCoordinate>`), so everything downstream (`TileDistributor`, `ObstacleApplier`, `BoardController`) is completely unaware of which authoring method was used.

### 8.3 Designer workflow (Editor tool, see §17)

1. Drop shape/layer PNGs into a level's asset folder.
2. Run "Preview Layout" Editor tool → renders the resulting grid + stacking directly in the Scene view as gizmos, before ever building tiles.
3. Adjust grid resolution / alpha threshold per level if needed (exposed on `LevelDefinitionSO`).
4. "Bake" button converts image → `BoardCoordinate` list stored in the asset, image references kept for re-baking if art changes.

---

## 9. UI Architecture

- **MVVM-inspired**, not full MVVM — pragmatic subset appropriate for a mobile game's UI complexity, per your "MVC/MVVM inspired where appropriate."
- `UIRoot` owns a small `UIScreenManager` that shows/hides `UIScreen` instances (HUD, WinPopup, LosePopup, PausePanel, Settings). Screens are **Addressable prefabs**, loaded on demand and pooled per screen-type since some (HUD) persist for the whole level and others (popups) are transient.
- Each screen has a thin **ViewModel** (plain C# class, not MonoBehaviour) exposing observable properties (`event Action<int> OnScoreChanged`, etc.) that the **View** (MonoBehaviour, holds Text/Image references) binds to. ViewModels subscribe to the `EventBus`/model layer; Views know nothing about gameplay systems, only their ViewModel.
- No runtime `GameObject.Find` or `transform.Find` chains for UI — every View gets its child references wired via `[SerializeField]` in the Inspector, keeping runtime lookups at zero cost.
- UI animations (popup scale-in, button press feedback, tray shake) go through the same DOTween/Juice presets as gameplay (§10) for visual consistency.

---

## 10. Animation & Game Feel Architecture

Juice is centralized so it can be **tuned, disabled (for low-end devices/reduced-motion), and extended** without hunting through gameplay code.

```csharp
public interface IJuiceDirector
{
    void PlayTileTap(TileController tile);
    void PlayMatchCombo(MatchResult result, Vector3 worldPosition);
    void PlayTrayFullWarning();
    void PlayLevelWin();
    void PlayLevelLose();
}

public sealed class JuiceDirector : MonoBehaviour, IJuiceDirector
{
    // Subscribes to EventBus (TileMatchedEvent, ComboIncreasedEvent, TrayFullEvent, ...)
    // Delegates to: CameraFeedbackService (shake/punch), HapticsService, AudioService,
    // ParticlePoolService, UI toast/floating-text spawning.
    // Reads tunable curves/durations from a JuiceConfigSO — no magic numbers in code.
}
```

- **`JuiceConfigSO`** holds every duration, scale curve, shake amplitude, and particle-count as data — a designer can retune "how bouncy is a match" without a code change or new build.
- **DOTween usage is wrapped** behind small extension methods / a `MotionPresets` static class (e.g., `transform.PlayTileTapBounce(config)`) rather than raw `.DOScale(...)` calls scattered through gameplay controllers — this keeps tween chains reusable, testable in isolation, and easy to swap for a different tween engine later if ever needed.
- **Combo escalation** (bigger camera kick / more particles on chained matches) lives entirely in `JuiceDirector`, driven by `ComboIncreasedEvent` payload data — gameplay logic doesn't know or care that combos affect visuals.
- **Device performance tiering**: `JuiceConfigSO` supports a "reduced" variant (fewer particles, shorter shakes) selected at boot based on a simple device-tier heuristic (`SystemInfo.systemMemorySize`, processor count), so the same codebase scales visual fidelity down gracefully on low-end hardware rather than needing a separate low-end code path.

---

## 11. Audio Architecture

```csharp
public interface IAudioService
{
    void PlaySfx(AudioClipReference clip, float volumeScale = 1f);
    void PlayMusic(AudioClipReference track, bool crossfade = true);
    void SetMuted(AudioCategory category, bool muted);
}
```

- **Pooled `AudioSource` components** (a fixed-size pool, e.g., 8–12 sources) — no `AudioSource.PlayClipAtPoint` (which allocates a GameObject per call).
- Clips are referenced via **Addressables** (`AudioClipReference` wraps an `AssetReference`) so per-theme SFX/music can be downloaded/updated independently of the app binary.
- `AudioService` subscribes to relevant `EventBus` events itself (match, win, lose, button taps) for ambient/global sounds; `JuiceDirector` also can request specific one-off SFX tied to a visual beat (e.g., combo-tier stinger) — audio has two entry points (event-reactive + direct request) by design, matching the same "broadcast vs. directed" split as §6.3.
- Mixer groups (Master/SFX/Music/UI) via a `AudioMixer` asset, with volume persisted through `SaveService`.

---

## 12. Theme System

```csharp
[CreateAssetMenu(menuName = "Themes/Theme Definition")]
public sealed class ThemeSO : ScriptableObject
{
    public string ThemeId;
    public List<TileTypeSO> TileTypeSkinSet;   // theme-specific sprite/prefab per logical tile type
    public AssetReference BackgroundArt;
    public AssetReference AmbientMusic;
    public ColorPalette UIPalette;
}
```

- `TileTypeSO` represents a **logical** tile identity (used for matching); `ThemeSO` maps logical types to **visual** skins. This decoupling means the same `LevelDefinitionSO` (logical layout + distribution) can be re-skinned for seasonal events by swapping only the `Theme` reference — no level redesign needed.
- Theme assets are Addressable-grouped so a new seasonal theme can be delivered as a **remote content update** without an app store submission (ties into Future Expansion: seasonal events, live content updates).
- `IThemeable` interface lets any view component (`TileController`, background renderer, HUD frame) react to `ThemeService.ThemeChanged` without `ThemeService` needing to know about every consumer.

---

## 13. Save System

```csharp
public interface ISaveService
{
    T Load<T>(string key, T fallback);
    void Save<T>(string key, T value);
    void Flush();  // explicit write-to-disk, batched rather than per-call
}
```

- Local save (JSON via `System.Text.Json` or Newtonsoft, encrypted lightly to deter casual tampering — not DRM-grade, just enough to stop trivial score edits) covers: level progress, currency (if/when meta layer exists), settings, theme unlocks.
- **Writes are batched and debounced**, not synchronous on every state change — avoids disk I/O jank during gameplay (e.g., don't fsync on every single tile match).
- Save system is intentionally decoupled from *what* is being saved — gameplay/meta systems own their own serializable state structs and just call `Save<T>`/`Load<T>`; `SaveService` doesn't know about levels, tiles, or currency.
- Designed so a future cloud-save layer (Play Games Services / Game Center / custom backend) can be added as an alternate `ISaveService` implementation without touching any call site.

---

## 14. Performance Strategy

Concrete rules, not just principles:

- **Zero gameplay-frame allocations**: object pooling for all spawned visuals; struct-based events; no LINQ in `MatchService`, `BoardGenerator`, or per-tap code paths (LINQ's iterator allocations are a measurable cost on low-end Android/Mono — reserved for Editor tooling and one-time load-time code only).
- **No `Update()` sprawl**: gameplay state changes are event-driven, not polled. The few legitimate per-frame needs (camera shake decay, active DOTween sequences) are owned by DOTween's own update loop or a single `JuiceDirector.Update()`, not scattered across every `TileController`.
- **No runtime `FindObjectOfType`/`GameObject.Find`**: every reference is wired at composition time (`SceneRoot`, `[SerializeField]`) or resolved through `GameServices`.
- **Draw call/batching awareness**: tile sprites share atlases per theme (Sprite Atlas), UI uses a small number of Canvases split by update-frequency (static HUD elements vs. frequently-updating tray/score, to avoid full-canvas rebuilds).
- **Texture memory**: Addressables + Sprite Atlas group per theme, only the active theme's assets resident in memory; image-based level source textures are Editor-only (§8.2) and never shipped/loaded at runtime.
- **Physics minimalism**: tile tap detection uses a cheap 2D overlap check against a small, fixed set of "currently exposed" colliders (which is itself typically a small subset of the board), not a full-board raycast sweep.
- **Profiling gates**: Unity Profiler + Frame Debugger checks are part of the definition-of-done for each system (documented in `PERFORMANCE.md`, to be created), with a baseline device (a genuinely low-end Android target) established early.

---

## 15. Future Obstacle System — Extension Point

This deserves explicit design now, even before implementation, since "future mechanics without modifying existing systems" is a hard requirement.

```csharp
public interface IObstacleState
{
    bool BlocksSelection { get; }         // can this tile be tapped even if exposed?
    bool BlocksMatchRemoval { get; }        // does it survive being matched (e.g., needs 2 hits)?
}

public interface IObstacleBehaviour
{
    void OnTileExposed(TileModel tile);
    void OnTileTapped(TileModel tile, out bool consumedTap);
    void OnAdjacentTileRemoved(TileModel tile, TileModel removedNeighbor); // for chains/webs
}
```

- `TileModel.ObstacleState` defaults to a **Null Object** (`NoObstacleState.Instance`) when a tile has no obstacle — every downstream check (`BlocksSelection`, etc.) is a normal interface call, never a null check scattered through gameplay code.
- Concrete obstacles (`IceBlockObstacle`, `LockedTileObstacle`, `ChainObstacle`, `SpiderWebObstacle`, `BombObstacle`) each implement `IObstacleBehaviour` and are attached via `ObstacleDefinitionSO` + `ObstaclePlacementSO` entries in the level asset (§7.1) — placed per-coordinate or per-layer, same as tile distribution.
- `BoardController`/`TileController` call into `IObstacleBehaviour` at well-defined hook points (tile exposed, tile tapped, neighbor removed) but never contain `if (obstacleType == IceBlock)`-style branching. Adding "spider web" later means: one new `SpiderWebObstacle` class + one new `ObstacleDefinitionSO` asset — zero edits to `BoardController`, `MatchService`, or `TileController`.
- **Power-ups/bombs** (player-triggered rather than board-state obstacles) will follow a parallel `IPowerUp` interface with its own activation entry point from UI — out of scope for detailed design until the base game is solid, but the hook (`GameFlowController` exposing a narrow "request power-up activation" API) is reserved now so it doesn't require a later refactor.

---

## 16. Risks & Recommendations

Being direct about where this architecture has real trade-offs, since pretending otherwise would be a disservice:

1. **Event Bus overuse risk.** It's tempting to route *everything* through the bus once it exists. We've explicitly scoped it to fan-out notifications (§6.3) and kept sequential gameplay logic as direct calls. If this line blurs over time, core game loop debugging gets harder ("who handles this event, in what order?"). Recommendation: keep a running list in `SYSTEMS.md` of every event type and its subscribers, reviewed whenever a new one is added.
2. **Service Locator can rot into a God Object if scope creeps.** `GameServices` must stay limited to true app-lifetime infrastructure. The moment someone is tempted to add `GameServices.CurrentBoard` or similar gameplay state, that's a smell — gameplay state belongs in explicitly-passed references, not global lookup. Recommendation: code review rule — no gameplay-scoped state in `GameServices`, ever.
3. **Image-based level generation is a genuinely hard content pipeline to get right.** Alpha-threshold sampling on messy source art (anti-aliased edges, semi-transparent pixels) can produce inconsistent grids. Recommendation: build the Editor preview/bake tool (§8.3, §17) *before* asking designers to author real levels with it, and keep manual layout as a fully-supported fallback (already designed in, §7.3) rather than a stopgap.
4. **No-DI explicit composition scales less gracefully past a certain system count.** With ~10–15 systems this is very manageable and more debuggable than a container. If the system count grows substantially (e.g., a large meta-game layer, multiplayer), `SceneRoot`/`GameRoot` wiring code could get long. Recommendation: if `SceneRoot.Awake()` exceeds roughly 40–50 lines of wiring, consider splitting into sub-composers (e.g., `GameplayComposer`, `UIComposer`) called from `SceneRoot` — still no container, just decomposition, before reaching for DI.
5. **Solvability guarantees for image-based + random distribution.** A board layout from an image plus a naive random tile-type shuffle can produce unsolvable states (tile count per type not divisible by `matchCount`, or worse, a layering order that traps a type). `TileDistributor` must guarantee divisibility by construction (already noted in §7.2) and `LevelValidator` should ideally simulate solvability for curated levels — this needs dedicated design time and should be its own follow-up doc/session before the level pipeline ships.
6. **Low-end device target must be chosen concretely, early.** "Low-end Android" is not actionable until we pick 1–2 real reference devices and profile against them from the first playable build, not at the end.

---

## 17. Editor Tooling (called out separately since it's a core part of the pipeline, not an afterthought)

- **Level Inspector**: custom `LevelDefinitionSO` Editor with live board preview (renders the resulting grid as gizmos/scene overlay) for both manual and image-based layouts.
- **Image Layout Baker**: the tool described in §8.3 — sample, preview, adjust, bake.
- **Level Validator window**: batch-runs `LevelValidator` across every level in the project, flags solvability/config issues before a build.
- These live under `Assets/Editor/`, are never compiled into player builds, and are documented in a future `LEVEL_SYSTEM.md`.

---

## 18. Documentation Plan

This `ARCHITECTURE.md` is the root reference. As we design/implement each system per your requested process, these companion docs will be created/updated:

- `PROJECT_OVERVIEW.md` — vision, scope, target audience, platform targets (next logical doc to write)
- `GAMEPLAY.md` — detailed rules, win/lose conditions, tuning values
- `SYSTEMS.md` — living registry of every manager/service/event type and its owners/subscribers
- `LEVEL_SYSTEM.md` — deep dive on §7–8, designer-facing workflow docs
- `UI.md`, `AUDIO.md` — per-domain detail beyond §9/§11
- `PERFORMANCE.md` — device targets, profiling baselines, budgets per system
- `ROADMAP.md` — phased build order
- `CHANGELOG.md` — dated entries as systems land
- `DECISIONS.md` — ADR-style log (e.g., "why no DI," "why baked image layouts") so future sessions have rationale, not just conclusions

---

## 19. Proposed Build Order (for your approval)

Given everything above, the dependency-safe order to implement systems is:

1. **Core infrastructure**: `GameRoot`, `GameServices`, `EventBus`, `PoolService` (foundation everything else needs)
2. **Board/Tile/Tray model layer** (pure C#, unit-testable, no scene needed)
3. **Manual level layout + `TileDistributor`** (simplest content path first, proves the model layer)
4. **Board/Tile/Tray view+controller layer** + basic input (first playable loop, no juice yet)
5. **Match detection + win/lose flow**
6. **Juice pass** (DOTween, camera, particles, haptics, audio hookup)
7. **UI layer** (HUD, popups)
8. **Image-based level generation pipeline** (once the core loop is proven and stable)
9. **Theme system**
10. **Save system**
11. **Obstacle system extension point + first obstacle (e.g., Locked Tile) as a proof of the extensibility model**

This order front-loads the riskiest architectural bets (event bus, model/view split, pooling) into the smallest possible surface area before layering content tooling and polish on top.

---

**Awaiting your review.** Flag anything you want restructured, and confirm the build order in §19 before we start on system #1 (Core Infrastructure) in detail per the step-by-step process (explain → propose → justify → class list → extension points → pitfalls → your approval → code).
