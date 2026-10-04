# Phase 4F maintenance proof (Final Accepted)

Audited by AntiGravity Independent Hostile Verification on 2026-10-04.

## Core Architectural Invariant

The fundamental Phase 4F maintenance requirement is:
> **Change OpenEmpires canonical data once, and the Commander knowledge layer sees the correct truth automatically, without AI-specific prompt edits, cost-table modifications, or duplicate fact databases.**

while strictly maintaining:
> **Knowledge discovery does not grant Commander execution authority.**

## Maintenance Proof Results

### 1. Canonical Cost Propagation: PASS
- **Test**: `CostMutation_PropagatesFromSyntheticCanonicalConfig`
- **Mechanism**: `SimulationConfig` exposes canonical unit balance (`SpearmanFoodCost`, `SpearmanWoodCost`). Modifying the canonical configuration source dynamically propagates into `GameKnowledgeCatalog.FindUnit("spearman").Cost.Food` without modifying any AI prompt or cost-table.
- **Evidence**: Verified with multiple synthetic values (85, 145) changing dynamically and reflecting immediately in rebuilt catalog projections.

### 2. Building & Technology Projection: PASS
- **Tests**: `BuildingDataMutation_PropagatesFromCanonicalSource`, `TechnologyDataMutation_PropagatesFromCanonicalSource`, `SimulationCatalog_UsesCanonicalBuildingAndTechnologyQueries`
- **Mechanism**: `GameKnowledgeCatalog.Build(GameSimulation)` reads canonical building costs via `simulation.GetBuilding*Cost`, construction duration via `simulation.GetConstructionTicks`, and technology specs via `simulation.GetTechnologySpec`. Modifying canonical values (e.g., `BarracksWoodCost` = 310, `BallisticsFoodCost` = 480) updates projections automatically.
- **Prerequisites**: Age gates (`RequiredAge`) and production/research buildings (`ProductionBuildingType`, `ResearchBuilding`) match canonical `LandmarkDefinitions` and `GameSimulation` rules (`Prerequisites_ExposesCanonicalAgeAndProducerRelationships`).

### 3. New Standard Content Discovery: PASS
- **Tests**: `NewContentDiscovery_DiscoversCustomUnitWithoutExecutionAuthority`, `NewContentDiscovery_DiscoversCustomBuildingWithoutExecutionAuthority`
- **Mechanism**: New units and buildings registered in the game knowledge layer (e.g., custom unit 99 "War Elephant" at Stables; custom building 999 "Grand Monument") are automatically discovered by `GameKnowledgeCatalog` with their canonical costs, age gates, and producer mappings without prompt edits or hardcoded AI lookup tables.
- **Capability Separation**: `CommanderCapabilityCatalog.CanExecuteUnit(99)` returns `false` and `TryResolveUnit` returns `false`. Knowledge discovery does NOT grant execution authority.

### 4. Civilization & Effective Player Projection: PASS
- **Tests**: `CivilizationProjection_UsesCanonicalReplacementRules`, `SimulationCatalog_CivilizationProjectionUsesCanonicalResolver`, `EffectivePlayerKnowledge_UsesCanonicalCivilizationStateWithoutMutation`, `EffectiveProjection_UsesSimulationCanonicalReplacementForHre`
- **Mechanism**: Civilization replacement mappings are dynamically queried through `GameSimulation.ResolveCivUnitType(Civilization, int)`. English archer resolves to Longbowman (`unit:10`), French horseman to Gendarme (`unit:11`), and HRE spearman to Landsknecht (`unit:12`). `EffectivePlayerKnowledge` snapshots current civilization, age, and effective training costs through `simulation.GetUnitTrainingSpec(playerId, ...)` without mutating simulation state.

### 5. Unsupported Capability Separation: PASS
- **Tests**: `CapabilityCatalog_DoesNotGrantUnsupportedDiscoveredContent`, `CapabilityCatalog_MatchesAcceptedPhase4EExecutionPolicy`
- **Mechanism**: The catalog projects all canonical units (including siege engines: Battering Ram `unit:13`, Mangonel `unit:14`, Trebuchet `unit:15`), but `CommanderCapabilityCatalog` rigorously restricts executable units to the accepted Phase 4E policy (Villager, Spearman, Archer, Knight). Unsupported units/structures fail closed with 0 goals, 0 commands, and 0 simulation mutations.

### 6. Reset & Cache Isolation: PASS
- **Tests**: `ResetAndCacheIsolation_MatchInstancesDoNotLeak`, `EffectivePlayerKnowledge_AgeTransitionUpdatesWithoutStaleCache`
- **Mechanism**: Independent simulation instances (e.g., Sim 1: English, Age 1; Sim 2: French, Age 3) produce strictly isolated `EffectivePlayerKnowledge` snapshots with zero crosstalk. Advancing a player's age in simulation immediately reflects in the next effective snapshot without stale cache artifacts.

### 7. Provider Bounds & Security: PASS
- **Tests**: `DuplicateStableIds_FailsClosed`, `AliasCollision_FailsClosed`, `UnknownAndForgedIds_FailsClosed`, `ContextSerialization_ContainsCanonicalPrerequisitesAndRemainsBounded`
- **Mechanism**:
  - Duplicate stable IDs trigger immediate fail-closed validation (`InvalidOperationException`).
  - Alias collisions fail closed rather than silently picking the first entry.
  - Forged/unknown IDs (`unit:999999`, `building:FakeBuilding`, oversized strings) return null and cannot be resolved by capability.
  - Serialized provider JSON includes canonical facts (`id`, `name`, `age`, `producer`, `cost`) and comfortably stays within the 8,192-character context budget.
  - Zero live Unity references (`GameSimulation`, `MonoBehaviour`, `GameObject`, `Transform`, `CommandBuffer`, `ICommand`) are serialized or leaked.
