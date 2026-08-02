# PROJECT_OVERVIEW.md

**Working title:** Triple Tile Match
**Genre:** Casual mobile puzzle — tile-collection / triple-match (Triple Tile / Triple Kingdom Tiles-inspired, original implementation)
**Platforms:** Android & iOS
**Engine:** Unity 6, URP, Portrait orientation
**Last updated:** 2026-08-01

## Vision

Players clear a stacked board of tiles by tapping exposed (unblocked) tiles, which fly into a tray. Collecting the configured number of identical tiles in the tray clears them; filling the tray without matching loses the level. The game should feel relaxing, premium, and highly responsive — every interaction reinforced with layered visual, audio, and haptic feedback.

## What makes this project different from a template clone

- **Fully data-driven levels**, including an original **image-based board generation pipeline**: designers can supply shape/layer mask images and have tile layouts generated automatically, with a fully-supported manual layout fallback.
- **Architecture built for thousands of levels and millions of players from day one** — not retrofitted later. See `ARCHITECTURE.md`.
- **Extensible by construction**: obstacles, power-ups, new board sources, and new match rules are additive (new classes implementing existing interfaces), not edits to shipped systems.

## Current Status

Core Infrastructure (event bus, pooling, service locator, boot sequence) implemented. See `ROADMAP.md` for build order and `CHANGELOG.md` for what's landed.

## Related Docs

`ARCHITECTURE.md` (root technical reference) · `SYSTEMS.md` (living system/event registry) · `ROADMAP.md` · `CHANGELOG.md` · `DECISIONS.md`
