# Commander Phase 4B.2 design: strategic approval and controlled execution

Date: 2026-09-13

Status: architecture approved in conversation; written specification awaiting user review. No implementation has started.

Authoritative brief: `C:\Users\RS\.codex\attachments\c470b24c-e66b-4c72-8b70-6db50405b325\pasted-text-1.txt`.

## 1. Outcome and boundaries

Connect Phase 4B.1 interpretation to the existing Phase 3 strategic pipeline through a mandatory, trusted approval boundary. The interpreter still returns only a validated, newly created intent. The approval layer and text router never create plans, goals, reservations, or commands. Only the existing strategic planner creates plans after approval, decision, and commitment checks.

Do not modify GameSimulation, CommandBuffer, ICommand implementations, networking, CommanderPlanner execution logic, or the Phase 3 tactical execution path. Do not introduce a parallel strategy executor, a new autonomous loop, or new AI authority. Preserve existing Phase 4A provider, validation, cancellation, and tactical submission behavior.

The Phase 4B production folders must contain no direct GameSimulation or CommandBuffer references. Providers must not reference or receive StrategicPlanner, an execution callback, or another execution service. Runtime tests may use real simulation fixtures; distinguish tests from production in the dependency audit.

## 2. Current code findings

- StrategicIntent preserves identity, owner, objective, parameters, tick, priority, and status, but has no source metadata.
- StrategicAIRequest currently accepts a caller-supplied integer; StrategicPlanner has a separate mutable counter. RuleBasedStrategicDecisionPolicy also produces deterministic IDs from tick and recommendation identity. Collision repair at submission is too late to establish unique request identity.
- IntentRouter routes all strategic requests through EvaluatePlayerIntentNow, which treats the supplied intent as a player override. AI recommendations must not use that route unchanged.
- StrategicPlanAuthority currently has Normal, Emergency, and PlayerOverride. Source precedence needs additional trusted metadata, not a model-provided authority or priority field.
- StrategicContext already contains detached economy, population, military, production, active plans, and visible threats. It lacks active-plan authority/source and exact age-aware build/training capabilities and costs.
- CommanderChatUI calls CommanderAIIntentAdapter directly. The adapter both translates and submits tactical intents, so invoking it from the new execution-free router would violate the router boundary.
- The workspace contains inherited uncommitted Phase 3/4A/4B.1 work. Preserve it and capture a current baseline before implementation; do not assume HEAD represents the working baseline.

## 3. Chosen approach and alternatives

Use additive source metadata, detached approval inputs, an execution-free text route, and a source-aware entry point on the existing StrategicPipeline.

Rejected alternatives:

1. Reuse EvaluatePlayerIntentNow for all LLM results: incorrectly grants player authority to AI recommendations.
2. Add a separate Phase 4B executor that submits directly to StrategicPlanner: duplicates strategic orchestration and risks bypassing commitment, decision history, or authority checks.

The existing player path remains available for trusted direct player intents. Provider results use the new approval path and cannot select the legacy override entry point.

## 4. Trusted identity and source

Add exactly these source values:

```csharp
public enum StrategicIntentSource
{
    PlayerDirect,
    AIRecommendation,
    AIConfirmedPlayerCommand
}
```

Use immutable source metadata on StrategicIntent, assigned only by trusted creation paths. Existing direct-player creation retains PlayerDirect. Phase 4B.1 parsing explicitly creates AIRecommendation, regardless of whether the text was typed by the player. Existing rule-based AI decisions are also recommendations; emergency authority remains a separate game-side decision.

The strategic JSON schema remains category, objective, and parameters only. Reject source, authority, owner/player ID, priority, request ID, confirmation, or execution fields. Do not silently ignore privilege fields or deserialize them into trusted metadata.

The request orchestration path binds provider output to the original request ID, owner, tick, and default source, and rejects mismatches. An input claiming PlayerDirect cannot relabel a provider-produced recommendation. Confirmation is a distinct trusted UI operation, not model text or a JSON property.

Add a planner/session-owned intent identity provider shared by player creation, AI request creation, and trusted materialization of rule-based decisions. Allocation is synchronized and monotonic, rejects overflow, and is deterministic for the same ordered sequence of game-side requests. Allocate before asynchronous translation so network completion order cannot choose request identity. Concurrent allocation guarantees uniqueness, not deterministic thread scheduling. No process-global counter shared across independent games.

