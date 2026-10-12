# Task7 — final freeze, single offline regression/builds and handoff

Read task-7-inventory.md and binding brief section8, plus progress/closure matrix.
Execute ONLY after prior source tasks and independent reviews have stabilized.
Parent will conduct the single whole-branch source-quality review BEFORE final
freeze/regression/builds, so source findings do not spend or invalidate the one
authorized fresh build per target. Final live/platform requirement verification
still occurs after the builds; a pre-freeze source review is not release proof.
Use verification-before-completion skill; never equate focused/package proof with
native/UI/live acceptance. Preserve Phase5B UNACCEPTED and all first failures.

1. Discover frozen test fullNames WITHOUT execution; classify every fixture transport
and exclude external HTTP/audio/paid probes from full OFFLINE EditMode/PlayMode include
sets. In particular exclude Phase5ALiveRuntimePlayModeTests and Phase5BFinalLivePlayModeTests.
Use existing connected Unity editor/MCP runner, not a second process against same
open project. Export exact included/excluded IDs and reasons, verify real discovery.
Read offline-test-triage-report.md as candidates, not a final discovered universe.
Loopback-only HTTP fixtures are not automatically excluded: inspect their current
asynchronous harness and bounded local routing, then include them if applicable or
record a concrete safety/platform reason for exclusion. Inspect new Phase5C fixtures
at freeze as well; do not silently shrink the authorized offline regression.
2. Generate current compatibility identity and freeze complete relevant runtime/editor/
tests/scene/assets/gateway source before full runs. Include changed legacy-test hashes;
verify snapshot/HEAD/dirty provenance, preserve historical manifests as historical.
Parent inspected NetworkCompatibilityBuildGuard.Generate and NetworkCompatibility:
generation is content-idempotent (writes only on JSON difference), excludes its own
generated identity, but hashes ProjectSettings/*.asset as well as runtime assets.
Verify identity/input hashes after each target switch/preprocess and at final
Windows restoration; do not assume platform switching caused no source drift.
Compatibility excludes Editor/Tests, so the full phase manifest must cover them
separately. Do not alter multiplayer semantics as part of this accounting.
3. ONE final full offline EditMode + applicable offline PlayMode run (human explicitly
approved); retain full durable XML/JSON/logs/exact IDs/counts/durations/failures. Recover
existing handle after observer timeout, no duplicate/forcekill/restart/full rerun.
Before starting, read task-7-result-retention-note.md: MCP SessionState restoration
deliberately retains status/progress but discards the native result tree. A
succeeded/completed counter with result:null is not retained leaf-pass proof.
Ensure a registered TestRunnerApi ICallbacks.RunFinished saves unique NUnit XML
or equivalent native per-leaf results before callback/reload loss; validate that
the capture survives the applicable Edit/Play transition. Do not rely solely on
the mutable shared TestResults.xml or rerun just to manufacture missing artifacts.
Classify all failures honestly. Source changes invalidate affected claims and require
covering verification; no automatic second full pass without new approval.
4. ONE fresh Windows64 and ONE Web build, nondevelopment existing release settings,
unique new output directories, no historical overwrite/cleanup. Preserve reports,
warnings/errors/logs/job handles and exact runtime DLL/wasm/data/framework/loader hashes.
Build preprocess generated compatibility mutations must be recorded and checked; don't
silently associate a stale source hash. Restore Windows active target after Web, since
user explicitly asked Windows default previously. Do not change unrelated settings.
5. After builds, Task6 final served-Web/Windows populated UI checkpoints and measured
routes if not yet done. No more than10 total authorized Phase5C paid attempts incl
repair/timeouts; coordinate ledger with parent. No audio/deployment/secret inspection.
Close only owned local host/processes and preserve user editor/work.
6. Complete relevant source/build/legacy manifest + compatibility verification and
requirement audit. Report actual measured input/usage/cost per successful objective/
latency, initial vs retries/expected clarification separately. Unknown remains unknown.
Update root remaining_work.md preserving history; no Phase5B acceptance, multiplayer,
voice-quality/victory claim or next phase.

Final whole-branch independent review belongs parent (no reviewer subagents here).
Deliver final-implementation-report.md, compact final closure matrix, manifest/evidence
links and exact verdict:
PHASE 5C IMPLEMENTED — READY FOR INDEPENDENT VERIFICATION
or PHASE 5C PARTIAL — specific implementation/evidence blockers.
Never mark goal complete merely because budget/time is exhausted or work stops.
Record elapsed/token usage from goal tool only when actually closing achieved objective.

One designated source/MCP runner; no concurrent work, commits/reset/clean/worktree/
credentials/deployment/multiplayer/Rust/voice rewrite/subagents. Apply_patch for edits,
approved mechanical manifest/build generators okay; scripts require read/verified scope.
Report exact commands/IDs/outcomes/limits in task-7-report.md, short return status/path.
