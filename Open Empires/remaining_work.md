# OpenEmpires AI Commander — Remaining Phases Handoff

## Phase 5A Closeout & Test Reconciliation (2026-10-07)

Phase 5A is **ACCEPTED / FROZEN** and its historical test-suite reconciliation is **COMPLETE**.
The independent AntiGravity hostile acceptance audit verified all strategic authority repairs, intent fidelity / request-scoped authorization boundaries, confirmation integrity rules, DynamicPlan DSL bounds, and deterministic execution invariants.

Primary acceptance question verified:
> Can a human player naturally request reasonable combinations of supported OpenEmpires mechanics while the Commander remains unable to invent a new strategic goal, gain authority from provider output, access forbidden information, or bypass deterministic game-side execution?
> **VERIFIED: YES.**

Verified totals:
```text
EditMode full regression baseline: 1,124 / 1,124 passed (100% GREEN, 0 failed, 0 skipped)
PlayMode historical suite reconciliation: 24 CommanderPhase4D3HostPlayModeTests + 4 Host/PlayMode suites resolved via trusted authorization fixtures
AntiGravity hostile test suite: 26 / 26 passed (CommanderPhase5AAntiGravityHostileAuditTests)
PlayMode authority verification: 1 / 1 passed (CommanderPhase5AAuthorityPlayModeTests)
Modern suites (Phase 4C through 5A): 100% GREEN
Production code modifications: 0 (Test-only reconciliation)
Fresh Windows 64-bit standalone build: Builds/Windows/OpenEmpires.exe, succeeded (0 errors)
Standalone execution: Clean D3D12 startup; 0 errors and 0 unhandled exceptions in Player.log
Source manifest: sources=68; changed=206; artifacts=79 (refresh-source-manifest.ps1 -VerifyOnly passed)
```

Final report: `Docs/CommanderPhase5A/antigravity-final-audit.md`.  
Phase 5A is **ACCEPTED / FROZEN**.  
**STOP: Do not begin Phase 5B automatically.**

### Post-acceptance verification continuation (2026-10-07)

The four reconciled PlayMode suites passed individually: Phase4B2 3/3, Phase4C1 17/17, Phase4C2 7/7, and Phase4D4Host 12/12 (39/39 total). The full PlayMode attempt completed 201/201 tests but reported one actual failure in the live Luna scenario `MillNearVisibleBerries_RealLunaNativeConstruction` at its tick-6000 lifecycle assertion. Unity MCP omitted the failed job's result summary and stack trace, so the pass/skip split and root cause remain unknown. The isolated follow-up stopped during fixture setup because the six-call provider cap had already been consumed; it issued no provider request. The full PlayMode result is not green and no production regression is established by the available evidence.

The accepted 1,124/1,124 EditMode run, 26/26 hostile suite, 1/1 authority PlayMode run, and clean standalone startup remain prior evidence and were not rerun here. Production C# hashes match the existing source manifest; no production code was changed during this verification continuation. Current standalone executable and runtime DLL hashes still match the accepted audit. Detailed job records are in `Docs/CommanderPhase5A/reconciliation-playmode-evidence-2026-10-07.json`. Keep Phase 5A frozen; do not begin Phase 5B.

The updated manifest records 78 sources, 217 changed files, and 79 XML test artifacts; the final `-VerifyOnly` check passed after these documentation and evidence updates.

### Live Luna Mill diagnostic continuation (2026-10-07)

The earlier full PlayMode result remains failed: job d957a71711a14314a727b6b1da4a4f5f completed 201 tests and reported the Mill scenario's tick-6000 assertion. After a normal Unity domain reload reset the live-call counter to zero, the single failing test passed twice in isolation (jobs 0a3a0151df9843a1a82b0a5946c60cba and fa99d068d2124d3e9e9df496fd6e6404). Both provider responses were HTTP 200/finish=stop and normalized to the same valid Request for one Mill near the worked berry; both completed through the Commander command path.

The failed full-run response, semantic mode/node, and request trace remain unavailable. Source flow proves that run returned a non-null IsValid result before the lifecycle assertion, but does not distinguish a valid non-effectful interpretation from tactical admission behavior. The live test accepts any IsValid semantic outcome before waiting for a Mill, so provider variability can surface as a lifecycle timeout; this is a harness classification/diagnostic concern, not evidence of a production defect. Final diagnostic: LIVE LUNA FAILURE STILL UNCLASSIFIED. No production code or tests were modified; no Sol production fix handoff is justified. A future Sol review may improve outcome-specific reporting without weakening the effect expectation. Do not begin Phase 5B.

The complete trace comparison and evidence limitations are recorded in Section 12 of Docs/CommanderPhase5A/antigravity-final-audit.md and the live_luna_mill_diagnostic field in Docs/CommanderPhase5A/reconciliation-playmode-evidence-2026-10-07.json.

## Phase 5A implementation handoff — 2026-10-07

**READY FOR ANTIGRAVITY AUDIT** — implementation/focused-evidence handoff, not release acceptance. The earlier in-progress entries below are chronological history, superseded by this entry and Docs/CommanderPhase5A/phase5a-implementation-report.md.

Baseline remains branch unit_models_and_voice_control, HEAD4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca; actual dirty/uncommitted Unity root is D:/unity_projects/OpenEmpires/Open Empires, Unity6000.5.9f1. Human background/emergency commitment is gated by actual consent; explicitly configured computer owners retain autonomy; authorized continuations remain. Local request/candidate authority, normalized single-use preview, existing KnownIntent fast paths, strict nine-primitive DynamicPlan/compiler, canonical Farm/Mill mechanics, frozen disjoint worker roles and exact producer/production results are implemented. Shared constraints reach implicit preparation; sticky takeover and cached Gold preservation are fixed. Canonical civilization replacements use the existing resolver and truthful preview names. Opt-in traces are bounded metadata, not authority/wire provenance.

Final focused EditMode48/48 (866e8777) and final-source PlayMode5/5 (3284c57a) passed, zero failures/skips and zero console errors. Six required configured Luna/normal UI/native-command scenarios have scoped passing evidence; 13 submitted interpretations total, earlier failed attempts retained. No additional paid calls for closeout. Full source/protected/evidence hashes and fourteen documents are in Docs/CommanderPhase5A, with guideline updated. Do not sum overlapping focused runs or treat historical suite totals as tests of this source.

Next owner: AntiGravity, following antigravity-test-plan.md. Still required for acceptance: full final EditMode/PlayMode, current Windows standalone build/Player.log, actual native UI/voice/reset/late-response checks, exhaustive hostile authority/graph/repair/result-concurrency/takeover matrix, all civilization/placement/source/modifier cases, and multiplayer owner/determinism/version-compatible peer checks where available. Historical economy/result-binding acceptance remains pending beyond the scoped refresh. Core/Buildings changes are local observational receipts/correlation; no new provenance wire bytes, but pre-existing restricted economy packets still require compatible updated peers.

