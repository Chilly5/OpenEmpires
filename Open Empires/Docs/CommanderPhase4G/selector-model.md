# Phase 4G selector model

Selectors are descriptions, not references. The executor resolves them against the current simulation on every planning attempt.

| Selector | Resolution | Visibility / tie break | Failure |
|---|---|---|---|
| Military / type / scout / villagers | Owned living units, sorted by stable entity ID | Ownership and health are mandatory | Blocked if fewer than requested |
| DamagedMilitary | Owned living non-villager military units below max health | Stable ID order | Blocked if none |
| PlayerBase | First surviving owned Town Center by ID | Own state only | Blocked if unavailable |
| WorkedResource | Visible non-depleted node currently targeted by an owned villager | Actual `TargetResourceNodeId`, then node ID | Blocked if the relation disappears |
| VisibleResource | Visible non-depleted node of requested type | Nearest to own Town Center, then node ID | Blocked if unknown/depleted |
| VisibleEnemy | Visible living enemy unit, then visible enemy building | Nearest to own Town Center, then entity ID | Blocked; hidden state is never searched |

Placement `WorkedResource` is separate from action locations but follows the same relation: the game-side resolver finds the real visible node and the planner searches bounded nearby legal tiles. Human worker protection and ordinary simulation placement validation remain in force.

## Exact producer results

An action may add `resultFromNode` to consume a prior producer node. This is not a selector shortcut:

1. Graph admission checks the source node index, dependency edge, topological order and compatible result kind.
2. Goal submission snapshots baseline IDs in the current simulation.
3. `EnsureUnitCount` publishes only newly spawned owned living units of its resolved civilization type.
4. `BuildStructure` publishes only newly completed owned buildings of its effective type.
5. The dependent executor requires the exact bound count/ID or exact building ID and rechecks ownership, liveness, completion and human protection.

If the result is missing, stale, destroyed, manually controlled, or from another manager/runtime, the action is blocked. The resolver never silently replaces a bound Spearman with another military unit or a bound Barracks with another production building. Ordinary selector fallback is available only when the semantic action omits `resultFromNode`.
