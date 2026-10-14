# Small movement, UI and voice repair — 2026-10-15

Implemented on `unit_models_and_voice_control`, HEAD `679238464e959fc175a2d8bd897e90c15bb7defe`, in `D:/unity_projects/OpenEmpires/Open Empires`. No commit, push or package build. This is targeted Editor verification, not overall Phase5C acceptance.

Final generated source identity: `535add94aecc339e5023661c5df77df8e8ccd5fae3d7baebf314d311c59299cf`; loaded runtime MVID `ed7952fd-0a56-4240-bbaf-88c5052ae2d3`. Evidence is under [small-movement-ui-voice](evidence/small-movement-ui-voice/).

## Changes and causes

- Existing-unit movement now uses canonical catalog identities and civilization replacements independently of production capabilities. Dynamic marker resolution, typed inclusion/exclusion and scope serialization preserve the selected group. Missing-group replies retain the original movement request and require the normal plan approval.
- Movement freezes actor IDs and the marker destination. Legal nearby arrival centers may have overlapping scatter envelopes, while every possible native scatter tile still requires a known valid path. Lost actors cannot be replaced. Expected protected/unreachable failures occur before goal insertion, retain the marker, and show the actual reason in compound, ordinary and later-marker UI routes.
- The real provider prompt had conflicting Request display-name versus DynamicPlan ID guidance, and no executable all-matching marker example. Shared canonical guidance and a request-relevant example repaired the captured archer failure; malformed responses still reject.
- Commander uses a narrow right dock above the minimap. Minimized controls show the configured PTT binding, Record/Stop, status, Cancel, editable transcript and Send/Discard. Expanded review has a bounded scroller for the full original and correction/choice guidance. Short-window composer overflow found during visual inspection was repaired.
- Catalog-only transcript correction preserves non-noun meaning; destination vocabulary such as marker is protected from similar building names. Ambiguity blocks submission until edited. Raw standalone approve is handled before correction and bound to the same displayed candidate from recording start; stale, quoted, negated and substring forms clarify locally.

Runtime changes are in Commander selectors/DTO/validator, DynamicPlan compiler/parser/preview, planner/executor/admission, semantic prompt, ChatUI presentation/marker/group/voice partials and voice controller/correction helper. Focused coverage is in `CommanderMovementSelectionRepairTests`, `CommanderVoiceReviewSafetyTests`, `CommanderVoiceApprovalIntegrationTests` and `CommanderGrandFixPresentationTests`.

## Verification

- Initial focused run: 60/72 passed; failures were retained, diagnosed and repaired. Subsequent affected runs: 22/23, then 4/4 and 20/20. These are separate receipts, not a claimed green full suite.
- Review fixes: 23/23 passed; short 350-unit review/composer checks: 2/2. Final source targeted run: **14/14 passed**, including four ordinary/compound protected/unreachable cases, expanded/compact review and seven approval integration cases. See `final-source-focused.json`.
- Live provider: three initial HTTP attempts, no repair/retry. Spearman succeeded; first archer response failed strict clarification validation safely; corrected archer request returned a valid all-matching plan.
- Native Editor movement: spearman ID46 arrived; English Longbowmen IDs48/49 arrived from the archer selector. The local missing-group/all-except-villagers continuation ordered exactly IDs6,7,8,9,33,46,47,48,49 and completed only after all nine arrived. No villager received a Commander order. These movement receipts use source `e557fd18…`; later changes only refine rejection presentation and expanded review layout.
- Actual Windows clicks checked Record→Listening→Stop→local synthetic approve→Accepted, with no Send/Approve click or semantic request. Actual compact typing and Discard worked. Final rendered expanded review fits above the minimap, with original/guidance accessible by scrolling. A controlled blocked-path request rendered its specific rejection with zero goals and retained marker. See `ui-observations.json`, native receipts and final PNGs.
- Final-source normal Play-mode input frames also verified a virtual keyboard V press→Recording→release→synthetic approve→exactly one admitted goal, with no provider request. The same V input while the composer was selected stayed Idle. See `keyboard-play-*.json` and `keyboard-typing-*.json`. Earlier same-frame EditMode injection was inconclusive and is retained as diagnostic evidence, not a passing PTT gate.

Paid accounting: **3/10** newly authorized calls used, **7 remaining**; global ledger **71/200**. No paid ASR.

## Evidence limits and handoff

Physical microphone recognition was not exercised: voice results use synthetic audio capture/transcripts through the real controller/UI. Keyboard PTT and its typing-focus guard were verified with virtual device events across normal Play frames; the physical mouse approval check used Record/Stop. Windows/Web package and standalone input verification remain pending. No voice engine or platform-specific runtime dependency was added. Next external check: in a fresh match, display one plan, hold/release the configured PTT key and say exactly approve; verify one admission, then repeat with do not approve and with a replaced candidate.

Unity is stopped in EditMode. Computer Use was released after brief checks; Blender was untouched. Source identity and roadmap were updated; no overall Phase5C acceptance is claimed.
