# Commander Phase 4C.2 Task 1 Report

Status: **DONE — Phase 4C.2 gate passed; overall Phase 4C remains open.**

## Scope and gate

- Task: integrated deterministic explanations from copied decision outcomes and detached current-plan snapshots.
- Authoritative requirements: `phase4c2-task1-brief.md` plus `2026-09-15-commander-phase4c2-design.md` and the overall Phase 4C architecture contract.
- Phase 4C.1 gate was already passed before dispatch: final EditMode `2c350469769c460ea10838ddaacc0737` 512/512 and PlayMode `14031c0c376d450ca2a6f845f69ab555` 69/69. Those are the pre-edit baseline and were not rerun.
- Unity preflight: `Open Empires@6d7310c7`, Unity 6000.5.9f1, idle, not compiling, not playing, no active test job, `ready_for_tools=true`.
- Existing checkout is intentionally dirty. No commit, branch, package, settings, credential, or unrelated-file operation is authorized or performed.

## Before snapshot

- Existing production file to be touched: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`.
- Exact current content preserved at `phase4c2-task1-before/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`.
- Pre-edit SHA-256: `5F03421B75320D7E83F4164177855A78D196A4A65B86B12EE2E0F8C6A536356B`.

## Final interfaces

- `ExplanationOutcome`: `NoDecision`, `Rejected`, `TransitionRefused`, `PlannerRejected`, `PlanCreated`, `SelectionNotSubmitted`.
- `CommanderExplanationQuery`: `LastDecision`, `LastRejection`, `AttackReason`, `CurrentPlan`.
- `ExplanationPlanState`: bounded immutable copied plan ID/type/status/milestone/status/reason values.
- `ExplanationContext`: bounded immutable copied decision provenance plus at most 32 copied plan states.
- `ExplanationResult`: bounded immutable display text and outcome.
- `CommanderExplanationService.Explain(...)`: deterministic pure value-to-value rendering.
- `CommanderChatUI.LatestExplanation`: read-only validation surface, independent of mutable `LatestStrategicDecision`.

Exact public value/service surface:

- `ExplanationPlanState(int planId, string planType, string status, string currentMilestone, string milestoneStatus, string reason)`; all six properties immutable, all strings bounded to 512.
- `ExplanationContext(int playerId, int? decisionId = null, int? decisionTick = null, ExplanationOutcome outcome = ExplanationOutcome.NoDecision, string reason = null, string requestedObjective = null, int? acceptedPlanId = null, string acceptedPlanType = null, int? currentSnapshotTick = null, IReadOnlyList<ExplanationPlanState> currentPlans = null)`; immutable copied list, maximum 32 plans.
- `ExplanationResult(string displayText, ExplanationOutcome outcome)`; immutable text bounded to 8192 and immutable outcome.
- `CommanderExplanationService.Explain(ExplanationContext context, CommanderExplanationQuery query = CommanderExplanationQuery.LastDecision)`; deterministic value-only output, null/invalid-query rejection.
- Existing `CommanderChatUI` public methods and arities are unchanged. Only `partial` was added to the declaration; the new public member is read-only `LatestExplanation`.

## Focused TDD evidence

- Import-diagnostic job `83812a82d58841828e15cfb36411a70a` reported 0 tests because the new externally written assets had not yet been explicitly imported. It is not test evidence. Explicit Unity asset import then compiled the four new scripts with zero console errors.
- Assertion RED job `8f973da9f3794c9ea14e2f55a54f2856`: MCP initialization bookkeeping timed out after 120 seconds, but the same underlying Unity run completed and wrote `TestResults.xml` before the timeout. Recovered result: 15 cases, 1 passed, 14 failed at assertions for the expected missing bounds/rendering/validation behavior; no compiler/type failure. Required named failures included `Explanation_MatchesDecisionReason` and `RejectedPlanHasReason`.
- Complete recovered per-test result: `phase4c2-task1-red-editmode-TestResults.xml`, 21,591 bytes, SHA-256 `CBD5B71207CF29DFA9252768C778C4D90E480105A7FE5180B24371FB260B2A9B`.
- Representative RED: expected reason `Insufficient available gold.` but placeholder returned `No recorded explanation is available.`; expected bounded length 512 but observed 700; expected invalid owner rejection but no exception was thrown.
- Value/service GREEN job `7a12c14450234bc88917de558b5cbbd0`: 15/15 passed, zero failures/skips, 0.59668 seconds. This covers all six outcomes, both required service-level named tests, copied/bounded inputs, invalid values, absent evidence, deterministic plan-ID ordering, 32-plan labeling, historical/current tick distinction, invariant culture, attack attribution, and 8192-character result bounds.
- Complete per-test result: `phase4c2-task1-value-green-editmode-TestResults.xml`, 13,777 bytes, SHA-256 `623BCE0F95758460EAC31CAB292782E853126F48A575717110A9855C4D166026`.
- Host-level PlayMode RED job `50f477b64b874d8dabe9155a6e4f3152`: 0/5 passed, five expected assertion/runtime failures because the partial exposed only the compilable `LatestExplanation` skeleton. Failures proved that existing routing cleared the pending intent and produced no explanation for rejected-attack, current-plan, reset, or offline queries.
- Complete per-test RED result: `phase4c2-task1-red-playmode-TestResults.xml`, 12,925 bytes, SHA-256 `B6104EB9F939A01FE54B1125A6E7A9B64F7071E1084845BBC022219883B96EEB`.
- First host GREEN job `41d30d0af8994467a52da20067102edf`: 5 total, 4 passed, 1 failed. `ExplanationCannotModifyIntent` found that starting a second strategic translation cleared the existing `LatestStrategicDecision` even though no new strategic evaluation had occurred. The copied explanation source remained intact, but the public latest-decision validation surface lost the last meaningful record.
- Scoped fix: removed only the unconditional `LatestStrategicDecision = null` from the ordinary message-submission preamble. Reset/Initialize/destruction still clear it, and a later real strategic evaluation still replaces it. The existing host test already proves pending identity, intent status, decision history, plan/milestone state, reservations, goal/command counts, provider count, memory kind, and exact prior decision identity across all four explanation questions. Focused verification after this fix is pending from the root-owned runner.
- Final focused PlayMode job `05f7822ccf2748e49250b589f866a030`: 5/5 passed, zero failures/skips, `Passed`, 1.4933965 seconds. `ExplanationCannotModifyIntent` passed after the scoped fix. The other passing real-host cases cover recorded rejected-attack reason/provenance despite an unrelated defense plan, detached current-plan progress without advancement, reset isolation from retained game decision history, and offline exact-form/adversarial routing.
- Final focused EditMode job `57a9a33443644ffbb8162629e5be223d`: 15/15 passed, zero failures/skips, `Passed`, 0.0360488 seconds. Required `Explanation_MatchesDecisionReason` and `RejectedPlanHasReason` passed; all six outcomes, bounds/copying, invalid inputs, invariant formatting, absent evidence, attack attribution, deterministic ordering, and current-state semantics passed.
- Unity MCP 10.2 returned complete per-test data for both final jobs but did not emit a workspace `TestResults.xml`. The job IDs and returned summaries/details are the authoritative current GREEN evidence. No result was fabricated or recovered from an older XML. Earlier RED and value/service GREEN XML files remain preserved with hashes above.

## Fix-round 1 and final gate evidence

- Scoped rereview: **APPROVED**; both independent-review findings were addressed, with no new Critical/Important breakage.
- Fix-round focused EditMode `4568d656eaaa490798eb59bbad443533`: **18/18 passed**, zero failures/skips.
- Fix-round focused PlayMode `a25af57761a44dc5bdc481ae98a18824`: **6/6 passed**, zero failures/skips.
- Final full EditMode `e828d0a94df44912a78e740277d896a8`: **530/530 passed**, zero failures/skips, 443.6047501 seconds.
- First full PlayMode `0611af991765467aa53cac17c69686fe`: 75 tests executed but failed only on the external Package Manager OAuth log from `api.unity.com` in `CommanderPhase3A1PlayModeTests`; this is preserved as failed environment evidence.
- Clean full PlayMode rerun `0a3decd672b648e8a87a2f8f7f923c31`: **75/75 passed**, zero failures/skips, 32.0661847 seconds.
- Frozen hash verification: **7/7 matched; mismatch count 0**.
- Refreshed boundary audit: 55 frozen boundary entries all present/unchanged with `auditGap=false`; 6 advisory host references; credential shapes 0 and assignment candidates 0; `.env` untracked and ignored.
- These results pass the Phase 4C.2 gate. They do not mark the overall Phase 4C goal complete or admit Phase 4C.3.

## Integration and safety boundaries

- The service/value files reference no `GameSimulation`, pipeline, planner, intent, decision-record, command, delegate, callback, or Unity object type.
- The game-owned partial retains the only pipeline reference and uses only existing `CaptureContext()` for a current-plan query; it copies primitive `StrategicPlanState` fields immediately into new `ExplanationPlanState` values.
- Event projection outcome order is: created plan; actual failed submission; explicit rejection; no decision; selected but transition-blocked; selected not submitted. A later `NoDecision` does not erase a meaningful source.
- Rejected objective provenance is captured immediately around the synchronous approval/evaluation call and cleared in `finally`; unknown provenance stays empty and `ActivePlanType` is never used to infer a rejected objective.
- The four queries are exact whole-form matches after invariant lowercase/whitespace normalization and at most one trailing `?` or `.`. They route before pending/latest-decision mutation and never start a provider call or approval/planner execution.
- Each query appends transcript locally and exactly one bounded `MemoryEntryKind.Explanation`; it does not store source records, intents, plans, or current contexts.
- Explanation source and `LatestExplanation` clear on tactical Initialize, strategic Initialize/session replacement, reset/clear memory, owner replacement through Initialize, and destruction. Game decision history is not cleared or rehydrated.
- Original Commander public method signatures/reflection arities remain unchanged.

## Changed files

Production source:

- Modified `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`.
- Created `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs`.
- Created `Assets/Scripts/AI/Commander/Phase4C/ExplanationContext.cs`.
- Created `Assets/Scripts/AI/Commander/Phase4C/ExplanationResult.cs`.
- Created `Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs`.

Focused tests:

- Created `Assets/Tests/EditMode/CommanderPhase4C2Tests.cs`.
- Created `Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs`.
- Unity generated the six matching `.meta` files for the six new C# assets.

Evidence:

- `Docs/CommanderPhase4C/phase4c2-task1-before/README.md` and exact pre-edit `CommanderChatUI.cs` copy.
- This report, three preserved RED/intermediate-GREEN XML artifacts, and `phase4c2-task1-source-hashes.json`.

No other production, test, package, settings, credential, branch, commit, or remote state was intentionally changed.

## Frozen source hashes

- `CommanderChatUI.cs`: `7F4C1D642DD2B373D3E7A49ED3F10E779EF36F415466BB4932147A05775DDBCA` (27,101 bytes).
- `CommanderChatUI.Explanations.cs`: `68458D82489BB8669EF4D95329C4DA869AC55DFA1BFC00C4A87C7D8D54EC5973` (6,061 bytes).
- `ExplanationContext.cs`: `A902F96FB5B18BD3DCC00BDB924A7BD67C180412D54CE6852E754AE3B10305EE` (4,419 bytes).
- `ExplanationResult.cs`: `2C1E02410A1D98D855C623AE9C819E0F84B83861A11CD44B04DE16FFC95D1D6D` (577 bytes).
- `CommanderExplanationService.cs`: `02820AAF58CD193D35A2E448D711E4C9586E4D513C80A765CABD6EE2F2EFCA01` (7,717 bytes).
- `CommanderPhase4C2Tests.cs`: `046BBA408E061EC5E7BE6E1CE510D5548B88AB9E1DD44AF233658556C3E7B9C4` (11,904 bytes).
- `CommanderPhase4C2PlayModeTests.cs`: `96DE63C4E7D34593FDB3AC50EA0BA39CBD21FBCEC4B85A35020F769582DB12C5` (20,781 bytes).

The machine-readable inventory is `phase4c2-task1-source-hashes.json`. Production and test source is frozen at these fix-round hashes pending root-owned focused verification.

## Fix round 1 — recorded NoDecision evidence

- Independent review found that the renderer treated every `ExplanationOutcome.NoDecision` as a pristine empty context, dropping a recorded event's copied decision ID, historical tick, bounded reason, and objective. The same enum-only check made pristine `LastRejection` and `AttackReason` queries describe a fictitious latest `NoDecision` outcome.
- Regression tests were added first for the exact distinction. `PristineContext_DecisionQueriesStateThatRecordedEvidenceIsUnavailable` requires all three decision-derived queries to state that evidence is unavailable without claiming a latest outcome. `RecordedNoDecision_PreservesOutcomeReasonIdAndHistoricalTick` requires an observed `NoDecision` to retain its outcome meaning, ID 23, tick 811, reason, objective, and result outcome. The existing six-outcome table now expects the recorded `NoDecision` meaning rather than the pristine message.
- Minimal production fix: `CommanderExplanationService` now detects whether copied decision provenance exists independently of the enum value. Only a context with no copied decision ID, tick, reason, objective, accepted-plan ID, or accepted-plan type is pristine. An observed `NoDecision` renders `No strategic decision was selected.` followed by the same copied ID/tick/objective/reason fields used for every other recorded outcome.
- Low-risk review coverage was also added: `Context_OwnsPlanListAfterCallerReplacesAndRemovesEntries` proves caller replacement/removal after construction cannot alter the owned plan list or rendered snapshot; `HostProjection_ClassifiesRefusedPlannerRejectedAndUnsubmittedSelection` drives the real host projection method through `TransitionRefused`, `PlannerRejected`, and `SelectionNotSubmitted`, and mutates the original intent after the first projection to verify the copied explanation does not change.
- No public production interface, constructor, enum, authority boundary, package, setting, or unrelated source was changed in this round. Changed source is limited to the service and the two focused test files listed above.
- Per root instruction, this worker did not invoke Unity. The new tests have not been observed RED or GREEN in this round; focused EditMode and PlayMode verification is pending from the root-owned runner. All earlier job results in this report predate this fix and remain historical evidence only.

## Concerns and handoff

- Fix-round focused and full regression verification is complete as recorded above.
- Evidence concern: final Unity MCP 10.2 jobs returned full per-test details but produced no workspace XML artifact. Their job IDs/results are recorded above; the earlier XML evidence is preserved and hash-verifiable.
- Phase 4C.2 is gated complete. The overall Phase 4C goal remains active; Phase 4C.3 is not admitted.
