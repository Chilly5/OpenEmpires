# Grand Release Finalization — binding specification

Date: 2026-10-07. Status: implementation in progress, NOT release acceptance.

## Current scope amendment — 2026-10-08

Later prioritization: do only very important/important single-player work and defer
minor polish, broad matrix/benchmark campaigns and elaborate acceptance packaging.
Track every cut and final limitations in
[the finishing report](grand-fix-implementation-report.md). Basic usable retained
routes, authority/privacy safety, focused checks/builds/notices remain required.
This overrides the original exhaustive implementation-time evidence/report workload;
missing physical/quality proof must still be disclosed, never counted as passed.

The user's later explicit instruction supersedes the original multiplayer obligations:
target single-player readiness with client-owned Commander; skip Rust/matchmaking/
relay work and peer certification for now. G11/G12/G23 are user-deferred, not repaired.
See [single-player scope and future checklist](single-player-scope-and-future-multiplayer-checklist.md).
Windows/Web single-player, authority/correctness, UI/input, speech/privacy and release
evidence remain. External semantic/optional speech services do not make gameplay
server-owned. Local play must not require multiplayer authentication. If used, the
gateway's Rust identity dependency needs a standalone service route, not bypassed auth.
The original requirements below remain historical context where superseded.

## Authority and scope

The complete Revision 2 user brief at `C:/Users/RS/.codex/attachments/d1000de8-7e43-44d3-8668-52621bb83f2c/pasted-text-1.txt` is binding, including every section, G01–G30, all nineteen required artifacts, and both Windows and Unity Web shipping targets. This file organizes that scope; it does not replace or narrow the brief. The audit documents are finding/evidence inputs, not new execution instructions. Their previous STOP and Windows-only statements are superseded by this implementation request; historical evidence remains unchanged.

Intent: an unobstructed RTS screen, deliberate typing or PTT, stronger measured speech recognition on Windows AND Web, reviewed transcripts, understandable separate approval, faithful ordinary gameplay and grounded explanations. Recognition quality and physical end-to-end evidence must never be inferred from a model name or a mock.

Source baseline: Git root `D:/unity_projects/OpenEmpires`, Unity root `D:/unity_projects/OpenEmpires/Open Empires`, branch `unit_models_and_voice_control`, HEAD `8586764e644240f93c99e2afa1c7d41c697cc879`. Initial changes: existing `remaining_work.md` and untracked `Docs/CommanderFinalization/`. Preserve them. Editor/project version 6000.5.9f1; MCP instance Open Empires@6d7310c7. No applicable AGENTS.md discovered. No commits, pushes, branch switches, resets, cleaning, remote deployment, account purchase or credential rotation.

## Architecture

Preserve player text/reviewed transcript → untrusted bounded semantic data → game-owned validation/scope/approval → deterministic existing planners → ordinary ICommand/CommandBuffer → GameSimulation. Provider/ASR/JS/network data cannot mint owner identity, approval, runtime entities, coordinates, executable code or hidden knowledge. Human background/emergency strategies remain advisory, exact results have no unrelated fallback, sticky human takeover remains, and existing negative constraints reach prerequisites.

Implement in-place canonical adapters, separate versioned structural scope comparison from display, detached bounded tactical observations, and typed actor/target specificity. Preflight dependent result cardinality before graph effects and revalidate material approval changes. Point patrols must not promise a perimeter/radius. No duplicate balance database, sentence-specific gameplay handlers, extra dynamic tactical DSL, save system, formations or autonomous exploration.

Presentation, capture, transcription, semantic submission and approval are separate states. Start/reset minimized; keep host/input/runtime active. Compact launcher/listening/transcribing/review/approval/error surfaces use HUD style, bounded raycasts and no automatic focus/expansion. Input maximum aligns with 1024; typing blocks game/PTT shortcuts only in editable/UI context. Escape cancels owned voice work/draft before minimizing, never accepted plans. One physical event cannot submit AND approve.

One voice-session contract owns job/runtime/capture generation, engine/configuration/privacy/consent, draft identity/revision and terminal callback. Windows local native and shared consented online adapters; Web primary authenticated gateway without WebGPU requirement; optional pinned browser-local worker after capability/inference checks. Do not load native Whisper DLLs in Web. Capture uses actual sample/container metadata, bounded duration/bytes, final chunk flush, permission-gesture affinity and cancellation on focus/page/device loss. Native contexts remain alive until genuinely finished without main-thread disposal waits or unlimited replacement work.

Use existing backend session authentication, noting that current login is pseudonymous rather than a durable account. Add fixed allow-listed audio and semantic routes with exact CORS, CSRF policy, explicit upload consent, server-held keys, ingress/decoded/response limits, per-session quotas/concurrency, global spending ceiling, deduplication and bounded lifetimes. No unauthenticated proxy or arbitrary URL/model; local source/templates/tests authorized, deployment is operator-owned. Keys are session-only in clients unless explicitly Windows OS-protected; no browser persistent reusable key.

Model distribution is pinned/provenance-verified and atomic, with writable Windows cache and optional verified Web cache outside startup download. Benchmark actual tiny baseline, base.en/small.en and stronger compatible candidate where warranted; consented online and feasible browser-local alternatives on the same audio. Record WER/critical counts/types/resources/negations/no-speech, held-out versus tuning, cold/warm median/p95, memory/frame costs and versions. Engineering targets: meaningful held-out 25% relative WER reduction, 90% critical-slot fidelity, no designated dropped negations, zero designated no-speech/cancel submissions; short warm p95 approximately 3s. Missing human corpus/physical evidence is an explicit gate, never fabricated.

Retire heavy ended ownership while monotonic IDs/generation prevent replay; prune unavailable worker history without reclaiming live takeover. Preserve restricted command encoding. Reject incompatible builds before gameplay. No snapshot service exists: bound full-history recovery and explicitly refuse unrecoverable rejoin before clone; never pretend history tails restore initial worlds or drop active traffic.

## Execution and evidence

Main owns all core writes/integration and one Unity runner. Luna agents are bounded read-only inventories/review/docs only. No imported Assets edits during live test/build. Each repair uses actual RED → minimal fix → affected GREEN, then one consolidated changed-area regression. Independent Luna owns exhaustive regression/hostile verification. Preserve all failed artifacts and stage semantics; valid Answer/Clarify is not successful construction.

Budgets are run-wide across reloads: at most SIX paid semantic HTTP transactions including repairs, six live submissions retained; at most TEN minutes cloud ASR billable-equivalent audio including repeats/minimums, consented clips only. Track both separately. No paid call has occurred at baseline. No ambient microphone recording.

Fresh stabilized-source Windows AND Web builds, actual served Web host with secure production-like headers, bounded mock tests separately labeled, per-browser version/status matrix, package notices/setup, source/config/backend/model/test/build inventories including committed and dirty bytes. Manifest byte consistency is not test success. At least one physical Windows packaged and one real supported-browser capture-to-gameplay proof are required to call operational cross-platform voice verified.

All required deliverables and final verdict requirements remain exactly those in brief sections 21–22. Ready only after every implementable fix/integration and available required proof; missing code/build/comparison is partial. Operator credentials/deployment, historical credential invalidation, hardware, Safari/other browsers and genuine peers remain explicit gates. Do not start another phase after handoff.
