# Commander Phase 4C design

Date: 2026-09-15. Authority: the user brief in attachment `691a2ccc-7545-452f-934d-6f5278f532ce/pasted-text-1.txt`.

## Scope and execution policy

Implement four gated sub-phases: 4C.1 memory, 4C.2 explanations, 4C.3 richer context, then 4C.4 supported objective expansion. Each sub-phase must pass focused tests, the entire existing EditMode/PlayMode suites (including all Phase 3/4A/4B tests), static safety checks, and scoped review before its successor begins. Final completion additionally requires runtime integration evidence and `Docs/CommanderPhase4C.md`, sections A-G, with an evidence-backed verdict.

The user requested continuous execution without routine approval pauses and explicit model routing. Record architecture choices before code, then proceed; stop only for necessary user action. Work in the existing Unity checkout on `unit_models_and_voice_control`, preserving inherited changes. Do not commit, merge, push, switch branches, modify credentials, or introduce packages. Use phase-specific before/after snapshots rather than HEAD as the change baseline: prior Commander implementation is partly untracked.

## Chosen architecture and alternatives

Choose application-owned advisory services, with copied value snapshots at every boundary. A raw conversation-history-only extension is simpler but cannot reliably distinguish a preference, a preview, and a strategy actually accepted by the game. A persistent/embedding-backed memory service is unnecessary and expressly outside scope. The chosen typed bounded store supports those distinctions without acquiring authority.

The only strategic execution path remains:

`Provider -> validated DTO -> StrategicApprovalLayer -> StrategicDecisionPolicy -> StrategicPlanner -> existing RTS execution`.

Conversation, memory, and explanation routes never call execution. A new recommendation still requires a separate explicit approval or confirmation; remembered approvals never approve another request. No new authority levels or priority promotion. Preserve exact intent identity, owner, source, freshness, cancellation, and replay protection.

## 4C.1: Commander memory foundation

Create `Phase4C/MemoryEntry.cs`, `ConversationState.cs`, and `CommanderMemory.cs`. Memory is instance-owned, local RAM only, bounded by count and text length. No persistence, telemetry, embeddings, automatic profiling, global singleton, hidden state, or background update loop.

Default capacity 32 entries; configurable capacity must be 1..128. Each text field is bounded to 512 characters. Deterministic insertion order, ordinal comparisons, invariant serialization, no wall clock or random IDs. Entries contain immutable primitive/enum/string values only; no object-typed payload, delegates, Unity objects, simulation, intent, plan, command, or decision-record references. Snapshots detach collections, not merely expose mutable lists as read-only interfaces.

Store explicit player preferences, recent conversation turns, and value summaries of actual accepted strategies, decisions, and explanations. Player text is labelled untrusted conversation, never copied into trusted outcome fields. Approved strategy summaries are created only after successful game acceptance, not after translation or a button click alone. A copied outcome must distinguish selection, rejection, commitment refusal, and actual submission acceptance.

`ConversationState` binds the memory to one local match/player lifecycle and exposes a detached snapshot. The UI owns it; initialization for a new runtime/match/player, explicit reset, and destruction clear it. Generation checks prevent a late provider response from repopulating cleared memory. Tactical and strategic histories must not survive a match reset. Retained transcript and provider history must be bounded as well; reset clears their visible and request state.

Provide visible local commands `show memory` and `clear memory`, and whole-form preference `focus cavalry` (accept a trailing period). This preference alone creates no recommendation, plan, goal, or command. `prepare attack` uses a currently retained explicit cavalry preference to interpret an AttackPreparation recommendation; without one, ask for the supported attack focus without guessing. Explicit `prepare cavalry attack` keeps its existing meaning. Expiration/eviction removes preferences rather than keeping a hidden unlimited preference dictionary.

