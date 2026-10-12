# Task 6 baseline reconstruction provenance

Status: reconstructed baseline verified; this is not a true pre-touch snapshot. The current source was read only and never edited.

## Inputs and method

- Base bytes: `evidence/task-2-fix1-review-baseline/Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Clarification.cs.snapshot`
  - SHA-256: `29E1DEE01FDFAECB688C11E439C8046E1C3A7EE9DCB5006E2F6BBFF736BCDE1E`
- Incremental patch: `evidence/task-2-fix1-review-package.md`, lines 273–344, section `Source delta: Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs`
  - SHA-256: `5B9351CBCE310BF4272623997079620027BB78EFBDEE83C2323ABD9D32E4EE8C`
- Task 2 final-hash authority: `task-2-report.md`, fix-round-1 file table; expected SHA-256 `D6582A384846F43ABADA7409CCC5EB8974CA93C6F9F8D00105A787A070A6602C`.
- The fix1 section was applied to a byte-for-byte copy of the preserved base with `apply_patch`: correction replies do not supersede the draft; graph clarification expires after six replies, while worker draft expiration remains three. The diff's final extra blank line was removed. Task 2 fix2/fix3 review-package searches contained no `CommanderChatUI.Clarification.cs` delta.

## Verification and boundary

Reconstructed file: `task-6-baseline/CommanderChatUI.Clarification.cs.reconstructed.snapshot` — SHA-256 `D6582A384846F43ABADA7409CCC5EB8974CA93C6F9F8D00105A787A070A6602C`, exact match to the Task 2 final report. This verifies the reconstruction against the recorded final source hash; it does not convert it into an actual pretouch capture.

The live source was only hash-read during this task and measured `28F940BADAC21A33BF20B6C99DE4E9716C498B2EBDC9C5B2871F352E7A55430A`; it was not used as reconstruction input. No tests, MCP, build, network, credentials, cleanup, or other files were changed. The two new artifacts are the reconstructed snapshot and this provenance note.
