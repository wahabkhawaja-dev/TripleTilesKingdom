# Triple Tiles Kingdom - Architecture Documentation

## Layered Architecture Overview

The project is organized into four distinct, independent layers:

```
┌─────────────────────────────────────────┐
│         PRESENTATION LAYER              │
│  (UI, Views, Controllers, Animations)   │
├─────────────────────────────────────────┤
│          DOMAIN LAYER                   │
│  (Pure game logic, models, no deps)     │
├─────────────────────────────────────────┤
│          CORE LAYER                     │
│  (Bootstrap, EventBus, Services)        │
├─────────────────────────────────────────┤
│       EXTERNAL DEPENDENCIES             │
│  (Unity, Third-party libraries)         │
└─────────────────────────────────────────┘
```

## Layer Details

### 1. Core Infrastructure Layer

**Purpose**: App-lifetime initialization and cross-cutting services.

**Key Components**:

- **GameRoot** (`Core/Bootstrap/GameRoot.cs`)
  - Singleton entry point
  - Initializes services in fixed order
  - Persists via DontDestroyOnLoad
  - Loads first gameplay scene
  - See [ARCHITECTURE.md §4](./ARCHITECTURE.md) for full boot sequence

- **GameServices** (`Core/Services/GameServices.cs`)
  - Static accessor for infrastructure only
  - Intentionally NOT a general service locator
  - Contains: EventBus, SaveService, AudioService, Addressables, Analytics, Haptics
  - Gameplay objects are constructed directly, not resolved here

- **EventBus** (`Core/EventBus/EventBus.cs`)
  - Zero-allocation hot-path event system
  - Type-keyed handler dictionary (O(1) lookup)
  - Struct-based events (IGameEvent marker)
  - Subscribe/Unsubscribe happen at scene setup (allocating)
  - Publish is zero-alloc on critical path

- **Services**: Pluggable interfaces with no-op placeholders
  - `IAudioService`: Music, SFX, voice (NoOpAudioService placeholder)
  - `ISaveService`: Persistence (NoOpSaveService placeholder)
  - `IAddressablesService`: Content streaming (AddressablesService)
  - `IAnalyticsService`: Event tracking (NoOpAnalyticsService)
  - `IHapticsService`: Device vibration (HapticsService)

**Design Rationale**:
- Services are app-scoped singletons because they represent infrastructure
- No gameplay state lives here (violates Single Responsibility)
- Separate from gameplay objects which are constructed explicitly by SceneRoot

**Initialization Order**:
1. SaveService (synchronous, no external dependency)
2. AddressablesService (async, supports content delivery)
3. AudioService, AnalyticsService, HapticsService, EventBus (stateless, no dependencies)

---

### 2. Domain Layer

**Purpose**: Pure game logic with zero Unity dependencies. Testable, portable, rules-engine focused.

**Key Models**:

#### Board (`Domain/Board/`)
- **BoardModel**: Single source of truth for board state
  - Owns all tiles via indexed dictionaries (by ID, coordinate, type)
  - O(1) lookups: `GetTile(id)`, `GetTileAt(coord)`, `GetTilesOfType(typeId)`
  - Tracks selectability, coverage, blocking relationships
  - Immutable public surface: read-only properties, no direct mutation
  - Events: TileRemoved, TileExposed, BoardCleared

- **TileModel**: Immutable representation of a single tile
  - Properties: ID, TypeId, Coordinate, State (Covered/Exposed/Removed)
  - Relationships: blockers, covered-by, obstacles
  - No methods beyond accessors (pure data)
  - Obstacle support for future content (spikes, boxes, locks)

- **BoardCoordinate**: Row/Column/Layer struct
  - 3D board support (layers for stacking)
  - Used as key for O(1) lookups

- **TileId, TileTypeId**: Value objects
  - Wrap int to prevent accidental mixing of IDs and types
  - Small stack allocation, zero-cost abstractions

- **StackingResolver**: Logic for multi-layer board updates
  - Determines which tiles become exposed when one is removed
  - Handles layer transitions

#### Tray (`Domain/Tray/`)
- **TrayModel**: Mutable tray state
  - Fixed-capacity slot array
  - Per-type count tracking (O(1) CountOf queries)
  - Events: TileInserted, TileRemoved, TrayFull, TrayEmptied
  - All-or-nothing RemoveSlots for atomic match removal

- **ITrayView**: Read-only interface consumed by MatchSystem
  - Prevents matching logic from mutating tray
  - Interface segregation: MatchSystem can't accidentally corrupt state

#### Matching (`Domain/Matching/`)
- **IMatchRule**: Pluggable match evaluation interface
  - `TryFindMatch(tray, justInserted, out result)`
  - Decouples game rules from match logic

- **ExactCountMatchRule**: Default implementation
  - Matches when N tiles of same type are in tray
  - O(1) count check, O(N) index collection
  - Reuses scratch list across evaluations (zero-alloc after first call)

