# Pre-Phase-5A Intent-Fidelity Investigation

**Status:** Investigation complete. Phase 5A implementation has not started.
**Date:** 2026-10-06
**Scope:** Read-only source and runtime investigation of strategic actions that appear without a player request. No source code was changed and no provider request was made.

## 1. Repository state

The investigation used `D:\unity_projects\OpenEmpires\Open Empires`. At the start, Git reported a clean working tree on `unit_models_and_voice_control`, tracking `origin/unit_models_and_voice_control`. The only files changed by this investigation are this report and the accompanying status note in `remaining_work.md`.

## 2. Commit baseline

Baseline HEAD was `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca` (`4b0ebc3 Add result binding and voice input`). This is the source snapshot audited and used for the focused Unity reproduction.

## 3. User-visible defect

The Commander can select and submit a strategic plan during a background evaluation with no player intent. If the resulting plan starts goals, the user can observe worker assignment, production, or construction as though the Commander chose a strategy by itself. This is a real game-side capability, not proof that it happens on every match start.

The audit found a separate provider risk: the semantic model may return an unrequested `StrategicObjective` for preview. Current UI code stages that result and requires a player action before plan submission. Static inspection cannot establish that Luna returned such a result in the user's session.

## 4. Reproduction steps

The focused reproduction used existing PlayMode test `Runtime_StrategicEvaluationRunsEndToEnd` in `Assets/Tests/PlayMode/CommanderPhase3Fix11PlayModeTests.cs:59-70`:

1. Fire a `StrategicEvent` trigger with no player intent.
2. Call `pipeline.Tick(0)`.
3. Observe a selected recommendation, a created `MilitaryReinforcement` plan, its resource reservation and first milestone, and a submitted `BuildStructure` goal.

Unity Test Framework job `c096c4f4a3054cb48e3e332109007c89` completed successfully (1/1). This proves the no-player-intent evaluation can create a plan in the test fixture. The fixture is not a full `GameBootstrapper` launch, so it does not prove the default opening state selects a plan.

## 5. Reliability and proof limits

The source path and the focused fixture behavior are deterministic. One existing focused PlayMode test was run and passed. No repeated-match reliability sample, full suite, standalone build, live provider reproduction, or real match-start smoke was performed. The exact strategy observed by the user cannot be tied to a specific trigger or provider response from the available evidence.

On the ordinary configured opening state, `GameSetup.SpawnPlayerBase` creates six villagers and a scout, while the starting Town Center contributes population cap 10. That is seven population and three available capacity; the evaluator's economy and military rules require capacity at least five. Thus the first normal opening evaluation is not statically expected to select those objectives. Other snapshots and later triggers can satisfy the rules. `SimulationConfig.StartingVillagers` returns 3, but the active setup loop creates six; do not use that property alone to predict the opening context.

## 6. Request-to-execution call graph

**Background strategy path (no player request):**

`GameBootstrapper.Update` creates the simulation and Commander for player 0 in single-player or `LocalPlayerId` in multiplayer (`GameBootstrapper.cs:160-173`), then fires `WorldStateChange` with reason `Commander runtime initialized` (`:171-173`). The first normal tick runs `CommanderStrategicPipeline.Tick` (`:261-263`, multiplayer `:323-325`). `StrategicPipeline.Tick` calls `Evaluate(..., playerIntent: null)` when its trigger is ready (`StrategicPipeline.cs:96-104`). The evaluator builds recommendations; the policy chooses one; `Evaluate` materializes it as `AIRecommendation` and submits it through `StrategicPlanner.SubmitIntent` (`:126-158`). The planner validates the intent and creates a registered plan (`StrategicPlanner.cs:132-200, 277-314`). It activates the first milestone, reserves resources or waits, and submits milestone requests to `CommanderGoalManager` (`:308-314, 552-600, 777-838`). `CommanderGoalManager.Tick` passes each runnable goal to `CommanderPlanner`; when a command is produced, it enqueues it to `GameSimulation.CommandBuffer` (`CommanderGoalManager.cs:434-535`). The simulation processes flushed commands (`GameSimulation.cs:930-946, 1691`).

**Provider semantic path:**

