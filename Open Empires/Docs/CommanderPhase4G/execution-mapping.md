# Phase 4G execution mapping

```text
semantic action
  -> CapabilityActionIntent
  -> CommanderCapabilityGoal
  -> CommanderCapabilityExecutor (owned/visible selectors)
  -> optional typed result binding (producer goal only)
  -> existing ICommand
  -> CommandBuffer(CommandEnqueueSource.Commander)
  -> GameSimulation validation and mutation
```

Mappings currently implemented:

| Semantic action | Existing command |
|---|---|
| MoveUnits / ScoutArea / DefendArea / RetreatUnits | `MoveCommand` |
| PatrolArea | `PatrolCommand` |
| SetRallyPoint | `SetRallyPointCommand` |
| AttackTarget | `AttackUnitCommand` or `AttackBuildingCommand` |
| RepairTarget | `RepairBuildingCommand` |
| ResearchTechnology | `ResearchCommand` |

## Result-reference lifecycle

`resultFromNode` is a semantic graph index, never an entity ID. Admission requires the source index to be in range, dependency-linked, topologically prior, and type-compatible:

- `EnsureUnitCount` -> unit actions only when the selector can represent that exact unit type.
- `BuildStructure` -> `SetRallyPoint` only, with an optional matching structure type.
- Other source goal types are rejected.

At submission, the goal manager records a baseline of owned living units or owned buildings. When the producer completes, it captures only newly-created, still-valid results in stable ID order. The dependent capability receives an internal typed `CommanderResultBinding`. The executor validates owner, health/liveness, type, exact count, and building completion again. Missing, stale, destroyed, cross-manager, or human-controlled results fail closed; no unrelated selector fallback is used for a result-bound action.

Without `resultFromNode`, normal semantic selectors retain their deterministic current-state behavior. This is the only path that permits fallback to an existing selector result.

The executor does not bypass `GameSimulation`. A command can still be rejected by ownership, visibility, reachability, age, resource, queue, or authority checks. Strategic objectives continue through the existing approval path.
