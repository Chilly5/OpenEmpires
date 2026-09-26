# Phase 4E.1 language-boundary gate

Status: **accepted as a bounded foundation slice**, not acceptance of Phase 4E as a whole. Source HEAD for this gate: `2d5c05a` on `unit_models_and_voice_control`. Phase 4D baseline was 703/703 EditMode and 159/159 PlayMode at `dedf0e4`; the Phase 4E.1 source commits are `e39d987`, `69471e5`, `c067454`, `1d8a96f`, `9ca9ec0`, and `2d5c05a`.

## What is implemented

- A bounded semantic JSON contract accepts exact tactical unit/build/resource and strategic-objective types, with strict shape, length, depth, duplicate-field and unsupported-field rejection. The model cannot supply concrete entity IDs, owners, workers, tiles, coordinates, commands or arbitrary goal scripts.
- The OpenRouter Luna provider receives a detached, bounded game-state projection and returns only parsed semantic results. Player text over the provider limit is rejected before transport; legacy provider methods remain.
- The chat host gives offline lifecycle/explanation controls precedence, then admits one semantic request through game-side context validation and the existing tactical dispatcher. Mixed/multi-node requests are rejected until the compound-goal phase.
- Strategic semantic output is staged as an `AIRecommendation` with a bridge-owned intent identity and a fresh owned context. It creates no plan before the established Approve/Confirm path. Active-plan adaptation still requires explicit Confirm; wrong-owner, stale-generation, busy, reset and owner/runtime replacement cases fail closed.

## Fresh Unity evidence

All jobs below ran after the source was compiled in Unity 6000.5.9f1. The runtime and test assemblies were newer than the edited source/test files; the Unity error-console check after refresh returned zero compiler entries. Result payloads retain fully qualified test IDs where the runner returned details. Counts below exclude the earlier interrupted job `b41c4cdbc436414db2a8d2e14e1a2752`, which had no terminal result after the user restarted Unity.

| Scope | Unity job ID | Result | Evidence |
| --- | --- | --- | --- |
| Phase4E1 EditMode parser, provider, tactical host and strategic bridge (`OpenEmpires.Tests.CommanderPhase4E1*`) | `7db15b4c92cd4322b3865987f06f9b82` | 71/71 passed, 0 failed/skipped | `phase4e1-task4-edit-green-7db15b4c.json` |
| Phase4E1 strategic PlayMode (`OpenEmpires.Tests.CommanderPhase4E1StrategicPlayModeTests`) | `1dbde891631041059806ad457878c2c6` | 6/6 passed, 0 failed/skipped | `phase4e1-task4-play-green-1dbde891.json` |
| Existing Phase4B1 EditMode strategic bridge | `4fa518f700d24e2e95aee69086f23bd3` | 62/62 passed, 0 failed/skipped | `phase4e1-task4-b1-regression-4fa518f7.json` |
| Existing Phase4D1 PlayMode runtime and host | `38440fcad0c8455489ea567c26f83b26` | 17/17 passed, 0 failed/skipped | `phase4e1-task4-d1-regression-38440fca.json` |
| Other relevant Phase4A/A1, Phase4B2 and Phase4D1–D4 EditMode categories | `725255d3f3dd4975b97ca3e7f3d856eb` | 225/225 passed, 0 failed/skipped | `phase4e1-task5-abd-regression-725255d3.json` |
| Other relevant Phase4A, Phase4B2 and Phase4D2–D4 PlayMode categories | `d596635e8a734d229baf2c689c782cf7` | 59/59 passed, 0 failed/skipped | `phase4e1-task5-abd-play-regression-d596635e.json` |

These are **separate focused/regression runs**, not a new whole-project count. The first PlayMode fixture attempts failed for test-harness reasons: a duplicate `CommanderChatUI` singleton was destroyed, the fixture started in an age that forbade Archery Range construction, and one assertion expected a different defensive plan type than its own semantic input. Those fixture errors were corrected before the final 6/6 run; no failed run was reclassified as a pass. The combined 225-test EditMode run briefly showed Unity's `Hold on` window and an MCP disconnect while it was actively logging; it recovered without restart and finished with the terminal result above.

## Boundary and source review

The reviewed production paths are `CommanderSemanticJson`, `CommanderSemanticProvider`, `CommanderSemanticAdmission`, `CommanderChatUI`, and `StrategicAIApprovalBridge`. The parser's allowlist has no provider-supplied identity or position fields. Tactical admission converts only one typed node through the current game-side context validator before the existing dispatcher. Strategic staging validates objective, owner, generation and bridge state, obtains fresh context, derives any cavalry focus from retained game-side preference, and leaves plan creation to existing approval policy. An independent Task 4 source review found no Important authority violation; its requested host reset/adaptation/owner-replacement coverage is included in the final six PlayMode tests.

## Deferred; not claimed by this gate

Spatial references and footprint-edge separation, bounded compound dependencies and created-building result identity, legal Castle Age desired-state progression, concurrent prerequisites/10-Spearman wood-stall repair, bounded follow-ups/clarification, complete UI scenarios A–J, controlled live-Luna corpus, final whole-project regressions, standalone build/smoke and final Phase 4E audit remain for 4E.2–4E.8. The current host intentionally rejects multiple semantic nodes, so the Barracks-then-Spearmen request is **not yet** end-to-end supported.