`CommanderChatUI.SubmitMessageAsync` chooses the semantic provider path when available (`CommanderChatUI.cs:271-280`); it sends player text plus bounded context and conversation facts (`:416-425`). `OpenRouterCommanderProvider` requests bounded JSON from `openai/gpt-6-luna` (`OpenRouterCommanderProvider.cs:153-181`). `CommanderSemanticJson` parses the response. A single strategic node is validated and staged through `StrategicAIApprovalBridge.StageValidatedSemanticObjective` (`CommanderChatUI.cs:515-570`). `ApproveStrategicRecommendation` or `ConfirmStrategicCommand` then submits the pending strategy after freshness checks (`:635-636, 706-740`). A direct strategic player intent through `IntentRouter.Route` instead reaches `StrategicPipeline.EvaluatePlayerIntentNow`; tactical dispatcher behavior remains separate (`IntentRouter.cs:92-102`).

For an accepted tactical semantic graph, chat admission dispatches through `CommanderSemanticGraphAdmission` and commits the admitted goal graph through `CommanderGoalManager`; those goals use the same goal planner, command buffer, and simulation path. This is the normal tactical route, not strategic auto-selection (`CommanderChatUI.cs:487-511, 570-620`; `CommanderSemanticGraphAdmission.cs:40-93`; `CommanderGoalManager.cs:200-326`).

## 7. Provider schema

The semantic request admits one to four typed nodes, including tactical unit, structure, economy, age, movement/combat, research, and `StrategicObjective` nodes (`OpenRouterCommanderProvider.cs:22-38`; node fields in `CommanderSemanticRequest.cs:78-115`). The strategic objective is an optional typed enum field. The response schema does not give the provider plan construction details, adaptation contents, priority, approval state, or provenance. Game code maps an objective to a fixed plan template; for example, `AttackPreparation` maps to `CavalryPressure` (`StrategicPlanner.cs:1141-1159`).

## 8. Prompt behavior

The system prompt asks Luna to translate the player's request into bounded JSON and explicitly lists `StrategicObjective` plus six strategy choices (`OpenRouterCommanderProvider.cs:19-63`, particularly `:37`). It does not require an explicit player strategy request before emitting that node. A separate instruction says not to choose or emit goals or plans (`:52`), which conflicts with allowing a strategic-objective response and leaves room for an unintended preview. The context also advertises `strategyCapabilities` (`CommanderSemanticProvider.cs:86-91`). The request sets `max_tokens: 1024` and reasoning effort `none`; it does not specify a constrained response schema or temperature (`OpenRouterCommanderProvider.cs:153-181`). The response parser rejects malformed/unsupported response envelopes rather than submitting them (`:186-190, 234-296`). No API request was made during this audit.

## 9. Strategy-selection inventory

`RuleBasedStrategicEvaluator` emits four rule-based recommendations (`StrategicEvaluator.cs:34-84`):

- **MilitaryReinforcement:** military units below 8, available capacity at least 5, food at least 100, and wood or gold at least 100 (`:45-55`).
- **DefensivePreparation:** a visible enemy military unit and estimated defense below 10 (`:57-65`).
- **EconomicExpansion:** available capacity at least 5 and fewer than 8 gathering workers (`:67-74`).
- **AttackPreparation:** army strength at least 12, food at least 800, and gold at least 500 (`:76-81`).

The policy selects in fixed priority order: defense at score 80+, military reinforcement at 85+, attack at 90+ with additional attack conditions, then economic expansion (`StrategicDecisionPolicy.cs:71-101`). Recommendation sorting and selection comparisons are deterministic. Active-plan compatibility can permit same-type plans (`StrategicCommitmentPolicy.cs:9-22`), so this pipeline is not a universal “one strategy per game” guard.

## 10. Randomness inventory

No RNG call was found in the Commander evaluator, policy, or strategic submission path reviewed. The recommendation scores and ordering derive from snapshot values. The broader game has RNG uses, including in `AIPlayerSystem`, but these are separate from Commander strategy selection. State in a live match can change the recommendation inputs; that does not make the selection algorithm random.

## 11. Enum and default behavior

`StrategicObjectiveType` has a real zero value (`AttackPreparation`), and `StrategicIntentSource` also has a zero-valued member. The active semantic parser does not select these through omitted fields: it requires node discriminators and required objective strings, maps supported strings explicitly, and rejects missing or unknown values (`CommanderSemanticJson.cs:49-85, 421-445`). Optional `dependsOn` defaults to empty; the worker-allocation `sourceKind` defaults to `Any`, but neither default selects a strategy (`CommanderSemanticResult.Economy.cs:9-35`).

