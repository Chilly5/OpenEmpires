# Phase 4D.2 Task 2 — host health and offline questions

Owner: `/root/phase4d2_host_impl`. Scope: host health presentation and PlayMode host tests only. No Task 3 runtime test, protected source, Phase 4C source, package, credential, provider configuration, staging, commit, or cleanup was changed.

## Implementation

- `CommanderChatUI.Explanations.cs` recognizes health questions through its existing exact whole-form dispatcher, before provider routing; `CommanderChatUI.cs` was untouched.
- `CommanderChatUI.StrategicControls.cs` adds a short selected-plan health line and exact offline questions. Every answer and visible health refresh calls the accepted two-argument `CapturePlanHealth(Conversation.PlayerId, planId)` on the owning thread. The host holds no `StrategicPlanHealthSnapshot` field or core projection cache; revision is never used as a freshness key.
- Current selection requires the current pipeline reference, runtime generation, trusted owner, exact selected nonterminal plan object/ID/creation tick/revision, and matching captured health token. Pipeline replacement, reset, owner mismatch, plan revision, and destruction cannot reuse an old snapshot. Existing 4D.1 controls/cancel callbacks were not changed.
- Bare `why did the plan stop?` and `did the strategy recover?` return evidence unavailable: a current snapshot cannot prove past recovery, and bare selection cannot silently select an archived plan. The additional exact form `why did plan #<ID> stop?` captures a retained terminal plan by explicit ID and reports only authoritative `Completed`, `Cancelled`, or `Failed`, not an inferred past blocker cause. Unknown/foreign/nonretained IDs fail closed.
- Rendering uses typed primary/secondary facts, copied milestone deficits, copied population/queue/resource counts; it never parses status-reason/outcome text, reads health from UI simulation state, calls a provider, or creates commands/goals/reservations. Informational explanation text uses the existing bounded memory path.

## Behavioral RED and diagnostics

- Initial meaningful RED: `b09525523a434c089244c60e02c20c72` discovered 6 host tests; five failed on absent exact health routing/status line/world refresh, one hostile-suffix test passed. Full terminal payload `phase4d2-task2-red-b09525523a434c089244c60e02c20c72.json`, SHA-256 `101C2068741E93A862488ADD9D668BDAC1E0F865A2D51C3AD3AE1D72BDB76B42`.
- Review-gap RED: `a910808264dd4a718dba7b4fde1d7769` discovered 12; two failed: explicit retained terminal form fell through the router and queue changes were not shown. Full terminal payload `phase4d2-task2-review-red-a910808264dd4a718dba7b4fde1d7769.json`, SHA-256 `CA6C5474A123C52A56EB81D7304386BFC064EDB8838E655888926A37419CCD5D`.
- Earlier jobs `9a160ee8de214ee9a07e49bfa111699c`, `330f386af9e64f44bf468c1a31484484`, `5f62e0974c3e48158b2b2f3a2eca08c1`, `48e1a0ee8f304ecbad48b34842427263`, and `75671f4bf39645d7bab63b46a6791511` terminated before test discovery and are not RED/GREEN evidence. Editor log identified test-assembly compile errors in the initial attempts (`TMPro` missing reference, then incorrect `ActiveReservations` property); the test helper/import was corrected before behavioral RED. No overlapping Unity test job was started.

## Current-source verification

- Focused PlayMode job `387cb12565914cfca1dc0839ab8bc202`: **12/12 passed**, 0 failed/skipped, duration 3.296551 s. Full terminal payload `phase4d2-task2-final-green-387cb12565914cfca1dc0839ab8bc202.json`, SHA-256 `31B1AA3017FF96E1334D4A2F73D4A453A47F64BE30B7B2A1FF1F7ED59ABE67E6`. All 12 expected `OpenEmpires.Tests.CommanderPhase4D2HostPlayModeTests` IDs were discovered. They cover exact/mixed forms, offline provider count zero, typed concurrent wait/block evidence, explicit retained terminal status, reset, reinitialization with matching numeric plan identity, owner mismatch, revision changes, unchanged-revision resource/population/queue refresh, and destruction.
- Affected PlayMode job `5b828e2674234542adb925b369bfe150`: **17/17 passed**, 0 failed/skipped, duration 4.4643098 s, comprising 6 `CommanderPhase4C2PlayModeTests` explanation and 11 `CommanderPhase4D1HostPlayModeTests` lifecycle/cancel cases. Full terminal payload `phase4d2-task2-final-affected-5b828e2674234542adb925b369bfe150.json`, SHA-256 `9B79495A20688A94BA4C42A2C2064A44AF542E04C26A632D27EDE8D4690593E3`.
- Final Unity `read_console(types=[error], count=100)` returned 0 entries. Unity MCP supplied no native NUnit XML for these jobs; full terminal JSON payloads are retained instead.

## Final SHA-256 source boundary

- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs`: `246EF488AD5E3530D58F601E1C84AAF0940098E000BBB8D6CE2288901FFF3546`
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs`: `8B49EC9E23CE5AB00A5189CECF2A8B95CD6256994CB3912D3625A2D972BE0E55`
- `Assets/Tests/PlayMode/CommanderPhase4D2HostPlayModeTests.cs`: `52C1677E44A17B415075DE7B1DE7987A1B247B509AC27098DD685697A29C4165`
- Accepted Task 1 hashes remain unchanged: snapshot `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408`, planner health `EAFDFC54C8B98757FBDDC40554C6E60256A5100B23A34D6FAD738ACA7F88EC9B`, EditMode tests `E1D4AC8B18A8AE493C870F9F13E8BE4930600D7F57FED828EC308B79DC2ABB89`.

