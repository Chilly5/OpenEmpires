# Commander Phase 4C.1 task report

Status: implementation is focused-green at the initial checkpoint; four independent-review findings are now in a test-first fix wave. Final full regression is intentionally pending and will be owned by the orchestrator's audit worker after this focused wave.

## Implemented scope

- Added bounded immutable match-local values in `MemoryEntry`, `CommanderMemory`, and `ConversationState`.
- Integrated UI-owned conversation state with tactical and strategic request history, explicit cavalry preference, strategic decision outcome copies, transcript bounding, clear/reset/reinitialize/destruction semantics, and late strategic-response invalidation.
- Added detached memory to both `StrategicAIRequest` constructor overloads and to `StrategicAIApprovalBridge` through an optional value-snapshot provider.
- Added contextual mock behavior and an explicitly untrusted Gemini memory payload while retaining `StrategicAIContextSerializer` for strategic context.
- No execution or approval authority was added; plans still require the existing approval, decision-policy, transition, and planner path.

## Final public interfaces at the initial checkpoint

- `MemoryEntry`: immutable `Sequence`, `Kind`, `Text`, optional `CavalryPreference`, `Status`, optional `IntentId`, optional `PlanId`, and `Objective` values.
- `CommanderMemory(int capacity = 32)`: `RecordConversation`, `RecordCavalryPreference`, `RecordApprovedStrategy`, `RecordDecision`, `RecordExplanation`, `Snapshot`, `Clear`, and `ToJson`.
- `ConversationState(int playerId, int capacity = 32)`: `PlayerId`, `Memory`, `Snapshot`, `TryGetCavalryPreference`, and `Reset`.
- Both `StrategicAIRequest` overloads accept optional trailing `conversationHistory` and `memorySnapshot` values and detach them on construction.
- `StrategicAIApprovalBridge` retains the reflection-compatible four-argument constructor `(IStrategicAIInterpreter, StrategicIntentIdProvider, Func<StrategicContext>, TimeSpan? = null)` and adds a distinct five-argument constructor whose final value is `Func<IReadOnlyList<MemoryEntry>>`. `TranslateAsync(string, CancellationToken = default)` remains the unique legacy method name. `TranslateWithMemoryAsync(string, IReadOnlyList<MemoryEntry>, CancellationToken = default)` is the explicit detached turn-start path.
- `CommanderChatUI.Conversation` is read-only and `ResetConversation()` is public. `CommanderAIIntentAdapter.ResetHistory()` is public.

## Preserved before-state and baseline

- Existing-file full-text snapshots and hashes: `Docs/CommanderPhase4C/phase4c1-task1-before/`.
- Source baseline: `Docs/CommanderPhase4C-source-baseline.json` (246 files).
- Baseline EditMode job `f858ad6eaf01466f9bf7544c4af70fb3`: 503/503 passed; evidence `phase4c1-task1-baseline-editmode.json`.
- Baseline PlayMode job `6a30f5a8c7c94ada8d1dcc495d5142de`: all 52 test bodies completed, but suite result failed on an unrelated Unity Package Manager OAuth/TLS log; evidence `phase4c1-task1-baseline-playmode.json`. No suppression or credential/package/network change was made.

## TDD evidence

- Initial assertion RED EditMode job `3f6bb2794532405ba23a7acca593c049`: 8 run, 6 intended behavioral failures; evidence `phase4c1-task1-red-editmode.json`.
- Integration RED PlayMode job `3f4939af4eb04e899435ef5eeff85c9d`: 9 run, 2 intended failures (current request duplication and stale strategic publication).
- Initial focused GREEN EditMode job `b6b47ccfe8da42cdb401c1c2bfb22f98`: 9/9 passed.
- Initial focused GREEN PlayMode job `e1c2aad328cf45ffb949c247c113fc46`: 11/11 passed.
- An interim full EditMode job `d026e6f4ecfa4ad397d0253a0cda0077` was lost across Unity reload; the runner later returned Unknown job ID. It is not counted as passed and will not be rerun as a checkpoint.

## Review fix wave

Focused RED PlayMode job `1ad433fe194e4ed888d22df26c63ee4e` completed 16 tests: 10 passed and six assertions failed across the four reviewed behaviors. MCP progress became stale after the actual run; the complete Unity `TestResults.xml` result was recovered through `Application.persistentDataPath` and preserved in `phase4c1-task1-review-red-playmode.json`. A subsequent overlapping initialization job `13707efc1c8840f2b3387a1418ddf8d6` failed to initialize and is not test evidence.

