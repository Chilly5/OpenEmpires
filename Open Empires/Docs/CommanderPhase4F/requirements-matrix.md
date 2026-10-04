# Phase 4F requirements matrix (Accepted)

Audited by AntiGravity Independent Hostile Verification on 2026-10-04.

| Area | Verified Evidence | Status |
|---|---|---|
| Source inventory | `source-data-inventory.md` maps canonical configs/rules to detached records; absent graphs documented rather than fabricated | PASS / VERIFIED |
| Detached knowledge catalog | `CommanderKnowledgeCatalog.cs` snapshots pure value-records without live Unity/simulation references | PASS / VERIFIED |
| Capability separation | `CommanderCapabilityCatalog`; custom discovered content is marked unexecutable; tested via `NewContentDiscovery_DiscoversCustomUnitWithoutExecutionAuthority` | PASS / VERIFIED |
| Stable IDs/order | `unit:<id>`, `building:<enum>`, `technology:<enum>`, ordinal sorting; duplicate stable IDs fail closed (`DuplicateStableIds_FailsClosed`) and alias collisions fail closed (`AliasCollision_FailsClosed`) | PASS / VERIFIED |
| Bounded provider slice | `KnowledgeSlice` caps records at 24 (context builder requests 12); deterministically serializes id, name, age, producer/location, and cost under 8,192-char budget (`ContextSerialization_ContainsCanonicalPrerequisitesAndRemainsBounded`) | PASS / VERIFIED |
| Base/effective separation | `EffectivePlayerKnowledge` snapshots civ, age, and canonical effective unit costs via `GameSimulation.GetUnitTrainingSpec` without simulation mutation | PASS / VERIFIED |
| Immutability/reset | Read-only collections, value-copy methods, zero live reference leakage; match reset isolation verified (`ResetAndCacheIsolation_MatchInstancesDoNotLeak`) | PASS / VERIFIED |
| Canonical cost propagation | Canonical cost changes in `SimulationConfig` propagate directly to projected unit/building/tech knowledge without AI table edits (`CostMutation_PropagatesFromSyntheticCanonicalConfig`, `BuildingDataMutation_PropagatesFromCanonicalSource`, `TechnologyDataMutation_PropagatesFromCanonicalSource`) | PASS / VERIFIED |
| Prerequisite/production propagation | Canonical age gates (`LandmarkDefinitions`) and production/research buildings (`GameSimulation`) projected accurately (`Prerequisites_ExposesCanonicalAgeAndProducerRelationships`) | PASS / VERIFIED |
| Civilization availability | Civilization replacement projections call canonical `GameSimulation.ResolveCivUnitType(Civilization,int)` (`SimulationCatalog_CivilizationProjectionUsesCanonicalResolver`) | PASS / VERIFIED |
| New content discovery | Dynamic custom content registration via `RegisterCustomUnit` / `RegisterCustomBuilding` discovered by catalog without prompt or AI cost edits; capability rejects execution authority (`NewContentDiscovery_DiscoversCustomUnitWithoutExecutionAuthority`) | PASS / VERIFIED |
| Security/authority | Unknown/forged IDs (`unit:999999`, `building:FakeBuilding`, oversized strings) fail closed safely (`UnknownAndForgedIds_FailsClosed`); no capability escalation | PASS / VERIFIED |
| Full regressions/build | EditMode 906/906 passed; PlayMode 183/183 passed; Focused 27/27 passed; Windows build `build-585b3f5a13` succeeded (0 errors, 74 warnings, 610.25 MB) | PASS / VERIFIED |
| Standalone engine launch | `Builds/Phase4F/OpenEmpires-Phase4F.exe` launches cleanly with D3D12/Mono/Input/Physics subsystems initialized; 0 exceptions in `Player.log` | PASS / VERIFIED |
| Source/boundary manifests | `phase4f-final-source-hashes.json` and `phase4f-final-boundary-audit.json` sealed with final hashes and acceptance flags | PASS / VERIFIED |
| Evidence package completeness | All documentation, source manifests, audit records, and guideline reconciled to final accepted state | PASS / VERIFIED |
