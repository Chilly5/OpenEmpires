# Commander Phase 4D.2 Strategic Plan Health Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player a truthful, deterministic, read-only health view of an approved strategic plan and prove that temporary waits recover without new approval.

**Architecture:** A planner-boundary capture copies typed plan, milestone, child-goal, owned-economy, and reservation evidence into a bounded immutable snapshot. Pure classification/rendering reports only causes the copied evidence proves; the host validates owner, plan identity, revision, and pipeline generation before displaying it. No event-maintained health cache or new gameplay authority is introduced.

**Tech Stack:** Unity 6000.5.9f1, C#, NUnit Unity EditMode/PlayMode, Unity MCP 10.2.0.

**Spec:** `Docs/superpowers/specs/2026-09-24-commander-phase4d2-design.md`; complete objective at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`.

## Global Constraints

- Start production work only after the root confirms the recorded Phase 4D.1 Gates 1–5 PASS against final 4D.1 source; reread current APIs and preserve the dirty, uncommitted user tree. `Docs/CommanderPhase4D/phase4d-source-baseline.json` remains the immutable Phase 4C reference.
- Only one production writer and exactly one Unity runner owner at a time. Finish each test job to terminal before starting another; do not overlap full suites or redo a completed run because a handle was lost.
- This phase is observation only. Preserve `StrategicApprovalLayer → StrategicDecisionPolicy → StrategicPlanner → CommanderGoalManager → existing deterministic RTS execution`; no provider/planner shortcut, control, intent, goal, reservation, command, simulation mutation, autonomous adaptation, or advisory emission from health.
- Do not add `TechnologyRush`, `SiegePreparation`, `NavalExpansion`, network messages, packages, credentials, cloud memory, wall-clock/frame/random decisions, or hidden enemy information. Do not alter the Phase 4C House reservation policy without an actual RED correctness failure and authority review.
- Preserve public and reflection-sensitive signatures. New snapshots contain copied primitive/value data only; explicitly serialized fields are bounded, deterministic, culture-invariant, and stable-order; unknown enum values fail closed.
- Before a protected-file edit, record frozen before-hash, exact diff, reason, authority impact, focused regression, after-hash, and independent review. Use `Docs/CommanderPhase4D/phase4d-protected-boundary-frozen.json`; do not silently rebaseline.
- Use meaningful behavioral RED before each change where practical. Record agent, task, expected output, RED, implementation, GREEN, review, and residuals in `Docs/CommanderPhase4D/phase4d2-task-report.md`; update `progress.md` and `requirements-matrix.md` only during execution, not as plan preparation.
- At the 4D.2 source freeze, run fresh complete EditMode **and** PlayMode suites: zero failed, skipped, or inconclusive and all new test IDs discovered. Do not infer current-source success from Phase 4C/4D.1 totals or historical jobs.

## Review Focus

1. An active plan with a milestone resource wait plus blocked child, or population pressure plus tactical House wood wait: preserve bounded typed secondary blockers under a deterministic primary ranking; do not call the plan failed or erase a concurrent wait. Pin with `PopulationAndResourceWait_PreservesBothEvidence` and `MilestoneResourceWaitAndBlockedChild_PreservesBothEvidence` in Task 1.
2. A plan waiting through ordinary gathering or queued production: classify an expected wait immediately, but never label it impossible from elapsed ticks. Pin with `ResourceWaiting_IsNotMisclassifiedAsFailure` and `QueuedProduction_IsExpectedWaiting` in Task 1.
3. A captured Plan A snapshot after reset/new pipeline creates Plan B with identical numeric ID/tick/revision: say evidence unavailable, not Plan B's health. Pin with `MatchingNumericIdentity_NewPipelineRejectsOldHealth` in Task 2.
4. An unknown/foreign plan ID or malformed enum in copied evidence: return no snapshot or Unknown without leaking another player's state or inventing a diagnosis. Pin with `ForeignPlanHealth_IsUnavailable` and `UnknownHealthEnum_FailsClosed` in Task 1.
5. A plan-linked child becomes terminal during pause or resource/population/queue state changes after capture without a revision change: the old snapshot remains immutable, but the host recaptures before every answer/render and pause does not accrue a blocker duration. Pin with `CapturedHealth_IsMutationIsolated`, `PausedBlockedGoal_DoesNotAccrueHealthDuration` in Task 1 and `UnchangedRevision_WorldChangeRefreshesHealth` in Task 2.

---

## File responsibilities and execution order

- Create `Assets/Scripts/AI/Commander/Strategic/StrategicPlanHealthSnapshot.cs` plus `.meta`: immutable DTOs, typed category, copied evidence, explicit deterministic serialization/render-safe bounds; no references to planner, simulation, Unity, goals, commands, or delegates.
- Create `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.Health.cs` plus `.meta`: read-only owner/plan-ID capture and typed classification using planner-owned `childGoalLinks`, retained terminal plans, owned context, and existing reservation queries. Keep `StrategicPlanner.cs` authority methods unchanged unless root approves a protected exception.
- Modify `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs`: bounded current-plan health summary beside existing status; no new control semantics. Modify `CommanderChatUI.Explanations.cs`: exact-form offline health questions and copied structured explanation. Modify `CommanderChatUI.cs` only if a small reset/dispatch hook is indispensable; preserve existing signatures and routing order.
- Create `Assets/Tests/EditMode/CommanderPhase4D2Tests.cs` plus `.meta`: projection/classification, isolation, deterministic and fog/owner/stale tests. Create `Assets/Tests/PlayMode/CommanderPhase4D2HostPlayModeTests.cs` and `CommanderPhase4D2RuntimePlayModeTests.cs` plus `.meta`: host and real approved-plan transition proof.
- Execution evidence goes to `Docs/CommanderPhase4D/phase4d2-task-report.md`, `progress.md`, and `requirements-matrix.md`, with durable job payload/XML and SHA-256 paths recorded. These are **execution deliverables**, not files edited in this planning task.

## Unity MCP job and evidence procedure for every task

1. The single designated runner reads `mcpforunity://instances` and `mcpforunity://editor/state`, selects the correct OpenEmpires instance if needed, and waits until `is_compiling == false`, no domain reload is pending, and `ready_for_tools == true`. Do not use a stale instance or start a second job.
2. After source edits, wait for Unity compilation/import, then call `read_console(action="get", types=["error"], count=100, format="detailed")`; record the exact current compiler-error count. A zero-test run or tool timeout is not GREEN.
3. Run one exact planned group using `run_tests(mode="EditMode", group_names=["OpenEmpires.Tests.CommanderPhase4D2Tests"], include_failed_tests=true, include_details=true)` or the corresponding `mode="PlayMode"` and `OpenEmpires.Tests.CommanderPhase4D2HostPlayModeTests` / `OpenEmpires.Tests.CommanderPhase4D2RuntimePlayModeTests`. Verify actual discovered fully qualified IDs from the current assembly; if zero tests are discovered, inspect assembly/category names, refresh only if Unity is stale, and rerun the corrected group. For an individual boundary test, use its discovered fully qualified ID in `test_names`.
4. Retain returned `job_id`; call `get_test_job(job_id="<same-id>", wait_timeout=60, include_failed_tests=true, include_details=true)` until terminal. Never substitute a later job or infer success from no output. Record exact discovered IDs, counts, skipped/inconclusive, failures, source hashes, job ID, and duration. Save genuine NUnit XML if supplied and SHA-256 it; if not supplied, save the complete terminal job payload and explicitly state “no native XML.” Preserve meaningful RED and final GREEN artifacts.
5. Run focused RED/GREEN first, then scoped affected Commander regression. Only after source freeze run complete EditMode and PlayMode (omit group/test filters), one at a time, with the same terminal-job, discovery, compiler, and artifact checks. Do not claim a suite PASS until all expected new IDs appear and zero failed/skipped/inconclusive are verified.

