# Phase 4E.4 — deterministic ReachAge desired-state goal

## Goal

Extend the approved bounded semantic Commander path with `ReachAge`, a desired-state request whose success is the real simulation age reaching the requested target. Keep the provider limited to a bounded age token; Unity resolves the next legal landmark, worker, tile, resources, and normal command.

## Architecture

- Parse `ReachAge` with an explicit bounded target token (`Next`, `Feudal`, `Castle`, `Imperial`) and reject invalid/unknown age values.
- Admit `ReachAge` as a tactical intent only after the existing DTO validation boundary; compound graphs remain atomic.
- Register a `ReachAgeGoal` that observes `GameSimulation.GetPlayerAge`, resolves only the next legal age, chooses the lowest stable legal landmark ID, and issues a normal `PlaceBuildingCommand`.
- Use the existing landmark validation and completion path. Never mutate age/resources/buildings directly and never accept provider IDs, tiles, workers, landmark IDs, or commands.
- Missing food/gold use existing bounded gather planning. Missing builder/placement is a typed block. In-progress landmark construction waits and completion is recognized only from the actual age transition.

## Constraints

- French/HRE Age 4 is unsupported through `LandmarkDefinitions.HasChoices`.
- Multi-age requests progress one actual age at a time; no skipped age or imagined prerequisite tree.
- Existing strategic approval, owner/generation, cancellation, reset, worker cleanup, and single-node behavior remain unchanged.
- Preserve unrelated scratch file `Docs/CommanderPhase4E/phase4e1-task1-review-package.diff`.

## Tasks

1. [x] Add RED parser/admission/goal tests for target tokens, invalid values, already-at-target, deterministic landmark choice, resources/builder/placement blockers, and real age completion.
2. [x] Extend semantic node/intent/DTO/graph admission with `ReachAge` while keeping concrete authority fields absent.
3. [x] Add `ReachAgeGoal` and manager submission/graph creation/cancel cleanup.
4. [x] Implement planner next-age resolution, resource gathering, deterministic landmark placement and normal command issuance.
5. [x] Route provider prompt/host responses and add focused Unity EditMode/PlayMode verification.
6. [ ] Record evidence, inspect diff, and commit only the Phase 4E.4 slice.

## Review focus

1. Can provider output choose an age outside the bounded semantic vocabulary or choose a landmark/entity/tile/worker?
2. Does unsupported civ/age produce a typed blocker/failure rather than `GetChoices` fallback?
3. Does the goal complete only after `GetPlayerAge` changes?
4. Do sequential target ages remain sequential and deterministic?
