# Economy semantic contract — targeted post-4G/4H fix

Implementation date: 2026-10-06. Verification status is recorded separately in `focused-test-evidence.md`; this document defines the contract, not a passing claim.

## Authority

The provider describes worker/resource criteria only. Strict semantic parsing, DTO admission and runtime validation precede deterministic selection. Only game-side code selects actual worker IDs, resource-node IDs, sheep IDs and paths. Execution uses existing GatherCommand or SlaughterSheepCommand through CommandBuffer and GameSimulation. Strategic approval and production-prerequisite policy remain separate.

## Completed request

```json
{"outcome":"Request","nodes":[{"type":"AllocateWorkers","mode":"SelectedCount","countMode":"Exact","count":4,"workers":{"state":"Idle"},"destination":{"resource":"Food","sourceKind":"Sheep"}}]}
```

Allowed fields:

| Field | Values / rule |
| --- | --- |
| mode | SelectedCount, Additional, TargetTotal |
| countMode | Exact, AllMatching |
| count | Exact requires integer 1..200, also bounded by the owning simulation's maximum population. AllMatching prohibits count. |
| workers.state | Any, Idle, Gathering |
| workers.currentResource | Optional Food/Wood/Gold/Stone, only with Gathering |
| destination.resource | Food, Wood, Gold, Stone |
| destination.sourceKind | Optional Any (default), Sheep, Berries, Farm, Tree, GoldMine, StoneMine |
| dependsOn | Existing bounded graph dependencies; no resultFromNode or producerFromNode for allocation |

Unknown/duplicate fields, numeric or unknown enum names, wrong JSON types, excessive bounds, incompatible combinations, concrete provider IDs and coordinates reject atomically. TargetTotal requires unconstrained Any workers. AllMatching requires SelectedCount and Idle/Gathering.

## Quantity meanings

| Meaning | Semantics |
| --- | --- |
| put 4 villagers on food | SelectedCount: select/assign exactly four eligible workers for this request, even when six already gather Food. Eligible same-destination workers may be selected; this is not a desired-total no-op. |
| gather food with four idle villagers | SelectedCount + Idle: select four eligible idle workers, without commandeering busy workers to cover a shortage. |
| put 4 more villagers on food | Additional: select four eligible workers not already assigned to the matching destination/source. Freeze selection; retries do not repeatedly add four. |
| make sure / keep 4 villagers on food | TargetTotal: at least four observed living owned assignments. Count existing protected assignments without taking control. Do not remove surplus. This is a bounded request, not a permanent replenishment service. |
| send idle villagers to berries | AllMatching + Idle: one-time snapshot of currently eligible matching workers; no numeric question. Empty or >200 snapshots block rather than silently truncate. |
| move 3 villagers from wood to gold | SelectedCount + Gathering/currentResource Wood: only transfer matching Wood workers. |

An exact shortage produces no initial assignment commands or new reservations. Completion observes the selected workers' current matching assignments; queued commands or unrelated existing workers are not sufficient. Selection stays fixed after issuance, including after death or human takeover. A human command touching a selected worker interrupts that request permanently; the 900-tick protection lease does not authorize reclaiming it.

## Sources and deterministic resolution

Source classification projects existing canonical registrations: Sheep carcasses are Food + IsCarcass; Farm is IsFarmNode with an owned, completed, non-destroyed linked Farm; current Berries registrations are Food minus Farm/carcass; Tree/GoldMine/StoneMine project Wood/Gold/Stone. Living sheep use actual IsSheep, health and ownership. Neutral/allied/enemy sheep are not owned sheep. This is not a manually maintained content/cost database.

Food permits Sheep/Berries/Farm, Wood permits Tree, Gold permits GoldMine, Stone permits StoneMine; Any permits the requested resource's legal source classes. An unspecified Food source can use deterministic legal Food selection. Explicit Sheep never silently becomes Berries.

Worker ranking uses idle, Commander-controlled gathering, then other eligible gathering; distance and stable IDs break ties. Targets require visible, non-depleted legal sources and known complete reachable paths. Whole-request preflight includes source capacity and existing/other-goal assignments; Farm capacity is one worker. Candidate checks are bounded per selected worker by the existing 3..5 candidate setting. If the bounded search cannot satisfy the request, report a blocker rather than an unrelated placement/target.

## Conditional native change: required by reproduced runtime loss

The initial implementation used unchanged ordinary commands. The saved `task2-runtime-gate-sheep-to-berries-red.xml` proves that after the selected carcass depleted, native automatic coarse-Food retargeting switched to a berry node after the Commander goal had completed. Therefore a goal-only selector could not preserve the explicit constraint.

The approved conditional branch adds SourceKind to existing gather/slaughter commands and queued orders, and GatherSourceKind to active unit state. Native redirection, depletion, farm substitution and automatic slaughter filter by the same source projection. Ordinary later human orders replace/clear the restriction through normal queue/override semantics. Automatic resource deposit/resume retains it. Restricted active/queued state participates in the simulation checksum.

Unrestricted binary batches retain exact legacy bytes. Restricted batches use marker int.MinValue, version byte 1, nonnegative tick, count, then ordinary command payloads with a source-kind byte on gather/slaughter. Updated readers accept both forms and reject unknown version/source/type, invalid lengths, truncation or trailing restricted data. Restricted batches require updated peers; legacy receivers cannot enforce the constraint and must not be treated as compatible. Legacy JSON omission defaults to Any; present malformed sourceKind rejects rather than downgrades. JSON rejection follows the existing null/error contract.
