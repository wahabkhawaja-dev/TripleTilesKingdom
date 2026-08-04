# CHANGELOG.md

All notable changes to the project, dated, most recent first.

## 2026-08-04 — Sprite-based tiles & tray, real fly-to-slot landing

**Changed**
- **TileView** rewritten as a world-space SpriteRenderer prefab (two `SpriteRenderer` children — Base + Fruit — plus a `BoxCollider2D` for `OnMouseDown` input). Draw order is driven by explicit `sortingOrder` per board layer (`layer × 10 + subOrder`), not by sibling index or transform.z — the sorting bugs from the uGUI implementation are gone by construction because a layer-2 tile's band strictly exceeds a layer-0 tile's band regardless of instantiation order.
- **BoardController** rewritten around plain `Transform`s in world space. Positions use `(X + layer × 0.5) × spacing` (same visual nesting math as before, no legacy xCorrection). Auto-spawns a shared `FlyLayer` transform where in-flight tiles are reparented at sortingOrder 1500 so they always draw above every board layer and the tray bar.
- **TrayController** rewritten as world-space sprites. Slot bg / icon sortingOrder pinned to 501 / 502; tray bar to 500. Removed the tray's own "insert pop" — insertions now come in via `ShowSlotIcon(slotIndex, type)`, called by GameFlowController the frame the flying tile lands.
- **GameFlowController** tap flow rewritten so the visible response (slot-icon reveal, match pop, board refresh, next-turn unlock) is deferred until the flying tile physically arrives at the tray. Model mutations still happen up-front so state is consistent, but the tile no longer "appears in the tray out of nowhere" — it lands there. `_boardRoot` / `_trayRoot` field types changed from `RectTransform` to `Transform`; adds a camera reference and portrait ortho settings baked in.
- **Hammer** power-up now flies its tile off the top of the play area instead of vanishing on the spot.

**Added**
- `Editor/TilePrefabBuilder` — `Tools ▸ Rebuild Tile Prefab` (also runs automatically at the start of `Tools ▸ Build Game Scenes`). Generates `Assets/Resources/Prefabs/TileView.prefab` with the right SpriteRenderer children and `TileView` serialized-field wiring, so pulling this refactor doesn't leave a stale UI-based prefab on disk.

**Notes**
- HUD stays uGUI on a Screen-Space Overlay Canvas — buttons, popups, splash all unchanged.
- Sorting scheme: board layers 0-10 (0-100), tray bar 500, slot bg 501, slot icon 502, flying tile 1500, HUD Canvas Overlay (always on top).

## 2026-08-04 — SO-only levels, dynamic Level Select, complete-grid enforcement

**Added**
- `LevelSystem.Generation.PyramidSizeSolver` — finds a nearby pyramid whose total tile count is divisible by MatchCount. Prefers to grow (25→27) rather than shrink, per the user rule.
- `LevelDefinitionBuilder.SolveCompleteGridLayers` + `TotalTiles` — public helpers so the SO custom inspector previews the exact geometry the runtime will produce, and the "Auto-fix authored layers" button can bake it back into the asset.
- SO inspector: **Auto-fix authored layers** button next to Reset. Preview footer now shows "Effective: base W×H · N layers · T tiles (÷match = G groups)".

**Changed**
- `LevelDefinitionBuilder` no longer trims tail cells (which produced a visible notch). It now enforces complete rectangles by dropping the topmost layer(s) until the total divides, and only shrinks the top-remaining layer as a last resort.
- `BatchLevelGenerator.Configure` runs `PyramidSizeSolver` on the computed dimensions before writing the SO — every generated level starts already divisible, so the builder's auto-adjust is a no-op at runtime.
- `Presentation.GameFlowController.InitializeGame`: removed the `LevelGenerator` procedural fallback. If `Resources/Levels/LevelCollection.asset` is missing, logs a hard error pointing to the Master Level Designer instead of silently generating a level. Everything is configured in the Inspector now.
- `Presentation.UI.LevelSelectUI`: level button count is now `LevelCollectionSO.Count`, not the hardcoded 50. Scroll content height resizes to match. Empty collection produces one warning + zero buttons rather than 50 buttons pointing at nothing.

