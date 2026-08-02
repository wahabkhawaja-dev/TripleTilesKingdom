# ROADMAP.md

Phased build order, mirrored from `ARCHITECTURE.md` §19. Check items off as they're implemented and approved; do not reorder without updating both files.

**Last updated:** 2026-08-01

- [x] **1. Core Infrastructure** — `GameRoot`, `GameServices`, `EventBus`, Object Pooling foundation (`IObjectPool<T>`, `GenericObjectPool<T>`, `PoolService`, `IPoolable`). Placeholder Save/Audio/Analytics/Content services wired as no-ops.
- [x] **2. Board/Tile/Tray model layer** — pure C#, unit-testable, no scene required. Implemented under `Domain/` (see `SYSTEMS.md`): `BoardModel`, `TileModel`, `TrayModel`, `MatchSystem`, `LevelModel`, `BoardStateMachine`. Not yet wired to a scene or to unit tests — next step (3) should include an EditMode test suite proving this layer before controllers are built on top of it.
- [ ] **3. Manual level layout + `TileDistributor`** — simplest content path, proves the model layer.
- [ ] **4. Board/Tile/Tray view+controller layer + basic input** — first playable loop, no juice yet.
- [ ] **5. Match detection + win/lose flow**
- [ ] **6. Juice pass** — DOTween, camera feedback, particles, haptics, real `AudioService`.
- [ ] **7. UI layer** — HUD, popups.
- [ ] **8. Image-based level generation pipeline**
- [ ] **9. Theme system**
- [ ] **10. Save system** — real `ISaveService` implementation.
- [ ] **11. Obstacle system extension point + first obstacle** (e.g. Locked Tile) as a proof of the extensibility model.

## Not yet scheduled

- Analytics vendor selection and real `IAnalyticsService` implementation.
- Native haptics plugin (Android VibrationEffect / iOS Core Haptics) to replace baseline `Handheld.Vibrate`.
- Assembly definitions (`.asmdef`) to enforce compile-time boundaries between Core/Gameplay/Presentation/UI — deferred until there's enough code for the boundaries to matter; revisit once step 4–5 land.
