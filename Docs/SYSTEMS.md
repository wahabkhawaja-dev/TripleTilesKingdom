# SYSTEMS.md

Living registry of every manager/service/event type, its owner, and its subscribers. Update this whenever a system, event type, or GameServices entry is added or changed. Cross-reference `ARCHITECTURE.md` for design rationale — this file is the current-state index, not the explanation.

**Last updated:** 2026-08-03

## GameServices registrations (app-lifetime singletons)

| Service | Interface | Current implementation | Real implementation lands | Owner |
|---|---|---|---|---|
| EventBus | `Core.EventBus.IEventBus` | `Core.EventBus.EventBus` (real) | — | `GameRoot` |
| Save | `Core.Services.ISaveService` | `NoOpSaveService` (placeholder) | Build order step 10 | `GameRoot` |
| Audio | `Core.Services.IAudioService` | `AudioService` (real — pooled sfx sources + looping BGM, sources scene-authored on `GameRoot`; see `DECISIONS.md` 2026-08-03) | — | `GameRoot` |
| Content | `Core.Services.IAddressablesService` | `AddressablesService` (placeholder, no Addressables package integrated yet) | When Level System needs bundled/remote content | `GameRoot` |
| Analytics | `Core.Services.IAnalyticsService` | `NoOpAnalyticsService` (placeholder) | Not yet scheduled — vendor TBD | `GameRoot` |
| Haptics | `Core.Services.IHapticsService` | `HapticsService` (real — Android: `AndroidJavaObject` reflection against `VibrationEffect` for genuine per-strength amplitude/duration; iOS: `Handheld.Vibrate` on Heavy only. See `DECISIONS.md` 2026-08-03) | iOS Core Haptics amplitude control — not yet scheduled | `GameRoot` |

All registered once, in this order, by `GameRoot.InitializeServicesAsync()`. `GameRoot` also owns the scene-authored `AudioListener` + `AudioSource` pool (`_musicSource`, `_sfxSources[6]`) passed into `AudioService`'s constructor, baked into `Bootstrap.unity` by `Tools > Build Game Scenes`.

## Audio content (`AudioTheme_Default.asset`, `Core.Services.AudioThemeSO`)

| Clip key | Trigger | Notes |
|---|---|---|
| `BackgroundBGM` | `GameRoot.Awake`, after first scene loads | Looping, started post-`LoadScene` (see `DECISIONS.md`/`CHANGELOG.md` 2026-08-03 for the pre-transition `Play()` timing quirk this avoids). |
| `MenuButtonClick` | `Clickable.OnPointerClick` (any UI button with `_pressJuice = true`) | Tiles set `_pressJuice = false` so they don't double-fire this alongside `TileSelectSound`. |
| `TileSelectSound` | `TileView.OnClicked` | Fires alongside `HapticStrength.Light`. |
| `TilePopSound` | `TrayController.PlayMatchPopSequence` (per popped tile, on `OnComplete`) | Pitch ramps per pop index (`1f + index * 0.06f`, capped at `1.35f`); fires alongside `HapticStrength.Heavy`. |

## Presentation (world-space sprites for gameplay, uGUI for HUD)

Board and tray are world-space `SpriteRenderer` hierarchies; HUD stays on a Screen-Space Overlay Canvas. Draw order is driven by explicit `sortingOrder` — never by sibling index or transform.z — so stacked pyramid layers never fight each other for visibility.

| SortingOrder band | Content |
|---|---|
| `layer × 10` .. `layer × 10 + 1` | Board tile base + fruit at layer L (up to ~100 for a 10-layer pyramid) |
| 500 | Tray bar |
| 501 / 502 | Tray slot backgrounds / icons |
| 1500 | Flying tile mid-fly (reparented under `FlyLayer`) |
| Overlay canvas | HUD (always on top of the world) |

Tap flow (`Presentation.GameFlowController.OnTileSelected`): model mutations happen up-front (tray insert, board select, match evaluation), the tile physically flies to its slot via `BoardController.FlyTileToSlot`, and every visible response (`TrayController.ShowSlotIcon`, `TrayController.Refresh`, `BoardController.Refresh`, next-turn unlock) is deferred to the fly's landing callback so the tile is *there* before the tray reveals its icon or the match pops.

## Scene-scoped systems (not in GameServices)

| System | Type | Constructed by | Notes |
|---|---|---|---|
| Object pooling | `Presentation.Pooling.PoolService` | `SceneRoot` (future) | One instance per gameplay scene; disposed on teardown. See `Presentation/Pooling/PoolService.cs` for the "active instances not tracked on Dispose" limitation. |

