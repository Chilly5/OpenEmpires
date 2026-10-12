# Task 3 — native correctness handback (2026-10-11)

**DONE_WITH_CONCERNS.** Task 3 implementation and focused source-matched EditMode fixture gate are complete. This does not certify the whole Phase 5C, a real-time native match, a Windows/Web package, or live-model behavior. No commit, cleanup, paid/network call, build, or full regression was made.

## Implementation

- `CommanderPlanner.cs` / `CommanderGoal.cs`: resolve one normal `MoveCommand` with distinct actor-aware, known complete-path slots within 10 tiles of the semantic objective; reject an out-of-bounds semantic anchor or insufficient legal routes without enqueueing. Store exact request subject slots and first post-issue native endpoints, then require every exact subject to reach its own observed endpoint. A partial/distant endpoint, unrelated replacement movement, no progress for 150 simulation ticks, and temporarily unavailable actors do not complete the task. Dead, missing, foreign-owned, garrisoned, and genuinely human-protected subjects have separate blockers; survivors are not substituted. Proximity uses 64-bit raw fixed-point arithmetic to avoid distant overflow.
- `CommanderPlanner.Economy.cs` / `CommanderGoalManager.cs`: retain eligible unprotected Farm incumbents before idle selection when permitted, count their occupied capacity, and do not reissue their native Gather. Additional and TargetTotal still require their own new/deficit workers; IdleOnly, current-resource, source and protected-floor gates stay intact. Selected-count non-Farm reassignment remains unchanged. Retained incumbents waiting for a real resource amount acquire the normal atomic worker lease even with zero command groups; no dummy command is emitted.
- `CommanderTacticalStatusSnapshot.cs` / `CommanderChatUI.ReadOnlyQuestions.cs` / the existing `CommanderSemanticProvider.cs` request projection: named causal questions enter the detached owned-task read-only projection even without status keywords. Overlong blocker or question facts use an explicit unavailable marker, not a meaning-cutting prefix or ellipsis. The pre-existing hint/confirmation code in the dirty provider file was preserved.
- Tests changed: `CommanderPhase4GCapabilityTests.cs`, `CommanderEconomyClarificationTests.Execution.cs`, `CommanderGrandFixStatusTests.cs`. They cover six-unit defense formation arrival with accelerated native ticks; exact subject loss/authority/garrison/unavailability; distant idle, no-progress, bounds and unreachable objectives; Farm incumbent/strict idle/protected/quantity/zero-command claims; and named causal facts without gameplay admission.

## RED → GREEN and durable evidence

All runs used Unity `Open Empires@6d7310c7`, 6000.5.9f1, `run_tests(mode=EditMode, test_names=[the 18 focused method filters], init_timeout=120000, include_failed_tests=true)`, followed by `get_test_job(job_id=..., wait_timeout=60, include_failed_tests=true)`. Parameterized methods expand to more cases than filters. Every script edit used `refresh_unity(mode=force, scope=scripts, compile=request)` and a compile-complete/editor-state/console check before a test start. Final console had zero errors, final editor state was idle/no compile/no test job.

| Gate | Job ID | Terminal result |
|---|---|---|
| Initial RED start | `1683520fbba441b085c1f7fee0c9fee3` | Zero tests started; initialization timeout. |
| RED allowed retry | `56013d7e5e1a49f19115e22a98d51c11` | 6 discovered, 5 expected failures, 1 pass. |
| First GREEN start / retry | `29223a7895df4f3b9e5502a253334297` / `54ff59190ad04466aa3518f736981476` | Zero-start timeout / 5 pass, 1 defense fixture still executing after only 200 accelerated ticks. |
| Extended defense start / retry | `fff0ac89e30e4a10a81499dae74ed787` / `4337084c23ae4f2b994509cbe27f20ef` | Zero-start timeout / 1 pass after 1,000-tick fixture bound. |
| Expanded controls | `b96c80f9edb94fd780a7cfc0886815a5` | 21 discovered, 20 pass; old quantity test exposed overly broad non-Farm retention. Fixed to Farm-only. |
| Zero-command lease RED | `508692fbe3724e6f80d70ec8bdb66de9` | 1 expected failure: selected Farm incumbent had no lease while waiting for AdditionalGathered amount. |
| Expanded focused first start / recovered job | `3d9c773586e74604a535b8ceec3cf64e` / `4ae755e789e0483e9f1aefd5d552a17a` | Zero-start timeout; a following request returned `tests_running`, fresh editor state exposed the actual already-running second handle. It completed 23/23. No duplicate was started. |
| Proximity/quantity source snapshot | `bba88deb00fb4a9dbb03698f3727e087` | 23/23 passed. |
| Temporary-unavailability RED / GREEN | `e4a1fb2a590540ba8a915d5295eaf7a8` / `469412554b834c5bb9759e476b924f49` | 1 expected classification failure / 24/24 passed after fix. |
| Bounds fixture first | `f28c13b2e7af4bb6b96598667669b27c` | 25/26; fixture painted water around TC origin, not resolved center. Corrected fixture only. |
| **Final source-matched focused gate** | **`8e58f33fdcfb41d28f7ea1f4bd9ecd3e`** | **26/26 passed, 0 failed/skipped, 9.03 s**. |

