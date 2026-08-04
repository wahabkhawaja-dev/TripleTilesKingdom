# LEVEL_SYSTEM.md

SO-driven level authoring pipeline. Implements ARCHITECTURE.md §7 (LevelDefinitionSO) for the manual-layout path.

**Last updated:** 2026-08-04

## Model

Every level is a stack of rectangular grids (a **pyramid**):

- **Base layer** (index 0) — the biggest grid. Fills a `Width × Height` rectangle completely; no holes.
- **Higher layers** — smaller `w × h` grids, each fully populated, placed on top so a layer-(L+1) tile at cell (x, y) sits centered over the four layer-L tiles at (x, y), (x+1, y), (x, y+1), (x+1, y+1).
- **Only the topmost tiles at any column start playable.** Removing a top tile exposes the (up to four) tiles it was sitting on, which then become playable.

The pyramid stacking rule is enforced by `Domain.Board.StackingResolver.ResolvePyramidStacking`, which is already what the runtime uses. The new Level System just supplies its input geometry in a designer-friendly form.

## Types (runtime, `Assets/_Project/Scripts/LevelSystem`)

| Type | Namespace | Role |
|---|---|---|
| `LayerDefinition` | `LevelSystem.Data` | One layer: `Width`, `Height`, `Offset`, `AutoCenter`. |
| `LevelDefinitionSO` | `LevelSystem.Data` | One level asset: rules (MatchCount, TraySize), content knobs (TileTypeCount, Seed, ThemeId), ordered `Layers` list, optional `PresetTemplate`. |
| `LevelPresetSO` | `LevelSystem.Data` | Reusable shape preset (PyramidShrink / TowerFlat / DoublePyramid / Custom). Produces a `List<LayerDefinition>` via `BuildLayers()`. |
| `LevelCollectionSO` | `LevelSystem.Data` | Ordered list of `LevelDefinitionSO`s. Runtime picks it up from `Resources/Levels/LevelCollection.asset`. |
| `LevelDefinitionBuilder` | `LevelSystem.Generation` | Converts a `LevelDefinitionSO` into a `Domain.Levels.LevelModel` (clamps oversize upper layers, trims tail cells to keep tile counts divisible by MatchCount, deterministic shuffle from `Seed`). |

## Runtime hookup

`Presentation.GameFlowController.InitializeGame`:

1. Reads `Resources/Levels/LevelCollection.asset`.
2. If present and non-empty, uses `LevelDefinitionBuilder.Build(collection.Get(index))`.
3. Otherwise falls back to the procedural `LevelGenerator` so a fresh checkout without any level assets still boots.

No other systems change — `BoardGenerator`, `BoardModel`, `TileView`, HUD, tray, matching all consume the same `LevelModel` they did before.

## Master Level Designer window (`Tools ▸ Level Designer`)

One window, four tabs. Everything the pipeline needs, no context switching to the Project pane.

### Toolbar (always visible)
- **Collection** — the `LevelCollectionSO` currently being edited. The runtime reads `Assets/Resources/Levels/LevelCollection.asset`; if the loaded collection is at that path, the Overview tab confirms with a ✓.
- **Create at Resources/Levels** — one-click bootstrap when none exists.
- **Ping Runtime Asset** — highlights the runtime collection in the Project view.
- **Save** — flushes dirty flags on the collection and every level asset.

### Tab 1 — Overview
Status card (has-collection / at-runtime-path check), a numbered how-to, and shortcut buttons to the other tabs. This is the "you just opened the window, now what?" tab.

### Tab 2 — Batch Generator (primary workflow)
Fill a collection with a full 50-level ramp in seconds:

1. **How many levels** — slider, 1–200. Defaults to 50 so it matches the Level Select screen's `MaxLevels`.
2. **Replace existing** — on: wipes and regenerates from scratch. off: appends after existing levels.
3. **Difficulty curve** — every axis is `start → end`:
   - **Rules** — MatchCount, TraySize, Tile faces (0 = auto).
   - **Board** — Base Width, Base Height, Layer Count, Shrink per Layer, Shape (`PyramidShrink` / `TowerFlat` / `DoublePyramid`).
   - **Curve** — `Linear` (even ramp), `EaseIn` (gentle then steep), `EaseOut` (steep then gentle), `EaseInOut` (S-curve).
   - **Theme id** — string passed to the level's `ThemeId`.
   - **Deterministic seed** — if on, each level's shuffle is stable across sessions.
4. **Preview snapshots** — a live one-line readout for start / mid / end levels (match count, tray, base size, layer count, tile total) so you can dial the curve without generating anything.
5. **Preview level N** buttons — spawns a scratch `LevelDefinitionSO` in memory and pings it, so the standard inspector preview shows the pyramid before you commit.
6. **Generate** — confirmation prompt → creates one `LevelDefinitionSO` asset per level under `Assets/Resources/Levels/Level_NNN.asset`, appends them to the collection, saves. Jumps you to the Levels tab.

