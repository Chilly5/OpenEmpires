# Single-player Commander finishing report

Status: **GRAND FIX IMPLEMENTATION COMPLETE — READY FOR NEXT-PHASE INDEPENDENT
VERIFICATION, UNDER THE USER-APPROVED AMENDED SCOPE.**

## Final scope amendment and closeout — 2026-10-09

The user explicitly directed: “move them to next phase and add it to the handoff
report and mark this goal done.” The following acceptance gates are therefore
transferred to the next verification phase, not passed or removed from the product
roadmap. They no longer block completion of this implementation goal:

- [ ] Physical Windows packaged and supported-browser microphone capture → transcript
  review → separate plan approval → ordinary gameplay. Include permission denial,
  cancellation and no unintended recording/upload; obtain explicit recording consent.
- [ ] Live semantic-provider and online-ASR/operator-service validation. Configure
  credentials privately, obtain explicit upload/spending consent, preserve the durable
  budgets (semantic HTTP 0/6 used; cloud ASR 0/600 seconds used), and record actual
  auth/network/transport results without exposing keys.
- [ ] Representative labeled speech-quality acceptance, including counts, negations,
  silence/noise and device/network impact. The existing two-clip comparison and
  exact-zero regression are not a general quality winner or physical capture proof.
- [ ] Normal interactive Windows final-package startup/storage and native-model/runtime
  provisioning. Reproduce the disclosed optional region-probe preference-write failure;
  determine whether a real fix is needed rather than treating headless evidence as
  clean interactive startup or a proven fatal defect.
- [ ] Operator confirmation of historical credential invalidation/expiry (G01).
  Source/package scans do not establish credential revocation or history clearance.

Next phase owns these gates and the already deferred independent/full/hostile checks.
Use the ordered next-AI procedure below, the gap ledger and future checklist; preserve
the exact final Windows/Web artifacts and source manifest. Record each gate as passed,
failed, blocked with evidence, or explicitly deferred—never infer a pass from mocks or
build success. Existing broad-campaign cuts, multiplayer/Rust/peer deferrals and optional
expansions remain unchanged. No new test/build/provider/microphone action was performed
for this documentation-only closeout. Stop here; do not automatically start the next phase.

This is completion of the amended implementation deliverable, not RC/public-release,
cross-platform physical voice, recognition-improvement or multiplayer certification.
Latest user direction2026-10-08: finish single-player/client-owned readiness as
quickly as possible; do only very important/important work, and record cut work here.
Implementation/checkpoint work is preserved and available for independent verification.
This is not a claim of complete voice-quality improvement, RC or multiplayer readiness.

## Current result — 2026-10-09 local date

- Branch `unit_models_and_voice_control`; actual HEAD
  `8586764e644240f93c99e2afa1c7d41c697cc879`, preserved dirty worktree, no commit/push.
  Source/config/evidence/artifact inventory: `grand-fix-source-manifest.json`.
  Run its documented `-VerifyOnly` before independent testing; bytes are not runtime proof.
- Current Windows final `build-50c3dac169`: **succeeded**,55.7469853s,235.72MiB,
  0errors/75warnings, Mono/non-development, SampleScene;
  `Builds/SinglePlayerRelease-Windows-20261009-Final/OpenEmpires.exe`.
- Current Web final `build-b3217372f0`: **succeeded**,341.965114s,129.75MiB,
  0errors/78warnings, non-development/gzip/fallback, SampleScene;
  `Builds/SinglePlayerRelease-Web-20261009-Final`.
  Runtime Assets held unchanged between final builds. Exact JSON reports named below.
- Exact final Web artifact served at loopback with existing bounded nonce/CSP host:
  menu→local1v1, compact startup, expanded HUD clearance and Escape/minimize observed
  at1536×864. Earlier alignment artifact also verified unsent typing and both setup
  cards. Browser screenshots are in tool history, not invented local image files.
  No mic permission/record/upload, provider Send or plan approval performed. Owned
  browser tabs/host sessions closed; re-enter browser use only when a new test needs it.
- User alignment request repaired;5focused Play checks passed and real local Editor
  screenshots retained. Last native exact-zero guard has4final native Edit checks,
  a real RED and read-only Sol review/recheck. No full historical/hostile regression.