Final command filters included the new movement/defense methods plus `CapabilityGoal_HumanOverrideBlocksWithoutFalseCompletion`; the new Farm methods plus `QuantityModes_KeepBaselineTransferAndAllMatchingDistinct` and `ReservationPreflight_DistributesCapacityOrIssuesNothing`; and the new causal methods plus `GenericEconomicWhy_IsNotReplacedWithUnrelatedArmyStatus`. The final `InvalidDefenseObjective_EnqueuesNoNativeMove` expands to two cases, and loss/quantity/causal methods expand similarly. Test names and terminal details are in `evidence/task-3-jobs.raw.json` and the per-run `evidence/task-3-*.json` records. The first four jobs expired from Unity's job registry after reload; their contemporaneous terminal results were retained in compact JSON before expiration, so full native job payloads for those four cannot now be re-fetched. This is an evidence-retention concern, not a hidden pass.

`git -c core.safecrlf=false diff --check -- [the ten Task 3 source/test paths]` exited 0. No unrelated dirty file was reverted or rewritten.

## SHA-256 at final focused gate

| File under `Assets/` | SHA-256 |
|---|---|
| `Scripts/AI/Commander/CommanderGoal.cs` | `51726074DCE172A420A625F732A0A019ADA93F58DECEC09D9A53D4426AF3E1B1` |
| `Scripts/AI/Commander/CommanderGoalManager.cs` | `D9D7EF1515CD882470C78EFDD236B317C814A99A5B390A467A38A9FAD3349C95` |
| `Scripts/AI/Commander/CommanderPlanner.cs` | `736F8A0519F9B2461F27765044947F735026997B88F71118198F4CCA77F32474` |
| `Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs` | `F9BF9552EB64E5BEB0100B54D343E7F6DEFECAAC5E1DFCEED15588FEF369F779` |
| `Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs` | `2C8EA0D3B6B8F8C96DAB792A6EAAE25481DDBEA3FBC1FCE85724567ACEA13718` |
| `Scripts/AI/Commander/Phase4A/CommanderChatUI.ReadOnlyQuestions.cs` | `37F2FFA936E5EF014613C3FBC0DAF7E3554810CFE1C1BE0922C78B55572988FF` |
| `Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs` | `E57DDA95C59B22F8BF01381536A05F1243763F3A13C07DB90BE143E19C7CE9CF` |
| `Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs` | `17C8FA37A561326A76C2C3514DECA2B9112CAC5C091D6FF33B35E26C50307249` |
| `Tests/EditMode/CommanderGrandFixStatusTests.cs` | `C1C3BE9EE428A021E1AA6BE7B5521EECDB858965F580AA12775A5B35D5175658` |
| `Tests/EditMode/CommanderPhase4GCapabilityTests.cs` | `AE2EEC1EFE2CA086549F47AECD9CDC87ABE12D5A4662C1049FFDCE42F43C1709` |

## Self-review and limits

The review caught fixed-point overflow risk, retention leaking to non-Farm Food, AllMatching potentially weakening Additional, and the zero-command lease gap; each was corrected before the final gate. Farm native Gather and player-wide AdditionalGathered accounting were not altered. The focused tests are deterministic accelerated simulation fixtures, not rendered/native real-time match evidence. No full EditMode/PlayMode regression, live provider, package, UI, voice, multiplayer, or platform build was run because Task 3 explicitly forbade those broader gates. Causal detection remains bounded deterministic language matching, not universal English understanding; non-matching generic questions retain the existing read-only route and cannot admit gameplay. Parent owns independent review and subsequent Task 4/5/6/7 integration/release gates.