There is a distinct legacy DTO hazard: absent `intentCategory` can be inferred as Strategic from `objectiveType`, and `Enum.TryParse` can accept numeric string `"0"` as `AttackPreparation` (`CommanderIntentDto.cs:129-140`; enum at `StrategicIntent.cs:7-15`). This is not the active semantic Luna parser. The older tactical provider parser requires a Tactical category and tactical forms (`CommanderAIProvider.cs:180-229`).

## 12. Conversation and stale-response state

Semantic memory retains accepted unit/structure facts and clarification text, not strategic plans (`CommanderSemanticConversationMemory.cs:9-18, 75-100`). The UI checks cancellation, runtime generation, provider/simulation/goal-manager/dispatcher identity, and player ownership before admitting a response (`CommanderChatUI.cs:443-452`). Reset cancels the runtime generation and clears conversation/bridge state (`:759-778`). I found no semantic request ID and no raw prompt/response in the provider trace (`OpenRouterCommanderProvider.cs:329-333`). Memory can influence a model's interpretation, but has no direct plan-submission authority.

## 13. Phase 4D adaptation path

A strategic result for an active plan is staged as a proposal. The UI distinguishes ordinary approval from explicit confirmation; an adaptation cannot be submitted by the ordinary approve action (`CommanderChatUI.cs:548-557, 635-636, 706-740`). The proposal is labeled “Player-requested strategic change” (`:548-557`) even though the provider path does not verify explicitness, so the preview wording can overstate provenance. `StrategicAIApprovalBridge` and `StrategicApprovalLayer` validate ownership, source, freshness, intent identity, and current plan/resource constraints before the approved pipeline submits (`StrategicAIApprovalBridge.cs:67-130`; `StrategicPipeline.cs:175-215`). This guarded path is separate from background `StrategicPipeline.Tick`, which has no approval result.

## 14. Prerequisite actions versus new strategy roots

The semantic graph admission layer rejects a `StrategicObjective` inside a compound tactical graph (`CommanderSemanticGraphAdmission.cs:48-68`). For admitted tactical nodes, current graph admission does not prove that each node is a necessary prerequisite or effect of the player's requested root goal; accepted nodes can be committed as goals (`CommanderGoalManager.cs:200-264, 310-326`). This is a separate Phase 5A intent-fidelity issue. Strategic plans also contain many derived tactical requests by design; those are legitimate only when they remain traceable to an explicitly authorized strategic root.

## 15. Root cause

The direct cause is that `StrategicPipeline.Tick` uses the same committing `Evaluate` method as explicit evaluation, and `Evaluate` submits a selected recommendation even when `playerIntent` is null. `GameBootstrapper` arms this background path for the local player's Commander, and the pipeline has no human-versus-computer owner gate. Therefore a sufficiently qualifying world-state snapshot can create and execute a plan without user consent. The tested trigger/fixture reached this branch.

## 16. Exact source locations

- Startup and pipeline scheduling: `Assets/Scripts/Core/GameBootstrapper.cs:141-173, 242-265, 315-325`.
- Trigger cooldown/readiness: `Assets/Scripts/AI/Commander/Strategic/StrategicEvaluationTrigger.cs:15-73`.
- Background evaluation and submission: `Assets/Scripts/AI/Commander/Strategic/StrategicPipeline.cs:96-158`.
- Recommendation thresholds and policy order: `StrategicEvaluator.cs:34-84`; `StrategicDecisionPolicy.cs:63-101`.
- Plan registration and milestone goal creation: `StrategicPlanner.cs:132-200, 277-314, 552-600, 777-838`.
- Goal planning and command enqueue: `CommanderGoalManager.cs:434-535`; `GameSimulation.cs:930-946, 1691`.
- Semantic prompt/staging/approval: `Phase4A/OpenRouterCommanderProvider.cs:19-63`; `Phase4A/CommanderChatUI.cs:271-280, 515-570, 635-740`.

## 17. Why the behavior occurs

The evaluator is designed to recommend based on state, and the policy is allowed to select an eligible recommendation. `StrategicPipeline.Evaluate` then treats that selection as executable intent regardless of whether the trigger came from player input. `MaterializeRecommendation` marks its source as `AIRecommendation`, but `StrategicPlanner.SubmitIntent` accepts that source under normal or emergency plan authority (`StrategicPlanner.cs:319-327`). This preserves provenance as a label but does not require consent.

