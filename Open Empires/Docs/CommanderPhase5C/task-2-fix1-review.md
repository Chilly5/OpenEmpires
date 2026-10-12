# Task 2 fix round 1 scoped re-review

## Finding verdicts

- **Explicit known-field correction — ADDRESSED for the reviewed defect.** `CommanderRequestGraphDraft.Correct` selects one changed leaf, clones the retained graph, and reparses it (`Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs:144-236`). The frozen diff adds structure, count-versus-amount, and source correction controls; the affected artifact reports 19/19 passing. The new negation breakage below still prevents an overall pass.
- **Unrecognized retained-graph reply spent discarded HTTP — ADDRESSED.** `SubmitSemanticTacticalAsync` locally re-asks when `localGraph == null` for a captured graph, before `RequestSemanticTranslation` (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:465-480`). The source-yes/unrecognized test is included in the 19/19 artifact. Standalone orders without correction markers still supersede the draft (`CommanderChatUI.Clarification.cs:31-34`).
- **Draft-specific owner/runtime/replay evidence gap — ADDRESSED as focused fixture evidence.** `PendingDraft_ForeignRuntimeAndReplayCannotCreateGoals` is present in the frozen test diff and passed in the 19/19 artifact (`Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs`). This is not native-runtime proof.
- **Mutable alternatives — ADDRESSED.** `Slot` clones alternatives on construction and on every public read (`CommanderRequestGraphDraft.cs:11-30`); the mutation control passed in the 19/19 artifact.
- **Finite graph-reply budget and full original input — ADDRESSED.** Graph turns are incremented for partial replies and corrections, expire at six, while worker drafts retain three (`CommanderChatUI.Clarification.cs:35-64,92-95`). The graph constructor rejects originals above the existing input limit instead of cropping (`CommanderPendingClarification.cs:64-69`). Four-slot, repeated-correction-expiry, and overlong-input controls passed in the 19/19 artifact.

## New breakage in the fix diff

- **Important — negated correction can become an admitted order.** Correction detection accepts `change` or `instead` anywhere in the reply, and `Correct` scans all words for canonical values without checking that the utterance actually requests the change (`CommanderRequestGraphDraft.cs:130-134,144-188,219-236`). With a pending single-node Mine proposal, `Don't change the Mine to a Mill` finds unchanged Mine plus changed Mill, produces exactly one leaf change, and returns a valid Mill Request. `HandlePendingControl` does not dismiss that sentence (`CommanderChatUI.Clarification.cs:20-34`), and the existing single-result path can admit it (`CommanderChatUI.cs:535-567`). This is a concrete unrequested gameplay action introduced by the correction path. An unsupported/negated correction must fail closed locally; preserve genuine explicit corrections.

## Out-of-scope observations

- None in this scoped incremental review.

## Evidence and verdict

- Frozen incremental package reviewed: `evidence/task-2-fix1-review-package.md`; no tests, MCP, network, or git commands were rerun. Saved Unity results report affected graph 19/19, old worker controls 2/2, and final ambiguity wording 1/1. The earliest RED handle is no longer retrievable (`Unknown job_id` in `evidence/task-2-fix1-initial-red.json`); its four failures remain only in the implementer's report/tool transcript, while later RED artifacts and GREEN artifacts are saved.
- **Spec compliance: NOT YET — one Important correction-authority breakage remains.**
- **Task quality: NEEDS FIX — prior review findings are addressed, but fix round 1 introduced the negated-correction admission path.** No whole-Phase-5C acceptance or live-model/native-runtime claim.