Supported limits: UnitSet has no new DynamicPlan effect consumer (existing KnownIntent follow-ups retained); only one ResourceSource per candidate and four shared constraints total; no conditions; Near gap exactly1; no deterministic English entailment. Unsupported constraints/mechanics must not be dropped or widened. Small localized audit fixes are AntiGravity's scope; architectural changes need an explicit fix phase. **Do not begin Phase5B automatically.**

## Phase 5A implementation in progress — 2026-10-06

Latest follow-up: allsixmandatoryrealLuna/native scenarios nowpass throughnormalUI/ordinarycommands (Farms, workedberryMill, shared3Idle2Sheep+thirdMill, 2newBarracks+10exactnewSpears, immediateKnown4berryworkers, nonexecutingCastlequestion). Failures retained;13total submittedliveinterpretations, paidchecks stopped. Targetedreview origin-lossreplacement/cancelcleanup/Neargap fixes pass32focusedcases, and canonicalmutation usesexistingvirtualBarrackscost (Farmcostreadonlyconstant, unchanged). Finalsource/constraint/result/voice/economy proof and documentation/manifest/guideline/requirementaudit stillopen; notREADY orcomplete.

Latest 2026-10-07: shared dynamic constraints pass16 focused cases; duplicate KnownIntent building vocabulary/parser lists were aligned to existingcanonical construction support after realLuna Farm mismatches. RealLuna/native Farms, worked-berry Mill and read-only Castle question now pass. Simple allocation nativeproof, shared-worker composition and two-new-producer composition remain open after threefailed attempts (onefixtureassertion issue, twoproviderrepresentation gaps); no paidbrute-force retries. Nine total submitted live interpretations sofar, no billingclaim. Completeattempt/passXMLs retained. Fullgoal remains active; noREADYverdict/fullsuite/standalone/Phase5B.

2026-10-07 update: independently typed effect scopes and complete symbolic previews, actual DynamicPlan UI confirmation, correlated KnownIntent admission before first publication, request-wide confirmed-plan cancellation, canonical content-ID projections, and bounded numeric-shape-only OpenRouter repair are implemented. Terminal79/79 affected focused EditMode checks pass (job6e680daa, no full regression). Real native lifecycle/Luna fixtures are being prepared; no paid/live provider call has yet been made. Dynamic prerequisite/negative-constraint completeness, result freshness/cleanup, mandatory live scenarios and final evidence/docs/review remain gates, not waived by these checks.

Current baseline is `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca` on `unit_models_and_voice_control`, live Unity root `D:/unity_projects/OpenEmpires/Open Empires`. Current implementation is dirty/uncommitted by explicit user instruction. Preserve the historical investigation and accepted-phase records below; none certify this changed snapshot.

Checkpoint A authority repair has focused evidence: original authority RED8cases (7failures), initial GREEN8/8, affected Edit29/29, and Play3/3 including actual bootstrap qualifying human/advisory and explicit computer control. Human background/emergency recommendations cannot commit via StrategicPlanner; direct/consumed-approved runtime-bound roots retain execution/continuation. Source labels/priority alone do not grant consent. Ordinary opening-state/user-incident replay is not claimed.

Checkpoint B partial implementation: normalized compound preview, one local single-use approval, manager-bound request/runtime/generation/revision sidecar, lower goal-manager unsigned-graph rejection, request/root dispatch correlation, and request-global NoConstruction (new and resumed construction) with strict four-constraint roundtrip. Later authority/observer/generation evidence is43/43 focused; negative constraints3/3 focused. Remaining B work includes independently grounded typed root/effect matching, full prerequisite/worker/source constraints and complete provenance/cancellation on simple/strategic paths.

Checkpoint C partial implementation: strict immutable nine-primitive DynamicPlan schema/parser and side-effect-free canonical compiler; exact accepted training-order/spawn attribution and original native placement correlation; exact newly built producer collections; frozen atomic shared-worker selection/partition and sticky takeover guards. Terminal22/22 covers compiler3, local relay ledger13, result/producer6; subsequent7/7 covers static compiler3, producer2 and actual shared-worker admission/ordinary commands2. These are focused in-process proofs, not full suites, live Luna, standalone or multiplayer peer certification. Semantic location/runtime adapters are currently being integrated after expected REDs; that newer source is not yet verified.

Latest location integration10/10 focused passed (job1872a087): one/two distinct exact Mills near a frozen visible berry source, three-worker atomic/disjoint role assignment, sticky human takeover, compiler and exact producer checks. Normal placement/cost/commands run, but foundations were explicitly completed to isolate attribution/placement from travel/construction duration. Complete XML and a dirty source-byte progress snapshot are retained. Full native lifecycle, live provider, standalone and multiplayer peers are not proven.

Remaining C/D gates: actual canonical Farm lifecycle and canonical mutation/discovery proof, owned/future-result anchor and selected-producer loss/freshness cases, complete selector/constraint/quantity semantics, normalized preview of all symbolic restrictions, provider/UI integration/repair/truncation limits, all mandatory real Luna/UI/normal-command scenarios, final focused integration/review and complete AntiGravity documents/manifest. No final Phase5A verdict or release acceptance yet; goal remains active. Do not begin Phase5B/full regression/standalone/hostile audit here. Independent audit still owns historical economy/result-binding/standalone/multiplayer verification.

Follow `Docs/CommanderPhase5A/phase5a-specification.md`, `phase5a-implementation-plan.md` and `execution-progress.md`. No commits, pushes, provider switches, credential disclosure or gameplay-network redesign authorized.

## Pre-Phase-5A intent-fidelity investigation — 2026-10-06

**Investigation complete; Phase 5A implementation not started.** A focused existing PlayMode test confirms that a background strategic evaluation with no player intent can select and submit a plan. The ordinary opening-state snapshot is not statically expected to meet the tested recommendation thresholds, and a full match-start reproduction or Luna response was not captured. No source code was changed. See `Docs/CommanderPhase5A/pre-phase5a-intent-fidelity-investigation.md` for the call graph, evidence limits, provider audit, and proposed Phase 5A invariant.

## Targeted economy/clarification repair — 2026-10-06

**READY FOR FINALIZATION AUDIT**, subject to the exact source/evidence records in `Docs/CommanderFix` (not a release acceptance or new phase).

- Generic AllocateWorkers now preserves exact ordinary counts, Idle/current-resource criteria, explicit canonical source kinds and bounded all-idle snapshots. Legacy desired-state/strategic economy policy is unchanged. Deterministic game-side selection, full preflight/reservation and normal Gather/Slaughter commands retain human authority.
- Structured pending clarification preserves Food/Sheep/worker criteria; `gather food → four` fills locally and creates no production goal. Explicit corrections are guarded; cancelled/reset/late old-runtime state cannot resume.
- Native restriction/version1 restricted command encoding was conditional: an actual Sheep carcass depletion retarget to berries proved it necessary. Unrestricted binary legacy bytes are unchanged; restricted commands require updated peers.
- Final focused economy EditMode: 15/15. Relevant Commander suites: 204/204. Five real Luna requests: all HTTP200/finish stop with expected typed output, no retries; bare count completion made no additional API call.
- Full suites were run once each: EditMode998/998; PlayMode193/194, with one existing strategic lifecycle busy-guard failure. That failure was reproduced and repaired narrowly. Post-repair affected checks: PlayMode16/16 and EditMode38/38 (including economy/chat/host/voice/result-binding). Original failed full XML is retained; do not call the final full PlayMode snapshot green or assume an exhaustive post-repair rerun occurred.
- No standalone build, native Computer Use, live two-editor multiplayer or hostile audit was done in this targeted fix. Independent finalization audit should verify the complete final checkout and those applicable release gates, plus existing producer-attribution/Scout/exact-structure priorities.
- Source checkpoint097f01b and manifest include preserved dirty integrated hooks; HEAD alone is not the tested source. Do not reset/overwrite the existing Phase4G/4H/provider/UI/package work. Gemini configuration was not deleted.
- Stop here. Do not begin Phase4I automatically. The older Phase4G/4H acceptance and narrower result-binding records below are historical evidence, not certification of this changed snapshot.

