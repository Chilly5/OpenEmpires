# Phase 4D.4 Task 1 — detached transition value and bounded feed

## Task / Agent / Expected

- Task: Task 1 only, pure `StrategicAdvisory` value and `StrategicAdvisoryFeed` over detached `StrategicPlanHealthSnapshot`.
- Agent: `/root/phase4d4_task1_feed`; sole production writer and Unity runner for this slice. No planner, host, package, scene, settings, credential, or Task 2 changes were made.
- Expected: first sample silent; owner/plan/creation identity copied; revision **or** tick regression rejected; typed status, health category, and resource blocker identity transitions; changing positive deficit does not retrigger; deterministic order; at most four outputs per observation and 32 retained identities; reset clears state; no live snapshot/authority reference or invented `Unknown` cause. `NeedsPlayerDecision` is unavailable in the Phase 4D.2 health category model and is not guessed.

## RED

The first exact-ID job `6649b4cb84404b869ef7430633fed8fe` discovered 0 and is **not RED**. After forced asset refresh, Unity MCP EditMode job `0ce252a1b06b42269aa936b9a1d2b84b` discovered all eight required exact IDs and failed 7/8 against the compiling, deliberately empty feed body; `Advisory_RemainsFogSafe` passed with that empty body. The meaningful assertion failures were missing resource wait, repeat suppression's required first emission, recovery, completion, cap output, version-safe first emission, and reset/recovery emission. This was behavioral RED, not a compiler or discovery failure.

An additional edge-test patch initially produced `CS1503` at test line 179; Unity kept running the previously compiled eight-test assembly. Jobs `1bdd5319ff5c44cea9176468368fcd67` (1/1 discovered) and `990d45ded2ae4cce8eef974696f23297` / `0b33fbb1b7f443a4803c178f20c4c85e` / `5c62da8c968d43c3b087a4cbd68902db` (8/8 previously compiled tests) are **not** evidence for the new edge assertions. `Logs/Editor.log` exposed the compiler error when `read_console` returned none. I corrected the assertion type, forced compilation, and confirmed `unity_reflect` discovered the ninth method. Unity MCP job `8a2a394270174b32a2671f767e86f4c9` then discovered 2/2 new edge tests and failed both behaviorally: `Advisory_PauseResumeDoesNotInventRecovery` emitted a second transition on pause, and `Advisory_RemainsFogSafe` emitted a recovery after an `Unknown` primary with a secondary resource hint.

Every test names the production mutation it catches in a comment. Test helpers use a real simulation, submitted plan, context builder, copied health construction, and one real planner capture for fog. They do not mock the feed.

## Implementation

- Added immutable advisory values (copied owner, plan, creation tick, observed tick, revision, typed transition, bounded display).
- Feed stores only scalar plan state and copied typed wait keys. It seeds first observation silently, compares status and typed blocker keys, ignores deficit *amount* changes while the resource key remains positive, emits terminal transitions once, and reports recovery only from an evidenced prior wait.
- Fixed-order status, recovery, then new-wait output is capped at four. Wait keys sort by enum/resource ordinal. Oldest insertion is evicted at 32 identities; `Reset()` clears them.
- A lower revision or lower observed tick is rejected independently. `Unknown` primary discards secondary hints and makes no causal claim. Pausing preserves prior wait keys and emits only the pause transition; resuming an unchanged wait emits only resume.
- No planner submission, lifecycle control, goal/command/provider call, world read, wall-clock use, or live health reference is present in the new production file.

## GREEN / compiler / evidence

- Required exact IDs: `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`, `Advisory_RemainsFogSafe`.
- Focused first GREEN: job `1fe9208072554350b57143631da00562`, **8/8 passed**, 0 failed/skipped, terminal MCP result `Passed` with all eight individual results. This preceded the two extra edge-test changes.
- Final 4D.4 group GREEN: job `53b645d1dfe54b7380512e5b251572af`, **9/9 passed**, 0 failed/skipped, terminal MCP result `Passed`; this includes the eight exact IDs plus `Advisory_PauseResumeDoesNotInventRecovery`.
- Relevant Phase 4D.2 health EditMode regression: job `47526b8f6c3842b1bd9cc79d0aaea408`, **42/42 passed**, 0 failed/skipped, terminal MCP result `Passed`.
- No native XML artifact was returned for these MCP jobs. Exact job IDs, discovered counts, failure messages, and terminal results are recorded above and in the MCP call history; failed jobs returned `result=null` but had terminal `progress.completed=total` and named `failures_so_far`.
- After final source refresh, Unity reflected the ninth test method and ran the current assembly. Latest `mcpforunity://editor/state` showed `is_compiling=false`, `is_domain_reload_pending=false`, last reload after the fixed compile. `read_console(types=[error])` contained test teardown cancellation log entries classified as `Exception`, but no C# compiler diagnostic. The earlier `CS1503` remains historical in `Logs/Editor.log`; no later `error CS` appeared, and current nine-test plus 42-test assemblies executed successfully.
- Reflection assertion in `Advisory_RemainsFogSafe` checked fields of the advisory/feed for `GameSimulation`, `StrategicPlan`, `StrategicPlanHealthSnapshot`, `CommanderGoalManager`, `StrategicPlanner`, or `CommanderContext` direct references. Static source inspection found `StrategicPlanHealthSnapshot` only in `Observe`/helper parameters, not a stored field. Nested retained state is scalars plus typed wait keys.

