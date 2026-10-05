# Phase 4G implemented capabilities

Status: implementation complete for the bounded Phase 4G scope; exhaustive regression and hostile verification belong to AntiGravity.

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
