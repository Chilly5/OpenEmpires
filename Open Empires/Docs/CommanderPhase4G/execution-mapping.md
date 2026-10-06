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

## Current binding checks

- Internal handles carry the actual `GameSimulation` reference, source goal ID/creation tick, and (in the production path) owning `CommanderGoalManager`. They are not serializable provider handles.
- A producer must be a completed dependency from the same owning manager/simulation. Missing mandatory bindings cannot fall through to ordinary selection. Disposed owners, evicted/missing source goals, changed creation anchors, future anchors, and cross-runtime handles reject command creation.
- Unit IDs are copied to a read-only collection. Exact count, unique IDs, current ownership, health and civilization-resolved type are rechecked. Empty, destroyed or incompatible results do not shrink to a partial selection.
- Manual commands invalidate the affected result-bound dependent action for its lifetime, including after the ordinary 900-tick protection window expires. A new explicit player request is required to authorize reuse. Other goals keep their existing worker-authority policy.
- A building binding bypasses unit selection entirely. With no explicit structure token, its effective game-side type is used; a supplied incompatible type still rejects. An absent/destroyed bound building never triggers an existing-building search.
- Scout uses the existing unit type 4 and canonical Stables training specification. No costs, prerequisites, footprints or civilization data are duplicated or migrated.

`EnsureUnitCount.count` remains a desired total. With one existing Spearman, producing three new Spearmen requires total 4, followed by a dependent selector count 3. If a stale observation makes the captured new count incompatible, execution blocks rather than borrowing existing units.

The executor does not bypass `GameSimulation`. A command can still be rejected by ownership, visibility, reachability, age, resource, queue, or authority checks. Strategic objectives continue through the existing approval path.
