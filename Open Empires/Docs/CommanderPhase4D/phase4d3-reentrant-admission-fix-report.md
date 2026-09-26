# Phase 4D.3 Important review fix — nested planner admission during replacement

Agent: scoped production writer and sole Unity runner. Task: close the whole-phase review finding that a synchronous cancellation observer can submit a plan into the replacement gap. This report is a focused fix handoff, not a Phase 4D.3 or Phase 4D gate.

## Root cause and intended behavior

`StrategicPlanner.SubmitIntent` preflights conflict/capacity against the old active set, then calls `BeginPlanCancellation` (which removes the old plan) and `FinishPlanCancellation` (which publishes `PlanStatusChanged`) before adding the incoming plan. A subscriber to that event can synchronously call public `SubmitIntent` with a trusted PlayerDirect intent. The nested call sees no active Ranged plan, admits another Ranged plan, and returns before the outer DefensivePreparation plan is added. The outer call then adds an incompatible plan without rechecking conflict or capacity. The same commit-phase exposure is possible from child-goal, reservation, incoming-plan status, and milestone events; it is not specific to one event name.

Expected behavior: an in-progress planner submission owns admission until its cancellation, new-plan insertion, and first milestone start finish. A nested submission receives a rejection with no new plan; an ordinary later call is unaffected. This does not give any provider, proposal, or health component new planner authority.

## Behavior-first RED

Added `OpenEmpires.Tests.CommanderPhase4D1Tests.ReplacementCancellationObserver_CannotAdmitNestedIncompatiblePlan` in the existing EditMode fixture. It starts a real RangedReinforcement plan, advances it to its reserving production milestone, captures its real active reservation, and subscribes to its cancellation event. That observer attempts a trusted PlayerDirect Ranged submission while the outer player-override DefensivePreparation submission proceeds. The test checks that the callback ran exactly once, nested admission was rejected, the outer replacement succeeded, only the Defensive plan is active, and the old reservation was released.

An initial fixture run (`c5525ba1a19a40c8b3da4687e2a24867`) failed at the nonempty-reservation setup assertion because Ranged's first economy milestone holds no reservation; it was **not** counted as behavioral RED. After advancing the real plan to its production milestone, exact focused Unity MCP EditMode job `df0cc94e7d934339854a41caa02bebaa` discovered 1/1 and failed for the intended behavior: `A nested submission must not enter the temporary replacement gap. Expected: False; But was: True`. The protected planner SHA-256 at RED was `23B96F77FBAA080C54A86AB1186470AE6691096BD646A5B7C0C0198326F17FC4`. Native [RED XML](phase4d3-reentrant-admission-red-df0cc94e.xml) SHA-256: `E940D8441795414B6C8548A00A66816C4F4956733936248F6AD0D6FDC87175F9`.

## Protected-file change

Protected `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` before SHA-256: `23B96F77FBAA080C54A86AB1186470AE6691096BD646A5B7C0C0198326F17FC4`; after SHA-256: `9410C6C1B3F414F596D2E9DDA67EA703C9C98D636D478DDAEC3D7DF8A9C72C44`. This is the exact incremental protected change on top of accepted Task 1, not the cumulative branch-to-working-tree diff:

