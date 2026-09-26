# Phase 4D.3 Task 1 — replacement admission preflight

Status: DONE_WITH_CONCERNS after fix round 3. This is the Task 1 handoff, not the Phase 4D.3 gate. Independent rereview remains with the root. Earlier source diffs and residuals are historical; the latest fix-round section supersedes them.

## Scope and provenance

- Baseline HEAD: `4ae6269d3a3d1c16f6160813f3a06c66456b5bf5`.
- Protected source: `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs`.
- SHA-256 before: `A50B64D49B4D2FB5CBB43ED2835E0890047DE28D46BC6C4ABAA14609EB1E5902`.
- SHA-256 after: `22FB9D4DE6BC139B7FBA2F5334413064A87C600E69A0FDDCB5F2CEACECE2B224`.
- Test file SHA-256 after: `065BFCA06276C7429BAE3F92EBCC35FEBDEC19E170C4B4E56110FCA257FBD552`.
- Changed only the protected planner and `Assets/Tests/EditMode/CommanderPhase4D1Tests.cs`, plus this report. Existing dirty 4D.2 work and unrelated assets were left intact. No staging or commit.

## Test-first evidence

Added five exact behavioral IDs to the 4D1 EditMode class:

1. `AIRecommendationCannotAutoReplaceActivePlan`
2. `PlayerApprovedReplacementUsesExistingAuthorityPath`
3. `ReplacementReleasesOldReservationsCorrectly`
4. `ReplacementLateAdmissionFailure_PreservesOldPlan`
5. `MultipleConflicts_PreflightAllBeforeCancellation`

The rejected-admission cases capture old status, revision, child IDs and nonterminal child states, and active reservation IDs before submission, then compare after. The late case installs a deterministic custom empty-milestone DefensivePreparation template in the *same* planner as the old CavalryPressure plan. The multi-conflict case starts compatible EconomicExpansion and DefensivePreparation plans, raises the later plan's revision to `int.MaxValue`, and submits an incompatible AttackPreparation player override.

Initial exact `test_names` Unity MCP job `eb25492548a64ebebc53c6efa8dab372` discovered **0** tests; it is not counted as RED. After script refresh, group job `8fb4c279803743b3a8bde99af1923c72` discovered 44/44 4D1 tests and failed the two intended new behaviors against the original planner hash. Native terminal payload: `status=failed`, `progress.completed=44`, `progress.total=44`, `result=null`; `failures_so_far` named `MultipleConflicts_PreflightAllBeforeCancellation` (`Expected: Active; But was: Cancelled`) and `ReplacementLateAdmissionFailure_PreservesOldPlan` (`System.InvalidOperationException: A strategic plan requires at least one milestone`). This is behavioral RED, not a compiler or discovery failure. A transient MCP receive timeout was followed by a terminal poll of the same job ID; no overlapping job was started.

## Protected patch and reason

The original planner cancelled conflicting plans before the active-cap check, budget calculation, worker fit, and `ActivateFirstMilestone()`. The patch validates the incoming plan's initial state and nonempty/non-null/contiguously ordered pending milestones; scans **every** cancellable conflict with `CancelPlan`'s `1 + active reservation count` revision budget; checks projected post-cancel active count; resolves canonical budget and worker fit; and converts a preflight exception to a rejected submission. Only then does the original `CancelPlan` loop run. Existing transition, authority, release, goal cleanup, and new-plan start paths remain in place.

Exact protected source diff (`git diff -- StrategicPlanner.cs`):

