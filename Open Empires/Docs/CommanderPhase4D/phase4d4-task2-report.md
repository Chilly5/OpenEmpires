# Phase 4D.4 Task 2 — host integration report

## Agent / Task / Expected

- Agent: Task 2 host implementer (sole production writer and Unity runner).
- Scope: transcript-only `StrategicAdvisoryFeed` presentation and real-host PlayMode proof. Task 3, final full suites, commits, staging, package/settings changes, and unrelated scenes were not touched.
- Expected: observe every active owned plan once per simulation tick outside selected-plan UI gating; observe exact status, milestone, child-goal, and reservation event plans independently of the tick scan, including terminal and same-tick states; reject stale identity/generation; reset feed at host lifecycle boundaries; leave provider and gameplay authority unchanged.

## RED

- Wrote real-host `Advisory_DoesNotExecuteAnything` and `Advisory_DoesNotCallProvider` before production integration. Unity PlayMode job `53caad64567141b9b486b9e6232dad96`: 0/2 passed, both failed on the intended missing `Plan paused.` transcript text after a real approved plan paused; no setup/compiler failure.

## Implementation

- Added `CommanderChatUI.Advisories.cs` as the detached health-to-feed-to-transcript presenter. It checks current pipeline, owner, exact `GetPlan(id)` reference, creation tick, revision, and runtime generation before rendering only feed-copy text through `AppendLine(..., false)`.
- Inserted the all-owned-plan periodic scan before the selected-plan `LateUpdate` early return. Kept event observations separate, so a same-tick state transition is not discarded by the scan gate.
- Routed existing planner callbacks to the presenter and reset feed at `Initialize`, same/different-pipeline `InitializeStrategic`, `ResetConversation`, and `OnDestroy`. `Initialize` replaces the simulation tick source; `OnDestroy` releases it.
- No bridge approval, provider, planner submission, goal mutation, command, or lifecycle-control call was added to advisory production code. Existing dirty Phase 4D.3 edits in shared host files were preserved.

## GREEN

- Focused host PlayMode job `07b1985a12e040c095893758b3b5f278`: 9/9 passed. Cases: `Advisory_DoesNotExecuteAnything`, `Advisory_DoesNotCallProvider`, `Advisory_PauseResumeTransition`, `Advisory_PopulationRecovery`, `Advisory_CancelAndComplete`, `Advisory_NonSelectedPlanTransition`, `Advisory_SameTickEventsAreNotLost`, `Advisory_SamePipelineReinitializeClearsState`, `Advisory_ResetRejectsOldProviderAndEvent`.
- Affected EditMode job `59cb05312aec46d08110f732f979ab6b`: 144/144 passed (Phase 4C.2, 4D.1, 4D.2, 4D.3, 4D.4 values).
- Affected PlayMode job `e63a8ed660d54161b06b1a487b4ba3b6`: 73/73 passed (Phase 4C.2 explanation, 4D.1 controls/runtime, 4D.2 health/runtime, 4D.3 host/runtime, 4D.4 host).
- First run after test-file updates twice timed out during Unity Test Runner initialization before any tests started (`9a5e29dd77a0476bb8087d5deffb0333`, `dabcfa9c7663483fb2cf84bcda0e55f9`, `1ab95928447348cf92826b2f167366f9`); immediate subsequent focused runs started and passed. These were not counted as test failures.
- Unity compiled and executed all named tests. Final console error-category query contained no C# compiler diagnostic; it contained 46 expected test-fixture cancellation logs emitted by `CommanderGoalManager`/`StrategicPlanner` as `Exception` entries. Thus the observed C# compiler-error count is zero, not a claim of a completely empty error-category console.

## Source hashes (SHA-256, final Task 2 snapshot)