### Frozen hashes (SHA-256)

- `Assets/Scripts/AI/Commander/Strategic/StrategicAdvisory.cs`: `3BB96D7CF7B51D8E1E7104561BCEF7333887F431D2FA964121AA0FD21E55DCDC`
- `Assets/Tests/EditMode/CommanderPhase4D4Tests.cs`: `3AEF620A34B19AD52FCAF056A874BE0C7C21408AECD7E31FDF665CA5F76B1AB2`

## Self-review

The new production file contains only `System` collections/culture dependencies and the copied health input type. All advisory properties are get-only; displays use typed known facts and invariant numeric formatting, capped at 200 characters. The feed's only mutable state is its bounded per-plan history. The current full-suite regression, host transcript/presenter, provider/authority tests, and runtime scenarios are Task 2/later gate work, not claimed here. The existing dirty workspace was preserved; nothing was staged, committed, reset, or cleaned.

## Concerns / handoff

- Focused tests use constructed health values over a real submitted plan for controlled category transitions; they are not an in-game scenario or proof that the planner naturally produces every category sequence.
- The compiler-error interval showed that MCP console can miss test-assembly compile failures while old assemblies still run. Use discovery counts and `Logs/Editor.log` when editing tests, and do not count the stale jobs above.
- Independent scoped review of detached value, fog safety, deduplication, and resource-key transition semantics is requested before Task 2 begins. No Task 2 work was started by this agent.

## Independent review fix round 1 — Unknown retention and typed resource discrimination

The independent scoped review found two Important issues. First, a nonterminal `Unknown` primary produced no causal advisory but still overwrote `previous.Waits` with an empty list. That made known Wood → Unknown → same Wood emit a duplicate wait, and known Wood → Unknown → Healthy lose the expected recovery. Second, the existing recovery test covered population → Healthy, not the harder typed-resource cases where one blocker recovers while another remains or Wood changes to Gold. I verified the first path in `Observe`'s `CopyWaits`/state assignment and added behavior-first tests before the production fix.

### RED and mutation evidence

- `Advisory_UnknownDoesNotSettleKnownWait` covers both known Wood → Unknown → same Wood (no duplicate) and known Wood → Unknown → Healthy (one Wood recovery). `Advisory_ResourceRecoveryWhileAnotherWaits` covers Wood+Gold → Gold (one Wood recovery). `Advisory_ResourceIdentityChangeIsTransition` covers Wood → Gold (ordered Wood recovery then Gold wait). All three use copied health construction over a real submitted plan and literal typed/text assertions.
- Unity MCP job `d9ef0e9598384f38ab1c3c3160ccb4e5` discovered 3/3, terminal `failed`; the `Unknown` test failed with `Expected: <empty>; But was: <StrategicAdvisory>` at the same-Wood check. The two resource tests passed against the then-current typed-key implementation. Unity reflection had confirmed all three new methods were loaded before this job.
- To prove the resource tests reject a realistic category-only mutation, I temporarily removed `Resource` from resource wait keys in production using a one-line patch. Unity MCP job `8605113f3e234867bff435f58e40384c` discovered 2/2, terminal `failed`, with both typed-resource tests failing at their expected transition arrays. This was a deliberate mutation check, **not** the original-source RED; the typed key was restored in the final source.

### Fix and GREEN