## Domain layer (pure C#, `Domain/` — build order step 2, complete)

No MonoBehaviours, no `UnityEngine` dependency. Constructed by whatever loads a level (a future `Gameplay/Flow` controller); not registered in `GameServices` — these are per-level instances, not app singletons.

| Type | Namespace | Responsibility |
|---|---|---|
| `BoardCoordinate` | `Domain.Board` | Immutable X/Y/Layer position, value equality. |
| `TileId`, `TileTypeId`, `TileIdFactory` | `Domain.Board` | Strongly-typed instance/type identifiers. |
| `TileState` | `Domain.Board` | Covered / Exposed / Removed (board lifecycle only). |
| `TileModel` | `Domain.Board` | One tile: identity, coordinate, state, blocker/covers relationships, obstacle, `IsSelected`. |
| `StackingResolver` | `Domain.Board` | Wires Blocker/Covers relationships from coordinates at board-construction time (same X/Y, higher Layer = blocker). |
| `BoardModel` | `Domain.Board` | Owns all tiles; O(1)/O(k) indexed queries by id/coordinate/type; `TrySelectTile` + exposure cascade; `TileExposed`/`TileRemoved`/`BoardCleared` events. |
| `IObstacleState`, `NullObstacleState` | `Domain.Obstacles` | Obstacle extension point + Null Object default. No concrete obstacles yet (build order step 11). |
| `ITrayView`, `TrayModel` | `Domain.Tray` | Fixed-capacity slot array, O(1) per-type counts, insert/remove, full/empty events. |
| `MatchResult`, `IMatchRule`, `ExactCountMatchRule`, `MatchSystem` | `Domain.Matching` | Pure match query against `ITrayView`; swappable rule strategy. |
| `TileSpawnData`, `LevelRuleSet`, `LevelModel` | `Domain.Levels` | Pure-data level definition: baked layout, tray size, match count, theme id, extensible rule bag. |
| `BoardState`, `BoardStateMachine` | `Domain.State` | Guarded gameplay flow state machine (Loading/Ready/Animating/Paused/Won/Lost). |

## Level System (SO-driven, `LevelSystem/*`)

Data-only ScriptableObjects that designers author, and one builder that converts them into a `Domain.Levels.LevelModel`. Runtime path: `GameFlowController.InitializeGame` loads `Resources/Levels/LevelCollection.asset` and calls `LevelDefinitionBuilder.Build(...)`; if the asset is missing it falls back to the procedural `LevelGenerator`. See `LEVEL_SYSTEM.md` for the full guide.

| Type | Namespace | Role |
|---|---|---|
| `LayerDefinition` | `LevelSystem.Data` | One rectangular grid layer (Width, Height, Offset, AutoCenter). |
| `LevelDefinitionSO` | `LevelSystem.Data` | One level asset: rules, content knobs, layer list, optional preset template. |
| `LevelPresetSO` | `LevelSystem.Data` | Reusable shape preset (PyramidShrink / TowerFlat / DoublePyramid / Custom). |
| `LevelCollectionSO` | `LevelSystem.Data` | Ordered list of `LevelDefinitionSO`s. Runtime picks `Resources/Levels/LevelCollection.asset`. |
| `LevelDefinitionBuilder` | `LevelSystem.Generation` | Converts SO → `LevelModel`; clamps oversize upper layers, trims tail cells for MatchCount divisibility, deterministic shuffle from Seed. |
| `LevelDesignerWindow` (Editor) | `LevelSystem.EditorTools` | `Tools ▸ Level Designer` — collection + level authoring with preview and preset apply. |
| `LevelDefinitionSOEditor` (Editor) | `LevelSystem.EditorTools` | Custom inspector: apply preset, reset, live pyramid preview, validation. |

## Event types (`IGameEvent` implementations, event bus)

None yet — the `Domain` model layer above raises its own plain C# events (`BoardModel.TileExposed`, `TrayModel.TileInserted`, etc.) directly to whatever controller owns it; these are NOT yet bridged onto the app-wide `EventBus`. That bridging (Domain event → `IGameEvent` → `EventBus.Publish`) happens once `Gameplay/Flow/GameFlowController` exists (build order step 4-5), per `ARCHITECTURE.md` §6.3's direct-call-vs-event split. This table stays empty until then.

| Event | Publisher | Subscribers |
|---|---|---|
| _(none yet)_ | | |

## Pooled resource ids

None yet — populated once `BoardController`/`TileController`/VFX systems start calling `PoolService.GetOrCreatePool`.

| Pool id | Component type | Created by |
|---|---|---|
| _(none yet)_ | | |
