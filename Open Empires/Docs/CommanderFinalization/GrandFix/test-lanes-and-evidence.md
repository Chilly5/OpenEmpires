# GrandFix test lanes and evidence — working contract

G14 focused implementation checkpoint, 2026-10-08. Full GrandFix and independent
or real-service acceptance remain unproven; implementation evidence is not billing,
vendor, physical or packaged acceptance.

## Lanes

- Deterministic/offline: `CommanderGrandFixOffline` and explicitly named affected
  fixtures. Fake HTTP responses, controlled temporary files and prerecorded local
  fixtures do not establish live service or physical microphone acceptance.
- Live provider: `CommanderPhase5ALiveRuntime` / `CommanderLiveProvider`. Disabled
  unless the Editor process has the single nonsecret flag
  `OPENEMPIRES_GRAND_FIX_LIVE_EVIDENCE=1`. No flag is set by this implementation.
  Do not run this lane merely by selecting all tests. Disabled setup skips before
  world creation, credentials/provider initialization or UI-instance mutation.
- Hardware/peer: deliberate consented recordings, packaged Windows/served browser
  and real peers. These gates remain distinct; no fake-media or paired-simulation
  check may be reported as a physical or genuine peer pass.

## Run-wide payment guard

The approved run is `d1000de8-7e43-44d3-8668-52621bb83f2c`; the existing
`paid-usage-ledger.json` is its authority. Opening the live lane requires this
matching file and never creates/reinitializes it. Do not edit counters, copy a
fresh ledger, delete it or change run identity to obtain more budget.

Test-only `EvidenceBudgetJournal` reloads before every reservation. It separately
enforces at most six submissions, at most six semantic HTTP attempts INCLUDING
repairs, and at most 600 billable-equivalent cloud-ASR seconds. Smaller recorded
limits remain smaller. A regular OS-exclusive lock plus bounded atomic replacement
protects concurrent updates. A leftover lock file is not treated as a live lock;
no PID guessing, forced lock deletion or reset interface exists. Unknown/interrupted
reservations remain consumed, including a crash before terminal completion.

Journal input/output is bounded to 65536 bytes and 256 records; corruption, unknown
fields, duplicate properties/IDs, mismatched counters, foreign run, links, unavailable
storage and lock contention fail closed. UTF-8 decode failures become a safe budget
failure, not a raw exception payload. Raw requests, headers, keys and audio are never
written. A reservation proves an attempted budget allocation, not confirmed vendor
billing: cancellation/network failure may leave whether transmission occurred unknown.

`BudgetedSemanticTransport` decorates the ordinary installed transport. One owned
logical async execution context permits the current OpenRouter initial POST and at
most one eligible schema repair; both reserve BEFORE forwarding. Foreign contexts,
out-of-operation requests and third attempts cannot borrow that operation. The
provider/model/schema/ordinary command path are unchanged. Submission budget denial
is an explicit guard category, not evidence of a provider timeout.

The shared support assembly uses `UNITY_INCLUDE_TESTS` / TestAssemblies; ordinary
shipping builds must exclude it. Actual final Windows/Web package exclusion still
needs verification. Internal test diagnostics use the existing friend-assembly
pattern; no new public gameplay API or execution authority was introduced.

### Cloud audio benchmark boundary

`BudgetedSpeechTransport` now wraps the actual existing gateway transport interface.
Inject it into the existing consented speech provider, not a new recognition path.
It validates the current encoder's complete bounded PCM16/mono16k WAV header and
data size, derives duration from actual bytes, reserves the maximum of the supplied
verified minimum and quantum-rounded clip duration BEFORE SendAsync, and records
duration/minimum/quantum/billing-basis reference without audio or session credentials.
Failures/cancellation stay consumed; another service kind cannot use this wrapper.
`OpenApprovedSpeechBenchmark` requires explicit opt-in and the original ledger and
refuses mock billing references. There is no default/guessed live pricing policy.

The caller must record and independently verify the real configured provider/model
and its minimum/rounding/pricing policy behind that reference before a live test.
Tests use synthetic `mock-billing-v1`, not a vendor pricing assertion or permission
to upload human recordings. Production gateway spending limits remain separate.
Actual operator access, billing proof and consented corpus remain external gates.

## Durable scenario evidence

The live fixture writes unique JSON files under `live-evidence/` at setup, provider,
semantic, approval, native and terminal stages, including before Mill's effect
assertion, native deadline assertions and teardown. `ScenarioEvidenceRecorder` has
a 16384-byte cap, atomic writes and first-failure preservation. It exports only
typed normalized nodes/parameters/dependency ordinals, outcome/validity, available
game-owned request correlation, approval state, goal/command counts, native receipt
count and tick. Missing attribution/correlation is explicitly unavailable, never
a manufactured ID. Symbolic provider node names, prose, original prompts/responses,
headers, exceptions, private context and audio are excluded.

Expanded diagnostics now carry reserved initial/repair attempts, observed HTTP status,
safe stop/length/other finish category, game-owned goal IDs, ordinary command class
names and published native result IDs. Each attribution array is bounded and has
an explicit truncation marker. Ordinary goals without published receipt identity
remain unavailable rather than guessed. Rejected provider finishes are categorized
BEFORE rejection without exporting arbitrary finish text or response bodies.

