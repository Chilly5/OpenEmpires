# Commander Phase 4D Progress

## Preflight baseline — 2026-09-23

Created `phase4d-source-baseline.json` before any Phase 4D production changes. This is a read-only capture of the live checkout; no Unity tests were run for this preflight.

- Branch: `unit_models_and_voice_control`
- HEAD: `205c4fa8d2aae8d5abe5b7806f1fb66bb0727519`
- The worktree is dirty. Phase 4C housing source/test/docs edits remain uncommitted and are treated as the baseline, not discarded or absorbed into a commit here.
- The final Phase 4C source manifest has 27 entries; all 27 current file hashes match. Its own SHA-256 is recorded in the baseline JSON.
- The protected-boundary audit records 55 files; all 55 current hashes match. The frozen exact path/hash pairs are preserved in `phase4d-protected-boundary-frozen.json`. All declared changed/new hashes also match, with zero missing files or audit gaps. The audit artifact's “in-flight source; not final phase evidence” wording is a label caveat, not a current hash mismatch; its recorded hashes independently match disk.
- Disk XMLs report full EditMode 571/571 and PlayMode 87/87; focused Phase 4C.4 EditMode 31/31 and PlayMode 10/10; housing-specific green EditMode 3/3 and PlayMode 1/1. The passing housing PlayMode case is `AtCurrentCap_ApprovedRangedPlanBuildsHouseThenTrainsAndReleasesReservations`. The final regression summary documents the observed House completion, Archer training, plan completion, and reservation release.
- Root reviewed the untracked `Assets/InitTestScene4e274152-2989-491e-9211-298d6647bbfd.unity` and classified it as a test-runner-only scene: it contains `PlaymodeTestsController`, its GUID is unreferenced, and it is not in Build Settings. It is retained for now; no cleanup was performed. The separately deleted tracked test/recovery scene files remain as recorded in the baseline status snapshot.

## Phase 4D implementation status

Phase 4D.1 focused implementation and runtime scenarios (Tasks 1–4) are complete. **Phase 4D.1 gate verdict: PASS, with documented evidence limitations.** This is a sub-phase verdict only: it is not a full Phase 4D regression, Phase 4D completion, or Phase 4E readiness claim. Full EditMode/PlayMode suites remain assigned to later Phase 4D gates (4D.2/4D.4). The captured Phase 4C baseline remains immutable; current source hash drift is reported separately from baseline-reference checks. Preserve Phase 4C authority/safety boundaries and keep the pre-existing scene/recovery churn untouched unless its owner explicitly resolves it.

## Phase 4D.1 current evidence — 2026-09-24

- Core lifecycle focused EditMode final-source run: **39/39 passed, 0 failed, 0 skipped**, Unity MCP job `711dd649ecd84591869561252dcc7bb9`; genuine NUnit XML `phase4d1-core-overflow-green-results.xml`, SHA-256 `DCDBB01966EDE586B687BE5E16391F7ECC9B70AE8339A3CE4972088E1FFF7680`. Unity compile/domain reload completed with console errors 0 before the run. This is focused core evidence, not a full EditMode suite.
- Host Task 3 focused PlayMode run: **11/11 passed, 0 failed, 0 skipped**, terminal job `438af9bd8518472e90cf5bf28d10be7c`. Covered whole-form/provider isolation, ownership, ambiguity/selection, UI callback staleness across same-number new pipeline, cancel confirmation, reset behavior, and pending/in-flight interpretation invalidation. MCP did not provide native NUnit XML for this job.
- Relevant regressions: Phase 4C.2/4C.4 PlayMode **16/16** (`9033961768b349c9ac941669f605bf6b`) and 4D.1 core EditMode **39/39** (`9d2a99c1b4134e0b8e4bac3525c78534`), both with zero failures/skips. These scoped results are not full-suite results.
- Independent core third review and host review: no Critical or Important findings reported within their respective scopes. This is not the final 4D.1 architectural/security gate.
- Task 4 final test source SHA-256 is `766B03668E7FE6C8548339710A4DF2F86E10A22B5498E920CBE40A54588E14DB`, matching `sourceSha256` in `phase4d1-task4-final-jobs.json` and the live test file. Durable final Unity MCP payloads: Task 4 PlayMode **4/4** (`1d359ebf1ca34289947ef79b2d1c197b`) and combined affected PlayMode **48/48** (`54cfbf047cf445deb6110ad22b3f9fcc`), each 0 failed / 0 skipped. Task 4 did not emit native NUnit XML; the exact terminal payloads in that JSON are its durable evidence.
- Additional recorded scoped evidence: latest current-source affected EditMode **70/70** (job `9e95280e6de544c38bd25ed13de8e283`, complete payload retained), host PlayMode **11/11**, and Phase 4C.2/.4 PlayMode regression **16/16** are reported in the Phase 4D.1 task/host reports; these do not constitute full suites. The distinct named-ownership EditMode job `bab99cbce7784ec58181cdc7f55610ca` passed **39/39**, including `PlayerCannotPauseOtherPlayersPlan`, `PlayerCannotCancelOtherPlayersPlan`, and `PlayerCannotResumeOtherPlayersPlan`.
- Gate 5 static/source-boundary audit: **PASS** (independent Luna audit, as reported for this gate); baseline verifier references also **PASS**: 27/27 Phase 4C paths present, 55/55 frozen protected paths present, 54 unchanged and one intentional `CommanderGoalManager.cs` drift, no missing paths, pinned XML 6/6. This does not replace the final Phase 4D boundary audit/manifest.
- Residuals/limits: no full EditMode or full PlayMode suite was run for this 4D.1 gate; no native XML exists for Task 4 or the affected PlayMode jobs; the frozen protected hash drift is intentional and documented; baseline audit retains its “in-flight/not final” label caveat; pre-existing deleted/untracked scene/recovery churn remains unresolved and untouched; no paid/external API calls were made. Core report retains arbitrary external event-subscriber reentrancy during revision preflight as a residual concern. Phase 4D.2 production status is tracked separately and should not be inferred from this 4D.1 verdict.

