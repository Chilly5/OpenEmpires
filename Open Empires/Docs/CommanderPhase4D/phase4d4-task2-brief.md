# Phase 4D.4 Task 2 brief — thin host presentation and PlayMode proof

This brief is extracted from `Docs/superpowers/plans/2026-09-25-commander-phase4d4.md` Task 2. Read this first as the single source of task requirements, then consult the design spec for ambiguities. Do not start production until Task 1's scoped gate passes.

**Files:** Create `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Advisories.cs` and `.meta`; modify `CommanderChatUI.cs` and possibly `.StrategicControls.cs` only at existing event, health refresh, and reset hooks; create `Assets/Tests/PlayMode/CommanderPhase4D4HostPlayModeTests.cs` and `.meta`.

**Interfaces:** Consume the accepted Task 1 feed values. The host owns pipeline/generation and owner validation; feed has no host reference. Presentation is transcript-only (`AppendLine` with non-authoritative memory behavior); existing approval/control and provider interfaces do not change.

- Write real-host PlayMode RED tests with exact IDs `Advisory_DoesNotExecuteAnything` and `Advisory_DoesNotCallProvider`: start an approved plan, induce typed wait/pause/recovery, assert a bounded advisory appears but provider, decision, plan, goal, reservation, and command counts do not change because of the observation. Add named host cases `Advisory_PauseResumeTransition`, `Advisory_PopulationRecovery`, `Advisory_CancelAndComplete`, `Advisory_NonSelectedPlanTransition`, `Advisory_SameTickEventsAreNotLost`, `Advisory_SamePipelineReinitializeClearsState`, and `Advisory_ResetRejectsOldProviderAndEvent`. Each case asserts transcript output and unchanged provider/decision/plan/goal/reservation/command counts except ordinary deterministic work explicitly induced by its fixture. Include reused numeric plan ID after reset where practical.
- Run exact focused host IDs to meaningful behavioral RED. Preserve job/evidence.
- Wire feed to existing planner status/milestone/goal/reservation callbacks and a periodic scan of **every active owned plan once per `simulation.CurrentTick`**. Event callbacks capture fresh health for their exact plan, including terminal plans removed from `ActivePlans`, even if the periodic scan already ran in the same tick; pure-feed dedupe suppresses repeated state, not distinct same-tick transitions. Verify owner, pipeline identity, `ReferenceEquals(planner.GetPlan(id), eventPlan)`, creation identity, and generation before presenting. Render only the copied snapshot, not mutable event-plan text. Never call bridge, approval, lifecycle, planner submission, goals, commands, or provider from this path. Reset feed on `Initialize`, same/different-pipeline `InitializeStrategic`, `ResetConversation`, owner/match replacement, and `OnDestroy`.
- Run focused GREEN, affected Phase 4C explanation/4D1 controls/4D2 health/4D3 adaptation and runtime PlayMode suites, and zero compiler errors. Independent Sol review must verify no authority path, fog leak, cross-match advisory, or spam before the 4D.4 gate.

## Binding global constraints

- Advisory is information only: no planner submission, goal-manager mutation, command buffer, lifecycle control, bridge approval, provider call, or new objective from advisory production code.
- Use only owner-scoped detached health; no hidden enemy data, simulation object retention, wall-clock gameplay logic, cross-match state, unbounded history, or per-tick transcript spam.
- Preserve current dirty workspace and protected hashes; do not stage, commit, reset, clean, change packages/settings/credentials, or touch unrelated scenes.
- One production writer and one Unity runner. Save exact job IDs/counts/native XML if available or complete terminal payload, hashes, and compiler state.

## Preflight source observation

The current `CommanderChatUI.StrategicControls.cs` `LateUpdate` returns early when selected-plan UI is absent or no plan is selected. The required all-owned-plan advisory scan must not sit behind that selected-plan return. This is a source observation, not permission to alter unrelated UI behavior.