- **MatchSystem**: Thin facade
  - Delegates to IMatchRule
  - Future modes can swap rules (wildcards, cascades, etc.)

- **MatchResult**: Immutable result
  - Contains matched type and slot indices
  - Caller owns removal decision

#### Levels (`Domain/Levels/`)
- **LevelModel**: Pure data container
  - Tile layout (spawn data per coordinate)
  - Tray capacity, match count, theme ID
  - Extensible rule set (flag bag pattern)

- **LevelRuleSet**: Extensible configuration
  - Supports time limits, move limits, wildcards, etc.
  - No constructor changes needed for new mechanics

- **TileSpawnData**: Tile definition per level
  - Coordinate, type, blocking status

#### State Management (`Domain/State/`)
- **BoardStateMachine**: Guarded state transitions
  - Valid states: Loading → Ready ↔ Paused → Animating → (Won/Lost) → Loading
  - Only valid transitions allowed (enforced, fail-fast)
  - Event: StateChanged

- **BoardStateType**: Enum of valid states
  - Ready: normal gameplay
  - Animating: processing match/cascade
  - Paused: menu open, not playable
  - Won/Lost: terminal states

**Design Principles**:
- Zero Unity dependencies (no MonoBehaviour, Transform, etc.)
- Immutable public surfaces where possible
- Value objects prevent ID confusion
- Pluggable interfaces for future expansion
- No global state (all state passed explicitly)
- Events for inter-component communication

**Testing Benefits**:
- Can instantiate and test without game engine
- Deterministic (same input → same output)
- No async/threading concerns
- No serialization/platform dependencies

---

### 3. Presentation Layer

**Purpose**: Visualization, interaction, animation—everything that depends on Unity and game feel.

**Controllers**:

- **GameFlowController**: Gameplay orchestrator
  - Initializes models from level data
  - Processes tile selection → tray insertion → match detection → board updates
  - Handles state transitions and win/lose logic
  - Manages pause/resume and scene transitions
  - Bridges Domain models to Views

- **BoardController**: Visual board management
  - Creates and updates TileView instances
  - Positions tiles using board coordinates
  - Handles cascading visual updates
  - Triggers tile animations

- **TrayController**: Visual tray management
  - Creates slot UI elements
  - Updates slot colors to reflect content
  - Displays current state

**Views**:

- **TileView**: Single tile visual
  - Button input handling
  - Color coding by type
  - Click, remove, win, lose animations
  - No business logic (purely presentation)

- **GameplayHUD**: Game state UI
  - Level info, progress display
  - Pause, restart buttons
  - Popup container management

**UI Controllers** (Scene-specific):

- **MainMenuUI**: Main menu navigation
- **LevelSelectUI**: Level selection and unlock logic
- **SplashScreenUI**: Splash screen transition
- **PauseMenuPopup**: Pause screen options
- **WinPopup**: Win screen actions
- **LosePopup**: Lose screen actions

**Infrastructure**:

- **SceneRoot**: Base class for scene-specific initialization
  - Gameplay scenes extend this
  - Constructs gameplay-scoped objects
  - Handles scene-specific setup

- **LevelGenerator**: Temporary level generation
  - Procedural, deterministic per level index
  - Will be replaced by image-based pipeline
  - Generates 50+ playable levels

**Design Principles**:
- Views depend on Domain models, not vice versa
- Controllers wire models to views
- UI communicates via callbacks/events, not direct method calls
- Reusable components (buttons, popups, grids)
- All animation/visual state in presentation only

---

## Communication Patterns

### Domain to Presentation
- **Models emit events** that views subscribe to
  - Example: `BoardModel.TileRemoved` → `TileView` plays remove animation
  - Example: `TrayModel.TileInserted` → `TrayController` updates display
- **Views query models** for state
  - Example: `BoardController.Refresh()` reads `BoardModel.Tiles`

### Within Presentation
- **Controllers manage views**
  - Example: `GameFlowController` instructs `BoardController` to refresh
- **UI uses callbacks**
  - Example: Buttons call `GameFlowController.Pause()` via Unity events
- **EventBus for cross-scene events**
  - Example: Analytics on level completion

### Presentation to Domain
- **One-way only**: Controllers call model methods
  - Example: `BoardModel.RemoveTile(id)` from `GameFlowController`
- **No Domain → Presentation coupling**
  - Domain never knows views exist

---

## State Flow Example: Tile Selection

```
1. Player clicks tile
   ↓
2. TileView.OnClicked() invokes callback
   ↓
3. GameFlowController.OnTileSelected(tileId)
   ↓
4. Validate: tile selectable, game not over, state == Ready
   ↓
5. TrayModel.TryInsert(tileId) — mutate tray
   ↓
6. BoardModel.RemoveTile(tileId) — mutate board
   ↓
7. BoardModel emits events → Views update visuals
   ↓
8. GameFlowController checks for matches
   ↓
9. MatchSystem.TryEvaluate() — read-only, returns MatchResult
   ↓
10. If match: TrayModel.RemoveSlots() — remove matched tiles
    ↓
11. BoardModel cascades — removes newly-exposed tiles
    ↓
12. Repeat until no more matches
    ↓
13. Check win/lose conditions
    ↓
14. BoardStateMachine transitions state
    ↓
15. GameFlowController shows appropriate UI (nothing/pause/game over)
```