## Residuals and review request

The health line is refreshed each active selected-plan frame and copied evidence may be expensive in large matches; no performance benchmark was run. Resource/population/queue freshness was tested at the host boundary, not as Task 3's real approved-plan recovery scenario. The bare terminal/recovery questions intentionally remain unavailable without historical transition evidence. Independent review is requested for authority, stale/owner checks, exact-form grammar, rendering truthfulness, and test adequacy. Task 2's focused result does not establish the 4D.2 full EditMode/PlayMode/runtime gate.

## Independent-review fix round 1 — supersedes earlier final hashes/jobs

Independent review found an Important test-adequacy gap, not a Critical/Important production defect: no pure EditMode rendering tests and no enabled-component LateUpdate freshness test. The host implementation was checked against these points before editing. Four pure rendering tests were added to the existing `CommanderPhase4D2Tests` group for bounded answer and fourth-secondary preservation/order, mixed blocked/population/resource evidence with copied numbers, unknown fail-closed behavior, and culture-invariant numbers. An enabled `CommanderChatUI` PlayMode test now yields actual frames after a same-revision wood change and verifies the visible label changes without provider/control mutation. That PlayMode test passed against the prechange host code; it is coverage, not a claimed RED.

Behavioral RED `2174e32d695a487ea93de8eae3b57569` discovered 41 EditMode tests and failed exactly two new assertions: population numbers were absent when population was a secondary fact, and the fourth proven secondary fact was dropped by the renderer's three-fact cap. Full terminal payload `phase4d2-task2-rereview-red-edit-2174e32d695a487ea93de8eae3b57569.json`, SHA-256 `F2D105768EDABB78BD006BBC02188FA183F9E61B6BACB8663E4B44B8D3181F07`. The enabled-host prechange job `4b1e2d39ab3f4dff8a14c188dbcdaea5` passed 13/13 and is saved as `phase4d2-task2-rereview-prechange-host-4b1e2d39ab3f4dff8a14c188dbcdaea5.json`, SHA-256 `2B8EB08A88F6D7B010BE855B7587CAE303F95E49C8B83BCE81DC7EFFC9B475BE`.

The renderer now preserves every bounded typed secondary category, displays copied population evidence even when secondary, and explicitly formats numeric answer fields with invariant culture. After an initial 41/41 focused pass, self-review separated malformed-primary fallback from numeric culture testing and added the stronger fail-closed assertion. Meaningful RED `cc5b88b5a1ae4893b466abae6e724903` discovered 42 EditMode tests and failed only `HealthAnswer_UnknownCategoryFailsClosed`: an invalid primary had still emitted `Waiting for resources` and a deficit. Full payload `phase4d2-task2-rereview-unknown-red-cc5b88b5a1ae4893b466abae6e724903.json`, SHA-256 `452BDCDC58F24D2720DF66590B697B354CF79FD311F89D551E82A8F58B107EE2`. The host renderer now returns only `Unknown` for Unknown/undefined primary categories instead of narrating inconsistent secondary data.

Final **current-source** serial jobs after that last production/test edit:

- EditMode `f1b43130127940879e172982549b02b8`: **42/42**, 0 failed/skipped, 46.4310659 s; complete terminal payload `phase4d2-task2-fix1-final-edit-f1b43130127940879e172982549b02b8.json`, SHA-256 `D8126EE99131953EE3F313F9C95AC65D7EAF0B61967E7AD669B8F6B8FA3903B4`.
- Host PlayMode `7665ee448feb415fbe0a0d9f0ad4b990`: **13/13**, 0 failed/skipped, 3.5638526 s; complete payload `phase4d2-task2-fix1-final-host-7665ee448feb415fbe0a0d9f0ad4b990.json`, SHA-256 `B04CB0D5C326A171020BF12849A470F5984E51ECA76BD1E1690BEE212CACC7A9`.
- Affected Phase 4C.2 explanation and Phase 4D.1 host PlayMode `66c54be125844572babc8514a7262538`: **17/17**, 0 failed/skipped, 4.6305059 s; complete payload `phase4d2-task2-fix1-final-affected-66c54be125844572babc8514a7262538.json`, SHA-256 `662C2FDB51BCE866EA890215B4319E89434BFE296B41DE5917977B25B5839162`.

Final Unity `read_console(types=[error], count=100)` returned zero entries. Unity MCP emitted no native NUnit XML; full terminal JSON payloads contain the discovered IDs and results. The prior 12/12 and 17/17 jobs above are historical, not current-source gate evidence.

Frozen fix-round SHA-256:

- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs`: `E58604100E7490D134DC6172FFA19BC79AE2A17A3F70E00ED26FD6D903E03F7D`
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs`: `8B49EC9E23CE5AB00A5189CECF2A8B95CD6256994CB3912D3625A2D972BE0E55` (unchanged)
- `Assets/Tests/EditMode/CommanderPhase4D2Tests.cs`: `6A0D517CDC9D7EAF7C5AF709EC43BC2E61D181EB27A354CF344636316B576BD2`
- `Assets/Tests/PlayMode/CommanderPhase4D2HostPlayModeTests.cs`: `75ED9DA0B7F7A447B118301825F078941966ACE823E166BEEEF9C2F7839CC619`

Task 1 production hashes remain snapshot `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408` and planner health `EAFDFC54C8B98757FBDDC40554C6E60256A5100B23A34D6FAD738ACA7F88EC9B`. No protected or Task 3 file was changed. Remaining work: independent scoped rereview, then Task 3 real runtime proof and the separate full-suite/boundary gate.
