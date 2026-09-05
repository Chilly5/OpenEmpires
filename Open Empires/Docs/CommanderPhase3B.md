# Commander Phase 3B — LLM readiness layer

## Scope and architecture

This phase adds a local, provider-neutral interpretation boundary. No AI API, HTTP client, credentials, voice, speech, memory, personality or network protocol changes are included.

```
Local input ── snapshot once ──> CommanderContextBuilder ──> CommanderContext
     │                                                       │
     └────────────> async interpreter <───────────────────────┘
                          │
                   untrusted JSON
                          │
                   CommanderIntentDTO
                          │
                 DTO validation/conversion
                          │
                    CommanderIntent
                          │
         existing validator → resolver → goal manager → planner
                          │
                  ordinary ICommand
                          │
             existing CommandBuffer / simulation / replication
```

The interpreter receives text, a detached snapshot and a cancellation token. It receives no simulation, registry, live unit/building, mutable goal, command buffer or Unity object. The dispatcher captures state once per submission and invokes the interpreter once; neither is added to the simulation tick loop.

## Context policy

- Copies the owning player's food, wood, gold and stone; population, current cap, absolute ceiling and available capacity.
- Copies live owned buildings, completion/construction flags, canonical production compatibility, queue contents and current training. Foundations retain capability information but are not active production.
- Aggregates owned live units, including garrisoned units, and queued unit types. No live collection is retained.
- Copies age, civilization, researched technologies and Commander unit civilization substitutions/age requirements. These are information, not authority to bypass execution prerequisites.
- Copies all nonterminal owned goals, including pending, blocked and waiting goals, with target and status. No history or planner-internal bookkeeping is exposed.
- Resources use exactly the planner's current-visibility check at the node origin tile. Unexplored and explored-but-not-visible nodes are absent. Visibility cheats remain governed by the existing fog API.
- The current Commander has no enemy-awareness planning. Both hidden and visible enemy rosters/buildings are omitted; this is an explicit policy, not an accidental missing visibility check.
- Snapshot leaves have getter-only values and collections are read-only wrappers over newly built lists. `ToJson()` serializes this same safe surface.

## DTO contract

Canonical wire identifiers are case-sensitive strings, not enum ordinals or runtime type names. Ownership is taken exclusively from the trusted snapshot; `playerId` in model JSON is rejected.

```json
{"intentType":"EnsureUnitCount","unit":"Spearman","amount":10,"constraints":[{"type":"PreferredWorkers","mode":"IdleOnly"}]}
```

Supported intents:

| intentType | Required payload |
| --- | --- |
| EnsureUnitCount | unit: Spearman, Archer or Knight; integer amount 1..maximum population |
| BuildStructure | structure: House, Barracks, ArcheryRange or Stables; integer amount 1..20 |
| SetResourceAllocation | resource: Food/Wood/Gold/Stone; mode: SetExact/Increase; amount 0..maximum population (omitted only for Increase) |

Supported constraints: `PreferredWorkers` with `mode: IdleOnly`; `MaximumQueue` with integer `amount: 1..8` for unit production; `ProtectedResource` with canonical `resource` and optional integer `amount` worker floor. At most one of each is accepted. Omitted protected amount preserves the existing submission-time worker-floor behavior.

The codec rejects unknown fields/types, duplicate properties, incorrect primitive types, numeric enum strings, unsupported units/structures, mismatched variant fields, invalid/duplicate constraints, out-of-range amounts, oversized responses and excessive nesting. Rejections expose `ErrorCode`, `ErrorField`, `Reason` and a null intent. JSON is never deserialized into an internal intent class. Existing live-state validation still runs at resolution after the delay.

## Async and ownership

Create and call the dispatcher on the simulation's Unity owning thread. `SubmitTextAsync` requires its synchronization context, uses a worker for the provider (including its synchronous prefix), and resumes on the owning thread before validation/resolution. No `.Wait()` or blocking task result access occurs in production code.

States: Idle → Submitting → WaitingForInterpretation → Resolving → Executing → Completed. Invalid responses/provider errors/timeouts produce Failed; cancellation before acceptance produces Cancelled without a goal. Executing means a goal was accepted, not that production is finished. Terminal goal events provide Completed/Failed/Cancelled.

Only one interpretation may be pending per dispatcher; duplicate submission returns `SubmissionInProgress`. Once accepted, another request may be submitted while existing goals continue. Displayed lifecycle tracks the latest submission, while response generation retains existing goal associations.

The default timeout is 10 seconds, configurable from 1 ms to 120 seconds. Cancellation/disposal/timeout prevents a late provider response from resolving, even when the provider ignores cancellation. A continuation observes late provider faults. Cancelling interpretation does not cancel an already accepted goal; that remains the goal manager's responsibility.

Notification callbacks are isolated from submission results. A throwing UI observer cannot change an accepted goal into a reported failure, and a throwing provider cancellation callback cannot prevent cleanup. Disposal marks a pending submission Cancelled. Lifecycle association is installed before acceptance notifications, so a callback that immediately cancels the accepted goal cannot leave the UI stuck at Executing.

