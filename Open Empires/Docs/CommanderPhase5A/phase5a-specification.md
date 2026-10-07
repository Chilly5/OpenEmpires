# Phase 5A: player-authorized dynamic composition

Status: implementation specification, not acceptance. 2026-10-06.

## Binding brief and source baseline

The complete 21-section user brief in attachment `47d8363c-b152-4f3c-bb9b-a0f91ffa8ca0/pasted-text-1.txt` is binding. This specification organizes it; it does not narrow it. The historical investigation remains unchanged.

Working root: `D:/unity_projects/OpenEmpires/Open Empires`; branch `unit_models_and_voice_control`; HEAD `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca`. Initial dirty paths: `remaining_work.md` and untracked `Docs/CommanderPhase5A/`. Unity MCP confirms this root and Unity 6000.5.9f1. No commit, push, reset, clean, branch switch, credential disclosure, or older-source replacement is authorized.

## Intended outcome

A human Commander may interpret player input and execute its authorized effects and legitimate prerequisites. Background strategy recommendations, emergency priority, provider prose and provenance labels never create human consent. Explicit computer control retains existing supported autonomous execution. Dynamic composition extends existing Commander goals; it does not introduce a gameplay engine or executable language.

## A: strategic commitment authority

Use trusted simulation setup/controller ownership, not local player number, host status, UI presence, or provider fields. Unknown owners fail closed. At `StrategicPlanner.SubmitIntent`, require an actual game-owned direct-request/approved-candidate credential or explicitly configured computer ownership. Check before templates, registration, reservations and conflicting-plan mutation. Record immutable authority evidence on committed roots. Recheck it before milestone work; unauthenticated human roots cannot acquire continuation authority simply by relabeling. Stop/release unauthorized work through the existing lifecycle without overriding human commands.

Background `StrategicPipeline` still evaluates and publishes bounded history/advisories, but cannot commit human strategies. Keep authorized plan ticks, health and milestones. Emergency only changes urgency within authorized scope, never approval or replacement authority. Preserve stronger adaptation confirmation and current stale/replayed/foreign control checks. Use truthful suggestion wording.

## B: requests, effects and confirmation

Game-owned immutable request identity binds owner/principal, manager/simulation generation, original bounded input, cancellation state, normalized root effects/constraints and candidate identity/relevant strategic revision. Provider metadata stays separate and untrusted. Trace root/child/prerequisite/continuation attribution into goals and dispatch diagnostics without network provenance bytes.

Simple valid KnownIntent fast paths remain automatic under established admission. Effectful synthesized DynamicPlans and independently ungrounded multi-effect existing graphs require one normalized plan preview and single-use confirmation. Provider-written roots/spans never qualify as independent grounding. Preview is side-effect-free and shows every effectful root, quantities, selectors, producer/result restrictions, negative constraints and prerequisite policy. Recheck exact immutable candidate/runtime/owner/revision at commit. Changed candidates need new approval. Pending previews create zero work/reservations and clear on cancellation, supersession or reset.

Enforce shared root constraints before every derived prerequisite and dispatch. Production alone does not authorize attack, scouting, arbitrary reassignment, unrelated content or age targets. No-build means use eligible existing capacity or block; canonical prerequisites cannot override a negative constraint. Preserve accepted count modes, worker protection, source kind, exact producers, spatial semantics and exact results. Legitimate existing concurrent preparation/continuing explicit age goals survive unrelated questions.

## C: bounded typed composition

Reuse Phase 4G graph/result machinery and ordinary planners. An explicit primitive registry describes stable mechanic IDs, strict parameters, typed inputs/results, effect class, bounds and compiler mapping. Required mechanics: selection of workers/units/structures/resources, deterministic bounded partition, semantic location, construction (including canonical Farms), allocation, production constrained to exact producer sets, dependencies and typed produced results. Advertise additional existing actions only through implemented adapters.

Canonical/effective-player knowledge remains authoritative; discover actual stable IDs from source. No AI content/cost database or manual provider content whitelist. Farm support must use real footprint, placement, cost/availability and lifecycle rules. Worked berries must use actual owned gather relationships, not proximity or hidden knowledge. Shortages wait/block rather than become unsupported content.

