# AntiGravity Phase 4G test plan

Codex ran only the bounded focused evidence listed in the implementation report. AntiGravity owns the independent audit and must not treat this document as already-passed evidence.

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
