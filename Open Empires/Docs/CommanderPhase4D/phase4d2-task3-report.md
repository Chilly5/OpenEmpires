# Phase 4D.2 Task 3 — real approved-plan recovery proof

Owner: `/root/phase4d2_runtime_gate`. Scope of this handoff: one new PlayMode runtime test and its focused evidence. No production, provider/configuration, package, protected source, Phase 4D.3, staging, commit, cleanup, or full-suite action was taken.

## Expected behavior and test design

`OpenEmpires.Tests.CommanderPhase4D2RuntimePlayModeTests.ApprovedRangedResourceWait_RecoversSamePlanAndCompletesWithoutReapproval` uses the existing Phase 4C.4/D.1 two-player simulation, six prebuilt Houses, real Food/Wood gatherers, initialized chat/pipeline, strategic provider, and actual **Approve strategy** button. It first proves the recommendation has no plan/goal/reservation/Commander command before approval. Immediately after the affordable Ranged Reinforcement plan is approved, it spends the owned Food/Wood stockpile to create a legitimate changing-world wait without changing plan targets, House policy, or production code.

The test captures fresh `StrategicPlanner.CapturePlanHealth(owner, planId)` after every relevant simulation tick. It checks exact owner/ID/creation tick/revision, milestone status, plan-linked retained child IDs/status, copied Wood requirement/deficit/owned stock, population/cap, and queued units. It also checks the enabled host's rendered health and offline `why is the plan waiting?` response. The progression assertion is an observed increase in completed milestones **after** the sampled wait, not a fabricated `Healthy` interval; the observed plan can go directly into another resource wait. Terminal checks require real Archery Range/10 Archer production, completion of every milestone, no replacement plan, unchanged provider/decision count, real approved Commander commands, no stray unlinked goals, and zero active plan/global strategic reservations.

Production mutations that this test would catch include misclassifying a real resource wait, a stalled plan that never advances after gathering, replacing a plan or calling the provider for recovery, unlinked tactical goals, and leaked reservations. No production change was needed for the observed GREEN.

## Discovery anomaly and focused proof

- Initial PlayMode job `83d7b29ece19441aa16c56fa59547a63` returned terminal `succeeded` but **0 discovered tests**. This is neither behavioral RED nor GREEN. Full terminal payload: `phase4d2-task3-zero-discovery.json`, SHA-256 `AD4CDCDBA3A6D4E5CDAE4D752AA427B728AD29D2B673AC7B10320E2C3CCAF01E`. A forced Unity asset/script refresh and domain reload resolved discovery; `read_console` and project `Logs/Editor.log` showed no `error CS` compiler errors.
- First proof job `108d57c8c8694eb5bf4d29f52bbc6839` discovered 1 and passed 1 after refresh. Its full terminal payload `phase4d2-task3-proof-v1.json` has SHA-256 `4E802A1DBE92526E192EA8564E47D6F6C83FFEC47A4212DE8962CF3E567B557B`. This is historical because the test-only assertions/diagnostic markers were then strengthened.
- Final-current-source PlayMode job `703ab3a549034a809dd3034817a0ba92`: **1/1 passed**, 0 failed, 0 skipped, 0 inconclusive; duration 4.6446264 s in the native NUnit XML. The exact fully qualified test ID above was discovered. Native XML: `phase4d2-task3-exact-runtime-703ab3a5.xml`, SHA-256 `081EB6CB6F9E256F2F42B613CBCB3120E57167F1F0E179486E4C5BBDFAC7077E`. Earlier focused payload `phase4d2-task3-final-green-ded6eac2bb6642bf8ac8e60337b072c9.json` is also retained but is superseded by this exact final rerun.
- Final Unity `read_console(types=[error], count=100)` returned 0 entries; `Logs/Editor.log` had no `error CS` matches after final refresh.

Live diagnostic markers in the final payload:

| Observation | Authoritative runtime evidence |
|---|---|
| Wait | Same plan `1`, creation tick `0`, revision `7`, simulation tick `1`; milestone `2/WaitingForResources`; health `WaitingForResources`; Wood owned `0`, current milestone requirement `150`, deficit `150`, plan reservation `0`; queue `0`, population `16/70`. |
| Linked child | At tick `2371`, current milestone `2` had retained required child goal `3/Executing`; test checks its ID against `plan.CurrentMilestone.RequiredChildGoals`. |
| Progress | At tick `3841`, same plan revision `17` had completed milestones increase `1 → 2`; health was already `WaitingForResources` for the next milestone, so no `Healthy` interval is claimed. |
| Completion | At tick `13981`, same plan revision `47` completed, with 10 Archers, zero active strategic reservations, and strategic provider calls still `1`. |

## Final source and boundary hashes

- `Assets/Tests/PlayMode/CommanderPhase4D2RuntimePlayModeTests.cs`: SHA-256 `07708958A428AD4BCBE42898F2A0185185EF996888F4ECAA395C9C9295CA1D0F` (source used by exact final PlayMode job `703ab3a549034a809dd3034817a0ba92`).
- `Assets/Tests/PlayMode/CommanderPhase4D2RuntimePlayModeTests.cs.meta`: SHA-256 `3365682E668734A5B66B3C4510F70DBAF6E52EB326329F1892AC41FF631650A0`.
- Accepted Task 1 source hashes were unchanged: `StrategicPlanHealthSnapshot.cs` `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408`; `StrategicPlanner.Health.cs` `EAFDFC54C8B98757FBDDC40554C6E60256A5100B23A34D6FAD738ACA7F88EC9B`.
- Accepted Task 2 source hashes were unchanged: `CommanderChatUI.StrategicControls.cs` `E58604100E7490D134DC6172FFA19BC79AE2A17A3F70E00ED26FD6D903E03F7D`; `CommanderChatUI.Explanations.cs` `8B49EC9E23CE5AB00A5189CECF2A8B95CD6256994CB3912D3625A2D972BE0E55`.

## Review and remaining gates

No behavioral RED arose from the existing recovery path; the initial zero-discovery run was an import/discovery anomaly, not a feature failure. The final test also independently verifies the produced Commander commands/goals against the approved RangedReinforcement template. Independent Sol rereview closed its prior Important finding; no Critical or Important findings remain. Task 3's scoped runtime proof is accepted.

## Final-source full-suite and audit status

- Full EditMode job `3273c4992ef749ed80a08dfd517bda60`: **656/656 passed**, 0 failed, 0 skipped, 0 inconclusive. Native XML `phase4d2-final-full-edit-3273c499.xml`, SHA-256 `F0847CFC6DF4EBBC72077981FC61B36D506E39ED86798C72302BEBE04F81615E`.
- Full PlayMode job `ab908946aba645f4a48a71995af9e02d`: **116/116 passed**, 0 failed, 0 skipped, 0 inconclusive. Native XML `phase4d2-final-full-play-ab908946.xml`, SHA-256 `05BFB892A53E3873A0B9B023867DF1CCDD59168972381A27DC6335B5E092B72D`.
- Parsed discovery in these XMLs includes all 42 `CommanderPhase4D2Tests` EditMode cases, all 13 `CommanderPhase4D2HostPlayModeTests`, and the 1 `CommanderPhase4D2RuntimePlayModeTests` PlayMode case.
- Root final baseline verifier: PASS — 27/27 Phase 4C paths present; 55/55 protected paths present, 54 unchanged and one documented `CommanderGoalManager` drift; pinned XML 6/6. The 4D.2 changed health/host source had no forbidden references, and the credential-pattern scan across Commander source/tests/docs found 0 files.
- These results make Phase 4D.2 Gates 1–5 ready for root decision, not a final Phase 4D verdict. Preserve the outstanding final-source scene-provenance item: the untracked `InitTestScene` observed during the full run disappeared automatically by test end; the baseline contains pre-existing scene/recovery churn now absorbed in user commit `4ae6269`. Do not clean or otherwise alter it as part of this report. The final Phase 4D verdict and readiness for Phase 4E remain outside Task 3.