## 2026-08-04 — Master Level Designer + batch generator

**Added**
- `LevelSystem.Generation.BatchLevelGenerator` + `DifficultyCurve` — pure C# routine that fills a `LevelDefinitionSO` from a start/end curve at a given level index. Every difficulty axis (match count, base size, layer count, tray size, tile faces) has a start value, end value, and shared curve shape (Linear / EaseIn / EaseOut / EaseInOut). Cap-aware: layer count is clamped so a pyramid never runs out of room and produces a degenerate 0×0 top.
- Rewrote `LevelSystem.EditorTools.LevelDesignerWindow` as a tabbed **Master Level Designer** (Overview / Batch Generator / Levels / Presets). Batch Generator is the primary flow — slider for level count (default 50, matching Level Select's `MaxLevels`), start/end fields per axis, live preview snapshots (start / mid / end), and one-click generation that writes every `Level_NNN.asset` under `Assets/Resources/Levels/` and appends them to the collection. Overview tab has an inline how-to and status card; Presets tab lists every `LevelPresetSO` in the project with one-click "Apply to selected".
- Levels tab gained `Duplicate` and asks before deleting the underlying asset on `Remove`.

**Changed**
- `LEVEL_SYSTEM.md` rewritten around the Master Designer's tabs; setup guide is now a 60-second walkthrough that ends with a full 50-level ramp.

## 2026-08-04 — SO-driven Level System + editor tooling

**Added**
- `LevelSystem/Data/` ScriptableObjects for designer-authored levels: `LayerDefinition` (struct), `LevelDefinitionSO` (per-level rules + layer list), `LevelPresetSO` (reusable shape presets — PyramidShrink, TowerFlat, DoublePyramid, Custom), `LevelCollectionSO` (ordered level list loaded at boot).
- `LevelSystem/Generation/LevelDefinitionBuilder` — converts a `LevelDefinitionSO` into `Domain.Levels.LevelModel`. Fills each layer as a complete `w × h` grid, auto-centers upper layers over the layer below (clamps oversize layers rather than crashing), keeps every tile type's count a multiple of `MatchCount`, deterministic shuffle from `Seed`.
- `LevelSystem/Editor/LevelDesignerWindow` (`Tools ▸ Level Designer`) — pick/create a `LevelCollectionSO`, add/remove/reorder levels, apply presets, live pyramid preview per level.
- `LevelSystem/Editor/LevelDefinitionSOEditor` + `LevelPreviewDrawer` — custom inspector with "Apply Preset → Layers", "Reset to Classic 5-4-3", inline pyramid preview drawn from the actual builder output, and a validation panel warning about undersized upper layers or non-divisible tile counts.
- `Docs/LEVEL_SYSTEM.md` — full guide (model, types, runtime hookup, editor tool, setup from a fresh checkout).

**Changed**
- `Presentation.GameFlowController.InitializeGame` now prefers `Resources/Levels/LevelCollection.asset` if present, falling back to the procedural `LevelGenerator` otherwise — so a fresh checkout still boots into a playable level, and shipping just means dropping in an authored collection.
- `ROADMAP.md` / `SYSTEMS.md` updated with the new level-system entries.

**Notes**
- Pyramid stacking (only-topmost-tile-playable; removing a top tile exposes the four beneath) is handled by the existing `Domain.Board.StackingResolver.ResolvePyramidStacking`; the new system just supplies its input in a designer-friendly form.

## 2026-08-03 — Presentation polish pass: audio, haptics, UI sizing, board/tray bug fixes

**Added**
- Full audio system: `Core.Services.AudioThemeSO` (BGM + 3 sfx clips), rewritten `AudioService` (pooled sfx sources, per-call `pitch` param, looping BGM), wired into `GameRoot` via scene-authored `AudioListener` + `AudioSource` refs (with a `BuildAudioFallback()` runtime path matching the project's established scene-authored/fallback pattern). `Tools > Build Game Scenes` baked a real `AudioListener` and 7 real `AudioSource` GameObjects into `Bootstrap.unity`.
- `TileView` plays `TileSelectSound` on tap; `TrayController` plays `TilePopSound` with an ascending per-tile pitch cascade on match-pop; `Clickable` plays `MenuButtonClick` on generic UI button presses (gated by its existing `_pressJuice` flag so tiles don't double-fire it).
- Tray-slot-based splash loading screen (`SplashScreenUI`) replacing the old fill-bar: 6 slots pop in with staggered `Ease.OutBack` scale over 3 seconds, reusing the real tile/tray sprites for a more game-accurate loading visual.
- Real Android haptic amplitude/duration control in `HapticsService`, via `AndroidJavaObject`/`AndroidJavaClass` reflection against `android.os.Vibrator`/`VibrationEffect` (no native plugin) — see `DECISIONS.md`.

**Changed**
- `UIFactory.CreateScreenCanvas`: `CanvasScaler.referenceResolution` corrected from `1920×1080` (landscape) to `1080×1920` (portrait) — this was the root cause of the game view rendering too small on device.
- Board tiles, tray slots, and HUD buttons enlarged and re-spaced (`BoardController._tileSize` 118→175, `TrayController` slot metrics now scale dynamically to fit tray width, `GameplayHUD` icon/power-up button sizes and spacing increased) so the layout fills the portrait screen properly.
- `TileView.OnClicked` haptic strength: `Medium` → `Light` (softer tap feel); `TrayController` match-pop haptic stays `Heavy` — click and pop are now genuinely distinguishable on Android instead of both firing an identical `Handheld.Vibrate()` buzz.
- New higher-quality UI sprite sheet processed with Python/OpenCV (background removal via `cv2.floodFill` with `FLOODFILL_FIXED_RANGE`, then sliced via the modern `SpriteDataProviderFactories` API) and wired into `UITheme_Default.asset`, replacing lower-quality placeholder UI art.
- `BoardController.GetAnchoredPositionForTile` gained a per-layer `xCorrection` term to cancel a spurious rightward lean in the pyramid layout (root cause: `LevelGenerator`'s `layer/2` integer-division under-insets odd layers by half a cell on both axes — desired on Y for the "peek from below" look, unwanted on X). Pure code change; no scene/canvas/sprite assets touched, per explicit instruction.

**Fixed**
- Hint pulse tween on `TileView` got stuck mid-scale when the Hint button was tapped repeatedly on the same tile (two `Yoyo` scale tweens fighting over `localScale`). Fixed by tracking `_hintTween` and resetting to identity scale before starting a new pulse.
- Tray showed the 4th+ tile of a same-type run as invisible after a match-pop. Root cause: a blanket per-`Refresh()` `Sequence.Stop()` was applied to every slot index regardless of relevance, which killed *other* slots' legitimate in-progress pop animations (`Stop()` skips `OnComplete`, so `icon.enabled = false` never ran). Fixed with a targeted `StopStalePop(index, icon)` called only when that specific index is about to be reused.
- Match-pop pitch cascade gave every popped tile the same (final) pitch instead of an ascending sequence — a classic C# `for`-loop closure bug (the loop's own control variable was captured by reference in a delayed `OnComplete` callback). Fixed by capturing a fresh per-iteration local before use.
- A board-layer-centering bug from an earlier session (each layer recentered independently) broke `StackingResolver`'s shared-coordinate covering logic, making tiles render in positions that didn't match what they logically covered. Fixed by reverting to a single shared `_centerOffset` for all layers, with only a per-layer vertical `lift` remaining as the visual difference.
- Game was completely silent: no `AudioListener` existed anywhere in the project (Canvas-only UI scenes never needed a camera, so none was ever added). `AudioSource.isPlaying`/`.time` reported normal progressing values regardless — Unity just silently drops output with zero listeners. Fixed by adding a persistent `AudioListener` to `GameRoot`.
- `AudioService` originally built its own hidden runtime `AudioSource` GameObjects, invisible in the Editor outside Play mode ("I don't see any audio sources"). Restructured so `GameRoot` owns real, scene-authored `AudioSource`/`AudioListener` components (baked into `Bootstrap.unity` via the scene builder), matching the project's established scene-authored-preferred pattern.
- `AudioSource.Play()` called immediately before an instant `SceneManager.LoadScene()` could silently not actually start playing. Fixed by moving the initial `PlayMusic("BackgroundBGM")` call to after `LoadScene` returns.

**Evaluated, not adopted**
- `Lofelt/NiceVibrations` (user-suggested): repo is archived, has no installable UPM package (its `Packages/manifest.json` is a full sample-project structure, not a redistributable package), and would require manually vendoring native Android/iOS binaries — not safely automatable. The custom `AndroidJavaObject`-based Android implementation already gives equivalent real amplitude control on the platform where it matters most.

## 2026-08-01 — Domain layer (pure C# gameplay simulation)

**Added** (all under `Assets/_Project/Scripts/Domain/`, zero `UnityEngine` dependency)
- `Domain.Board`: `BoardCoordinate`, `TileId`/`TileIdFactory`, `TileTypeId`, `TileState`, `TileModel`, `StackingResolver`, `BoardModel`.
- `Domain.Obstacles`: `IObstacleState`, `NullObstacleState` (extension point only — no concrete obstacles yet).
- `Domain.Tray`: `ITrayView`, `TrayModel`.
- `Domain.Matching`: `MatchResult`, `IMatchRule`, `ExactCountMatchRule`, `MatchSystem`.
- `Domain.Levels`: `TileSpawnData`, `LevelRuleSet`, `LevelModel`.
- `Domain.State`: `BoardState`, `BoardStateMachine`.

**Docs**
- `ARCHITECTURE.md` §3 folder structure updated to reflect the `Domain/` vs `Gameplay/` split; new §6a explaining the refinement.
- `SYSTEMS.md` updated with the full Domain type registry.

**Not yet done**
- No EditMode unit tests written against this layer yet — should land before/alongside the next build-order step (manual level layout + controllers), since "unit-testable" was the entire point of this layer.
- Domain model events (`BoardModel.TileExposed`, `TrayModel.TileInserted`, etc.) are not yet bridged onto the app-wide `EventBus` — that happens once `GameFlowController` exists.

## 2026-08-01 — Remove premature Addressables dependency

**Changed**
- `Core.Services.AddressablesService` no longer references the Addressables package; it's now a placeholder implementation until the Level System actually needs to load content. See `DECISIONS.md`.

## 2026-08-01 — Core Infrastructure

**Added**
- `Core.EventBus`: `IGameEvent`, `IEventBus`, `EventBus` — struct-based, allocation-free-on-publish pub/sub.
- `Core.Services`: `ISaveService`/`NoOpSaveService`, `IAudioService`/`NoOpAudioService`, `IAnalyticsService`/`NoOpAnalyticsService`, `IHapticsService`/`HapticsService` (real, baseline), `IAddressablesService`/`AddressablesService` (real), `GameServices` static locator.
- `Core.Bootstrap.GameRoot` — app-lifetime boot sequence, constructs and registers all infrastructure services in a fixed order, persists via `DontDestroyOnLoad`, hands off to the first scene.
- `Presentation.Pooling`: `IPoolable`, `IObjectPool<T>`, `GenericObjectPool<T>`, `PoolService` — scene-scoped, allocation-free-after-prewarm object pooling.

**Docs**
- Created `PROJECT_OVERVIEW.md`, `SYSTEMS.md`, `ROADMAP.md`, `DECISIONS.md`.
- `ARCHITECTURE.md` finalized (root technical reference, all sections per initial design pass).

**Notes**
- `ISaveService` and `IAudioService` are wired with no-op placeholders pending their dedicated build-order steps (10 and 6 respectively) — see `DECISIONS.md`.
