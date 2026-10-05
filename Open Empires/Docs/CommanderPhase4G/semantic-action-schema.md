# Phase 4G semantic action schema

The provider may emit at most four graph nodes. Existing graph limits remain: 8,192 response characters, eight dependency references and depth four. Node references are indices only.

## Generic actions

`MoveUnits`, `ScoutArea`, `PatrolArea`, `SetRallyPoint`, `AttackTarget`, `DefendArea`, `RetreatUnits`, `RepairTarget`, and `ResearchTechnology` are semantic-only request types. They contain no entity IDs, workers, coordinates, tiles or commands.

Action nodes use:

```json
{"type":"PatrolArea","unitSelector":"Spearman","count":3,
 "location":"WorkedResource","resource":"Gold","dependsOn":[]}
```

An action may use `"resultFromNode": <index>` when it consumes a prior producer result. The index must also appear in `dependsOn`. Unit results may come only from a compatible `EnsureUnitCount` node; structure results may come only from a `BuildStructure` node and may feed `SetRallyPoint`. The reference is validated and converted to a game-side runtime binding; it is never an entity ID or a provider-owned handle.

Allowed unit selectors are `Military`, `Scout`, `Villagers`, `Spearman`, `Archer`, `Knight`, and `DamagedMilitary`. Allowed locations are `PlayerBase`, `WorkedResource`, `VisibleResource`, `VisibleEnemy`, and `RelativeToSelectedUnits`. Resource locations require an allow-listed `Food`, `Wood`, `Gold`, or `Stone` value. Research uses one of the canonical `TechnologyType` names.

The semantic parser and graph admission revalidate all values. `CapabilityActionIntent` is then validated by the normal intent validator and becomes a `CommanderCapabilityGoal`; the planner emits an ordinary existing `ICommand` only after deterministic resolution.
