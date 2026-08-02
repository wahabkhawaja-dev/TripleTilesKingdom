# DECISIONS.md

ADR-style log of notable architecture decisions and their rationale, so future sessions (human or AI) have context, not just conclusions. Newest first.

## 2026-08-03 — Real Android haptic amplitude control via `AndroidJavaObject` reflection, not a native plugin

**Decision:** Superseded the 2026-08-01 `Handheld.Vibrate` baseline decision below. `HapticsService` now drives `android.os.Vibrator`/`VibrationEffect.createOneShot(duration, amplitude)` directly via `AndroidJavaObject`/`AndroidJavaClass` reflection (API 26+, with a plain-duration fallback below that). iOS still falls back to `Handheld.Vibrate()`, reserved for `Heavy` only. A 0.12s cooldown prevents rapid taps from stacking into a buzzing mess.

**Why:** `Handheld.Vibrate()` is single-intensity/single-duration on every platform — every `HapticStrength` produced an identical physical buzz, which is why tile taps and match pops felt the same and both read as "too strong." The user asked for taps to feel soft and pops to feel distinguishably different, which is impossible without real amplitude control. `android.os.Vibrator` provides that with zero native plugin dependency — the `AndroidJavaObject` bridge is part of Unity's Android support already.

**Alternative considered:** `Lofelt/NiceVibrations` (user-suggested). Rejected — the GitHub repo is archived, has no installable UPM package (its `unity/NiceVibrations/Packages/manifest.json` is a full sample-project structure, not a redistributable package), and adopting it would mean manually vendoring native Android/iOS binaries from `unity/NiceVibrations/Assets/NiceVibrations/Plugins/`, which isn't something that can be done safely without hand-verifying the binaries. Revisit if iOS-side amplitude control (Core Haptics) becomes a priority, since the `AndroidJavaObject` approach only solves Android.

## 2026-08-03 — Audio sources are scene-authored on `GameRoot`, not constructed at runtime

**Decision:** `AudioService` no longer creates its own `AudioSource` GameObjects internally. `GameRoot` now owns `[SerializeField] AudioListener` + `[SerializeField] AudioSource _musicSource` + `[SerializeField] AudioSource[] _sfxSources` (with a `BuildAudioFallback()` runtime-construction path used only if those refs are empty), and passes them into `AudioService`'s constructor. `Tools > Build Game Scenes` bakes the real components into `Bootstrap.unity`.

**Why:** The original runtime-constructed sources were invisible in the Editor outside Play mode, which is inconsistent with this project's established scene-authored-preferred / runtime-fallback pattern used everywhere else (see `ARCHITECTURE.md`), and made the audio system hard to inspect or debug ("I don't see any audio sources" bug report). It also surfaced the real root cause of a separate silence bug: the project had no `AudioListener` anywhere, since Canvas-only UI scenes never needed a camera — `AudioSource.isPlaying`/`.time` report normally even with zero listeners present, so the missing listener was invisible to inspection. Putting a single `AudioListener` on the persistent `GameRoot` guarantees exactly one exists for the app's lifetime regardless of active scene.

## 2026-08-01 — Domain layer physically separated from Gameplay controllers

**Decision:** Introduced a top-level `Domain/` folder (`Board`, `Tray`, `Matching`, `Levels`, `State`, `Obstacles`) containing only pure C# with zero `UnityEngine` references. `Gameplay/` (not yet built) will hold MonoBehaviour controllers that wrap `Domain` instances, per the original §6.2 split — but now the boundary is a folder/assembly boundary, not just a code-review convention.

**Why:** The stated goal was a gameplay simulation "runnable and unit-testable without Unity rendering." A convention ("try not to reference UnityEngine in model classes") erodes over time — someone reaches for `Vector2Int` or `Mathf.Clamp` because it's convenient, and the boundary is gone. A folder (soon an `.asmdef` with no Unity engine assembly reference) makes the violation a compile error instead of a review comment.

## 2026-08-01 — TileState vs. IsSelected are separate dimensions

**Decision:** `TileModel` has both a 3-value `TileState` (`Covered`/`Exposed`/`Removed` — board lifecycle) and an independent `IsSelected` bool (tray occupancy), rather than folding "selected" into the state enum as a fourth value.

**Why:** A tile leaves the board's indexes (`BoardModel.TrySelectTile` sets `State = Removed`) the instant it's tapped — before any animation plays, before the tray has resolved whether it's part of a match. If "selected" were a `TileState` value, a tile that's tapped-but-not-yet-matched and a tile that's fully cleared-and-forgotten would be indistinguishable without a second flag anyway. Keeping them separate now avoids a confusing enum with overlapping meanings later (e.g. what does `Removed`-but-still-`Selected` mean vs. plain `Removed`? Nothing — because `Removed` always implies it left the board, and `IsSelected` answers the orthogonal "does the tray still care about it" question).

