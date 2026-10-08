# Served Web probe: load evidence and input failures

Actual build `build-63b0c14d8e` succeeded, errors0/warnings118,3380.3563461seconds,
completed2026-10-07T23:57:23.4728639Z. Report and seven artifact SHA-256 records
are adjacent JSON files. This is a development probe, not a final release.

Served unchanged output on loopback8088 using web-host.mjs. The nonce/CSP host
loaded the real Unity loader/framework/WASM/data into the Codex in-app browser.
Main menu rendered, then a normal local Single Player → 1v1 match started with
French/Godfrey, seed2081198396/Albion selected in the pre-existing menu. No world
completion flags, resources, training or god powers were changed by the test.
The game's ordinary Town Center auto-villager production was not disabled;
population/resources changed over time, so those changes are NOT attributed to
typing based on screenshots alone.

Conversation screenshots show actual states: main menu, mode selection, minimized
match at0:09, expanded at0:55, typed `four villagers` at2:09 with14/1024characters,
and Escape at3:12. These are UI observations, not fake-media or physical capture.
No separate screenshot export path is available in this checkpoint; durable
repository screenshot files still need supported export. Do not invent paths.
Initial menu view was990×828; match snapshots1936×1080. Exact embedded Chromium/OS
version is unavailable: the allowed read-only DOM scope exposes no navigator.
This is an in-app-browser load check, NOT a Chrome/Edge/Firefox/Safari version pass.

## Observed behavior

- Commander starts compact at upper-right, full conversation collapsed.
- Deliberate Commander click opens the panel and text field; typing remains visible.
  No Send, plan approval, microphone permission, Record or online consent was clicked.
  No paid semantic/STT transaction was performed.
- Escape minimizes Commander AND opens game settings from the same physical key.
  This fails the required precedence and cannot be reported as an input pass.
- Expanded panel overlays score/resources/selection HUD. Final adaptive layout
  and1280×720/high-DPI verification remain incomplete.
- Web status says Luna configured even though the explicit semantic gateway is not
  configured. This is misleading status, not evidence of a bundled credential or
  working online route; trace status construction and fix it before release.
- Browser error/warning query returned four warnings and no errors at initial menu;
  only categories were exposed, not raw payloads. This does not classify all118
  build warnings or certify the whole session error-free.

## Root cause and next repair

`UnitSelectionManager.OnEscapePressed` handles the Input System callback before
`CommanderChatUI.Update` can consume it, and does not ask Commander whether Escape
belongs to capture/review/setup/expanded state. Thus game settings open and the
later Update independently minimizes. Fix the actual dispatch boundary, preserving
normal Escape when Commander owns no active surface and preventing duplicate
callbacks from acting twice in one frame. Do not globally disable gameplay merely
because Commander is visible.

Source inspection also finds `OnAttackMovePerformed` lacks the existing editable
input guard; camera BeginMouseControl/HandlePan/HandleRotation omit it while
zoom/edge scrolling already have partial guards. Add focused behavior tests and
fix those consumers. Selection/command click-through needs actual UI-region hit
testing before gameplay callbacks, not last-frame selected state alone. All
remaining C2 typing/pointer/hotkey/fullscreen cases still require scoped proof.

The owned tab was closed and owned local host session24641 stopped after the
scenario. Native desktop Computer Use remains disabled; no desktop inputs,
ambient recording, credential entry or remote deployment occurred.
