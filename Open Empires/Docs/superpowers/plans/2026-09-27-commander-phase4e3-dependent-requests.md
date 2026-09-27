# Phase 4E.3 — bounded compound and dependent Commander requests

## Goal

Make the approved compound request path work end to end: a semantic request can build a new Barracks at a deterministic location and then train ten Spearmen from that exact new Barracks. Preserve the authority boundary: the provider emits only bounded typed semantics; Unity resolves entities, placement, workers, resources, prerequisites, and commands.

## Architecture

- Parse a bounded `CommanderRequestGraph` from semantic JSON. Nodes are positional, typed operations; dependency and producer references are bounded node indices, never entity IDs.
- Validate the complete graph before mutating `CommanderGoalManager`: supported node kinds only, bounded node/depth/reference/count limits, valid acyclic dependencies, and producer compatibility.
- Submit graph goals in deterministic topological order. A `BuildStructure` result is a game-side identity created by the ordinary command path. A dependent `EnsureUnitCount` goal waits for and revalidates that result; it never falls back to an unrelated producer.
- Keep the existing single-node tactical path and strategic approval path unchanged. Multi-node strategic/unsupported graphs are rejected safely rather than bypassing approval.
- “Then” gates only the dependent producer edge. Unrelated preparation remains eligible for later bounded scheduling work; this slice must not claim arbitrary workflow execution or broad concurrent projection.

## Tech stack

Unity 6.5 / C#, Newtonsoft JSON, existing Commander semantic parser, goal manager, planner, normal `PlaceBuildingCommand` and `TrainUnitCommand` paths, NUnit EditMode and PlayMode tests through Unity MCP.

## Global constraints

- No provider-supplied concrete IDs, coordinates, workers, tiles, commands, callbacks, loops, or recursion.
- Existing strategic approval/authority and stale-request/provider-reset protections remain intact.
- Invalid graphs create zero goals and do not consume a goal slot.
- A producer reference is required to remain owner/type/compatibility/survival safe; destroyed or changed results block/replan rather than silently switching producers.
- Preserve prior Phase 4E.2 spatial behavior and the unrelated scratch file `Docs/CommanderPhase4E/phase4e1-task1-review-package.diff`.

## Task 1 — RED graph contract tests

- [x] Add parser tests for a valid two-node Barracks→Spearman graph, omitted optional dependency normalization, bounded node/reference/count limits, duplicate/unknown fields, invalid node-index references, cycles, incompatible producer types, concrete entity-ID attempts, and unsupported strategic compound nodes.
- [x] Add admission tests proving invalid graphs leave the manager unchanged and valid graphs produce typed link metadata without selecting game entities.

## Task 2 — bounded graph model and parser

- [x] Extend `CommanderSemanticNode`/result or introduce a small `CommanderRequestGraph` model with immutable node index, `dependsOn`, and `producerFromNode` fields.
- [x] Parse only the documented bounded schema with strict integer/range validation. Reject cycles and impossible reference shapes before admission.
- [x] Keep parser errors typed and provider-safe; do not expose implementation IDs.

## Task 3 — game-side admission and goal linkage

- [x] Add a graph admission service that validates every node and edge against the current simulation and existing semantic admission rules before submission.
- [x] Add a bounded producer-link contract to `EnsureUnitCountGoal` and a create-new result contract to the referenced build goal. Submit in deterministic topological order with no partial graph on preflight failure.
- [x] Reuse `OnBuildingPlacedFromCommand` provenance and `PlacedBuildingId`; bind only the exact ordinary command result and retain bounded linkage through the dependent goal lifetime.

## Task 4 — planner enforcement

- [x] Gate dependent unit planning/training on the bound new producer being owned, alive, completed, compatible, and still valid.
- [x] Remove fallback to an unrelated producer for linked goals; emit the existing typed blocker/retry state when the dependency is unavailable.
- [x] Preserve existing producer selection for unlinked single-node goals.

## Task 5 — host/UI routing

- [x] Route valid multi-node tactical graphs through the normal admission/dispatch path.
- [x] Preserve generation, owner, provider reset, timeout, and strategic approval checks. Reject multi-node provider output that would bypass strategic approval.
- [x] Add deterministic offline host tests; defer live provider quota/use until the later provider-evidence phase.

## Task 6 — verification and evidence

- [x] Run focused EditMode RED then GREEN suites, existing Phase 4E.1/4E.2 regressions, and focused PlayMode end-to-end proof in a fresh/idle Unity Editor.
- [x] Verify exact new Barracks binding, no unrelated producer fallback, ordinary command provenance, and typed blocker behavior after producer destruction/invalidity.
- [x] Record job IDs/counts/hashes and scope caveats in `Docs/CommanderPhase4E/`.
- [ ] Run `git diff --check`, inspect the intended diff, and commit only Phase 4E.3 files/docs.

## Review focus

1. Can any provider JSON choose an entity ID, tile, worker, command, or arbitrary execution?
2. Can an invalid graph partially mutate goals?
3. Can a linked training goal silently switch to an unrelated producer?
4. Are existing single-node and strategic approval paths behaviorally unchanged?
5. Does evidence prove the real Unity command path, not just parser/model behavior?
