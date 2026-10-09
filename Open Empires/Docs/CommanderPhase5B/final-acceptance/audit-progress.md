# Phase 5B final acceptance audit — independent verification

2026-10-09. Independent audit finished; see ../final-acceptance-verification.md for the verdict, complete matrices and exact Sol handoff.

Current branch `unit_models_and_voice_control`, HEAD `20da6eff6b63ebbf82cfd6570bbc416ce1d71832`.
Verified 374 manifest records, aggregate `bfaeb894e89f4e326b73f2c763ac1f9fb95ce2bf70ba492ca6428f206f5ab39e`.
Unity MCP project root and version match the working directory and Unity 6000.5.9f1; StandaloneWindows64.
Existing production/test dirty changes preserved. No production edits, reset, clean, commit or push.

## Evidence collected

- Focused/hostile EditMode job `23509fea74734a5794afb1a646e249af`: 124 passed, zero failed/skipped, 35.1114036 seconds.
- Mistyped regression assembly `OpenEmpires.Tests.EditMode`, job `e2a73c4e8a7b453cbc590589b9242604`: zero cases. Excluded from pass evidence.
- Correct full EditMode assembly `OpenEmpires.EditModeTests`, job `5f2b1d97e0dd4e5abffb6c72527ef5c3`: terminal failed, 1585 completed, 20 uncapped failure records. MCP omits the aggregate result for failed jobs (TestJobManager.ToSerializable only serializes Succeeded). Failure list saved in offline-regression.json; no invented pass total.
- Independent focused PlayMode job `0733b3b46561415abdfb7b444a48152f`: 9/9 passed, 2.9886258 seconds, zero failed/skipped.
- First match: 10 HTTP attempts; one timeout, one network error, eight HTTP 200. Exact idle Lumber/Mill, House→Wood, repeated Mill and protected Gold all reached native completion. No-idle Lumber blocked without construction. Same-source declaration/diagnostic/repair focused fixtures passed.
- Natural variation “Build a House first, and only after it finishes send a villager to wood.” returned Clarify, falsely saying the contract cannot wait for completion and mentioning newly produced villagers. No pending draft/goals. Exact “then” case already proves supported completion dependencies. Preserve this provider-stage failure.
- Original Food clarification retained 400 Stockpile through local “4”; native delivery reached 210 then visible berries depleted, so it failed truthfully. A separate fresh match provisions visible berries before submission; replay completed natively at 400 Food. Original outcome remains intact.
- User stopped Computer Use with Escape and later the first match ended. Continuation uses actual UGUI callbacks via MCP; no further Computer Use. First file live-evidence.json preserved; continuation-live-evidence.json is separate. Conservative total cap remains 100 (10 prior + 90 continuation).

## Final closeout

All requested Editor scenarios and focused/schema checks have classified outcomes
in the final report. New provider failures were preserved; a named Castle control
completed native Age 3, and exact producer/new-unit bindings completed. Additional
Food did not reach its target. Native UI cancellation, Human Stop recovery,
conversation reset and fresh-runtime clearing were observed. Two train-five
timeouts precede its successful third capture entry; they remain consumed.

22 actual HTTP attempts are terminal (17 HTTP 200, five transport errors), within
the existing 100 authorization. The temporary probe was detached and PlayMode
stopped. Current source identity remains 374 records with zero mismatches.

Release gates: Sol provider fixes, additional-resource recovery classification,
full regression reconciliation and rerun, authorized fresh Windows/Web builds and
packaged acceptance. No next phase; no production/test edits or Git mutations.
