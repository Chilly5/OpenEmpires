# Task 6 scoped review — 2026-10-12

### Spec Compliance

- ❌ Issues found. The integrated, bounded offline runner and explicit player-capture seam are present, but the replay does not retain an actual request ID for rejected/read-only submissions, its negative-case verdicts are not guarded by their stated conditions, and the ordered-patrol check does not establish native actor state. These are Task 6 evidence-contract defects, not findings against untouched Task 1–5 code.
- ⚠️ Cannot verify from this increment: live Luna English fidelity, actual direct/gateway tokens and cost, three short live checkpoints, release Windows/Web compilation, standalone-player and served-browser populated screenshots, and final full regression. `task-6-report.md` correctly leaves these open for Task 7; the 0/10 paid ledger and offline PNG tests cannot close them. The reconstructed Clarification baseline matches the recorded Task 2 hash but is not a pretouch capture.

### Strengths

- `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:710-807` submits declared turns through `CommanderChatUI`, replaces only the semantic provider, and writes an atomic checkpoint after each turn. `Assets/Tests/Shared/ScenarioEvidenceRecorder.cs:71-113` bounds and allow-lists records; the checked 25 current JSON files have no non-null actual usage or paid attempts.
- `Assets/Scripts/AI/Commander/Phase5A/CommanderRequestTicket.cs:34-55` retains the original request while requiring owner, runtime, generation, current-turn and unclaimed checks. `Assets/Scripts/AI/Commander/CommanderGoalManager.cs:816-831` records builder IDs only after the exact owned native foundation is attributed; `Assets/Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs:241-262` distinguishes dead/missing from protected human control against that foundation.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.EvidenceCapture.cs:45-125` keeps automatic screenshots behind explicit Windows-player flags and uses a normal separate approval turn for the offline fixture. The current artifacts show Food reaching 400 at completion and 510 later with ordinary gathering still active, a fog-hidden Scout control followed by travel reveal, and exact new-result patrol subjects. These are offline/native-fixture observations, not live or standalone proof.
- Saved XML hashes and counts match the handback: final builder/corpus 3/3, ticket controls 7/7, capture leaves 2/2. All 19 current source hashes match `evidence/task-6-review-source-identities.json`; the 175,423-byte review package hash matches `5EA65856F307EF85B5CE5A70C931614C03A2B3D44EE1D349E8DE769E5FD91BA7`. No tests, MCP runs, builds or network calls were repeated.

### Issues

#### Critical (Must Fix)

- None found in this increment.

#### Important (Should Fix)

- `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:1180-1185`: request correlation falls back to `case:<id>` whenever no pending plan/goal exposes the ticket. The current `malformed-provider-json.json:8` and `causal-information-owned-task-facts.json:8` consequently contain a scenario ID in the `requestId` field despite an actual UI semantic submission. This violates the requested distinct request ID and loses correlation precisely on parse rejection/read-only answers. Expose the safe locally generated ticket ID to the recorder at submission time; retain a separate case ID and use null/unknown if no request ticket genuinely exists, not a substituted case label.
- `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:1224-1239`: the malformed evaluator computes `rejected` but leaves `status=observed` and `fidelity=not-evaluable` even if it is false; the schema-valid-wrong evaluator assigns `fidelity=wrong` unconditionally without checking valid parse, the deliberately mismatched fields, or admitted effects. `CorpusReplay_UsesProductionChatBoundaryAndPersistsAllDeclaredCases` checks only malformed `errorStage` and wrong `fidelity` (`:245-248`). The current artifacts happen to show rejection and a valid Wood/count-2 admission, but the repeatable gate would not fail if either safety boundary regressed. Assert these properties explicitly and produce an `open-gap`/mismatch on failure.
- `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:878-882,1330-1340`: exact-result patrol acceptance is inferred from enqueued `PatrolCommand.UnitIds` plus a generic `PatrolCommand:processed` callback, with no assertion that the two attributed units became patrolling actors or received the intended waypoints. Focused contract check outside the diff: `Assets/Scripts/Core/GameSimulation.cs:6291-6295` skips missing/dead/foreign actors, while `:1853-1856` still publishes the generic processed callback after dispatch. Inspect `IsPatrolling`, waypoints/destination and ownership on the exact two result IDs after native processing; keep the goal's `Executing` state distinct from completion.

#### Minor (Nice to Have)

- `Docs/CommanderPhase5C/task-6-report.md:19`: “25/25 declared turns” conflates 25 cases with 35 declared turns in `evidence/phase5c-replay-corpus.v1.json`; change the wording to “25/25 cases, 35 declared turns” after confirming each turn history. This is a reporting precision issue, not a claim that a case was skipped.

### Declined or out-of-scope observations

- Did not treat 0/10 live paid attempts, absent Windows/Web player screenshots, or absent one full regression/build as green or as an unapproved source defect: the brief/report explicitly reserve scheduling and final source-matched validation for Task 7. The opt-in offline fixture is synthetic; its separate approval is within the explicit fixture flag's scope.
- Did not flag the historical eight-gap run, an earlier invalid ordered response, or unrelated dirty Task 1–5 files. Their provenance is preserved and the current Task 6 package compares exact pre-Task 6 bytes (with the disclosed reconstructed Clarification exception).
- Did not propose a provider protocol change, new planner, hidden-fog lookup, extra paid probe, or broader branch cleanup; none is needed to remedy the above scoped findings.

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** The source and saved native fixtures establish a substantial production-path offline harness, but three targeted evidence-contract gaps could falsely certify rejection, correlation, or patrol execution. Fix those assertions/instrumentation before treating this Task 6 gate as independently accepted; keep Task 7's live, release and served checks open.
