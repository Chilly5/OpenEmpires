# Commander Phase 4C.3: Detached strategic insights

Architecture only. Implementation begins after the memory and explanation gates pass. This contract supplements the overall Phase4C design and does not authorize simulation or provider authority changes.

## Chosen data boundary

Derive insights from the existing fog-safe owned-state snapshot and the existing game-owned plan projection. Providers never receive a sampler delegate, simulation, planner, plan instance, or context-builder reference. Add a dedicated immutable `StrategicContextInsights` value on StrategicContext, optional for backwards-compatible callers, and include it deliberately through StrategicAIContextSerializer. The builder owns aggregation; the provider only reads serialized values.

No time-series recorder is added. There is no existing authoritative detached income series, and current resources minus previous resources mixes spending, refunds and gathering. Expose income trends as unavailable rather than inventing an income metric. Observed worker allocation is labelled activity, not measured productivity/efficiency. No hidden enemy history or future prediction is derived.

## Aggregates and semantics

1. Worker activity: own total workers and currently gathering workers; activity basis points `gathering * 10000 / total`, clamped 0..10000, with availability false when total is zero. Use wide integer multiplication to avoid overflow. Remaining workers are not labelled idle: they may be building, walking or performing other tasks.
2. Army composition: owned and queued counts by explicitly sorted unit type, excluding villagers and sheep under existing rules. Owned percentage basis points use owned military total only; queued units are separate. Zero total means no owned composition, not a forecast of the future army. Unit display names must not require provider access to simulation services.
3. Production pressure: per owned producer type, completed count, under-construction count, active queues, queued units, and idle completed count. Describe no idle producers only when that is supported by the counts; do not call a long queue a proved resource shortfall. A type with only construction pending is identified separately from completed busy producers. Population current/cap/maximum may be included as observed constraints; do not infer that a particular queued unit is blocked without execution evidence.
4. Plan progress: detached plan ID/type/status and completed/total milestone counts, with current milestone name/status from existing projection. Count actual completed milestones; do not estimate elapsed time, completion date, chance of success, or enemy response. The ratio is stage progress, not work-weighted completion. Keep terminal/completed outcome explanations distinct from the active-plan list, which intentionally excludes terminal plans.

All aggregates use deterministic integer arithmetic and stable ordinal/enum ordering. Constructors clone inputs and collection entries are immutable. Optional absent insights preserve old constructor callers. Provider serialization selects only these fields and neither reflects arbitrary game objects nor serializes full planner state.

## Useful advice without authority

The strategic interpreter can use these aggregates to understand why a request may require infrastructure or has little free production capacity, but cannot change objective allowlists, quantities, priority, authority, owner, or identity. All requests still produce previews requiring fresh explicit approval. The deterministic explanation/progress surface can display factual aggregate summaries; it must not turn a displayed bottleneck into an automatic command or a policy evaluation.

## Required proof

- `ContextRemainsFogSafe`: compare actual emitted request JSON from fixtures differing only in hidden and explored-only enemy state; new insights and full permitted payload are unchanged. Visible enemy differences remain confined to pre-existing allowed visible-threat aggregates.
- `ContextSerializationDeterministic`: identical detached input with different source collection insertion orders and non-English current cultures yields byte-identical stable JSON. Do not compute expected values with the implementation's own serializer.
- Hand-checked arithmetic cases: workers zero and nonzero; queued army excluded from owned percentages; producer under construction vs completed busy/idle; milestone transitions and detached snapshots surviving subsequent plan changes.
- Include a captured provider request that consumes the new insights; adding unused properties is not integration proof.
- PlayMode: change own worker allocation/queues and advance a real plan via existing execution, then capture fresh insights and verify observed values change without the capture call generating commands, goals, plans, or policy decisions.
- Full focused/regression/static/review gates before objective expansion.

## Verified integration map (2026-09-16)

- `StrategicContextBuilder.Build` already receives the fog-safe CommanderContext and game-owned planner. It sorts military by unit type and production by ordinal building name; production accumulation distinguishes construction, active queues and available completed capacity. Reuse these detached aggregates instead of another simulation reader.
- `StrategicProductionState.ProductionBuildingCount - UnderConstructionCount` is completed production count; `AvailableCapacity` is idle completed producers; `ActiveQueueCount` and `QueuedUnitCount` remain separate measures. Under-construction producers must not be called idle completed buildings.
- `StrategicPlanState` currently copies plan ID/type/status/current milestone/reason but no milestone counts. Extend this existing host-side projection with copied completed/total counts from `plan.Milestones`; retain the constructor shape. Do not pass the live plan to the insights service/provider.
- `StrategicContext.TotalGatheringWorkers` is derived from detached WorkerAllocation; no throughput or gathering-time series exists in this path. Income remains explicitly unavailable. Arithmetic uses wide intermediates.
- `Phase4B1/StrategicAIContextSerializer.Serialize` deliberately builds the provider payload; adding only StrategicContext properties does not integrate insights. Add explicit whitelisted fields and stable sorting there, preserving old fields and public signatures.
- Existing context/plan constructors and reflection-facing APIs must remain compatible; use distinct builder methods or compatible overloads rather than silently replacing an existing arity.

## Delegation (execution remains gated)

Sol implements the detached values, game-owned projections, provider serialization, and non-trivial tests after 4C.2 stabilizes. Luna owns repetitive tests/static audits/evidence formatting. Astra reviews fog safety, honest metric semantics, immutable boundaries, and integration. Detailed file/signature planning uses the final prior-phase interfaces and existing StrategicPlanState projection.
