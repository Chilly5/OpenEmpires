# Phase 4D.1 Core Lifecycle — combined plan Tasks 1 and 2

Read first: `Docs/superpowers/specs/2026-09-23-commander-phase4d1-design.md`, the complete user Phase 4D objective at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt` (4D.1 and invariants), and `Docs/superpowers/plans/2026-09-23-commander-phase4d1.md` Tasks 1–2. The live Phase 4C source is pinned by `Docs/CommanderPhase4D/phase4d-source-baseline.json`; do not reset, clean, stash, stage, commit, or modify pre-existing Phase 4C files.

## Deliverable

Implement a validated, deterministic, owner-checked plan-control boundary with immutable detached request/result, stable plan ID + creation tick + revision staleness checks, and exact pause/resume/cancel/status semantics. Pause only plan-owned tactical goals and milestone advancement, retaining reservations; compensate tick-based goal timeout/retry/stall/cooldown anchors on resume. Manual goals/commands continue. Cancel reuses existing `StrategicPlanner.CancelPlan(int)` without changing its signature. No chat/UI code in this task.

## Files in scope

- New: `Assets/Scripts/AI/Commander/Strategic/StrategicPlanControl.cs` and `.meta`.
- Modify: `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs`, `StrategicPlanner.cs`; a `StrategicPlanner.Controls.cs` partial is allowed if it improves focus.
- Protected change only if strictly necessary: `Assets/Scripts/AI/Commander/CommanderGoalManager.cs`, and `CommanderGoal.cs` for tick anchors. Record each frozen hash, exact authority effect, and regression coverage.
- New tests: `Assets/Tests/EditMode/CommanderPhase4D1Tests.cs` and `.meta`.
- Report only: `Docs/CommanderPhase4D/phase4d1-core-task-report.md`; save meaningful RED/GREEN XML under the same Docs directory.

## Required behavior and tests

Use behavioral TDD. Run the focused RED suite before production edits, recording job ID, exact XML, failing IDs, and why each failure is correct. A compile-only missing-symbol error or a deliberate `NotImplementedException` is not adequate RED: use compilable test reflection/adapters if necessary, and assert actual plan/world behavior after the attempted control. Implement only after RED. Cover `PauseStrategy_StopsFutureStrategicProgress`, `ResumeStrategy_ContinuesSamePlan`, `CancelStrategy_ReleasesReservations`, `CancelStrategy_DoesNotDeleteCompletedAssets`, `CancelStrategy_DoesNotAffectUnrelatedPlayerCommands`, `StalePauseRequest_CannotPauseReplacementPlan`, `StaleCancelRequest_CannotCancelReplacementPlan`, `LifecycleCommands_CannotBypassOwnership` at planner boundary, `Reset_ClearsLifecycleState` for runtime teardown, `SameLifecycleStateProducesSameResult`, and three cross-player pause/cancel/resume cases. Add multiple-active-plan ambiguity, unknown action rejection, long pause across goal timeout/blocked retry/construction stall, in-flight atomic completion during pause, manual goal continuity, and no goal/reservation duplication. If a named test logically belongs to the host integration task rather than this core, document that handoff and do not create a fake test.

Only one Unity test runner owner exists: you own focused EditMode tests during this task. Do not overlap with anyone else. Use Unity MCP resource-first workflow, poll the exact job to terminal, preserve XML, check compiler/console. Do not run full EditMode/PlayMode; the user reserves full suites for later gates. Do not call paid providers. Do not spawn subagents.

## Reporting and review

Self-review the actual diff and preserve a detailed report with RED/GREEN commands, job IDs, XML hashes and counts, files touched, protected boundary before/after hashes and exact diff rationale, unresolved concerns. Do not claim the full 4D.1 gate; host/UI/PlayMode remain separate. Do not commit or stage anything because Phase 4C remains uncommitted and must not be silently absorbed. Return a short status `DONE`, `DONE_WITH_CONCERNS`, `BLOCKED`, or `NEEDS_CONTEXT` with report path. If blocked by MCP approval or editor state, inspect live job/console rather than restarting or faking evidence.