### Task 1: Detached health value and planner projection

**Files:** Create `Assets/Scripts/AI/Commander/Strategic/StrategicPlanHealthSnapshot.cs` and `StrategicPlanner.Health.cs` with `.meta`; test `Assets/Tests/EditMode/CommanderPhase4D2Tests.cs` with `.meta`. The reviewer can accept/reject this task without host/UI changes.

**Interfaces:** `StrategicPlanner.CapturePlanHealth(int trustedPlayerId, int planId)` builds the fresh owned context inside the planner boundary and returns `StrategicPlanHealthSnapshot` or `null` for unknown/foreign/disposed state. It does not accept a caller-supplied context that could be stale or inconsistent. `StrategicPlanHealthSnapshot` exposes immutable `PlayerId`, `PlanId`, `CreatedTick`, `Revision`, `ObservedTick`, `PlanType`, `PlanStatus`, `MilestoneId`, `MilestoneStatus`, `CompletedMilestones`, `TotalMilestones`, current milestone required/completed child counts, retained live child-status counts, missing-history count, `PrimaryHealthCategory`, bounded ordered `SecondaryHealthCategories`, copied plan-budget, *current milestone* requirement/deficit, plan-owned/global active reservation, population/queue, production, and child-status evidence, and `ToJson()` with explicitly selected fields. `StrategicPlanHealthCategory` is a closed enum with `Unknown`, `Healthy`, `Paused`, `WaitingForResources`, `WaitingForPopulation`, `WaitingForPrerequisite`, `WaitingForConstruction`, `WaitingForProduction`, `TemporarilyBlocked`, `Completed`, `Cancelled`, `Failed`; do not emit unsupported `NeedsPlayerDecision`, `WaitingForWorker`, or `WaitingForReservation` without direct typed proof. A pure classifier consumes only copied evidence. Primary ranking is terminal (`Completed`/`Cancelled`/`Failed`) > `Paused` > child `TemporarilyBlocked` > `WaitingForPopulation` > milestone/child `WaitingForResources` > `WaitingForPrerequisite` > `WaitingForProduction` > `WaitingForConstruction` > `Healthy`/`Unknown`, with child-ID tie-breaking; other proven facts form a bounded, ID-ordered secondary list. The capture must not call planner/goal `Tick`, `CommanderPlanner.Plan`, providers, or any mutating operation.

