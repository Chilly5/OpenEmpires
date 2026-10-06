# AntiGravity Phase 4G test plan

Codex ran only the bounded focused evidence listed in the implementation report. AntiGravity owns the independent audit and must not treat this document as already-passed evidence.

## Current entry point — 2026-10-06

Audit the current working tree, not only Git HEAD or the older accepted executable. Current narrow evidence is nine selected EditMode cases (`d1766219a9b34da3b5ab637ec73bc976`) and one controlled PlayMode case (`974e325de24b421f8d35af8f393887eb`). These do not supersede full independent acceptance. Read `known-limitations.md` and `source-consistency.md` before running anything.

Do not undo the existing Phase 4H or provider/UI work merely to reconstruct an older snapshot. Keep audit findings attributable: record both Phase 4G defects and unrelated integration defects, then make only authorized small final fixes. No secrets, `.env` contents or authorization headers belong in evidence.

## Priority acceptance contracts

1. **Exact new Spearmen:** start with at least one old Spearman; issue `patrol my goldmine with 3 new spearmen you build from a barracks`. The semantic producer total must equal observed owned total + 3, and the patrol count must be 3 with `dependsOn` and `resultFromNode` referencing that producer. Require three real spawns via normal TrainUnitCommand, source completion, and exact new IDs in PatrolCommand. Old Spearmen are forbidden even if nearest/idle. Repeat with no Barracks and ordinary resource shortages to exercise legitimate prerequisite construction/economy.
2. **Exact structure:** start with an existing Barracks and construct a second. Dependent rally must target the new completed building. Run with zero living military to catch irrelevant unit-selector coupling. Test absent and explicit matching structure types, mismatch, destruction, construction stall and incomplete foundation. No lowest-ID/nearest-building replacement.
3. **Exact Scout:** start with one Scout, ensure total 2, then ScoutArea count 1 with `resultFromNode`. Build Stables through the existing producer dependency if needed. The new Scout, never the old one or another military unit, must be the subject of the normal MoveCommand. Check canonical age/cost/queue behavior and current-visible location semantics; map-west is not camera-left.
4. **Human control:** move one bound unit before dependent issuance, after issuance, and before capture. Wait past 900 ticks, then past blocked retries. The invalidated action must not reissue/reclaim or substitute. Check all-unit and partial takeovers; already-issued orders on untouched units need not be undone. A new explicit request is the only intended reauthorization path.
5. **Runtime/lifecycle:** create identical numeric IDs in another simulation, another manager on the same simulation, and a reset runtime. Reusing a handle must reject. Exercise default/empty handles, copied arrays mutated by the caller, missing source pointer, source cancellation/failure, disposed manager, goal archive eviction and stale creation anchors. A semantic plan may be newly admitted in a fresh runtime; it must create fresh local results, never reuse old results.

## Highest-risk attribution cases — mandatory

Current capture uses baseline exclusion, not training-queue provenance. Treat the following as acceptance gates, not presumed passes:

- Pre-existing same-type queued units finish after graph submission.
- Player queues the same type during the producer goal, in the same and a different building.
- Two result-consuming producer graphs overlap for the same type; no ID may be incorrectly attributed to both.
- A different goal completes a same-type building while the referenced source is still constructing.
- Auto-production, producer destruction, queue cancellation/reordering, rejected train commands, dead results and population-cap changes occur between issued orders and capture.
- Units die while producing; count satisfaction and the required exact new set must both be observed correctly. A zero-new result cannot borrow old units.

If any case consumes unrelated post-baseline results, report a production-attribution defect and implement a small fail-closed fix or explicit causal tracking before accepting. Do not waive it because the controlled three-Spearman case passes.

## Evidence procedure for each gameplay case

Record seed, civilization, age, existing/queued entity sets, resources, map/fog state and source hashes. Keep setup mutations clearly separated from commander execution. Capture player text, bounded provider JSON (redacted), admitted graph, source/consumer goal IDs, creation/capture ticks, statuses, every emitted command and final subject IDs. Require normal simulation mutation after CommandBuffer, not direct test mutation as proof of completion.

Run controlled semantic-fixture cases first, then equivalent real-provider typed-chat requests in Unity and the fresh standalone. Provider success, graph admission, command enqueue, simulation application and observed gameplay are separate gates. A source-level check or parser green result cannot stand in for a gameplay gate.

## Suggested independent run order

1. Import/reload and verify current compilation/manifest; enumerate the entire current suite rather than copying old test totals.
2. Rerun the nine selected cases and single exact-result scenario, then the Phase 4G hostile cases, provider tests and nearby authority/graph/spatial tests.
3. Complete the priority runtime/lifecycle/attribution scenarios above and the broader capability matrix below.
4. Run full EditMode and full PlayMode separately; retain complete XML and exact job IDs. Re-run only affected suites after fixes, then regenerate final hashes and full final evidence as your independent acceptance process requires.
5. Build/launch a current standalone, inspect fresh Player.log, and perform normal player-command and minimal multiplayer/determinism smoke. Record any unavailable gate as not run, never passed.
6. Publish an independent verdict plus actionable remaining issues. Stop after that report; no automatic feature-phase expansion.

## Audit gates

