# Phase 5B final intent/progress fix — implementation checkpoint

2026-10-09. **Historical implementation checkpoint; superseded by
[final handoff](phase5b-final-intent-progress-fix-report.md). Phase 5B is not accepted.**
The three P1 requirements in the active goal remain the completion criteria. Preserve
the independent verification report and all its initial failures/timeouts.

## Completed progress-counting change

`CommanderTaskBoardProjection.GoalProgress` now pairs exact placed/result-bound
construction observations with the request's `Count`, rather than the global
baseline-plus-request `TargetTotal`. Ordinary global-count construction retains
its owned-total counting basis. This changes presentation only, not native
completion, authorization, goal creation, dependencies, or cancellation.

The focused concurrent native scenario also exposed repeated rejected placement:
resource-relative preflight checked a zero-tile border while ordinary native
construction requires a one-tile border for Lumber Yard/Mill. The planner now
checks the native border (including the native zero-border building exceptions).
It retains the same bounded semantic candidate set; no unrelated-site fallback
or native rule weakening was introduced.

### Evidence

- RED job `7ff41bdd7f25490880d13bfcbed14bd4`: six completed cases; the two cases
  with pre-existing Lumber Yard/Mill failed with `Completed buildings: 0 / 2`
  instead of the request-local `0 / 1`. These were observed tool failures, not
  reconstructed XML results; the later editor session no longer retains the job.
- Intermediate job `91466ebeec324f6bb8041ab06270ffb8`: 21 completed cases, with
  the concurrent same-resource construction failure recorded in the tool's
  failure list. That failure motivated the native-border investigation. Full
  terminal result details were unavailable; do not present it as a green run.
- Final explicitly filtered EditMode job `088641145fce4b5ba42450cc150fb9fd`:
  **15/15 passed**, zero failed/skipped, 20.6962813 seconds.
  [Exact results](evidence/final-progress-focused-results.json).
- Focused PlayMode job `2aa9c059c6224a7d943338de466c913b`: **1/1 passed**, zero
  failed/skipped, 0.4233428 seconds. Actual minimized badge and visible task-Cancel
  controls. [Exact results](evidence/final-progress-playmode-results.json).

Native fixtures use normal placement/construction/gather commands and simulation
ticks on controlled visible terrain. They include existing same-type buildings,
two concurrent same-resource placements with distinct produced IDs, build→assign
completion ordering, and unplaced global progress across cancellation. Existing
alias/provider-context/future-birth/age-card controls were also sampled. These are
not real-provider, standalone, Web, exhaustive content, or broad regression proof.

## Open intent/schema work

The audited provider prompt omitted the exact Request-node constraint object shape and made
dependency use optional. It also conflicted about new-production count semantics,
dependency-reference limits, and whether semantic worker selectors are allowed.
Typed admission faithfully preserves supplied fields, but cannot recover omitted
fields from prose. Mandatory empty arrays alone would not solve this.

The bounded contract change now declares construction builder eligibility/count
and compound execution ordering explicitly. Admission rejects unrepresentable
builder quantities and inconsistent/missing sequential completion dependencies.
The LLM still supplies semantic selectors only; the game chooses entities and
issues ordinary commands. No phrase-specific checks or approval changes.

`CommanderPhase5BFinalIntentContractTests` produced RED job
`fd910712fde0451889866e69cde60495`: nine completed cases, seven expected failures.
[Observed RED record](evidence/final-intent-contract-red.json). The declaration
boundary has since been activated, with the revised boundary RED record in
[final-provider-boundary-red.json](evidence/final-provider-boundary-red.json).
Final job `935a92d37a844bd9932f270d160edd5e` passed **18/18**, zero failed/skipped,
6.8648334 seconds ([exact results](evidence/final-provider-native-green.json)).
This combines nine contract/diagnostic cases with native placement/progress,
concurrent requests, compound dependencies and cancellation.
Bounded allowlisted diagnostics are now implemented; the
private-value/invalid-mode case passed **1/1**, job
`02f06abdbe474c269c81b80f2cdabbf8` ([result](evidence/final-schema-diagnostic-green.json)).
The idle-Mill original
offending field remains unknown. Capture bounded allowlisted field-path/error-code
diagnostics and reproduce before any field-specific repair. No timeout increase,
repair-policy expansion or malformed-output acceptance has occurred.

The first live baseline launch was rejected by the safety reviewer because exact
payload/destination approval was missing; its opt-in was disabled and it made no
HTTP attempt. The user then explicitly approved four bounded OpenRouter attempts,
including repairs, for the disclosed controlled fixture context. The authorized
baseline job `51240f06e3e14324bb0a9ec84582250a` was **Inconclusive**, not passed:
one initial HTTP attempt terminally cancelled at the unchanged provider timeout,
with zero HTTP status, zero repairs/goals. Preserve [baseline capture](evidence/final-intent-live/idle-mill-before-contract.json)
and [test details](evidence/idle-mill-baseline-test-result.json). Its zero-total
summary is not a pass; the individual case states Inconclusive. The original
HTTP-200 malformed-field cause remains unverified. No field-specific codec repair
is justified. Three attempts remain in the separate persistent four-attempt
scope; original ledgers and credentials are untouched. The live opt-in is disabled
by teardown. The isolated PlayMode fixture compiles without a new assembly/package
dependency.

OpenRouter now uses `ParseProviderResponse`: each Request construction node declares
`builders:{state:Eligible|IdleOnly,count:1}` and each compound declares
Independent/Sequential/DependencyGraph ordering. Sequential requires predecessor
completion edges. Missing declarations, unrepresentable builder counts and
contradictory constraints reject before admission, rather than inventing intent.
IdleOnly normalizes to the existing PreferredWorkers constraint through DTO/scope
validation. Legacy internal typed parsing remains separate for existing DTO/native
fixtures. The prompt now documents exact constraint syntax, eight dependency
references, semantic worker selectors and correct New production counts.
DynamicPlan retains its existing constrained grammar and approval path.

Remaining: only the three approved remaining HTTP attempts for normalized idle
Lumber Yard, idle Mill and compound “then” verification, with safe evidence and
native completion/approval checks. The original malformed Mill field was not
reproduced because the baseline timed out; no speculative compatibility repair was
made. Offline greens do not certify broader language fidelity, stale packages,
standalone/runtime acceptance or independent audits.

## Verification incident — retain, do not hide

Job `ceff9efaa1554b7a99183b31d4ab4c99` was intended to reuse the focused list,
but temporary orchestration storage was cleared by automatic continuation. The
undefined filter was omitted, unintentionally starting broader EditMode regression,
contrary to scope. Editor logs showed unrelated historical fixtures. Observation
then stalled; after reconnection MCP reported `Unknown job_id`, no active tests,
and empty test-job SessionState. Its result is **unknown/interrupted, not a pass**.
No duplicate broad run or force-kill was performed. Subsequent launches define
explicit filters in the same call and assert their exact nonzero lengths before
dispatch. Never read a potentially missing stored filter into `run_tests`.

No Windows/Web rebuild, independent hostile audit, commit, push, reset, or clean.
Existing packages are stale. Do not issue `READY FOR LUNA VERIFICATION` until the
remaining intent/schema work and its bounded live/native verification are complete.
