# Phase 4D.3 gate report — source freeze 2026-09-26

Root gate decision: **Phase 4D.3 PASS** on the frozen source and evidence below. I independently verified the terminal full-suite summaries and unique discovery, all 16 required Phase 4D.3 IDs, the listed source hashes, and the baseline verifier; the independent whole-phase and protected-fix rereviews report no unresolved Critical/Important finding. This verdict is only for Safe Recovery and Adaptation Proposals. Phase 4D.4, the final Phase 4D audit, and Phase 4E remain separate.

## Gate assessment

| Gate | Evidence | Assessment |
|---|---|---|
| Focused behavior | Task 1 admission RED/GREEN and three protected fix rounds; Task 2 detached-value RED/GREEN; Task 3 host RED/GREEN and C2 independent-review correction; Task 4 real recovery IDs 2/2 and affected 29/29. See task reports and `phase4d3-reentrant-admission-fix-report.md`. | PASS for scoped behavior. Task 2's initial 17-test RED artifact is explicitly unavailable; later review-driven behavioral REDs are preserved. |
| Current compiler/import | Final source imported and complete suites discovered; Unity console `error CS` query returned 0. | PASS. |
| Independent review | Independent whole-phase Sol review and targeted protected-fix rereview found no unresolved Critical/Important. Task 3 C2 Important was closed by a real host Confirm/rejection/explanation fixture plus mutation sensitivity check. Root accepted those findings with the documented exceptional-observer residual. | PASS. Minor active-adaptation explanation-query dismissal coverage gap remains. |
| Relevant and complete regression | Final-source EditMode `e16cfa9309dd42dcbfdb59e4b333cfa4`: **689/689**, 0 failed/skipped/inconclusive; PlayMode `e5e4a460cdcb4c2e8088d8817cb77f6b`: **145/145**, 0 failed/skipped/inconclusive. Both have complete per-test MCP terminal payloads, 689/145 unique names and required 4D.3 IDs discovered. | PASS. No native XML was returned; no historical job is substituted. |
| Protected/static boundary | Phase 4D baseline verifier PASS: 27/27 Phase 4C paths, 55/55 frozen protected present, 54 unchanged and one documented goal-manager drift, zero missing, pinned XML 6/6. Original-source diagnostic 24/27 match; three documented Phase 4D drifts are StrategicPlan (4D.1), StrategicPlanner (4D.3 protected preflight/reentrancy), and CommanderChatUI (4D.3 host). No package/settings change or lingering untracked TestRunner scene. | PASS within 4D.3 scope; inherited committed test/recovery scenes were retained. |

## Evidence and frozen source

Complete final terminal payloads:

- `phase4d3-full-edit-e16cfa93.json`, SHA-256 `DE04E481DFA6874FCDE4111647D219E8C6EEBFD7E4E9FC633DC9F2CE19F2FA0F`.
- `phase4d3-full-play-e5e4a460.json`, SHA-256 `BFA8AEE8491579DA3DEC61A94473C585CB8B5C956FD14524B55C5714846C1084`.

The final jobs were sequential, on the source hashes below; the full EditMode 689 names and PlayMode 145 names are unique. Required proposal/value IDs (22), host IDs (26), and `TemporaryResourceBlocker_RecoversWithoutNewApproval` plus `PopulationPrerequisite_RecoversWithoutNewApproval` are in the complete results. The runtime cases use real GameSimulation, chat approval, pipeline, planner, goals, natural gathering/reservation retry and House construction; no second approval/provider/plan, target change, or terminal reservation leak was observed. The approved Ranged → pending Defensive → explicit Confirm path is the existing Task 3 PlayMode test, not a DTO-only substitute.

The five named Task 1 admission IDs and both reviewed nested-admission regression IDs each occur exactly once in the 689-result EditMode payload. The root independently verified both terminal full-suite counts and saved payload hashes against disk before gate acceptance.