Minimal API shape (names above are the contract; secondary DTO constructors may remain internal):

```csharp
public StrategicPlanHealthSnapshot CapturePlanHealth(int trustedPlayerId, int planId);
public sealed class StrategicPlanHealthSnapshot
{
    public int PlayerId { get; }
    public int PlanId { get; }
    public int CreatedTick { get; }
    public int Revision { get; }
    public int ObservedTick { get; }
    public StrategicPlanHealthCategory PrimaryHealthCategory { get; }
    public IReadOnlyList<StrategicPlanHealthCategory> SecondaryHealthCategories { get; }
    public string ToJson();
}
```

- [ ] **Step 1: Write behavioral RED tests.** Use the existing EditMode simulation/plan fixtures; capture before and after plan/goal/reservation/command/tick counts. Add the objective's exact IDs: `PlanHealth_IsDeterministic`, `PlanHealth_DoesNotMutateSimulation`, `PlanHealth_DoesNotAdvancePlan`, `PlanHealth_DoesNotCallProvider`, `SameStateProducesSameHealthSnapshot`, `ResourceWaiting_IsNotMisclassifiedAsFailure`, `PopulationWaiting_IsStructured`, `PausedPlan_ReportsPaused`, `CompletedPlan_ReportsCompleted`, `CancelledPlan_ReportsCancelled`, `HealthSnapshot_DoesNotRetainSimulationObjects`, `PlanHealth_RemainsFogSafe`, `Reset_ClearsPlanHealthProjection`, `OldPlanHealth_DoesNotDescribeReplacementPlan`. Add the Task 1 Review Focus cases, `PrimaryAndSecondaryBlockers_AreRankedAndBounded`, `ArchivedGoalEviction_ReportsMissingHistory`, `PopulationMirror_UsesPlanUnitsRegistryAndQueuePredicates` (living versus garrisoned units, matching versus all queues, exactly-at-cap and above-cap), `MaxPopulation_DoesNotInventHouseRecovery`, `PlanBudget_DiffersFromMilestoneDeficitAndReservations`, unresolved-requirement Unknown, blocked-tick boundary/overflow cases, and `ReservationTotals_UnchangedByHealthCapture`. Test actual values/state, not a throw-only stub.

