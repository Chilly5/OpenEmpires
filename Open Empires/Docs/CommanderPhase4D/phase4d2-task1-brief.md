# Phase 4D.2 Task 1 brief

Read this with the approved design spec `Docs/superpowers/specs/2026-09-24-commander-phase4d2-design.md`. Do not implement Tasks 2–3.

## Global Constraints

- Start production work only after the root confirms the recorded Phase 4D.1 Gates 1–5 PASS against final 4D.1 source; reread current APIs and preserve the dirty, uncommitted user tree. `Docs/CommanderPhase4D/phase4d-source-baseline.json` remains the immutable Phase 4C reference.
- Only one production writer and exactly one Unity runner owner at a time. Finish each test job to terminal before starting another; do not overlap full suites or redo a completed run because a handle was lost.
- This phase is observation only. Preserve `StrategicApprovalLayer → StrategicDecisionPolicy → StrategicPlanner → CommanderGoalManager → existing deterministic RTS execution`; no provider/planner shortcut, control, intent, goal, reservation, command, simulation mutation, autonomous adaptation, or advisory emission from health.
- Do not add `TechnologyRush`, `SiegePreparation`, `NavalExpansion`, network messages, packages, credentials, cloud memory, wall-clock/frame/random decisions, or hidden enemy information. Do not alter the Phase 4C House reservation policy without an actual RED correctness failure and authority review.
- Preserve public and reflection-sensitive signatures. New snapshots contain copied primitive/value data only; explicitly serialized fields are bounded, deterministic, culture-invariant, and stable-order; unknown enum values fail closed.
- Before a protected-file edit, record frozen before-hash, exact diff, reason, authority impact, focused regression, after-hash, and independent review. Use `Docs/CommanderPhase4D/phase4d-protected-boundary-frozen.json`; do not silently rebaseline.
- Use meaningful behavioral RED before each change where practical. Record agent, task, expected output, RED, implementation, GREEN, review, and residuals in `Docs/CommanderPhase4D/phase4d2-task-report.md`; update `progress.md` and `requirements-matrix.md` only during execution, not as plan preparation.
- At the 4D.2 source freeze, run fresh complete EditMode **and** PlayMode suites: zero failed, skipped, or inconclusive and all new test IDs discovered. Do not infer current-source success from Phase 4C/4D.1 totals or historical jobs.

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

