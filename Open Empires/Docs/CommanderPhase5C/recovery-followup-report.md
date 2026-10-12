# Approved recovery follow-up — 2026-10-11

Historical narrow-follow-up report. The PAUSED references below describe that
checkpoint only; the full Phase5C goal was subsequently resumed and is ACTIVE.
The user reconfirmed player-wide Food-income semantics and narrow recovery
authorization. Current phase status is in progress.md and remaining_work.md.

**ALREADY FIXED IN PRODUCTION; FOCUSED REGRESSION ADDED.** The approved four-nearer-unreachable Berries recovery case passes through the current manager/planner without a production edit. This does not resume the broader paused Phase 5C goal or certify product acceptance.

## Current source facts and conclusion

`CommanderPlanner.Economy.cs` already uses `progressiveRecovery: true` only after an already-started amount allocation strands its exact selected workers. `TryResolveWorkerAssignments` reads each worker's `RecoveryCandidateOffsets`, examines at most `pathValidationCandidates` candidates per observation (default 4, clamped 3–5), advances to the next window when no candidate is reachable, and sets `RecoverySearchExhausted` after a complete failed sweep. It filters `VisibleEconomyTargets` by the original `Destination.SourceKind`, performs known complete-path checks, and keeps the original `SelectedWorkerIds`, count, and `GatheredIncomeAtActivation` when it rebuilds only recovery assignments/commands. Manager blocked retries retain the 1800-tick fail-closed deadline. At the start of each planning observation, `AdditionalGathered` compares player-wide credited income against the unchanged activation baseline and completes before considering further recovery orders; it does not cancel ordinary native Gather.

The prior defect description was stale for this checkout. The new first case proves four nearer, visible, isolated Berries candidates consume one bounded window with no issued command; the next observation finds the farther visible/reachable Berries source and issues one ordinary Gather for the original actor. An available owned Sheep is not chosen under explicit Berries. The other two cases prove exhaustion blocks and expires without replacement/commands, and a different worker's ordinary native DropOff credits the player-wide Food amount while the selected worker's Gather lifecycle continues and Commander emits no new commands after completion. Source depletion/idle transition in the recovery fixtures is deterministic test setup after an actual initial native Gather processing tick; these are accelerated EditMode fixtures, not rendered real-time evidence.

## Files and exact focused verification

Only `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs` changed: three `EconomyFixture` cases, a test-only isolated-Berries setup helper, and a variable-amount overload of its existing semantic request helper. Before editing, its byte-for-byte source was copied to `evidence/recovery-followup-baseline/CommanderEconomyClarificationTests.Execution.cs`. No production source, command, network envelope, UI, or prior Task 3 FIX1 movement file changed.

Unity `Open Empires@6d7310c7`, 6000.5.9f1; test assembly leaves were discovered through the loaded NUnit tree. Both launches used `run_tests(mode=EditMode, test_names=[the three exact leaf full names below], init_timeout=120000, include_failed_tests=true)` and `get_test_job(job_id=..., wait_timeout=60)`; complete terminal payloads are preserved in the named raw JSON files.

- `OpenEmpires.Tests.CommanderEconomyClarificationTests.BerriesRecovery_AdvancesPastFourNearerUnreachableSourcesWithBoundedChecks`
- `OpenEmpires.Tests.CommanderEconomyClarificationTests.BerriesRecovery_ExhaustedVisibleSourcesFailClosedWithoutReplacement`
- `OpenEmpires.Tests.CommanderEconomyClarificationTests.BerriesAdditionalIncome_OtherWorkerNativeDepositCompletesWithoutStoppingGather`

| Gate | Job ID | Terminal result | Durable evidence |
|---|---|---|---|
| Initial characterization against unchanged production | `52fc2f4a72c14125896c64fdb0c66884` | 3/3 passed, 0 failed/skipped, 1.146 s | `evidence/recovery-followup-focused-3.raw.json` |
| Final after strengthening the ordinary-gather-state assertion | `0e7a7a79903544e78b9aeb4df3254c0a` | 3/3 passed, 0 failed/skipped, 1.104 s | `evidence/recovery-followup-final-3.raw.json` |

There is no bug RED in this follow-up: the real existing implementation passed the new reproduction on first run, so no production rewrite was justified. A test-only `Is.AnyOf` matcher initially failed C# compilation because this NUnit version lacks it; it was replaced with an explicit state predicate before the final run. `refresh_unity(mode=force, scope=all, compile=request, wait_for_ready=true)` then succeeded, final console query returned zero errors, and a direct editor code check reported the correct project with `isCompiling=False` and `isUpdating=False`. Final job is terminal. `git -c core.safecrlf=false diff --check -- Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs` exited 0.

## SHA-256 at final focused gate

| File | SHA-256 |
|---|---|
| `evidence/recovery-followup-baseline/CommanderEconomyClarificationTests.Execution.cs` | `17C8FA37A561326A76C2C3514DECA2B9112CAC5C091D6FF33B35E26C50307249` |
| `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs` | `099DB3081D70DE9BECE1700E8434EA0A9C69CF1B226DEC19A4E4E258D49BC462` |
| `Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs` (read-only) | `2C8EA0D3B6B8F8C96DAB792A6EAAE25481DDBEA3FBC1FCE85724567ACEA13718` |
| `Assets/Scripts/AI/Commander/CommanderGoalManager.cs` (read-only) | `8B8DA6FB77EDC27023A1FFC961897C839871C0CAC03B35039A9E840D95D5C21A` |
| `Assets/Scripts/AI/Commander/Economy/AllocateWorkersGoal.cs` (read-only) | `26A0A8557803A0235EAAC52B0CC0AD41AE4B00E46DFBD75167A0F7A76161AE47` |

## Limits and handback

Per-tick bounded work is checked at the planner's existing diagnostic path-check counter (at most 4 in each tested observation), not profiled for wall-clock time. Only the three requested accelerated EditMode cases ran; no full assembly/regression, native real-time match, build, provider, paid, audio, or network test ran. The older Phase 5B fixture also exists but was not re-run. No commit, reset, cleanup, editor restart/kill, credential access, or broader Task 4–7 work occurred.

**Handback:** narrow recovery follow-up complete; sole source/Unity ownership returns to parent. The broader product goal remains PAUSED unless separately resumed by the human.

## Independent narrow review and parent verification

`recovery-followup-review.md` approves spec compliance and task quality with no
actionable defect in this test-only delta. Parent re-fetched the exact terminal
job (without rerunning), read all three Passed leaves and 3/3 summary, checked
current test/planner hashes, diff whitespace and idle/no-compilation/no-job editor
state, and read zero console errors.

Non-blocking evidence limit: the selected worker's native gathering state is
asserted immediately after task completion, and later observations assert no new
Commander commands. A full subsequent native delivery cycle is not proved. Keep
that scenario for later integrated verification; no additional test or full run
was authorized for this closeout. Broader Phase5C requirements remain pending.
