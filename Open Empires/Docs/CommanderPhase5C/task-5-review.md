# Task 5 review

### Spec Compliance

- ❌ Issues found. The dock, typed task projection, visible action-plan decisions, draft retention and single-use retry are implemented, but the latest-request state can be taken over by an older in-flight request (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:226-246`, `:678-682`); Voice settings lost their normal UI entry point (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:264-272`, `:296`); and a pending strategic proposal replaces Chat with a generic approval card rather than its concrete effects (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:475-492`).
- ⚠️ Cannot verify from this scoped diff: real 1280×720/1920×1080, scaled/Fit Editor, actual match HUD/input, Windows, or served-Web rendering. The reported captures are actual populated Editor GameView frames at 1518×654, not those platform/resolution proofs (`Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs:403-449`; `Docs/CommanderPhase5C/task-5-report.md`). Final checkpoint must check these without presenting the editor captures as native-size evidence.
- ⚠️ Cannot verify cross-task global gates here: actual measured sub-4K complete model-facing inputs, phase paid-attempt ledger, final offline suites, and one Windows/Web build. They belong to the phase-level checkpoint, not Task 5's focused proof.

### Strengths

- The UI retains the real `CommanderChatUI` submission/authority path; `CompactButton` uses the existing same-frame presentation claim, and plan approval still goes through `HandleActionPlanControl` (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:189-196`, `:336-345`).
- Timeout keeps the draft and offers manual Retry/Edit/Cancel; the semantic revision and current-scope checks reject late successful results, with no automatic retry or production-timeout increase (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:458-466`, `:501-529`; `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:131-171`).
- Capability cards use frozen subjects and native movement/garrison/ungarrison facts rather than a generic action or synthetic count (`Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs:251-325`). Active cards precede collapsed history and put Cancel above expandable details (`Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs:76-99`, `:160-196`).
- Focused new PlayMode cases exercise timeout/late response, populated views, approval, clarification, input ownership and voice review (`Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs:51-151`, `:234-396`, `:429-475`, `:517-548`). Controller-verified final handles: new class 18/18, affected input 5/5, voice 2/2; no rerun in this review. The report preserves original first-failure chronology and discloses overwritten early PNG names (`Docs/CommanderPhase5C/task-5-report.md`).

### Issues

#### Critical (Must Fix)

- None identified in this scoped diff.

#### Important (Should Fix)

- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:226-246`, `:678-682`: information questions bypass the `submitting` guard but neither supersede nor sequence the older semantic request for presentation. If a player asks `strategy status` during a pending order, the question can show `Completed`, then the older reply sets `Accepted`/`currentRequestGoalId` and becomes the apparent latest answer. Give every chat turn a monotonic presentation identity; only the newest turn may update latest-request status, while older orders retain their own task cards. Add an overlapping in-flight order/question regression.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:697-705`: the generic exception handler checks runtime generation but not `semanticRequestRevision`. A provider fault that wins `WhenAny` and completes after user Cancel can replace `Cancelled` with `Failed`; an old fault can also overwrite a newer status in the same runtime. Apply the same request-scope guard used by timeout/result handling and add a cancelled-late-fault fixture.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:264-272`, `:296`: both launcher settings buttons are hidden, and the only expanded Settings button opens semantic setup. The remaining voice PTT path opens voice setup only on certain readiness/consent failures (`Assets/Scripts/AI/Commander/Phase4H/CommanderChatUI.Voice.cs:245-252`), so normal users cannot proactively select Voice settings/consent. Provide a visible Voice settings entry in the separate settings surface and test it.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:475-492`: a pending strategic intent suppresses Chat and activates an approval card whose body is only `Strategic change proposed. Review the effects before approving.`; its own Approve/Reject buttons are hidden. The legacy strategy row may still carry authority controls (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:911`), but this card does not expose validated effect details or direct visible decisions as requested. Project the actual strategic proposal into this card and make its decision route explicit without bypassing existing approval authority.

#### Minor (Nice to Have)

- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:285-299`, `:451-469`: the expanded header shows latest request text, but not a distinct connection/provider state. Add a compact connection label or icon-plus-word state sourced from the existing provider setup state; do not conflate it with request completion.

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** The core dock and bounded authority path are credible under focused Editor tests, but request-state races and missing settings/approval presentation are task-level defects. Platform and full-integration claims remain correctly deferred.
