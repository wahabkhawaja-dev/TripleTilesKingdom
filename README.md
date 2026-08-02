# Triple Tiles Kingdom

A mobile puzzle game featuring tile-matching mechanics with a cascading board system. This repository contains a **fully playable vertical slice** built with a production-ready, layered architecture.

## Quick Start

### Prerequisites
- Unity 6000.3.19f1 or later
- TextMesh Pro (included with Unity)
- Optional: DOTween Pro (for animation polish)

### Running the Game

1. **Open the project** in Unity
2. **Run SceneSetup** (Tools → Setup All Scenes) to initialize all scenes
3. **Play** from the Splash scene
4. **Navigate** through: Splash → Bootstrap → Main Menu → Level Select → Gameplay

### Project Structure
```
Assets/
├── Scenes/              # Game scenes (Splash, Bootstrap, MainMenu, LevelSelect, Gameplay)
└── _Project/Scripts/
    ├── Core/            # App infrastructure (Bootstrap, EventBus, Services)
    ├── Domain/          # Pure game logic (Board, Tray, Matching, Levels, State)
    └── Presentation/    # UI and views (GameFlow, Controllers, Views, UI)
```

## Game Overview

### Core Mechanics
1. **Tile Selection**: Click exposed tiles to add them to your tray
2. **Matching**: When your tray contains N tiles of the same type, they're removed
3. **Cascading**: The board updates after tiles are removed, exposing new tiles
4. **Win**: Clear the board and empty your tray
5. **Lose**: Run out of moves while your tray still has tiles

### Features
- **50 Procedurally-Generated Levels**: Each with configurable difficulty
- **Level Progression**: Unlock levels by beating previous ones
- **Save System**: Progress persists using PlayerPrefs
- **Pause/Resume**: Full pause menu with restart and menu options
- **Responsive UI**: Adapts to different screen sizes

## Architecture

The project uses a **layered architecture** for scalability, testability, and maintainability:

### Layers

```
PRESENTATION LAYER
    ↓
DOMAIN LAYER (Pure Logic)
    ↓
CORE LAYER (Infrastructure)
```

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed documentation.

### Key Design Principles

1. **Domain Layer Independence**: Game logic has zero Unity dependencies
2. **Single Responsibility**: Each class has one reason to change
3. **Pluggable Components**: Match rules, services, and systems are swappable
4. **Zero-Allocation Gameplay**: Critical path optimized for mobile
5. **Testable**: Domain logic can be tested without the game engine

## Implementation Status

### ✅ Completed
- [x] Core infrastructure (GameRoot, EventBus, Services)
- [x] Complete Domain Layer (Board, Tray, Matching, Levels, State Machine)
- [x] Full Presentation Layer (Controllers, Views, UI)
- [x] All 5 game scenes (Splash, Bootstrap, MainMenu, LevelSelect, Gameplay)
- [x] Level generation system (50+ playable levels)
- [x] Save system foundations
- [x] Gameplay loop (selection, matching, cascading)
- [x] Win/Lose detection
- [x] Pause/Resume functionality
- [x] Level progression and unlocking

### 🚀 Ready for Next Phase
- [ ] Final art assets (tiles, backgrounds, UI)
- [ ] Audio system integration
- [ ] Animation polish (DOTween)
- [ ] Particle effects
- [ ] Platform-specific optimizations

## Game Flow

```
Splash Screen (2 seconds)
    ↓
Bootstrap (Initialize Services)
    ↓
Main Menu
    ├─ Play → Level Select
    ├─ Settings (placeholder)
    └─ Quit
        ↓
    Level Select (50 levels)
        ↓
    Gameplay
        ├─ Win → Next Level or Menu
        └─ Lose → Retry or Menu
```

## Scenes

### Splash (`Assets/Scenes/Splash.unity`)
- Branding intro (2-second display)
- Auto-transitions to Bootstrap

### Bootstrap (`Assets/Scenes/Bootstrap.unity`)
- App initialization and service registration
- Persists across scene loads via DontDestroyOnLoad
- Auto-loads Main Menu

### Main Menu (`Assets/Scenes/MainMenu.unity`)
- Navigation hub
- Play, Settings, Quit options
- Coin display

### Level Select (`Assets/Scenes/LevelSelect.unity`)
- 50 dynamic level buttons
- Level unlock system (Progressive unlocking)
- Difficulty indication

### Gameplay (`Assets/Scenes/Gameplay.unity`)
- Board and tray display
- Game controls (Pause, Restart)
- HUD with level information
- Win/Lose popups

## Systems

### Board System
- Supports multi-layer tiles with cascading logic
- Tracks tile relationships (blocking, covering)
- O(1) tile lookups by ID, coordinate, or type
- Automatic exposure when tiles above are removed

### Matching System
- Pluggable match rules (ExactCountMatchRule by default)
- Configurable per-level match count (3, 4, 5...)
- Zero-allocation evaluation

### Tray System
- Fixed-capacity tile holder
- Per-type count tracking (O(1) lookups)
- Atomic insertion and removal

