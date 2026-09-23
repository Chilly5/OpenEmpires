# Phase 4C.3 Task 1 report

## Scope and inherited gate

- Task: owned-state insights and actual provider integration only.
- Phase 4C.2 entering gate: focused EditMode `4568d656eaaa490798eb59bbad443533` 18/18; focused PlayMode `a25af57761a44dc5bdc481ae98a18824` 6/6; full EditMode `e828d0a94df44912a78e740277d896a8` 530/530; clean full PlayMode rerun `0a3decd672b648e8a87a2f8f7f923c31` 75/75.
- No repeated baseline suite was run.
- Unity instance at task start: `Open Empires@6d7310c7`, Unity 6000.5.9f1, idle and ready for tools.

## Before snapshots

Snapshot root: `Docs/CommanderPhase4C/phase4c3-task1-before/`.

| Existing file | SHA-256 before edit | Snapshot match |
| --- | --- | --- |
| `Assets/Scripts/AI/Commander/Strategic/StrategicContext.cs` | `6cb890602b3ddbc15b5af499e1048c59bb828a38ff0a4714563ba980d28ab897` | yes |
| `Assets/Scripts/AI/Commander/Strategic/StrategicContextBuilder.cs` | `7644837d2390647c3f491c4176f32cf90b6e3a381e0e138f942544c230aae051` | yes |
| `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIContextSerializer.cs` | `b59a1b0a537b2efee256bf6e8150ecebb663d0e8014760f64e9db55f9c771ed9` | yes |

## TDD evidence

- EditMode RED `b18adfe89a0141c1916739dd0e5bd576`: 7 discovered, 1 passed, 6 failed at assertions against the API skeleton. Worker activity, milestone counts, input validation, deterministic payload, and absent explicit provider fields failed as intended. The fog test also exposed a defective test-only JSON slicing helper.
- EditMode reruns `8f6272f15fa3430eb4a04e0931f00270` and `36350cdcf8f444059738e81bbad735fb` repeated the old six failures because the EditMode test DLL was stale. `Logs/Editor.log` identified `CS0246` on newly added Newtonsoft imports in the test helper; the test assembly has no Newtonsoft reference. The helper now uses `JsonUtility` without changing assembly settings. These two reruns are not GREEN evidence.
- Full per-test XML for the latest stale-assembly RED is preserved as `phase4c3-task1-red-editmode-TestResults.xml` (7 total, 1 passed, 6 failed; SHA-256 `9aaa06ed75640aee0bf6c42397c97921b251c44dcec7092c19e706f5bf7b3eaa`).
- Test-inclusive clean compilation rebuilt both test assemblies; the editor completed a domain reload. After that, EditMode `2d1af7e474f44c85b8bca622346b1349` ran current implementation: 6/7 passed; the remaining failure was a test-only NUnit `Has.Count` constraint applied to an array. The corrected assertion uses `Length`. XML: `phase4c3-task1-first-implementation-editmode-TestResults.xml`.
- EditMode `a433e44197f544c6b28735f48f997ade` passed 7/7. XML: `phase4c3-task1-focused-green-editmode-TestResults.xml`.
- Additional integer-range RED `2943e190a84344729ff5f6660e073c7d` failed 1/1 at the intended assertion: two separate military types with `int.MaxValue + 1` owned units did not throw. XML: `phase4c3-task1-overflow-red-editmode-TestResults.xml`. A `ToInt(totalOwned)` guard was then added.
- Initial PlayMode `fd0662f1326a4ab59cc6ee13ec29654f` passed the chat-provider test but failed the runtime test's hard-coded completed-milestone expectation. Rerun `6c642e07e13943c98078a4cb50498057` showed that one explicit `CompleteMilestoneAndAdvance` can recursively complete already-satisfied following milestones. The revised test asserts real completed-milestone growth and compares the detached snapshot to actual statuses. XML: `phase4c3-task1-first-playmode-TestResults.xml` and `phase4c3-task1-second-playmode-TestResults.xml`.
- PlayMode `8b216e321c0343cfb65939d1fc7b9a27` passed 2/2. XML: `phase4c3-task1-focused-green-playmode-TestResults.xml`.
- The first additional audit found `StrategicContextBuilder` summed detached worker counts in `int` before passing them to the insights builder. RED `ea109dcb8e3a40b18238e6e7a0243a2d` failed 1/1 at the intended overflow assertion (`int.MaxValue + 1` wrapped and raised `ArgumentOutOfRangeException`, not `OverflowException`). XML: `phase4c3-task1-owned-worker-overflow-red-TestResults.xml`. The game-owned sum now uses a checked `long` intermediate and rejects totals outside `Int32`.
- Earlier focused run: EditMode `1554149073de46e8a4db3c59e061d0bd` passed **9/9**, including `ContextRemainsFogSafe`, `ContextSerializationDeterministic`, and both overflow guards; PlayMode `e4621a02af034e3c809b8f13993c9097` passed **2/2**. Full per-test XML: `phase4c3-task1-frozen-editmode-TestResults.xml` and `phase4c3-task1-frozen-playmode-TestResults.xml`.
- Independent review identified one deterministic tie omitted by the existing production-capability serializer order: `AvailableCapacity`. A new test reversed two same-type/same-count entries differing only in that field and compared complete `GeminiStrategicAIProvider.BuildRequestJson` bytes. RED `4af436fdbd2949d1b5f5d4b70e1d6cf0` failed 0/1 at the expected byte difference; XML: `phase4c3-task1-review-production-tie-red-TestResults.xml`. A final `ThenBy(AvailableCapacity)` tie-breaker fixed it. The fog test now checks that normalizing only `visibleThreats` makes all other safe-context fields equal, and the recording-transport PlayMode test asserts concrete worker and production insight values in the actual HTTP body.
- **Review-fix frozen run on current source:** EditMode `983bd720c7f148029383951039890286` passed **10/10**; PlayMode `2c0185918ff94e4489225834bd0a0f6b` passed **2/2**. Full per-test XML: `phase4c3-task1-review-fix-editmode-TestResults.xml` and `phase4c3-task1-review-fix-playmode-TestResults.xml`. Unity error console: 0 entries after these runs.

