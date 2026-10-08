# Commander presentation and input contract

Status2026-10-07: C1 implementation/focused verification in progress. NOT Windows packaged/Web visual or physical microphone proof.

## Delivered C1 source

Presentation is separate from controller, voice state, semantic submission and approval. Initial/new host/reset starts collapsed; only the conversation Panel is hidden. The Commander host, voice controller, request affinity checks and accepted goals stay live. CompactCommander is a bounded top-right launcher/status surface, not a full-screen raycast image. The expanded panel has a deliberate Minimize control. Replies and voice transcripts never invoke OpenCommander automatically.

Compact transcript review uses scrollable complete current editable text with Send/Edit/Discard. It does not submit or approve merely on PTT release. A separate plan approval card carries the immutable action preview and explicit approval/rejection. Strategic proposals use Review strategy/Details rather than a generic collapsed Approve: the existing expanded strong confirmation/adaptation controls remain authoritative. Readable semantic preview replacement G07 is still pending; current action detail text is not the final release UX.

Voice-owned drafts record text and input revision. Cancel clears only untouched owned content; independent edits survive. Sending snapshots the actual edited text before consuming voice preview. Input accepts the full draft without silent240-character truncation; an explicit1024 bound prevents sending oversized text and retains it with length feedback. Long transcripts need deliberate editing/review, not automatic splitting.

Physical button/Enter callbacks share one frame claim, including expanded strategic/task controls and legacy voice buttons. Public/programmatic text/control APIs keep their own authorization semantics and do not acquire authority from that UI claim. The claim prevents same-frame Send→Approve; it is not an approval credential. PTT button/shortcut labels derive from actual configured key rather than hardcoded V.

Actual selected TMP/legacy editable fields suppress gameplay through CommanderUIInputGuard even when another chat clears the old shared Boolean. The existing camera/selection paths consume UIInputSuppressed. Mere expanded-panel visibility is NOT global gameplay suppression. The PTT start check uses the same actual editable-field observation. Additional pointer-region, shortcut/rebinding, Escape/focus-loss and Web DOM integration remain C/D work; do not describe those as tested already.

## Focused evidence and limitations

- Genuine initial REDf11087a7:8cases,7fail/1pass; collapsed/reset/review/discard/oversized behavior reproduced. Launcher d4443477 had compile setup error and0tests; NOT evidence.
- First C1GREEN0bb0f703:55/55,37.0091086s (presentation8/scope13/4A13/voice13/grounded8).
- Read-only Luna review identified compact strategic-confirmation and inconsistent callback guard gaps. RED682ce7d2 reproduced both; fixes now route strategies to details and gate expanded mutating physical callbacks.
- Focus check first763b289d and broad959b0365 need fixture caveat: EditMode EventSystem was not registered, so broad97 run is96pass/1fail and retained FAILED. Source inspection verified normal OnEnable registration; corrected fixture proves a real current EventSystem. Genuine old-property RED5209be2a then restored selected-editable guard GREENa562d2a4:1/1,2.7261619s. Final affected Edit6b6419e5passed97/97,0failed/0skip,83.917466s on restored final C1source.

Remaining: voice PlayMode parity, actual1280x720/1920x1080/highDPI screenshots/layout/raycast/input runtime proof, shared action/rebinding/activation UX, first-use online consent, complete Escape/page/device-loss handling, readable previews, Windows/Web builds and physical speech acceptance. Camera edge/zoom consume UIInputSuppressed, but source inspection finds keyboard/middle-pan gating needs a dedicated follow-up case; do NOT claim all camera inputs are already covered. No Computer Use session or generated/mock screenshot is claimed.

Focused PlayMode parity now GREENa13103f3:7/7,0failed/0skip,1.8755708s; mock capture/provider/normal Commander routing only. This removes that focused parity gate above, not any physical/package/browser gate. No source change after final97Edit and7Play runs.

## C2 dispatch and pointer checkpoint — 2026-10-08

Real served Web probe loaded a local match and started compact, but Escape both
minimized Commander and opened settings. That observation disproved complete
Escape handling; the earlier C1 tests did not exercise Input System callback order.
The probe predates these newer source repairs and cannot verify them.

Gameplay's Escape callback now synchronously asks the actual local Commander host
to consume capture/transcription/preview, then voice setup, then expanded chat.
The host claims a frame before changing state, so a second callback or later UI
poll cannot also open settings. If none applies in a later frame, normal game
Escape still works. Accepted goals and unrelated unsent drafts are not cancelled.
Rebinding keeps its existing precedence. This claim is input ownership, not an
approval token or network identity.

Attack-move now uses the existing editable-input/menu guard. Camera middle-button
start, keyboard/mouse pan and rotation also consume the guard; owned pointer lock
is released on suppression, and accumulated movement state is cleared. Mere panel
visibility does not disable the entire RTS input map.

Current pointer ownership is the active expanded or compact Commander rectangle,
not a full-screen invisible image. Selection/commands/hotkeys and camera consumers
use the existing UIInputSuppressed property with this bounded region. The pointer
query uses current Input System mouse state before MonoBehaviour.Update, not stale
VirtualCursor.Position; locked/no-device paths retain the virtual-cursor convention.
Inactive hosts cannot block the world. Layout at additional resolutions and pointer
lock/fullscreen/touch edge cases remain required final runtime evidence.

Focused evidence: dispatch RED3/3failed; first affected run21/22 (EditMode fixture
singleton registration corrected, original failure retained); GREEN22/22; region
RED3/3failed; actual synthetic-device PlayMode RED2ran1pass/1fail, despite launcher
init-timeout summary. The failure also caused fixture teardown's held world-click
to reach a missing camera; original stack is retained, not suppressed. Final
affected Edit2c7471c76dd3434ab71ac1e027cda9ca25/25passed10.0995287s and
Play8bc99601f2114b5f926b0dc7cd904fdf9/9passed3.8368664s,0fail/skip each.
Complete XML files are adjacent input/pointer artifacts. The final Play run includes
two real-input-callback/synthetic-device cases and seven existing voice routing
cases. These are neither physical input/microphone nor rebuilt Web acceptance.

Still open: expanded HUD overlap, misleading unconfigured-Web provider wording,
complete shared Enter/PTT/rebinding and focus/DOM controls, visible recording limit,
all supported resolution/browser/fullscreen matrices and same-source fresh releases.