Versioned graph limits: at most 12 nodes, dependency depth at most 5, at most 4 references per node; conditions are optional and must not be advertised unless implemented (at most 4). Depth counts dependency edges from a root (root depth 0). Reference count includes dependency references and distinct typed input/result reference fields, counting each field occurrence. Retain stricter compatible existing bounds. Aggregate requested entities/collections and repeated structures are bounded to 200 per candidate, further constrained by existing population/content rules. No loops, recursion, arbitrary expressions, predicates, methods, runtime IDs, coordinates, hidden selectors or authority fields. Parser/text/dependency/prerequisite/per-tick bounds must be explicit in the final schema, validated before any work.

Distinguish WorkerSet, UnitSet, StructureSet/StructureResult, ResourceSet, LocationIntent and produced UnitResult. Reject missing/self/cyclic/cross-graph/wrong-type references or forward references without a valid dependency. Select three workers once, partition two/one disjointly, and preflight the entire role assignment. Build two Barracks into distinct valid placements and bind production to both newly completed structures, never unrelated existing producers. New-unit follow-ups preserve exact production attribution, simulation/manager identity, immutable results and sticky human override.

Validate all nodes/effects before compilation and preview. Compilation is side-effect-free with explicit trusted adapters, no reflection dispatch/code generation. Commit revalidates availability/ownership/freshness/reservations and admits initial work all-or-none; future resource presence is not a static prerequisite. Dispatch rechecks real targets/results/control. Cancellation/reset/failure releases reservations. Completed gameplay is not rolled back; partial progress is reported without invented replacement objectives.

## Provider and UI

Outcomes: KnownIntent (existing Execute equivalent), DynamicPlan, Clarify, Question, Unsupported. Keep configured OpenRouter/Luna and provider abstraction. Advertise actual registry vocabulary and bounded canonical slices. Correct contradictory strategy prompt language: semantic proposals are permitted, execution/approval/unrequested strategies are not. Questions about an age differ from an explicit age-up request.

Measure graph response sizes and provide bounded configurable output/request limits; detect truncation. Per submission, at most one initial provider call plus one eligible schema-only repair, fully revalidated. Never repair/fallback after authority/injection/hidden-ID/coordinate rejection or broaden effects; altered candidates lose approval. Keep one clarification draft, three turns, local count filling and explicit-field preservation. Keep voice on the shared text path. Preserve economy source restriction and pre-existing restricted command encoding; no new network redesign.

## D: evidence and handoff

Use approximately 15–25 focused EditMode tests and 5–7 PlayMode scenarios, grouping hostile cases without omitting critical boundaries. Show old behavior RED before authority repair, with explicit computer control coverage retained. Test actual qualifying bootstrap/no-input and later/emergency routes, conflicting live plans, direct/approved/continuation parity, request effect attacks, negative constraints, unsupported compounds, whole-graph rejection, bounds/reference/type attacks, takeover and stale/reset/repair limits. Record attributable deltas rather than requiring an already-running simulation to stop.

Real configured Luna plus normal UI admission/confirmation and normal commands must demonstrate four Farms; Mill near berries; shared three-idle partition two Sheep/one Mill; two new Barracks producing ten Spearmen; simple fast-path parity; nonexecuting question/suggestion. Record input/state/provider response (bounded redacted)/validation/preview/approval/compiled effects/actual results for every attempt. Parser/mock success is not live gameplay evidence. Tool/credential/content blockers must be specific and accompanied by reproducible fixtures.

Create every named artifact in brief section 20, update guideline and prepend accurate remaining work, retain historical evidence and pending economy/result-binding/standalone/multiplayer gates. Manifest identifies baseline HEAD and actual dirty tested bytes, changed/protected boundaries and test artifacts. Opt-in bounded request-correlated diagnostics cannot log secrets/hidden state or grant authority. AntiGravity owns exhaustive final suites, current Windows standalone/Player.log, native UI/voice, multiplayer/determinism and hostile audit. No full regression, standalone builds, Phase 5B or release process here.

Final verdict is exactly READY FOR ANTIGRAVITY AUDIT only after required implementation and focused/live evidence; otherwise REQUIRES IMPLEMENTATION FIX with accurate evidence gaps. READY is not release acceptance.