Recorded: 2026-10-05

## Current narrow Phase 4G refresh — 2026-10-06

**READY FOR ANTIGRAVITY AUDIT** for the refreshed result-binding implementation. Stop here; no automatic next phase.

- Added explicit simulation/source-manager identity and mandatory-result checks, immutable result IDs, civilization-resolved selection, explicit Scout production adapters, exact-building rally without unrelated-unit selection, and sticky result-bound human override.
- Fresh selected EditMode: 9/9, zero failures/skips, job `d1766219a9b34da3b5ab637ec73bc976` (3.8637103s).
- Fresh single PlayMode: 1/1, zero failures/skips, job `974e325de24b421f8d35af8f393887eb` (0.7476932s). New Spearmen 7,8,9 patrolled; existing Spearman 6 excluded, tick 901.
- Documentation and the current source manifest/consistency record are in `Docs/CommanderPhase4G`. This controlled semantic-fixture proof is not live provider, standalone, or native UI acceptance.
- AntiGravity owns exhaustive regression, independent hostile testing, standalone verification and small final fixes. Priority cases: concurrent/queued producer attribution, actual Scout-to-scouting, constructed-building-to-rally, disposed/evicted/cross-manager references, override timing and partial takeover.
- No full suites, hostile audit or standalone build were run by this refresh. Existing provider/UI/voice and package working-tree changes were preserved and are outside its verification claim.

The acceptance/build records below are historical snapshots from 2026-10-05. They remain intact, but do not certify the current changed source. The immediate next step is independent audit of this refresh, not automatic release finalization or a new feature phase.

## Phase 4H Closeout — AntiGravity Hostile Audit Passed (2026-10-05)

Phase 4H is **ACCEPTED / FROZEN**. Final AntiGravity hostile audit, local Whisper integration, real audio fixture verification, and full regression battery passed completely.

Verified totals:
```text
EditMode full: 969 / 969 passed (job `7bfcf1e4674f4067a9de92e4490d524e`, duration 196.73s)
PlayMode full: 190 / 190 passed (job `ff08847d590f4b73a954cac6b185b996`, duration 113.30s)
Phase 4H unit tests: 12 / 12 passed
Phase 4H audio fixture tests: 4 / 4 passed (whisper.unity local inference)
Phase 4H PlayMode tests: 6 / 6 passed (job `9e1e55c176a743339f96ac1fff0ac0d9`, duration 1.62s)
Total Phase 4H tests: 22 / 22 passed
Combined engine regression: 1,159 / 1,159 passed (0 failed, 0 skipped)
Windows build: `Builds/Windows/OpenEmpires.exe`, job `build-2f1418ff23`, succeeded (0 errors, 74 warnings, 706.08 MB)
OpenEmpires.exe SHA-256: 36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277
OpenEmpires.Runtime.dll SHA-256: D1DFDF284217D3D684AB0A2F278885FDB3E7DF05901933B47573CCE00B8C1823
com.whisper.unity.dll SHA-256: 02C6C797FEDF95A6348B0B0A7BD77D550971E5AA8DD5B13209A9E3B4A2BC2AEB
libwhisper.dll SHA-256: 63C782F7A8D2EB3EE7F0CC41508DD53E1BF14DAD9165BE89B10AA5207A1DF687
ggml-tiny.bin SHA-256: BE07E048E1E599AD46341C8D2A135645097A538221678B7ACDD1B1919C6E1B21
Standalone launch: verified clean engine/player startup; 0 exceptions in Player.log
```

Phase 4H verdict is **READY FOR RELEASE CANDIDATE / FINALIZATION**.
Phase 4E is ACCEPTED / FROZEN.
Phase 4F is ACCEPTED / FROZEN.
Phase 4G is ACCEPTED / FROZEN.
Phase 4H is ACCEPTED / FROZEN.
NEXT: **Release Candidate / Finalization**.

## Phase 4G Closeout — AntiGravity Hostile Audit Passed (2026-10-05)

Phase 4G is **ACCEPTED / FROZEN**. Final AntiGravity hostile audit and full regression battery passed completely.

Verified totals:
```text
EditMode full: 953 / 953 passed (job `10a473c668c6406197c281a06dfa6c62`, duration 214.85s)
PlayMode full: 184 / 184 passed (job `e31fd48af2794202963825b747eeee78`, duration 104.44s)
Phase 4G hostile audit: 33 / 33 passed (job `7f85a277aaa54c2ca056912077a64509`, duration 37.85s)
Phase 4G capability tests: 14 / 14 passed (job `2efc9d67f938482e9e54283ee7bec635`, duration 7.27s)
Phase 4G result binding PlayMode: 1 / 1 passed (job `01a7de1a8a2d43ff8495e2d36d657a99`, duration 0.43s)
Windows build: `Builds/Windows/OpenEmpires.exe`, job `build-85dbe438d4`, succeeded (0 errors, 74 warnings, 610.3 MB)
SHA-256: 36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277
Standalone launch: verified clean engine/player startup; 0 exceptions in Player.log
```



Repository:



```text

https://github.com/Chilly5/OpenEmpires/tree/unit_models_and_voice_control

```



Unity project development has historically included important local working-tree changes that may not yet exist in Git HEAD.



Therefore:



```text

LOCAL UNITY WORKING TREE = SOURCE OF TRUTH

```



## Current Phase 4F closeout (2026-10-04)



Phase 4F implementation is present as a non-invasive detached knowledge projection, with a separate Commander capability catalog and bounded provider-context slice. Canonical source provenance is recorded in `Docs/CommanderPhase4F/source-data-inventory.md`; developer guidance is `Docs/Commander/guideline.md`.



Current verified totals after AntiGravity hostile audit:



```text

EditMode: 906 / 906 passed (job `5c88f592a9fd48dda0fd5b212445a8dd`, duration 232.35s)

PlayMode: 183 / 183 passed (job `e8be71c1eb5945d0b8d2c5b19a1a017c`, duration 98.98s)

Phase 4F focused hostile: 27 / 27 passed (job `9f94e104843a4783af5e91a8245a1926`, duration 1.94s)

Windows build: `Builds/Phase4F/OpenEmpires-Phase4F.exe`, job `build-585b3f5a13`, succeeded (0 errors, 74 warnings, 610.25 MB)

SHA-256: 36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277

Standalone launch: verified clean engine/player startup; 0 exceptions in Player.log

```



