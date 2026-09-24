# Commander Phase 4D.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the local player deterministic, offline status/pause/resume/cancel controls over an approved strategic plan, with stale-request, ownership, reservation, and reset safety.

**Architecture:** A primitive-only plan control request captures plan ID, owner, creation tick, and revision. A validated planner boundary applies actions only to the matching live plan. Planner and plan-owned tactical goals suspend together; existing plan progress and reservations remain. The local chat host parses whole-form commands before provider dispatch and presents a small, plan-bound UI with a two-step cancel confirmation.

**Tech Stack:** Unity 6000.5.9f1, C#, NUnit Unity EditMode/PlayMode, Unity MCP 10.2.0.

**Spec:** `Docs/superpowers/specs/2026-09-23-commander-phase4d1-design.md`; full Phase 4D objective at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`.

## Global Constraints

- Use the live uncommitted Phase 4C source captured by `Docs/CommanderPhase4D/phase4d-source-baseline.json`; preserve all pre-existing user changes and scene/recovery status.
- Only one production writer and one Unity test-runner owner at any time; never overlap suites.
- Retain `StrategicPlanner.CancelPlan(int)` and all reflection-sensitive public signatures. A new validated API may call that primitive after revalidation.
- Do not change provider, approval, policy, simulation, network, package, credential, or unsupported-objective authority.
- Save meaningful RED and GREEN XML; verify discovered test IDs, not just counts. Do not run full suites until the prescribed 4D.2/4D.4 freezes unless a later shared-authority fix requires them.
- Before changing a frozen protected file, document its baseline hash, exact intended authority effect, targeted regression, and independent review. The list is `Docs/CommanderPhase4D/phase4d-protected-boundary-frozen.json`.

## Review Focus

1. Two compatible active plans: a bare text control must reject ambiguity; per-plan UI selection must not target the other plan.
2. Long pause with blocked/construction goals: pause time must not consume tactical duration, retry, stall, or cooldown budget.
3. Already-dispatched atomic command completing during pause: strategic milestone remains still until resume, then advances once.
4. Cancel while translation is in flight: old response cannot recreate pending approval or control state.
5. New match reuses plan ID/tick: an old UI callback cannot act on the new planner instance.

---

### Task 1: Frozen baseline and control value/identity model

**Files:**
- Create: `Assets/Scripts/AI/Commander/Strategic/StrategicPlanControl.cs` and `.meta`
- Modify: `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs`
- Test: `Assets/Tests/EditMode/CommanderPhase4D1Tests.cs` and `.meta`
- Evidence: `Docs/CommanderPhase4D/phase4d1-task-report.md`

**Interfaces:**
- `StrategicPlanControlType`: `Pause`, `Resume`, `Cancel`, `Status` only; unknown values reject.
- `StrategicPlanControlRequest`: immutable `PlayerId`, `PlanId`, `CreatedTick`, `ObservedRevision`, `ControlType` primitive values.
- `StrategicPlanControlStatus`: `Applied`, `NoActivePlan`, `AmbiguousPlan`, `AlreadyPaused`, `AlreadyRunning`, `AlreadyCompleted`, `AlreadyCancelled`, `StalePlan`, `Unauthorized`, `Rejected`.
- `StrategicPlanControlResult`: immutable status plus copied identity and bounded message; no live references.
- `StrategicPlan.Revision`: deterministic integer initialized on plan registration and incremented on structural lifecycle transitions. Preserve `StrategicPlanId` and public `CancelPlan(int)`.

- [ ] Write EditMode RED tests `StalePauseRequest_CannotPauseReplacementPlan`, `StaleCancelRequest_CannotCancelReplacementPlan`, `LifecycleCommands_CannotBypassOwnership`, `SameLifecycleStateProducesSameResult`, and `Reset_ClearsLifecycleState`. Include an unknown enum value that returns `Rejected` and two-active-plan ambiguity. Assert plan status and plan-keyed reservations are unchanged after rejection, rather than asserting only a message.
- [ ] Run only `CommanderPhase4D1Tests` via Unity MCP; save the actual RED XML and exact job ID in the task report. A useful failure must show missing behavior or incorrect state, not a deliberate throw-only stub.
- [ ] Add the value types and planner-captured identity. Ensure a later plan with reused ID but different creation/revision cannot satisfy the old request. Guard checked revision overflow by rejecting rather than wrapping.
- [ ] Re-run the focused EditMode group; record XML path, discovered test IDs, counts, SHA-256, and any compiler errors.

### Task 2: Plan-owned suspension, resume, and cancellation authority

**Files:**
- Modify: `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` and, if separation helps, create `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.Controls.cs` plus `.meta`
- Modify only with documented protected-file exception: `Assets/Scripts/AI/Commander/CommanderGoalManager.cs`, and `CommanderGoal.cs` only if its tick anchors cannot be handled internally
- Test: `Assets/Tests/EditMode/CommanderPhase4D1Tests.cs`

**Interfaces:**
- `StrategicPlanner.CaptureControlRequest(int trustedPlayerId, int planId, StrategicPlanControlType action, out StrategicPlanControlRequest request)`: capture exact nonterminal plan identity only for planner/plan owner; `planId` is explicit for UI.
- `StrategicPlanner.CaptureCurrentControlRequest(int trustedPlayerId, StrategicPlanControlType action, out StrategicPlanControlRequest request)`: exactly one owned active plan required.
- `StrategicPlanner.ApplyControl(StrategicPlanControlRequest request)`: revalidate trusted owner, ID, creation tick, revision, and current status immediately before mutation; status observation is nonmutating.
- Internal goal-manager `SuspendGoal`/`ResumeGoal` operations receive only child goal IDs selected by the planner, never text or provider values.

- [ ] Write behavioral RED tests `PauseStrategy_StopsFutureStrategicProgress`, `ResumeStrategy_ContinuesSamePlan`, `CancelStrategy_ReleasesReservations`, `CancelStrategy_DoesNotDeleteCompletedAssets`, `CancelStrategy_DoesNotAffectUnrelatedPlayerCommands`, and three cross-player pause/cancel/resume cases. Check goals, milestone index/status, reservation IDs/amounts, world assets, and manual command outcomes across ticks.
- [ ] Add RED cases for a long pause (past duration and blocked timeout), a child atomic action finishing while paused, waiting-resource retry suppression, duplicate goal/reservation prevention, and cancellation of a paused plan.
- [ ] Run only the relevant EditMode tests and preserve RED output.
- [ ] Implement scoped suspension: skip plan-owned goals in `CommanderGoalManager.Tick` while suspended, keep manual goals active, and shift the plan-owned goal's tick anchors by the pause interval on resume. Do not mutate sentinels or overflow; reject unsafe arithmetic. Gate `StrategicPlanner.Tick`, `StartOrWaitForMilestone`, `CreateGoalsForMilestone`, `CompleteMilestoneAndAdvance` (public and private), and plan-owned goal event advancement while paused. On resume, process world-observed progress once through existing goals/milestones; do not re-create already submitted child goals or reservations.
- [ ] Route cancellation through the existing `CancelPlan(int)` implementation after identity/owner revalidation. Verify it releases only the selected plan's reservations, skips future milestones, and preserves completed world state and unrelated goals. Recheck stale old requests after replacement and after same-ID/new-runtime setup.
- [ ] Run the focused EditMode group to 100% and capture compiler state. Independently inspect the exact protected-file diff before accepting it.

### Task 3: Offline whole-form chat controls and intentional UI cancel

**Files:**
- Modify: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`
- Create if needed to keep routing focused: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` and `.meta`
- Modify: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs` only for the two exact status aliases
- Test: `Assets/Tests/EditMode/CommanderPhase4D1Tests.cs`
- Test: `Assets/Tests/PlayMode/CommanderPhase4D1PlayModeTests.cs` and `.meta`

