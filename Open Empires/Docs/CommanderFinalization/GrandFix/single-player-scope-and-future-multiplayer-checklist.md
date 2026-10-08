# Single-player scope and future multiplayer checklist

## Acceptance transferred to next phase — 2026-10-09

The user explicitly approved moving the remaining acceptance gates to the next phase
and completing this implementation goal. Next phase owns physical packaged Windows /
served-browser capture→review→separate approval→gameplay, private live-provider/service
validation with recording/upload/spending consent, representative labeled speech-quality
acceptance, interactive Windows startup/storage/native provisioning, and operator G01
credential invalidation/expiry confirmation. None is passed by this scope transfer.
See `grand-fix-implementation-report.md` for the complete checklist and ordered procedure.
Earlier scope statements below are superseded by this amendment where applicable.
Do not automatically start a verification phase or resume multiplayer work.

## User scope amendment — 2026-10-08

The user explicitly changed the goal to **single-player ready, client-based Commander**
and asked to skip Rust/backend/multiplayer work for now. This supersedes the original
brief's mandatory G11/G12/G23 multiplayer repair/certification gates. These tasks are
user-deferred, not fixed or passed. Do not install Rust or resume relay/matchmaking work
in this goal. Preserve completed changes and historical evidence; do not roll them back.

Windows and Web **single-player**, correctness/approval, UI/input, speech quality/
lifetime/privacy, setup/notices and release evidence remain in scope unless separately
changed. This amendment does not make unfinished single-player/speech work complete.

## Client-owned execution, not necessarily offline models

Unity owns observations, validation, approval, deterministic planning, owned entity/
worker/placement selection, reservations, ordinary commands and simulation. Local
Commander must not require multiplayer login, matchmaking, relay, peer acknowledgement
or Rust. Provider/ASR data does not acquire gameplay authority.

Configured OpenRouter/Luna interpretation still uses its external API; explicitly
consented online speech can use a secure service. Those are data-only interpretation/
transcription services, not a multiplayer game server. Client-based is not a promise
of on-device LLM inference or completely offline operation. Keep destinations/consent
clear, and never bundle reusable keys or persist them in browser storage.

Source observations:

- `GameBootstrapper` creates local Commander/planner/dispatcher for player0 when not
  multiplayer. The local branch ticks Commander and `Simulation.Tick()` directly;
  it does not call `ProcessMultiplayerTick()`.
- Local `StartAIFilledGame` sets `IsMultiplayer=false` and configures local teams/AI;
  the multiplayer `MatchmakingManager.StartGame()` gate is a separate path.
- Windows session OpenRouter entry is independent of matchmaking authentication.
- The Node gateway now supports bounded standalone service-session authentication
  without Rust/multiplayer, with26focusedNode/41clientEdit passes. Operators issue
  private expiring capabilities; this is service/quota identity, not game authority.
  Actual configured provider/proxy/physical speech proof remains unverified. Never
  bypass authentication or bundle a reusable key. See the finishing report/setup README.

## Future checklist for the player

- [ ] Use a future release explicitly verified for multiplayer, not this single-player
  checkpoint. All players need compatible source/data/command versions.
- [ ] Use the operator's matching, verified backend. The new client currently rejects
  missing legacy acknowledgement; do not bypass this safeguard to force a connection.
- [ ] Check sign-in, region, ready/start and compatibility errors before playing.
- [ ] Test human/AI ownership, Sheep/Berries restrictions, construction/training,
  exact new-unit/producer dependencies and human queues/manual takeover.
- [ ] Test movement/combat/rally, delayed packets, reset/late AI replies and reviewed
  voice commands with at least two actual clients.
- [ ] Read the published reconnect window/history limit. Beyond it, expect an explicit
  unavailable result, never a partial history pretending to restore the whole match.
- [ ] Do not confuse session reconnect with save/load, or mock/paired simulations
  with actual peer certification.

Players do **not** need Rust or a compiler to play. These are operator/developer tasks:

## Deferred developer/operator checklist — G11/G12/G23

- [ ] Provision a Rust toolchain and compile/test the relevant backend.
- [ ] Validate the expected release profile before binding authentication/session;
  reject missing/mixed profiles before queue/play, including repeated/stale auth.
  Never simply echo each client's hash as proof of compatibility.
- [ ] Verify actual native/browser socket lifecycle, epochs, send ordering, fragmented
  UTF8/size bounds and disconnect/reconnect beyond existing mock callback checks.
- [ ] Cap complete relay history by frames/serialized bytes; release it and permanently
  mark full recovery unavailable once exceeded. Keep healthy live traffic correct.
- [ ] Refuse missing/unavailable/already-attached reconnect before sender mutation,
  cloning or serialization; never present a history tail as complete recovery.
- [ ] Bound concurrent replay allocations; preserve existing restricted encoding.
- [ ] Clear history/deadlines/all mappings, including soft-disconnected IDs, on
  completion/abandonment/expiry.
- [ ] Run focused Rust/local socket tests, then genuine Windows/Web peer scenarios
  and independent regression/audit; retain exact identities and artifacts.
- [ ] Arrange separately authorized operator deployment; no automatic remote changes.

Historical partial work: [client checkpoint](network-compatibility-client-checkpoint.md)
and [retention report](retention-and-peer-compatibility.md). Neither certifies the
deferred checklist. Single-player readiness still requires its own package/runtime proof.

## Current narrow local verification

Unity PlayMode job `ae09626e129240cda0c702c5eba05ed0`:6/6 passed,0failed/skipped,
3.7175645s. Complete XML: `single-player-scope-play-ae09626e.xml`.
Fixtures: `CommanderGrandFixStatusPlayModeTests` (1),
`CommanderGrandFixQuantityPlayModeTests` (2), `CommanderGrandFixTargetPlayModeTests`
(2), `CommanderPhase4GResultBindingPlayModeTests` (1), namespace `OpenEmpires.Tests`.
They construct local simulation/Commander and use real native game actions without
matchmaking authentication or a relay. Legitimate initial fixture state only; no
forced completion after submission. Not packaged/menu, physical voice, actual
vendor/service, full regression or final single-player release acceptance.