```diff
@@ planner fields
+        private bool committingSubmission;
@@ SubmitIntent(StrategicIntent intent, bool isEmergency, bool isPlayerOverride), after ThrowIfDisposed
+            if (committingSubmission)
+                return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
+                    intent, null, StrategicIntentValidationError.CommitmentBlocked,
+                    "A strategic plan submission is already being committed.");
@@ after successful preflight, before building cancellation list
-            var cancellations = new List<StrategicPlan>();
-            for (int i = 0; i < conflictingPlans.Count; i++)
-            {
-                StrategicPlan conflicting = conflictingPlans[i];
-                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
-                {
-                    cancellations.Add(conflicting);
-                }
-            }
-            // Commit all cancellation states before cleanup publishes any reentrant event.
-            for (int i = 0; i < cancellations.Count; i++)
-                BeginPlanCancellation(cancellations[i]);
-            Exception cancellationError = null;
-            for (int i = 0; i < cancellations.Count; i++)
-                FinishPlanCancellation(cancellations[i], ref cancellationError);
-            if (cancellationError != null) ExceptionDispatchInfo.Capture(cancellationError).Throw();
-
-            plan.Authority = incomingAuthority;
-            plan.Source = intent.Source;
-            plan.StrategicPlanId = nextPlanId++;
-            plan.CreatedTick = goalManager.CurrentTick;
-            plan.InitializeRevision();
-            plans.Add(plan);
-            activePlans.Add(plan);
-            intentsByPlanId.Add(plan.StrategicPlanId, intent);
-
-            plan.Status = StrategicPlanStatus.Active;
-            SetIntentStatus(intent, StrategicIntentStatus.Active, string.Empty);
-            StrategicMilestone milestone = plan.ActivateFirstMilestone();
-            Debug.Log($"[StrategicPlanner] Intent #{intent.IntentId} selected template "
-                + $"'{validation.Template.TemplateId}' and started plan #{plan.StrategicPlanId}: "
-                + $"{plan.PlanType}.");
-            PublishPlanStatus(plan);
-            StartOrWaitForMilestone(plan, milestone);
-            return CreatedSubmission(intent, plan);
+            committingSubmission = true;
+            try
+            {
+                var cancellations = new List<StrategicPlan>();
+                for (int i = 0; i < conflictingPlans.Count; i++)
+                {
+                    StrategicPlan conflicting = conflictingPlans[i];
+                    if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
+                    {
+                        cancellations.Add(conflicting);
+                    }
+                }
+                // Commit all cancellation states before cleanup publishes any reentrant event.
+                for (int i = 0; i < cancellations.Count; i++)
+                    BeginPlanCancellation(cancellations[i]);
+                Exception cancellationError = null;
+                for (int i = 0; i < cancellations.Count; i++)
+                    FinishPlanCancellation(cancellations[i], ref cancellationError);
+                if (cancellationError != null) ExceptionDispatchInfo.Capture(cancellationError).Throw();
+
+                plan.Authority = incomingAuthority;
+                plan.Source = intent.Source;
+                plan.StrategicPlanId = nextPlanId++;
+                plan.CreatedTick = goalManager.CurrentTick;
+                plan.InitializeRevision();
+                plans.Add(plan);
+                activePlans.Add(plan);
+                intentsByPlanId.Add(plan.StrategicPlanId, intent);
+
+                plan.Status = StrategicPlanStatus.Active;
+                SetIntentStatus(intent, StrategicIntentStatus.Active, string.Empty);
+                StrategicMilestone milestone = plan.ActivateFirstMilestone();
+                Debug.Log($"[StrategicPlanner] Intent #{intent.IntentId} selected template "
+                    + $"'{validation.Template.TemplateId}' and started plan #{plan.StrategicPlanId}: "
+                    + $"{plan.PlanType}.");
+                PublishPlanStatus(plan);
+                StartOrWaitForMilestone(plan, milestone);
+                return CreatedSubmission(intent, plan);
+            }
+            finally { committingSubmission = false; }
```

The guard begins only after fallible read-only preflight, avoiding a change to normal admission checks. It remains set through all synchronous event callbacks during cancellation and new-plan startup, then clears even if an observer throws. Nested rejection returns directly without publishing a rejection event, so a hostile rejection subscriber cannot recursively reopen or abort the outer cleanup; it does not register or start a plan. This guard is local to the existing planner and does not alter approval, policy, ownership, compatibility, or execution paths.

## GREEN and affected verification