```diff
@@ -191,40 +191,70 @@ namespace OpenEmpires
-            for (int i = 0; i < conflictingPlans.Count; i++)
+            // Complete every fallible admission check before cancelling any existing plan.
+            try
             {
-                StrategicPlan conflicting = conflictingPlans[i];
-                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
+                if (plan.Status != StrategicPlanStatus.Created || plan.Revision != 0
+                    || plan.Milestones.Count == 0)
+                    throw new InvalidOperationException("The incoming plan has no valid initial milestone.");
+                for (int i = 0; i < plan.Milestones.Count; i++)
                 {
-                    if (!CancelPlan(conflicting.StrategicPlanId))
+                    StrategicMilestone stage = plan.Milestones[i];
+                    if (stage == null || stage.OrderIndex != i
+                        || stage.Status != StrategicMilestoneStatus.Pending)
+                        throw new InvalidOperationException("The incoming plan has invalid milestone order or status.");
+                }
+
+                int projectedActiveCount = activePlans.Count;
+                for (int i = 0; i < conflictingPlans.Count; i++)
+                {
+                    StrategicPlan conflicting = conflictingPlans[i];
+                    if (!isPlayerOverride && !(isEmergency
+                        && conflicting.Authority < StrategicPlanAuthority.Emergency)) continue;
+                    if (!CanAdvanceRevision(conflicting, 1L + CountActiveReservations(conflicting)))
                         return RejectIntent(intent, StrategicIntentValidationError.CommitmentBlocked,
                             "The conflicting plan could not be cancelled safely.");
+                    projectedActiveCount--;
                 }
+                if (projectedActiveCount >= MaxActivePlans)
+                    return RejectIntent(intent, StrategicIntentValidationError.ActivePlanLimitReached,
+                        $"The active strategic plan limit of {MaxActivePlans} has been reached.");
+
+                if (plan is DefensiveTurtlePlan turtle)
+                    turtle.TowerTargetTotal = CountCompletedTowers() + DefensiveTurtlePlan.TowerCount;
+                if (plan is CavalryPressurePlan || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan
+                    || plan is RangedReinforcementPlan || plan is DefensiveTurtlePlan)
+                {
+                    var totals = new SortedDictionary<ResourceType, int>();
+                    foreach (StrategicMilestone stage in plan.Milestones)
+                        foreach (StrategicResourceRequirement cost in ComputeRequirements(stage,
+                            plan as DefensiveTurtlePlan))
+                            totals[cost.ResourceType] = (totals.TryGetValue(cost.ResourceType, out int amount) ? amount : 0) + cost.Amount;
+                    var budget = new List<StrategicResourceRequirement>();
+                    foreach (var total in totals) budget.Add(new StrategicResourceRequirement(total.Key, total.Value));
+                    plan.SetBudgetFromSimulation(budget);
+                }
+                FitEconomyToAvailableWorkers(plan);
             }
-
-            if (activePlans.Count >= MaxActivePlans)
+            catch (Exception error)
             {
-                return RejectIntent(intent, StrategicIntentValidationError.ActivePlanLimitReached,
-                    $"The active strategic plan limit of {MaxActivePlans} has been reached.");
+                return RejectIntent(intent, StrategicIntentValidationError.TemplateCreationFailed,
+                    $"Strategic plan admission failed: {error.Message}");
             }
 
-            if (plan is DefensiveTurtlePlan turtle)
-                turtle.TowerTargetTotal = CountCompletedTowers() + DefensiveTurtlePlan.TowerCount;
-            if (plan is CavalryPressurePlan || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan
-                || plan is RangedReinforcementPlan || plan is DefensiveTurtlePlan)
+            for (int i = 0; i < conflictingPlans.Count; i++)
             {
-                var totals = new SortedDictionary<ResourceType, int>();
-                foreach (StrategicMilestone stage in plan.Milestones)
-                    foreach (StrategicResourceRequirement cost in ComputeRequirements(stage,
-                        plan as DefensiveTurtlePlan))
-                        totals[cost.ResourceType] = (totals.TryGetValue(cost.ResourceType, out int amount) ? amount : 0) + cost.Amount;
-                var budget = new List<StrategicResourceRequirement>();
-                foreach (var total in totals) budget.Add(new StrategicResourceRequirement(total.Key, total.Value));
-                plan.SetBudgetFromSimulation(budget);
+                StrategicPlan conflicting = conflictingPlans[i];
+                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
+                {
+                    if (!CancelPlan(conflicting.StrategicPlanId))
+                        return RejectIntent(intent, StrategicIntentValidationError.CommitmentBlocked,
+                            "The conflicting plan could not be cancelled safely.");
+                }
             }
+
             plan.Authority = incomingAuthority;
             plan.Source = intent.Source;
-            FitEconomyToAvailableWorkers(plan);
             plan.StrategicPlanId = nextPlanId++;
```

## GREEN and affected regression