Phase 4F verdict is **READY FOR PHASE 4G**.

Phase 4E is ACCEPTED / FROZEN.

Phase 4F is ACCEPTED / FROZEN.

Phase 4G is ACCEPTED / FROZEN.
Phase 4H is NEXT (Local Whisper Voice / Speech-to-Text Input). Phase 4H remains frozen until explicitly directed.



Do not assume GitHub or Git HEAD contains the entire current Commander implementation.



---



# 1. Final product vision



The goal is an AI Commander for OpenEmpires that allows a player to interact naturally.



Eventually the player should be able to type or speak requests such as:



```text

"Hey, I want 10 spearmen."



"Build a barracks five tiles left of my town center

and make 10 spearmen from it."



"I want to reach Castle Age."



"Build a mill near the berries my villagers are working."



"Move my scout around their base so I can get vision."



"Put more villagers on gold."



"Defend this gold."



"Rally new spearmen near the bridge."



"What counters spearmen?"



"How do I reach Castle Age?"



"Why haven't you finished making my army?"

```



The player should not have to memorize a Commander command syntax.



The AI should understand normal language.



But the AI itself must never directly play the simulation.



The permanent architecture is:



```text

PLAYER

  ↓

natural language

  ↓

LLM / semantic interpretation

  ↓

bounded typed data

  ↓

strict game-side validation

  ↓

deterministic selectors / planners / goals

  ↓

ordinary gameplay ICommand

  ↓

CommandBuffer

  ↓

GameSimulation

```



Never:



```text

LLM

 ↓

GameSimulation

```



The project's foundational rule remains:



> **Player decides. Deterministic game systems execute. The LLM interprets, explains and advises.**



This authority separation has been a permanent architectural requirement throughout the project.



---



# 2. Development workflow



The development workflow should continue to be:



```text

Phase specification

      ↓

Codex implementation

      ↓

focused RED/GREEN tests

      ↓

full Unity regression

      ↓

playable/runtime evidence

      ↓

Codex closeout report

      ↓

AntiGravity hostile independent audit

      ↓

Fix phase if Critical/Important issues exist

      ↓

re-audit

      ↓

freeze accepted phase

      ↓

next phase

```



Do not skip the independent hostile audit merely because the test suite is green.



Earlier hostile audits found real defects despite passing automated tests. The established workflow intentionally combines testing, runtime proof, source inspection and independent boundary review.



---



# 3. Permanent safety invariants



Every future phase must preserve these.



```text

LLM cannot create ICommand.



LLM cannot enqueue CommandBuffer actions.



LLM cannot mutate GameSimulation.



LLM cannot choose trusted player identity.



LLM cannot grant itself PlayerDirect provenance.



LLM cannot automatically approve strategic requests.



LLM cannot directly create CommanderGoal.



LLM cannot directly mutate StrategicPlan.



LLM cannot directly reserve or release gameplay resources.



LLM cannot select concrete workers as authority.



LLM cannot select arbitrary entity IDs as authority.



LLM cannot select arbitrary world coordinates as authority.



LLM cannot access hidden enemy information.



LLM cannot replace/cancel strategies autonomously.



Provider output is always untrusted.



Human commands outrank Commander worker control.



Gameplay-authoritative decisions must remain deterministic.



Wall-clock time must not control gameplay decisions.



Late provider responses must fail closed after reset.



Voice must never get a separate gameplay authority path.

```



These safety requirements are more important than feature count.



---



# 4. Current state — Phase 4E



## Phase 4E title



```text

Natural-Language Goal Understanding & Coordinated Execution

```



Phase 4E substantially expanded the Commander from individual interpreted commands toward natural desired-state requests.



Important implemented/verified concepts include:



```text

natural paraphrase understanding



bounded semantic requests



compound requests



semantic building placement



contextual references



conversation follow-ups



Castle Age desired-state execution



concurrent prerequisite preparation



human worker override protection



provider reset/generation safety



hostile semantic validation



standalone Windows build

```



---



# 5. Current Phase 4E automated evidence



Reported final Unity results:



```text

EditMode:

879 / 879 passed

0 failed

0 skipped



PlayMode:

183 / 183 passed

0 failed

0 skipped

```



Total:



```text

1062 / 1062

```



Focused runtime evidence includes:



```text

Natural ten-Spearman request:

10 / 10 living Spearmen

tick 5101



Compound request:

Barracks #3 at (120,128)

five clear map-west tiles

specific producer binding

10 living Spearmen

tick 4291



Castle Age:

real Age 2 + Age 3 progression

Age 3 reached at tick 27811



Human authority:

manual worker command released Commander ownership

manual movement survived strategic cancellation



Reset / hostile:

new runtime:

pending=0

memory=0

plans=0

commands=0



Hostile semantic validation:

passed

```



Standalone Windows build succeeded:



```text

Builds/Phase4E/OpenEmpires-Phase4E.exe

```



---



# 6. Historical Phase 4E verdict (Accepted baseline)



Historical Phase 4E was accepted on 2026-10-04 (879/879 EditMode, 183/183 PlayMode):



```text

READY FOR PHASE 4F (ACCEPTED / FROZEN)

```



Important:



This does **not currently mean a known production-code defect exists**.



The reason is that the standalone acceptance record is incomplete.



The remaining Phase 4E acceptance work is:



```text

1. Fresh standalone compound request:

   verify final 10 Spearmen

   AND verify identity of the newly created Barracks used as producer.



2. During standalone Commander work:

   issue manual worker command

   verify human authority survives

   verify cancellation does not destroy the human command.



3. Standalone conversation:

   "make 5 archers"

   then

   "make five more"



   Verify final living Archer result.



4. Correlate the latest standalone session with Player.log

   and inspect for fatal/unhandled Phase 4E errors.

```



AntiGravity should perform this independent verification before Phase 4F begins.



If these checks pass with no unresolved Critical/Important issues:



```text

READY FOR PHASE 4F

```



If a real defect appears:



```text

Phase 4E Fix

      ↓

regression

      ↓

AntiGravity re-audit

```



Do **not** start 4F until 4E is formally frozen.



---



# 7. Revised remaining roadmap



The current planned order is:



```text

Phase 4E

Natural-language goal understanding

ACCEPTED BASELINE / frozen for Phase 4F

        ↓

Phase 4F

AI Data Foundation & Future-Proof Content Interface

ACCEPTED / FROZEN (READY FOR PHASE 4G)

        ↓

Phase 4G

Complete Commander Gameplay Capability

NEXT / READY TO BEGIN

        ↓

Phase 4H

Voice / Speech-to-Text

        ↓

Release Candidate / Finalization

```



---



# 8. Phase 4F — AI Data Foundation & Future-Proof Content Interface



## Core goal



Phase 4F should make the Commander as **data-driven and low-maintenance as practical**.



When game content changes, the Commander should automatically discover those changes whenever the underlying mechanic is already supported.



Examples:



```text

unit cost changes

building cost changes

new unit using existing mechanics

new building using existing mechanics

new civilization

new technology

changed prerequisites

changed age requirements

changed combat bonuses

changed civilization availability

```



These should not require manually teaching the LLM new facts whenever the existing game data already contains them.



---



# 9. Critical Phase 4F rule — do not redesign existing gameplay data



The purpose of 4F is NOT to rewrite OpenEmpires' existing data architecture.



Existing game data continues to control gameplay exactly as it does now.



If an existing data structure is already suitable:



```text

USE IT.

```



Do not duplicate or replace it unnecessarily.



If existing data is difficult or unsafe for AI use:



```text

existing authoritative game data

          ↓

AI adapter / projection

          ↓

detached AI-friendly representation

```



The goal is NOT:



```text

GAME DATA



and separately



MANUALLY MAINTAINED AI DATA

```



That would create synchronization problems.



The desired rule is:



```text

GAME DATA

=

SOURCE OF TRUTH



AI DATA

=

READ-ONLY PROJECTION / ADAPTER

```



---



# 10. Example Phase 4F architecture



Conceptually:



```text

                 EXISTING OPENEMPIRES DATA

                           │

          ┌────────────────┼────────────────┐

          │                │                │

        Units          Buildings      Technologies

          │                │                │

          ├──────── Civilizations ──────────┤

          │                │                │

          └──────── Gameplay Rules ─────────┘

                           │

                           ▼

                 AI Knowledge Adapter

                           │

                           ▼

                Detached Knowledge Data

                           │

             ┌─────────────┼──────────────┐

             │             │              │

             ▼             ▼              ▼

         LLM context     Commander     Information Q&A

                         planning

```



No live Unity objects should escape through this layer.



---



# 11. Phase 4F data categories



Investigate and expose useful canonical information for at least:



## Units



```text

stable type/id

display name

civilization availability

cost

train time

production building

age requirement

prerequisites

population cost

HP

armor

attack

range

movement

combat classes/tags

bonus damage

relevant upgrades

```



## Buildings



```text

stable type/id

display name

cost

construction time

footprint

age requirement

prerequisites

units produced

technologies provided

civilization availability

important gameplay tags

```



## Technologies



```text

stable type/id

cost

research location

prerequisites

age

effects

civilization availability

```



## Ages



```text

age identity

progression requirements

required structures

resource requirements

actual age-up mechanism

```



## Civilizations



```text

available units

available buildings

available technologies

replacements

bonuses

modifiers

restrictions

```



## Resources



```text

resource type

valid gathering sources

relevant costs

gathering relationships

```



---



# 12. Effective vs base game data



Phase 4F should distinguish:



```text

Base Game Data

```



from:



```text

Effective Player Data

```



Example:



```text

Knight base cost:

100 Food

75 Gold



Player civilization bonus:

-15% Gold cost

```



A player asking:



```text

"How much does a Knight cost?"

```



should ideally receive the cost applicable to their actual civilization/state.



The effective-data layer may incorporate:



```text

civilization

age

technologies

upgrades

modifiers

game mode

```



when those are legitimate and supported.



---



# 13. GameKnowledgeCatalog vs CommanderCapabilityCatalog



This distinction should probably become explicit.



## GameKnowledgeCatalog



Answers:



```text

What exists?



How does it work?



What does it cost?



What are its prerequisites?



What counters it?



Which civilization has it?

```



## CommanderCapabilityCatalog



Answers:



```text

Can the Commander currently execute this?

```



These are different.



Example:



A new siege unit may exist in game data.



The knowledge system should be able to answer:



```text

"What does it cost?"

```



But if Commander siege execution is not implemented:



```text

"Build five of them"

```



must not silently become executable.



Therefore:



```text

EXISTS IN GAME

!=

COMMANDER CAN CONTROL IT

```



---



# 14. Phase 4F game-information questions



Phase 4F should lay the data foundation for questions such as:



```text

"What counters Spearmen?"



"How much does a Knight cost?"



"What does this technology do?"



"What do I need to make Archers?"



"Can my civilization build Knights?"



"What age do I need for this unit?"



"How do I age up?"



"What resources do I need for Castle Age?"

```



The facts must come from game data.



Do not hardcode them into the LLM system prompt.



---



# 15. Information request vs action request



A key natural-language distinction:



```text

"How do I reach Castle Age?"

```



means:



```text

QUESTION

```



while:



```text

"Take me to Castle Age."

```



means:



```text

ACTION

```



They should share authoritative underlying data but follow different execution paths.



Conceptually:



```text

                Castle Age definition

                         │

             ┌───────────┴───────────┐

             │                       │

             ▼                       ▼

       Knowledge query          ReachAgeGoal

             │                       │

       explain requirements     satisfy requirements

```



---



# 16. Phase 4F maintenance target



After 4F:



### Simple data change



Example:



```text

Spearman Food:

60 → 70

```



Desired Commander maintenance:



```text

NONE

```



The AI projection should automatically reflect the new value.



### New content using existing mechanics



Example:



```text

new infantry unit

```



Desired maintenance:



```text

mostly / entirely data registration

```



### Entirely new gameplay mechanic



Example:



```text

teleport tunnel network

```



Expected maintenance:



```text

new deterministic Commander execution support may be required

```



The goal is not magical understanding of arbitrary future code.



The goal is:



> Anything expressed through standard OpenEmpires game definitions should become discoverable without separately teaching the LLM.



---



# 17. Phase 4F documentation requirement



Required major artifact:



```text

Docs/Commander/guideline.md

```



This should explain exactly how future developers add content while preserving Commander compatibility.



It should include sections such as:



```text

Adding a unit



Adding a building



Adding a technology



Adding a civilization



Adding a resource relationship



Adding an age



Changing a cost



Changing prerequisites



Adding content using existing mechanics



Adding an entirely new mechanic



Exposing new knowledge to the Commander



Making a capability executable



What must never be hardcoded into AI prompts

```



Example rule:



```text

ADDING A NORMAL UNIT



DO:

- define canonical OpenEmpires unit data

- define cost

- define production source

- define prerequisites

- define age

- define civilization availability

- define combat classification



DO NOT:

- add the unit name manually to the LLM prompt

- duplicate the cost in Commander code

- make a phrase-specific parser rule

- manually maintain a second AI database

```



---



# 18. Phase 4F acceptance philosophy



Phase 4F should end with proof that changing canonical game data changes AI-visible knowledge **without modifying Commander/LLM prompt code**.



Suggested tests:



```text

change test fixture unit cost

→ AI data projection updates



add test unit using known mechanics

→ knowledge catalog discovers it



change civ availability

→ effective player knowledge updates



change prerequisite

→ AI-visible prerequisite updates



unsupported execution capability

→ knowledge exists

→ execution remains rejected safely

```



Phase 4F must not change current gameplay behavior merely to support AI data.



---



# 19. Phase 4G — Complete Commander Gameplay Capability



## Core goal



Phase 4G should identify and implement the major remaining things a player reasonably expects an RTS Commander to do.



The objective is not to create hundreds of phrase-specific commands.



