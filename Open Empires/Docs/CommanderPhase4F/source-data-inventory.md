# Phase 4F source/data inventory

Inventory date: 2026-10-04. The local Unity working tree is authoritative.

This is the source/data inventory for the current provisional Phase 4F implementation. It identifies canonical paths and the detached adapter used for each; unresolved canonical gaps remain explicitly listed. No row authorizes a manually maintained AI fact table.

| Knowledge | Canonical source | Adapter rule |
|---|---|---|
| Unit identity/state | `UnitData.UnitType`; `UnitData` is runtime state | Snapshot stable `unit:<integer>` IDs; never expose `UnitData`. |
| Unit costs/times/combat | `SimulationConfig`; mapping and civ resolution in `GameSimulation.GetUnitTrainingSpec`, `GetUnitTrainingCosts`, `ResolveCivUnitType` | Read through config/rules, then detach. |
| Buildings/footprints/costs | `BuildingType`, `SimulationConfig`, `GameSimulation.GetBuilding*Cost`, `GameSimulation.GetConstructionTicks` | `Build(GameSimulation)` reads the public simulation queries, then detaches values. Live `CreateBuilding` max-health/footprint authority has no detached getter yet and remains an explicit gap. |
| Building age gates | `LandmarkDefinitions.GetBuildingRequiredAge` | Call the existing rule; do not duplicate a second table. |
| Production relationships | `GameSimulation.TryGetProductionBuildingType` and `IsCompatibleProductionBuilding` | Runtime-effective queries remain authoritative; base catalog records the current canonical producer mapping for supported standard units. |
| Technologies | `TechnologyType`, `SimulationConfig`, `ResearchSystem.GetResearchTicks`, `GameSimulation.GetTechnologySpec` | The read-only wrapper reuses command-path age/cost/location rules and research duration; unavailable structured effects remain documented gaps. |
| Ages/landmarks | `GameSimulation` age state and `LandmarkDefinitions` | Age records are detached summaries; live age remains simulation state. |
| Civilizations/replacements | `Civilization`, `GameSimulation.GetPlayerCivilization` and `ResolveCivUnitType`, landmark definitions | Runtime availability projection calls `ResolveCivUnitType(Civilization,int)`; the separate requested-type production gate remains gameplay authority. |
| Resources | `ResourceType`, `GameSimulation` drop-off rules | Static resource relationships only; live nodes never enter static catalog. |
| Combat | `SimulationConfig`, runtime application in `GameSimulation`, combat systems | Expose supported base fields; no invented combat tags. |

Known canonical gaps are intentional: there is no standalone immutable unit/building definition table, no structured technology-effect graph, and no combat-tag model. The projection does not invent those facts.

Open inventory work: field-level prerequisite/modifier/availability coverage, localization, and structured effects remain incomplete; the exact source members above are the current adapter anchors, with unsupported fields recorded rather than fabricated.
