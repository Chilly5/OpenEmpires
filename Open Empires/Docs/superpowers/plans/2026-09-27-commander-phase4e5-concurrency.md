# Commander Phase 4E.5 Concurrency Plan

## Goal

Make an implicit `EnsureUnitCount` request discover independent capacity, producer, and unit-resource prerequisites without serializing them behind producer completion.

## Architecture

- Keep Unity/game-side authority for buildings, workers, resources, placement, and commands.
- Re-evaluate the bounded tactical goal on the existing planning cadence.
- Start a House when projected population exceeds the current cap, then continue evaluating producer and unit-resource branches while an eligible foundation is already active.
- While a compatible producer is under construction, prepare the next unit's missing resource through the normal `GatherCommand` path.
- Preserve existing worker reservations, manual-command protection, duplicate-building detection, and one-Commander-command-per-planning-tick behavior.

## Tasks

1. Add the bounded planner overlap path for population, producer, and next-unit resource preparation.
2. Add focused PlayMode evidence that House, Barracks, and food gathering overlap before Barracks completion.
3. Run Phase 4E.1-4E.4 regressions and the focused Phase 4E.5 test.
4. Record evidence and commit only the Phase 4E.5 change.

## Constraints

This slice does not introduce a second resource ledger or promise an entire ten-unit stockpile in one command. It starts the next missing resource branch and lets later planning cadence re-evaluate real simulation state.