- Final Windows has five native DLLs/notices;0test assemblies/`.env` filenames;
  only116-byte ECS StreamingAssets bin, no startup Whisper weight. Final Web validated
  data header has8entries/no native-weight or `.env` paths. Bounded key candidate
  scans: Windows0matching files; Web wasm0candidates, data generic pattern crosses
  13distinct IL2CPP literals, not a credential literal. No values printed; not
  compressed/history/credential-incident clearance.

## What is fully implemented in the retained single-player code scope

Canonical age/cost answers and detached owned progress/blockers; typed repair/enemy
targets; honest point/radius handling; total/new result-count admission and exact
result provenance; structural approval independent of display; readable previews;
minimized/open-to-type/PTT/review/approval input separation; cancellation/disposal/
reinitialization/duration/transport bounds; verified model import/download/use;
session-only Windows semantic setup and Web authenticated gateway setup; browser
getUserMedia/AudioWorklet bridge and consented bounded online transport; standalone
Node service authentication independent of Rust/matchmaking; identity/dead-worker
retention cleanup; notices/setup and current final Windows/Web compilation. Concrete
paths/tests/limits remain in `gap-closure-matrix.md` and linked checkpoint documents.
These are scoped implementation/focused-evidence statements, not full product passes.

## Next-phase acceptance gates and other deferred work

1. **Required evidence unverified:** deliberate packaged Windows and supported-browser
   physical capture→review→separate approval→ordinary gameplay; configured vendor/
   operator service and online consent/transport runtime; representative speech-quality
   improvement. No available two-clip result proves a general accuracy winner.
2. **Windows packaged startup acceptance:** restricted headless probe logged a preference
   write failure in optional region discovery. Cause/interactive behavior unproved;
   reproduce normal interactive startup/storage before claiming clean package acceptance.
   Native model/driver/runtime provisioning needs actual packaged-player verification.
3. **Operator-owned G01:** confirm historical credential invalidation/expiry; never infer
   resolution from a source/package scan. No credentials rotated/retrieved here.
4. **User-cut future work:** full/hostile regression; broad40–60clip/multi-engine quality,
   browser/DPI/iframe/fullscreen/device and long-session FPS/memory campaigns; secondary
   browser-local ASR; minor visual/provider-banner wording polish and duplicate reports.
5. **Separate future scope:** multiplayer/Rust/relay/socket/peer repairs and certification
   (G11/G12/G23); formations/frontier exploration/save-load (G27/G28/G29). Not implemented
   or certified merely because the single-player path works. See future checklist.

No remaining available source change is being invented to hide these gates. Further
physical/operator work requires user action/access/consent; quality acceptance needs
representative labeled recordings. With the explicit 2026-10-09 scope amendment above,
this implementation goal is complete; acceptance remains pending in the next phase.
The ordered next-AI procedure and all cut work follow.

## Usage and privacy checkpoint

Goal accounting snapshot:9,745,206tokens /86,210active seconds (23h56m50s), captured
before final smoke/documentation/manifest; not a final consumed total. Paid semantic
HTTP0/6 and cloud ASR0/600s throughout this closeout, no microphone or vendor access,
remote deployment, funding, commit or push. Model comparison used existing local WAVs
and generated silence only; contexts released sequentially. Historical failed/interrupted
builds and XMLs retained. Final accounting must be read when the goal is actually ended.

## Important checkpoint: standalone service authentication

The companion gateway now has `standalone` service-session authentication independent
of Rust/matchmaking. An explicit offline operator command issues a short-lived UUID
capability into private files; the service retains its hash/owner/expiry only. Owners
select quotas, not game identity. No public issuing endpoint, client-bundled key or
setup-time provider request. Existing consent/concurrency/durable budget rules remain.

Windows ACL/owner checks are enforced; new directories are protected without taking
ownership, and public existing directories/registries are refused. Startup validates
store/ACL/expiry before listening; per-request checks permit subsequent revocation.
No actual user credential file was inspected or production token issued in this work.
Only isolated synthetic fixtures were used. Registry128records/32KiB, expiry<=24h;
issuer defaults1h, no overwrite, no spending-ledger reset.

