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
- Detailed worker evidence and residual concerns: `phase4d1-core-task-report.md`, `phase4d1-host-task-report.md`, `phase4d1-task-report.md`, and `phase4d1-gate-report.md`.

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