| File | SHA-256 |
| --- | --- |
| `Assets/Scripts/AI/Commander/Strategic/StrategicAdvisory.cs` (accepted Task 1, unchanged) | `44B513AA877D2F2C80EA6A5175091AFD41BE077AFA57EC8B0225ECA7ADD11F67` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Advisories.cs` | `7055A0797A735A654EDB013CF3D4B70D2842AA1CE00BBF1EBC8D3958E71E3FFC` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs` | `00B953750289A1F56A07B00B901F2265C0C8F6A7A76195BACD837FA2960B84C8` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` | `686F1D2324F5E541AE1F27832BE6961F7CC727CF4AD579E69F253718DBA9CE81` |
| `Assets/Tests/PlayMode/CommanderPhase4D4HostPlayModeTests.cs` | `352CC41E1604624318551B01817A6D342A00BDE8B5F5BC996852375F6ED7CDB9` |

## Self-review / Concerns

- Advisory presenter only consumes `CapturePlanHealth(owner,id)` detached values and appends feed display text with `remember:false`; no mutable event-plan reason/text is rendered, and no enemy or fog state is read.
- Event identity checks prevent an old plan object from colliding with a reused numeric ID. The reset PlayMode case holds a provider reply, resets, replaces the pipeline with a new planner whose first plan reuses the ID, invokes the old callback, and verifies no leaked advisory or approval.
- The named host tests exercise ordinary deterministic fixture work (plan approval/control, milestone completion, direct unit/house changes) separately from observation; several cases assert the full authority counts, while cases with deliberate planner-control side effects assert the relevant stable counts and transcript. Independent Sol review is still required for the formal 4D.4 gate.
- `advisorySimulation` comes from `Initialize`; standard bootstrap and test fixtures pass a matching pipeline. The host does not expose a public simulation identity from `StrategicPipeline`, so a caller deliberately passing a pipeline for a different simulation but the same player ID would bypass that relation's explicit validation. This is outside the existing `InitializeStrategic` owner check and should be examined in independent review rather than silently claimed proven.

## Independent review fix round 1 (2026-09-26)

The independent scoped review graded two issues Important: a same-owner pipeline could belong to a foreign simulation, and the original non-selected test only exercised an event callback rather than the periodic health-only path. The foreign-simulation concern above is resolved by the change below; the reviewer still needs to rereview the fixed source and evidence.

### Behavior-first RED and mutation proof

- Added `Advisory_ForeignSimulationPipelineRejectedBeforeHostMutation`: two real `GameSimulation` instances share owner ID 0. The existing host has an approved plan and transcript. Passing a pipeline backed by the foreign simulation must throw before transcript, conversation, subscriptions, or approved-plan behavior change. Job `b3f5b44062f64c92adcb0ba6d3db296f` discovered 1/1 and failed behaviorally: expected `ArgumentException`, got none.
- Added `Advisory_NonSelectedHealthOnlyChangeUsesPeriodicScan`: two active plans, the selected UI remains on plan 1, plan 2 reaches its Force child-goal milestone; direct owned-unit creation makes its detached health `WaitingForPopulation` without a planner status/milestone event. Advancing the actual simulation tick then invoking the host scan must append plan 2's population wait without changing authority counts. The pre-fix scan already handled this path, so the new case initially passed 1/1 (`af7ed3952975423e945a29bb02690cc3`). To verify test sensitivity, a temporary selected-plan-only scan mutation produced a behavioral 0/1 (`38c4118c087c4e08986dffa2cdaab1f5`, transcript remained unchanged). The mutation was immediately restored; final StrategicControls SHA-256 matches the pre-fix hash exactly.
- Non-test infrastructure attempts `e49ea5ee69aa433b983418f80bf8d817` (0 discovered during Unity bridge reload) and `7adc446aef294490b6ef9f7efa4da346` (runner initialization timeout before discovery) were not counted as behavioral RED.

### Minimal production fix and protected boundary

- Added only this read-only internal predicate to protected `StrategicPlanner.cs`, adjacent to `PlayerId`:

  ```csharp
  internal bool UsesSimulation(GameSimulation candidate) => candidate != null
      && ReferenceEquals(goalManager.Simulation, candidate);
  ```

- `CommanderChatUI.InitializeStrategic` now calls that predicate after its existing owner check and before `ResetConversation`, generation increment, bridge disposal, pipeline detachment, transcript changes, or feed reset. A mismatch throws `ArgumentException(nameof(pipeline))`. No planner execution, approval, provider, command, goal, reservation, or plan lifecycle logic changed.
- Protected planner SHA-256 before: `47171C6D433B54A78FF98EF900AA1C57807081C1A34F51CDAEBD08B6591177EC`; after: `3BBC3D57AF7F0118B89688A6BEB6908917EDD3E9E7B647E7877534443EA07205`. The Task 1 feed remains `44B513AA877D2F2C80EA6A5175091AFD41BE077AFA57EC8B0225ECA7ADD11F67`. The protected-file delta is exactly the two predicate lines above; no simulation object is returned to the host.

### GREEN and affected regressions

- New focused pair: `6ca1afc354ca452fae9376085400483d`, 2/2 passed.
- Full Task 2 host suite: `afb7086061814a128a98c953378d95ff`, 11/11 passed, 0 failed/skipped. An immediately preceding runner-start attempt `0c41ed985480430cbf7d2b2c6377658e` timed out before test discovery; the successful job is the behavioral result.
- Affected EditMode Phase 4C.2/4D.1/4D.2/4D.3/4D.4 values: `fff943e1c8794d31affa5f9068b7ea1a`, 144/144 passed, 0 failed/skipped.
- Affected PlayMode Phase 4C.2/4D.1/4D.2/4D.3/4D.4 host/runtime: `9bcf821fd25241b89f0749d7a14a3d90`, 75/75 passed, 0 failed/skipped.
- Final Unity Editor state was idle, not compiling or reloading. Error-category console query returned 46 fixture cancellation log entries, with 0 C# compiler diagnostics; this is not a claim that the console is otherwise empty.

### Final fix-round SHA-256

| File | SHA-256 |
| --- | --- |
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` | `3BBC3D57AF7F0118B89688A6BEB6908917EDD3E9E7B647E7877534443EA07205` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs` | `170194321FFB7624ABDF6DC95EB48C6B6B8C07D80720E0EF712B5372A90F5276` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` (unchanged from initial Task 2) | `686F1D2324F5E541AE1F27832BE6961F7CC727CF4AD579E69F253718DBA9CE81` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Advisories.cs` (unchanged from initial Task 2) | `7055A0797A735A654EDB013CF3D4B70D2842AA1CE00BBF1EBC8D3958E71E3FFC` |
| `Assets/Tests/PlayMode/CommanderPhase4D4HostPlayModeTests.cs` | `013E1E98E5DFEEFCCAFB99D486A7A60E08FF6320BE8BE8DF14026B1E77C2D2DB` |

No Task 3 or final full suites were started in this fix round. The deferred minor per-case count assertion gap remains for the final whole-phase review.

## Root terminal-evidence preservation

The root polled the same terminal Unity MCP job IDs after handoff, without rerunning tests, and saved complete returned payloads:

| Job | Durable payload | SHA-256 |
|---|---|---|
| Behavioral RED, 2 discovered / 2 failed | `phase4d4-task2-red-53caad64.json` | `06EB27AD00E7911D7EFE6DF3742EB49E45133BBDA07A3779D351634726E4DF52` |
| Focused host GREEN 9/9 | `phase4d4-task2-focused-07b1985a.json` | `0461D87B0809E3D399F36A6DEE9D9629ADED063C1BFF75E582CD0CBABD3A79DE` |
| Affected EditMode 144/144 | `phase4d4-task2-affected-edit-59cb0531.json` | `FD569A014D51D19F2D7410551B6DDA58A2A2F98AA4285D239E411A1149BEBE8C` |
| Affected PlayMode 73/73 | `phase4d4-task2-affected-play-e63a8ed6.json` | `F0F4305C563C6250CC870B913EAD98AA8EA87760DE0917784CEEF59FC00A635B` |

After fix-round handoff, the root polled the same six terminal job IDs without rerunning tests and saved their full returned payloads:

| Fix-round job | Durable payload | SHA-256 |
|---|---|---|
| Foreign-simulation behavioral RED, 1/1 failed | `phase4d4-task2-fix1-red-foreign-b3f5b440.json` | `13EABCCD62C5E9E33746B9C4151BBF03F05A51C9C4FF77F8B4F0FF25E62D487E` |
| Selected-only scan mutation RED, 1/1 failed | `phase4d4-task2-fix1-red-scan-mutation-38c4118c.json` | `81552BE20C00812168666CCAB0D8FB0FDC696BA1886639422790FEDC053B1C5F` |
| Focused pair terminal `succeeded`, 2/2 progress with no failures; MCP `result=null` | `phase4d4-task2-fix1-focused-pair-6ca1afc3.json` | `3DB3E1A406509430232A3AB73F4348B7B54BBD848090C35C94CE9B3512B281EE` |
| Full host GREEN 11/11 | `phase4d4-task2-fix1-host-afb70860.json` | `D166CB28CACF60451DC3BD28361EFFEEED3A16DB592D010388D9287C20F995F8` |
| Affected EditMode 144/144 | `phase4d4-task2-fix1-affected-edit-fff943e1.json` | `74A8EAD3A27C7C39E7AB83FB47052B0AC6E184686B1092B3A0F9495F2293C6C0` |
| Affected PlayMode 75/75 | `phase4d4-task2-fix1-affected-play-9bcf821f.json` | `4404F6BAC6C56DCFD44CC0E495BF0B9DBBE82E4E60F0F8B2761C40B7CD0053AD` |