Offline RED:4 initial missing features; configuration case initially rejected no-Rust
mode; review cases exposed Windows privacy and unusable-store startup. Security fixes
now pass7focused standalone checks, including Windows ACL and local HTTP revocation.
Client41/41Edit checks passed13.7311325s; XML `single-player-service-edit-ce007e85.xml`.
Final combined Node:26/26passed,0failed/skipped,26.5549445s on Windows via
`node --test` in `D:/unity_projects/OpenEmpires/backend/speech-gateway`.
These are synthetic sessions, fake vendors and loopback HTTP, not deployed-service
or physical speech evidence. No vendor/audio/mic call occurred.

Setup: see `backend/speech-gateway/README.md`, standalone section, and the secret-free
configuration template. A user privately obtains the operator's service UUID and
enters it in the masked game field. Windows direct session OpenRouter entry still
does not require this optional service. Operator access/pricing/HTTPS/physical
transcription remain unverified; code/fixture success is not a speech-quality claim.

## Current priorities — retained important work

## Player quick start (single-player)

## Narrow player-reported alignment follow-up — 2026-10-09 local date

User confirmed another restart during the unresponsive-looking Web retry and asked
for only a slight in-game alignment fix. Existing expanded panel overlapped the
player-list/resources HUD. Moved its left reference offset16→280 with right-edge
clamping; primary compact controls now precede Text AI. No input/voice/approval or
gameplay authority behavior changed. Actual1520×656 local-menu match: panel left
291.202px versus HUD right281.842px, bottom24.960px; minimap/resources remain visible.
Screenshots: `single-player-expanded-alignment-before-20261008.png`,
`single-player-expanded-alignment-after-20261008.png`,
`single-player-compact-alignment-after-20261008-1.png` (actual generated filename).

Focused RED2/2 failures `alignment-red-2a3993b7.xml`; GREEN5/5Play,0failed/skipped,
2.2763798s, `alignment-green-recovered-7d9b525b.xml`. Durable XML exact five names/
timestamps recovered despite stale MCP job reporting running after reload; actual
Editor had exited tests and XML proves completion. No duplicate/full regression.
Old Windows release predates this alignment follow-up, so final builds must be fresh.

Web packaging uses gzip instead of Brotli (`WebGLCompressionFormat.Gzip` verified
as1 in installed Unity); decompression fallback stays enabled. This shortens the
previous407s Brotli stage, at a potential larger download size; no gameplay/security
change or measured end-to-end build-speed claim. No compiler cache purge. Unity's
blocking build pipeline can look frozen; user asked to leave editor open while
compiler/job is monitored. No crash root cause is inferred from interrupted logs.

## Release preparation checkpoint

## Last important native safety follow-up

The narrow available native comparison is preserved in
`native-small-comparison-before-silence-fix-20261009.json`: two existing speech WAVs
and generated2s silence per tiny/base.en, nativeCPU/en, no mic/vendor/gameplay. Speech
text materially matched; base.en slower and produced `you` for exact digital silence.
This is not a labeled held-out corpus, WER improvement, model winner or physical proof.
Default tiny remains unchanged. Exact-zero prepared waveform now returns typed
`EMPTY_TRANSCRIPTION`, with finite validation and cancellation/disposal precedence
preserved. No quiet-speech threshold, general noise VAD or online-silence guarantee.

RED1failed `native-silence-red-4fa72b7f.xml`; firstGREEN4/4Edit9.2956165s
`native-silence-first-green-9f0f10b8.xml`. Sol read-only review identified typed-result
assertion and cancellation ordering, both tightened; read-only recheck found no
remaining Important issue in those lines. Final4/4Edit,0failed/skipped,8.3507336s,
`native-silence-final-green-2a4fa900.xml`. No full regression. CPU/environment/model
hashes are recorded in comparison JSON; no GPU inference/physical/quality-winner claim.
Alignment Windows/Web builds and Web smoke above predate this last native change;
they remain historical checkpoints. New source-matched final artifacts are required.

- Added root/player notice text for pinned Whisper binding/native/models and the
  installed JSON package notices. Root and StreamingAssets notices are text-equivalent;
  player copy uses `.txt` so the bounded Web host serves it with an allowed MIME.
- Read-only native PE import inspection found VC14/OpenMP and Vulkan loader
  dependencies, including Vulkan in the pinned CPU bundle's dependency chain.
  Setup states the limitation; no system DLLs copied or runtime installer executed.
