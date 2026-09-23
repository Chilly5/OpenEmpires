# Phase 4C.4 Task 1: executable objective expansion

**Implementer:** GPT-6 Sol. **Scope:** both supported objectives and their complete admission, approval, planning, and runtime path. **Gate:** independent review, full regression, boundary audit, and final Phase 4C report after this task freezes.

Read the complete [4C.4 plan](../superpowers/plans/2026-09-16-commander-phase4c4.md), [design contract](../superpowers/specs/2026-09-15-commander-phase4c4-design.md), and [verified integration map](phase4c4-integration-map.md) before editing. The plan's Task 1 steps and file list are the implementation checklist; this brief records the handoff and current entering gate.

## Entering gate

- Phase 4C.1 and 4C.2 passed their recorded gates. Phase 4C.3 passed on 2026-09-22: full EditMode `5e75475490774304a8e9db599b9c08b8` 540/540 and full PlayMode `324e2dad9a174f48bfe107d78813c80d` 77/77, both with zero failures/skips. The independent review and 55-file protected-boundary audit were clean. Use this as the current baseline; do not rerun it before changes.
- Current source is dirty with authorized prior-phase edits. Snapshot exact before-content and SHA-256 for every existing file you touch, rather than using HEAD as the comparison base. Preserve unrelated changes.
- Unity instance `Open Empires@6d7310c7`, Unity 6000.5.9f1, Unity MCP 10.2.0. Read editor state before test actions. If a subagent MCP approval prompt stalls, send the exact intended action to root; root can run/poll it. One Unity runner at a time.

## Non-negotiable implementation boundary

- Implement only `RangedReinforcement` (food8, wood8, ensure ArcheryRange, ensure10 Archers, Ready) and `DefensiveTurtle` (food8, wood8, ensure Barracks and ArcheryRange, build two mandatory Towers, ensure8 Spearmen and8 Archers, Ready). Preparation is finite; no automatic holding, attack micro, walls, keeps, or garrisons.
- TechnologyRush, SiegePreparation, and NavalExpansion remain unsupported and unadmitted. Append enums without changing old numeric identities; unknown enum values and unknown/mixed input fail closed.
- Use existing goal requests, approval, planner, executor, and simulation. No new command, tactical goal, executor, simulation mechanic, autonomous evaluator rule, package, setting, or credential change. AIRecommendation retains Normal authority; no name-based Emergency upgrade or direct-player displacement.
- Integrate both plans at all three concrete-plan checks: canonical budget assignment, worker fitting, and insufficient-resource prepared-economy/recovery. Quote costs from current game specifications and actual deficits. For only the two new plans, permit a fitted zero allocation when workers are fewer than resource buckets; prove funded one-worker execution. Zero workers remain infeasible.
- Add strict parser/provider/router mappings, complete honest preview wording, and actual UI Approve path. Preview must have no plan, goal, command, reservation, or history side effect.

## Evidence and handoff

- Follow assertion-level RED then focused GREEN for `CommanderPhase4C4` EditMode and PlayMode. Save full per-test artifacts and report job IDs. Compilation failures or zero-discovery jobs do not count as RED.
- The PlayMode tests must show each objective progressing from an unsatisfied fixture through real approval and existing execution to completed plan, required structures/units, and released reservations within a declared tick bound. Include one-worker funded execution and below-age turtle rejection.
- Write `phase4c4-task1-report.md` incrementally, save before snapshots under `phase4c4-task1-before/`, and freeze source/tests after focused verification and self-review. Root will arrange independent review and full suites; do not start full regression in Task 1.
- No commit, push, merge, branch, or unrelated edits. Never print credential values.
