# Phase 4C.1 fix-round-2 final regression

Run date: 2026-09-16. Source was frozen before the runs. Round-1 artifacts
remain untouched under their original names.

## Full suites

| Suite | Job | Terminal result | Counts | XML artifact |
| --- | --- | --- | --- | --- |
| EditMode | `2c350469769c460ea10838ddaacc0737` | Passed | 512 total, 512 passed, 0 failed, 0 skipped | `phase4c1-fixround2-full-editmode-TestResults.xml` |
| PlayMode | `14031c0c376d450ca2a6f845f69ab555` | Passed | 69 total, 69 passed, 0 failed, 0 skipped | `phase4c1-fixround2-full-playmode-TestResults.xml` |

The PlayMode start call first timed out at transport level; editor state showed
the same job actively running, so it was polled to completion without a
restart. XML validation found 512 EditMode test cases / 512 unique IDs and 69
PlayMode test cases / 69 unique IDs. The focused Phase4C1 tests all passed.

Phase4C1 named-test coverage is 9 EditMode cases / 9 unique IDs and 17
PlayMode cases / 17 unique IDs. The PlayMode count is 17 because round2 adds
`GenericAttackWhitespace_RequiresRetainedPreferenceBeforeProviderCall`.

## Round2 review package

`phase4c1-fixround2-review/` contains actual normalized diffs against the
three complete fix-round-2 before snapshots plus `manifest.json` with current
SHA-256 hashes. Round-1 review/evidence artifacts were not overwritten.

## Refreshed boundary audit

`phase4c1-fixround2-final-boundary-audit.json` reports: 7 changed, 5 new, 0
missing; 55/55 frozen hashes present and unchanged; 6 advisory host-reference
candidates; 0 high-confidence credential shapes and 0 assignment candidates;
`.env` present, untracked, ignored; AQ-dot detector self-test passed.

All six candidates are classified `documented-host-candidate` in
`CommanderChatUI.cs`. No explanation-service/value-type reference candidate was
reported by this audit.
