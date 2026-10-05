# Phase 4G specification and closeout boundary

## Objective

Expose a bounded set of semantic tactical capabilities while preserving the existing Commander authority path and the simulation as the only source of concrete game actions.

## Provider boundary

The provider may describe action type, allow-listed selectors, counts, resources, technology names, structure names, dependency node indices and typed result references. It may not select entity IDs, workers, tiles, coordinates, commands, hidden state or strategic authority.

## Supported action families

Movement/scouting/patrol/defend/retreat, attack, rally, repair, research, existing resource allocation, and existing contextual construction are supported through ordinary game commands. Capability discovery remains separate from execution support.

## Result binding contract

`resultFromNode` is a graph-local semantic index. Admission requires a valid dependency edge, acyclic/topological ordering and a compatible source/result kind. A completed `EnsureUnitCount` publishes only newly-created owned living units after its baseline. A completed `BuildStructure` publishes only newly-completed owned buildings after its baseline. The dependent receives an internal typed runtime binding and revalidates exact ownership, liveness, type, count, completion and human-control state. Missing, stale, cross-manager, destroyed or manually controlled results block; no unrelated fallback is allowed.

## Lifecycle and authority

Goals are created atomically, dependency-gated, planned deterministically, and executed through the normal `CommandBuffer` with Commander source attribution. Human commands remove reservations and protect all subject types. Long-running actions are observed from simulation state; patrol remains persistent and executing while active.

## Verification boundary

This closeout includes 9 focused EditMode tests, one focused PlayMode production-result scenario, zero compiler errors, updated documentation and a refreshed manifest. AntiGravity must own full regression, standalone launch/Player.log, hostile provider cases, broader gameplay scenarios and final independent sign-off. Phase 4H must not begin from this document.