| Source/test file | Final SHA-256 |
|---|---|
| `StrategicPlanner.cs` | `47171C6D433B54A78FF98EF900AA1C57807081C1A34F51CDAEBD08B6591177EC` |
| `CommanderGoalManager.cs` | `0F79E294D202340044BDDE0539735AAB139CDBAA4E0DBCEB327D31CFE21C048A` |
| `CommanderChatUI.cs` | `D999D806A54CF515AD98E223B618393C8CAF20004133BF9DF7C3CAA85734E8CD` |
| `CommanderChatUI.StrategicControls.cs` | `F5642EF3806645AE328336EE285FDD14301E0F3E9771CE20C0E92FC7C54A0A2B` |
| `StrategicAdaptationProposal.cs` | `85111D1E1C77F4E08BF1FFC3F130DBDA3F5D34D2BDA4E88A548F2DD332AA754A` |
| `CommanderPhase4D1Tests.cs` | `0D7709546F7164EB51E58764615FAE2DC25034C3972C53655AAFEC8323EFAC62` |
| `CommanderPhase4D3Tests.cs` | `4DC7CE9A67A0BD4E793191911500F797DFFA85DF91195C95A5101D7ABC23AA3E` |
| `CommanderPhase4D3HostPlayModeTests.cs` | `9A1EC73A383FE9CAD57F838E2B1F4FD8F5870B1044B39AA9A9A3D6CED854EA4D` |
| `CommanderPhase4D3RuntimePlayModeTests.cs` | `69040993B008F6BD470C9632A94ABDDAABF945450ADE0B5C4C41A55D6F341EB1` |

Task 1's protected planner before hash `A50B64D49B4D2FB5CBB43ED2835E0890047DE28D46BC6C4ABAA14609EB1E5902` and accepted pre-fix `23B96F77FBAA080C54A86AB1186470AE6691096BD646A5B7C0C0198326F17FC4` are recorded with exact diffs and focused tests in `phase4d3-task1-report.md`/the ledger. The later nested-admission guard's before `23B96F77...`, first after `9410C6C1...`, and final after `47171C6D...` hashes, incremental diffs, RED/GREEN jobs, and independent rereview are in `phase4d3-reentrant-admission-fix-report.md`. The second protected goal-manager edit's rationale, before/after and affected regressions are in Task 1's report/ledger. No baseline was rewritten.

## Boundary and provenance audit

`verify-source-baseline.ps1` default exited 0: baseline references intact; 27/27 manifest paths, 55/55 frozen paths, 54 unchanged/1 intentional `CommanderGoalManager.cs` drift, pinned evidence 6/6. `-CheckOriginalSource` exited 2 only for the three documented ongoing Phase 4D source drifts above (24/27 match); that mode intentionally treats any drift as diagnostic failure. A read-only changed-source scan found no forbidden planner/simulation/command/reflection dependency in the new proposal value file. Host planner references are the existing owner-scoped capture/control and approved decision route, not provider-to-planner authority. A credential-shape filename scan found only three pre-existing provider files (Gemini strategic, Gemini tactical, OpenRouter); none changed in the 4D.3 working diff, and no changed 4D.3 source/test file matched. `.env` exists locally, is ignored by `.gitignore:47`, and is not tracked; contents were not read or printed. No `Packages`, `ProjectSettings`, or `UserSettings` status drift was found.

Machine-readable scoped audit: `phase4d3-boundary-audit.json`, SHA-256 `C0BC779F68EF7F233190C4FE7662B4D880EEBFBBECDB60EFC0F85176721E89FD`. Every one of its 14 current source/test hash entries was recomputed against live disk with zero mismatches. This is not the final whole-Phase-4D boundary audit or manifest.

During PlayMode, an untracked `Assets/InitTestScene32862490-2aa9-4e22-8b6b-4ed6f2ae2051.unity`/`.meta` appeared with `PlaymodeTestsController`, then disappeared by terminal without cleanup. Other test and `_Recovery` scenes are tracked in HEAD/current inherited workspace; no 4D.3 scene change remains in status. They were not deleted, moved, or reclassified as Commander production.

## Residuals and exclusions

- Replacement is not a general transaction against arbitrary throwing external observers. Preflight failures leave old plans intact; after commit begins, exceptional observer cleanup completes and propagates before new-plan insertion, requiring player retry. The nested-admission fix prevents a synchronous callback from inserting a competing plan into that gap; it does not lock every lifecycle API. This remains a documented non-blocking residual for the reviewed 4D.3 boundary, not a claim of universal atomic rollback.
- Minor test gap: an explanation question while an adaptation proposal is actively pending is not directly asserted for dismissal. Existing active new-message dismissal, no-plan explanation retention, real host rejection/explanation association, and stale-confirmation cases remain covered.
- The tactical Spearman wood-gathering problem is separate and unclaimed. Unsupported objectives/targets/budgets remain fail-closed. Phase 4D.4 advisories and the final Phase 4D/Phase 4E verdict are not part of this gate.