---

## Dependency Graph

```
Domain Layer (no external dependencies)
│
├── BoardModel (← StackingResolver)
├── TrayModel (← MatchSystem)
├── MatchSystem (← ExactCountMatchRule)
├── BoardStateMachine
└── LevelModel

Core Layer (→ Unity, external libs)
│
├── GameRoot (→ DontDestroyOnLoad, SceneManager)
├── GameServices (static accessor)
├── EventBus (→ collections only)
└── Services (→ Addressables, Analytics, etc.)

Presentation Layer (→ Domain, Core, Unity UI)
│
├── GameFlowController (→ BoardModel, TrayModel, MatchSystem, BoardStateMachine, LevelGenerator)
├── BoardController (→ BoardModel, TileView)
├── TrayController (→ TrayModel)
├── TileView (→ simple Domain types)
└── UI Controllers (→ GameFlowController)
```

**Key Property**: Domain has no upward dependencies. Can be tested in isolation.

---

## Scalability Considerations

### Board Sizes
- Current: 6x6 grid
- Scalable to: 10x10 without changes
- Layer support: Handles multi-layer boards
- Lookup complexity: O(1) for tile queries

### Tile Types
- Current: 6 types
- Scalable to: unlimited
- Memory: Types only stored in TrayModel slots (not duplicated)

### Level Count
- Current: 50 generated levels
- Scalable to: thousands
- Generator: Deterministic, O(1) per level (seed-based)
- Storage: Only tile layout + rules stored per level

### Player Count
- Single-player only (current architecture)
- Multiplayer: Would require different state synchronization
- No global state, so could support multiple GameFlowController instances

---

## Future Architecture Changes

### Art & Animation
- All in Presentation layer, no Domain changes needed
- Pluggable animation system (DOTween, Animator, custom)

### Audio
- NoOpAudioService → RealAudioService
- EventBus fires events, AudioService subscribes
- No Domain changes

### Persistence
- NoOpSaveService → RealSaveService (FileIO or Cloud)
- Serializes PlayerPrefs-managed state
- No Domain changes needed

### Content Delivery
- LevelGenerator → ImageLevelLoader
- Reads PNG/JSON instead of generating
- LevelModel interface unchanged
- Gameplay is oblivious to source

### Monetization
- Shop UI in Presentation
- Currency tracking via SaveService
- Ads via third-party SDK
- No Domain coupling

---

## Performance Notes

### Zero-Allocation Gameplay Loop
- EventBus publish: zero allocation (type-key dictionary cached)
- MatchSystem: reuses scratch list
- Model queries: O(1) via indexed dictionaries
- Critical path suitable for mobile (60 FPS target)

### Memory Budget
- TileModel: ~64 bytes (ID, typeID, state, flags)
- BoardModel (6x6, 2 layers): ~2 KB
- TrayModel (capacity 12): ~500 bytes
- Total per-game state: <10 KB

### GC Pressure
- Event subscriptions: happens at scene load (allocating, expected)
- Per-frame: should be zero after initial setup
- UI updates: cached layout groups, no per-frame hierarchy changes

---

## Testing Strategy

### Domain Layer
- Unit tests in EditMode
- No Unity setup required
- Deterministic, fast
- Examples:
  - BoardModel: add/remove tiles, verify state
  - TrayModel: insert/remove, verify counts
  - MatchSystem: various match scenarios
  - BoardStateMachine: state transitions

### Presentation Layer
- PlayMode tests with minimal setup
- Verify controller→model communication
- Verify view→model binding
- Examples:
  - GameFlowController: tile selection → tray update
  - BoardController: model change → view refresh

### Integration
- Scene load tests
- Full game loop simulation
- Performance benchmarks

---

## Glossary

- **View**: Visual representation of a model (TileView, TrayController)
- **Controller**: Bridges models and views (GameFlowController, BoardController)
- **Model**: Pure game logic state (BoardModel, TrayModel)
- **Service**: Infrastructure singleton (AudioService, SaveService)
- **Rule**: Pluggable game mechanic (IMatchRule)
- **State Machine**: Guarded state transitions (BoardStateMachine)
- **Event**: Pub/sub message (IGameEvent)
- **Cascade**: Automatic updates after removal (exposed tiles)

---

## References

- [VERTICAL_SLICE.md](./VERTICAL_SLICE.md) - MVP implementation details
- [Domain/Board/BoardModel.cs](./Assets/_Project/Scripts/Domain/Board/BoardModel.cs) - Core board logic
- [Presentation/GameFlowController.cs](./Assets/_Project/Scripts/Presentation/GameFlowController.cs) - Gameplay orchestration