- The narrow production fix updates retained wait keys only when the primary health is known or the plan is terminal. Nonterminal `Unknown` still advances accepted revision/tick/status, emits no causal claim, and now preserves the prior typed blocker keys for later comparison. No new authority or world reference was introduced.
- After this fix, exact new-case job `cc970864396d41379d1ea37e82d63aed` discovered **3/3 passed**.
- The first whole-group job `c15267f2f18148a692885f114283e828` discovered 12/12 and failed only `Advisory_RemainsFogSafe`: its old fixture had seeded a *real known resource wait*, then expected no recovery after `Unknown` → `Healthy`. That expectation conflicted with the reviewed preservation rule. I reset and silently seeded a `Healthy` state before the test's no-prior-cause assertion, preserving its real fog-capture check separately. This was a test-fixture correction, not a second production change.
- Final whole 4D.4 EditMode job `fe328b0245e147e68b7306f71767c0c7`: **12/12 passed**, 0 failed/skipped, terminal `Passed`.
- Affected Phase 4D.2 health EditMode regression job `c423dbc374de4aa485bde435d96cdf40`: **42/42 passed**, 0 failed/skipped, terminal `Passed`.
- MCP returned terminal payloads with job IDs, counts, and `failures_so_far`; no native XML artifact was returned. Current `OpenEmpires.EditModeTests.dll` write time was `2026-09-25T21:20:43.9000827Z`. Latest editor state showed `is_compiling=false`, `is_domain_reload_pending=false`, and domain reload after the final source/test import. `Logs/Editor.log` contains only the historical Task 1 test-assertion `CS1503`; no later `error CS` was found, and current 12/12 plus 42/42 assemblies executed.

### Round-1 frozen SHA-256

- `Assets/Scripts/AI/Commander/Strategic/StrategicAdvisory.cs`: `44B513AA877D2F2C80EA6A5175091AFD41BE077AFA57EC8B0225ECA7ADD11F67`
- `Assets/Tests/EditMode/CommanderPhase4D4Tests.cs`: `0BF83039EFC0C8852EF8162D074D4A797741828DDB5E612ADA6CA731356800FE`

### Remaining concern

- The reviewer noted a Minor test gap: `Advisory_IsPlanVersionSafe` checks an older revision with an older tick and a lower tick at the current revision, but not a lower revision carrying a *newer* tick. The production condition independently compares each field (`revision < last || observedTick < last`); this round records the test gap without expanding beyond the two Important fixes. Task 2 and host/runtime gates remain untouched.

## Root terminal-evidence preservation

After the handoff, the root polled the same four terminal Unity MCP job IDs without rerunning tests and preserved their complete returned payloads:

| Job | Durable payload | SHA-256 |
|---|---|---|
| Initial behavioral RED 8 discovered / 7 failed | `phase4d4-task1-red-initial-0ce252a1.json` | `67AE7AFB607D94E143960C250C6AEF68CC68A78A8C58A7E68CB2EC15A665D743` |
| Edge behavioral RED 2 discovered / 2 failed | `phase4d4-task1-red-edge-8a2a3942.json` | `64C0115148DAEEB4CFADD5EBBF4A2B32C3514D5E5C6FCEF1E73C2F0E906E8D98` |
| Final focused GREEN 9/9 | `phase4d4-task1-green-53b645d1.json` | `F5E4032AE4EE74F420E9602819BC3FAA4476A15662A55B66401BF31CB1A7D432` |
| Health regression GREEN 42/42 | `phase4d4-task1-health-regression-47526b8f.json` | `DCC64847D82E113F66E6D106C15D6820D8FD4545D475C181A43F074EE56EBBA2` |

The root also polled and saved the same four terminal fix-round job IDs after the implementer's handoff, without rerunning tests:

| Fix-round job | Durable payload | SHA-256 |
|---|---|---|
| Unknown-state behavioral RED, 3 discovered / 1 failed | `phase4d4-task1-fix1-red-unknown-d9ef0e95.json` | `4713BB160726BC34FFA3B2006A4C0166F1662891F0E239A9794C8A27B925B445` |
| Typed-resource mutation RED, 2 discovered / 2 failed | `phase4d4-task1-fix1-red-resource-8605113f.json` | `0FF69BDE3E57EDEE49C8A5907EEDB758ACDB3FC1939454BE45F4430E4CC4A01D` |
| Final advisory GREEN 12/12 | `phase4d4-task1-fix1-green-fe328b02.json` | `EFF8D929EB1AE315FD69A499DB317918AB65AA514C6DD3C4F68B3B805109E643` |
| Affected health regression GREEN 42/42 | `phase4d4-task1-fix1-health-regression-c423dbc3.json` | `27E3B458AB474ED7074AF01EB80AECCFA00EDDC0DD51E8D19B0C1DE066DB7E60` |
