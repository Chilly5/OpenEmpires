# Phase 5B Commander Content Inventory

This inventory records the existing native simulation content that Commander can name and route through ordinary training or construction. The catalog is a detached projection; it does not grant the provider gameplay authority.

## Native trainable roster

Names follow the existing `UnitInfoUI` name/plural tables. `GameSimulation.ResolveCanonicalCivilizationUnitType` (also used by its instance resolver) is the sole civilization replacement rule source.

| Type | Canonical UI name | Native producer | Age | English | French | HRE |
|---:|---|---|---:|---|---|---|
| 0 | Villager | Town Center | 1 | Villager | Villager | Villager |
| 1 | Spearman | Barracks | 1 | Spearman | Spearman | Landsknecht (12) |
| 2 | Archer | Archery Range | 1 | Longbowman (10) | Archer | Archer |
| 3 | Horseman | Stables | 1 | Horseman | Gendarme (11) | Horseman |
| 4 | Scout | Stables | 1 | Scout | Scout | Scout |
| 6 | Man-at-Arms | Barracks | 3 | Man-at-Arms | Man-at-Arms | Man-at-Arms |
| 7 | Knight | Stables | 3 | Knight | Knight | Knight |
| 8 | Crossbowman | Archery Range | 3 | Crossbowman | Crossbowman | Crossbowman |
| 9 | Monk | Monastery | 3 | Monk | Monk | Monk |
| 10 | Longbowman | Archery Range | 1 | Longbowman | — | — |
| 11 | Gendarme | Stables | 1 | — | Gendarme | — |
| 12 | Landsknecht | Barracks | 1 | — | — | Landsknecht |
| 13 | Battering Ram | Siege Workshop | 3 | Battering Ram | Battering Ram | Battering Ram |
| 14 | Mangonel | Siege Workshop | 3 | Mangonel | Mangonel | Mangonel |
| 15 | Trebuchet | Siege Workshop | 3 | Trebuchet | Trebuchet | Trebuchet |

An em dash means the unit is unavailable to that civilization. Unique units are available only in their canonical owner civilization; this is derived from `GameSimulation.ResolveCivUnitType`, not a manually maintained availability table. Sheep (type 5) is a map resource, not a trainable unit. English King (type 16) is not ordinary trainable content: native handling is a landmark completion reward. It remains in the knowledge inventory but is excluded from ordinary Commander training.

The inventory contains all 17 native UI unit identities, including non-trainable Sheep and English King. Canonical labels come from `UnitInfoUI.GetUnitTypeDisplayName` / `GetUnitTypePluralName`. Native producer identity is queried by `GameSimulation.TryGetCanonicalProductionBuildingType`, and ages by `LandmarkDefinitions.GetUnitRequiredAge`. Runtime costs/admission remain in `GameSimulation.GetUnitTrainingSpec`, `CanBuildingTypeTrainUnit`, and the TrainUnit command handler. Civilization availability is projected from the static native replacement query; world fauna/rewards are not ordinary trainable roster entries.

## Named civilization landmarks

In addition to all 22 `BuildingType` identities, all 14 declared `LandmarkId` records are projected directly from `LandmarkDefinitions.Get`: native name, owning civilization, target age, food/gold cost, footprint, health and construction duration. Their deterministic IDs are `landmark:<LandmarkId>`; copied projections retain that identity. Foreign landmarks remain known but unavailable. Named landmark execution/selection is not added: these records are knowledge-only, while existing ReachAge keeps its original game-owned landmark choice. A named landmark target cannot collapse to the generic Landmark selector. Total building identities: 36.

## Ordinary constructible buildings

| Building type | Canonical UI name | Required age | Commander ordinary placement |
|---|---|---:|---|
| House | House | 1 | Yes |
| Barracks | Barracks | 1 | Yes |
| TownCenter | Town Center | 2 | Yes |
| Mill | Mill | 1 | Yes |
| LumberYard | Lumber Yard | 1 | Yes |
| Mine | Mine | 1 | Yes |
| ArcheryRange | Archery Range | 2 | Yes |
| Stables | Stables | 2 | Yes |
| Farm | Farm | 1 | Yes |
| Tower | Tower | 2 | Yes |
| Monastery | Monastery | 3 | Yes |
| Blacksmith | Blacksmith | 2 | Yes |
| Market | Market | 2 | Yes |
| University | University | 3 | Yes |
| SiegeWorkshop | Siege Workshop | 3 | Yes |
| Keep | Keep | 3 | Yes |

The building enum also contains Wood Wall (`Wall`), Stone Wall, Stone Gate, Wood Gate, Landmark, and Wonder. These use segment/gate placement, civilization-specific age-up, or victory mechanics and are explicitly outside the ordinary `BuildStructure` path. Catalog discovery does not make them Commander-placeable.

Building names come from `UnitInfoUI.GetBuildingTypeDisplayName` / `GetBuildingTypePluralName`; ages come from `LandmarkDefinitions.GetBuildingRequiredAge`. Runtime cost and construction-time facts are projected from `GameSimulation.GetBuilding*Cost` / `GetConstructionTicks`; dimensions are from `SimulationConfig`. `CommanderPlanner.GetFootprint` now maps every ordinary constructible building to its corresponding config dimensions. Native placement still owns age, cost, legal tile, and command validation.

## Request and execution boundaries

`CommanderContentNameResolver` resolves exact/normalized names and catalog aliases into detached records; role terms such as “cavalry” return ambiguous bounded suggestions, and close misspellings return suggestions without a match. Availability is reported separately from execution support. Civilization replacement does not silently rewrite an explicitly named base unit, and custom catalog registrations do not become available or executable merely by appearing in knowledge.

Semantic `EnsureUnitCount` and `BuildStructure` parse through the shared name path. Producer-node validation uses the catalog’s native producer relationship. Execution continues through `CommanderIntentResolver` → `CommanderGoalManager` → `CommanderPlanner` → `TrainUnitCommand` / `PlaceBuildingCommand` → `GameSimulation`; parsing, retrieval, and catalog suggestions cannot directly issue game effects.