**Handback:** Task 3 sole source/Unity ownership ends with this report. No new test job remains active.

## FIX1 — native processing provenance, formation parity, and counted causal facts (2026-10-11)

**DONE_WITH_CONCERNS for the three reviewed findings only.** This section supersedes the movement receipt/formation and counted causal claims above where they conflict; the Farm and zero-command lease work was unchanged. The current source-matched affected gate is 16/16 EditMode cases, not a full regression or real-time match.

### Source and test changes

- `Core/GameSimulation.cs` plus new `Core/GameSimulation.MovementObservation.cs`: emit a local, immutable, per-exact-unit processing receipt from the ordinary `ProcessMoveCommand` path with runtime, verified original command box, simulation tick, actual native endpoint, and complete/already-at-target/partial/no-path result. A separate local post-`ProcessCommand` observation identifies later unrelated unit orders (including Stop) without changing command encoding or gameplay dispatch. Extracted native connectivity snap, radius scaling, and scatter target calculation into one shared resolver, preserving the existing native algorithm; Commander preflight reads its deterministic center and a conservative scatter envelope without consuming the native tick/hash.
- `Core/CommanderCommandOriginLedger.cs`: include a uniquely verified ordinary Move replay box in the existing local-origin association. Identical Move payloads from different original boxes remain unattributed, rather than assigning either by ordinal. No shared network envelope or serializer change.
- `AI/Commander/CommanderPlanner.cs`, `CommanderGoal.cs`, and new `CommanderGoalManager.NativeMove.cs` (with manager event wiring): preflight every possible native scatter cell for semantic-area membership, player/actor-aware walkability, known complete route, and distinct formation coverage. Completion requires a receipt attributable to this exact issued command for every captured actor, an acceptable native path result, and arrival at the *actual* native endpoint. Idle position alone, missing processing, partial/no-path result, or unrelated replacement order cannot complete. The pre-existing 150-simulation-tick no-progress bound and exact subject/human/garrison distinctions remain. Receipt arrays are runtime-only and released with the goal.
- `AI/Commander/CommanderTacticalStatusSnapshot.cs`: bounded read-only `what happened to the six units?` count matching selects owned task groups and preserves ambiguity; generic economic why is not recast as an unrelated army status. Overlong facts return an explicit unavailable marker, not a truncated meaning-bearing prefix.
- `Tests/EditMode/CommanderPhase4GCapabilityTests.cs` and `CommanderGrandFixStatusTests.cs`: added near-slot issued-but-unprocessed, partial path, unrelated Move and Stop replacement, mixed-radius native formation, six/seven-unit and ambiguous causal cases. Strengthened the human-priority fixture to prove an actual Move was first issued before human takeover. All movement fixtures use accelerated deterministic simulation ticks; none is labeled real-time gameplay proof.

### Exact RED → GREEN record

Unity project `Open Empires@6d7310c7`, 6000.5.9f1. Runs used `run_tests(mode=EditMode, test_names=[exact NUnit leaf full names], init_timeout=120000, include_failed_tests=true)` and `get_test_job(job_id=..., wait_timeout=60)`; raw terminal records are in `evidence/task-3-fix1-*.raw.json`. The original short-method filters did not select NUnit leaves: `f3205975742b4ee193511e45485a77d5` timed out at zero start, its one allowed unchanged-input retry `8d3da2ab251a4a12b4c769dfe0934024` had 0 discovered/0 run. `evidence/task-3-fix1-discovery.json` records actual Unity NUnit discovery and the corrected full-name filters; no source was written before a valid RED.

