# Phase 4E.1 Task 1 — strict semantic JSON contract

## Result

Task 1's focused Unity EditMode contract tests passed. This is parser-contract evidence only; admission against the live `CommanderContext`, host integration, provider behavior, and wider regressions belong to later tasks/gates.

## RED and GREEN evidence

| Gate | Unity EditMode job | Observed result |
| --- | --- | --- |
| RED | `a98e92f4eaee40a591b7bc7d7cf47ce6` (exact-method retry `5203efe151824d74b3d24991eb75ad8b`) | Zero runnable tests because the new test assembly could not compile. `Logs/Editor.log` lines 609495–609524 reported CS0103 for absent `CommanderSemanticJson`, `CommanderSemanticOutcome`, and `CommanderSemanticNodeType`; no unrelated compiler error was reported. This matches the missing-contract expectation. |
| GREEN | `80cf31a3531d4daf8d3c630f27d728a4` | `OpenEmpires.Tests.CommanderPhase4E1SemanticJsonTests`: 31 total, 31 passed, 0 failed, 0 skipped, `Passed` in 0.377 seconds. Unity error console: 0 entries. |

The parent task owned the sole Unity test runner and supplied both run results. The initial GREEN attempt found no Unity instance and did not create a job; the successful run above followed editor reconnection and compilation/domain reload.

## Changed files

- `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs` and `.meta`: immutable data-only node/result contract and bounded outcome/node-type enums.
- `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs` and `.meta`: strict JSON parser with size/depth/node caps, duplicate-property rejection, exact names, schema/type allowlists, bounded counts and safe invalid result.
- `Assets/Scripts/AI/Commander/Phase4E.meta`: Unity folder metadata.
- `Assets/Tests/EditMode/CommanderPhase4E1SemanticJsonTests.cs` and `.meta`: 31 focused cases for typed valid nodes/outcomes and malformed, unsupported, excessive, or authority-like inputs.

## Self-review

- Model JSON is parsed only to detached typed data. No parsed object, raw response, player/source, entity ID, position, command, approval, or game authority is retained in the result.
- A rejected parse returns `IsValid=false` with zero nodes and a fixed safe explanation, never a partial request. `Clarify` and `Unsupported` carry no nodes; a `Request` requires one to four nodes.
- Duplicate keys, trailing content, comments, NaN, trailing commas, unknown or node-inappropriate fields, numeric/case-varied enum names, out-of-range counts, markup in explanations, oversize raw output and excessive depth are rejected. The parser caps the array before allocating retained nodes.
- Enum and count parsing is not a gameplay authorization: current-context population, structure availability, and strategy support remain admission responsibilities in subsequent tasks.
- Only the focused Task 1 EditMode class was run here. No full EditMode or PlayMode regression claim is made.

## Post-review Task 1 boundary fix

The Task 1 review identified two defects in the first commit (`e39d987`): the `internal` result constructor let any runtime class set `IsValid=true`, and the structure parser accepted defined-but-unsupported enum values such as `Landmark`. The parser now owns result construction through a private constructor and a validating internal parse method; the public `CommanderSemanticJson.Parse` signature is unchanged. The structure name allowlist is restricted to the six types supported by `CommanderIntentCatalog`; context-specific legality still belongs to admission.

| Gate | Evidence | Observed result |
| --- | --- | --- |
| Fix RED | Unity EditMode job `8696813c3ffb4c0187fc34761870233d`; `Docs/CommanderPhase4E/phase4e1-task1-fix-red-8696813c.xml`, timestamp 2026-09-26 16:23:40 UTC | 33 total, 31 passed, 2 failed, 0 skipped. `Parse_RejectsDefinedButUnsupportedStructure` returned `IsValid=true` for `Landmark`; `SemanticResult_HasNoAssemblyAccessibleConstructorOrValiditySetter` found an assembly-accessible constructor. Both are the expected defects. |
| Fix GREEN | Unity EditMode job `43613b32d7e941dd9ad661a0b2da177a`; raw result `Docs/CommanderPhase4E/phase4e1-task1-fix-green-43613b32.json` | 33 total, 33 passed, 0 failed, 0 skipped, duration 0.7775 seconds. Unity error console: 0. |

The RED Unity MCP job handle remained orphaned/running after a domain reload despite no active test execution. The parent runner used Unity's authoritative `TestResults.xml` for the terminal RED evidence, then cleared the stale handle only after editor state confirmed no running tests. This was a runner-state anomaly, not another test failure.

Self-review of the fix: no gameplay admission path changed; result constructors are private, `IsValid` has no setter, and the parser's data-only node/result shape remains unchanged. Only the two reviewed boundaries and their regression tests changed. The focused class is green; broader regressions remain unclaimed here.