## Phase 4D.2 current status — 2026-09-25

- Tasks 1–3 have scoped acceptance; this is not the Phase 4D.2 gate verdict. Task 3's exact current-source PlayMode job `703ab3a549034a809dd3034817a0ba92` passed **1/1**, 0 failed, 0 skipped, 0 inconclusive. The test source SHA-256 is `07708958A428AD4BCBE42898F2A0185185EF996888F4ECAA395C9C9295CA1D0F`; native NUnit XML `phase4d2-task3-exact-runtime-703ab3a5.xml` SHA-256 is `081EB6CB6F9E256F2F42B613CBCB3120E57167F1F0E179486E4C5BBDFAC7077E`. See `phase4d2-task3-report.md` for observed wait, same-plan progress/completion, and approved-template assertions.
- Independent Sol rereview closed the prior Important finding about checking commands/goals against the approved template; no Critical or Important findings remain in that review.
- Full EditMode job `0d7f26870b1a4c89b138c6b2400ce5e3` passed 656/656 before the final Task 3 test-only patch. It is historical only; XML `phase4d2-historical-full-edit-0d7f2687.xml` SHA-256 `89D5F34CF6CF1B177A6730FD496E0641D360D6B98D84377462104321354BB150`.
- Final-source full EditMode job `3273c4992ef749ed80a08dfd517bda60`: **656/656 passed**, 0 failed/skipped/inconclusive; native XML `phase4d2-final-full-edit-3273c499.xml`, SHA-256 `F0847CFC6DF4EBBC72077981FC61B36D506E39ED86798C72302BEBE04F81615E`. Full PlayMode job `ab908946aba645f4a48a71995af9e02d`: **116/116 passed**, 0 failed/skipped/inconclusive; native XML `phase4d2-final-full-play-ab908946.xml`, SHA-256 `05BFB892A53E3873A0B9B023867DF1CCDD59168972381A27DC6335B5E092B72D`.
- Parsed full-suite XML discovery includes all 42 `CommanderPhase4D2Tests` EditMode cases, 13 `CommanderPhase4D2HostPlayModeTests`, and the single `CommanderPhase4D2RuntimePlayModeTests` PlayMode case. Root final baseline verifier PASS: 27/27 Phase 4C paths; 55/55 protected present (54 unchanged, one documented `CommanderGoalManager` drift); pinned XML 6/6. Changed health/host source had no 4D.2 forbidden references; credential-pattern scan across Commander source/tests/docs found 0 files.
- Root gate decision: **Phase 4D.2 PASS** (Gates 1–5 and both required complete suites); see `phase4d2-gate-report.md`. This is not the final Phase 4D verdict. Scene provenance remains a carried final-source residual: the untracked `InitTestScene` observed during the full run disappeared automatically by test end; the pre-existing scene/recovery churn was later absorbed into current branch commit `4ae6269`. Preserve it and do not clean based on this report.
- Detailed Phase 4D.2 worker evidence and residual concerns: `phase4d2-task1-report.md`, `phase4d2-task2-report.md`, `phase4d2-task3-report.md`, and `phase4d2-gate-report.md`.