- Actual local match at1536×672 exposed Voice/model setup below screen by98.5px.
  Retained all controls in bounded scrollable setup cards; first-frame layout forced
  before height clamp, close stays available, launcher shifted away from right HUD.
  Before/after screenshots are `single-player-voice-layout-before.png` and
  `single-player-voice-layout-after.png`. After layout bottom=12px at that viewport;
  this is Editor local-match proof, not package/physical/browser certification.
- RED `setup-layout-red-a482e411.xml`; intermediate first check9pass/1fail
  `setup-layout-first-check-c2fe1391.xml`; GREEN10/10Play4.7908837s
  `setup-layout-green-a076adc7.xml`. First release Edit68pass/1old-path failure
  `setup-release-edit-first-0dadfd37.xml`; retained no-mic/no-gameplay assertion at
  new hierarchy; final69/69Edit27.6745951s `setup-release-edit-green-aa1d2419.xml`.
- Windows release build `build-081a743443` started at
  `Builds/SinglePlayerRelease-Windows-20261008/OpenEmpires.exe`, Mono, non-development,
  SampleScene, LZ4/detailed report. No unsaved scene; no imported Assets edits while
  building. This was the pre-result checkpoint; the terminal result follows below.

Windows build subsequently **succeeded**,273.2297548s,235.72MiB reported,0errors/
75warnings. These warnings are not classified as harmless by this summary alone.
All five pinned Whisper/ggml native DLLs are present under player Plugins. Actual
notice/exclusion and startup checks are recorded separately; build success is not
physical microphone or complete gameplay proof.

Windows file checks: StreamingAssets notice present, all five native DLLs present,
0test assemblies and0`.env` files by filename scan. One116-byte `.bin` is ECS
`EntityScenes/scene_info.bin`, not a Whisper weight. No native model `.bin` is included
as a player startup payload; model import/download remains deliberate. This is not
an exhaustive binary/history secret audit. Startup-only probe remains separate from
interactive/native-ASR proof; its first log argument was misquoted, not counted as
successful smoke evidence, and was corrected after confirming that process ended.

Corrected hidden Windows startup produced the owned log. Values-free checks found
0Exception/Crash/MissingNative/compile-rule matches during that bounded probe; the
task-created process was stopped after collection, with exact executable/ID checked.
This is startup-only evidence, not a normal-exit, interactive match, model load,
microphone or provider test. Unity/editor and other user processes were not stopped.

Web release attempt `build-43208daff8`, non-development, SampleScene,
`Builds/SinglePlayerRelease-Web-20261008`, was interrupted by the user-reported Editor
crash. After the requested30-second restart wait, MCP reconnected to the same project
on port6401, idle/not compiling/importing/building, with no dirty scene. The old job
is absent: inspected MCP `BuildJobStore` is static/in-memory and loses jobs on restart.
The incomplete output is preserved (only notice/worklet StreamingAssets, no loader/
wasm player). Project `Logs/Editor.log` ends with `Tundra build interrupted` after
WebAssembly link completion,844/851items evaluated,1529.90s. Import-worker transport
disconnects also appear; these do not establish the crash cause. No remaining
bee_backend/wasm-ld/clang/emcc process was found by name. No game/source/compiler
settings changed as a speculative fix. One cache-reusing retry in a separate output
folder is authorized by the retained Web-build requirement; no clean-cache rebuild,
full regression or Web success claim. New handle/results follow below.

Retry `build-78071ea797`, same non-development WebGL settings/source,
`Builds/SinglePlayerRelease-Web-20261008-Retry1`: first status observation timed out
after300s, not a build verdict; `bee_backend` PID23860 and Brotli PID2188 were then
confirmed live. Subsequently MCP switched from port6401 to6400 and a new Editor
PID24412, playing/not building, whose job store lacks this handle. Both compiler/
compression processes are now gone. The prior Editor's AppData log ends with
`Tundra build interrupted (422.97 seconds)`,847evaluated, after407s Brotli compression.
Preserved retry output has a12672876-byte wasm.unityweb and two StreamingAssets,
but no index/loader/data/framework: **not a runnable build or success**. Exact reason
for the editor loss remains unestablished. Asked user whether another restart
occurred and to return to Edit Mode; do not stop their current match or launch another
build while editor ownership is unclear. Native desktop UI inventory has no apps;
an in-app browser remains available once a complete served artifact exists.

