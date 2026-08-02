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
