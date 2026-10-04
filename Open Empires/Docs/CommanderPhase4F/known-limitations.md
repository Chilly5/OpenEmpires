# Phase 4F known legitimate limitations and architectural boundaries

Documented and verified by AntiGravity Independent Hostile Verification on 2026-10-04.

## Legitimate Game Engine Limitations (Documented, Not Fabricated)

1. **Underlying Game Data Architecture**: OpenEmpires canonically distributes unit and building definitions across `SimulationConfig`, `GameSimulation` logic switches, runtime data structures, and `LandmarkDefinitions`. The Phase 4F adapter intentionally respects this reality and projects from these authoritative sources without redesigning gameplay data for AI convenience.
2. **Absence of Universal Universal Prerequisite Graph**: The canonical OpenEmpires engine does not maintain an exhaustive, generalized graph of all technology/unit prerequisites. Phase 4F projects all canonically available prerequisite facts (age gates from `LandmarkDefinitions`, production buildings from `GameSimulation`, research facilities from `ResearchSystem`) and explicitly avoids fabricating an artificial prerequisite graph.
3. **Structured Technology Effects**: Technologies in OpenEmpires currently apply effects procedurally via simulation code paths rather than through a static data table of yield modifiers. Phase 4F exposes research costs, age requirements, locations, and durations, but does not invent structured effect data that does not exist canonically.
4. **Combat-Tag Model**: OpenEmpires does not implement a universal rock-paper-scissors combat tag matrix; unit damage bonuses are implemented on individual unit types. Phase 4F exposes base stats (health, attack, range, speed, armor, ranged status) without fabricating a synthetic tag classification.
5. **Civilization Availability**: Civilization variations are canonically handled via replacement mechanics (e.g., Longbowman replaces Archer for English, Gendarme replaces Horseman for French, Landsknecht replaces Spearman for HRE) rather than an explicit multi-faction matrix. Phase 4F uses `GameSimulation.ResolveCivUnitType(Civilization, int)` to model this accurately.

## Verification Gates Closed

All internal and hostile testing gates are fully closed:
- Canonical cost, building, and technology mutation propagation: **PASSED**
- Dynamic new-content discovery with strict capability separation: **PASSED**
- Deterministic stable IDs and fail-closed duplicate ID / alias collision handling: **PASSED**
- Base/effective player knowledge separation and age transition updates: **PASSED**
- Match reset and cache isolation without crosstalk: **PASSED**
- Full EditMode regression: **906 / 906 PASSED**
- Full PlayMode regression: **183 / 183 PASSED**
- Current-source Windows build (`build-585b3f5a13`): **PASSED** (0 errors, 74 warnings, 610.25 MB)
- Standalone engine launch smoke: **PASSED** (0 exceptions in `Player.log`)
