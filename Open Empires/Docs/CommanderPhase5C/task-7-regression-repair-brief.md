# Task7 interrupted regression — bounded repair

Read `task-7-stall-report.md`, `task-7-recovery-report.md`, and the read-only
`task-7-regression-failure-inventory.md`. The human reopened Unity; direct native
registries are empty and old job `e2b46c85ca834f0a9bc1523d400da96f` is Unknown.
Old partial evidence stays immutable: 1,723 Passed, 19 Failed, one Error, 54 missing.
The sole full Edit run was interrupted, not green. No Play/full build/paid started.

## Scope

1. Fix the unbounded `OlderCancelledLateAttempt_DoesNotOverwriteNewerRequestMetrics`
   fixture first. It must verify that the intended first call really dispatches,
   bound waits, release/observe its owned pending fake transport in finally, and
   never hang the editor if preflight refuses a request or an assertion fails.
   Preserve late/cancelled/old-versus-new attempt identity assertions. Do not alter
   the production 15s timeout, add retries or fake a transport observation.
   First diagnose the exact House/Mill fixture pack refusal/call order using
   bounded local instrumentation. No real HTTP or failed paid setup probes.
2. Investigate and repair the 20 nonpassing recorded leaves as shared causes,
   not one patch per test. Snapshot every touched file before edits.
   - Provider/context/input-limit cluster: distinguish fixture data gaps from
     legitimate production retrieval/budget defects using section estimates and
     actual fake transport call counts. Keep original natural paraphrases,
     especially total ten `spears in my army`; do not replace natural wording with
     easier canonical vocabulary merely to pass. Required context/families,
     negations/counts/source/dependencies must fit or fail truthfully; never raise
     the input cap, cut meaning, grant new capabilities or bypass canonical facts.
   - Old schema/vocabulary/cardinality assertions: update only demonstrably stale
     assertions to current observable contract/parse/admission/effect behavior.
     Do not remove safety, exact bindings, count/source, privacy or approval checks.
   - Approval scroll/source/runtime mismatch: inspect actual instantiated parent
     hierarchy and source/assembly affinity; keep full readable approval and Details
     zero-authority. Do not relabel this a stale test without actual proof.
   - Military Executing-versus-Blocked leaf: establish actual blocker and native
     source/actor/path setup. Correct unrealistic fixture setup only if proved;
     do not weaken its ordinary ICommand/exact actor/Executing semantics or
     substitute unrelated units/location to hide a real runtime defect.
   - SharedWorkerComposition error must be diagnosed, not ignored.
3. Write durable causal findings, minimal changes and exact focused results to
   `task-7-regression-repair-report.md`. Keep first failures/old frozen manifest
   as historical; never overwrite the interrupted capture or claim full green.

## Verification and authority

- One source writer/Unity runner; no subagents/reviewer/commit/worktree/reset/
  force-kill/restart/console clear/credential inspection/backend deployment/audio.
- Read debugging/TDD/Unity skills; import/verify compile and current loaded source.
- Only exact focused tests for diagnosis/repair and affected controls, with actual
  native per-leaf XML, source hashes and durable job IDs. Use the now run-bound
  default-inactive capture helper; no ordered/global-mutating test fixtures.
- NO full Edit rerun, NO full Play, NO build, NO paid call authorized here.
  Subsequent human approval: ONE replacement full Edit pass is approved after
  these repairs on final source; it remains reserved for parent freeze release.
  This implementer still runs focused checks only. Original full Play is unspent.
- Before executing the remaining 54 IDs or combined recovery set, report proposed
  exact scope/count to parent. The union of missing + nonpassing is 74 existing
  IDs before any necessary affected controls; it is not a second full pass.
  Parent decides recovery execution after the harness is safe.
- Production source facts remain authoritative, deterministic game chooses all
  concrete identities/coordinates, model semantic only, separate authority/approval,
  fog/human priority/exact results remain strict. AdditionalGathered player-wide;
  ordinary gathering continues after task completes without new Commander recovery.
- Actual endpoint usage still unknown; fixture usage is not calibration. Paid 0/10.
- Raise any meaningful architecture/scope choice with concrete evidence before
  broadening a fix. Source gate must not hide a real failure behind new test wording.

Return the report/current identities/incremental diff and lease after the bounded
repairs; parent verifies, then handles missing coverage and package gates.
