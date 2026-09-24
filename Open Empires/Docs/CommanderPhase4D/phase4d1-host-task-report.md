# Phase 4D.1 Task 3 — Commander host controls

Agent: `/root/phase4d1_host_impl` (sole Task 3 production writer and Unity test runner during this task).

Task: Offline exact-form chat lifecycle routing, current-plan status, selected-plan Pause/Resume, and separately confirmed Cancel. Task 4 real partially progressed RangedReinforcement/DefensiveTurtle runtime scenarios are **not** claimed here.

Expected output: Only the trusted local Commander player can route the eight exact whole-form control aliases before provider dispatch. Text requires one owned live plan; UI captures owner/plan ID/created tick/revision plus the exact pipeline instance/runtime generation. Cancel is two-click and disarms on state change. Applied mutation invalidates pending and in-flight strategic interpretation. Conversation-only reset preserves the live plan but clears callbacks.

## RED evidence

- Unity MCP PlayMode job `1e8b20b3b32746cb897fadf34c683f69`: discovered 2 tests; `LifecycleCommands_DoNotCallProvider` failed because `strategy status?` returned `Unsupported or mixed Commander request` rather than the local `No active strategy` observation. This was a behavioral failure, not a missing-method stub. `LifecycleCommands_AreWholeFormOnly` already passed against the existing reject route.
- No native Unity XML was emitted by the MCP job; the authoritative test-job payload above is the retained RED evidence.
- Intermediate jobs `185cfec7c10547cf88859e1f2f8f1993`, `c95f6d5061734d4ba3e5d05c06b8f228`, `0de30f901a2244f4aecfbfce2e4dc654`, and `2125d46a70444efd9348eb83cea87dc4` failed to initialize before tests started. `Logs/Editor.log` showed test-only `CS0246` for `TMPro` references in the new PlayMode test assembly; those assertions were changed to inspect host state without adding an assembly dependency. These zero-discovery jobs are not counted as RED or GREEN tests.

## Implementation

- `CommanderChatUI.cs` routes exact lifecycle phrases before explanation, tactical classification, and strategic translation; wires/unwires planner status, milestone, child-goal, and reservation observations; clears host callback state on Initialize, InitializeStrategic, ResetConversation, and OnDestroy; and adds a compact plan-control row to the existing panel.
- `CommanderChatUI.StrategicControls.cs` normalizes invariant case/whitespace and strips at most one terminal `.` or `?`. It rejects all other forms through the existing router. Status uses copied `StrategicContext` data. Text actions require exactly one owned nonterminal plan and call only the reviewed `CaptureCurrentControlRequest`/`ApplyControl` boundary. The UI displays a selected plan, captures exact primitive token plus pipeline/generation in each callback, and requires two clicks on the same Cancel token. A later plan event or revision change disarms Cancel; `LateUpdate` catches child-goal revisions whose observation event precedes core revision increment. Applied mutations call the existing bridge `ClearPending()` and invalidate the host async generation/cancellation token.
- New `CommanderPhase4D1HostPlayModeTests.cs` contains 11 host-level tests with tactical/strategic provider counters and real host/pipeline objects. No new provider, planner, goal, simulation, networking, package, credential, scene, or Phase 4C file was changed.

## GREEN and regression evidence

- Focused PlayMode final job `438af9bd8518472e90cf5bf28d10be7c`: **11/11 passed**, 0 failed, 0 skipped. Discovered: `AppliedLifecycleMutation_InvalidatesInFlightInterpretation`, `AppliedLifecycleMutation_InvalidatesPendingRecommendation`, `ArmedCancel_DisarmsWhenPlanRevisionChanges`, `CancelUi_RequiresTwoClicksOnSameRevision`, `LifecycleCommands_AreWholeFormOnly`, `LifecycleCommands_CannotBypassOwnership`, `LifecycleCommands_DoNotCallProvider`, `MultiplePlans_TextFailsClosedAndUiSelectsExactPlan`, `Reset_ClearsLifecycleState`, `StaleUiCallback_RejectsRevisionAndNewPipelineEvenWithSameNumbers`, `TextControls_PauseResumeCancelAndClearMemoryPreservesPlan`.
- Existing affected Phase 4C.2/4C.4 PlayMode regression job `9033961768b349c9ac941669f605bf6b`: **16/16 passed**, 0 failed, 0 skipped.
- Existing Phase 4D.1 core EditMode regression job `9d2a99c1b4134e0b8e4bac3525c78534`: **39/39 passed**, 0 failed, 0 skipped.
- Unity console error query after these runs: 0 entries. MCP did not emit native XML for these focused host/regression jobs; the exact job IDs/payloads are the available evidence. Earlier accepted core XML remains separate and is not claimed as host XML.

## Source hashes (SHA-256, final Task 3 source)

- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`: `9CA64EA93FCE4B49C0DF7A746D170209809903437299532954FCA83E723A3C6D`
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs`: `D5DB43044F235E163C5627B491442BFDC77D0941CA1087085D3F17DF7735CFE1`
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs.meta`: `1F5A9B8EC858355D173AADC04D77FEC51226ADABA0D7365F8AFE0D36872004AF`
- `Assets/Tests/PlayMode/CommanderPhase4D1HostPlayModeTests.cs`: `BC57CB3277D96F4437A91D037F28ECB77240862F622912046A6DD03728BE9129`
- `Assets/Tests/PlayMode/CommanderPhase4D1HostPlayModeTests.cs.meta`: `28BD51AF343D658D9E32DDFDFB55C13CC4F146A2E6FFF48A1E72A949B2E17700`

## Authority impact and self-review

The frozen 55-path protected boundary list contains no `CommanderChatUI` path; Task 3 changed no protected file. A targeted static read found no credential handling, command-buffer access, dynamic text-to-command construction, provider-to-planner reference, or new network path in the new host partial. Its planner reference is the explicitly authorized local host control boundary; the provider never receives that reference. `git diff --check` on the tracked host file reported no whitespace errors. The hostile input cases assert both provider call counters remain zero and that no plan/pending intent/command appears. Same-ID/new-pipeline test asserts plan ID, created tick, and revision numerically match the old captured callback, then verifies the new plan stays active. Independent root review and Task 4 runtime scenarios remain separate gates.

Remaining concerns: MCP's focused test interface did not provide native XML. Host callback tests do not replace Task 4's real partial-progress PlayMode scenarios. The project remains a dirty shared worktree by design; no commit, staging, scene cleanup, or full-suite claim was made.

Task 3 handoff: `DONE_WITH_CONCERNS` — focused and relevant regression tests passed; native XML and Task 4/independent-review gates remain outside this handoff.