1. Added adapter generations. `ResetHistory` invalidates the active generation, clears history, and makes the adapter available; late success, rejection, exception, timeout, and cancellation paths cannot append or dispatch for a stale generation.
2. `InitializeStrategic` validates interpreter, pipeline, and player ownership before mutating valid state. Replacing a different valid pipeline resets conversation, transcript, tactical/strategic history, pending state, and stale results.
3. Strategic outcome copying now classifies `StrategicDecisionStatus.Rejected` and `NoDecision` before consulting the transition flag. Approved memory still requires `Submission.CreatedPlan == true`.
4. The UI captures the bounded memory at turn start before appending the current player turn and passes that explicit detached snapshot to the bridge. Explicit snapshots skip the legacy current-turn stripping heuristic. Normal memory eviction remains effective for later turns; no secondary preference cache was added.

Focused GREEN evidence after all four fixes:

- EditMode job `c7ac8508e6cb4c8a829b1df293ffec6b`: 9/9 passed in 2.2669861 seconds; complete per-test evidence `phase4c1-task1-focused-editmode.json`.
- PlayMode job `6c29c14fb2ba4312a56720c2ddc1cbe4`: 16/16 passed in 4.6336658 seconds; complete per-test evidence `phase4c1-task1-focused-playmode.json`.
- Unity compiler console after the production patch: zero errors.

## Remaining validation

- Source is frozen after detailed focused EditMode and PlayMode GREEN.
- The orchestrator's audit worker will run the final full EditMode/PlayMode regression pair. No final full-suite phase gate is claimed yet in this report.

## Current concerns

- No live paid Gemini request is part of this phase; request payload construction is inspected in tests.
- The baseline Package Manager authentication log is environmental and remains unsuppressed.
- This report covers Phase 4C.1 only; explanation and richer context phases are not claimed.

## Review fix round 2

Before editing, the complete fix-round-1 contents of `StrategicAIApprovalBridge.cs`, `CommanderChatUI.cs`, and `CommanderPhase4C1PlayModeTests.cs` plus SHA-256 hashes were preserved under `Docs/CommanderPhase4C/phase4c1-fixround2-before/`.

Two review blockers were corrected:

1. Reflection compatibility is restored exactly: the original four-argument bridge constructor exists, the memory-aware constructor is a separate five-argument overload, and `TranslateAsync` has exactly one two-argument method shape. The UI uses the distinctly named `TranslateWithMemoryAsync`; explicit snapshots continue to skip the legacy current-turn heuristic.
2. The bridge now normalizes internal whitespace before its generic-attack no-guess gate. A custom valid-cavalry provider cannot be called for `prepare    attack` without a retained preference, while the same request with a retained cavalry preference produces only a pending preview.

TDD and focused evidence:

- Whitespace RED PlayMode job `f41f1470a315408b801369de4e6ffd50`: 1/1 failed as intended because the provider was called once instead of zero times; evidence `phase4c1-task1-fixround2-red-playmode.json`.
- The pre-fix full EditMode result reported by the orchestrator was 509/512; the three failures were the existing Phase 4B.2 reflection compatibility tests. No existing Phase 4B.2 test was changed.
- Focused EditMode job `79591d15af904ad1bad9d2115ededcd8`: Phase 4B.2 plus 4C.1, 78/78 passed in 22.1602732 seconds; complete details `phase4c1-task1-fixround2-focused-editmode.json`.
- Focused PlayMode job `06b0dd1130c64e84afb41dce30f4a35e`: Phase 4B.2 plus 4C.1, 20/20 passed in 7.5301487 seconds; complete details `phase4c1-task1-fixround2-focused-playmode.json`. Unity MCP returned each detail twice while its authoritative summary reported 20, so the evidence artifact records that anomaly and deduplicates details by full test name.
- A preceding PlayMode initialization job `66da19aa648d4c8385fc4a6353aa4359` recompiled/reloaded but did not start tests and timed out during play-mode transition. After confirming tests were not running, the editor was returned to idle and the focused suite was started once successfully.
- Unity compiler console after fix round 2: zero errors.

Fix-round-2 source is frozen for independent full regression. No full suite was run by this worker in this round.