| Gate | Exact job ID | Result |
|---|---|---|
| Corrected initial RED | `eb33dd69db964d89ba62ec79cb0138b1` | 7/7 executed, 6 expected failures, 1 existing partial-path control pass. |
| First affected GREEN | `961bcf99f72e420da98b172eed5fad37` | 15/15 passed, 0 failed/skipped. The old human fixture passed for the wrong reason, so it was strengthened. |
| New unrelated Stop RED | `cd4f8f10212a4184b927e14cdb8bdaac` | 1/1 expected failure: Stop near the old endpoint falsely Completed. |
| First 16-case attempt | `e994773355514465ab2e2567a60e0a38` | 16/16 executed; strengthened human fixture failed because its starting actor was too close for this preflight. Source replacement fix passed. Fixture start moved to the same visible/legal distance as the other successful movement controls. |
| Human fixture control | `29eea3d466d84cce9ec216058379d8f7` | 1/1 passed, output shows Executing/issued Move followed by human-priority Blocked. |
| **Final source-matched affected gate** | **`ad65a36b3ea143a7b7423e05d6452aa6`** | **16/16 passed, 0 failed/skipped, 5.01 s.** |

`refresh_unity(mode=force, scope=all, compile=request, wait_for_ready=true)` was used for the new C# files; the final console read had zero errors. Final fresh `mcpforunity://editor/state` read: correct project, idle, no compilation or test job. `git -c core.safecrlf=false diff --check -- [the eight tracked FIX1 source/test paths]` exited 0; the two new C# files had no trailing whitespace. No unrelated dirty file was reverted.

### FIX1 SHA-256 at final gate

| File under `Assets/` | SHA-256 |
|---|---|
| `Scripts/AI/Commander/CommanderGoal.cs` | `6C7EC7A7A36A2C26EFAC5C1A9FDE805F79B3A955DA2A11246D7F6D5B8B18F916` |
| `Scripts/AI/Commander/CommanderGoalManager.cs` | `8B8DA6FB77EDC27023A1FFC961897C839871C0CAC03B35039A9E840D95D5C21A` |
| `Scripts/AI/Commander/CommanderGoalManager.NativeMove.cs` | `0977E988ED8FCC2DFF9CFC1B371CC168143A0A8FAA9F7E1CF8612004455F0732` |
| `Scripts/AI/Commander/CommanderPlanner.cs` | `4EA6E18CF79C19DCCCD0AE263744C2D111B72BCEB46FA25D168F662925064B4A` |
| `Scripts/AI/Commander/CommanderTacticalStatusSnapshot.cs` | `75D58B28015248E106FD8F4958498CE69F1D11E241F491EB4C5CF645C7216AF1` |
| `Scripts/Core/CommanderCommandOriginLedger.cs` | `55F65DC6897EF9B3ABBAF3B9783F4C16A252527D4D4D564F760D2A493EC45B24` |
| `Scripts/Core/GameSimulation.cs` | `8BB4822858164CAA48D1A2868D2409D16CE5DC63AFC4B4E5305B8E7118D821D0` |
| `Scripts/Core/GameSimulation.MovementObservation.cs` | `376DA12EF0BB787A825388071FE68DD72CB10AAF398629C6F9F0445619DFC62F` |
| `Tests/EditMode/CommanderGrandFixStatusTests.cs` | `CCFDC2A148C6F5B14F027904F48AE4142696B0BDD4BD722CB97B4D547E0708E5` |
| `Tests/EditMode/CommanderPhase4GCapabilityTests.cs` | `05894F0D8B28E80F5DE6D209E4AC18C2FF7BB5D49884E247DA183A473874FFC1` |

### Self-review, limits, and future multiplayer/replay check

The Stop RED exposed that observing only replacement Move would still permit a false completion after another native order; the final observer covers all native unit-command subjects after normal processing. A verified original-command reference and runtime check guard receipt affinity; missing or ambiguous origin fails closed under the 150-tick bound. The conservative all-cell scatter envelope may reject some legal-but-unprovable large formations rather than invent a legal destination. There is no unbounded receipt log and no hidden-enemy answer path. No second executor or duplicate target algorithm was introduced.

Future multiplayer/replay verification must check the relay's exact original-box correspondence for unique Move payloads, ambiguous identical payload handling, deterministic target parity across peers, and local-only observer timing; this pass did not alter or test binary/network serialization. Full EditMode/PlayMode, rendered real-time/native match, provider, build, paid, audio, UI, and network gates remain outside Task 3 and pending for parent orchestration. No commit, cleanup, reset, restart, or force kill occurred.

**FIX1 handback:** sole Task 3 source/Unity ownership ends here; parent owns scoped review and Task 4 onward. No test job remains active.
