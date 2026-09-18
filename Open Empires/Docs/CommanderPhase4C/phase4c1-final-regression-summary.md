# Phase 4C.1 final regression checkpoint

Run date: 2026-09-16. Source was frozen before these runs; no production,
test, package, settings, credential, or log-suppression changes were made.

## Full suites

| Suite | Job | Terminal result | Counts | Complete artifact |
| --- | --- | --- | --- | --- |
| EditMode | `b13a3f11f32742a399644ec7e6bbcc88` | Failed | 512 total, 509 passed, 3 failed | `phase4c1-full-editmode-TestResults.xml` |
| PlayMode | `d75f39e8c7eb442295919098de7d9fec` | Passed | 68 total, 68 passed, 0 failed, 0 skipped | `phase4c1-full-playmode-TestResults.xml` |

EditMode failures were exactly these existing Phase4B2 reflection-constructor
tests, each reporting `MissingMethodException: Constructor on type
'OpenEmpires.StrategicAIApprovalBridge' not found`:

- `BridgeCancellation_RejectsLateUncooperativeProviderResult`
- `BridgeConfirmation_TransfersExactIntentOnceWithoutExecution`
- `BridgeTimeout_UnlocksEvenIfProviderIgnoresCancellation`

No Phase4C1 test failed in either full suite. The PlayMode result includes all
16 Phase4C1 tests as passed.

## Refreshed boundary audit

`phase4c1-final-boundary-audit.json` reports: 7 changed, 5 new, 0 missing;
55/55 frozen hashes present and unchanged; 6 advisory host-reference
candidates; 0 high-confidence credential shapes and 0 assignment candidates;
`.env` present, untracked, ignored; AQ-dot detector self-test passed.

This remains source/review evidence. The three EditMode failures prevent a
clean full-regression gate until the constructor compatibility issue is fixed
and the required full pair is rerun.