| Unity MCP job | Exact scope | Terminal result | Native XML SHA-256 |
|---|---|---|---|
| `08301bc10b1441c19721a60bdf783d25` | EditMode `CommanderPhase4D1Tests`, including the new regression | 49/49 passed, 0 failed/skipped | [XML](phase4d3-reentrant-admission-focused-08301bc1.xml) `3E863DF7498EF900A3CA763AC9B14F8BD72450527BDD3BAA743D40A804731053` |
| `7a57269da066428bbb4aaf971a952bf8` | EditMode `CommanderPhase1Tests`, `CommanderPhase3C1StrategicPlanTests`, `CommanderPhase3C5StrategicDecisionTests`, `CommanderPhase4B2Tests`, `CommanderPhase4D3Tests` | 144/144 passed, 0 failed/skipped | [XML](phase4d3-reentrant-admission-affected-7a57269d.xml) `8F1384CECF0D947E3F1B91BB5377EE6B7E806CA24C3B5E4F0294E18929BA2ED8` |
| `cbd8ad4d161c47128a67907ffe8c24cb` | PlayMode `CommanderPhase4D3HostPlayModeTests`, `CommanderPhase4D3RuntimePlayModeTests` | 28/28 passed, 0 failed/skipped | [XML](phase4d3-reentrant-admission-play-cbd8ad4d.xml) `5316F345A7F41125C63A96E7E2077449F84EA1FC541C1EC4CF9463FDFE3EF3D5` |

One intervening exact GREEN job `1422347164124bfabb2bbe6d5c43bdaf` was reported by MCP as an initialization timeout although its corresponding native [XML](phase4d3-reentrant-admission-green-14223471.xml) records the new test 1/1 passed; XML SHA-256 `7697B145D25527FD2F072FA3AC22607FCEF2542E60AE044A807D7E54791A7D34`. That ambiguous MCP job is **not** used as the terminal GREEN claim; the later 49/49 terminal job is. Transient instance loss during the 144-test job was resolved by polling the same ID. Jobs were serial; no full suite was run.

Final test-file SHA-256: `D2E4F96429FC67FC13774245DA97B922C70217AF7D0BFC45920A10DF11A1B961`. Unity error-console query filtered to `error CS` returned zero compiler entries; unrelated Package Manager authentication/network errors existed in the console. `git diff --check` found no whitespace error. No staging, commit, reset, cleanup, or modification to another protected file.

## Residual and review handoff

This closes synchronous nested **submission** during a successful outer admission commit. It does not turn replacement into a general transaction against arbitrary observer exceptions; Task 1's documented exceptional-observer residual still applies. A callback can still call other public planner lifecycle APIs, subject to their existing checks; this fix deliberately does not expand into a general mutation lock. Independent root review must inspect the protected guard, the callback test, and the unchanged authority path before accepting the whole-phase finding. Complete EditMode/PlayMode suites remain the subsequent source-freeze gate.

## Independent whole-phase rereview — fix round 1, intent-history cleanup

The reviewer identified an Important regression in the guarded return above: `SubmitIntent(StrategicObjectiveType...)` calls `CreateIntent` first, which registers a `Created` intent. The guarded overload then returned a rejected submission without terminating that intent. `TrimIntentHistory` only removes terminal intents, so repeated nested convenience calls could grow retained intent history past `MaxIntentHistory`. A directly supplied, pre-created intent also remained `Created` after rejection. The previous before/after hashes and GREEN evidence above remain valid historical evidence for the first guard revision, **not** the latest source.

