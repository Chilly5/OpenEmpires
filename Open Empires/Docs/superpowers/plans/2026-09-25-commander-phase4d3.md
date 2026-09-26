# Commander Phase 4D.3 implementation plan

> Execute task-by-task with superpowers:subagent-driven-development, test-first, one source writer and one Unity runner at a time. The root owns design rulings, evidence, and gates.

**Goal:** Preserve deterministic recovery inside an approved plan; surface material change as a detached stale-safe proposal; permit only an explicit player-confirmed, existing-authority replacement.

**Spec:** `Docs/superpowers/specs/2026-09-25-commander-phase4d3-design.md`; original user Phase 4D.3 objective at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`.

**Baseline:** 4D.2 gate PASS in `Docs/CommanderPhase4D/phase4d2-gate-report.md`; current branch HEAD plus accepted 4D.2 test/doc changes are user workspace state. Preserve them, all recovery/test scenes, Gemini config, and separately authorized OpenRouter Luna selection. No staging, commit, reset, or cleanup during tasks unless the root explicitly rules on exact provenance.

## Global constraints

- Never route proposal/provider/health directly to planner or gameplay commands. Execution remains `StrategicApprovalLayer` → `StrategicPipeline.EvaluateApprovedIntentNow` → `IStrategicApprovedDecisionPolicy.DecideApproved` → `StrategicPlanner.SubmitIntent` → existing deterministic goals.
- No new objective, arbitrary budget/target parameters, hidden-enemy data, unbounded memory, wall-clock/frame/random gameplay decisions, package/network change, or credential exposure. Unsupported requests fail closed; current approved plan remains authoritative.
- Exactly one production writer and one Unity runner. Tests are written first; observe a meaningful behavioral RED before production, then focused GREEN. Run affected regression and complete suites at source freeze. Preserve exact native XML or terminal payload, job IDs, discovered IDs/counts, source hashes, and zero failed/skipped/inconclusive evidence. Do not rerun an in-flight job because of a transient MCP timeout.
- Any protected-file edit requires frozen before-hash, diff, reason, after-hash, regression, and independent Sol review in `Docs/CommanderPhase4D/phase4d3-orchestration-ledger.md`. Do not silently rebaseline Phase 4C.
- The UI has two distinct actions: ordinary approval of an AI recommendation and explicit confirmation as a player command. Only the latter can supersede conflicting AI plans; neither bypasses fresh approval/policy. Compatible plans may coexist. A direct-player plan remains protected.
- Treat a source plan revision, lifecycle state, source set, blocker assumption, pipeline generation, or owner change as a stale proposal. A stale rejection must not consume the source plan, reservations, goals, or commands.
- Do not claim that Phase 4D.3 fixes the tactical Spearman wood-gathering issue; retain it as a separately observed runtime issue.

## Preflight dependency table

| Tasks | Producer and consumer | Ruling before execution |
|---|---|---|
| 1 → 3 | Task 1 preflights planner replacement; Task 3 host confirmation uses it. | Gate Task 1 before any host replacement claim. |
| 2 → 3 | Task 2 creates detached proposal/stale validator; Task 3 stores and checks it. | Task 3 consumes the value API only; no second validator. |
| 1, 2 → 4 | Task 4 integrates behavioral runtime proof. | Keep the Unity runner single-owner; do not overlap source edits and a test job. |
| 1 internal | RED replacement test versus minimal planner change. | Assert old plan/reservation state after rejected admission, not only a rejection return. |
| 2 internal | RED proposal/value tests versus new DTO. | Proposed quote is explicitly not canonical budget. Unsupported increased targets/budgets are not executable. |
| 3 internal | RED host tests versus bridge/UI changes. | Capture source before awaiting provider; stale check before `Confirm` consumes pending identity. |
| 4 internal | Runtime tests versus frozen production source. | Controlled stockpile fixtures prove deterministic recovery, not player spending causation. |

### Task 1: Replacement admission preflight

**Files:** `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` (protected); extend `Assets/Tests/EditMode/CommanderPhase3C5StrategicDecisionTests.cs` or create `CommanderPhase4D3Tests.cs` with `.meta`.

- [ ] Record baseline hash/status of the protected planner. Write behavior-first RED for `AIRecommendationCannotAutoReplaceActivePlan`, `PlayerApprovedReplacementUsesExistingAuthorityPath`, `ReplacementReleasesOldReservationsCorrectly`, and additional `ReplacementLateAdmissionFailure_PreservesOldPlan` and `MultipleConflicts_PreflightAllBeforeCancellation`. Construct the late failure with a controlled malformed incoming template or equivalent deterministic post-cancellation exception; do not assume a simple one-for-one replacement can fail the active-plan cap. Construct multiple existing compatible plans where a later conflict is noncancellable by revision budget, and assert the earlier one stays active. Existing passing characterization may be reused, but at least one new late-failure test must fail under current source for the intended reason.
- [ ] Run exact focused EditMode test IDs through Unity MCP, save terminal RED job and native XML/payload. Do not call a compile error behavioral RED.
- [ ] Validate nonempty, non-null, ordered incoming milestones before cancellation, then move read-only incoming-plan budget/fit, projected active-count check, and every conflicting-plan cancellation-feasibility check ahead of all `CancelPlan` calls. Catch preflight exceptions as rejected submissions while the old plan is intact. Keep the existing policy decision, old-plan cancellation/release, and new-plan start unchanged after successful preflight. Do not add rollback or a new public submission path.
- [ ] Run focused GREEN, affected strategic decision/reservation suites, and zero compiler-error check. Record before/after hash/diff and task report. Independent Sol review must approve authority and old-plan preservation before Task 3.

### Task 2: Detached adaptation proposal and staleness

**Files:** new `Assets/Scripts/AI/Commander/Strategic/StrategicAdaptationProposal.cs` with `.meta`; new or extended `Assets/Tests/EditMode/CommanderPhase4D3Tests.cs` with `.meta`. No planner/source mutation.

- [ ] Write RED behavior tests with exact IDs: `MaterialObjectiveChange_RequiresPlayerApproval`, `AdaptationProposal_DoesNotModifyPlan`, `AdaptationProposal_DoesNotCallPlanner`, `AdaptationProposal_IsDetached`, `AdaptationProposal_IsDeterministic`, `StaleAdaptationProposal_IsRejected`, `CompletedPlanRejectsOldAdaptationProposal`, `CancelledPlanRejectsOldAdaptationProposal`, `ReplacementPlanRejectsOldProposal`, `BudgetIncreaseRequiresApproval`, `TargetIncreaseRequiresApproval`. Include blocker-disappeared, pause/resume, revision-only, owner mismatch, malformed/null, source-list mutation, and explicit quote-versus-canonical-budget assertions. Assertions must observe real plan state or copied value output, not a mock's existence.
- [ ] Observe focused RED. Implement a bounded, immutable scalar/list DTO and pure builder/checker. Capture owner-scoped source health, **actual current source-plan milestone tactical requests** (including fitted worker targets), canonical budget, and identity as one synchronous detached source value before any provider await; validate the plan identity/revision again after copying. Do not reconstruct old targets from template constants or the health snapshot. Build the eventual proposal from that copied source, a pending supported intent, and optional detached feasibility quote. It may create an ephemeral template plan for proposed fixed targets but must not call or retain `StrategicPlanner`, submit an intent, reserve resources, or emit a command. Label new feasibility cost a quote. Deterministic serialization uses explicit fields and stable order.
- [ ] Run focused GREEN and static reflection/serialization boundary checks. Independent Sol review must approve value detachment, deterministic behavior, and fail-closed staleness before Task 3.

### Task 3: Host proposal, explicit confirmation, and reset safety

**Files:** `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs` and a focused new partial if needed; `Assets/Tests/PlayMode/CommanderPhase4D3HostPlayModeTests.cs` with `.meta`. Do not change the bridge's provider authority or the policy layer unless a behavioral RED proves a narrowly scoped need.

- [ ] Write RED host tests for active RangedReinforcement → requested DefensiveTurtle pending while old plan/reservations remain active; normal approval cannot replace; explicit confirm goes through the existing approval/policy path and releases old reservations; direct-player plan protection; stale response if source changes during async translation; stale confirmation after complete/cancel/revision/pause-resume/blocker disappearance; no-source request that becomes active while provider waits; multiple active plans fail closed; reset/reinitialize/dismissal clear proposal and bridge state. Record exact provider, planner submission, goal, reservation, and command effects.
- [ ] Observe focused RED. Capture the source token before provider await; reject stale response before displaying. Store `PendingAdaptationProposal` only alongside matching bridge pending intent. Keep old plan untouched while pending. For an adaptation, ordinary Approve must not consume intent or replace a plan; Confirm validates fresh source/pipeline/generation first, then calls the existing bridge conversion and `StrategicApprovalLayer`/pipeline path. Fail closed on unsupported parameters. Clear proposal on all lifecycle paths.
- [ ] Run focused GREEN, affected 4B2/4C/4D1/4D2 host regressions, and zero compiler-error check. Independent Sol review must approve stale async, authority path, and test adequacy.

### Task 4: Deterministic recovery/runtime proof and 4D.3 gate

**Files:** new `Assets/Tests/PlayMode/CommanderPhase4D3RuntimePlayModeTests.cs` with `.meta`; reports/ledger/progress/matrix under `Docs/CommanderPhase4D/`. Production source is frozen except an independently reviewed repair for a genuine RED.

- [ ] Write and run `TemporaryResourceBlocker_RecoversWithoutNewApproval` and `PopulationPrerequisite_RecoversWithoutNewApproval` on real approved plans and deterministic simulation/goals, with typed waits and natural continuation. Observe RED only for uncovered behavior; do not force a fake failure when the existing deterministic recovery already works. Prove no second provider call/approval, same plan ID/objective/approved target, bounded commands, and reservation release at terminal. Include actual RangedReinforcement → DefensiveTurtle pending/confirm/replacement PlayMode flow if Task 3's host fixture does not already prove it.
- [ ] Complete focused and affected regressions; collect exact discovered IDs, counts, XML/payload/hash, zero compiler errors. Run independent Sol whole-4D.3 review against spec, authority, protected diff, staleness, and test adequacy; close Critical/Important findings.
- [ ] Freeze exact production/test hashes. Run fresh complete EditMode and PlayMode suites sequentially against that source, zero failed/skipped/inconclusive, preserving genuine job/XML evidence. Re-run baseline/protected hash audit, credential scan without printing secrets, package/settings status, and classify inherited recovery/test-scene provenance. Write a 4D.3-only gate report. Do not claim Phase 4D complete; 4D.4 and final audit follow.

## Unity evidence procedure

Select the live OpenEmpires instance from `mcpforunity://instances`; verify ready/idle and no compile/reload before running. After edits, let Unity import and check `read_console` error entries. Call `run_tests` with exact discovered group/test filters, keep its `job_id`, then poll `get_test_job` for that same ID to terminal. Reject zero-discovery as evidence. Record source SHA-256 at execution, complete terminal counts and native XML SHA-256 (or full terminal payload if no XML). Wait for one job to finish before another; never infer success from an MCP timeout or earlier run.
