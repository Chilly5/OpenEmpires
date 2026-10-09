# Phase 5B — implementation and Luna handoff

Status: **PHASE 5B IMPLEMENTED — READY FOR CODEX LUNA VERIFICATION.**
User brief: attached `7c44130c-b942-4501-b186-dad3b0f9cfdd/pasted-text-1.txt`.
Baseline HEAD: `35be7427503cdfe04c3a6ce5474fc50fabf247a6`; current local checkout, no commit/push/reset/clean.

## Changes

- Typed pending worker requests preserve original input/request identity/runtime generation, distinct worker count and resource amount/meaning, worker eligibility and resource/source restrictions. Numeric/word replies fill Count locally. Strict semantic/DTO validation and structural scope equality include new fields. Corrections are field-local; cancellation/reset/late-response checks remain bounded.
- Content recognition reads native UI labels and native civilization replacement/producer/age sources. Canonical identities are managed-only/thread-safe; discovery does not grant execution. Ordinary roster training and ordinary construction now have mappings. Exact aliases, bounded typo suggestions and ambiguous functional names are separate outcomes. Provider context retrieves up to eight relevant entries rather than the first twelve units.
- Resource objectives reuse the existing worker goal lifecycle: Stockpile observes current balance; AdditionalGathered observes authoritative credited gathering income since activation. Starting stock, refunds and generic credits do not count as gathering; spending cannot erase credited income. Assigning workers does not complete the objective.
- Finite WatchFutureUnits subscriptions bind a game-owned producer at preview and revalidate that exact producer at approval/commit. Existing queues count as future births, but no extra production is queued. Train-N-and-follow-up instead uses only its own native attributed results. Observed/assigned/interrupted counts are separate; assignment is credited only after native processing.
- Task board projects existing goals/requests/strategic plans, with one request card and subordinate steps. It shows real observations/blockers, supports validated Cancel, keeps all active tasks discoverable and limits terminal history. Minimized startup and a compact active badge remain; updates do not open conversation. No new generic Pause scheduler.

## Semantics and boundaries

Plain “gather 400 food” selects a **400-food stockpile target**, shown on the card; “gather 400 additional food” selects additional delivered gathering income. The worker count is a separate slot.

“Next five villagers from this TC” watches matching births after activation, including pre-existing/human queues, without training. “Train five new villagers and send them to wood” owns new production and binds only attributed results. A lost/overridden unit is not replaced by extra births. Producer loss does not switch buildings. Cancelling a request does not undo completed gameplay or unrelated human production/orders.

LLM output stays bounded semantic data. Runtime entities, workers, coordinates, placements, command generation and authorization remain game-owned. Future/compound orders use explicit preview confirmation; numeric clarification is not unrelated approval. No command/network serialization redesign, multiplayer, save/load or voice-engine work.

## Evidence ledger

Exact RED/GREEN jobs and integration findings are in [progress.md](progress.md). Confirmed native exact-production scenarios: `6087944f72be453b9bf038a4c16ca0b0`, 10/10 focused EditMode tests, including native Train(New)→gather and Train(New) Spearmen→patrol with exact result IDs. These tests run the real simulation/commands with accelerated fixture queues, not a paid live provider or standalone player.

Final focused EditMode `30694838a3c24acc9539bf75c52e58dd`: **104/104 passed**, 43.9033793s. Final PlayMode `3cf74e2b486b4854a845951b2f648022`: **4/4 passed**, 1.5260707s, including real UGUI minimized/task-Cancel/reset controls and native observation hooks. Native simulation scenarios use actual training queues, receipts, normal gather/patrol/slaughter commands and exact born IDs, not parser-only assertions.

Source identity: [source-identity.json](source-identity.json), **368 records**, SHA256 `8fa3542c3df5de5026dbf1d7a9bdc52054130f7789ce23c980108b59f9ba4f8a`; baseline HEAD above. Verified before and after Windows build and during Web observation. Hash equality is source identity, not runtime certification.

Windows `build-ef239ad274`: **succeeded, 0 errors/75 warnings**, 193.9854909s, 632.38MiB. Package: `Builds/CommanderPhase5B-Windows/OpenEmpires.exe`; executable SHA256 `36c5c9f13481406382a8e9ef8fc0ea7cdf055c43bb12fc8fd545b07c199cd277`.

Web `build-e4976ada39`: **succeeded, 0 errors/90 warnings**, 1090.16023s, 125.56MiB. The original job recovered and returned its terminal report; no duplicate build or forced editor restart was performed. Package: `Builds/CommanderPhase5B-Web/index.html` with data/framework/loader/wasm present. Wasm artifact SHA256 `fb3fdc20e345467fd195a7a006d837fd8127c8d0bc23d403d5e025ec64ea2a99`. Earlier stale-bridge observations were an evidence gap, not a claimed failed build.