Behavior-first tests were added before the new source edit. The original callback test now submits an explicitly pre-created intent and requires `Rejected` status. A second real-planner test, `ReplacementCancellationObserver_RepeatedConvenienceSubmissionsRemainBounded`, performs `MaxIntentHistory + 5` nested convenience submissions during the actual cancellation callback and checks every result rejected with a terminal intent, retained history at or below the cap, exactly one active incoming plan, no extra plans, and old reservation release. Exact EditMode RED job `5fcc999c51a342e0bab550acd6570be8` discovered 2/2 and failed both on `Created` versus `Rejected` status (not compilation or discovery). Native [RED XML](phase4d3-reentrant-intent-history-red-5fcc999c.xml) SHA-256 `FF9722B23B228AAEA4DD2F8F166CD5541E2C38D0A18445C96480526AB77EC7E2`. Planner at RED SHA-256 `9410C6C1B3F414F596D2E9DDA67EA703C9C98D636D478DDAEC3D7DF8A9C72C44`; final test-file SHA-256 `0D7709546F7164EB51E58764615FAE2DC25034C3972C53655AAFEC8323EFAC62`.

The guarded path now marks only a `Created` supplied intent as `Rejected`, records the existing deterministic reason, and trims terminal history. It still emits no status/rejection observer callback while the outer commit is in progress, so rejection cannot trigger recursive admission or interrupt cancellation. Already non-Created intents are not mutated. No new registration, approval, planner path, or public API was added. This is the **exact incremental protected diff** from the first guard revision:

```diff
@@ SubmitIntent(StrategicIntent intent, bool isEmergency, bool isPlayerOverride)
             if (committingSubmission)
+            {
+                const string reason = "A strategic plan submission is already being committed.";
+                if (intent != null && intent.Status == StrategicIntentStatus.Created)
+                {
+                    intent.Status = StrategicIntentStatus.Rejected;
+                    intent.StatusReason = reason;
+                    TrimIntentHistory();
+                }
                 return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
                     intent, null, StrategicIntentValidationError.CommitmentBlocked,
-                    "A strategic plan submission is already being committed.");
+                    reason);
+            }
```

Protected planner before SHA-256 `9410C6C1B3F414F596D2E9DDA67EA703C9C98D636D478DDAEC3D7DF8A9C72C44`; **latest after** SHA-256 `47171C6D433B54A78FF98EF900AA1C57807081C1A34F51CDAEBD08B6591177EC`. Only the planner and existing 4D1 test file changed in this round.

| Unity MCP job | Scope | Terminal result | Native XML SHA-256 |
|---|---|---|---|
| `20820f95f1024d9e9d1dca70bbced8b6` | Exact two new/extended EditMode IDs | 2/2 passed, 0 failed/skipped | [XML](phase4d3-reentrant-intent-history-green-20820f95.xml) `44AD19793F86BA71808643061F5FCABC1595FC9448B5E81E727953834D10FEE7` |
| `6bbe609b6fdf42be8fe8930cccb49261` | Focused EditMode `CommanderPhase4D1Tests` | 50/50 passed, 0 failed/skipped | [XML](phase4d3-reentrant-intent-history-focused-6bbe609b.xml) `B6CA881ED32C5445A65EC7C4D62DCDFE112D71FF54E658433506289FCCE31106` |
| `8d7e0d1498e14c67a7d66797cddccdee` | Affected EditMode goal, 3C1/3C5 strategic, 4B2, and 4D3 value groups | 144/144 passed, 0 failed/skipped | [XML](phase4d3-reentrant-intent-history-affected-8d7e0d14.xml) `62700904F8E456015C28455E10EF14F2F73FEA09518A78D76632C4971E308628` |
| `7964a8773db44494af57e6356dafc33a` | 4D3 host/runtime PlayMode groups | 28/28 passed, 0 failed/skipped | [XML](phase4d3-reentrant-intent-history-play-7964a877.xml) `7B8E7295F64CED405FE5F2A7437AF9A7CA29748785C1074261F0D5D2E915B5B4` |

Jobs ran serially to terminal; a transient Unity instance lookup during the 50-test job was resolved by polling the same ID. Final Unity `error CS` console query returned zero entries. `git diff --check` found no whitespace error (Git emitted only LF/CRLF conversion warnings). Full suites and independent source review remain pending. The original nontransactional throwing-observer residual remains unchanged.