## Baseline verifier — 2026-09-23

Added `verify-source-baseline.ps1`. Default mode verifies pinned Phase 4C references and test artifacts, requires all 27 manifest paths and 55 frozen protected paths to exist, and reports live protected-file hash drift without treating post-baseline Phase 4D edits as corruption. `-CheckOriginalSource` additionally compares all 27 current source files to their captured Phase 4C hashes; expected drift is diagnostic and does not change the baseline. Neither mode prints file contents or secret values.

Default run (`.\Docs\CommanderPhase4D\verify-source-baseline.ps1`) — exit code 0:

```text
Baseline references: PASS; manifest SHA-256 76D571C3FB9FED87206712D5FD559FE162F1E034754CA2E3B8A765174A4E245F; audit SHA-256 A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122; frozen audit SHA-256 A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122
Phase 4C source paths present: 27/27
Frozen protected paths present: 55/55; current hashes unchanged 54; changed 1; missing 0
FROZEN_PATH_HASH_DRIFT (current working tree; inspect against Phase 4D changes): Assets/Scripts/AI/Commander/CommanderGoalManager.cs
Pinned XML artifacts verified: 6/6; summary SHA-256 1F8FBDA5F303A659BDDDB391CB2284B460343C7442948D8BF65C61923FFA7B7C
Original source content hashes: not checked (use -CheckOriginalSource); ongoing Phase 4D edits are permitted.
```

Original-source comparison (`.\Docs\CommanderPhase4D\verify-source-baseline.ps1 -CheckOriginalSource`) — exit code 1 because two captured-source hashes now differ; all reference, path-presence, frozen-list, and pinned-artifact checks passed. The mismatches are the in-progress 4D.1 edits, not a baseline rewrite or a claim that the old hashes are current:

```text
Baseline references: PASS; manifest SHA-256 76D571C3FB9FED87206712D5FD559FE162F1E034754CA2E3B8A765174A4E245F; audit SHA-256 A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122; frozen audit SHA-256 A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122
Phase 4C source paths present: 27/27
Frozen protected paths present: 55/55; current hashes unchanged 54; changed 1; missing 0
FROZEN_PATH_HASH_DRIFT (current working tree; inspect against Phase 4D changes): Assets/Scripts/AI/Commander/CommanderGoalManager.cs
Pinned XML artifacts verified: 6/6; summary SHA-256 1F8FBDA5F303A659BDDDB391CB2284B460343C7442948D8BF65C61923FFA7B7C
Original source hash check: 25/27 match; mismatches 2
ORIGINAL_SOURCE_HASH_DRIFT (diagnostic; may be an intentional Phase 4D edit): Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs
ORIGINAL_SOURCE_HASH_DRIFT (diagnostic; may be an intentional Phase 4D edit): Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs
```

## Phase 4D.3 source-freeze evidence — 2026-09-26

Tasks 1–3 reached scoped acceptance; Task 4 added real approved-plan resource and population/House recovery PlayMode tests without a production repair. Independent whole-phase Sol review found a protected planner nested-admission Important issue; the root-coordinated test-first guard and bounded intent-history follow-up closed it, with scoped independent rereview reporting no remaining Critical/Important. The Task 3 C2 host-association Important finding was separately closed with a real Confirm rejection/explanation test and mutation check. **Root Phase 4D.3 gate verdict: PASS** after independent current-source evidence checks; see `phase4d3-gate-report.md`. Phase 4D.4 and final Phase 4D remain open.

