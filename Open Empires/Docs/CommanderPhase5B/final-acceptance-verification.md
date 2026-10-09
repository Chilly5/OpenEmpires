# Phase 5B final acceptance audit

2026-10-09 — **REQUIRES CODEX SOL FIX**

The targeted idle-builder, sequential dependency and repeated-building counter fixes work in the tested cases. Broader natural-language interpretation still fails, the additional-Food objective did not finish, and the full offline regression is red. Phase 5B is not accepted or frozen.

## Source identity and boundaries

Project `D:/unity_projects/OpenEmpires/Open Empires`; branch `unit_models_and_voice_control`; HEAD `20da6eff6b63ebbf82cfd6570bbc416ce1d71832`. Read [Sol’s final report](phase5b-final-intent-progress-fix-report.md), [identity](source-identity.json), [original playtest](realistic-playtest-audit.md), and [prior independent findings](resource-placement-fix-verification.md).

The verifier passed at entry, continuation and closeout: **374 records**, aggregate SHA256 `bfaeb894e89f4e326b73f2c763ac1f9fb95ce2bf70ba492ca6428f206f5ab39e`. Independent per-record comparisons found zero mismatches. Coverage is runtime scripts, project/package configuration, listed native data/scenes and Phase 5B/economy tests; it does not cover every asset, meta, legacy test or document. Existing Sol production/test changes were uncommitted and preserved. No production/test edits, reset, clean, commit, push or new phase.

Unity MCP verified the actual project root, instance `Open Empires@6d7310c7`, Unity `6000.5.9f1`, and target `StandaloneWindows64`. Final fresh reads showed EditMode, not compiling, and zero console errors. This is current Editor evidence, not certification of an older package.

## Method and evidence limits

