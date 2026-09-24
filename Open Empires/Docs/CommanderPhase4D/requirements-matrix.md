# Commander Phase 4D Requirements Matrix

Source of truth: the complete Phase 4D objective supplied at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`. Planning context: `Docs/superpowers/specs/2026-09-23-commander-phase4d1-design.md`, `Docs/superpowers/plans/2026-09-23-commander-phase4d1.md`, `Docs/CommanderPhase4D/phase4d1-core-task-brief.md`, and `Docs/CommanderPhase4D/phase4d1-orchestration-ledger.md`.

## Status key and verified baseline

- **Verified** means a current recorded artifact directly demonstrates the requirement. Baseline verification is limited to what `phase4d-source-baseline.json` records; those historical Phase 4C test results are not Phase 4D results.
- **In progress** means preparation or implementation is underway, but the behavior/gate is not fully verified. Phase 4D.1 now has a scoped PASS; later Phase 4D work and full-suite/final audit gates remain open. Do not mistake scoped evidence for full regression or Phase 4D readiness.
- **Not started** means no implementation or evidence is recorded.

| Baseline item | Status | Evidence / limits |
|---|---|---|
| Captured starting source state (branch, HEAD, dirty status, Phase 4C source manifest, protected-boundary snapshot) | Verified | `phase4d-source-baseline.json`; source-manifest SHA and 27/27 file matches, protected audit SHA and 55/55 frozen hashes. This establishes the recorded baseline, not a fresh re-audit after Phase 4D edits. |
| Phase 4C final EditMode / PlayMode and housing proof | Verified (baseline only) | `phase4d-source-baseline.json` references `../CommanderPhase4C/phase4c4-housing-full-editmode.xml` (571/571), `phase4c4-housing-full-playmode.xml` (87/87), focused 31/31 and 10/10, housing green 3/3 and 1/1, plus the 4C regression summary. No Unity tests were run for the 4D preflight. |
| Existing uncommitted scene/recovery state and boundary-audit label caveat | Verified as recorded, unresolved | `phase4d-source-baseline.json` and `progress.md`. Existing deletions/untracked runner scene were not changed by this matrix; the baseline audit's “in-flight” label caveat remains explicit. |
| Phase 4D.1 work state | Complete — scoped gate PASS | Tasks 1–4 and Gates 1–5 have scoped evidence in the core, host, Task 4, and gate reports. Full EditMode/PlayMode suites and the final Phase 4D manifest/boundary audit remain later-phase requirements; this is not a Phase 4D or Phase 4E verdict. |

## Phase 4D.1 — Player Strategic Plan Controls

| Requirement / named test | Status | Evidence / remaining proof |
|---|---|---|
| Exact whole-form offline pause/resume/cancel/status commands; deterministic whitespace/punctuation handling; reject mixed, incomplete, hostile commands; no provider call | Verified (focused host tests) | Host PlayMode job `438af9bd8518472e90cf5bf28d10be7c`, 11/11: `LifecycleCommands_DoNotCallProvider`, `LifecycleCommands_AreWholeFormOnly`; detailed in host report. |
| Stable plan identity, owner, creation tick/revision; stale controls fail closed; no random IDs or wall-clock authority | Verified (focused tests and Task 4 runtime) | Core XML 39/39 covers stale pause/cancel and revision; Task 4 scenario E invokes the retained displayed callback after pipeline replacement with numerically matching identity and confirms the new plan is unchanged. See `phase4d1-task-report.md`. |
| Pause boundary: stop plan milestones/goals/reservations, preserve completed progress/reservations, allow already-dispatched atomic actions to finish; manual work unaffected | Partially verified | Core 39/39 covers no future plan progress, retained/protected reservations, manual Commander goal continuity, and goal tick behavior. Already-dispatched atomic completion and actual in-flight runtime proof remain for Task 4 Scenario A. |
| Resume the same plan without duplicate goals/reservations/assets; preserve active-time and tick timeout/retry/stall/cooldown semantics across pause | Partially verified | Core 39/39 covers same-plan continuation, long pause, and shifted duration/retry/construction tick anchors. Actual chat/UI pause-tick-resume-complete and reservation release are pending Task 4 Scenario A. |
| Cancel selected plan, release only its reservations, stop future work, preserve completed assets/manual work, do not invent refunds | Partially verified | Core 39/39 covers reservation release and manual Commander goal continuity; completed-asset check is only an EditMode smoke test on an unrelated House. Task 4 Scenario B must verify plan-produced completed assets, raw player CommandBuffer continuity, and no later progress. |
| Ownership and action/result validation; unknown action rejection; deterministic result; active-plan ambiguity fails closed; reset lifecycle semantics | Verified (focused tests and reset runtime) | Core 39/39 covers ownership, unknown actions, deterministic result, and ambiguous core capture; host 11/11 covers provider isolation/ownership and selection; Task 4 scenario F covers held provider reply across runtime reset. The distinct named-ownership job also passes all three exact pause/cancel/resume names (39/39 total). |
| Player-visible status and controls, selected plan when multiple plans exist, intentional separate cancel confirmation, generation-bound callbacks | Verified (focused host tests; real scenario integration remains) | Host job `438af9bd8518472e90cf5bf28d10be7c`, 11/11 covers plan selection, two-click same-revision cancel, disarming on revision change, and stale callback rejection across same numeric identity/new pipeline. |
| Behavioral test-first RED/GREEN, compiler/import clean, scoped review, relevant regression, static boundary gate | Verified (scoped Phase 4D.1 gates) | See `phase4d1-gate-report.md`: all five scoped gates PASS, including independent Sol Gate 3 after the Task 4 test-only fix and Luna Gate 5 static audit. Core XML 39/39; focused/combined affected jobs and regression evidence are itemized there. Full Phase 4D suite/audit gates remain later work. |
| Required 4D.1 PlayMode: actual chat/UI RangedReinforcement pause/tick/resume/complete; DefensiveTurtle partial progress/cancel; reservations/assets/manual work checked | Verified (focused runtime) | Final Task 4 job `1d359ebf1ca34289947ef79b2d1c197b` passed 4/4, 0 failed/skipped; combined affected PlayMode job `54cfbf047cf445deb6110ad22b3f9fcc` passed 48/48. Durable terminal payloads and final source hash are in `phase4d1-task4-final-jobs.json`; Unity MCP emitted no native XML. This is not full PlayMode-suite evidence. |

The three separately named cross-player tests `PlayerCannotPauseOtherPlayersPlan`, `PlayerCannotCancelOtherPlayersPlan`, and `PlayerCannotResumeOtherPlayersPlan` are present in `CommanderPhase4D1Tests.cs` and passed in `phase4d1-named-ownership-job.json` (39/39 total). Focused results do not establish full-suite coverage; Task 4 report documents the real chat/UI scenarios and raw human-command/plan-produced-asset checks.

## Phase 4D.2 — Strategic Plan Health & Stall Detection

| Requirement / named test | Status | Evidence / remaining proof |
|---|---|---|
| Immutable, detached health projection with structured status/blocker, copied resource/population/production/progress data; no retained live Unity/planner/simulation/command objects | Not started | No 4D.2 plan, implementation, or evidence. Candidate type in objective: `StrategicPlanHealthSnapshot`. |
| Deterministic stall detection based on simulation ticks/progress; distinguish expected gathering/waiting, no progress, impossible continuation, and pause; named threshold and boundary tests if used | Not started | Objective only; no implementation or tests. |
| Fog-safe diagnosis limited to owned/approved/known/visible state; no blocker inferred from explanation strings | Not started | Objective only; no audit or runtime proof. |
| Read-only health observation: no simulation mutation, plan advancement, provider call, or reset/replacement leakage; mutation-isolation proof | Not started | Required tests: `PlanHealth_IsDeterministic`, `PlanHealth_DoesNotMutateSimulation`, `PlanHealth_DoesNotAdvancePlan`, `PlanHealth_DoesNotCallProvider`, `SameStateProducesSameHealthSnapshot`, `ResourceWaiting_IsNotMisclassifiedAsFailure`, `PopulationWaiting_IsStructured`, `PausedPlan_ReportsPaused`, `CompletedPlan_ReportsCompleted`, `CancelledPlan_ReportsCancelled`, `HealthSnapshot_DoesNotRetainSimulationObjects`, `PlanHealth_RemainsFogSafe`, `Reset_ClearsPlanHealthProjection`, `OldPlanHealth_DoesNotDescribeReplacementPlan`. |
| Structured health states (healthy, paused, resource/population/prerequisite/capacity/worker/reservation waiting, temporary block, player decision, completed/cancelled/failed) without redundant state | Not started | Conceptual states only; no status mapping or evidence. |

## Phase 4D.3 — Safe Recovery & Adaptation Proposals

| Requirement / named test | Status | Evidence / remaining proof |
|---|---|---|
| Deterministic recovery only within already-approved intent (wait/gather/build ordinary prerequisite/retry/reassign within constraints); no objective/target/budget expansion, attack behavior, or silent target reduction | Not started | No 4D.3 plan or implementation. |
| Material change represented as detached, bounded proposal with source plan identity/revision, objective/reason/budget/targets; proposal itself cannot mutate plan or call planner | Not started | Objective suggests `StrategicAdaptationProposal`; no implementation. |
| Approval chain remains proposal → player → validated intent → approval layer → policy → planner; old active plan remains until approval; replacement cleans reservations through existing authority | Not started | No integration or proof. |
| Stale proposal rejection after pause/resume/revision change, completion, cancellation, replacement, or blocker change; no resurrection | Not started | Required tests: `StaleAdaptationProposal_IsRejected`, `CompletedPlanRejectsOldAdaptationProposal`, `CancelledPlanRejectsOldAdaptationProposal`, `ReplacementPlanRejectsOldProposal`. |
| Temporary resource/population recovery without reapproval; material objective, budget, or target changes require approval | Not started | Required tests: `TemporaryResourceBlocker_RecoversWithoutNewApproval`, `PopulationPrerequisite_RecoversWithoutNewApproval`, `MaterialObjectiveChange_RequiresPlayerApproval`, `BudgetIncreaseRequiresApproval`, `TargetIncreaseRequiresApproval`. |
| Proposal detached/deterministic/non-authoritative; AI cannot auto-replace; player-approved replacement uses existing path and releases old reservations | Not started | Required tests: `AdaptationProposal_DoesNotModifyPlan`, `AdaptationProposal_DoesNotCallPlanner`, `AdaptationProposal_IsDetached`, `AdaptationProposal_IsDeterministic`, `AIRecommendationCannotAutoReplaceActivePlan`, `PlayerApprovedReplacementUsesExistingAuthorityPath`, `ReplacementReleasesOldReservationsCorrectly`. |

## Phase 4D.4 — Commander Strategic Advisories

| Requirement / named test | Status | Evidence / remaining proof |
|---|---|---|
| Offline deterministic advisory rendering from structured state; emit only meaningful transitions, not every tick; no background paid provider calls | Not started | No 4D.4 plan, implementation, or evidence. |
| Advisory is informational only: cannot approve, create intent, control lifecycle, submit planner work, or execute | Not started | Required tests: `Advisory_DoesNotExecuteAnything`, `Advisory_DoesNotCallProvider`. |
| Deterministic per-plan transition dedupe; bounded retention; reset on match/owner/system/Commander replacement or destruction; no cross-match persistence; tick cooldown only if needed | Not started | Required tests: `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`. |
| Fog-safe messages and lifecycle/health/adaptation summary in minimal UI; cancel intentional and separate from approval | Not started | Required test: `Advisory_RemainsFogSafe`; UI remains unimplemented. |

## Cross-phase invariants and controls

| Requirement | Status | Evidence / remaining proof |
|---|---|---|
| Preserve authority chain: validated player input/detached recommendation → approval layer → decision policy → planner → goal manager → deterministic execution; LLM never gains lifecycle, budget, goal, command, or simulation authority | Not started for 4D changes | Existing baseline does not prove future Phase 4D changes. Requires source inspection, scoped review, tests, and final audit. |
| No new objectives (`TechnologyRush`, `SiegePreparation`, `NavalExpansion`, or others), autonomous switching/approval, background LLM polling, cloud/hidden memory, wall-clock decisions, network redesign, unrelated package/auth/credential changes | Not started for 4D changes | No post-change audit exists. These remain explicit exclusions. |
| Reservation/resource invariants: pause/resume/status/advisory/proposal do not duplicate or hold resources; cancel/stale controls release only correct plan; completed/cancelled plans do not leak reservations; preserve documented Phase 4C opportunistic House limitation | Not started for 4D changes | Baseline records the Phase 4C House limitation and runtime proof; no Phase 4D invariant test evidence yet. |
| Async/concurrency: provider result after cancel/revision change/reset cannot restore pending intent, lifecycle, health, memory, proposal, advisory, or execution in new session | Not started | Objective scenarios only; no Phase 4D tests/runtime proof. |
| Multiplayer ownership: trusted host/player identity only; cross-player pause/resume/cancel rejected; preserve host-only model and no networking redesign | Verified for scoped 4D.1 paths | Core and named-ownership EditMode jobs cover owner validation and all three named cross-player rejections; Task 4 runtime exercises host controls. No networking redesign is claimed, and this is not a broad multiplayer/networking audit. |
| Serialization/memory/explanations: detached immutable bounded deterministic culture-invariant values; unknown enums fail closed; non-authoritative bounded memory; answers cite copied structured evidence or report unavailable, never invent causes/motive | Not started | No 4D.2+ implementation or evidence. |
| Test-first records (agent/task/expected output/RED/implementation/GREEN/review/concerns) | In progress for 4D.1 preparation; not started for later phases | Task-report template requirements appear in 4D.1 brief/plan; no execution reports or RED artifacts yet. |

## Sub-phase gates

| Gate | Status | Required evidence before advancing |
|---|---|---|
| 4D.1 Gate 1 — focused tests 100% | Verified (scoped gate) | Core genuine NUnit XML 39/39; host focused PlayMode 11/11; Task 4 final PlayMode 4/4; affected EditMode 70/70; combined affected PlayMode 48/48; named-ownership EditMode job 39/39. All recorded with zero failures/skips; see reports and durable job payloads. No claim of full suites. |
| 4D.1 Gate 2 — compiler/import state | Verified for scoped runs | Core and host task reports record Unity compiler/console errors 0 for their runs. This is not a fresh full-suite freeze/import audit. |
| 4D.1 Gate 3 — independent scoped review | Partially verified | Independent core third review and host review reported no Critical/Important findings for their scopes. Root-owned overall architectural/boundary review remains open. |
| 4D.1 Gate 4 — relevant Commander regression | Verified (scoped) | Latest current-source affected EditMode 70/70 (`9e95280e6de544c38bd25ed13de8e283`, durable terminal payload) covers Phase 4D.1 and Phase 4C.4; combined affected PlayMode 48/48 (`54cfbf047cf445deb6110ad22b3f9fcc`) covers Task 4, host, and Phase 4C.1/.2/.4. Zero failed/skipped. Not complete suites. |
| 4D.1 Gate 5 — static boundary check | Verified (PASS) | Independent Luna static/source-boundary audit PASS. Baseline verifier PASS: 27/27 source paths present, 55/55 frozen protected paths present, 54 unchanged and one intentional `CommanderGoalManager.cs` drift, no missing paths, pinned XML artifacts 6/6. The baseline audit's in-flight/not-final label caveat remains; this is not the final Phase 4D boundary audit. |
| 4D.2 / 4D.3 / 4D.4 respective Gates 1–5 | Not started | Must pass each subphase's focused tests, compiler/import, independent review, relevant regression, and static boundary audit before next implementation begins. |
| Full-suite freezes | Not started | Complete EditMode + PlayMode after 4D.2, after 4D.4/final source freeze, and after any later Critical/Important shared-authority fix. Do not reuse historical totals as current results. |

## Required final runtime scenarios

| Scenario | Status | Acceptance evidence |
|---|---|---|
| A — Pause / Resume RangedReinforcement | In progress | Task 3 host controls are tested, but actual player approval; pause mid-progress; substantial simulation ticks with no plan progress/duplicate goals/new reservations and stable world; resume same plan, complete, and release reservations remain to be demonstrated. |
| B — Cancel DefensiveTurtle | In progress | Task 3 host controls are tested, but actual approval and partial progress; cancel; cancelled status, no future milestones, plan reservations released, completed plan assets and unrelated raw player commands preserved, and no stale progression remain to be demonstrated. |
| C — Temporary blocker recovery | Not started | Cause legitimate resource depletion/production wait; health reports temporary blocker; deterministic gathering recovers it without reapproval and plan completes. |
| D — Material strategy change | Not started | Request DefensiveTurtle during RangedReinforcement; pending recommendation leaves old plan active; no execution before approval; existing approval/policy replaces safely, reservations correct, new plan starts, old work does not resurrect. |
| E — Stale control attack | Not started | Capture Plan A request, replace/cancel A, create Plan B, apply old request; reject and leave B untouched. |
| F — Match reset during async work | Not started | Old provider task completes after reset; no stale intent, lifecycle, health, memory, advisory, proposal, or gameplay execution leaks into new session. |

## Audits, manifests, and final documentation

| Deliverable / audit | Status | Evidence / acceptance |
|---|---|---|
| Frozen Phase 4D protected list before implementation | Verified | `phase4d-protected-boundary-frozen.json` exists and is referenced by the baseline. This is the pre-change snapshot, not the final audit. |
| Final source manifest `phase4d-final-source-hashes.json` | Not started | Must enumerate all changed/new Phase 4D production and test files; verify every disk hash and record manifest SHA-256. |
| Final boundary audit `phase4d-final-boundary-audit.json` | Not started | Must report protected counts, intentional changes, gaps, credential/forbidden-reference candidates, `.env` state, package/settings changes, unexpected source files. |
| Static security/protected-boundary/source-scope audit | Not started | Must inspect actual source, classify candidates manually, account exact hashes/diffs, and inspect temporary scenes/logs/backups without unsafe cleanup. |
| Per-phase plans, reports, preserved meaningful RED/GREEN artifacts | In progress for 4D.1 planning only | Existing 4D.1 design, plan, brief, and ledger are listed above. 4D.1 report/XML absent; 4D.2–4D.4 plans/reports absent. |
| External review package | Not started | Must include spec, baseline, exact changed/new production/test files, diffs/hashes, test artifacts, boundary audit, lifecycle/adaptation diagrams, limitations, and failure/fix record. |
| `Docs/CommanderPhase4D.md` final report and verdict | Not started | Must cover architecture, actual delegation, systems, safety, all test tiers, runtime evidence, limitations; end with exactly `READY FOR PHASE 4E` or `REQUIRES FIX PHASE`. No readiness verdict is supportable yet. |

## Persistent limitations and scope exclusions

| Item | Status | Evidence / handling |
|---|---|---|
| Phase 4C opportunistic House wood is not included in static strategic reservation; deterministic tactical gathering may wait/recover | Verified as baseline limitation | `phase4d-source-baseline.json` cites the Phase 4C regression summary. Do not redesign unless Phase 4D shows an actual correctness defect, with RED evidence and authority review. |
| Unsupported strategic objectives and all other explicit non-goals | Verified as requirements, implementation audit pending | Objective sections 5 and 15; no Phase 4D audit yet. |
| Phase 4E must not begin as part of this work | Not started / deferred | Objective sections 29 and 34. Phase 4D must reach its own evidence-backed verdict first. |
