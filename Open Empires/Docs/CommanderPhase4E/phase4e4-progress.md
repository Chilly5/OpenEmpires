# Phase 4E.4 progress — ReachAge desired-state slice

## Implemented

- Added bounded semantic `ReachAge` nodes with only `Next`, `Feudal`, `Castle`, and `Imperial` targets.
- Extended both semantic graph admission and the legacy tactical DTO adapter without adding entity, tile, worker, landmark, or command authority to provider data.
- Added `ReachAgeIntent`, `ReachAgeGoal`, typed age blockers, manager submission/graph registration, cancellation/disposal cleanup, context reporting, and player-facing responses.
- Planner observes the real simulation age, rejects unsupported civilization targets before `GetChoices`, advances one age at a time, chooses the lowest stable legal `LandmarkId`, checks real landmark food/gold costs, selects a reachable owned villager, searches visible/buildable/reachable tiles using the landmark footprint, and emits normal `PlaceBuildingCommand` with game-side `LandmarkIdValue`.
- Goal binds the exact command-created landmark, waits/recoveries through ordinary construction, and completes only after `GameSimulation.GetPlayerAge` changes. Already-satisfied targets complete without a command.
- OpenRouter and Gemini instructions now advertise the bounded `ReachAge` form; Gemini remains intact.

## Unity evidence

- Phase4E4 EditMode: job `870f83b01e934d709e940ace41e6c22c`, 10/10 passed.
- Phase4E4 PlayMode: job `dac25026a4e74ab28d1f4711a27f54e5`, 3/3 passed.
  - normal landmark placement and exact binding;
  - no completion before real age transition, then completion after transition;
  - already-at-target no-command success;
  - French Imperial target typed unsupported failure.
- Phase4E1 host/provider/security: job `097ea0094d86433392c4da814dbb6d62`, 71/71 passed.
- Phase4E2 + Phase4E3 EditMode regressions: job `4d4f19ab47ae4a25b053dafd0c4e874c`, 83/83 passed.
- Phase4E2 PlayMode regression: job `52f73ad503f042cebed6577d3c834009`, 3/3 passed.
- Unity console: no current errors after final refresh; only pre-existing API deprecation warnings remain.
- `git diff --check`: clean (line-ending normalization warnings only).

## Scope caveats

- This slice does not add Phase 4E.5 concurrent prerequisite scheduling or Phase 4E.6 provider live/quota evidence.
- The real simulation currently models age prerequisites as landmark legality, builder reachability, resources, and construction; no extra tech/structure prerequisite was invented.
- Full Castle/Imperial multi-stage runtime was not claimed as a long-running smoke test; planner logic is sequential and the focused runtime proof covers one real transition plus unsupported-target handling.
