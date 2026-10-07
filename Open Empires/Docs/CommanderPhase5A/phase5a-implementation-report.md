# Phase 5A implementation handoff

Implementation and focused evidence are complete for independent audit; exhaustive release acceptance is not claimed. The final manifest and consistency check establish the delivered bytes. Historical ledger entries remain historical.

## Source and workflow

Unity root: `D:/unity_projects/OpenEmpires/Open Empires`; Git root: `D:/unity_projects/OpenEmpires`; branch: `unit_models_and_voice_control`; HEAD: `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca` (`Add result binding and voice input`). Unity MCP instance: `Open Empires@6d7310c7`, Unity `6000.5.9f1`. Delivery is a dirty, uncommitted working tree, not that historical commit alone. No reset, clean, branch switch, commit, push, credential replacement, model switch, network redesign, Computer Use or Phase 5B was performed.

The complete attachment `47d8363c-b152-4f3c-bb9b-a0f91ffa8ca0/pasted-text-1.txt` remains the binding brief. Root owns production integration; cheaper read-only inventories/documentation and targeted Sol reviews used disjoint ownership. Unity MCP orchestrator, systematic debugging, test-driven development and verification-before-completion guidance governed connection/compile checks, reproduced RED/GREEN fixes and evidence-backed closeout; the user's focused-only policy overrides generic full-suite guidance. One Unity runner was used; no Assets writes occurred during a live test job. Focused tests exceeded the suggested approximate case budget because grouped boundaries and reproduced review defects needed verification; they were not full regression jobs. Exact runs, failures and launcher anomalies are retained in `execution-progress.md` and XML artifacts.

## Delivered behavior

### A. Strategic authority

`StrategicPlanner.SubmitIntent` checks an exact planner/runtime-owned direct-request or consumed approval credential before registration, reservations, replacement or cancellation. Provider source labels, priority, emergency and non-null intents are not consent. Computer autonomy is allowed only for an in-range owner with an actual simulation-created AI controller (`GetAiPlayer`); player zero, local/host status, missing UI, remote humans and unknown slots do not grant it. The separate AIPlayerSystem was not redesigned.

Background and emergency pipeline evaluation remains useful but human recommendations are advisory: **Suggested strategy — not started**. Existing authorized roots retain milestone progression and stronger strategic adaptation confirmation; continuation rechecks the live root credential. The actual qualifying bootstrap/no-input route and later emergency have scoped PlayMode proof. This does not reconstruct the user's precise incident or claim that every ordinary opening snapshot immediately satisfies strategic thresholds.

### B. Requests, consent and fidelity

A locally minted ticket precedes chat interpretation and binds original input, owner, runtime/generation/current request and one candidate. It alone grants no authority. An immutable action candidate contains all normalized effect roots, quantities, selectors, source/producer/location/result restrictions, shared constraints and strategic revision. Synthesized DynamicPlans and ungrounded multi-effect legacy graphs require one compact local preview and single-use confirmation; pending preview creates no goals, reservations or commands. Changed, replayed, foreign, reset or stale candidates cannot commit. Provider-authored roots or spans do not qualify as independently typed player input.

The established single KnownIntent path remains automatic, with a game-owned scope attached before first publication. Independently trusted typed roots may avoid another confirmation only on exact type/effect/constraint/binding equality. Strategic proposals retain their separate approval bridge. Questions create no attributable gameplay work and do not cancel accepted goals. Confirmed-plan cancellation releases every linked live root's reservations; completed or issued commands are not rolled back. Multiple active confirmed plans require disambiguation.

Shared constraints support at most four unique entries from five types: NoConstruction, PreferredWorkers/IdleOnly, MaximumQueue (1..8), ProtectedResource and ResourceSource. Negative constraints cover new and resumed construction. Protection and source restrictions reach trusted implicit preparation, not just direct effects. A private one-worker economy view inherits the parent identity and uses existing source-restricted Gather/Slaughter commands; Sheep cannot silently become berries. Sticky human takeover prevents reclaim after the ordinary protection lease expires. Cached opposing-resource floors now rise on every retry, preserving Gold workers added after an initial Food blocker.

Only one ResourceSource constraint is representable per candidate; independent source restrictions for several resources are unsupported and must not be dropped. This is a bounded representation limit, not permission to relax a request. Structural scope and explicit confirmation are not deterministic English entailment: narrow KnownIntent mistranslation remains possible. Trusted legacy direct game APIs retain their existing credentials and do not all create chat tickets. Opt-in request traces carry bounded stage/owner/generation/evidence/root/goal/dispatch metadata, not original text, credentials or hidden state.

### C. Dynamic composition and canonical mechanics

The strict immutable version-1 DSL has nine registered mechanics: select-workers, partition-workers, select-units, select-structures, select-resources, resolve-location, build, allocate-workers and produce. Only Build/AllocateWorkers/Produce lower to effect goals; selection/location remain typed game-side binding. No generic code, reflection, execution command, authority grant, loops, expressions, conditions, runtime IDs, coordinates or hidden selectors are accepted. UnitSet is currently preflight-only: there is no new DynamicPlan patrol/scout/combat consumer. Preserved KnownIntent graph result follow-ups remain available.