| Unity MCP job | Filter | Native terminal evidence |
|---|---|---|
| `3e31d4a0d20f4a0f9194d526ef3086ea` | `OpenEmpires.Tests.CommanderPhase4D1Tests` | `status=succeeded`, `summary.total=44`, `passed=44`, `failed=0`, `skipped=0`, `resultState=Passed`; all five new IDs present and Passed. |
| `337723bafa8e49c4bab8b3ddc6d22cc8` | `CommanderPhase3C5StrategicDecisionTests`, `CommanderPhase3C1StrategicPlanTests`, `CommanderPhase4B2Tests` | `status=succeeded`, `progress.completed=93/93`, `summary.total=93`, `passed=93`, `failed=0`, `skipped=0`, `resultState=Passed`. |

Unity MCP returned terminal JSON payloads, not native XML paths or bytes, for these jobs; therefore no XML SHA-256 can honestly be given. Terminal summaries and job IDs above are the available native evidence. The affected run temporarily lost the bridge after a receive timeout; the same job ID was recovered to terminal, without starting a replacement job. After compilation and regressions, `read_console(types=[error])` returned 0 entries. `git diff --check` reported no diff errors.

## Residuals and handoff

- This is preflight safety, not a transaction. A live event subscriber can reenter or mutate planner state during `CancelPlan` callbacks after preflight; the later cancellation could then fail, leaving a partial replacement. No rollback or new submission path was added, per scope. Independent review should judge whether this residual is acceptable for the Task 1 gate.
- The custom malformed template test covers empty milestones. Null/order validation is implemented but not separately fault-injected here; `AddMilestone` normally enforces order and non-null at construction.
- Focused and adjacent EditMode groups were run. Complete EditMode/PlayMode suites and Phase 4D.3 gate are root/Task 4 work, not claimed here.

## Independent review fix round 1 — 2026-09-25

The reviewer identified a concrete inter-cancellation event hole in the first revision. `CancelPlan` published the first old plan's status before the loop attempted a later conflicting plan. A callback could use the public reservation API to advance the later plan's revision past the prechecked cancellation budget. This violated the binding spec, so the earlier residual is **not** treated as accepted.

Behavior-first addition: `OpenEmpires.Tests.CommanderPhase4D1Tests.MultipleConflicts_ReentrantRevisionCannotCausePartialCancellation`. It starts two compatible CavalryPressure plans with active reservations, sets the later plan's revision to exactly the preflight budget boundary, and installs a real `PlanStatusChanged` subscriber that attempts to edit the later plan's reservation when the first plan is cancelled. It checks callback execution and either complete replacement/release with the reentrant reservation edit blocked or rejection with the earlier plan/status/revision/children/reservations intact. Against first-revision planner SHA-256 `22FB9D4DE6BC139B7FBA2F5334413064A87C600E69A0FDDCB5F2CEACECE2B224`, Unity MCP EditMode job `50f916ac55d3472d824d1fad781f95b0` discovered 45 tests and failed only this ID: `Expected: Active; But was: Cancelled`. Native terminal payload had `status=failed`, `progress.completed=45`, `progress.total=45`, `result=null`, and the failure in `failures_so_far`. A transient receive timeout was resolved by polling the same job ID.

Fix: after all read-only admission passes, the planner now commits *all* selected old-plan cancellation status/revision/active-list/archive anchors before any cleanup or event callback, then runs the existing skip-child-goal-reservation-release and publish sequence for each. The public single-plan `CancelPlan` uses the same begin/finish helpers, preserving its cleanup. Public reservation update/release now rejects terminal plans, so a cancellation callback cannot revise a later already-anchored old plan. The preflight catch no longer wraps `RejectIntent`; it stores the error and invokes rejection once outside `try`, closing the minor double-notification path for a throwing rejection subscriber. No public submission, rollback, provider, or authority path was added.

Protected source SHA-256 now: `F93581986CAC563260DFDA308C40CA6333123C325374C1D75783EEA5A98D0535`. Test file SHA-256 now: `D68542F64DE4A643A01FED81525B71EC54625892A504660F621742429922C977`. Original baseline source SHA-256 remains `A50B64D49B4D2FB5CBB43ED2835E0890047DE28D46BC6C4ABAA14609EB1E5902`.

The exact **current cumulative protected-source diff** from baseline (`git diff -- Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs`) is:

```diff
@@ -191,40 +191,87 @@ namespace OpenEmpires
                 }
             }
 
-            for (int i = 0; i < conflictingPlans.Count; i++)
+            // Complete every fallible admission check before cancelling any existing plan.
+            StrategicIntentValidationError preflightError = StrategicIntentValidationError.None;
+            string preflightReason = string.Empty;
+            try
             {
-                StrategicPlan conflicting = conflictingPlans[i];
-                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
+                if (plan.Status != StrategicPlanStatus.Created || plan.Revision != 0
+                    || plan.Milestones.Count == 0)
+                    throw new InvalidOperationException("The incoming plan has no valid initial milestone.");
+                for (int i = 0; i < plan.Milestones.Count; i++)
                 {
-                    if (!CancelPlan(conflicting.StrategicPlanId))
-                        return RejectIntent(intent, StrategicIntentValidationError.CommitmentBlocked,
-                            "The conflicting plan could not be cancelled safely.");
+                    StrategicMilestone stage = plan.Milestones[i];
+                    if (stage == null || stage.OrderIndex != i
+                        || stage.Status != StrategicMilestoneStatus.Pending)
+                        throw new InvalidOperationException("The incoming plan has invalid milestone order or status.");
                 }
-            }
 
-            if (activePlans.Count >= MaxActivePlans)
+                int projectedActiveCount = activePlans.Count;
+                for (int i = 0; i < conflictingPlans.Count; i++)
+                {
+                    StrategicPlan conflicting = conflictingPlans[i];
+                    if (!isPlayerOverride && !(isEmergency
+                        && conflicting.Authority < StrategicPlanAuthority.Emergency)) continue;
+                    if (!CanAdvanceRevision(conflicting, 1L + CountActiveReservations(conflicting)))
+                    {
+                        preflightError = StrategicIntentValidationError.CommitmentBlocked;
+                        preflightReason = "The conflicting plan could not be cancelled safely.";
+                        break;
+                    }
+                    projectedActiveCount--;
+                }
+                if (preflightError == StrategicIntentValidationError.None
+                    && projectedActiveCount >= MaxActivePlans)
+                {
+                    preflightError = StrategicIntentValidationError.ActivePlanLimitReached;
+                    preflightReason = $"The active strategic plan limit of {MaxActivePlans} has been reached.";
+                }

+                if (preflightError == StrategicIntentValidationError.None)
+                {
+                    if (plan is DefensiveTurtlePlan turtle)
+                        turtle.TowerTargetTotal = CountCompletedTowers() + DefensiveTurtlePlan.TowerCount;
+                    if (plan is CavalryPressurePlan || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan
+                        || plan is RangedReinforcementPlan || plan is DefensiveTurtlePlan)
+                    {
+                        var totals = new SortedDictionary<ResourceType, int>();
+                        foreach (StrategicMilestone stage in plan.Milestones)
+                            foreach (StrategicResourceRequirement cost in ComputeRequirements(stage,
+                                plan as DefensiveTurtlePlan))
+                                totals[cost.ResourceType] = (totals.TryGetValue(cost.ResourceType, out int amount) ? amount : 0) + cost.Amount;
+                        var budget = new List<StrategicResourceRequirement>();
+                        foreach (var total in totals) budget.Add(new StrategicResourceRequirement(total.Key, total.Value));
+                        plan.SetBudgetFromSimulation(budget);
+                    }
+                    FitEconomyToAvailableWorkers(plan);
+                }
+            }
+            catch (Exception error)
             {
-                return RejectIntent(intent, StrategicIntentValidationError.ActivePlanLimitReached,
-                    $"The active strategic plan limit of {MaxActivePlans} has been reached.");
+                preflightError = StrategicIntentValidationError.TemplateCreationFailed;
+                preflightReason = $"Strategic plan admission failed: {error.Message}";
             }
+            if (preflightError != StrategicIntentValidationError.None)
+                return RejectIntent(intent, preflightError, preflightReason);
 
-            if (plan is DefensiveTurtlePlan turtle)
-                turtle.TowerTargetTotal = CountCompletedTowers() + DefensiveTurtlePlan.TowerCount;
-            if (plan is CavalryPressurePlan || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan
-                || plan is RangedReinforcementPlan || plan is DefensiveTurtlePlan)
+            var cancellations = new List<StrategicPlan>();
+            for (int i = 0; i < conflictingPlans.Count; i++)
             {
-                var totals = new SortedDictionary<ResourceType, int>();
-                foreach (StrategicMilestone stage in plan.Milestones)
-                    foreach (StrategicResourceRequirement cost in ComputeRequirements(stage,
-                        plan as DefensiveTurtlePlan))
-                        totals[cost.ResourceType] = (totals.TryGetValue(cost.ResourceType, out int amount) ? amount : 0) + cost.Amount;
-                var budget = new List<StrategicResourceRequirement>();
-                foreach (var total in totals) budget.Add(new StrategicResourceRequirement(total.Key, total.Value));
-                plan.SetBudgetFromSimulation(budget);
+                StrategicPlan conflicting = conflictingPlans[i];
+                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
+                {
+                    cancellations.Add(conflicting);
+                }
             }
+            // Commit all cancellation states before cleanup publishes any reentrant event.
+            for (int i = 0; i < cancellations.Count; i++)
+                BeginPlanCancellation(cancellations[i]);
+            for (int i = 0; i < cancellations.Count; i++)
+                FinishPlanCancellation(cancellations[i]);
+
             plan.Authority = incomingAuthority;
             plan.Source = intent.Source;
-            FitEconomyToAvailableWorkers(plan);
             plan.StrategicPlanId = nextPlanId++;
             plan.CreatedTick = goalManager.CurrentTick;
             plan.InitializeRevision();
@@ -287,11 +334,22 @@ namespace OpenEmpires
             StrategicPlan plan = GetPlan(strategicPlanId);
             if (plan == null || plan.IsTerminal) return false;
             if (!CanAdvanceRevision(plan, 1L + CountActiveReservations(plan))) return false;
+            BeginPlanCancellation(plan);
+            FinishPlanCancellation(plan);
+            return true;
+        }
+
+        private void BeginPlanCancellation(StrategicPlan plan)
+        {
             plan.Status = StrategicPlanStatus.Cancelled;
             plan.AdvanceRevision();
             plan.OutcomeMessage = plan.CancellationMessage;
             activePlans.Remove(plan);
             ArchivePlan(plan);
+        }
+
+        private void FinishPlanCancellation(StrategicPlan plan)
+        {
             SkipUnfinishedMilestones(plan);
             CancelOwnedNonTerminalGoals(plan);
             reservationManager.ReleasePlanReservations(plan.StrategicPlanId, cancelled: true);
@@ -299,7 +357,6 @@ namespace OpenEmpires
             Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} cancelled.");
             PublishPlanStatus(plan);
             ResponseGenerated?.Invoke(plan, plan.OutcomeMessage);
-            return true;
         }
 
         public void Dispose()
@@ -407,7 +464,7 @@ namespace OpenEmpires
                 StrategicResourceReservation reservation = reservationManager.Reservations[i];
                 if (reservation.ReservationId != reservationId) continue;
                 StrategicPlan plan = GetPlan(reservation.PlanId);
-                if (plan == null || plan.Status == StrategicPlanStatus.Paused
+                if (plan == null || plan.IsTerminal || plan.Status == StrategicPlanStatus.Paused
                     || !CanAdvanceRevision(plan, 1)) return false;
                 bool updated = reservationManager.UpdateReservationAmount(reservationId, newAmount);
                 if (updated && newAmount > 0) plan.AdvanceRevision();
@@ -424,7 +481,7 @@ namespace OpenEmpires
                 StrategicResourceReservation reservation = reservationManager.Reservations[i];
                 if (reservation.ReservationId != reservationId) continue;
                 StrategicPlan plan = GetPlan(reservation.PlanId);
-                return plan != null && plan.Status != StrategicPlanStatus.Paused
+                return plan != null && !plan.IsTerminal && plan.Status != StrategicPlanStatus.Paused
                     && CanAdvanceRevision(plan, 1)
                     && reservationManager.ReleaseReservation(reservationId, cancelled);
             }
```