- Start **Single player**; no multiplayer login, relay or Rust is required for gameplay.
- Commander starts minimized. Open **Commander** to type; **Text AI** configures the
  masked session-only OpenRouter key on Windows or an operator gateway/service session.
  Availability is checked on a normal request, not by a paid setup probe. **Disable**
  stops new interpretation; approved local work is not silently cancelled.
- **Voice** opens processing/model settings. On Windows, choose a pinned candidate,
  Download or Import its `.bin`, wait for verification, then **Use verified** separately.
  The game uses a writable per-user cache; no Python/compiler is required by players.
  Baseline tiny is multilingual with English decoding; base.en is English-only.
  Candidate availability is not a measured accuracy winner. No native model is bundled
  in the Web startup payload. Browser-local offline ASR is deferred/not advertised.
- Native Windows DLL imports require Microsoft's VC14 x64 runtime/OpenMP and the
  graphics driver's Vulkan loader even in CPU mode for this pinned bundle. Use official
  vendor runtime/driver installers, not arbitrary copied DLLs. No CUDA or Rust.
- Hold the configured PTT key (label shown in UI, default V) or use Record. Release to
  transcribe; review/edit/discard the text. Online audio needs its separate explicit
  processing consent and configured service. No automatic recording on startup.
- Sending a transcript/text is **not** approval of a compound/strategic plan. Read the
  preview, then approve separately. Escape cancels owned voice work or closes the panel,
  not already approved gameplay. Manual unit orders take priority.
- Supported examples: `build a barracks`, `make 10 spearmen`, `put 4 villagers on food`,
  `put 4 more villagers on food`, and `how do I reach castle age?`. Exact new-unit plans
  require their own approval and compatible dependency/count. Radius/perimeter promises,
  ambiguous/unavailable targets and contradictory constraints fail rather than widen.
- Model/service/key/device errors leave typing usable. Do not enter service keys into
  multiplayer fields, publish session tokens, reset spending ledgers, or assume online
  cancellation reverses a paid upload. Multiplayer/save/formation expansion is deferred.