```csharp
// Core assertion pattern inside an NUnit test using the existing fixture's live planner and context.
int beforeTick = simulation.CurrentTick;
int beforeRevision = plan.Revision;
int beforeReservations = planner.GetReservationsForPlan(plan.StrategicPlanId).Count;
var first = planner.CapturePlanHealth(plan.OwnerPlayerId, plan.StrategicPlanId);
var second = planner.CapturePlanHealth(plan.OwnerPlayerId, plan.StrategicPlanId);
Assert.That(second.ToJson(), Is.EqualTo(first.ToJson()));
Assert.That(simulation.CurrentTick, Is.EqualTo(beforeTick));
Assert.That(plan.Revision, Is.EqualTo(beforeRevision));
Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId).Count, Is.EqualTo(beforeReservations));
```
- [ ] **Step 2: Run focused RED.** Use the runner procedure for the verified `CommanderPhase4D2Tests` group. Save failing assertions, job ID, source SHA-256, and XML or full terminal payload in the task report. Separate compile failure from behavioral RED.
- [ ] **Step 3: Implement the smallest snapshot DTO.** Constructors copy bounded lists of immutable scalar records; reject negative counts and unknown plan/resource/goal enums to `Unknown` or no snapshot. `ToJson()` serializes an explicit ordered projection using invariant integers and no `TypeNameHandling`; do not serialize live object graphs, status-reason strings, `OutcomeMessage`, or Unity references. Add a reflection/serialization test that checks fields and forbids object/reference-bearing members. Give current-milestone requirements an explicit known/unresolved flag, not a misleading zero.

```csharp
// Copy at construction; never expose the source collection or plan-derived object.
SecondaryHealthCategories = new ReadOnlyCollection<StrategicPlanHealthCategory>(
    new List<StrategicPlanHealthCategory>(orderedDistinctSecondaryCategories));
```
- [ ] **Step 4: Implement planner-only capture.** Check `trustedPlayerId == PlayerId`, reject unknown/foreign plan IDs before building fresh owned context from `goalManager.Simulation` and `goalManager` on the simulation-owning thread, then resolve exact `GetPlan(planId)` including retained terminal plans. Enumerate **current milestone `RequiredChildGoals`**, not only `childGoalLinks` (terminal links are removed); use `CompletedChildGoals` for historical completion and retained `goalManager.GetGoal(id)` only for finer current status. An evicted archived goal increments missing-history count and has Unknown fine status. Copy active reservations via `GetReservationsForPlan`. Keep whole-plan canonical budget separate from current milestone requirement/remaining deficit and separate from plan-owned versus global active reservation; use checked nonnegative arithmetic and existing available-amount rules. An unresolved canonical current-milestone requirement is Unknown, not zero. Copy owned economy/production from the freshly built `CommanderContext`, but mirror **exactly** `CommanderPlanner.PlanUnits`' population, living matching-unit registry, and training-queue predicates on the planner's existing owned simulation read boundary; `CommanderContextBuilder.Units` includes garrisoned units and is not an exact substitute. Resolve civilization unit type; count matching queued and all queued across owned non-destroyed buildings; calculate `remainingOrders = target - owned - matchingQueued`. Require unfinished current-milestone `EnsureUnitCountGoal` and `population + allQueued > cap || (remainingOrders > 0 && population + allQueued >= cap)` before asserting `WaitingForPopulation`; at max cap, preserve tactical `Blocked` and do not invent House recovery. Preserve simultaneous House wood/resource evidence. Follow the exact primary ranking in Interfaces, stable child-ID tie breaks, and bounded typed secondary facts; no string parsing. `Healthy` requires positive active/executing or completed-progress evidence and no wait/blocker; absence of a blocker alone is Unknown. Calculate blocked duration with widened arithmetic and `max(0, min(observedTick, PausedAtTick) - BlockedSinceTick)` while paused; after resume use 4D.1's shifted anchor. Reject/Unknown on overflow or invalid ordering. Missing evidence never becomes a fabricated cause.
- [ ] **Step 5: Run focused GREEN and review.** Verify every expected test ID, equality of repeated snapshots and JSON, captured-snapshot mutation isolation, zero state/command/reservation changes, and compiler errors zero. Record file hashes, job/XML or payload hashes, and reviewer findings. Do not change reservation, feasibility, goal execution, or plan lifecycle code to make health tests pass without root authority review.

### Task 2: Offline host health view, explanations, and stale reset

**Files:** Modify `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` and `CommanderChatUI.Explanations.cs`; modify `CommanderChatUI.cs` only for a necessary narrow hook; test `Assets/Tests/PlayMode/CommanderPhase4D2HostPlayModeTests.cs` with `.meta` and extend `CommanderPhase4D2Tests.cs` for pure rendering. The reviewer can reject host UX while preserving Task 1's core projection.

