# CHANGELOG.md

All notable changes to the project, dated, most recent first.

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