`SimpleTextIntentParser` retains the optional synchronous compatibility interface, so existing deterministic callers never synchronously wait for an async provider. The mock implements only the async interface, delays 500–2000 ms (750 ms in the debug window), and passes its generated or overridden JSON through the real DTO codec.

## Files and modifications

| File | Purpose and responsibility |
| --- | --- |
| Assets/Scripts/AI/Commander/CommanderContext.cs | New immutable snapshot models and safe context JSON serialization. |
| Assets/Scripts/AI/Commander/CommanderContextBuilder.cs | New submission-time owner/fog-filtered projection using existing read queries. |
| Assets/Scripts/AI/Commander/CommanderIntentDto.cs | New JSON DTOs, strict parsing, structured rejection, validated conversion and round-trip support. |
| Assets/Scripts/AI/Commander/MockLlmIntentInterpreter.cs | New delayed, cancellable, deterministic provider stand-in and malformed-response injection. |
| Assets/Scripts/AI/Commander/CommanderIntentInterpreter.cs | Async interface, optional immediate-parser interface, new transport/lifecycle error codes and error field. |
| Assets/Scripts/AI/Commander/SimpleTextIntentParser.cs | Adds a cancellation-aware async adapter; existing text grammar remains unchanged. |
| Assets/Scripts/AI/Commander/CommanderIntentDispatcher.cs | Adds single-flight async submission, snapshot creation, timeout/cancellation, owner-thread checks and lifecycle reporting. Existing resolver remains the execution gateway. |
| Assets/Editor/CommanderIntentDebugWindow.cs | Async mock submit, thinking/state/result display, cancel button, malformed JSON toggle and Play Mode cleanup. Preserves the older QA session entry point. |
| Assets/Tests/EditMode/CommanderIntentTests.cs | Migrates only the counting test double to the new interface; existing assertions remain intact. |
| Assets/Tests/EditMode/CommanderPhase3BTests.cs | New snapshot, fog, ownership, immutability and DTO regressions. |
| Assets/Tests/PlayMode/CommanderPhase3BPlayModeTests.cs | New async, state, cancellation, failure, live-tick and end-to-end production tests. |
| Packages/manifest.json; Packages/packages-lock.json | Promotes already-installed Unity Newtonsoft JSON 3.2.2 from transitive to direct dependency; no provider SDK. |
| New .cs.meta files | Unity-generated asset identities for the new scripts and tests. |
| Docs/CommanderPhase3B.md; verification artifacts | Architecture, file responsibilities, test evidence and handoff. |

CommanderPlanner, CommanderGoalManager, ICommand implementations, CommandBuffer, GameSimulation and networking are preserved in this phase. Earlier phases already left some of these dirty; their existing changes are not Phase 3B modifications.

## Future provider plug-in point (Phase 4)

Implement `ICommanderIntentInterpreter.InterpretAsync(text, context, cancellationToken)` and inject it into the dispatcher constructor in place of `MockLlmIntentInterpreter`. A future GPT/DeepSeek/etc. adapter may serialize `context.ToJson()` and must pass returned JSON through `CommanderIntentDtoCodec.InterpretJson`. It must not retain or acquire simulation access. Keep provider credentials, transport configuration and API policy outside deterministic execution.

Future work includes selecting a real provider, request schema/versioning, secure credential handling, provider-specific transport/retry/rate-limit behavior, operational limits and a player-facing UX. None is implemented here. Voice, memory and personality remain outside Phase 3B.

## Verification

Verified on 2026-09-05 in Unity 6000.5.9f1, instance `Open Empires@6d7310c7`:

| Gate | Result |
| --- | --- |
| C# compilation | Passed; zero compiler errors in the current Bee build log; refreshed runtime, editor and test assemblies. |
| Full EditMode suite | **170/170 passed**, 0 failed/skipped, 30.941 seconds. Job `88008ff960004a158527e74632c440f4`. |
| Full PlayMode suite | **16/16 passed**, 0 failed/skipped, 8.326 seconds. Job `c0313e06e7534279aa2711aefe53e910`. |
| Prior phase regressions | Phase 1: 29; Phase 2: 40; Phase 3A: 29; Phase 3A.1: 20 EditMode + 3 PlayMode, all passed. |
| New Phase 3B tests | **31 EditMode + 12 PlayMode cases passed**. |
| Other project regressions | 21 EditMode + 1 PlayMode, all passed. |
| No real AI/network integration | Source audit: no provider HTTP/API/key/voice/memory additions; no intent/context references in networking or command execution. |

All cases in the inventory below passed. Exact case names, durations, results and runtime output are retained in [EditMode results](CommanderPhase3B-editmode-results.json) and [PlayMode results](CommanderPhase3B-playmode-results.json). The initial 161-case EditMode attempt encountered two MCP transport error logs during setup; it was not counted as passing. The final complete runs above supersede it without suppressing test errors or changing earlier assertions.

### Demonstration result

Input: `make 10 spearmen`.