Allocation must not register a plan or reserve resources. Cancellation consumes an ID without recycling it. Existing externally supplied IDs must be reserved/observed safely or rejected on conflict; silently changing the identity of an approved request is not acceptable. Preserve legacy deterministic decision-policy behavior where possible, but ensure all intents entering the shared runtime use the same trusted identity ownership.

## 5. Approval contract and authority precedence

StrategicApprovalLayer accepts a current StrategicContext, the created StrategicIntent, and trusted StrategicIntentSource. It returns an immutable StrategicApprovalResult with Approved, Reason, Authority, and Intent. Rejected results contain no approved intent. Results cannot be publicly constructed as approved by arbitrary consumers.

Validate owner, known objective/source, Created status, allowed parameters, and consistency of the supplied source with the intent's trusted metadata. Reuse StrategicIntentValidator. Approval never calls template CreatePlan or planner SubmitIntent, and never mutates existing plans or goals.

Source precedence is strict:

| Incoming source | Final plan authority | Replacement permission |
| --- | --- | --- |
| PlayerDirect | PlayerOverride | May replace conflicting AI, emergency, or earlier player plans after all feasibility checks |
| AIConfirmedPlayerCommand | PlayerOverride | May replace conflicting normal AI and emergency plans, but not a higher-priority PlayerDirect plan |
| AIRecommendation | Normal | Cannot cancel or supersede player-command or emergency plans |

Store source with resulting plans/snapshots so the two player-command sources remain distinguishable despite sharing the existing PlayerOverride authority value. Never infer source from an explanation string or objective.

An ordinary defensive suggestion does not gain Emergency authority merely because its objective is DefensivePreparation. Emergency defense is selected by the existing trusted evaluator/policy. An active emergency defense or a currently eligible emergency-defense recommendation blocks an ordinary attack recommendation, with a specific rejection reason. Direct player override may supersede emergency defense after resource and capability checks. Explicitly confirmed AI intent remains below PlayerDirect.

Compatible coexistence remains subject to existing commitment policy and plan limits. Approval of a replacement is not cancellation: nothing is cancelled until controlled submission succeeds through existing planner ownership. A failed approval or final revalidation must leave existing plans and reservations intact.

## 6. Feasibility inputs and checks

Extend the existing detached strategic snapshot, not a duplicate context builder. Include trusted active-plan source/authority and age-aware capability/cost summaries supplied by the existing game-side snapshot/planner boundary. These summaries contain values only, with no simulation or network handles and no hidden enemy information.

Expose a read-only feasibility quote for each of the four existing objectives. It must not instantiate a StrategicPlan, create goals, or reserve resources. Derive costs from canonical game-side building/training specifications and existing plan target constants, accounting for owned and queued units, completed or already-building infrastructure, and age-locked optional stages. Do not copy guessed building/unit prices into Phase 4B.

Interpret the brief's four unlabelled feasibility groups in objective order:

| Objective | Required approval checks |
| --- | --- |
| AttackPreparation | Available food and gold satisfy existing attack decision thresholds and the applicable quoted costs; required military strength and age-aware cavalry production/build capability are available |
| DefensivePreparation | Defensive infrastructure can be built or relevant existing infrastructure can be used; workers, age rules, and available resources cover the applicable defense preparation quote |
| EconomicExpansion | Available population and production capacity permit growth; required Town Center construction is age-available and affordable; worker production is supported |
| MilitaryReinforcement | Required unit types are trainable at the current age through existing or feasible required infrastructure; production/population capacity and available resources cover the applicable quote |

Compare against available resources after existing reservations, not total stockpile. Missing capability evidence rejects safely. Optional age-locked defense towers remain optional in accordance with existing execution behavior; do not reject age-one defense solely for lacking towers. Required age-locked expansion/cavalry capability rejects at this approval gate rather than creating a partially runnable plan.

Approval is a current-state permission, not a promise that future execution cannot stall. Rebuild the context and revalidate when consuming approval so an asynchronous response, intervening resource spend, new emergency, or new player plan cannot use stale permission. Planner/goal execution retains its normal ongoing validation.

## 7. Routing, confirmation, and UI

CommanderIntentRouter performs deterministic classification only and returns a typed route/rejection. It never calls the tactical adapter's submitting method or any strategic submission method. The UI/application orchestration consumes the route:

```text
CommanderChatUI
  -> CommanderIntentRouter (classification only)
     -> Tactical route -> existing CommanderAIIntentAdapter -> unchanged tactical flow
     -> Strategic route -> StrategicAIRequest -> Strategic AI Interpreter
        -> validated Created intent (AIRecommendation)
        -> StrategicApprovalLayer
        -> existing StrategicPipeline source-aware decision entry
        -> StrategicDecisionPolicy -> StrategicCommitmentPolicy
        -> StrategicPlanner.SubmitIntent
        -> StrategicPlan -> CommanderGoalManager -> existing execution
     -> Unknown -> safe rejection with no provider/submission side effects
```

At minimum, route "make 10 spearmen" tactically and "prepare cavalry attack" strategically; preserve existing supported tactical phrases and all four strategic objective phrases. Use whole, recognized request forms rather than substring matches that misroute mixed/injection requests. The router must not choose authority from natural language.

Add a minimal strategic preview/confirmation surface to CommanderChatUI without rewriting the tactical adapter. Ordinary recommendations can proceed only through normal-authority approval and policy. If the player explicitly confirms a displayed recommendation as a command, a trusted operation records AIConfirmedPlayerCommand and re-runs approval on fresh state. Provider responses cannot trigger that operation. Clear pending confirmation on cancellation, disposal, replacement by another request, or consumption. Confirmation applies to the exact displayed intent once, not whatever response happens to arrive last.

The trusted direct-player creation API supports the brief's explicit replacement example. Free-form text passing through the LLM does not become PlayerDirect merely by containing "cancel" or "override". Its preview explains the proposed replacement, and explicit player confirmation is required for elevated authority.

Preserve current tactical UI test contracts where practical; expose a separate typed strategic result instead of disguising a strategic plan as a tactical goal submission. Preserve bounded history, single-request handling, safe errors, and lifetime cancellation. No background strategy loop is added.

## 8. Controlled submission and policy integration

Add a source-aware approval entry to the existing StrategicPipeline. The application host calls it explicitly; neither router nor interpreter holds it. It rebuilds current context, obtains/rechecks approval, invokes the existing decision policy, checks commitment using approved authority/source, and submits only the selected approved intent. Record outcome in existing RecentDecisionHistory.

The decision policy must not treat an approved AIRecommendation as its legacy playerIntent argument. Add an explicit source-aware policy path that retains the existing game-side rules without changing AI recommendations into player overrides. If policy declines the requested objective, report rejection; do not silently submit an unrelated recommendation under its approval.

Approval consumption is bound to owner, exact intent identity/content/source, and a single successful submission. Replays, altered requests, reused confirmation, invalid status, or foreign-owner results reject. Perform the final decision/commitment/submission sequence synchronously on the existing game thread after translation. Do not hold an approval across another asynchronous operation.

Do not modify tactical execution or instantiate plans in the bridge. Small strategic admission/identity/metadata changes are allowed where required to enforce this boundary. Existing strategic tick behavior remains; do not add another tick driver.

## 9. Planned files and permitted edits

New production files under `Assets/Scripts/AI/Commander/Phase4B2/`:

- StrategicApprovalLayer.cs: pure approval rules and immutable result.
- CommanderIntentRouter.cs: execution-free classification and route values.
- StrategicAIApprovalBridge.cs: request identity binding, translation orchestration, and explicit confirmation state; execution remains in the shared pipeline.

New trusted strategic support under `Assets/Scripts/AI/Commander/Strategic/`:

- StrategicIntentSource.cs and StrategicIntentIdProvider.cs.
- Detached capability/feasibility value types where keeping them separate improves clarity.

Targeted edits expected:

- StrategicIntent, StrategicPlanner, StrategicPlan: source preservation, shared identity allocation, read-only quote support, and safe strategic admission metadata.
- StrategicContext and StrategicContextBuilder: detached approval inputs.
- StrategicPipeline, StrategicDecisionPolicy, StrategicCommitmentPolicy: approved source-aware admission and strict precedence.
- Phase4B1 StrategicAIInterpreter/StrategicAIJson: explicit safe default source and request provenance; preserve strict wire schema and translation-only behavior.
- Phase4A CommanderChatUI: minimal routing, strategic result display, and explicit confirmation integration. No rewrite of CommanderAIIntentAdapter or providers.

Only extend a game-side context capture boundary if canonical values cannot already be obtained through StrategicPlanner; no forbidden simulation/execution implementation is edited. Final documentation must list actual additions and edits, not only this forecast.

## 10. Verification and completion gates

Create `Assets/Tests/EditMode/CommanderPhase4B2Tests.cs` with all 19 required names:

1. AIIntent_CannotBecomePlayerOverride
2. JsonCannotControlIntentSource
3. PlayerIntentKeepsPlayerAuthority
4. AIRecommendation_IsApprovedWhenSafe
5. AIRecommendation_RejectedDuringEmergency
6. PlayerIntent_OverridesAIRecommendation
7. InvalidResources_RejectIntent
8. EmergencyDefense_BlocksAttack
9. PlayerOverride_CanReplacePlan
10. AIRecommendation_CannotCancelPlayerPlan
11. InterpreterDoesNotCreatePlans
12. ApprovalLayerDoesNotCreatePlans
13. OnlyApprovedIntentReachesPlanner
14. SpearmenRequestUsesTacticalProvider
15. CavalryRequestUsesStrategicProvider
16. UnknownRequestRejected
17. LLMJsonCannotSetAuthority
18. LLMJsonCannotSetOwner
19. LLMJsonCannotSetPriority

Additional required coverage:

- PlayerDirect preservation, AIRecommendation default, forged source, confirmed/direct precedence, and AI defense never self-promoting to Emergency.
- Sequential and concurrent IDs, player/AI/rule-based coexistence, overflow, deterministic ordered allocation, cancellation gaps, and identity preserved through approval/submission.
- Each objective's insufficient resources/capacity/age/training/build capability cases, reserved-resource accounting, and no partial plans or cancellation on failure.
- Stale approval, foreign owner, repeated confirmation/submission, out-of-order asynchronous completion, cancellation/disposal, and provider exception safety.
- Hidden enemy changes do not change approval/provider snapshots; newly visible threats may change approval.
- Pure router, interpreter, and approval stages have Plans=0, Goals=0, Commands=0 and no reservations/registration side effects.
- Real shared-pipeline runtime scenarios show normal strategic plan and goal progression after approval; direct commands still come only from existing execution. Include approved attack, rejected emergency conflict, explicit player replacement, and tactical spearmen regression.
- UI integration proves actual provider selection and explicit confirmation, not only route enum values. PlayMode exercises lifecycle cancellation and the real execution boundary using offline providers.

Use test-driven implementation and independent code review according to Superpowers. Run focused Phase 4B.2 tests and full EditMode/PlayMode suites, proving all Phase 3, 4A, and 4B.1 regressions pass. Unity MCP tests must use live job handles and saved detailed results. Check compilation after a full refresh; zero compiler errors and zero test failures are required.

Capture pre-edit hashes of forbidden production files and verify them unchanged. Scan all tracked files and new deliverables for key patterns without printing secrets. Scan both Phase 4B production folders for forbidden simulation/command dependencies, and inspect providers for execution access. Do not claim a live Gemini test from mocked transport evidence.

Create `Docs/CommanderPhase4B2.md` containing architecture changes, actual new/modified files, approval flow, test evidence, runtime evidence, remaining limitations, and the verdict `READY FOR PHASE 4C` or `REQUIRES FIX PHASE`. Save machine-readable test/runtime/audit evidence alongside it. Mark the goal complete only after every brief requirement has direct, current evidence.

## 11. Scope and known limitations

- Existing four objectives and existing strategic plans only; no arbitrary plan generation, new tactical commands, voice support, or networking changes.
- A model can misunderstand ordinary language, but cannot assign identity, ownership, authority, or confirmation. Strict schema and trusted approval remain mandatory.
- Feasibility is snapshot-based; ongoing game events may later cause the existing executor to wait or fail safely.
- Unique IDs are owned within the existing commander/planner identity domain; owner plus ID identifies an intent across independent players. Independent games do not share allocation state.
- Live paid provider calls are not necessary for approval/execution proof; use deterministic offline and hostile-provider fixtures and clearly label evidence.
- Preserve inherited work. Do not commit unrelated files, reset the checkout, change branches, or clean recovery scenes as part of this phase.

## 12. Design review checklist

- [x] All four objectives and the source enum match the brief.
- [x] Direct player, confirmed command, ordinary recommendation, and emergency authority are distinct.
- [x] Router and approval cannot execute; tactical adapter remains outside the pure router.
- [x] Shared pipeline and decision/commitment checks remain mandatory.
- [x] Resource quotes use trusted canonical values without pre-creating plans.
- [x] Named tests, runtime proof, frozen boundaries, secret scan, and final report are included.
- [x] No placeholders or unresolved alternative implementations remain in the behavioral contract.
- [ ] User reviews this written specification before the implementation plan is written.
