# Task5 — modern Commander dock and explicit request recovery

Read task-5-inventory.md and full binding brief sections5/6:
C:/Users/RS/.codex/attachments/09e28337-caab-4e27-80ec-184020265c71/pasted-text-1.txt.
Implement Task5 using existing UGUI/TMP/controller, not a new test-only Commander.

Opaque graphite adaptive380–460px dock, readable proportional14–16px-equivalent
body/18–20px headings, restrained accent/cards/8point rhythm. Minimized launcher at
match start with attention/active count; header latest-request/connection state,
settings/minimize. Separate Chat and Tasks views, one scroll each, bottom-fixed
multiline composer/Send/voice/review indicator/character feedback. Settings separate.
Task cards objective/true progress/currentstep/blocker/expandable details and always
visible Cancel. Active first, completed history collapsed. Approval cards expose
validated plain-language counts/source/order/exact producer/new-unit effects; visible
buttons; details never authorize. Pending clarification prominent and separate.
Safe area clamped1280x720/1920x1080/scaledEditor, avoid minimap/resources/selectionHUD.
Do not cover central base with giant wall of DTO text. Populate real states in tests.
The supplied old screenshot shows GameView scale1.3 with clipping on BOTH edges.
Inspect the actual Editor GameView zoom/render-size versus Canvas safe-area geometry;
don't blindly translate the entire HUD right to compensate for a cropped preview.
Verify the scaled/Fit Editor view separately from native Windows/Web screen geometry
and label any editor-only preview adjustment honestly.

Latest chat request status independent from selected old task: Interpreting, Needs
clarification, Awaiting approval, Accepted, Working, Waiting/Blocked, Failed,
Cancelled, Completed derived from real state; no old task impersonating new reply.
Explicit Retry/Edit/Cancel after timeout with retained draft, single-use request
generation/revision/ticket; late/cancelled/disposed/double-send cannot execute twice.
No auto-retry/model fallback/timeout increase,15s production unchanged. Observe faults.
No partial-stream execution; no paid calls to refresh controls/status.
Task4 adds capability-backed explicit worker Move, Garrison/Ungarrison and finite
discovery. Project their real objective, frozen count and native progress on cards,
not generic `CapabilityAction` or synthetic lowering defaults (such as PlayerBase
or a placeholder one-worker selector). Display must not mint execution authority.

Preserve fresh ContainsScreenPoint/input ownership, same-frame ClaimPresentationAction,
Escape/Enter/click/PTT no gameplay leakage or double consent. Voice reviewed-text and
separate audio-consent/Windows/Web provider routes unchanged. Minimize never disables
controller/tasks; replies don't force-open. Keep drafts and manual edits protected.
Read installed Input System1.20 docs required by Unity skill before touching input.
Installed package is Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194.
Its current Documentation~/ui-input.md routes to split files (not an old UISupport.md).
Read handling-input-target-ambiguity.md and relevant uGUI input-module compatibility
guidance. Parent already read those instruction/reference pages fully; implementer
must do its own read before edits. UI does not automatically consume gameplay inputs;
InputAction callbacks can see prior-frame EventSystem hover. Preserve fresh geometry
and text-focus checks and verify actual leak prevention, not only canvas appearance.

Focused RED→GREEN actual UI PlayMode tests at minimum/scaled sizes, populated active/
approval/clarification/timeout states, visible controls/no overlap; existing input/
generation/voice review controls. Save real rendered populated screenshots via legitimate
Unity render/GameView capture; served-Web build screenshot belongs final checkpoint
after single build (don't build now). No mockup-as-runtime evidence/native click hacks.
No full regression/build/paid/network. Follow task-3-brief.md global Ownership/report
constraints; sole writer+runner, no subagents, parent review. Read applicable skills.
Write task-5-report.md with exact changes/tests/handles/source hashes/screenshots and
honest platform/rendering limits. Short return status/report path/concerns.