Mill acceptance requires exactly the requested normalized Mill construction effect,
not just IsValid. Answer/Clarify/Unsupported fail at the semantic stage before
approval/native waiting. Dynamic pure selection/location nodes may support the one
Mill effect; additional unrelated effects or incorrect count/type do not pass.

Cleanup remains in finally even if evidence storage fails. A disabled reused
fixture does not retain or overwrite an earlier artifact or reset another UI host.

## Evidence history

- `evidence-lane-red-22c5485f.xml`: 11 failing feature-contract tests before test
  support existed. These prove missing implementation, not a measured paid overspend.
- `evidence-lane-first-green-16bf647e.xml`: 19/19 offline passed, 3.9728174 s.
- Scoped read-only Sol review `g14_budget_review` found logical operation borrowing
  and budget-denial-as-timeout classification. No tests/paid calls in that review.
- `evidence-lane-review-red-68801a91.xml`: 26 cases, 21 passed / 5 failed,
  7.8576291 s. Borrowed operation, missing denial category, raw UTF-8 failure and
  missing durable artifact writer reproduced. Actual OpenRouter schema-repair and
  blocked-repair tests already passed with fake HTTP, no external requests.
- Final affected `891eae391006439b9801da01f7008adb`: 40/40 EditMode,
  8.1330409 s, zero failed/skipped. Artifact `evidence-lane-final-edit-891eae39.xml`:
  26 evidence-lane cases plus 14 existing provider-contract cases. Logical-owner,
  denial-category, UTF-8 and failure artifact fixes pass; actual schema repair is
  exercised through the real OpenRouter code with fake responses, not direct-only mocks.
- PlayMode `d09d168196d445faa0bb7745979ec29c`: 12 selected, 6 native parity
  passed, 6 live scenarios ignored by disabled policy, zero failed, 5.9285069 s.
  Artifact `evidence-lane-play-disabled-d09d1681.xml`. The aggregate is Skipped:Ignored,
  not 12 passed and not live-provider evidence. Original ledger counters and bytes
  remain unchanged at zero. No opt-in flag was set.
- No full regression,
  actual vendor response, physical capture or peer claim follows from these cases.

### Integration follow-through

- RED `19b63939a66747cb8550cf536cb405ac`: 5/5 missing audio/expanded-diagnostics
  feature failures, 1.9572254 s; `evidence-integration-red-19b63939.xml`.
- First GREEN `de052e4d0f8e4f36bb434bb4fc9acf8a`:57/57,12.973624 s;
  `evidence-integration-first-green-de052e4d.xml`, before final coverage additions.
- Read-only Sol `g14_audio_integration_review` confirmed current WAV encoder/header
  compatibility and before-upload reservation, found loss of rejected finish reason.
  RED `cb9ca27a3bd34155812d73dfe5469abf`:2/2 failed,1.6676588 s;
  `evidence-finish-red-cb9ca27a.xml`. Safe category now emitted before rejection;
  private/unrecognized finish text is never traced. No re-review or hostile audit.
- Final GREEN `ff2b7217255a4a00995a795ca1910a8c`:62/62 EditMode,15.6842922 s,
  zero failed/skipped; `evidence-integration-final-edit-ff2b7217.xml`: Integration8,
  EvidenceLane28, OnlineVoice12, ProviderContract14. Includes actual consented speech
  provider→budgeted encoder/upload with fake gateway, denied-consent zero-upload,
  billing minimum/rounding/failure retention, metadata/finish and idle configured
  question one-call/no-gameplay proof. Synthetic audio/minimum is not vendor evidence.
- Play `212c50415b19402e80e7ac903c583ef6`:12 selected,6 native passed,6 disabled
  live ignored,0 failed,4.6114789 s; `evidence-integration-play-disabled-212c5041.xml`.
  Aggregate Skipped:Ignored, not12 passed. Original paid ledger unchanged atzero.
- `evidence-integration-checkpoint-20261008.json` records tested source hashes;
  historical checkpoint bytes are preserved, current working manifest separate.

## Remaining G14 work before closeout / paid execution

1. Actual operator/vendor billing-basis proof and consented audio are still required
   before enabling cloud-ASR benchmarks. The upload guard is implemented above;
   it must not be initialized with guessed or synthetic live terms.
2. Independent adversarial coverage for bounded HTTP/finish and initial/repair counts,
   request/goal/command/native correlation and safe blockers where authoritative. Current
   result-receipt counts may be unavailable for ordinary goals without published
   receipts; do not pretend they cover every native result.
3. Question fixture reconciliation: live-source tracing confirms idle configured
   `SubmitReadOnlyQuestionAsync` still calls the semantic provider once; local
   facts override its prose, while busy/no-provider/failure branches use local
   fallback. The current fixture is idle and configured, so its one-call expectation
   is appropriate. No routing change is justified by the earlier inventory assumption.
   Add offline fixture-path proof and keep unavailable results distinct from timeout.
4. Extend outcome/failure/finalization harness coverage and artifact correlation;
   default-disabled PlayMode is proved above, not an enabled service run.
5. Independent full regression remains Luna-owned with explicit lane exclusions;
   real paid service/hardware/peer evidence requires its separate available access.

No paid HTTP or online-ASR transaction has been used in this checkpoint. The
original run ledger remains at zero; all reservation tests use isolated temporary
copies. Current-source manifests prove byte consistency only, not test success.
