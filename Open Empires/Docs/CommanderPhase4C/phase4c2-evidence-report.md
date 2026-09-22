# Phase 4C.2 Task 2 evidence report

Date: 2026-09-21 (Asia/Tehran). Scope is mechanical review/package evidence only. No production or focused-test source was modified.

## Frozen hash verification

Command:

```powershell
$manifest=Get-Content -Raw 'Docs/CommanderPhase4C/phase4c2-task1-source-hashes.json' | ConvertFrom-Json
foreach($item in $manifest.files){ Get-FileHash -Algorithm SHA256 -LiteralPath $item.path; (Get-Item -LiteralPath $item.path).Length }
```

Result: all 7 manifest entries matched both expected byte length and SHA-256. Hash mismatch count: **0**.

The exact current values are reproduced in `phase4c2-review-package.md` and the manifest remains the authority.

## Exact CommanderChatUI diff

Command:

```powershell
git diff --no-index --no-ext-diff -- Docs/CommanderPhase4C/phase4c2-task1-before/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
```

Result: exit code **1**, expected for a non-empty no-index diff; **102 diff lines**. The exact diff is included in `phase4c2-review-package.md`. Changes are limited to `partial`, explanation lifecycle resets, whole-form query routing, preservation of the last meaningful decision during ordinary submission, synchronous provenance capture/clear, and event projection.

## Boundary checker

Checker source/help inspection:

```powershell
Get-Content -TotalCount 240 Docs/CommanderPhase4C/check-boundaries.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/CommanderPhase4C/check-boundaries.ps1 -?
```

The checker has one optional `-Root` parameter, reads the baseline, scans source/test C# and docs for bounded patterns, and writes JSON only to stdout. It was run read-only with output redirected to:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/CommanderPhase4C/check-boundaries.ps1 -Root (Get-Location).Path | Set-Content Docs/CommanderPhase4C/phase4c2-boundary-audit.json -Encoding UTF8
```

Fresh result (`generatedAt`: `2026-09-21T13:44:51.8926357Z`):

| Audit field | Result |
|---|---:|
| Baseline changed files | 7 |
| Baseline new files | 11 |
| Baseline missing files | 0 |
| Frozen boundary files | 55 |
| Frozen boundaries unchanged | 55 |
| Frozen boundary audit gaps | 0 |
| Advisory forbidden-reference records | 6 |
| High-confidence credential shapes | 0 |
| Credential assignment candidates | 0 |
| `.env` present / tracked / ignored | true / false / true |

The six advisory records are documented host candidates: `CommanderChatUI.cs` contains the existing `StrategicPipeline`, `GameSimulation`, `CommanderGoalManager`, and `StrategicPlanner` integration; `CommanderChatUI.Explanations.cs` uses the existing read-only `CaptureContext()` path for current-plan projection. They are not references from the value/service files to simulation or command authority.

## Independent seven-file search

The seven frozen files were searched independently with the following PowerShell pattern groups (credential values were not emitted):

```powershell
$credential='AIza[0-9A-Za-z_-]{20,}|sk-[0-9A-Za-z]{20,}|AQ\.[0-9A-Za-z_-]{20,}|(?i)(api[_-]?key|secret|password|token)\s*[:=]\s*["''][^"'']{8,}'
$boundary='\b(GameSimulation|CommandBuffer|ICommand|StrategicPlanner|StrategicPipeline|CommanderGoalManager|StrategicAIApprovalBridge|StrategicDecisionPolicy)\b'
$authority='\b(EnqueueCommand|Execute|Evaluate\s*\(|Submit|Approve|CreatePlan|Tick\s*\(|Set[A-Z]|Add[A-Z]|Remove[A-Z]|\.Simulation\b|\.CommandBuffer\b)'
$hidden='\b(GameSimulation|StrategicPipeline|StrategicPlanner|StrategicIntent|StrategicDecisionRecord|StrategicPlanState|CommanderGoal|CommandBuffer|ICommand|UnityEngine\.Object|MonoBehaviour)\b'
Select-String -LiteralPath $sevenFrozenFiles -Pattern $credential,$boundary,$authority,$hidden -AllMatches
```

Results:

- Credential shapes: **0**.
- Boundary references: **30 matching lines** across 3 files: 16 in `CommanderChatUI.cs`, 2 in `CommanderChatUI.Explanations.cs`, and 12 test references in `CommanderPhase4C2PlayModeTests.cs`. The checker’s narrower forbidden-symbol detector reports 6 records because it only scans its configured production-boundary subset.
- Direct execution/authority matches: matches are confined to existing host UI flow and test setup/assertion plumbing. The explanation service/value files do not call `EnqueueCommand`, mutate simulation, or invoke planner/policy authority. `CommanderChatUI.Explanations.cs` only calls `CaptureContext()` and copies primitive fields.
- Hidden simulation-object retention: no simulation/planner/record/Unity-object fields exist in `ExplanationContext`, `ExplanationResult`, or `CommanderExplanationService`. The host retains only an immutable copied `ExplanationContext`; source record/intent/plan references are local variables during projection and are not stored. The PlayMode test intentionally retains simulation/planner references as its fixture and uses Unity objects for cleanup.

## Review package and scope

`phase4c2-review-package.md` contains the exact modified-file diff, a hash/byte table for all seven frozen files, and full contents of all seven frozen files with path delimiters.

Only these Task 2 evidence artifacts were created by this pass:

- `Docs/CommanderPhase4C/phase4c2-review-package.md`
- `Docs/CommanderPhase4C/phase4c2-boundary-audit.json`
- `Docs/CommanderPhase4C/phase4c2-evidence-report.md`

No commits, packages, settings, credentials, Unity MCP actions, or production/test source changes were performed.

## Concerns

1. The checker is explicitly an in-flight static audit, not runtime proof; its six host-candidate references require architectural interpretation as above.
2. The independent authority regex is intentionally broad and produces UI construction/test-fixture false positives; it does not establish a production authority violation.
3. Fresh full EditMode/PlayMode regression and runtime proof are outside this mechanical evidence pass and remain the orchestrator’s gate.

Status: **DONE** for the requested mechanical review package and evidence artifacts.

## Fix-round 1 audit note

The scoped fix-round-1 package is `phase4c2-fixround1-review-package.md`. It contains the two independent-review findings verbatim, exact unified diffs from the prior embedded versions of `CommanderExplanationService.cs`, `CommanderPhase4C2Tests.cs`, and `CommanderPhase4C2PlayModeTests.cs`, exact current hashes, and current full contents. The boundary checker was refreshed read-only at `2026-09-21T14:06:41.9103789Z`: 55/55 frozen boundaries unchanged, 0 gaps, 6 advisory references, and 0 credential-shape or assignment candidates. No Unity or source edits were performed in this fix-round evidence pass.