Notices are included in `THIRD_PARTY_NOTICES.md` and player StreamingAssets text.
Sources: [Whisper binding license](https://github.com/Macoron/whisper.unity/blob/e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687/LICENSE.MD),
[whisper.cpp1.7.5 license](https://github.com/ggml-org/whisper.cpp/blob/v1.7.5/LICENSE),
[Whisper weights license](https://github.com/openai/whisper#license),
[Microsoft runtime guidance](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).
Installed Unity/Newtonsoft package copyright notices are reproduced without changing
package/native binaries. Full existing asset-store license audit remains outside this cutdown.

### Retained priorities

1. Local game-owned Commander authority, strict interpretation/approval, correct
   targets/quantities/exact results, human override, truthful blockers and no stale
   execution. Existing focused repairs must remain intact.
2. Single-player must not require matchmaking, relay, multiplayer compatibility
   acknowledgement or Rust. External semantic/transcription services remain data-only.
3. A usable supported voice/text path per retained Windows/Web single-player target.
   Node gateway now has tested standalone bounded service authentication independent
   of Rust/multiplayer. Do not bypass auth, upload without consent, bundle
   reusable keys or claim unsupported browser-local voice works.
4. Basic usable minimized UI, input/approval separation, no gameplay-control leakage,
   honest errors, cancellation, bounded duration/transport and model provisioning.
5. Correct required dependency/model notices, short actionable player setup, focused
   changed-area verification, fresh relevant builds and basic runtime smoke evidence.
6. Final source/artifact identity, honest known limitations and this future checklist.

## Work cut/deferred to accelerate closeout

These are not passes and must not quietly re-enter this goal:

- **Multiplayer/Rust/backend relay scope (G11/G12/G23):** explicitly deferred by the
  user. No Rust installation, relay-history repair, peer certification or deployment.
  Preserve partial client guards and evidence. See
  [future player/operator checklist](single-player-scope-and-future-multiplayer-checklist.md).
- **Exhaustive browser certification:** every Chrome/Edge/Firefox/Safari version,
  iframe/fullscreen variant and device matrix. Retain a basic supported-route check;
  document other combinations as unverified, not universally supported.
- **Broad visual polish:** decorative changes, animation/theme refinements, extensive
  high-DPI/resolution sweeps. Fix obstructed gameplay, unreadable essential controls
  and broken focus/approval behavior; defer cosmetic refinements.
- **Large quality/performance campaigns:**40–60-clip multi-engine/vendor corpus,
  larger/quantized model exploration beyond what is needed for a usable route,
  exhaustive WER/p95/GPU-contention and long-session memory/FPS profiling. Basic
  representative smoke evidence remains; do not claim comparative accuracy targets
  or physical quality passed without measurements. The player's reported bad STT
  remains important; a filename switch alone is not a proved quality repair.
- **Secondary recognition experiments:** optional additional browser-local/runtime/
  provider routes beyond a usable primary and honest privacy/unavailable choices.
  Do not advertise deferred offline/browser-local capabilities as functional.
- **Full historical regression and hostile audit:** leave to the next independent
  verification pass. Run proportionate focused tests for actual important changes.
- **Large standalone acceptance/report package:** no new duplicate narrative reports,
  screenshot/video collection for every state or exhaustive nineteen-document rewrite.
  Preserve existing evidence; consolidate essential setup/source/results/limitations
  here and link existing checkpoint documents.
- **Optional formations/frontier exploration/save-load (G27/G28/G29):** still absent;
  unsupported requests must remain honest. No incidental feature expansion.

## Still important but external/unverified

Actual provider access, microphone consent/hardware, operator service configuration
and credential-incident invalidation are not minor code chores and are not marked
passed by being unavailable. They must be reported as explicit gates. No ambient
microphone or paid requests are authorized by this prioritization alone; existing
six-semantic-HTTP and600-second cloud-audio ceilings remain unchanged.

## Verified current checkpoints

Existing correctness, lifecycle, provider and identity-retention results are linked
from `gap-closure-matrix.md`; each proves its stated scope only. Most recent local
scope verification:6/6 native PlayMode cases passed3.7175645s in
`single-player-scope-play-ae09626e.xml`, without matchmaking/relay. Not a packaged
launch, real provider, physical microphone or final release acceptance.

## Future resumption checklist

- [ ] Complete the deferred multiplayer/operator checklist before promoting multiplayer.
- [ ] Run independent full/hostile regression on the final source, with lane exclusions.
- [ ] Collect consented representative audio; compare selected routes honestly.
- [ ] Verify physical Windows/browser capture-to-gameplay on actual retained routes.
- [ ] Extend browser, device, DPI/embedding coverage; label unsupported combinations.
- [ ] Measure long-session frame/memory/load effects before optimizing speculative work.
- [ ] Revisit secondary recognition engines only with clear need and measured benefit.
- [ ] Review optional product expansions separately; do not confuse them with bug fixes.

## Consolidated handoff and evidence records

Current source-matched final Windows **SUCCEEDED** `build-50c3dac169`,0errors/
75warnings,55.7469853s,247165633bytes; `windows-current-final-build-20261009.json`.
Current final Web **SUCCEEDED** `build-b3217372f0`, same stabilized runtime source,
non-development/gzip/fallback, `Builds/SinglePlayerRelease-Web-20261009-Final`.
Earlier “final” alignment artifacts below are preserved chronology and predate the
native silence guard; do not mistake them for the current final pair.

## Consolidated next-AI verification handoff

Do not rerun this implementation or automatically begin another feature phase.
Read the latest scope amendment, this report, gap ledger and original failure XMLs.
Preserve both interrupted Web folders, prior builds and historical manifests.
Root roadmap is the Unity-root `remaining_work.md`, not a nonexistent GrandFix copy.

1. Revalidate actual branch/HEAD/dirty checkout and active Unity project. Final identity
   must include the alignment/gzip changes and actual final artifacts. The manifest
   generator `-VerifyOnly` proves inventory/bytes, not builds/tests/voice quality.
2. Independent full/hostile regression is future work, not run in this closeout.
   Use installed Unity MCP `run_tests`/`get_test_job`, one job at a time, capture
   complete `Application.persistentDataPath/TestResults.xml` before the next run.
   Watch zero-test launchers, reload-stale job status and exclusions; recorded
   terminal XML may recover a handle but must match exact names/timestamps/counts.
3. Node gateway offline tests: in `backend/speech-gateway`, `node --test` (mock vendors/
   loopback fixtures). Do not read production `.env`/private-state/session files,
   expose tokens, issue public enrollment, reset spending ledgers or deploy remotely.
4. Serve the final complete Web directory with `node Docs/CommanderFinalization/GrandFix/web-host.mjs BUILD_ROOT --port 8088`.
   Loopback host keeps nonce CSP, restricted MIME/path access, permissions and no
   client secrets. Add only an explicitly configured exact gateway origin; real
   deployment TLS/CORS/iframe policy is operator-owned, not this local smoke proof.
5. Verify current packaged/served compact→type→minimize, HUD clearance, Escape/focus,
   readable approval, exact targets/new results and manual takeover. Native desktop
   Computer Use is not available in this session; Editor shots are not Windows
   packaged acceptance. In-app-browser load is not Chrome/Edge/Firefox/Safari certification.
6. Obtain deliberate microphone/online-upload consent and configured access before
   physical capture-to-reviewed-transcript-to-separate-approval gameplay checks.
   Do not record ambient audio or remotely upload prerecorded fixtures whose upload
   provenance is unknown. Native model load/runtime dependencies need actual player proof.
7. For future quality work, use same labeled recordings/consent across pinned models/
   online candidates, retain all failures, and run the existing bounded scorer.
   Record critical counts/negations/no-speech, timing/memory/FPS and device/browser
   versions. Do not invent a quality winner from model size or two keyword assertions.
8. Operator confirms historical credential invalidation separately (G01). Multiplayer/
   Rust/relay/peers and optional formations/exploration/save-load remain deferred;
   resume only with a separate request and the player/operator checklist.

Paid semantic0/6 and cloud audio0/600s at this checkpoint; no paid vendor/mic/deploy
action occurred. Existing durable budget controls remain; future attempts/repairs/
minimum billable audio count against the stated ceilings, cancellation is not a refund.

Final Windows build **SUCCEEDED** `build-ca7d6705ec`, non-development Mono,
SampleScene/LZ4/detailed, `Builds/SinglePlayerRelease-Windows-20261009/OpenEmpires.exe`.
0errors/75warnings,81.314889s MCP duration,247165321bytes native report. Report:
`windows-final-build-report-20261009.json`. Warning message rules:48deprecated API,
25serialization,2unclassified; this is not a harmlessness/resolution claim.
Runtime Assets frozen. Final Web **ACTIVE** `build-892e36f4d0`, non-development,
gzip/fallback, `Builds/SinglePlayerRelease-Web-20261009`; same stabilized runtime
source, no duplicate job. Poll exact job, then complete served artifact smoke.

Final Windows filename checks:0test assemblies/0`.env` filenames, actual notice and
five pinned native DLLs present; StreamingAssets `.bin` is116-byte ECS scene_info,
not Whisper weight. New hidden startup probe PID17468 yielded one `PlayerPrefsException`
in `NetworkManager.ProbeAllRegions` while storing a preference, plus unavailable
region-network probes;0crash/native-entrypoint/compile-rule matches. This is **not a
clean startup pass**, not microphone/native-model or interactive packaged evidence.
Storage/environment causality is unproven; do not speculate a graphics/ASR defect or
change frozen gameplay source. Actual interactive packaged startup/storage remains
an explicit next check. Only the exact task-owned hidden executable/PID was stopped
after observation; no Unity/user process killed. Log `windows-final-startup-20261009.log`.

Final Windows bounded streaming raw-byte candidate scan (`rg -a -l -P`) found0files
matching configured long OpenRouter/Google/OpenAI key patterns (including UTF16
OpenRouter form), exit1 meaning no matches. No matched values were printed. A prior
whole-string multi-encoding scan was stopped to avoid build-memory contention and
is not counted as passed. Neither scan proves compressed content/history clearance
or credential invalidation; G01 remains operator-owned.
Working manifest2108records/463changes byte-verified before alignment/gzip changes;
this checkpoint is historical, not a final-current-source identity or acceptance.

Primary verdict/source/builds/results/current gates are populated at the top. The
evidence below preserves checkpoint chronology. Do not mark the persistent goal
complete merely to end quickly; representative quality and required physical/operator
acceptance remain unproved unless the user explicitly moves them to future scope.
