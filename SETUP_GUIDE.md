# Setup Guide - Triple Tiles Kingdom

## Initial Project Setup

### Step 1: Open the Project
1. Clone or open the project in Unity 6000.3.19f1+
2. Wait for initial import to complete
3. Open the Project window

### Step 2: Initialize Scenes
Two options:

#### Option A: Automatic Setup (Recommended)
1. Go to **Tools** menu → **Setup All Scenes**
2. This creates and configures all 5 scenes automatically
3. Verify scenes exist in `Assets/Scenes/`

#### Option B: Manual Setup
1. Create each scene manually in `Assets/Scenes/`
2. Use the scene setup guide below for hierarchy details

### Step 3: Build Settings
1. Go to **File** → **Build Settings**
2. Verify scenes are in order:
   - 0: Splash
   - 1: Bootstrap
   - 2: MainMenu
   - 3: LevelSelect
   - 4: Gameplay
3. Set **Default Scene** to Splash

### Step 4: Project Settings
1. **Canvas Scaler** (for responsive UI):
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1080 x 1920

2. **Sorting Layers**:
   - Add: "Background", "Gameplay", "UI", "PopupUI"

3. **Tags**: (if not present)
   - Add: "Player", "Board", "Tray"

4. **Layers**: (if needed)
   - Add: "UI", "Board", "Tray"

## Scene Hierarchy Reference

### Splash Scene
```
Canvas (ScreenSpaceOverlay)
├── SplashImage (Image, placeholder background)
└── SplashScreenUI (Script: auto-transitions to Bootstrap after 2 seconds)

EventSystem (for UI input)
```

### Bootstrap Scene
```
GameRoot (MonoBehaviour)
├── Core.Bootstrap.GameRoot (Script)
└── DontDestroyOnLoadWrapper (keeps GameRoot alive across scenes)

No UI needed - this is initialization only
```

### Main Menu Scene
```
Canvas (ScreenSpaceOverlay)
├── Background (Image, dark background)
├── Logo (TextMeshPro, "Triple Tiles Kingdom")
├── PlayButton (Button)
│   └── Text (TextMeshPro, "Play")
├── SettingsButton (Button, placeholder)
│   └── Text (TextMeshPro, "Settings")
└── QuitButton (Button)
    └── Text (TextMeshPro, "Quit")

EventSystem (for UI input)
MainMenuUI (Script on Canvas)
```

### Level Select Scene
```
Canvas (ScreenSpaceOverlay)
├── Background (Image)
├── Title (TextMeshPro, "Select Level")
├── LevelButtons (GridLayoutGroup with 50 level buttons)
│   └── LevelButton[0-49]
│       └── Text (TextMeshPro, "Level N")
├── BackButton (Button)
│   └── Text (TextMeshPro, "Back")
└── ProgressText (TextMeshPro, shows unlocked levels)

EventSystem (for UI input)
LevelSelectUI (Script on Canvas)
```

### Gameplay Scene
```
MainCamera (Camera)
├── Clear Flags: Solid Color
├── Projection: Orthographic
└── Orthographic Size: 5

BoardRoot (Container)
├── TileView[0-35] (created at runtime by BoardController)
│   └── Image (colored square representing tile)

TrayRoot (Container, position: 0, -6, 0)
├── SlotView[0-11] (created at runtime by TrayController)
│   └── Image (slot visual)

HUDCanvas (Canvas, ScreenSpaceOverlay)
├── InfoPanel (TextMeshPro)
├── PauseButton (Button)
├── RestartButton (Button)
└── PopupContainer (parent for overlays)
    ├── PauseMenuPopup (instantiated at runtime)
    ├── WinPopup (instantiated at runtime)
    └── LosePopup (instantiated at runtime)

EventSystem (for UI input)

GameFlowController (orchestrates gameplay)
├── BoardController (manages board visuals)
├── TrayController (manages tray visuals)
└── GameplayHUD (manages UI and popups)
```

## Runtime Setup

### Scene Transitions Flow

```
1. Splash (2 seconds)
   ↓ [Auto]
2. Bootstrap
   ├─ Initialize Services
   ├─ Register with GameServices
   └─ Load Main Menu
   ↓ [User clicks Play]
3. Main Menu
   ↓ [User clicks Level]
4. Level Select
   ├─ Display 50 buttons
   ├─ Mark unlocked levels (from PlayerPrefs)
   └─ [User selects level]
5. Gameplay
   ├─ Load level (procedurally generated)
   ├─ Initialize board and tray
   ├─ Run gameplay loop
   └─ [User wins/loses]
   ↓ [User clicks Next/Menu]
3. Main Menu or 5. Gameplay [Next Level]
```

## Prefab Creation

While prefabs aren't strictly necessary for the vertical slice, they're useful for reuse:

### TileView Prefab
1. Create empty GameObject "TileView"
2. Add components:
   - Image (sprite: placeholder)
   - Button
   - CanvasGroup
3. Add child "Scale" (for animation)
4. Attach TileView.cs script
5. Drag to `Assets/_Project/Prefabs/`

### SlotView Prefab
1. Create empty GameObject "SlotView"
2. Add Image component
3. Drag to `Assets/_Project/Prefabs/`

