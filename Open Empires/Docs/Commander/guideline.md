# Commander data-maintenance guideline (Phase 4F accepted)

Status: Accepted and verified by independent hostile audit. OpenEmpires canonical gameplay systems remain authoritative; GameKnowledgeCatalog provides detached read-only projections.

OpenEmpires gameplay definitions remain authoritative. The Commander consumes detached projections through `GameKnowledgeCatalog`; `CommanderCapabilityCatalog` separately controls what execution currently supports.

## Adding or changing ordinary content

Use the existing canonical path first: unit/runtime identity and rule mapping in `UnitData`/`GameSimulation`, balance and footprints in `SimulationConfig`, building/age rules in `LandmarkDefinitions`, and research rules in `ResearchSystem`/`GameSimulation`. Do not add a second Commander cost, prerequisite, civilization, or production table. After a canonical change, rebuild the catalog and run the Phase 4F catalog tests plus full Unity regression.

## Adding a unit

Register the unit through the normal game identity/rule path and populate its canonical config/runtime combat values. The current inventory points to `UnitData.UnitType`, `SimulationConfig`, and `GameSimulation.GetUnitTrainingSpec`/`ResolveCivUnitType`; verify those members in the live source before relying on them. The projection should expose only fields that are actually canonical. A unit appearing in knowledge does not make it executable; add a capability entry only after deterministic production/validation exists and add an execution test. Required Phase 4F maintenance tests include cost mutation propagation, prerequisite/producer propagation where supported, civilization availability, and discovery of a new standard unit without prompt edits.

## Adding a building or technology

Use `BuildingType`, `SimulationConfig`, `LandmarkDefinitions`, and the existing research/production rules. The catalog adapts those values. Do not teach the provider with a phrase-specific parser rule or duplicate numeric facts. Verify construction time, footprint, age, research location, effects, and prerequisites against the actual canonical member; if a structured source is absent, document the gap instead of inventing metadata. Run the focused catalog suite plus the relevant full EditMode/PlayMode jobs after source freeze.

## Adding a civilization or modifier

Use `Civilization` and the existing `GameSimulation` resolution/modifier paths. Effective projections must call those read-only rules and return detached values. Do not mutate the simulation while answering a knowledge question. Add tests proving civilization availability, effective cost/stat changes, reset/cache isolation, and that adding a civilization using existing mechanics does not require a second Commander table or prompt edit.

## New mechanics

If the mechanic is not represented by an existing deterministic rule, document the missing canonical source and implement gameplay authority first. Knowledge discovery alone must never grant execution.

## Provider and security rules

Send only bounded `KnowledgeSlice` data. Never expose `GameSimulation`, registries, workers, buildings, commands, delegates, or credentials. Reject unknown/forged/removed IDs, ambiguous aliases, duplicate IDs, oversized text, wrong-civilization content, invalid ages/technologies, and unsupported capabilities safely. These security and provider-boundary cases remain required Phase 4F evidence; do not mark them passing from static intent alone.

## Phase 4G capabilities

Generic actions belong in the semantic action schema and deterministic capability executor, not in prompt-only facts. Add a selector only when it can resolve owned/current/visible state and tie-break deterministically. Reuse existing `ICommand` paths and let `GameSimulation` perform final validation. A discovered knowledge record is not automatically executable; register execution separately and add focused runtime evidence. See `Docs/CommanderPhase4G/semantic-action-schema.md` and `execution-mapping.md`.
