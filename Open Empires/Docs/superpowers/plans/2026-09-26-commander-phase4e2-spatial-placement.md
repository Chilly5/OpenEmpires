# Phase 4E.2 — deterministic references and spatial placement

Status: implementation plan, not completion evidence. Depends on a verified 4E.1 gate and a responsive Unity test runner. The approved design in `../specs/2026-09-26-commander-phase4e-design.md` governs semantics.

## Authority and conventions

- The provider supplies only semantic selectors and relations; it never supplies an ID, tile, coordinate, worker or command.
- `left` is map-west (negative tile X). `MyTownCenter` is the oldest surviving owned Town Center by ascending `BuildingData.Id`; a requested ordinal uses the same ordering. This semantic selector is separate from the existing `CommanderPlanner.FindPrimaryTownCenter`, which gives `IsMainTownCenter` precedence and is not an exact implementation of the approved selector.
- Gap `G` is the number of clear intervening tile columns between footprints. If the anchor minimum X is `A`, and the new building width is `W`, the exact west origin is `A-G-W`. Z aligns footprint centerlines by `anchorMinZ + floor((anchorHeight-newHeight)/2)`.
- Check the exact origin first. Fallback is bounded to `|dx|<=2`, `|dz|<=2`, clear-gap error <=1, never east of the anchor. Rank `(gap error, |dx|+|dz|, x, z)` with stable integer tie-breaks. Exhaustion returns a placement blocker, never generic relocation.

## Live reuse points

- `CommanderPlanner.cs:258` is the ordinary build/worker/command flow; preserve its `PlaceBuildingCommand` and resource/age checks.
- `CommanderPlanner.cs:664` generic perimeter search is not suitable for directional placement. Its `GetFootprint` (`:774`), `IsVisibleBuildableArea` (`:693`), `HasReachableAdjacentTile` (`:710`) and `IsKnownPath` (`:736`) are reusable validation precedents. The new resolver should share/extract the game-side validation rather than weaken it.
- `MapData.IsBuildable` and `GameSimulation.ProcessPlaceBuildingCommand` remain authoritative for terrain, footprint+border occupancy, costs and owned living builder. The command processor does **not** replace the planner's fog and complete-known-path validation.

## Tasks and evidence

1. Add bounded typed selector/relation fields to the semantic contract and strict parser. Write RED tests for allowed enum tokens, ordinal bounds, clear-gap integer bounds, duplicate/extra fields and model-supplied coordinates/IDs; then implement and run focused GREEN. Preserve Task 1 tactical and strategic forms.
2. Implement a game-side selector for owned/legitimately known entities. RED/GREEN tests must cover multiple owned TCs (oldest-by-ID), destroyed/foreign TCs, ordinal selection, fog-safe resource references, and stable tie behavior. Never expose selected concrete IDs to the provider.
3. Implement pure deterministic candidate generation for west/east/near with integer footprint math and documented ordering. RED/GREEN table tests should pin exact 5-clear-tile origin, unequal/odd footprints, edge bounds, half-tile Z tie, fallback tolerance, and rejection of opposite/unrelated locations.
4. Integrate candidate validation into the ordinary construction planner path. RED/GREEN tests must cover each footprint+border tile's bounds/terrain/occupancy/current visibility, full builder path through visible/explored known tiles, no partial path, construction age/cost/ownership checks, and typed blocker on exhaustion. Do not turn a failed semantic placement into generic `TryFindBuildableTile` fallback.
5. Add real PlayMode proof: the normal command creates an owned Barracks at the validated tile west of the resolved TC with five clear columns (or explicitly reported bounded tolerance), uses a legitimate worker, and never creates a building when all bounded candidates fail. Record exact job IDs/counts, result XML/JSON, console status and source scope.

The Barracks-to-producer binding and dependent ten-Spearman request belong to 4E.3/4E.5 and must not be claimed from a placement-only pass.