The delayed mock generated JSON, the DTO codec produced a validated `EnsureUnitCountIntent`, and the existing resolver registered the goal. The unchanged planner emitted **10 ordinary TrainUnitCommands**, training **10 living owned spearmen**. The goal and dispatcher reached **Completed at simulation tick 3001**.

In the separate nonblocking test, a **502 ms** request overlapped **375 Unity frames and 375 real simulation ticks**. No goal existed while interpretation remained pending. Cancellation, malformed responses, timeout, late responses, ownership rejection and callback failures all passed their dedicated tests.

### Preserved-source check

The following SHA-256 values match the live worktree baseline captured before Phase 3B edits:

- CommanderPlanner.cs: `2005F6E69F6CC9C6C98FAC6FB7E25CA1E9C0F712B77D90F80F7865234004102F`
- GameSimulation.cs: `4DCDB275C3FDF9D229CDDD4F8867D7B6807D6433C2DE66CFFF7653A948AB1CD4`

No Phase 3B edits were made to the goal manager, command execution/serialization, CommandBuffer or networking. The working tree's earlier-phase edits were preserved.

### Phase 3B test inventory

Each parameterized case is listed separately in the machine-readable result artifacts. The following table explains the purpose of every new test method.

| Test | Purpose |
| --- | --- |
| Context_ContainsOwnedResources | Copies all four owned resources, population and capacity; snapshot does not change with live resources. |
| Context_DoesNotLeakHiddenEnemyInformation | Hidden enemy mutations do not change serialized context; an explicitly visible enemy is also excluded under the no-enemy-awareness policy. |
| Context_ExcludesHiddenAndExploredResources | Hidden/unexplored and explored-only nodes are absent; current visible resource values are detached. |
| Context_ContainsActiveGoals | Includes nonterminal targets/statuses and excludes cancelled goals; subsequent goal changes cannot mutate the snapshot. |
| Context_CopiesOwnedProductionGarrisonAndCivilization | Includes garrisoned units, copied queues/current training, construction, civ/age and age requirements; queue is read-only. |
| IntentDTO_SerializesCorrectly | Exact canonical example JSON and typed round trip using trusted ownership. |
| InvalidDTO_IsRejected (17 cases) | Unknown command/unit, wrong primitive/enum types, player injection, invalid amounts, duplicate keys, trailing content, runtime type injection and malformed/nonstandard JSON are rejected. |
| ConstraintDTO_RoundTripWorks | Protected worker floor, idle-only and queue limit survive the JSON boundary. |
| IntentDTO_AllIntentKindsRoundTrip (2 cases) | Resource increase with unspecified count and building intents survive conversion. |
| InvalidConstraintDTO_IsRejected (4 cases) | Rejects unknown/ordinal worker mode, oversized queue, negative resource floor and unknown constraint. |
| ResponseLimits_AndDuplicateConstraints_AreRejected | Response length/depth and duplicate constraint defenses. |
| Interpreter_DoesNotBlockSimulation | Frames and real simulation ticks advance during mock interpretation. |
| Interpreter_CancellationStopsSubmission | External cancellation yields no goal and Cancelled state. |
| Interpreter_InvalidResponseFailsSafely | Malformed delayed mock JSON returns a structured field/code error and no goal. |
| SubmittingState_WaitingState_ResolvingState_ExecutingState_CompletedState | Full normal state ordering and owner-thread callbacks, then goal completion. |
| FailedState_ProviderExceptionAndTimeout | Provider exception and non-cooperative timeout are safe; late completion creates no goal. |
| CancelledState_DisposeAndLateProviderResponse | Dispatcher disposal prevents late response resolution and reports cancellation. |
| CancellationAtResolvingCreatesNoGoal_AndRetryWorks | Cancellation at the final pre-commit state boundary wins; a subsequent request works. |
| SingleFlight_OneSnapshot_OneInterpretation_NoTickInvocations | Duplicate pending submissions are rejected; snapshot is captured before the delay; ticking does not invoke the provider. |
| AsyncResult_StillPassesOwnershipValidation | Even a provider returning a foreign-player typed intent cannot bypass the existing resolver validator. |
| Demo_Make10Spearmen_AsyncDtoToNormalCommandExecution | Real production from delayed text interpretation through DTO, intent, goal and ordinary commands to ten live units. |
| ObserverAndCancellationCallbackFailures_DoNotChangeCommittedSubmission | Throwing observers and provider cleanup callbacks cannot corrupt acceptance or leave the dispatcher permanently busy. |
| AcceptanceObserverCancelsGoal_TerminalStateIsNotLost | An immediately cancelled accepted goal produces a terminal lifecycle state. |

### Debug interface

Open the existing Commander Command window through the Commander editor menu, enter Play Mode and submit `make 10 spearmen`. The window shows the state and `Thinking...` during the default 750 ms mock delay. Use `Cancel interpretation` before acceptance to create no goal; use `Mock malformed JSON` to exercise structured rejection. The accepted goal still depends on the normal economy, production and population rules. No final player UI is introduced.

The automated demonstration uses explicit setup (owned completed TC/Barracks and starting resources), then normal simulation ticks for all training. It is Unity PlayMode execution evidence, not a two-client multiplayer session or visual UI screenshot verification.
