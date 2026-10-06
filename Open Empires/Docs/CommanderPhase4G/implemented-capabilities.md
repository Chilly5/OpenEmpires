# Phase 4G implemented capabilities

## Targeted economy/clarification repair, 2026-10-06

The earlier coarse SetResourceAllocation entry did not provide generic direct worker orders: Idle/current-resource/source-kind criteria and typed clarification were missing. The targeted repair now adds AllocateWorkers with SelectedCount (ordinary exactly-N assignment), Additional, explicit TargetTotal and one-time AllMatching modes. Unity selects/reserves actual owned eligible workers and legal visible sources, then uses ordinary Gather/Slaughter commands. Explicit source fidelity required the approved conditional native restriction after actual Sheep-to-Berries depletion was reproduced. Restricted binary version1 packets require updated peers; unrestricted legacy bytes remain unchanged.

Evidence: `../CommanderFix/` includes 15/15 focused EditMode, 204/204 relevant existing Commander tests, three controlled PlayMode scenarios and five live Luna requests including local count continuation. Single final full EditMode is 998/998; full PlayMode closeout is tracked in that folder. This is not a new phase, standalone build or hostile-audit acceptance. Historical Phase4G rows below retain their original verification scope.

Status: implementation complete for the bounded Phase 4G scope; exhaustive regression and hostile verification belong to AntiGravity.

## Narrow refresh, 2026-10-06

This refresh changes only production-result binding and its existing adapters. It adds explicit simulation/manager identity checks, immutable unit-result copies, civilization-resolved type matching, and result-specific human-override retention. A dependent rally action no longer needs unrelated military units and infers its structure type from the exact bound structure when the semantic request omits the type. Scout production is explicitly enabled through the existing canonical production path; catalog discovery alone still grants no authority.

The current verification boundary is nine selected EditMode cases and one controlled PlayMode scenario, not every capability listed below. Older evidence in this file/report is historical; use `narrow-verification-2026-10-06.json` for this source refresh.

| Capability | Semantic support | Game-side resolution/execution | Result-binding status | Evidence |
|---|---|---|---|---|
| Move / scout / defend / retreat | Bounded unit and location selectors | Owned living units -> `MoveCommand`; normal `CommandBuffer` and simulation validation | Can consume exact produced units | 9 focused EditMode tests; runtime path shares the PlayMode result test |
| Patrol | `PatrolArea` with base, worked/visible resource, or relative anchor | Exact selector resolution -> `PatrolCommand`; persistent observation remains `Executing` | Exact produced unit IDs only with `resultFromNode`; no fallback | 9 focused EditMode tests; PlayMode produce-three-then-patrol passed |
| Production | `EnsureUnitCount`, optional producer dependency | Canonical civ unit type, age, cost, queue and producer rules; normal `TrainUnitCommand` | Captures IDs created after the goal baseline, sorted deterministically | PlayMode scenario captured exact IDs 6,7,8 |
| Structure result | `BuildStructure` source may feed `SetRallyPoint` | Completed owned building is captured by ID after baseline | Exact building ID; destroyed/missing/wrong-type result blocks | Focused code path; AntiGravity must exercise construction runtime |
| Rally | `SetRallyPoint` with optional structure selector | Normal `SetRallyPointCommand` on an owned completed building | `resultFromNode` binds the exact completed structure; absent reference uses deterministic selector | Focused parser/admission path |
| Attack | `AttackTarget` with visible enemy selector | Visible non-allied unit/building -> normal attack command | Unit result binding is type-checked; target remains visible-state resolved | Ally rejection test passed |
| Repair | `RepairTarget` with villager selector | Damaged owned building -> normal `RepairBuildingCommand` | Produced villager result can be bound; military repair is rejected | Validator rejection test passed |
| Research | `ResearchTechnology` | Canonical technology spec, age/cost/building checks -> normal `ResearchCommand` | No result consumer is admitted for research in this slice | AntiGravity runtime check remains |
| Contextual Mill placement | Existing `BuildStructure` + `WorkedResource` placement | Existing deterministic footprint, terrain, occupancy, reachability and authority rules | Structure result can be consumed by rally only | Parser/admission evidence; AntiGravity gameplay check remains |
| Human override | Applies to workers and military subjects | Human command removes Commander reservation and protects the subject | Result-bound command fails closed if required result units are human-controlled | Human override focused test passed |
| Dependency graph | Bounded node indices, topological order, producer links | Goal manager gates every dependency and fails dependents on failed/cancelled prerequisites | `resultFromNode` is typed, in-range, dependency-linked and runtime-local | Invalid dependency test passed |

The provider never supplies entity IDs, worker IDs, tiles, coordinates, commands, or result handles. Result handles are created only after game-side goal submission and are valid only inside that goal manager and simulation lifetime.