The first English/Albion 1v1 match was entered from the rendered menu (displayed seed `687500982`, owned TC #0 at `(193,142)`). The first command used Windows Computer Use; subsequent submissions used the **actual UGUI input field and Send callback**, including the existing game-side approval control. They did not directly call parsing/admission. The exact House→Wood preview was physically approved. Computer Use was stopped by the user with Escape and was not used afterward.

Runtime setup cleared AI player systems, disabled original TC auto-production and granted resources. An already issued enemy Scout order later caused combat; villager #40 lost health. Scout #16 was removed for subsequent cases. The first session therefore has this disclosed interference rather than an unconditional peaceful-match claim. The fresh continuation removed enemy-owned units, cleared AI systems, set Food to zero and provisioned six already visible berry nodes #2070–2075 to 10,000 before submission. No resource credits were used to finish a Food objective.

Native progress used normal simulation/construction/training rules. Bounded loops called Commander.Tick and Simulation.Tick; they advance native ticks faster than wall-clock play and do not shorten timers or set completion flags. Strategic background evaluation was not included in those loops. Immediate UI text can lag a projected state within the same synchronous loop; later Editor frames and task-board captures are retained. This is controlled Editor verification, not an uninterrupted standalone player session or full pixel/layout certification.

The temporary probes were compiled in memory, outside Assets. They forwarded unchanged production transport calls and observed typed results, native commands, goals, workers, buildings and task projections. They exported no credentials, headers, private request context or raw response body. [First capture](final-acceptance/live-evidence.json), [continuation capture](final-acceptance/continuation-live-evidence.json), [first probe](final-acceptance/FinalAuditProbe.cs), [continuation probe](final-acceptance/ContinuationAuditProbe.cs).

Each scenario has its own goal baseline. Historical completed cards in a rejection/clarification sample are **not** cards for that rejected request. Commander commands must be filtered by `source`; Human setup commands are also recorded.

## 1. Previously failing construction and restriction challenges

All successful simple builds below normalized to count 1 and Near placement, then used ordinary PlaceBuildingCommand and native completion.

| UI wording / state | Actual semantics and native result | Result |
|---|---|---|
| Build a Lumber Yard near the woodline with one idle villager. | First HTTP attempt timed out; no goal. Deliberate replay: VisibleResource/Wood/Tree, PreferredWorkers IdleOnly; goal #1 selected idle #1 while busy #0 was moving to dropoff/gathering. Placement tick 3075 `(188,155)`, Lumber Yard #4 completed; card 1/1. | PASS on replay; first timeout retained |
| Build a Mill near berry bushes with one idle villager. | VisibleResource/Food/Berries, IdleOnly; goal #2, builder #2; Mill #5 `(201,148)` completed, card 1/1. | PASS |
| Build a House near my Town Center, then assign one idle villager to Wood. | Sequential, node 1 dependsOn `[0]`; allocation SelectedCount/Exact 1/Idle/Wood. Zero goals/commands before explicit approval. House goal #3/building #6; allocation goal #4. House observed natively complete by tick 10676; Wood GatherCommand tick 10680 for #40. Overall request completed after both goals. | PASS |
| Repeat idle Lumber request with no idle villagers | Worker snapshot had gathering/moving/combat workers, no idle worker. Human gather protection had expired for the setup workers. IdleOnly persisted on goal #5; no Commander placement, truthful blocker and eventual 1800-tick failure. | PASS restriction/blocker control |
| Build a Mill near the berries using an idle worker. | VisibleResource/Food/Berries, IdleOnly; repeated Mill goal #6 selected #3, one placement tick 16845, building #7 `(199,152)` completed, **1/1 despite existing Mill**. | PASS |
| Build a Lumber Yard near the trees, but don't take workers off gold. | ProtectedResource Gold persisted; #0 was gathering Gold node #2270 after protection expiry. Goal #7 selected #1, placed tick 25380; building #8 `(199,159)` completed, 1/1. Gold worker remained on #2270. | PASS |

The busy-worker and no-idle cases establish that restrictions were not merely satisfied by coincidence. Repeated Mill and Lumber Yard requests no longer report completed 1/2. These passes do not prove arbitrary language fidelity.

## 2. Language fidelity and schema boundary

**Confirmed provider-stage failure:** “Build a House first, and only after it finishes send a villager to wood.” returned valid Clarify, zero nodes/goals, no pending draft. Its UI explanation claimed the contract cannot wait for House completion and introduced a newly produced villager condition. The exact “then” case above proves that actual completion dependencies are supported. The player did not ask to train a villager. This is an interpretation/capability explanation defect, not a planner failure. Full displayed text and safe trace are retained in `house-only-after-variation`.

Focused contract checks passed for required builder declarations, IdleOnly admission, unrepresentable counts, missing compound ordering, required Sequential completion edges, invalid placement anchor/relation/gap/source/resource, and private-value-safe diagnostics. Numeric-string count repair made exactly two fake-transport calls (initial + one repair), retained IdleOnly and initial field diagnostics; missing builder declarations made one call and could not consume repair. Other hostile/resource fixtures reject conflicting ordinals, hidden resources, ownership/provenance mutation and invalid structural references. These are **offline provider-boundary fixtures**, not paid malformed requests.

Live explicit-berries wording “Gather 400 additional Food from berries with four villagers.” received HTTP 200 but was invalid before admission. Safe trace: `schema=field=<unknown>;code=missing-or-wrong-type`; zero nodes/goals. Values/raw reply were not retained. **Do not invent the offending field or attribute this to the old Mill ordinal defect.** The diagnostic is bounded and safe but insufficient to identify the exact malformed field.

## 3. Essential acceptance cases

| Case | Semantic acceptance vs actual gameplay | Result |
|---|---|---|
| Gather 400 Food → clarification → “4” | First match retained amount 400/Stockpile through local reply, issued four-worker gathering, reached 210, then failed after visible berries depleted. Fresh provisioned replay retained Count=4/Any/Food/Stockpile, used one provider call (bare 4 resolved locally), remained waiting after assignment, and completed at native stockpile 400, tick 4186. Later live card observed 450/400 as gathering continued. | PASS controlled completion; depleted-source first outcome retained |
| Stockpile vs additional-gathered | With Food already 450, additional request normalized 400/AdditionalGathered/Exact4/Any; card began **0/400**, not 450/400. Native slaughter/gathering credited 150 additional Food, stockpile reached 600; request failed after its selected sheep source was exhausted. Provisioned visible berries existed at setup but no alternate native gather command was attempted. Explicit Berries variation then failed schema validation. | Semantic/progress distinction PASS; native 400-additional completion FAIL / recovery classification unresolved |
| Reach Age 3 | First attempt timed out. Replay normalized **AgeTarget Next (0)** from Age 1; native goal #3 advanced to Age 2 and completed card **Age 2/2**. It did not fulfill Age 3. | FAIL at interpretation |
| Reach Castle Age (Age 3). | Correction normalized Castle (3); ordinary landmark construction advanced Age 2→3 at tick 22166; goal #4 completed, card Age 3/3. Resource provision occurred before age requests, not completion flags/timer edits. | PASS native Age 3 control; original failure retained |
| Next five from a specific TC | “my first Town Center” returned Clarify asking for supported ordinal. “Town Center number 1” normalized WatchFutureUnits/Count5/ProducerOrdinal1/Gather Wood. Explicit approval bound original TC #0. Human queued five ordinary births there and one at fixture TC #8; watcher issued **zero training commands**, observed/assigned exactly `[55,58,60,62,64]`, and completed 5/5. Other births remained outside its Commander gather set. | PASS producer-bound native watcher; ordinal-language Clarify retained |
| Train five new Villagers and assign only those new units to Wood. | Capture contains three same-name submissions: two terminal timeouts, then HTTP-200 Request. Successful semantics: New count5, Sequential, allocation Exact5/Any/Wood, dependsOn `[0]`, resultFromNode 0. Explicit approval; five Commander TrainUnitCommands. Exact native request results `[69,71,72,73,74]` matched the gather ID set with **zero pre-existing intersection**. Both goals/card completed. | PASS later native result-binding case; not a one-submission reliability pass |
| Cancellation | Repeated paid watcher wording returned Clarify even with explicit number 1, so it never provided an active task to cancel. Separate labeled native UI fixture created pending House #8; actual active CancelTask callback cancelled it before any placement. | Native UI fixture PASS; paid watcher admission inconsistent |
| Manual takeover | Separate native House fixture #9 issued ordinary construction; Human Stop on builder #0 while unfinished left #0 Idle/no construction target. Another builder completed House #10 within the protection interval. Human/Commander command evidence retained. | PASS native authority fixture |
| Conversation and runtime reset | ResetConversation cleared draft/preview/transcript and minimized UI while pending native House #10 remained live, as intended. Stopping PlayMode then entering a fresh real Single Player→1v1 callback path produced zero goals, no pending preview/clarification, minimized UI. Focused tests additionally reject stale cards across generations/runtimes. [Reset evidence](final-acceptance/runtime-reset.json). | PASS native/UI fixtures |

The continuation added completed owned TC #8 as a **setup fixture**, not as Commander construction proof. English landmarks created by age progression also had auto-production enabled; these produced additional native villagers before/alongside watcher execution. That interference is explicitly retained and makes exact identity filtering essential. All owned building auto-production was disabled after train-plan approval; already queued births still remained outside that request’s result set. No training timer was shortened.

## 4. Regression results

- [Focused/hostile results](final-acceptance/focused-hostile.json), job `23509fea74734a5794afb1a646e249af`: **124/124 passed**, zero failed/skipped, 35.1114036 seconds. Suites: Phase4G hostile 39; Phase5A hostile 26; final intent contract 16; resource fix 28; task board 8; truthful status 7.
- [PlayMode results](final-acceptance/playmode-results.json), job `0733b3b46561415abdfb7b444a48152f`: **9/9 passed**, zero failed/skipped, 2.9886258 seconds. Real UGUI callbacks, clarification, native income/birth observations and authority. These do not substitute for live Luna interpretation.
- Incorrect assembly-name launch `e2a73c4e8a7b453cbc590589b9242604` ran zero cases. Excluded from pass evidence.
- [Full offline regression](final-acceptance/offline-regression.json), correct assembly `OpenEmpires.EditModeTests`, job `5f2b1d97e0dd4e5abffb6c72527ef5c3`: **terminal failed, 1585 completed, 20 uncapped failure records**. MCP’s TestJobManager.ToSerializable exports Result only for Succeeded; no aggregate pass/skip total is claimed from its null failed-job Result.
- [Isolated triage](final-acceptance/isolated-regression-triage.json), job `e45903268c084f6189b106198aac4799`: four cases completed; the garrison-reservation assertion, timeout UI helper collision and strategic approval assertion reproduced. These are not dismissed as full-suite contamination.

Failure groups and first stages:

1. Nine network fixtures: generated compatibility identity is stale. Current asset SHA `e42f0ae393d39fcb5ca49029d922798ad53319f539100de995a81aaacdcf7336`; live source computation `353c37c883227d214ee07de3201e9e1a836b553c5d7a96bc6e88366b2afbda0f`; NetworkCompatibility.Current returns null by its Editor safety rule. Artifact/release prerequisite, not evidence of relaxed network admission.
2. Four resource-reference fixtures: reflection supplies four arguments to an API that now has a fifth optional SourceKind parameter; TargetParameterCountException occurs before resolver behavior. Fixture/API drift; current fog/resource checks passed separately.
3. Response casing, Trebuchet-supported-content and two legacy capability-context assertions disagree with current content contracts. Preserve the failures for contract/fixture reconciliation; they do not establish unsafe native execution.
4. Garrison reservation fixture expects immediate release, while current WorkerAuthority retains living garrisoned references. Reproduces independently; Sol must reconcile the intended reservation lifecycle, not remove authority protection to force a green test.
5. Timeout UI fixture’s broad Single lookup finds multiple matching components; reproduces independently before the timeout behavior can be asserted. Live UI timeouts were truthful, but the historical gate remains red.
6. Strategic approval fixture stages successfully but TakeRecommendation returns null after rejected untrusted evaluation. Reproduces independently. The unauthorized-plan guard held; the trusted approval continuation requires source/contract triage. Existing hostile authority checks passed; this remaining regression is not certified.

## 5. Paid accounting and platform gates

**22 actual HTTP attempts**, all terminal: **17 HTTP 200 and five transport timeout/network errors**. Requested/returned successful envelopes identify `openai/gpt-6-luna`; rolling model revision is not exposed. There are no recorded numeric repair HTTP calls in this live capture. Paid accounting is separate from fake-transport repair tests and Codex token accounting.

The first capture used 10 of its enforced 100 cap; continuation enforced only 90 remaining and used 12. Protected prior test ledgers and credentials were not reset/rewritten. Two train-five timeout scenario entries precede the successful third entry; their shared labels are disambiguated by scenario order and HTTP ordinal, and their failures are not erased or described as a first-attempt pass. A temporary approval-review deadline delayed a runtime-resume call; it was not a production/provider error. No outstanding HTTP attempt remains at closeout.

Windows/Web packages were **not rebuilt, replayed or accepted**. The Phase5B Windows runtime DLL was written `2026-10-08T23:48:16Z`, Web index `2026-10-09T00:07:56Z`; the new declaration source was written `2026-10-09T12:04:22Z`. The separately named SinglePlayerRelease “Final” Windows/Web artifacts also predate the fixes. Timestamps and Sol’s report establish stale inputs; they are not a replacement for an eventual source-matched build manifest. Rebuild authorization and fresh package/runtime acceptance remain release gates after Sol fixes; no packaged acceptance claim is made.

## Sol handoff and remaining release gates

1. **P1 provider fidelity:** reproduce the exact only-after-finish wording against this contract. It falsely rejects supported dependency behavior and invents a new-unit condition. Preserve bounded declarations and deterministic completion edges; do not add phrase-specific execution checks, silent constraint dropping or auto-approval.
2. **P1 numeric age fidelity:** from Age 1, “Reach Age 3.” produced Next and completed Age 2. Named Castle target completed Age 3. Fix interpretation/contract coverage without mapping every age request to Next or treating the wrong admitted goal’s completion as success.
3. **P1 source/amount execution gate:** additional Food progress was correctly separated from stockpile but stopped at 150/400 after the prepared sheep assignment ended. CommanderPlanner.Economy retains a one-time prepared assignment/command set and blocks on missing matches; no same-worker alternative source attempt occurred. Determine whether a currently visible, reachable alternative under the same Any/Food request should be retargeted. Exact alternative reachability was not independently proven before runtime reset; do not assert a pathfinding cause. Preserve worker identity, human takeover and explicit source restrictions. A supported-source completion replay remains necessary.
4. **P1 provider/schema and ordinal explanation:** explicit Berries additional-Food request was rejected with generic unknown-field diagnostic; the actual offending field is unverified. First/number-1 producer wordings inconsistently Clarify although ordinal binding works when Request is supplied. Add bounded diagnostics/interpretation coverage without exporting raw replies or making the model choose actual producer IDs.
5. Reconcile the 20 full-regression failures, including independently reproduced approval/reservation/UI fixtures, and rerun a source-matched offline regression. Maintain current hostile authority/fog and numeric-only repair gates.
6. Reverify corrected first-failure wordings through the real Commander UI; preserve repeated-building, exact new-unit identity, Human Stop, cancellation/reset and current amount semantics. Then rebuild Windows/Web with authorization and validate both fresh packages before freezing Phase 5B.

Physical voice, multiplayer/Rust acceptance, save/load and unrelated phases were not added to this audit. No next phase was started. Verification work stops with this handoff.