The evaluator assigns DefensivePreparation an Emergency priority. The pipeline promotes any selected Emergency-priority result to emergency transition authority even when the trigger itself was an ordinary world-state trigger (`StrategicPipeline.cs:146-155`). With emergency switching enabled by default, a defensive recommendation can replace a conflicting AI-owned plan; player-override plans have a separate protection (`StrategicCommitmentPolicy.cs:7, 67-85`).

## 18. Luna versus game-side cause

Game-side background evaluation independently explains how a strategy can start with no provider call. The Luna semantic contract also permits a model-generated strategic recommendation without checking whether the user asked for one, but the live UI stages that as a preview until approval/confirmation. No captured provider response ties the user's observed plan to Luna. Existing paid-request records under `Docs/CommanderFix` concern ordinary economy commands and do not establish this strategy behavior.

## 19. Randomness verdict

The Commander strategy decision is deterministic for a given strategic snapshot and policy configuration. No evidence found supports randomness as the source of the unexpected plan. Changes in world state, trigger timing, or active commitments can produce different deterministic results between evaluations.

## 20. Smallest correction to consider

Before Phase 5A, add an execution-authority gate around background evaluation: keep the periodic evaluator and recommendation visible, but do not submit its `AIRecommendation` for a local human-controlled Commander without explicit approval. Preserve auto-execution only for an owner explicitly identified as computer-controlled. Keep explicit `EvaluatePlayerIntentNow` and approved-strategy submissions on their existing paths. Do not use wording filters as the authority check.

This is a recommendation only. No correction was implemented in this investigation.

## 21. Phase 5A invariant

Every effectful goal or command must be justified by an explicit player-requested root goal or by a validated computer-controlled strategy root. A model-produced suggestion, contextual capability, inferred objective, or derived tactical node cannot silently create a new root goal. Ambiguity should produce a clarification or preview, not an additional effect.

## 22. Provenance tracking proposal

Attach immutable request provenance to each admitted root and derived node: request/correlation ID, submitting principal, explicit user language span or semantic root ID, objective/constraints, and provenance kind such as `PlayerExplicit`, `PlayerClarified`, `DerivedPrerequisite`, or `PlanContinuation`. Each prerequisite should record its parent root and the validated reason it is necessary. Preserve this provenance through admission, strategic plan, milestone, Commander goal, and eventual command so the final effect can be audited. Do not treat `StrategicIntentSource.AIRecommendation` alone as proof of user authorization.

## 23. Root-goal/effect validation

Validate the semantic graph against one or more explicit root goals after parsing and before goal registration. Require each direct effect to map to a root goal and its constraints; permit derived prerequisites only when a trusted game-side planner declares them necessary for that root. Reject extra branches with no provenance edge, conflicting effects, and provider-created strategy roots that were not explicitly requested. Validate ownership, state freshness, and current simulation feasibility again at commit time. This complements current structural parsing and graph admission.

## 24. Relevant tests

The focused PlayMode reproduction `Runtime_StrategicEvaluationRunsEndToEnd` was run and passed 1/1 (job above). Existing related coverage includes `StrategicPipeline_SubmitsAllowedIntent`, deterministic policy tests, semantic parser tests, stale-response/reset tests, and `AIRecommendationCannotAutoReplaceActivePlan`. The latter submits directly with non-emergency defaults and does not exercise the pipeline's Emergency-priority escalation. Existing coverage also does not assert behavior from the exact GameBootstrapper startup trigger with a threshold-qualifying state.

For implementation, add focused coverage for: human-owned background evaluation retaining recommendations without submitting; AI-owned auto-execution remaining enabled; explicit player and approved strategy requests still submitting; emergency recommendations not bypassing authority; the actual startup-trigger route; and Phase 5A root/prerequisite/effect cases including unrelated extra nodes. These are recommendations, not tests added or run by this investigation.

## 25. Diagnostic modifications

No diagnostic code, logging, test, package, or runtime configuration was modified. The reproduction used an existing test and its Unity output. The only edits made for this task are this report and the short factual status note in `remaining_work.md`.

## 26. Risks and limits

Gating background submission without distinguishing human and AI owners could disable intended computer-player automation. Conversely, retaining emergency authority on an unapproved recommendation preserves plan-replacement behavior. Phase 5A provenance must allow legitimate strategic plan prerequisites and continuations while rejecting unrelated provider nodes; an overly strict validator could block required gathering, production, age-up, or infrastructure steps. Current evidence proves a deterministic no-player-intent path and a focused fixture reproduction, but does not identify the exact trigger or provider output in the user's match.