Final focused Unity MCP job `4caa271218a7403b9de13d008bcd686c`: 45/45 passed, 0 failed/skipped. An earlier GREEN job `8d6304ae70f547db919a8dfb83492e83` also passed 45/45 before tightening the test's terminal-reservation assertion. Affected decision/strategic-plan/approval regression job `ad16e3a9e436402aa0782d6a179743af`: 93/93 passed, 0 failed/skipped. All jobs used the same live Open Empires Editor, sequentially, to terminal; transient MCP receive/bridge timeouts were polled against the same IDs. No native XML bytes/path were returned, so no XML hash is available. Final Unity error-console query returned 0 entries; `git diff --check` returned no diff errors.

Residual: the two-phase anchor prevents the reviewed revision-budget failure through public event callbacks, but this is still not an all-or-nothing transaction against arbitrary throwing external event subscribers or arbitrary reentrant planner calls. The root's independent rereview must decide whether further containment is required before accepting Task 1. No full EditMode/PlayMode suite or runtime-game proof is claimed here.

## Independent review fix round 2 — 2026-09-25

The next scoped review identified a post-preflight exception hole: the two-phase cancellation anchor marked all old plans cancelled, but a throwing `GoalStatusChanged`, `ReservationReleased`, or `PlanStatusChanged` subscriber stopped the first cleanup. The reservation manager removes/archives each reservation **before** firing `ReservationReleased`; `CommanderGoalManager.CancelGoal` marks/releases/archives a goal **before** its status event. Those observed orderings permit per-item cleanup continuation without falsely treating a failed observer notification as a failed release.