**Interfaces:**
- Whole-form parser returns a fixed enum, never an intent or command. It runs before either provider and before stale pending recommendation can become authority.
- The UI owns a selected displayed plan ID and a captured generation/pipeline identity. Cancel's second click must revalidate the same captured control request; a plan status/revision change disarms it.

- [ ] Write RED tests `LifecycleCommands_DoNotCallProvider`, `LifecycleCommands_AreWholeFormOnly`, `LifecycleCommands_CannotBypassOwnership`, plus punctuation/whitespace invariance, multiple-plan text ambiguity, pending-recommendation behavior, and in-flight provider cancellation. Use mock provider call counters and assert no new pending intent, command, or plan appears.
- [ ] Run focused tests and retain RED XML.
- [ ] Implement exact local routing before `CommanderIntentRouter.Classify` and provider calls. Use a single invariant normalizer; reject mixed or abbreviated text. Route status to a detached current-plan projection, preserving multiple-plan visibility. Lifecycle actions call only the validated planner control boundary with the host's trusted owner.
- [ ] Add minimal status, pause, resume, selected-plan cycle, and separate two-click cancel controls to the existing panel. Bind callbacks to current pipeline/generation and exact plan token. Clear armed cancel on any plan change, reset, or host replacement. Keep approval controls separate. Use bounded deterministic labels and messages.
- [ ] Use existing `StrategicAIApprovalBridge.ClearPending()` generation/cancellation behavior to invalidate pending or in-flight interpretation on lifecycle mutation; verify late results cannot restore pending state. Preserve conversation-only reset semantics and full match teardown safety.
- [ ] Run focused EditMode and PlayMode tests; record exact XML, counts, hashes, compiler state.