## 2026-08-01 — BoardModel assumes pre-wired tile relationships; stacking resolution lives in StackingResolver

**Decision:** `BoardModel`'s constructor does not compute which tiles block which — it assumes the `TileModel` list handed to it already has correct `Blockers`/`Covers` via `StackingResolver.ResolveStacking` (or, later, whatever `BoardGenerator` produces). `BoardModel` itself only handles runtime queries and the tap→cascade→reveal flow.

**Why:** Single Responsibility — "how do we decide what covers what from a set of coordinates" is a board-*generation* concern (image sampling, manual layout, and eventually procedural generation all need to answer it identically), while "given known relationships, how do taps/removal/reveal behave" is a board-*simulation* concern. Conflating them would mean `BoardModel` grows a new branch every time a new layout source is added. `StackingResolver`'s default rule (same X/Y column, higher Layer blocks lower) matches the stacking rule already specified in `ARCHITECTURE.md` §8.2.

**Trade-off accepted:** any code constructing a `BoardModel` directly (including tests) must remember to call `StackingResolver.ResolveStacking` first, or every tile will incorrectly default to `Exposed` regardless of intended stacking. Worth a guard/warning if this trips people up in practice.

## 2026-08-01 — Addressables package not integrated yet

**Decision:** `AddressablesService` no longer calls into the `UnityEngine.AddressableAssets` API. It's a placeholder (`IsInitialized = true` immediately, `LoadAssetAsync` logs a warning and returns null) with zero dependency on the Addressables package.

**Why:** Nothing in the project loads content yet — pulling in the package and its runtime initialization now would be an unused dependency with no payoff. The interface (`IAddressablesService`) stays in place so this is a one-line swap in `GameRoot` once the Level System (image-based generation, theme content) actually needs to load bundled or remote assets.

## 2026-08-01 — Walking skeleton: no-op placeholders for Save and Audio

**Decision:** `GameRoot` registers `NoOpSaveService` and `NoOpAudioService` now, instead of deferring `GameServices` registration until those systems are designed.

**Why:** Save and Audio are substantial systems with real design decisions still to come (build order steps 10 and 6). Blocking Core Infrastructure on their design would delay every downstream system. Since every consumer depends on the *interface*, not the concrete type, swapping in the real implementation later is a one-line change in `GameRoot.InitializeServicesAsync()` with zero call-site impact.

**Trade-off accepted:** any code that calls `GameServices.Save.Save(...)` before step 10 silently does nothing (logged as a warning for Load). This is acceptable pre-launch; would not be acceptable to ship.

## 2026-08-01 — No dependency injection container

**Decision:** Explicit composition (`GameRoot`, future `SceneRoot`) instead of a DI container (Zenject/VContext/Reflex). `GameServices` is a minimal, scoped Service Locator for app-lifetime infrastructure only — never for gameplay objects.

**Why:** With the current system count (~10–15), explicit wiring is more debuggable — you can jump to exactly where an object was constructed with no container indirection. See `ARCHITECTURE.md` §16 risk #4 for the revisit condition (if `SceneRoot.Awake()` wiring exceeds ~40-50 lines, split into sub-composers before reaching for DI).

## 2026-08-01 — Events are structs, not classes

**Decision:** Every `IGameEvent` implementation must be a `struct`.

**Why:** `EventBus.Publish<T>` is called on gameplay-critical paths (every match, every tile removal). Struct events avoid a heap allocation per publish; the generic `Action<T>` invocation avoids boxing. Enforced via the `where T : struct, IGameEvent` constraint on `IEventBus`, not just convention.

## 2026-08-01 — Haptics baseline is `Handheld.Vibrate`, not a native plugin

**Superseded 2026-08-03** — see the Android `AndroidJavaObject`/`VibrationEffect` decision above; `HapticStrength` now has a real, audible-difference effect on Android.

**Decision:** Ship `HapticsService` on Unity's built-in `Handheld.Vibrate` for now; `HapticStrength` (Light/Medium/Heavy) is defined but currently has no effect since `Handheld.Vibrate` is single-intensity on both platforms.

**Why:** Avoids taking on a native plugin dependency before the core loop exists. The interface already models graduated intensity so `JuiceDirector` call sites won't need to change when a real amplitude-controlled implementation (Android `VibrationEffect`, iOS Core Haptics) is swapped in. Tracked in `ROADMAP.md` under "Not yet scheduled."

## 2026-08-01 — PoolService.Dispose does not track active instances

**Decision:** `PoolService.Dispose()` only destroys currently-*inactive* pooled instances. Instances still active at teardown (e.g. a tile mid-flight animation when a level is abandoned) are the responsibility of their owning controller or full scene unload.

**Why:** A generic pool has no way to know an active instance's owner without added coupling. Documented here explicitly rather than silently assumed, so `BoardController` (build order step 4) is designed with this responsibility in mind from the start.