New behavior-first test: `MultipleConflicts_ThrowingCancellationSubscriberDoesNotStrandCleanup`, parameterized over goal, reservation, and plan-status events. It starts two compatible old plans with held reservations, throws once from a real subscriber during replacement, and asserts both plans have terminal goals, zero active reservations, cancelled intent status, and no unauthorized incoming active plan. Against fix-round-1 planner SHA-256 `F93581986CAC563260DFDA308C40CA6333123C325374C1D75783EEA5A98D0535`, Unity MCP EditMode RED job `c4cc15b639654d6995fb4e2bdd943cba` discovered 47 tests and failed both first cases: active reservation remained for reservation throw; cleanup assertion failed for goal throw. After per-item continuation, job `aab973beb9a8473cb97f3135cb53717e` passed 47/47. Adding the plan-status case produced an additional meaningful RED in job `3da381da3e574f89b201b85430402268`: 48 discovered, only `MultipleConflicts_ThrowingCancellationSubscriberDoesNotStrandCleanup(2)` failed (`Expected: Cancelled; But was: Active`) for source intent status.

Fix-round-2 protected edit: `FinishPlanCancellation` now catches the first exception from each goal cancellation, reservation release, plan-status notification, and response notification, while continuing all child/reservation cleanup and every selected old plan. It rethrows the first captured exception with `ExceptionDispatchInfo`, preserving failure visibility and original stack. `PublishPlanStatus` completes the intent-status update even if `PlanStatusChanged` throws, then rethrows that observer error. This does **not** suppress arbitrary external exceptions or add rollback/provider authority. The exact incremental protected diff from fix-round-1 source is:

```diff
@@ imports
+using System.Runtime.ExceptionServices;
@@ replacement cancellation loop
             for (int i = 0; i < cancellations.Count; i++)
                 BeginPlanCancellation(cancellations[i]);
+            Exception cancellationError = null;
             for (int i = 0; i < cancellations.Count; i++)
-                FinishPlanCancellation(cancellations[i]);
+                FinishPlanCancellation(cancellations[i], ref cancellationError);
+            if (cancellationError != null) ExceptionDispatchInfo.Capture(cancellationError).Throw();
@@ public CancelPlan
             BeginPlanCancellation(plan);
-            FinishPlanCancellation(plan);
+            Exception cancellationError = null;
+            FinishPlanCancellation(plan, ref cancellationError);
+            if (cancellationError != null) ExceptionDispatchInfo.Capture(cancellationError).Throw();
@@ finish cancellation
-        private void FinishPlanCancellation(StrategicPlan plan)
+        private void FinishPlanCancellation(StrategicPlan plan, ref Exception cancellationError)
         {
             SkipUnfinishedMilestones(plan);
-            CancelOwnedNonTerminalGoals(plan);
-            reservationManager.ReleasePlanReservations(plan.StrategicPlanId, cancelled: true);
+            for (int i = 0; i < plan.ChildGoalIds.Count; i++)
+            {
+                int goalId = plan.ChildGoalIds[i];
+                CommanderGoal goal = goalManager.GetGoal(goalId);
+                try
+                {
+                    if (goal != null && !goal.IsTerminal) goalManager.CancelGoal(goalId);
+                }
+                catch (Exception error) { if (cancellationError == null) cancellationError = error; }
+                finally { childGoalLinks.Remove(goalId); }
+            }
+            var reservationIds = new List<int>();
+            for (int i = 0; i < reservationManager.Reservations.Count; i++)
+            {
+                StrategicResourceReservation reservation = reservationManager.Reservations[i];
+                if (reservation.PlanId == plan.StrategicPlanId)
+                    reservationIds.Add(reservation.ReservationId);
+            }
+            for (int i = 0; i < reservationIds.Count; i++)
+            {
+                try { reservationManager.ReleaseReservation(reservationIds[i], cancelled: true); }
+                catch (Exception error) { if (cancellationError == null) cancellationError = error; }
+            }
             ClearDeferredTerminalEvents(plan);
             Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} cancelled.");
-            PublishPlanStatus(plan);
-            ResponseGenerated?.Invoke(plan, plan.OutcomeMessage);
+            try { PublishPlanStatus(plan); }
+            catch (Exception error) { if (cancellationError == null) cancellationError = error; }
+            try { ResponseGenerated?.Invoke(plan, plan.OutcomeMessage); }
+            catch (Exception error) { if (cancellationError == null) cancellationError = error; }
@@ PublishPlanStatus
-            PlanStatusChanged?.Invoke(plan);
-            if (!intentsByPlanId.TryGetValue(plan.StrategicPlanId,
-                out StrategicIntent intent)) return;
-            switch (plan.Status)
+            Exception observerError = null;
+            try { PlanStatusChanged?.Invoke(plan); }
+            catch (Exception error) { observerError = error; }
+            if (intentsByPlanId.TryGetValue(plan.StrategicPlanId,
+                out StrategicIntent intent))
             {
-                case StrategicPlanStatus.Completed:
-                    SetIntentStatus(intent, StrategicIntentStatus.Completed, plan.OutcomeMessage);
-                    break;
-                case StrategicPlanStatus.Failed:
-                    SetIntentStatus(intent, StrategicIntentStatus.Failed, plan.OutcomeMessage);
-                    break;
-                case StrategicPlanStatus.Cancelled:
-                    SetIntentStatus(intent, StrategicIntentStatus.Cancelled, plan.OutcomeMessage);
-                    break;
+                switch (plan.Status)
+                {
+                    case StrategicPlanStatus.Completed:
+                        SetIntentStatus(intent, StrategicIntentStatus.Completed, plan.OutcomeMessage);
+                        break;
+                    case StrategicPlanStatus.Failed:
+                        SetIntentStatus(intent, StrategicIntentStatus.Failed, plan.OutcomeMessage);
+                        break;
+                    case StrategicPlanStatus.Cancelled:
+                        SetIntentStatus(intent, StrategicIntentStatus.Cancelled, plan.OutcomeMessage);
+                        break;
+                }
             }
+            if (observerError != null) ExceptionDispatchInfo.Capture(observerError).Throw();
```

Latest planner SHA-256 `23B96F77FBAA080C54A86AB1186470AE6691096BD646A5B7C0C0198326F17FC4`; latest test SHA-256 `211A734A3A268F57CFA726558CB32F1415F667D9041D444345F61051A79FD9B2`. The current cumulative baseline-to-latest diff is the preceding historical baseline-to-fix-1 diff plus the exact fix-2 delta above. Final focused GREEN Unity MCP job `8dc3e8917d9e47b9b2521160cf855f75`: 48/48 passed, 0 failed/skipped. Affected decision/strategic-plan/approval regression job `3f17eb25f4b741cabd4722437dd2738c`: 93/93 passed, 0 failed/skipped. Jobs ran sequentially to terminal; native MCP terminal payloads supplied job IDs/counts but no XML bytes/path, so no XML hash is available. Final Unity console filter `error CS` returned 0 entries; `git diff --check` found no diff errors.

Residual requiring gate judgment: if a post-preflight observer throws, cleanup finishes for all old plans and the original exception is rethrown **before the incoming plan starts**. Thus no cancelled old plan retains a child goal or active reservation, and no unauthorized incoming plan is created, but this is not a successful replacement and the old plans are gone. A strict all-or-nothing guarantee against arbitrary external observer exceptions would require broader event isolation/transaction design outside this bounded patch. Do not call that scenario an atomic replacement. Independent reviewer/root must accept or reject this explicit residual; complete suites and runtime proof remain separate.

## Independent review fix round 3 — 2026-09-25

The scoped reviewer identified a deeper child-goal leak under a throwing observer: `CommanderGoalManager.CancelGoal` changes the child to terminal, releases its worker authority, and archives it, then fires `GoalStatusChanged` before `CleanupTerminalGoals`. If that subscriber throws on the **last** cancelled child, no later cancellation sweeps the terminal child out of `ActiveGoals`; it continues to count against `MaxActiveGoals`. The prior test only checked `GetGoal.IsTerminal`, which did not observe this manager capacity.