**Interfaces:** The host calls `strategicPipeline.StrategicPlanner.CapturePlanHealth(Conversation.PlayerId, selectedPlanId)` on Unity's owning thread immediately before every health answer and visible refresh. The planner creates its own fresh owned-state context; the UI never accesses `GameSimulation` for health. Revision is an identity/staleness check, **not** a cache freshness key: resource/population/queue state may change without a plan revision. A host-local validity check compares current pipeline reference, `runtimeGeneration`, trusted owner, plan ID/creation tick/revision, and nonterminal current selection before rendering as current. Terminal responses are explicit plan-ID historical observations only while that exact plan remains retained; no “current strategy” alias silently selects an archived plan.

- [ ] **Step 1: Write RED host tests.** Start the real initialized `CommanderChatUI` with a counting provider double. Test exact whole-form `why is the strategy paused?`, `why is the plan waiting?`, `what is blocking the current strategy?`, `did the strategy recover?`, and `why did the plan stop?`; mixed/hostile suffixes must not match. Assert zero provider calls, zero plan/goal/reservation/command changes, bounded truthful primary/secondary evidence, and explicit “evidence unavailable” when no matching current evidence exists. Add `MatchingNumericIdentity_NewPipelineRejectsOldHealth`, `UnchangedRevision_WorldChangeRefreshesHealth` for resource/population/queue changes, owner mismatch, plan revision change, reset/reinitialize, and destruction tests. Retain existing 4D.1 button and cancel-confirmation behavior.

- [ ] **Step 2: Run focused RED** for the verified `CommanderPhase4D2HostPlayModeTests` group; preserve the terminal job and specific assertion, not only a count.
- [ ] **Step 3: Implement minimal rendering/routing.** Reuse the exact whole-form normalization style in `CommanderChatUI.Explanations.cs`; dispatch before the provider in `SubmitMessageAsync`. Recapture on each answer/render even if plan revision did not change, then render copied typed primary and bounded secondary health evidence. Never parse `OutcomeMessage` or `StatusReason` or diagnose a hidden enemy, a worker shortage, or a unit-specific producer from generic evidence. Show a short health line next to existing selected-plan status. Do not create recommendation/advisory events or mutate memory with live DTO graphs; if recording text, use the existing bounded informational memory path only.

```csharp
// Read-only routing belongs before the existing provider branch; use actual host-private names.
if (TryHandleStrategicLifecycle(trimmed)) return null;
if (TryHandleExplanationQuery(trimmed)) return null;
if (TryHandlePlanHealthQuery(trimmed)) return null;
```
- [ ] **Step 4: Implement host invalidation.** Keep no core cache. If the host retains the most recent snapshot, clear it on `ResetConversation`, `Initialize`/`InitializeStrategic`, pipeline replacement, owner change, and destruction; revalidate pipeline/generation/token at render time. An old numeric identity from another match must never be considered current. Preserve existing public/reflection-sensitive signatures; add an overload rather than alter one.
- [ ] **Step 5: Run focused GREEN** and affected Phase 4C.2 explanation plus Phase 4D.1 host groups under the single runner. Record discovered IDs, no-provider counters, exact jobs, source hashes, compiler zero, and XML/payload evidence. Obtain independent review of stale, ownership, and authority boundaries before Task 3.

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

## Plan self-review and handoff

- Coverage: Task 1 owns immutable typed projection, deterministic stall semantics, fog/owner/resource evidence, terminal capture, and mutation isolation; Task 2 owns offline host explanation/status and stale reset; Task 3 owns real wait/recovery proof and all sub-phase/full-suite gates. No 4D.3 adaptation or 4D.4 advisory implementation is included.
- Required test IDs from the Phase 4D.2 objective are assigned in Task 1; the real Scenario C is assigned in Task 3. Review Focus cases each have a named test in the owning task. No production file is modified by creating this plan.
- Root decisions before Task 1 coding: health capture builds fresh owned context inside the planner boundary and takes only trusted player ID plus plan ID; the host does not supply context or access simulation. Completed/archived child goal fine status may be unavailable after eviction and must be Unknown while milestone completed counts remain authoritative. Phase 4D.1 scoped Gates 1–5 are PASS at this plan freeze, including current-source affected EditMode 70/70 and combined affected PlayMode 48/48; later full suites remain mandatory. None of these decisions permits a new execution path.