1. Refresh/import the current working tree in a healthy Unity editor. Record Unity version, MCP/package state, compiler output, current branch, and the SHA-256 source manifest.
2. Confirm the Phase 4G source inventory is current and that the detached capability layer did not alter canonical gameplay data.
3. Run the full EditMode and PlayMode suites once, separately, after the focused tests. Record job IDs, totals, failures and duration; do not merge totals from stale jobs.
4. Produce a current Windows standalone build from the same source snapshot. Launch it once, exercise a commander request, and inspect `Player.log` for exceptions, assertion failures, serialization errors, or command-loop stalls.
5. Keep Phase 4H frozen. This audit is for Phase 4G only.

## Focused scenario matrix

| Request / setup | Expected semantic action | Resolver proof | Gameplay result | Forbidden behavior |
|---|---|---|---|---|
| `patrol my goldmine with 3 new spearmen you build from a barracks` | Ensure three Spearmen, then `PatrolArea`, `resultFromNode` to producer | Exact post-baseline unit IDs; worked Gold is an owned-villager relation | Patrol command contains exactly the three new IDs | Pre-existing Spearman, sheep, scout or arbitrary military substitution |
| Build a Mill near berries currently worked by a villager | `BuildStructure` with WorkedResource placement | Real node, footprint, legal tile, map bounds, reachability | Normal placement/construct command and completion | Camera-relative coordinate, hidden/depleted berry, unrelated tile |
| Build a Barracks, then set its rally point | Build result -> `SetRallyPoint` with `resultFromNode` | Exact new completed building ID | Rally command targets that building | Lowest-ID/nearest existing Barracks fallback |
| Produce one Scout, then scout the visible western ridge | Ensure Scout -> `ScoutArea` with exact result | Result type Scout, owned/alive/available | Move command contains the produced Scout | Existing Scout substitution or hidden destination |
| Request fifteen Spearmen and rally to worked Gold | Ensure count plus rally action | Canonical producer/cost/queue and deterministic Gold node | Normal train and rally commands | Provider-selected producer ID or coordinates |
| Move military to base | `MoveUnits` / `DefendArea` | Owned living units and Town Center by stable ID | Normal MoveCommand; completion observed in simulation | Human-controlled subject reclaimed |
| Attack visible enemy unit/building | `AttackTarget` | Visible, non-allied target only | Normal attack command | Allied, hidden or stale target |
| Repair damaged owned building | `RepairTarget` | Villager selector and damaged owned building | Normal RepairBuildingCommand | Military repair or enemy/complete target |
| Research a canonical technology | `ResearchTechnology` | Effective age/cost/research building | Normal ResearchCommand | Provider-supplied tech cost or building ID |

For every row record request text, provider JSON, admitted graph, goal IDs/status transitions, emitted command type, subject/target IDs observed only in game-side logs, and final simulation state.

## Result-reference and lifecycle attacks

- `resultFromNode` out of range, negative, self-referential, missing from `dependsOn`, duplicate dependency, cycle, depth overflow and cross-node-type references: reject atomically with no goal mutation.
- Unit source to incompatible selector (Spearman -> Archer, military -> Villager, Scout -> damaged military): reject before command creation.
- Structure source to non-rally action or mismatched structure type: reject before command creation.
- Complete a producer, destroy one result, transfer ownership if supported, reset/recreate the simulation, or dispose the goal manager before the dependent runs: block/fail closed; never select a replacement.
- Manually issue a command to one or all result-bound units between producer completion and dependent planning: Commander must not reclaim them; partial exact sets must block rather than shrink or substitute.
- Inject a stale provider response, reuse a semantic plan in another manager, or reorder nodes: runtime-local typed bindings must reject or remain isolated.
- Verify producer baselines exclude every pre-existing unit/building, including civilization substitutions and under-construction foundations.

## Authority, ownership and information boundaries

- Provider attempts to emit concrete entity IDs, worker IDs, coordinates, tiles, commands, secret state, unknown enum values, duplicate JSON fields, oversized counts or strategic objectives must be rejected atomically.
- Ownership, alive/health, civilization substitution, age, costs, prerequisites, queue limits, terrain, occupancy, pathing, fog-of-war and ally checks remain game-side.
- `VisibleEnemy` must never search unexplored or merely historical objects. Allied visible units/buildings are not enemies.
- Human commands must remove Commander reservations for every subject type, including military units; a later retry may wait, block or report the override.
- Strategic requests and approval/authority paths remain separate from tactical capability nodes.

## Q&A and routing

- Ask factual questions such as “what counters spearmen?” and “why is the plan waiting?”; confirm no command or goal mutation occurs and responses use bounded catalog/context data.
- Submit tactical actions with equivalent wording; confirm they take the tactical path and do not enter strategic approval accidentally.
- Mix Q&A and action text, malformed JSON, provider timeout, late response, cancellation, duplicate submission and follow-up; confirm safe state transitions and no leaked goal/reference.

## Robustness and determinism

- Repeat identical semantic graphs from identical snapshots and compare admitted node order, selector tie-breaks, command subjects and result IDs.
- Run long sessions with repeated bounded requests, target invalidation, construction stalls, production queues, cancellation and archived goals; watch for reference leaks or active-goal growth.
- Exercise large maps/content, JSON escaping, fog transitions, population caps, civilization substitutions and resource depletion.
- Run a minimal multiplayer/desync smoke for each new command mapping where the project harness supports it.

## Evidence package

Attach compiler output, focused/full test job IDs, standalone build hash, Player.log excerpt/hash, scenario transcripts, command/goal timelines, source manifest and a signed verdict. If any gate fails, report `REQUIRES IMPLEMENTATION FIX` and leave Phase 4H untouched.
