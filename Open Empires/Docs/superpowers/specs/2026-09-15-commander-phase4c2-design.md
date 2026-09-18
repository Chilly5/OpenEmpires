# Commander Phase 4C.2: Grounded explanation boundary

This is an architecture contract, not an implementation or completion claim. Implementation waits for the 4C.1 phase gate. Overall authority remains `2026-09-15-commander-phase4c-design.md` and the original user brief.

## Choice

Use a deterministic explanation service over copied recorded outcomes. An LLM-only explanation path could paraphrase fluently but would require factual verification of every reason; it adds no necessary capability for this phase. Re-running decision policy when asked why would explain a new hypothetical decision rather than the actual historical decision and could create side effects. Both alternatives are rejected.

## Boundaries

`CommanderExplanationService.Explain(ExplanationContext)` returns an immutable `ExplanationResult`. Neither class receives a planner, pipeline, intent, submission, simulation, callback, or mutable decision record. The game-owned UI host projects existing `EvaluationCompleted` data immediately into primitives and detached plan-state values, then discards the source record for explanation storage. Existing UI diagnostic properties may remain for backward compatibility but are not provider/explanation-service inputs.

ExplanationContext distinguishes these outcomes explicitly: no recorded decision; policy/approval rejected; commitment refused; planner rejected; plan actually created. Preserve the original outcome and decision reason, decision ID/tick, known requested objective, and known accepted plan ID/type. A selected intent is not proof a plan began: `Submission.CreatedPlan` is the relevant acceptance evidence. Do not keep source-record references or derive rejection objective from ActivePlanType, which may refer to an unrelated ongoing plan.

For a submission initiated by this UI, capture objective/intent identity before approval and correlate the synchronous returned record with that submission. For decisions observed without such provenance, keep objective unknown if the record has no selected intent. Never infer it from prose or invent missing metadata. No new authority-bearing identifiers are created.

The service returns bounded text and detached reason values. It preserves factual reasons faithfully and explicitly labels historical decision tick. It does not translate resource stock into income, add predictions, promise plan completion, or claim an emergency blocked an attack unless the actual record supports that claim. Stored explanation text is advisory, never a request to execute.

## Conversation integration

Recognize whole-form questions `why are we not attacking?`, `why was that rejected?`, `explain last decision`, and `what is the plan doing?`, with invariant normalization and a documented punctuation policy. Mixed/hostile appended instructions remain rejected. Route these questions before code that clears pending recommendations or resets latest decision state.

`explain last decision` and `why was that rejected?` explain the latest known meaningful recorded outcome. If it was not rejected, say that instead of fabricating a rejection. For `why are we not attacking?`, an actual recorded attack rejection may be explained; if the latest outcome has another objective or unknown objective, say there is no attributable attack rejection and identify the known latest outcome separately. Lack of evidence is not evidence of gold shortage or emergency priority.

`what is the plan doing?` may ask the game-owned host for a fresh detached context using the existing read-only CaptureContext path; it must never Evaluate, Tick, Submit, or modify plans. Render actual plan status/current milestone, not an inferred prediction. A fresh plan snapshot is labelled current; recorded decision reasons retain their historical tick. The service still sees values only.

Queries never clear, confirm, replace, or approve the pending strategy. They do not change policy decision-history count, plan/milestone status, resource reservations, goal count, or command count. Memory receives only a bounded explanation summary; it is not the authority for future approval.

## Lifecycle

One local owner/session owns the last copied explanation context. Subscribe and unsubscribe pipeline events with runtime initialization/destruction, exactly as the memory host does. Clear memory/new match/player change clears the copied decision source too; do not silently reconstruct cleared explanation state from old Pipeline.DecisionHistory. The actual game decision history remains untouched. A subsequent new decision may establish a new local explanation source.

Conversation-only messages must not overwrite the last meaningful decision. All transcript/state publication follows the same generation/lifetime checks as 4C.1. No remote calls are needed to explain decisions, so explanations remain available with no configured key or with an unavailable remote provider.

## Verification contract

Required named tests: `Explanation_MatchesDecisionReason`, `ExplanationCannotModifyIntent`, `RejectedPlanHasReason`. Add hand-checked cases for each distinct outcome above, a requested attack rejected while an unrelated defense is active, a selected-but-blocked transition, a planner rejection after selection, and missing attack provenance. A captured context must remain unchanged when the original intent/plan/record changes.

PlayMode proof: create an actual rejected strategic submission through chat; query why and match the rejection reason; assert pending preview identity and all gameplay/decision-history counts are unchanged. Complete/advance a real plan using existing execution, query progress, and show it matches the detached current snapshot without advancing anything on query. Reset and verify old explanation data is unavailable. Run full regression/static/security gates before 4C.3.

## Delegation

Sol implements the service, projection and UI integration plus non-trivial tests after the 4C.1 interfaces are final. Luna runs repetitive regression/static checks and formats evidence. Astra reviews outcome provenance, authority neutrality, lifecycle isolation, and runtime proof. The detailed implementation plan is written after 4C.1 stabilizes; it must consume the actual final memory interfaces, not guessed signatures.
