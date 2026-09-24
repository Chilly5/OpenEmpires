# SDD ledger — plan: Docs/superpowers/plans/2026-09-23-commander-phase4d1.md

## Preflight

The user supplied a continuous agent-orchestrated execution method and explicitly forbade routine approval pauses. The live Phase 4C housing fix is uncommitted and is the required baseline. No isolated worktree or per-task commit will be created now: a new worktree from HEAD would omit that baseline, while committing Phase 4D against HEAD alone would produce a misleading standalone source state. All Phase 4D changes remain separately identifiable in the working tree and in the frozen manifests. This is a deliberate adaptation of the subagent-driven workflow, not permission to stage or absorb Phase 4C files.

| Scan row | Producer / consumer or internal check | Finding and resolution |
| --- | --- | --- |
| Task 1 internal | Control value API and its RED tests | The drafted Task 1 lists planner/host behaviors before their implementation task. Combine Tasks 1 and 2 into one core lifecycle implementation/review unit; keep reset/provider cases for Task 3. |
| Task 2 internal | Planner and tactical-goal suspension | All pause/progress paths must be gated, including public advancement and child events. Protect unrelated goals and frozen files. |
| Task 3 internal | Chat, UI, async reset | Host generation must bind callbacks; status is observation, and cancel confirmation cannot share approval target. |
| Task 4 internal | Runtime and gate | Reuse actual chat approval, not direct planner submission; no full-suite claim at this interim gate. |
| Tasks 1 ↔ 2 | Control request/revision produced in Task 1 and consumed by planner in Task 2; shared EditMode file | Treat as one inseparable RED/GREEN unit with one Sol-class writer and one independent review. |
| Tasks 1/2 ↔ 3 | Validated planner API and its host route; shared EditMode file | Freeze API after core review, then host writer integrates against it. |
| Tasks 2 ↔ 4 | Pause/cancel semantics and runtime fixture | Runtime scenario tests must validate the core behavior through real host controls. |
| Tasks 3 ↔ 4 | PlayMode test file and UI hooks | Complete host controls before runtime gate tests; one production writer at a time. |

Ruling: Work in the existing dirty checkout — the Phase 4C brief requires the current live uncommitted housing fix as baseline, and copying it into a new worktree risks omission; cost if wrong is weaker isolation, mitigated by explicit hash/status snapshots and scoped diffs.

Ruling: Do not commit or stage Phase 4D against the older HEAD until the Phase 4C baseline has an explicitly managed commit boundary — otherwise a Phase 4D commit would not contain the source state it was tested against; cost if wrong is delayed commit history, mitigated by durable docs and per-task hashes/diffs.

Ruling: Combine planned Tasks 1 and 2 for implementation/review, while preserving their separate checklist IDs in the plan — the value API has no meaningful behavioral RED or independently useful GREEN without planner lifecycle semantics; cost if wrong is a larger first review, mitigated by focused test groups and a narrow writer scope.

Ruling: Bare text controls reject multiple active plans; UI plan selection targets a displayed ID — the existing planner permits compatible concurrency, and silently choosing the first would create an authority surprise; cost if wrong is an extra selection click when concurrent plans exist.

Ruling: `Reset_ClearsLifecycleState` means match/runtime reset, not `clear memory` — conversation reset must not mutate an approved live strategy; cost if wrong is a test-name ambiguity, resolved by explicit scenario naming in evidence.

Ruling: Keep the detached control token planner-local (owner, plan ID, creation tick, revision) and bind cross-runtime UI callbacks to their captured pipeline identity plus host runtime generation — a new planner can reuse all those primitive values, so only the host can distinguish that reset without a global mutable or random authority token; cost if wrong is a host-only safety dependency, mitigated by mandatory new-match/same-ID PlayMode tests in Tasks 3–4. The core unit must not claim that direct application of an old token to an unrelated new planner is safe by those primitive fields alone.

## Task status

- Tasks 1+2 core lifecycle: `DONE_WITH_CONCERNS` after independent review fix. Initial RED job `52e31fa3eb1746d7ab0748c677494a7c` failed 7/16; reservation RED `aa3030ecee984e1c9c8e8d0e637a9844` failed 1/19. First core GREEN `02dab460a36040dab116803102eb91ad` passed 22/22. Review RED `0f0378e6035e4c94bd776401cda32b2b` failed 7/29, and targeted resume-overflow RED `8b6df5c948434cae9ec7cab1debf559e` failed 1/1. Review-fix GREEN `97b23bea5ab34e2e971cdc7b0878cc57` passed 31/31 with genuine Unity NUnit XML preserved at `phase4d1-core-review-green-results.xml`. Exact details, hashes, and host-only gaps are in `phase4d1-core-task-report.md`. No full suite was run by this worker.
- Core review package: `phase4d1-core-review-package.md`; independent review's two Important findings were fixed test-first, with deferred terminal child reconciliation and revision preflights. Host/PlayMode integration still owns completed plan-produced assets, raw player CommandBuffer continuity, and same-ID/new-runtime callback isolation.
- Second core review: ordinary event-driven checked-revision overflow fixed test-first. RED jobs `8bd40fc67fd14d10978613c83d34e8ab` (4/35 failed), `2540dbc42c534cdebec41d67145c7b1a` (7/38 failed), and `486be9c67e62418b9bfd7981378dd1e7` (1/1 failed) reproduced partial-mutation paths. Focused GREEN job `711dd649ecd84591869561252dcc7bb9` passed 39/39, with genuine NUnit XML `phase4d1-core-overflow-green-results.xml`. Deterministic synchronous cascades now preflight revision headroom before mutation; arbitrary external callback reentrancy remains an explicitly reported residual risk. Host remains gated, no full suite/commit.
- Task 3 chat/UI integration: pending.
- Task 4 runtime scenarios/gate: pending.

## Final 4D.1 reconciliation (2026-09-24)

The preceding task-status bullets are the historical orchestration checkpoint, not current status. Tasks 3 and 4 have since completed. The root-owned sub-phase verdict is **PASS for the scoped 4D.1 gates**, based on the final Task 4 scenarios, focused/regression jobs, independent reviews, and Gate 5 static audit. This is not a full Phase 4D regression or Phase 4E readiness decision.

- Final Task 4 PlayMode payload: `phase4d1-task4-final-jobs.json`; source SHA-256 `766B03668E7FE6C8548339710A4DF2F86E10A22B5498E920CBE40A54588E14DB`; focused Task 4 4/4 and combined affected PlayMode 48/48, both zero failed/skipped. No native XML was emitted for these jobs.
- Other scoped evidence is itemized in `progress.md` and `phase4d1-gate-report.md`, including the latest current-source affected EditMode 70/70 (job `9e95280e6de544c38bd25ed13de8e283`), host PlayMode 11/11, 4C.2/.4 PlayMode 16/16, and the named-ownership job 39/39 with all three exact ownership test names passing.
- Gate 5: independent Luna static/source-boundary audit PASS. Baseline verifier PASS on references/path presence: 27/27 and 55/55; current frozen-hash comparison is 54 unchanged and one intentional documented `CommanderGoalManager.cs` drift. The baseline audit's “in-flight/not final phase evidence” label caveat remains visible and is not converted into a final Phase 4D audit claim.
- Residuals: no full suites in this sub-phase; no native Task 4/combined PlayMode XML; one documented core event-subscriber reentrancy concern; old baseline audit caveat; pre-existing scene/recovery churn remains unresolved and untouched. No external/paid API calls. Full suites and final Phase 4D source/boundary artifacts remain later gates.