### Level System
- Deterministic procedural generation per level
- Supports 50+ levels with increasing difficulty
- Configurable: tile types, board size, match count, tray capacity

### State Machine
- Guarded transitions (Ready → Animating → Ready/Paused/Won/Lost)
- Prevents invalid state combinations
- Event-driven state changes

## Gameplay Mechanics

### Tile Selection
- Click an exposed (uncovered) tile
- Tile moves to tray
- Match detection runs
- Board cascades if needed

### Matching
- **Match Count**: Configurable per level (3-5 tiles typically)
- **Matching**: When N tiles of same type in tray, they're removed
- **Cascading**: Multiple matches can occur in sequence

### Cascading
- When tiles removed, tiles above fall and become exposed
- Newly exposed tiles can form new matches
- Process repeats until no more matches

### Win Condition
- All tiles removed from board
- AND tray is empty

### Lose Condition
- No selectable tiles remain
- AND tray still has tiles
- OR tray becomes full and can't fit more tiles

## Save System

Currently uses **PlayerPrefs** (temporary):
- `CurrentLevel`: Current level index
- `Level_{index}_Unlocked`: Per-level unlock status
- `Coins`: Currency placeholder
- Audio and graphics settings

**Production Implementation** (deferred):
- Secure file-based save
- Cloud sync support
- Version migration

## Controls

### Main Menu
- **Play Button**: Go to Level Select
- **Settings Button**: Placeholder
- **Quit Button**: Exit game

### Level Select
- **Level Button**: Select level
- **Back Button**: Return to Main Menu

### Gameplay
- **Tile Click**: Select tile (move to tray)
- **Pause Button**: Show pause menu
- **Restart Button**: Restart level

### Popups
- **Next**: Progress to next level (Win)
- **Retry**: Retry current level (Lose/Pause)
- **Menu**: Return to Main Menu (Pause/Win/Lose)
- **Resume**: Continue gameplay (Pause)

## Extending the System

### Adding a New Match Rule
```csharp
public class WildcardMatchRule : IMatchRule {
    public bool TryFindMatch(ITrayView tray, TileTypeId justInserted, out MatchResult result) {
        // Custom matching logic
    }
}

// Use in GameFlowController:
_matchSystem = new MatchSystem(new WildcardMatchRule());
```

### Adding a New Service
```csharp
// 1. Define interface in Core/Services
public interface INewService { /* methods */ }

// 2. Create implementation
public class NewService : INewService { /* implementation */ }

// 3. Register in GameRoot.InitializeServicesAsync()
GameServices.Register(/* ... */, new NewService(), /* ... */);

// 4. Access via GameServices.NewService in gameplay
```

### Adding New Scenes
```csharp
// 1. Create scene in Assets/Scenes/
// 2. Add to build settings
// 3. Extend SceneRoot for initialization
// 4. Create UI controllers as needed
// 5. Update scene navigation in existing controllers
```

## Performance Targets

- **Main Menu Load**: < 100ms
- **Level Load**: < 500ms
- **Gameplay FPS**: 60 FPS target
- **Memory Usage**: < 100MB
- **GC Pressure**: Zero-allocation gameplay loop

## Known Limitations

### Current (Placeholder)
- Visual assets are solid colors only
- Audio system is a no-op placeholder
- Save system uses PlayerPrefs (not secure)
- Level generation is procedural (not final art)
- No animations beyond basic scale/fade

### By Design
- Single-player only (current architecture)
- Deterministic level generation (no procedural variation)
- No time limits or move restrictions (extensible via LevelRuleSet)

## Development Roadmap

### Phase 1 ✅ (CURRENT)
- Core architecture
- Gameplay loop
- Level generation
- Basic UI
- Save system foundations

### Phase 2 🚀 (NEXT)
- Final art assets
- Audio system implementation
- Animation polish
- Particle effects
- Platform optimization

### Phase 3 (FUTURE)
- Image-based level editor
- Level packs and campaigns
- Cosmetics and shop
- Daily rewards
- Tutorial and onboarding

## Documentation

- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Layered architecture deep dive
- **[VERTICAL_SLICE.md](./VERTICAL_SLICE.md)** - MVP implementation details
- **Inline comments** - Code documentation in scripts

## Contributing

### Code Style
- PascalCase for public types and methods
- camelCase for private fields and local variables
- Concise comments explaining WHY, not WHAT
- Prefer specific types over generic base classes

### Architecture Rules
1. Domain layer has zero Unity dependencies
2. Presentation layer depends on Domain and Core
3. Core layer has no gameplay dependencies
4. Services are registered in GameRoot only
5. Gameplay objects are constructed explicitly, not resolved

## License

(Add appropriate license here)

## Credits

- **Design**: Puzzle mechanics and game flow
- **Implementation**: Full vertical slice (MVP)
- **Art**: Placeholder assets (final art TBD)

---

**Built with production-quality architecture from day one.** Ready to scale from prototype to shipped product.