- Task 4 final focused runtime 2/2 (`03568f54ef0d4582ba3c86c76ebbe54d`) and affected PlayMode 29/29 (`faff8396424d461a8c3bcc9f5e634f60`) passed, zero failed/skipped. Full final-source EditMode **689/689** (`e16cfa9309dd42dcbfdb59e4b333cfa4`) and sequential PlayMode **145/145** (`e5e4a460cdcb4c2e8088d8817cb77f6b`) passed, zero failed/skipped/inconclusive. All 689/145 unique test IDs and complete MCP terminal payloads are preserved in `phase4d3-full-edit-e16cfa93.json` / `phase4d3-full-play-e5e4a460.json`; no native XML was returned. The two required runtime IDs, 22 value tests, and 26 host tests were discovered. Current Unity console `error CS` query returned 0.
- Baseline verifier default PASS: Phase 4C paths 27/27, frozen protected paths 55/55 (54 unchanged, one intentional documented goal-manager drift), pinned XML 6/6, no missing/gaps. Original-source diagnostic now matches 24/27; the three mismatches are documented Phase 4D edits to `StrategicPlan.cs`, protected `StrategicPlanner.cs`, and 4D.3 `CommanderChatUI.cs`. Final planner SHA-256 `47171C6D433B54A78FF98EF900AA1C57807081C1A34F51CDAEBD08B6591177EC`. No package/settings status change or remaining untracked TestRunner scene after the complete PlayMode run. See `phase4d3-gate-report.md` and `phase4d3-boundary-audit.json` for scope and hashes.
- Residuals remain explicit: replacement is not a general transaction against throwing external observers (cleanup finishes and the player retries); active-adaptation explanation-query dismissal is a Minor untested edge. The separately reported tactical Spearman wood-gathering issue is not diagnosed or claimed fixed. No 4D.4 advisory or final Phase 4D/Phase 4E verdict is implied.

## Phase 4D.4 scoped work before final freeze — 2026-09-26

The detached offline advisory feed (Task 1) and transcript-only host integration (Task 2) reached scoped acceptance after behavior-first fixes and independent Sol rereviews; neither is a Phase 4D.4 or final-Phase4D gate verdict. Task 1's final focused EditMode run passed 12/12 and affected 4D.2 health tests passed 42/42. Task 2's real-host PlayMode passed 11/11, affected EditMode 144/144, and affected PlayMode 75/75, all with zero failed/skipped; complete terminal payloads and SHA-256 values are retained in their task reports. No background paid advisory call or advisory execution authority was added.

Task 3A added real PlayMode D/E/F scenarios, initially passing focused 3/3 and affected 70/70. At this historical checkpoint, independent scenario review found that D did not yet attribute post-replacement work to the approved new plan, and F reinitialized the host without actually ending the old match systems. Those were **Important evidence gaps at that checkpoint**; the final-source section below records their closure. The user's tactical 10-Spearman wood shortage was separately observed and is not proven fixed by the strategic Phase 4D work.

## Phase 4D.4 final-source gate — 2026-09-26

The preceding paragraph is a historical in-progress snapshot. Task 3A's D/F Important proof gaps were closed with test-only strengthening and independent Sol rereview; accepted D/E/F focused PlayMode job `9fea77a2651c4e6da2f87817e8fcd31d` passed 3/3. D now isolates paused Plan B for 1,200 ticks against orphaned Plan A work, resumes B for 1,200 ticks, and attributes new child goals to B. F cancels/disposes old Commander systems before releasing the held provider reply into a new simulation and host; it is a programmatic reset, not scene teardown. E additionally checks goal states and Plan B's next tick.

Final source fixes added exact constructor/host simulation-context validation and first-transition advisory seeding after reset, with behavior-first RED/GREEN and a corrected four-test compatibility regression. Final-source focused advisory EditMode **14/14** and host PlayMode **12/12** passed. Fresh complete EditMode job `9c1ea3310e2342d09e034d80ec4d0fb7` passed **703/703**; sequential complete PlayMode job `69b5be6707f74388b3bb27e7489226d1` passed **159/159**, each zero failed/skipped/inconclusive and all fully qualified names unique. All 52 required D1–D4 method names and A–F runtime scenario IDs were discovered and passed. Three required method names intentionally occur in both EditMode and PlayMode classes; their six fully qualified IDs are distinct. Only the complete terminal JSON payloads were returned for final jobs; no native XML was available.

The final manifest covers 56/56 baseline-to-working-tree Commander source/test/meta paths with live SHA-256 matches, and the final boundary audit reports 55/55 protected paths present, 54 unchanged, one intentional reviewed goal-manager drift, no audit gap, and zero package/settings status drift. The original specification copy, relevant tracked diffs, source hashes, test payload hashes, lifecycle/authority maps, limitations, and historical failure/fix record are in `antigravity-audit-package.md`. Three Unity TestRunner/recovery scene/meta pairs remain untracked and untouched. Root's 4D.4 Gates 1–5 decision is **PASS** in `phase4d4-gate-report.md`; the separate whole-phase final verdict and external-review handoff are in `../CommanderPhase4D.md`. The tactical 10-Spearman wood-gathering issue remains unclaimed and separate.