Test-first refinement of `MultipleConflicts_ThrowingCancellationSubscriberDoesNotStrandCleanup(1)`: the injected `GoalStatusChanged` throw targets the last old-plan child, captures whether `GoalEventPublished(GoalCancelled)` still runs, and asserts every old child ID is absent from `goals.ActiveGoals` plus `goals.ActiveGoals` is empty for the controlled fixture. An initial broader assertion passed in Unity MCP job `d88d99c72a2045cc83fceb79d0f541bf` (48/48), because a later successful `CancelGoal` swept the earlier leaked terminal child; this was **not** counted as RED. The corrected last-child fixture failed behaviorally against goal-manager before SHA-256 `9AFCFFAB700E41C137DA7C66FE3ED66ED46E3DEC58382220E0DCAC73F1383A5A`: Unity MCP job `9271ff7f536345e98a95f007834a6c43`, 48 discovered, only `MultipleConflicts_ThrowingCancellationSubscriberDoesNotStrandCleanup(1)` failed: `Cancelled children must not consume the active-goal capacity. Expected: False; But was: True`.

Smallest owner-boundary fix in frozen `Assets/Scripts/AI/Commander/CommanderGoalManager.cs`: attempt both cancellation notifications even if `GoalStatusChanged` throws, run `CleanupTerminalGoals` in `finally`, then rethrow the first subscriber exception with its original stack. Cancellation authority, terminal/archive transition, worker release, and nonthrowing event order remain unchanged. The protected before SHA-256 above and after SHA-256 `0F79E294D202340044BDDE0539735AAB139CDBAA4E0DBCEB327D31CFE21C048A` frame this **exact** diff:

```diff
@@ -1,5 +1,6 @@
 using System;
 using System.Collections.Generic;
+using System.Runtime.ExceptionServices;
 using UnityEngine;
@@ -200,9 +201,13 @@
                 ArchiveGoal(goal);
                 if (ActiveGoal == goal) ActiveGoal = null;
                 Debug.Log($"[Commander] Goal #{goal.GoalId} cancelled.");
-                GoalStatusChanged?.Invoke(goal);
-                PublishEvent(CommanderGoalEventType.GoalCancelled, goal, simulation.CurrentTick);
-                if (!isTicking) CleanupTerminalGoals();
+                Exception observerError = null;
+                try { GoalStatusChanged?.Invoke(goal); }
+                catch (Exception error) { observerError = error; }
+                try { PublishEvent(CommanderGoalEventType.GoalCancelled, goal, simulation.CurrentTick); }
+                catch (Exception error) { if (observerError == null) observerError = error; }
+                finally { if (!isTicking) CleanupTerminalGoals(); }
+                if (observerError != null) ExceptionDispatchInfo.Capture(observerError).Throw();
                 return true;
```

No edit was made to the current `StrategicPlanner.cs` in this round; its SHA-256 remains `23B96F77FBAA080C54A86AB1186470AE6691096BD646A5B7C0C0198326F17FC4`. Final test-file SHA-256 is `8A06D7E0643FFC86AC6038BD581935D2219C69156D22E988AF56E474009437AC`. Focused GREEN Unity MCP job `d5d0d25908a144189a2fad2bf4dfdb7d` passed 48/48; after adding explicit cancellation-event delivery assertion, final focused job `eb90eb2729d74129a20c6e77cd21f136` passed 48/48. Adjacent `CommanderPhase1Tests`, strategic plan/decision, and approval regression job `7ff8220ef01e4ad4bb3b10d556ef873a` passed 122/122, with zero failed/skipped. Every job reached a terminal status before the next began; transient MCP instance loss was polled on the same job ID. Native terminal JSON exposed job IDs and counts but no XML bytes/path, so no XML hash exists. Final Unity console query for `error CS` returned zero entries; `git diff --check` found no diff errors. No staging, commit, reset, or cleanup.

Remaining gate judgment is unchanged from fix round 2: an observer failure is propagated after old-plan cleanup, before incoming-plan creation. All old child goals now leave active capacity, and cancellation events are attempted even when a status observer throws. This is not a transactional success guarantee under arbitrary external exceptions; root/independent reviewer must explicitly rule on that residual.
