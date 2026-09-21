# Phase 6 - Graphics Optimizer

## Goal

Provide useful, hardware-aware graphics recommendations while making automatic file changes only where the launcher has a deliberately verified config profile.

Default target:

- 1920x1080
- 60 FPS
- Quality preference

The target is configurable.

## Hardware profile

The Windows detector records:

- CPU model
- discrete/integrated GPU name
- physical RAM
- primary display resolution
- primary display refresh rate
- detected graphics-performance tier

GPU classification is deliberately conservative and name-based. Unknown/future hardware is left as Unknown and can be assigned a manual tier in the UI.

## Recommendation policy

The policy combines:

1. effective GPU tier
2. Performance / Balanced / Quality preference
3. resolution and FPS target
4. built-in game-specific guidance when available

For mainstream 1080p hardware the quality-oriented default begins from High, keeps textures high when practical, disables ray tracing by default, and reduces expensive shadows/volumetrics/crowd settings before texture quality.

This is a recommendation policy, not a benchmark result. Phase 6 does not claim a game will hold the selected FPS without measurement.

## Safe automatic apply

Automatic edits require a built-in `GameGraphicsProfile` with:

- a known config location
- a known config format
- explicitly listed writable fields
- no fuzzy game matching

Matching uses Steam App ID first, then exact normalized title/known aliases.

The writer:

- requires the config file to already exist
- modifies only listed keys/elements already present
- never creates missing keys/elements
- validates XML before editing XML configs
- preserves unrelated XML text/formatting by replacing only matching value attributes
- creates `<config>.game-launcher.bak` before the first change
- never overwrites that original backup on later applies
- writes through a temporary file before replacing the active config
- supports one-click restore from the backup

## Current safe-apply profiles

### The Witcher 3

Safe fields:

- Resolution
- FPS limit

Recommendation guidance covers HairWorks, foliage/shadows, and next-gen ray tracing.

### Grand Theft Auto V

Safe fields:

- screen width
- screen height
- refresh rate
- VSync

Recommendation guidance covers MSAA, shadow quality, and Advanced Graphics options.

### Red Dead Redemption 2

Safe fields:

- screen/window width and height
- refresh-rate numerator/denominator
- VSync

Recommendation guidance covers MSAA/TAA, volumetrics, water physics, and tree tessellation.

## Recommendation-only built-ins

Initial recommendation-only profiles include:

- Control: Ultimate Edition
- God of War
- Ghost of Tsushima DIRECTOR'S CUT
- Assassin's Creed Shadows
- Black Myth: Wukong

These profiles add game-specific guidance but do not allow automatic config changes.

## Launch integration

When **Automatically apply verified safe fields before launch** is enabled:

1. the launcher checks whether the selected game has a verified safe-apply profile;
2. unsupported/recommendation-only games are skipped silently;
3. supported games receive the configured safe changes;
4. any optimizer failure is converted to a launcher warning;
5. the game launch proceeds regardless.

## Intentional limits

- Phase 6 does not benchmark games automatically.
- Phase 6 does not scrape arbitrary internet recommendations at launch time.
- Phase 6 does not edit NVIDIA/AMD/Intel global driver profiles.
- Phase 6 does not change undocumented config fields.
- Phase 6 does not infer exact optimal quality levels from GPU name alone.
- Adding a new auto-apply profile requires regression tests proving backup/restore and no creation of missing fields.
