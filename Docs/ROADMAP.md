# ROADMAP.md

Phased build order, mirrored from `ARCHITECTURE.md` §19. Check items off as they're implemented and approved; do not reorder without updating both files.

**Last updated:** 2026-08-03

- [x] **1. Core Infrastructure** — `GameRoot`, `GameServices`, `EventBus`, Object Pooling foundation (`IObjectPool<T>`, `GenericObjectPool<T>`, `PoolService`, `IPoolable`). Placeholder Save/Audio/Analytics/Content services wired as no-ops.
- [x] **2. Board/Tile/Tray model layer** — pure C#, unit-testable, no scene required. Implemented under `Domain/` (see `SYSTEMS.md`): `BoardModel`, `TileModel`, `TrayModel`, `MatchSystem`, `LevelModel`, `BoardStateMachine`. Not yet wired to a scene or to unit tests — next step (3) should include an EditMode test suite proving this layer before controllers are built on top of it.
- [x] **3. Manual level layout + `TileDistributor`**
- [x] **4. Board/Tile/Tray view+controller layer + basic input** — `BoardController`, `TrayController` built and iterated on heavily (sizing, layout, match-pop bug fixes — see `CHANGELOG.md` 2026-08-03).
- [x] **5. Match detection + win/lose flow**
- [x] **6. Juice pass** — PrimeTween (project uses PrimeTween, not DOTween), particle bursts, real per-strength haptics (Android amplitude control), real `AudioService` (BGM loop + UI/tile/pop sfx with pitch variation). Camera feedback not yet added.
- [x] **7. UI layer** — HUD, popups, splash screen (tray-based loading visual), portrait `CanvasScaler` fix, new sprite-sheet-based UI theme.
- [ ] **8. Image-based level generation pipeline**
- [ ] **9. Theme system**
- [ ] **10. Save system** — real `ISaveService` implementation.
- [ ] **11. Obstacle system extension point + first obstacle** (e.g. Locked Tile) as a proof of the extensibility model.

Per the 2026-08 commit history (`Project Setup done` → `Game UI implementation` → `Game Sound Added` → `Game Completion`), the core gameplay loop, UI, and audio are now in place end-to-end; remaining roadmap items are content-pipeline and persistence work, not core-loop gaps.

## Not yet scheduled

- Analytics vendor selection and real `IAnalyticsService` implementation.
- iOS Core Haptics amplitude control (Android already has real amplitude/duration control via `AndroidJavaObject`/`VibrationEffect` as of 2026-08-03 — see `DECISIONS.md`; iOS still falls back to a single-intensity `Handheld.Vibrate()` on Heavy only).
- Assembly definitions (`.asmdef`) to enforce compile-time boundaries between Core/Gameplay/Presentation/UI — deferred until there's enough code for the boundaries to matter; revisit once step 4–5 land.
