# Task 6 corpus data preparation report

Status: prepared; data lint passed. This is not Task 6 runner implementation, a fixture execution, a Unity test, or an acceptance gate. No source/test/MCP/Unity/provider/network/build/credential action was taken.

## Outputs

- `task-6-corpus-map.md` — reconciles the earlier inventory with current Task 3/4 focused evidence and preserves explicit live/integrated gaps.
- `evidence/phase5c-replay-corpus.v1.json` — 25 stable offline replay-input cases spanning the closure matrix, with safe original turns, expected semantic fields/constraints/order/exact-result/approval/call expectations, and exact fixture path/method references where verified.
- All inputs remain `schemaVersion: 1`, `evidenceMode: offline-fixture-inputs`, `executionState: not-run`. No case outcome, native/live/UI pass, actual usage, or cost is asserted.

## Validation

Read-only PowerShell command logic used: `Get-Content -Raw ... | ConvertFrom-Json`; iterate cases for required case/expectation fields and unique IDs; `Test-Path -LiteralPath` each nonempty fixture path; check each referenced test name against a method declaration (not merely any text occurrence); print counts. Final result: `cases=25 uniqueIds=25 errors=0`. The prior `20/20` figure was the historical lint before the four worker-mode/AdditionalGathered cases were added. Synthetic malformed and schema-valid-wrong cases deliberately have no fixture refs; their exact test-coverage gap is stated in the corpus/map. This validates JSON shape and referenced declarations only—not test execution or semantic correctness.

`git diff --check -- Docs/CommanderPhase5C/task-6-corpus-map.md Docs/CommanderPhase5C/evidence/phase5c-replay-corpus.v1.json Docs/CommanderPhase5C/task-6-corpus-data-report.md` exited 0.

Review verification also confirmed the public contract spellings `Berries`, resource `Stockpile|AdditionalGathered`, and distinct worker quantity `TargetTotal` in the current request-pack/vocabulary and targeted Graph fixture sources.

The parent review corrected the Mine and House clarification transcripts/approval order, resource/source enum spellings, explicit Move versus Gather DynamicPlans and their separate pending action-plan approval, public DefendArea labeling, Garrison wording, causal/status call expectations, and separated synthetic provider descriptors from the user utterances. Schema-valid-wrong inputs now assert only mismatch recording/not-fulfilled; actual admission and command behavior remain runner-observed. Added distinct SelectedCount/Additional/TargetTotal worker-mode inputs and a finite AdditionalGathered Food/Berries objective. The recovery follow-up methods are fixture provenance only; the corpus remains not-run, and a full delivery cycle is still unproven. These are corpus data corrections only.

SHA-256:

- `task-6-corpus-map.md`: `3D2293DB8957F0A531CEBBAEC27F4EE9E85C388331EF14B3C07AD360341EEB9A`
- `evidence/phase5c-replay-corpus.v1.json`: `944F5D9FC4F4CF659738E0F7CBF7F4F56BD2211456C2EB5BDB26424BD13B3CFA`

## Gaps retained

No production-seam runner/replay execution or durable per-case output; no populated Windows/served-Web UI screenshots; no integrated real-time/native gameplay proof; no live English/provider interpretation or measured actual token/cost data; no final full regression/build. Task 3/4 accelerated EditMode fixture evidence is cited only as fixture input provenance. The parent owns runner integration and independent review after the Task 5 source work is complete.
