# CHANGELOG.md

All notable changes to the project, dated, most recent first.

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
