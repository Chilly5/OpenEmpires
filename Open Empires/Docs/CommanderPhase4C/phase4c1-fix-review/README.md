# Phase 4C.1 fix-wave review package

This package compares the frozen focused-GREEN checkpoint with the current
source after the four reviewed fixes. The original checkpoint remains
preserved in `../embedded/` and `../phase4c1-review-package.md`.

`package-fix-review.ps1` reconstructs the checkpoint in a temporary directory
from the seven `phase4c1-task1-before` snapshots plus their embedded diffs,
copies the five embedded new-file snapshots, and emits normalized
checkpoint-to-current diffs and SHA-256 hashes. It never edits live source.

The manifest marks 4 files with scoped changes:

- `Phase4A/CommanderAIIntentAdapter.cs`
- `Phase4A/CommanderChatUI.cs`
- `Phase4B2/StrategicAIApprovalBridge.cs`
- `Assets/Tests/PlayMode/CommanderPhase4C1PlayModeTests.cs`

The other eight files are unchanged from the frozen checkpoint.