Bounds: 12 nodes; dependency-edge depth 5 with root depth 0; four references per node counting each dependency and input occurrence; aggregate declared entities 200 excluding partition views; construction 20; unit selection 50; ID 32 ASCII characters; canonical IDs 64; enum/mechanic strings 32; semantic text 32,768 characters/JSON depth 12. Legacy Request/Answer retain 8,192 characters/depth 8. Simulation/population/capability limits additionally apply. Conditions are absent, not advertised. Parser/compiler reject unknown/duplicate fields, numeric enums, invalid versions/content/types/references/cycles/overlap/bounds and invalid later nodes before any valid prefix executes.

Compilation and normalized preview are side-effect-free. Commit freezes eligible shared workers once, partitions by stable IDs, preflights combined protected-resource losses and reserves the complete role set atomically with rollback. Runtime conservatively rejects overlapping effect worker roles, including sequential reuse not yet supported by this binder. Loss/takeover does not substitute unrelated workers. Owned/visible/worked resources and exact existing/future structure anchors are bound game-side and rechecked at dispatch. Map-west/east semantics and footprint-to-footprint separation are retained; Near supports one clear tile, not arbitrary distances. Repeated buildings receive distinct legal placements.

Farm is admitted through the real native construction adapter: footprint, border-zero Farm tile rule, occupancy, cost, worker construction and linked food-node lifecycle. No invented Mill influence gate or second content database was added. Mill near worked berries uses actual owned gathering relations and visible canonical berries, not proximity guesses. Canonical cost mutation proves a fresh detached projection and ordinary placement both use changed existing gameplay values while the old snapshot stays detached.

Canonical unit/building IDs come from current gameplay knowledge and actual civilization availability. Effective replacement units such as HRE `unit:12` map to an existing supported base training request through `GameSimulation.ResolveCivUnitType`, not a duplicate replacement table. The provider slice uses the same adapter mapping. A replaced/unavailable base ID rejects. Knowledge discovery never grants a missing mechanic or unsupported training adapter.

Exact build results contain only this root's attributed native placement IDs. Producer collections wait for the complete new compatible set and use only those structures, choosing queue length then stable ID. Exact unit results use accepted queue receipts and actual native spawn observation, not total owned units, baseline subtraction alone, equal-looking human orders or old queues. Lost/cancelled/taken-over results do not silently authorize replacement. An ambiguous relay batch may still execute ordinary gameplay, but loss of attribution is sticky and blocks additional exact production rather than treating it as proof of rejection.

### Provider/UI

Configured OpenRouter/Luna and the Gemini/provider abstraction remain intact. Registry-derived vocabulary and bounded canonical slices replace contradictory/duplicate provider content lists. Existing schema wire names Request and Answer correspond to KnownIntent and Question; DynamicPlan, Clarify and Unsupported are strict outcomes. Voice remains speech-to-text into the same text submission path; count clarification fills one pending draft with at most three continuation turns and does not approve strategies.

Semantic output defaults to 4,096 tokens, configurable only within 1,024..4,096; HTTP body is bounded to 65,536 characters. Legacy output remains 256 tokens/8,192 characters. Truncation fails closed. Each submission allows one initial provider call and only one eligible numeric-string shape repair whose exact normalized template already passes full strict validation. Repaired JSON must match it ignoring property order. No semantic broadening, authority/injection repair, fallback synthesis or third call exists. Credentials were consumed only through existing runtime provisioning, not printed or copied to evidence.

## Scoped real-provider/native results

The fixture source is `Assets/Tests/PlayMode/CommanderPhase5ALiveRuntimePlayModeTests.cs`. It uses real configured Luna, normal UI submission/confirmation and ordinary gameplay; it does not force training/construction completion. Fixture resource/population/terrain setup is explicit legitimate initial state, not live-match acceptance.

| Input / scenario | Passing evidence | Actual result |
|---|---|---|
| make 4 farms | live-farms-green-9ecf1cce.xml | Known Farm4; four native Farms IDs3..6, distinct footprints, 300 Wood and linked food nodes; no needless dynamic confirmation. |
| make a Mill near worked visible berries | passing case in live-five-attempts-1d933713.xml | Known Mill1 with WorkedResource/Near; native completed Mill ID3 at legal nearby placement. |
| three idle villagers: two Sheep, third Mill near berries | passing case in live-three-attempts-c7cadee4.xml | Dynamic preview/confirmation; exact workers0/1 Sheep, worker2 Mill, disjoint ordinary commands and native Mill completion. |
| two new Barracks near Town Center, ten new Spearmen from them | live-new-producers-green-901016eb.xml | Dynamic preview/confirmation; native Barracks3/4; Spearmen8..17; ten accepted Train orders, five from each exact new producer. |
| simple four-idle-villager berry allocation | passing case in live-three-attempts-c7cadee4.xml | Immediate single KnownIntent; workers0..3, one ordinary Gather, no extra strategy/preview. |
| Castle Age informational question | passing case in live-five-attempts-1d933713.xml | Real Answer; zero attributable goals/commands/age advancement. |