## Interfaces and behavior

- `StrategicContext.Insights` is a nullable get-only value. The existing 13-argument internal constructor remains and delegates with null insights; the game-owned builder calls the new overload explicitly. Legacy provider serialization omits `insights` when null.
- `StrategicPlanState` keeps its three-argument constructor and copies actual completed/total milestone counts, counting only `Completed` statuses. The prior active-only plan projection remains; terminal plans are still excluded.
- `StrategicContextInsightsBuilder` accepts only detached state lists, validates null/negative/inconsistent inputs, uses wide sums and integer basis points, rejects out-of-range aggregate counts, combines military/production duplicates, and emits sorted immutable entries. Income trend remains explicitly unavailable. Queued units are separate from owned army shares; construction is separate from completed/idle production.
- `StrategicAIContextSerializer` explicitly emits primitive fields under optional `insights`; it never reflects insight objects. Existing field names remain. Existing arrays/maps and the new arrays are sorted deterministically for full-request byte equality under reordered source lists and Turkish/French cultures.
- The actual Gemini provider request was compared through `BuildRequestJson`; the PlayMode chat test captured its HTTP body through a recording transport and verified non-null insights reached the provider without creating plans, intents, reservations, goals, commands, or decision history before approval. The game-owned runtime test changed own worker allocation and a barracks training queue, advanced a real plan, then confirmed capture itself did not change those authority objects or plan statuses.

## Verification and self-review

The final writer patch was checked with `git diff --check`; it reported only Git's existing LF-to-CRLF warnings, no whitespace errors. The original three before snapshots still match their recorded hashes. No full suite was run in Task 1; the inherited Phase 4C.2 full pair is the entering gate, and full 4C.3 regression belongs to Task 2.

| Task 1 source/test file | Final SHA-256 |
| --- | --- |
| `Assets/Scripts/AI/Commander/Strategic/StrategicContext.cs` | `97d601611774f0b346370d6fdf167512be13eb83825ed33a1c2e886d5eeea9aa` |
| `Assets/Scripts/AI/Commander/Strategic/StrategicContextBuilder.cs` | `bc0d439ab4b485eb3875064e283e742ee39380af59b8b6bab65c3a54ad035f76` |
| `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIContextSerializer.cs` | `d6654713bd0bf03a119771f309530d4fb118069361d0aece050bface0f61c49a` |
| `Assets/Scripts/AI/Commander/Phase4C/StrategicContextInsights.cs` | `c10bf1a86a0734ccd9cbf29ed330d56e1a0eefee7dc45b9a9e76d847901c13c4` |
| `Assets/Scripts/AI/Commander/Phase4C/StrategicContextInsightsBuilder.cs` | `dd1cfc39430e9c63efbe9485323e638c26cda862545fc887cda832dec42f3125` |
| `Assets/Tests/EditMode/CommanderPhase4C3Tests.cs` | `b4f51067753e359db70f10d0b0dd269792d6f27faac6b67f072c5be5790e4ff9` |
| `Assets/Tests/PlayMode/CommanderPhase4C3PlayModeTests.cs` | `c70be2181dbd50738cb919acc35ae0c675a0be48b65761e84474f559e8d1781c` |

Self-review confirmed: no simulation/planner/command/delegate fields in insight value types; no provider-side simulation read; no authority, policy, request identity, default objective, or output DTO changes; no inferred income, idle-worker, resource-shortfall, or predictive labels. Existing unrelated dirty documentation was preserved. The final test fixture also clears caller source lists after building insights and asserts the detached values survive. Task 1 is frozen for independent review/full regression.