### Tab 3 — Levels
Ordered list on the left (numbered `001 · <name>`), full inline inspector on the right. Buttons: `+ New`, `Duplicate`, `Remove` (asks whether to also delete the asset file), `↑ Up` / `↓ Down`. The Preset field + Apply button on top of the inspector lets you overwrite the level's layers from any `LevelPresetSO` without leaving the tab.

### Tab 4 — Presets
Scans the whole project for `LevelPresetSO` assets and lists them with their key parameters. "Apply to selected" applies a preset to whatever level is currently selected in Tab 3, so this tab reads like a shape library.

### Per-level inspector (used inline by the Levels tab)
`LevelDefinitionSOEditor` — default fields, "Apply Preset → Layers", "Reset to Classic 5-4-3", live pyramid preview, and validation warnings (undersized upper layers, non-divisible tile counts).

## Setup guide (fresh checkout, 60 seconds)

1. Open the project in Unity 6.
2. `Tools ▸ Level Designer`.
3. **Overview** tab → **Create Collection at Assets/Resources/Levels/LevelCollection.asset**.
4. **Batch Generator** tab → keep default 50 levels or drag the slider.
5. Set the difficulty curve. Sensible starting values (already the defaults):
   - Match count 3 → 3 (keeps the core rule stable across the run).
   - Base size 4×4 → 7×7.
   - Layers 2 → 5.
   - Tray 7 → 7 (raise the end to 9 if you want later levels to breathe more).
   - Tile faces 3 → 6 (more variety as you climb).
   - Curve = Linear (or `EaseOut` for "easy start, difficulty spikes later").
6. Click **Generate 50 levels**. Assets appear under `Assets/Resources/Levels/`, listed in the collection in order.
7. **Levels** tab → tweak any individual level; changes save on Save or auto-save on domain reload.
8. Press Play — Level Select still shows 50 buttons because it's driven by the same count; `PlayerPrefs["LevelToPlay"]` picks which one loads.

## Presets shipped

- **PyramidShrink** — classic. `BaseWidth × BaseHeight`, each layer shrinks by `ShrinkPerLayer` on both dimensions, up to `LayerCount` layers.
- **TowerFlat** — every layer is the same size (useful for a deliberately stubborn deep-stack level).
- **DoublePyramid** — one shared base row, two independent pyramids rising off either side.
- **Custom** — the preset's `CustomLayers` list is used verbatim, for exact hand-authored shapes.

## Solvability & complete-grid guardrails

`LevelDefinitionBuilder` enforces two invariants at build time so the game never renders a broken board:

1. Every tile type's count is an exact multiple of `MatchCount` (no leftover single tile can strand a level).
2. Type count × (MatchCount − 1) ≤ TraySize — worst-case simultaneous tray occupancy never exceeds capacity.

**Auto-adjust for divisibility (never trim mid-row):** if the raw layer set produces a total tile count that isn't a multiple of `MatchCount`, the builder:

1. Drops the topmost layer entirely and retries — repeats until the total divides or only the base is left.
2. If still not divisible with only one layer, shrinks that layer's height (then width) by whole rows/columns.

Every remaining layer stays a **complete rectangle**. Reads as "the pyramid is a bit shorter than you asked for" rather than "there's a hole in the base."

**Grow-not-shrink at generation time:** the Batch Generator runs `PyramidSizeSolver` on the computed dimensions before writing the SO — it picks a nearby pyramid whose total is already divisible, preferring to grow (25 → 27) rather than shrink (25 → 24). By the time the SO hits disk, the builder's auto-adjust is a no-op.

**Inspector visibility:** the SO custom inspector always shows the *effective* layers (post-auto-adjust) in the preview, plus an info card explaining what changed. A one-click **Auto-fix authored layers** button bakes the effective set back into the asset so what you see in the inspector matches what's saved.

## No runtime level generation

The runtime path is SO-only. `Presentation.GameFlowController.InitializeGame` loads `Assets/Resources/Levels/LevelCollection.asset` and logs a hard error if it's missing — there is no procedural fallback. Everything is configured in the Inspector via the Master Level Designer window.

## Dynamic level count

`Presentation.UI.LevelSelectUI` reads the same `LevelCollectionSO` and creates exactly one button per level in the collection. The old 50-level constant is gone — the Level Select screen scales with what's authored.

## Related docs

`ARCHITECTURE.md` §7 (LevelDefinitionSO), §8 (image-based generation — still upcoming, will slot in as a second `IBoardLayoutSource`), §17 (Editor tooling philosophy). `SYSTEMS.md` (registry). `CHANGELOG.md` (dated entries).