The existing provider receives a bounded, detached advisory projection through `StrategicAIRequest`, with no execution references. Preserve legacy request constructors. The deterministic offline provider must demonstrate contextual interpretation, and captured Gemini request JSON must contain the same advisory values labelled untrusted, not system instructions. There is no new cloud memory service; an explicitly configured remote translator can receive the bounded request context through its existing request path. Do not make a paid live call to prove this integration.

Required tests: `Memory_DoesNotLeakGameState`, `Memory_IsBounded`, `Memory_ClearsBetweenMatches`, `SameHistoryProducesSameContext`. Also cover immutable snapshots, text limits, eviction semantics, player isolation, explicit reset, late responses after reset/reinitialization/destruction, rejected strategy not recorded as approved, preference-only no execution, and preference-to-preview with approval still required.

## 4C.2: Grounded explanations

Create `CommanderExplanationService`, `ExplanationContext`, `ExplanationResult` under Phase4C. The host copies primitive reason/outcome/status/identity and detached plan progress into ExplanationContext. Do not pass StrategicDecisionRecord directly into a retained explanation context: it contains mutable intent/submission references.

Use a deterministic renderer of actual recorded reasons. No provider is required to explain a decision; this avoids fabricated reasons and works offline. Distinguish explanation of an historical recorded rejection from an assessment of the current state. Preserve the original reason and label its decision tick. Do not infer gold income from stored resources or say defense blocked an attack unless the recorded outcome says so. With no applicable decision, report that no recorded decision is available.

Route whole-form questions such as `why are we not attacking?`, `why was that rejected?`, `explain last decision`, and `what is the plan doing?` before tactical/strategic intent translation. Querying must not clear, confirm, or replace a pending recommendation, create a fresh decision, or mutate a plan. The last meaningful decision remains available after a conversational turn; the current UI's unconditional LatestStrategicDecision reset must not erase the explanation source. Explain copies rather than recomputing approval or policy. Append bounded explanation summaries to visible memory.

Required tests: `Explanation_MatchesDecisionReason`, `ExplanationCannotModifyIntent`, `RejectedPlanHasReason`. Also cover commitment refusal, accepted-plan progress, no decision, historical vs current state, and zero execution/decision-history mutation when asking why.

## 4C.3: Richer detached context

Add optional, deterministic aggregates calculated at the game-owned context-builder boundary, then expose only detached values to providers. Initial aggregates: own-worker gathering allocation ratio (explicitly an activity proxy, not economic productivity), owned army composition, current production queue/capacity bottlenecks, and plan milestone progress counts/status. Sort collections with explicit stable keys and use integers/counts or basis points to avoid locale/floating-point drift.

Do not label resource stock deltas as income. Genuine income trends remain a future improvement unless an existing authoritative gathered-income history is found; no new simulation telemetry is authorized. Optional does not mean fabricated: unavailable data is marked unavailable, never zero-measured. No hidden enemy history, extrapolation, predictions, or memory of previously visible enemies is added.

Prefer a dedicated detached `StrategicContextInsights` value and builder, avoiding provider-to-game access. Explicit provider serialization must include approved fields; adding a property only to StrategicContext.ToJson is insufficient because Gemini uses StrategicAIContextSerializer. Old callers remain compatible when insights are absent.

Required tests: `ContextRemainsFogSafe`, `ContextSerializationDeterministic`. Also prove changing hidden/explored-only enemy state cannot change the new aggregate payload, source mutation cannot alter a captured snapshot, aggregation hand-calculated values are correct, and providers consume the fields without acquiring references to game-owned services.

## 4C.4: Supported objectives only

Begin only after 4C.1-4C.3 gates pass. Choose concrete objectives based on inspected execution support, not the existence of an enum. The user's TechnologyRush/SiegePreparation/NavalExpansion/DefensiveTurtle list contains candidates, not permission to invent missing gameplay. Record the selected objectives and rejected candidates in the sub-phase spec before implementation.

