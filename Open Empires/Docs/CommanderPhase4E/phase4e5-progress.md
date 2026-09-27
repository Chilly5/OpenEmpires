# Phase 4E.5 Progress

## Scope

Bounded concurrency for `EnsureUnitCount(Spearman=10)`: population capacity, producer construction, and unit-resource preparation may overlap when their game-side prerequisites are independently legal.

## Implementation

- `CommanderPlanner.PlanUnits` now starts a House when the projected target exceeds the current population cap, but does not wait for an existing House foundation to finish before continuing to producer/resource evaluation.
- A compatible Barracks foundation can coexist with a food `GatherCommand` for the next Spearman when food is below the real training cost.
- All worker selection, reachability, reservations, manual-command protection, duplicate detection, and command cadence remain in the existing deterministic systems.

## Evidence

Pending Unity run after the editor restart. The focused test asserts one active House foundation, one active Barracks foundation, a food-gathering villager before Barracks completion, no duplicate Barracks, and at most one Commander command per planning tick.

## Caveat

This is intentionally a first-preparation slice: it starts the next missing unit resource and relies on later planning cadence to re-evaluate the real resource totals. It does not add a separate tactical future-resource reservation ledger.