### Task 4: Real PlayMode lifecycle scenarios and 4D.1 gate

**Files:**
- Test: `Assets/Tests/PlayMode/CommanderPhase4D1PlayModeTests.cs`
- Evidence: `Docs/CommanderPhase4D/phase4d1-task-report.md`, `Docs/CommanderPhase4D/progress.md`, `Docs/CommanderPhase4D/requirements-matrix.md`

**Interfaces:** Reuse `CommanderPhase4C4PlayModeTests`' initialized host, real chat recommendation, and approval-button pattern. Do not replace it with a direct planner-only fixture.

- [ ] Write PlayMode RED for RangedReinforcement: approve through chat/UI, allow partial progress, capture plan identity/milestone/child goals/reservations, pause through actual player control, tick substantially, verify no strategic progression/new plan-owned commands/goals/reservations, resume same plan, complete, verify zero leaked reservation. Assert manual player actions remain executable while paused.
- [ ] Write PlayMode RED for DefensiveTurtle: real approval, partial progress, deliberate cancel confirmation, verify cancelled status, future milestone stop, reservation release, previously built assets remain, and unrelated manual commands survive. Add stale captured control against a replacement plan and reset-during-late-provider scenarios.
- [ ] Run only new PlayMode group for RED, save XML; implement only defects revealed within 4D.1 scope, then re-run focused GREEN once per source freeze.
- [ ] Run compiler/import/console check, independent Sol-class review against brief and actual diff, relevant existing Commander EditMode/PlayMode regressions, and static forbidden-reference/protected-hash audit. Any Critical/Important finding returns to the owning RED test and same production writer; never advance on an unresolved finding.
- [ ] Record each worker's task, expected output, RED, implementation, GREEN, review, and concerns. Update the requirements matrix and mark Gate 1–5 with evidence paths and hashes. Only then admit Phase 4D.2 source work.

## Self-review and execution rule

The plan covers 4D.1 controls, identity, ownership, pause/resume/cancel, offline routing, minimal UI, reset/async, and actual runtime scenarios. 4D.2 health, 4D.3 adaptation, and 4D.4 advisories require separate plans after the prior gate. The user requested continuous agent-orchestrated execution without routine approval pauses; the root agent will review this written plan and then dispatch one Sol-class production writer at a time, with a separate reasoning reviewer at each gate. No commit or worktree fork should silently absorb the uncommitted Phase 4C baseline.
