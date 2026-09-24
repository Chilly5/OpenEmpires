# Phase 4D.2 Task 3 brief

Read this with the approved design spec `Docs/superpowers/specs/2026-09-24-commander-phase4d2-design.md`. Do not expand into Phase 4D.3.

## Global Constraints

- Start production work only after the root confirms the recorded Phase 4D.1 Gates 1–5 PASS against final 4D.1 source; reread current APIs and preserve the dirty, uncommitted user tree. `Docs/CommanderPhase4D/phase4d-source-baseline.json` remains the immutable Phase 4C reference.
- Only one production writer and exactly one Unity runner owner at a time. Finish each test job to terminal before starting another; do not overlap full suites or redo a completed run because a handle was lost.
- This phase is observation only. Preserve `StrategicApprovalLayer → StrategicDecisionPolicy → StrategicPlanner → CommanderGoalManager → existing deterministic RTS execution`; no provider/planner shortcut, control, intent, goal, reservation, command, simulation mutation, autonomous adaptation, or advisory emission from health.
- Do not add `TechnologyRush`, `SiegePreparation`, `NavalExpansion`, network messages, packages, credentials, cloud memory, wall-clock/frame/random decisions, or hidden enemy information. Do not alter the Phase 4C House reservation policy without an actual RED correctness failure and authority review.
- Preserve public and reflection-sensitive signatures. New snapshots contain copied primitive/value data only; explicitly serialized fields are bounded, deterministic, culture-invariant, and stable-order; unknown enum values fail closed.
- Before a protected-file edit, record frozen before-hash, exact diff, reason, authority impact, focused regression, after-hash, and independent review. Use `Docs/CommanderPhase4D/phase4d-protected-boundary-frozen.json`; do not silently rebaseline.
- Use meaningful behavioral RED before each change where practical. Record agent, task, expected output, RED, implementation, GREEN, review, and residuals in `Docs/CommanderPhase4D/phase4d2-task-report.md`; update `progress.md` and `requirements-matrix.md` only during execution, not as plan preparation.
- At the 4D.2 source freeze, run fresh complete EditMode **and** PlayMode suites: zero failed, skipped, or inconclusive and all new test IDs discovered. Do not infer current-source success from Phase 4C/4D.1 totals or historical jobs.

### Task 3: Real recovery scenario and 4D.2 gate

**Files:** Create `Assets/Tests/PlayMode/CommanderPhase4D2RuntimePlayModeTests.cs` with `.meta`; execution evidence `Docs/CommanderPhase4D/phase4d2-task-report.md`, `progress.md`, `requirements-matrix.md`. Production changes here are limited to a demonstrated RED correctness defect reviewed by root; the runtime worker does not become a second concurrent production writer.

**Interfaces:** Reuse the existing Phase 4C.4/4D.1 initialized host, chat recommendation, and real Approve-button flow. Observe health through the Task 1 capture and Task 2 host view; do not construct a fake health snapshot as runtime proof. This task provides the source-freeze evidence and explicit gate decision, not new recovery authority.

- [ ] **Step 1: Write runtime RED/proof test.** Approve a supported `RangedReinforcement` or `DefensiveTurtle` plan through actual chat/UI. Establish a legitimate resource depletion or production wait without changing approved targets or the House reservation policy. Capture plan ID/tick/revision, typed milestone and linked child states, owned resource/queue/population values, and health. Assert waiting is temporary/structured, no new approval/provider call occurs, and deterministic gathering or queue clearance restores **observable progress** in the same plan (milestone or child completion/owned or queued target progress). Sample the transition after each relevant simulation tick; if recovery and next wait occur in one tick with no externally stable `Healthy` interval, assert the supported progress delta rather than inventing a `Healthy` observation. Require eventual completion with zero leaked strategic reservations and no unapproved commands/goals. Use bounded simulation ticks and concise diagnostic markers.

```csharp
int approvedPlanId = plan.StrategicPlanId;
int completedBefore = health.CompletedMilestones;
bool observedProgress = false;
for (int i = 0; i < 36000 && !plan.IsTerminal; i++)
{
    simulation.Tick();
    var observed = planner.CapturePlanHealth(plan.OwnerPlayerId, approvedPlanId);
    if (observed.CompletedMilestones > completedBefore) observedProgress = true;
}
Assert.That(observedProgress, Is.True, "Recovery must yield observed progress, not merely elapsed ticks.");
Assert.That(plan.StrategicPlanId, Is.EqualTo(approvedPlanId));
Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
```
- [ ] **Step 2: Run the exact runtime group** and preserve RED if existing health incorrectly classifies or fails to recover. A fixture/setup failure or zero discovered tests is not a feature RED; correct the fixture and rerun. Do not edit shared planner/tactical recovery code to force GREEN unless root reviews a real source-level correctness defect and its authority impact.
- [ ] **Step 3: Run final focused and affected Commander regressions.** Require 100% focused 4D.2 EditMode/PlayMode PASS, compiler/import errors zero, independent Sol review of spec, lifecycle, stale/reset, fog, resource ownership, test adequacy, and no Critical/Important findings. Run relevant Phase 4C explanation/context and 4D.1 lifecycle/host regressions. Record actual counts, discovered IDs, source hashes, job IDs, XML/payload hashes, and residuals; do not quote historical totals as current.
- [ ] **Step 4: Freeze source and run complete suites.** Run full EditMode then full PlayMode via the singular runner, no filters. Require zero failed, skipped, inconclusive and all expected Phase 4D.2 test IDs present. Save durable native XML where supplied; otherwise save terminal payloads and state the limitation. If a shared planner fix follows, repeat complete suites on the changed source.
- [ ] **Step 5: Boundary and gate review.** Search actual changed source for credentials, `.env`, forbidden provider/AI-to-CommandBuffer or simulation paths, reflection escape hatches, new network authority, Unity object retention, and package/settings drift. Compare frozen protected hashes and exact intended diffs; classify every candidate. Inspect `git status` for test/recovery scenes, logs, and scratch assets without deleting user work. Update report/progress/matrix with the five gate verdicts and SHA-256 evidence. Root alone declares 4D.2 PASS and allows 4D.3 production; any Critical/Important authority, stale, ownership, reservation, or full-regression defect stops advancement.