Browser Web smoke on this exact package: local HTTP host, rendered main menu → Single Player → 1v1 → actual match. At1936×1048 the HUD/Commander were in bounds; Commander started minimized, expanded to show task-board empty state and normal input, unsent numeric draft “4” left no task/strategy/approval, Escape minimized it. No Send, microphone, provider request or sensitive-data entry. Owned browser tab and host were ended afterward. Active Unity platform restored and read back as **StandaloneWindows64 / Standalone / Player**; source identity still matched.

No full historical EditMode/PlayMode regression has been run. No hostile audit, paid LLM/ASR call, physical microphone test or large standalone acceptance package was run. Interactive Windows testing and exhaustive live-provider/gameplay scenarios remain on Luna's verification checklist; builds, native/editor scenarios and the limited browser smoke are not exhaustive standalone certification.

## Codex Luna verification checklist

1. Verify final source identity and test/package evidence below before trusting historical results.
2. With configured provider, test “gather 400 food” → “4”; “gather food” → “four”; amount/count corrections, source restrictions, new-command supersession, cancellation/reset and delayed replies. Check one goal submission and no numeric approval of a different plan.
3. Sample every native unit/building/unique landmark name, canonical alias, typo and functional description across English/French/HRE. Check known unsupported/foreign/age-restricted content is explained rather than invented or silently replaced. Test “lumber yard”, “lumber camp” and ambiguous “dropoff building”.
4. Observe Age 3 and stockpile/additional-food cards through real completion, spending/refunds and cancellation. Check one request card for compounds, actual blockers and active badge while minimized.
5. Native next-N orders: pre-existing queue, multiple producers/ordinal ambiguity, owner change/destruction, duplicate/stale birth events, manual takeover, cancel before native dispatch and reset. Assert no extra Train command or replacement births. Test Food from owned Sheep and Wood.
6. Train-N-follow-up: compare exact native attributed result IDs against gather/patrol subjects; unrelated existing units/human queued births must not satisfy it.
7. In Windows and Web single-player, verify layout, task scrolling/Cancel, minimized startup and chat/input/approval separation. Reset conversation while an ordinary task remains live, then cancel via its refreshed card.
8. Run expanded regression/hostile cases independently if desired; do not interpret this phase’s focused evidence as exhaustive certification. Do not begin another phase automatically.

## Known limitations / final evidence

All narrow Important review findings are fixed and covered by focused tests. The named content inventory covers 17 native unit identities and 36 building identities (22 ordinary enum identities plus 14 native named landmarks). Ordinary build/train mappings are expanded; special landmark choice, wall/gate segments and Wonder mechanics remain known but not newly executable. Existing bounded actor-selector/point-patrol conventions remain; discovering content does not grant unsupported mechanics. Indefinite “every future unit”, multiplayer/save persistence and new voice engines are outside Phase 5B.

No required Phase5B implementation item remains open. Luna owns independent verification and any newly discovered fixes, using the checklist above. Stop here; do not begin another phase automatically.

## Requirement-to-evidence closeout

| Brief section | Authoritative implementation/evidence |
|---|---|
| 1. Clarification context | Typed draft/allocation, strict parser/DTO/scope equality, original request ticket; focused chat-to-goal tests retain400food+worker4, source/state, corrections, supersession, cancellation/reset/late responses. |
| 2. Complete recognition | Native UI label/producer/age/civ projections; 17 unit +36 building identities, named landmark/native cost tests, aliases/typo/functional ambiguity tests, ordinary build/train admission and bounded relevant context. |
| 3. Objectives/cards | Existing goal/request/plan projection; native gathered-credit counter and stockpile/additional lifecycle tests; actual UGUI grouping, Cancel, badge, reset callback and no-forced-open tests. |
| 4. Future units | Native producer-birth ordinal and exact receipts, pinned producer preview/approval, native processing credit; real queue/new-production/gather/patrol/slaughter, cancellation/takeover/loss/dependency/replay tests. |
| 5. Authority | Strict typed references and field equality, explicit future/compound preview, validated runtime-affine card controls, normal commands. Existing allocation/new-versus-total focused compatibility tests remain green. |
| 6. Acceptance/handoff | Final104Edit+4Play, same-source Windows/Web builds, limited Web UI smoke, source identity, updated roadmap and this Luna checklist. No broad regression or next phase. |