All six have scoped passing evidence (1+2+2+1 passing cases across four artifacts). Mixed-run failed cases remain failures, not passes. Thirteen semantic interpretations were submitted across all attempts; no provider billing total or reliability rate is claimed. Early Farm→Mill/provider/parser mismatches and deferred-enumerable fixture assertions were diagnosed, corrected and retained. No further paid calls were made for closeout. These live artifacts precede the final localized preparation/canonical/trace fixes; final local affected checks verify those deltas, not a new paid rerun of every scenario on final bytes.

## Focused verification and review

The ledger identifies each run separately; overlapping totals must not be summed into a unique full-suite total. Highlights: authority RED/GREEN8 and affected29; review/observer43; provider/UI/grounded affected79; compiler/ledger/production22; location/worker/producer10; shared-constraint/parser16; lifecycle/ledger/native observation32; source/DTO/takeover28; preservation RED1→GREEN29. Exact final focused results are appended below after terminal verification and are indexed by the final manifest.

The initial preservation fixture at tick15 did not reach the blocked retry and passed; corrected tick180 produced the actual RED reclaim. Canonical base-ID rejection was expected, but the actual replacement-ID rejection exposed a separate capability projection issue and was fixed through the existing resolver. Narrow read-only Sol reviews plus a read-only requirements/evidence map are not the independent hostile audit. The root independently checked source, terminal counts, full XML summaries and live passing-case counts rather than relying on agent prose alone.

## Protected boundaries and changed files

The manifest enumerates actual changed/source/protected file hashes, not just HEAD. Commander context/parser/compiler/UI/goals/planners and strategic authority were extended; focused test fixtures were added/adjusted while preserving explicit computer-control coverage. Core/Buildings changes are local observation plumbing: training queue integer/timer behavior retained behind receipt-aware collection; native acceptance/spawn/placement events; trusted original-command correlation through bootstrap relay/replay; root-origin cleanup/loss notification. No training costs, combat values, build timings or gameplay wire bytes were added for Phase 5A provenance.

CommandSerializer, Gather/Slaughter restricted command encoding and CommandBuffer are protected and unchanged by this phase. This does **not** imply legacy multiplayer compatibility: pre-existing source-restricted economy versioned packets already require updated peers. Local ambiguity/deterministic correlation fixtures are not complete peer/network proof. Explain and revalidate Core/Buildings observation changes before integration; do not replace them with arbitrary newly spawned entity matching.

## AntiGravity ownership and stopping boundary

Run the comprehensive `antigravity-test-plan.md`: full final EditMode/PlayMode, fresh current Windows standalone build and Player.log, actual no-input/emergency/bootstrap and native UI confirmation, hostile graph/authority/repair/reset/late-response tests, exact results under concurrent queues and human takeover, native voice-to-text, all civilization/cost/modifier/source/placement cases and multiplayer owner/determinism/peer-version checks where available. Carry forward historical pending economy/result-binding/standalone/multiplayer acceptance; focused refreshes do not erase those gates.

No full regression, standalone build, exhaustive hostile audit, Computer Use/native UI certification or complete multiplayer certification was run here. Small localized audit fixes may be made by AntiGravity; architecture changes require an explicit new fix phase. Do not begin Phase 5B automatically. See known-limitations.md for supported representation limits and remaining language uncertainty. READY means implementation handoff, not release acceptance.

## Final verification

- Final focused EditMode job `866e87778be2455a9cd8c3eafd178611`: 48/48 passed, zero failures/skips, 12.4432531 seconds. Runtime23 + grounded8 + compiler3 + provider14. Retained `final-focused-edit-green-866e8777.xml`.
- Final-source PlayMode job `3284c57a86274bb28a28a5c4837423be`: 5/5 passed, zero failures/skips, 3.4196547 seconds. Qualifying bootstrap/later emergency authority, exact build/produce/patrol Spearmen, and three voice/economy/clarification cases. Retained `final-source-play-green-3284c57a.xml`.
- Unity console error check returned zero entries after both terminal runs. These are focused jobs, not full suites or a standalone build.
- Fresh narrow source reviews cleared the cached preservation fix, canonical adapter/cost/result mapping, metadata-only traces and canonical preview. Read-only requirements/evidence audit found no remaining required implementation/focused-evidence blocker; exhaustive acceptance coverage remains open.
- All fourteen required documents, guideline and root roadmap are delivered. `phase5a-source-manifest.json` indexes dirty source/protected boundaries/document and XML hashes; `refresh-source-manifest.ps1 -VerifyOnly` verifies those exact bytes. The generated manifest excludes its own recursive hash and does not read credential contents.

READY FOR ANTIGRAVITY AUDIT