### UI Prefabs (Buttons, Popups)
Similar process - create in scene, add components, save as prefab.

## Testing Checklist

### Boot Sequence
- [ ] Project opens without errors
- [ ] Splash scene loads
- [ ] 2-second delay occurs
- [ ] Bootstrap initializes (check Console for no errors)
- [ ] Main Menu appears

### Navigation
- [ ] Main Menu displays
- [ ] Play button navigates to Level Select
- [ ] Level Select shows 50 buttons
- [ ] Level 0 is selectable (unlocked)
- [ ] Levels 1+ are disabled initially
- [ ] Back button returns to Main Menu
- [ ] Selecting a level loads Gameplay

### Gameplay Basics
- [ ] Gameplay scene loads with board visible
- [ ] Tiles appear in 6x6 grid
- [ ] Tray appears below board
- [ ] Clicking a tile adds it to tray
- [ ] Pause button shows pause menu
- [ ] Resume returns to gameplay

### Win Condition
- [ ] Board clears completely
- [ ] Tray becomes empty
- [ ] Win popup appears
- [ ] Next button unlocks next level
- [ ] Menu button returns to Main Menu

### Lose Condition
- [ ] No valid moves remain
- [ ] Tray still has tiles
- [ ] Lose popup appears
- [ ] Retry button restarts level
- [ ] Menu button returns to Main Menu

### Save Persistence
- [ ] Complete level 0
- [ ] Return to Main Menu
- [ ] Go back to Level Select
- [ ] Level 1 is now unlocked
- [ ] Close game and reopen
- [ ] Level 1 remains unlocked

## Common Issues & Fixes

### Issue: "GameRoot is already initialized"
**Cause**: Bootstrap scene loaded twice
**Fix**: 
1. Check build settings - Bootstrap should only be index 1
2. Don't manually load Bootstrap in gameplay

### Issue: No tiles visible on board
**Cause**: TileView prefab missing or camera setup wrong
**Fix**:
1. Verify BoardController has TileView prefab assigned
2. Check MainCamera orthographic size (should be ~5)
3. Verify board root position (0, 0, 0)

### Issue: Buttons not responding to clicks
**Cause**: EventSystem missing or disabled
**Fix**:
1. Verify EventSystem exists in scene
2. Check Canvas render mode (should be ScreenSpaceOverlay)
3. Verify buttons have Button component and onClick listeners

### Issue: PlayerPrefs not persisting
**Cause**: Different project/build
**Fix**:
1. Verify project name is "TrippleTilesKingdom"
2. Check PlayerPrefs.HasKey() in editor PlayMode
3. Clear PlayerPrefs: `PlayerPrefs.DeleteAll()` in console

### Issue: Levels too easy/hard
**Cause**: Level generation needs tuning
**Fix**:
1. Edit `LevelGenerator.GenerateLevel()` difficulty calculation
2. Adjust match count progression
3. Adjust tray capacity scaling

## Next Steps

### Before Polish
1. ✅ Verify all scenes load
2. ✅ Confirm gameplay loop works
3. ✅ Test win/lose conditions
4. ✅ Verify save persistence

### Before Release
1. Replace placeholder colors with final art
2. Integrate audio (swap NoOpAudioService)
3. Add animations (DOTween integration)
4. Implement particle effects
5. Platform-specific optimizations

## Performance Profiling

### Check Frame Rate
1. Game → Stats (toggle in Game window)
2. Target: 60 FPS on mobile devices
3. Profile with Profiler window (Window → Analysis → Profiler)

### Memory Check
1. Profiler → Memory
2. Target: < 100MB for MVP

### GC Allocation Profiling
1. Profiler → Memory → Allocations
2. Gameplay should show zero allocation per frame
3. UI updates may allocate (acceptable)

## Common Customizations

### Change Initial Level
Edit `GameRoot.cs`:
```csharp
[SerializeField] private string _firstSceneName = "MainMenu"; // Change this
```

### Adjust Level Difficulty
Edit `LevelGenerator.GenerateLevel()`:
```csharp
var difficulty = 1f + (levelIndex * 0.15f); // Increase multiplier for harder
var matchCount = 3 + (levelIndex / 15); // Increase ramp for more matches
```

### Change UI Colors
Edit TileView.cs `GetTileColor()` or create a theme system

### Modify Tray Capacity
Edit `LevelGenerator.GenerateLevel()`:
```csharp
var trayCapacity = 8 + (levelIndex / 5); // Start at 8 instead of 6
```

## Editor Shortcuts

- **F5**: Play game from current scene
- **Ctrl+L**: Load Scenes in Build Order
- **Ctrl+Shift+B**: Open Build Settings
- **Window → Hierarchy**: Show scene tree
- **Window → Inspector**: Show component details

## Resources

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Deep dive into design
- [VERTICAL_SLICE.md](./VERTICAL_SLICE.md) - Feature list
- [README.md](./README.md) - Project overview
- Unity docs: https://docs.unity.com
- TextMesh Pro: Included, requires no setup

---

**Need help?** Check the inline code comments or review the example scenes.