Every selected objective requires a distinct StrategicIntent objective, strict DTO/parser validation, registry template, canonical-cost feasibility quotation, meaningful milestone sequence, decision/approval/commitment compatibility, provider interpretation, and an end-to-end runtime completion test through existing execution. No direct commands, new simulation rules, unsupported training type, or nominal alias masquerading as a distinct objective. Preserve old enum numeric values by appending types. Source/priority protections apply identically to new objectives.

Source-backed selection after discovery: add `RangedReinforcement` (resource allocation, ensure ArcheryRange, ensure ten archers) and `DefensiveTurtle` (resource allocation, ensure Barracks and ArcheryRange, two mandatory Towers, ensure eight spearmen and eight archers). The latter is explicitly a finite fortified-force preparation objective, not automated territory holding or perimeter placement. Towers are not silently optional as in the earlier defense template: unavailable age/resources must yield an honest rejection or established deterministic wait through player-direct flow. These objectives are distinct from existing optional-tower spear defense and mixed military reinforcement. TechnologyRush has no Commander research goal; SiegePreparation lacks Commander support for workshop/siege types even though simulation has them; NavalExpansion lacks the required gameplay model. Those three remain unavailable, with no nominal enum entries. No new evaluator/autonomous selection rule is introduced.

## Delegation and dependency order

| Stage | Agent tier | Expected output | Validation |
| --- | --- | --- | --- |
| Discovery | Luna | Source/method map, lifecycle and test pointers | Astra reviews integration points |
| Objective feasibility | Sol | Source-backed supported-objective assessment | Astra chooses supported scope |
| 4C.1 | Sol | Memory values/service, application integration, non-trivial tests | Focused, full regression, static checks, Sol scoped review, Astra boundary review |
| 4C.2 | Sol | Detached grounded explanation service and chat routing | Same gates plus no-mutation runtime proof |
| 4C.3 | Sol | Detached insights and provider payload integration | Same gates plus fog-safety proof |
| 4C.4 | Sol | Supported templates integrated through all existing gates | Same gates plus real milestone completion |
| Repetitive validation and report formatting | Luna | Saved JSON evidence, hash/dependency audits, documentation | Astra interprets results and final verdict |
| Final architecture/security/integration review | Astra | Requirement-by-requirement verdict | Current source plus saved detailed test/runtime evidence |

Only one implementation worker writes production files at a time. Unity's global test runner has one assigned owner. Read-only discovery/audit can run alongside useful independent architecture work. Each dispatched task records agent, requirements, expected output, validation, touched files, and report path in a phase-specific progress ledger. Reviewer findings return to the implementation worker; the orchestrator does not perform routine patches.

## Risks and gates

1. Authority laundering through memory: typed summaries cannot carry intents/commands; prior approval cannot become current approval. Adversarial history and replay tests.
2. Fog leakage: derive own aggregates from existing fog-safe snapshots; exact hidden-state differential tests and provider payload inspection.
3. Lifecycle races: cancel and generation-check before publishing transcript, memory, preview, or explanation; uncooperative delayed-provider tests.
4. Explanations becoming fabricated current-state advice: preserve provenance/tick and exact factual reasons; no approval/policy rerun on a query.
5. Unsupported objective execution: inspect canonical specs and existing executor before selecting; real runtime completion required for every new objective.
6. Regressions obscured by inherited dirty files: snapshot current source before edits; compare to this snapshot, never HEAD alone.
7. Stale Unity assemblies: verify compilation and current assembly timestamps in addition to error console and test results.

Static audit must find no new credentials, CommandBuffer access from advisory code, GameSimulation access from AI providers/services, or provider-to-planner references. The existing game-owned host/context builder/planner boundaries retain their existing necessary access. Report those exceptions explicitly instead of claiming the entire Commander tree is simulation-free.

Final report: A architecture; B delegation; C new systems; D safety; E focused/full tests; F runtime evidence; G future improvements. Verdict is READY FOR PHASE 4D only after every requirement is verified; otherwise REQUIRES FIX PHASE with concrete missing evidence.