Instead:



> Build generic deterministic gameplay capability families and let natural language compose them.



This is where remaining requests like:



```text

"build a mill near the berries my villagers are working"



"move my scout around the enemy base for vision"

```



belong.



---



# 20. Phase 4G capability audit



Before implementation, inspect what current 4E/4F code already supports.



Create a matrix covering:



```text

economy

construction

production

technology

age progression

movement

rally

scouting

vision

attack

defend

patrol

retreat

repair

resource targeting

unit grouping

spatial references

information questions

status/explanations

compound commands

bounded conditional commands

```



Classify each:



```text

fully supported



partially supported



semantic only



execution missing



game mechanic absent

```



Do not assume old handoff information is current.



Inspect live source.



Historically, battlefield move/attack/defend/patrol/rally capabilities were identified as a possible gap.



---



# 21. Phase 4G — economy capabilities



Possible player requests:



```text

"put five more villagers on food"



"move some villagers from wood to gold"



"balance my economy"



"send idle villagers to resources"



"get more villagers on the berries"



"prepare enough resources for ten knights"



"focus more on food"



"stop gathering stone"



"make sure we have enough wood for two barracks"



"prepare the resources for Castle Age"

```



Important:



The LLM describes intent.



Deterministic economy logic chooses workers.



Human worker commands remain higher authority.



---



# 22. Phase 4G — contextual construction



Examples:



```text

"build a mill near the berries my villagers are working"



"build a barracks behind my town center"



"put a stable near the gold"



"build houses around the back of my base"



"make another barracks next to the first one"



"build production near my forward base"



"put a defensive building near this entrance"

```



This should extend the generic:



```text

BuildStructureGoal

+

semantic location selector

+

deterministic placement resolver

```



Do NOT create:



```text

BuildMillNearBerryGoal

BuildBarracksNearTCGoal

BuildStableNearGoldGoal

...

```



unless genuinely necessary.



---



# 23. Phase 4G — production capabilities



Examples:



```text

"make ten spearmen"



"keep producing villagers"



"make five archers and five spearmen"



"make more cavalry"



"train units from these barracks"



"stop making knights"



"make reinforcements"



"produce units until I have 30 military"

```



These should use generic production/desired-state systems where possible.



---



# 24. Phase 4G — movement



Examples:



```text

"move those spearmen to the bridge"



"send my army near the town center"



"move the archers behind the spearmen"



"bring the scout back home"



"gather the army outside their base"



"move damaged units back"

```



Potential generic concept:



```text

MoveUnitsGoal



UnitSelector

+

LocationSelector

+

formation / positioning rule if supported

```



The LLM must not select concrete world coordinates directly.



---



# 25. Phase 4G — scouting and vision



This is a major desired capability.



Examples:



```text

"move my scout around the enemy base for vision"



"explore the north side"



"find another gold deposit"



"scout around their base"



"check whether they expanded"



"keep vision around this area"



"explore the edge of the map"



"look for more berries"

```



Potential generic capability:



```text

ScoutAreaGoal

```



with bounded data such as:



```text

ScoutSelector

TargetAreaSelector

RoutePattern

VisionObjective

CompletionCondition

```



The deterministic system chooses the actual route.



The LLM cannot see unexplored information merely because the player asked for it.



---



# 26. Phase 4G — rally points



Examples:



```text

"rally my spearmen near the gold"



"rally new units behind my town center"



"send new archers to the bridge"



"set the barracks rally point near the army"

```



This is important for the original long-term goal.



Ultimate example:



```text

"I want 15 spearmen and set their rally point

to the nearest gold around my Town Center

that my villagers are working on."

```



Conceptually:



```text

EnsureUnitCount:

    Spearman >= 15



RallySelector:

    Resource = Gold

    Anchor = owned TownCenter

    WorkerCondition = currently worked by player villagers

    Selection = nearest

```



Then deterministic game-side code resolves the actual gold entity/location.



---



# 27. Phase 4G — combat



Potential requests:



```text

"attack those archers"



"attack their army"



"focus their cavalry"



"defend this gold"



"protect these villagers"



"attack their base"



"raid their economy"



"fall back"



"retreat if we're losing"

```



These must not become LLM micromanagement.



Create bounded deterministic tactical goals.



Possible families:



```text

AttackTargetGoal



DefendAreaGoal



EscortGoal



RetreatGoal

```



Target selection must be based only on legitimately visible/known game state.



---



# 28. Phase 4G — patrol



Examples:



```text

"patrol between these two locations"



"patrol around my base"



"keep these units around the gold"



"watch the northern entrance"

```



Use normal deterministic movement/patrol commands if the game supports them.



---



# 29. Phase 4G — repair/support



If OpenEmpires mechanics support them:



```text

"repair my town center"



"repair damaged buildings"



"keep the wall repaired"



"send villagers to repair this building"

```



Again:



```text

semantic selection

→ deterministic worker selection

→ normal repair command

```



---



# 30. Phase 4G — technology



Examples:



```text

"research the archer upgrade"



"get the next infantry upgrade"



"upgrade my spearmen"



"research technologies that improve cavalry"

```



This should use Phase 4F's authoritative game-data layer.



The LLM should not guess tech prerequisites or costs.



---



# 31. Phase 4G — game-information Q&A



Phase 4G should expose player-facing natural questions using the 4F data layer.



Examples:



```text

"What counters spearmen?"



"What are knights good against?"



"How much does a barracks cost?"



"How do I age up?"



"What do I need for Castle Age?"



"Where do I make archers?"



"Can my civilization build knights?"



"What does this upgrade do?"



"Why can't I build this unit?"



"What is this building for?"

```



Answers should come from canonical/effective game data.



The LLM may phrase the explanation.



It must not invent game mechanics.



---



# 32. Phase 4G — live-state questions



Also useful:



```text

"How many villagers do I have?"



"How many are on wood?"



"How much food do I need?"



"Why haven't you made the spearmen?"



"What are you currently doing?"



"What is blocking the strategy?"



"How many barracks do I have?"



"Which resources am I short on?"



"What is my army made of?"

```



These should use bounded detached snapshots.



No direct provider access to live simulation objects.



---



# 33. Phase 4G — compound commands



Examples:



```text

"build a mill near those berries and move six villagers there"



"make two barracks and use them to produce twenty spearmen"



"send the scout around their base and then bring him home"



"build houses while gathering enough resources for Castle"



"move my army to the bridge and defend it"



"build a stable near my TC and rally knights to the gold"

```



Extend Phase 4E's bounded compound graph rather than introducing an arbitrary scripting language.



Keep limits on:



```text

node count

dependency depth

reference count

cycles

conditional complexity

```



---



# 34. Phase 4G — bounded conditional requests



Potential examples:



```text

"if we're population blocked, build houses"



"if I already have enough spearmen, make archers"



"if this barracks is busy, use another one"

```



Do not introduce arbitrary LLM-authored code or unrestricted scripting.



Only support predefined typed conditions that deterministic code evaluates.



---



# 35. Phase 4G — capability design philosophy



Bad:



```text

ScoutEnemyBase

ScoutNorth

ScoutGold

ScoutMapEdge

ScoutTownCenter

```



Better:



```text

ScoutAreaGoal

+

AreaSelector

+

VisionObjective

```



Bad:



```text

BuildMillNearBerriesGoal

BuildBarracksNearTCGoal

BuildHouseBehindBaseGoal

```



Better:



```text

BuildStructureGoal

+

StructureType

+

LocationSelector

```



Bad:



```text

AttackArcherGoal

AttackCavalryGoal

AttackBaseGoal

```



Better:



```text

AttackTargetGoal

+

TargetSelector

```



Phase 4G should optimize for **generic deterministic capability families**, not phrase-specific features.



---



# 36. Phase 4G acceptance goal



At the end of 4G, a large representative corpus of ordinary RTS Commander requests should work naturally.



The acceptance corpus should cover:



```text

economy

construction

production

age/technology

movement

rally

scouting

vision

attack

defense

repair if supported

information questions

live-state questions

compound requests

follow-ups

ambiguity

unsupported request handling

human override

fog safety

reset/stale safety

```



And include real PlayMode/runtime scenarios, not parser tests alone.



---



# 37. Phase 4H — Voice / Speech-to-Text



Voice comes only after the text system is broad and stable.



The goal is deliberately simple:



```text

MICROPHONE

    ↓

speech-to-text

    ↓

string

    ↓

EXISTING Commander text input pipeline

```



No new gameplay authority.



No new planner.



No VoiceCommander.



No VoiceGoalManager.



No voice-specific game commands.



---



# 38. Phase 4H conceptual interface



Potential abstraction:



```text

ICommanderSpeechToTextProvider

```



Possible implementations:



```text

MockSpeechToTextProvider



real STT provider

```



The actual provider should remain swappable.



---



# 39. Phase 4H UX



Likely features:



```text

push-to-talk



recording indicator



cancel recording



transcription preview



submit transcription



retry transcription



microphone permission handling



STT timeout



STT provider unavailable



network error



input device selection if needed



clear state after match reset

```



Voice should feed the exact same text pipeline as keyboard entry.



---



# 40. Phase 4H required equivalence



If typing:



```text

"build a mill near the berries my villagers are working"

```



works after Phase 4G,



then saying the same sentence should work after Phase 4H because STT produces equivalent text.



Likewise:



```text

"uh hey commander could you get me like ten spearmen?"

```



should simply become text and enter the existing natural-language system.



4H must not duplicate Phase 4E/4G semantics.



---



# 41. After Phase 4H — stop adding feature phases by default



Do not automatically invent:



```text

4I

4J

4K

...

```



After 4H, perform a product-completeness audit.



If no major Commander capability family is missing, switch to a Release Candidate/finalization process.



---



# 42. Finalization — save/load



Save/load was intentionally deferred earlier and remains a required release task.



Review what Commander state genuinely needs persistence.



Possible candidates:



```text

active goals



active strategic plan



plan lifecycle



milestone state



resource reservations



worker reservations where safe



plan IDs / goal IDs



paused state



bounded conversation memory



recent semantic references



Commander settings

```



Never serialize:



```text

HTTP requests



provider tasks



CancellationToken



delegates



live Unity references



provider credentials



stale asynchronous provider replies

```



A loaded save must not allow old provider responses to mutate the new runtime.



---



# 43. Finalization — genuine multiplayer/desync validation



Real multiplayer certification is still required when technically possible.



The intended architecture remains:



```text

authorized player/client Commander reasoning

                ↓

normal deterministic gameplay command

                ↓

existing multiplayer command transport

                ↓

shared simulation

```



Commander reasoning itself should not become a second replicated simulation authority.



Required multiplayer scenarios should eventually include:



```text

worker reassignment



construction



unit production



compound goal



age progression



movement



scouting



attack



plan cancel



plan replacement



pause/resume



provider response arriving late



match reset



conversation follow-up



voice-originated text command

```



Verify simulation checksums/no desync.



Historical plans already treated real two-client multiplayer certification as a deferred finalization task.



---



# 44. Finalization — performance



Profile realistic large/late-game matches.



Focus on:



```text

CommanderContext construction



AI data projection



GameKnowledgeCatalog queries



effective civilization calculations



selector resolution



worker selection



pathfinding



placement search



dependency planning



plan health



conversation memory



semantic reference memory



provider prompt size



JSON parsing



scouting route generation



unit-group selection



goal archives



plan archives

```



No heavy AI context builder should run every frame.



Provider calls should remain request/event-driven.



Historical performance requirements already identified context construction, worker selection, path validation, histories, JSON parsing and provider prompt size as final profiling targets.



---



# 45. Finalization — provider production architecture



Development can use direct providers.



A public release should not ship reusable provider secrets in the Unity client.



Long-term production architecture should be closer to:



```text

OpenEmpires client

      ↓

controlled backend/proxy

      ↓

OpenRouter / other provider

```



The backend does not gain gameplay authority.



Even backend/provider output still passes the same strict game-side validation.



---



# 46. Finalization — credentials/security



Before public release:



```text

rotate development credentials



do not ship provider secrets



rate-limit backend/API usage



enforce request-size limits



bound conversation context



bound semantic graph size



validate every enum



validate every numeric value



reject NaN/infinity



reject unexpected JSON



reject multiple JSON objects



protect against prompt injection



protect reset/generation boundaries



protect player identity



protect fog-of-war data



run final static authority audit

```



Historical project guidance already requires credentials not to be shipped client-side and requires strict validation even behind a trusted backend.



---



# 47. Finalization — UX



After feature work stabilizes, polish:



```text

Commander chat UI



goal/progress display



current task display



plan status



blockers



pause/resume/cancel



clarification UX



question vs command distinction



voice state



recording indicator



provider/STT errors



unsupported capability messaging



conversation history



accessibility



settings



input history



response verbosity



clear strategic approval controls

```



The player should never have to understand internal concepts such as:



```text

DTO

GoalId

PlanRevision

GenerationToken

StrategicCommitmentPolicy

```



---



# 48. Finalization — long-session robustness



Run long matches involving repeated:



```text

natural requests



follow-ups



cancellations



strategic replacements



goal completion



failed goals



scouting



combat



age progression



provider timeout



provider retry



voice requests



reset/reinitialize

```



Check for:



```text

memory growth



goal archive growth



plan archive growth



dangling callbacks



stale provider responses



stale conversation references



worker reservation leaks



resource reservation leaks



duplicate goals



duplicate advisories



disposed object references



performance degradation

```



---



# 49. Final release hostile audit



Before release, AntiGravity or an equivalent independent reviewer should attack the complete Commander.



The audit should include:



```text

authority boundaries



provider compromise



prompt injection



stale/cross-match commands



save/load



multiplayer



fog-of-war



human override



voice path



AI data projection



new civilization compatibility



new unit compatibility



unsupported capability handling



conversation references



compound graphs



long-session lifecycle



performance



credential handling

```



The release should not rely on green tests alone.



---



# 50. Recommended acceptance sequence from current state



Immediate sequence:



```text

CURRENT:

Phase 4E closeout

      ↓

AntiGravity standalone acceptance audit

      ↓

if defect:

    Phase 4E Fix

      ↓

    regression

      ↓

    re-audit

      ↓

if accepted:

    freeze Phase 4E

      ↓

write detailed Phase 4F implementation specification

      ↓

Codex implements Phase 4F

      ↓

AntiGravity audits Phase 4F

      ↓

freeze

      ↓

write detailed Phase 4G implementation specification

      ↓

Codex implements Phase 4G

      ↓

AntiGravity audits Phase 4G

      ↓

freeze

      ↓

write detailed Phase 4H implementation specification

      ↓

Codex implements voice

      ↓

AntiGravity audits Phase 4H

      ↓

freeze

      ↓

Release Candidate / Finalization

```



---



# 51. Important instruction for future chats



If this chat is lost, the next AI should NOT immediately start writing Phase 4F code.



First determine the exact current state.



Ask for / inspect:



```text

latest Phase 4E AntiGravity audit



latest Phase 4E final report



branch



HEAD



git status



current source manifest



current boundary audit



current Unity full-suite totals



current standalone build



known limitations

```



If Phase 4E still says:



```text

REQUIRES FIX PHASE

```



finish Phase 4E first.



Only when the independent verdict says:



```text

READY FOR PHASE 4F

```



should Phase 4F begin.



---



# 52. Rules for writing future phase prompts



Every major phase prompt should contain:



```text

accepted baseline



exact phase objective



explicit out-of-scope items



authority invariants



subphase decomposition



agent delegation rules



one-production-writer rule



one-Unity-runner rule



RED/GREEN requirements



focused test requirements



full regression requirement



real PlayMode scenarios



standalone/build requirement where relevant



security/fog requirements



performance bounds



source manifest requirement



boundary audit requirement



known limitations



final report format



READY / REQUIRES FIX verdict

```



Do not give Codex a vague feature request for a multi-hour phase.



---



# 53. Multi-agent policy



Continue the existing delegation style.



## Root / strongest reasoning agent



Own:



```text

architecture



authority



phase dependency order



integration



review decisions



final source freeze



final verdict

```



## Implementation agents



Use for:



```text

bounded production implementation



tests



planner integration



data adapters



capability implementation

```



Only one overlapping production writer at a time.



## Read-only / cheaper agents



Use for:



```text

source search



requirements matrix



hashes



documentation



test enumeration



static boundary review



evidence extraction



capability inventory

```



Only one agent should own Unity's full test runner at a time.



This delegation pattern is already established in the project's development workflow.



---



# 54. Current long-term architecture



The target architecture after all remaining phases is approximately:



```text

                              PLAYER

                                 │

                 ┌───────────────┴───────────────┐

                 │                               │

               TEXT                            VOICE

                 │                               │

                 │                         Speech-to-Text

                 │                               │

                 └───────────────┬───────────────┘

                                 │

                                 ▼

                         Commander Input

                                 │

                                 ▼

                  Natural-Language Interpreter

                                 │

                                 ▼

                     bounded semantic data

                                 │

                       strict validation

                                 │

           ┌─────────────────────┼──────────────────────┐

           │                     │                      │

           ▼                     ▼                      ▼

   Knowledge Query        Tactical Goal         Strategic Request

           │                     │                      │

           │                     │               Approval Boundary

           │                     │                      │

           │                     │              Decision/Commitment

           │                     │                      │

           │                     │              StrategicPlanner

           │                     │                      │

           └─────────────┬───────┴──────────────┬───────┘

                         │                      │

                         ▼                      ▼

                  contextual/data        strategic lifecycle

                    resolution            / health / recovery

                         │                      │

                         └──────────┬───────────┘

                                    │

                                    ▼

                         CommanderGoalManager

                                    │

                                    ▼

                           CommanderPlanner

                                    │

                                    ▼

                                ICommand

                                    │

                                    ▼

                             CommandBuffer

                                    │

                                    ▼

                            GameSimulation

```



Supporting both knowledge and execution:



```text

Existing OpenEmpires Game Data

             │

             ▼

       AI Data Projection

             │

        ┌────┴─────┐

        ▼          ▼

     Knowledge   Planning

```



---



# 55. Ultimate maintenance target



After 4F/4G, adding ordinary new content should ideally look like:



```text

Developer adds new unit to OpenEmpires

       ↓

uses canonical game definitions

       ↓

AI data layer discovers it

       ↓

Commander can explain it

       ↓

if its mechanics are already supported:

Commander may also control it automatically

```



Not:



```text

add unit to game

+

edit AI prompt

+

edit parser

+

edit cost table

+

edit civilization table

+

add special-case Commander code

```



That is the long-term maintainability goal.



---



# 56. Ultimate player experience



The finished Commander should feel approximately like this:



```text

Player:

"Hey, I want 15 spearmen and rally them

to the nearest gold around my TC

that my villagers are working on."



Language layer:

understands semantic request



Game-side resolver:

finds owned TC



Game-side resolver:

finds known Gold nodes



Game-side resolver:

filters Gold currently worked by owned villagers



Game-side resolver:

chooses nearest deterministically



Planner:

derives Barracks/resource/population requirements



Planner:

works on independent prerequisites concurrently



Simulation:

builds / gathers / trains normally



Rally system:

uses normal deterministic game command



Commander:

reports progress/blockers



Result:

15 living Spearmen

with requested rally behavior

```



And:



```text

Player:

"How do I reach Castle Age?"



Commander:

reads authoritative game data

and explains the requirements.

```



Then:



```text

Player:

"Okay, get me there."



Commander:

converts that into the supported ReachAge desired state

and executes through deterministic systems.

```



Then eventually the same interaction can happen by voice.



---



# 57. Current snapshot in one paragraph



OpenEmpires Commander has progressed from a deterministic Spearman-production prototype into a layered natural-language tactical and strategic system with typed provider boundaries, worker ownership, resource reservations, compound requests, contextual placement, desired-state execution, Castle Age progression, concurrent prerequisite handling, bounded conversation memory, human override protection, strategic approval/provenance, strategic plan controls, health/recovery/advisories, live Luna interpretation, and a standalone Windows build. Phase 4E is the accepted baseline at 879/879 EditMode and 183/183 PlayMode. Phase 4F is formally accepted (READY FOR PHASE 4G) following AntiGravity independent hostile verification with 906/906 EditMode, 183/183 PlayMode, 27/27 focused tests, clean Windows build (build-585b3f5a13), zero-exception standalone engine launch, and strict knowledge-vs-capability separation. Phase 4G (Complete Commander Gameplay Capability) is the active next phase to implement broader RTS Commander capabilities such as contextual construction, scouting, movement, rally, combat, defense, patrol, repair, and information questions.



---



# 58. Central rule



If every other document is lost, preserve this:



> **The player expresses intent.**

>

> **The LLM understands language.**

>

> **Game data defines truth.**

>

> **Deterministic systems resolve and execute.**

>

> **The human player retains authority.**



Do not replace this architecture with:



```text

LLM → direct game control

```



even if doing so appears easier for a future feature.

