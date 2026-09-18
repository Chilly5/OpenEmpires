# Commander Phase 3 Final Fix 1.3
Verified 2026-09-09 in Unity 6000.5.9f1.

## A. Files changed
This inventory describes this fix, not all pre-existing uncommitted changes in the checkout.

Production files under `Assets/Scripts/AI/Commander/`:
- `IntentRouter.cs`: routes strategic requests through the shared pipeline.
- `CommanderIntentDispatcher.cs`: includes prerequisite/resource reasons in the response.
- `CommanderIntentCatalog.cs`: canonical ArcheryRange normalization.
- `CommanderGoalManager.cs`: internal access to its simulation for existing planning queries.
- `Strategic/StrategicPipeline.cs`: one shared pipeline per planner, with matching disposal.
- `Strategic/StrategicPlanner.cs`: canonical budgets and milestone requirements, bounded worker allocation, resource recovery, age waiting, and reservation lifecycle.
- `Strategic/StrategicPlan.cs`: corrected defensive, economic, and cavalry milestones.
- `Strategic/StrategicMilestone.cs`: prerequisite waiting, repeat-safe request submission, and resource requirement bookkeeping.
- `Strategic/StrategicTacticalGoalRequest.cs`: reuse existing infrastructure and skip optional age-locked structures.
- `Strategic/StrategicContext.cs`: exposes the plan's waiting reason to context consumers.

New tests:
- `Assets/Tests/EditMode/CommanderPhase3Fix13Tests.cs` and its Unity metadata.
- `Assets/Tests/PlayMode/CommanderPhase3Fix13PlayModeTests.cs` and its Unity metadata.

Updated regression fixtures:
- EditMode: `CommanderPhase3C1StrategicPlanTests.cs`, `CommanderPhase3C2StrategicContextTests.cs`, `CommanderPhase3C3StrategicIntentTests.cs`, `CommanderPhase3FixTests.cs`, `CommanderPhase3Fix11Tests.cs`, `CommanderPhase3Fix12Tests.cs`.
- PlayMode: `CommanderPhase3C1StrategicPlanPlayModeTests.cs`, `CommanderPhase3C2StrategicResourcePlayModeTests.cs`, `CommanderPhase3C3StrategicIntentPlayModeTests.cs`.
- These updates supply the added wood dependency and test reservations at actual spending milestones instead of the former fictitious economy cost.

Evidence:
- [EditMode results](CommanderPhase3Fix13-editmode-results.json)
- [PlayMode results](CommanderPhase3Fix13-playmode-results.json)
- [Production source hashes](CommanderPhase3Fix13-source-hashes.json)

## B. Architecture impact

### Strategic routing
Player text and decoded LLM strategic intents reach `CommanderIntentDispatcher -> IntentRouter -> StrategicPipeline.EvaluatePlayerIntentNow -> decision policy -> commitment policy -> StrategicPlanner.SubmitIntent`. The existing planner validator still checks intent parameters before accepting a plan. The router uses the same pipeline instance as the runtime bootstrap; standalone planner users obtain one lazily. Evaluation events, trigger tracking, and decision history therefore cover player/LLM submissions.

Source inspection found no production caller of strategic `SubmitIntent` outside the pipeline (the planner retains its own low-level overloads and test helpers). Plan templates remain factories invoked by StrategicPlanner; the router/pipeline never constructs plans themselves. Tactical resolution is unchanged.

No gameplay command is created in these routing/strategic layers. Typed goals still reach CommanderGoalManager, CommanderPlanner, the normal command buffer, and GameSimulation. No provider API, voice, memory, personality, networking, or new autonomous scheduling loop was added.

### Plan execution
- Defense: food/wood preparation -> reuse or build Barracks -> towers only when their canonical age requirement is available -> spearmen. At age one, towers are skipped and spearman production proceeds. Preparation scales down to the living worker supply, so three starting villagers are not asked to satisfy sixteen simultaneous gatherer assignments.
- Expansion: checks the Town Center age requirement before creating goals. Below age two it remains `WaitingForPrerequisite`, reports why in the response and strategic context, and retries through the existing planner tick. After age advancement it prepares food, wood, and stone, builds an additional Town Center, then reaches the villager target. No age-advance feature was added.
- Cavalry: food/gold/wood preparation, existing-or-new Stables, then knights. The economy milestone now has canonical food/gold claims; infrastructure and army stages have their own requirements. Missing resources create a waiting state while gathering requests remain runnable.

### Resources and reservations
Costs come from GameSimulation building-cost queries and its civilization-resolved unit training specification backed by SimulationConfig. There are no replacement guessed plan totals. For the verified French setup:

| Execution item | Canonical cost |
| --- | --- |
| Barracks / Stables | 150 wood each |
| Tower | 300 wood, no stone |
| Spearman | 60 food + 20 wood |
| Town Center | 400 wood + 350 stone |
| Knight | 140 food + 100 gold |
| Villager | 50 food |

Unit requirements account for living and queued units. Training claims use canonical base costs; building-specific discounts can make actual spending lower. Future plan budgets remain estimates, while milestone reservations are active planning claims. Claims are released when their milestone completes or their plan ends/cancels. A resource-waiting spending stage does not submit its spending goal before obtaining its claim. Recovery gathering is attached to the plan through normal goals and the existing retry cadence. Simulation stockpiles are still changed only by normal gameplay.

## C. Tests
**327/327 Commander EditMode tests passed; 46/46 Commander PlayMode tests passed. No skipped or failed tests. 0 C# compiler errors.**

The focused Fix13 suite passed **20/20** cases, including every requested test name:

- `PlayerStrategicIntent_UsesStrategicPipeline`
- `LLMStrategicIntent_UsesStrategicPipeline`
- `StrategicIntent_CreatesDecisionHistory`
- `TacticalIntent_DoesNotUseStrategicPipeline`
- `DefensivePreparation_CreatesBarracksBeforeSpearmen`
- `DefensivePreparation_UsesCorrectResources`
- `DefensivePreparation_DoesNotDeadlockAgePrerequisite`
- `EconomicExpansion_AllocatesStoneForTownCenter`
- `EconomicExpansion_DoesNotDeadlockOnTownCenterPrerequisite`
- `EconomicExpansion_CreatesVillagersAfterTownCenter`
- `CavalryPlan_CreatesMilestoneReservations`
- `CavalryPlan_WaitsWhenResourcesMissing`
- `CavalryPlan_ReleasesReservationsOnCancel`
- `Catalog_ResolvesArcheryRangeAliases`

The full EditMode run covered Phase1, Phase2's intent/parser/resolver/validation/response/integration suites, Phase3A, Phase3A1, Phase3B, Phase3C preparation and C1-C5, Phase3Fix, Fix11, Fix12, and Fix13.

| EditMode fixture | Passed |
| --- | ---: |
| CommanderIntentIntegrationTests | 6 |
| CommanderIntentParserTests | 21 |
| CommanderIntentResolverTests | 6 |
| CommanderIntentValidationTests | 3 |
| CommanderPhase1Tests | 29 |
| CommanderPhase3A1Tests | 20 |
| CommanderPhase3ATests | 29 |
| CommanderPhase3BTests | 31 |
| CommanderPhase3C1StrategicPlanTests | 11 |
| CommanderPhase3C2StrategicContextTests | 13 |
| CommanderPhase3C3StrategicIntentTests | 13 |
| CommanderPhase3C4StrategicEvaluationTests | 15 |
| CommanderPhase3C5StrategicDecisionTests | 13 |
| CommanderPhase3CPreparationTests | 21 |
| CommanderPhase3Fix11Tests | 25 |
| CommanderPhase3Fix12Tests | 19 |
| CommanderPhase3Fix13Tests | 20 |
| CommanderPhase3FixTests | 28 |
| CommanderResponseGeneratorTests | 4 |

| PlayMode fixture | Passed |
| --- | ---: |
| CommanderPhase3A1PlayModeTests | 3 |
| CommanderPhase3BPlayModeTests | 12 |
| CommanderPhase3C1StrategicPlanPlayModeTests | 2 |
| CommanderPhase3C2StrategicResourcePlayModeTests | 4 |
| CommanderPhase3C3StrategicIntentPlayModeTests | 3 |
| CommanderPhase3C4StrategicEvaluationPlayModeTests | 4 |
| CommanderPhase3C5StrategicDecisionPlayModeTests | 4 |
| CommanderPhase3CPreparationPlayModeTests | 3 |
| CommanderPhase3Fix11PlayModeTests | 5 |
| CommanderPhase3Fix13PlayModeTests | 6 |

Final full-run job IDs:
- EditMode: `902fa42452994d1ba2737af9608c702e`
- PlayMode: `4123a77b64b04587aa86250af817e274`

The focused unit tests use controlled milestone advancement for accounting checks. The execution evidence below instead runs real GameSimulation ticks, construction, resource collection, and training.

## D. Runtime evidence
All six Fix13 PlayMode scenarios passed in the final full run:
1. **Player pipeline:** the actual dispatcher creates a strategic plan and the matching decision-history submission; dispatch itself emits no ICommand.
2. **Defensive execution:** completed Barracks, two towers, and at least eight living spearmen in an age-enabled fixture.
3. **Fresh defense:** `prepare defense` completed at age one with three villagers, canonical starting resources, one starting Town Center, and no prebuilt houses or military infrastructure. Optional towers were not requested. Normal Commander execution supplied infrastructure, population capacity, resources, and defensive units.
4. **Economic expansion:** constructed a second Town Center and reached twenty living villagers through simulation execution.
5. **Cavalry:** recovered from zero wood through actual collection, progressed through Stables and knight production, and released its reservations.
6. **Determinism:** two separately initialized simulations and Commanders received the same defensive intent and matched checksums at every one of 1,500 simulation ticks.

Final source checks found no changes to the networking directory, GameSimulation, SimulationConfig, or command definitions. No fresh multiplayer session was run; the determinism evidence is the paired simulation test plus the existing regression suite.

## E. Phase 4 readiness
**READY FOR PHASE 4** for the requested LLM integration boundary.

This conclusion is supported by the verified common strategic route, preserved validation and command ownership, observable prerequisite/resource waits, canonical plan accounting, and successful real simulation execution—not just test counts. Phase 4 can feed strategic or tactical intents through the existing dispatcher without introducing a second plan-creation or command path.

Readiness does not add an age-advancement agent or promise success without workers, reachable resources, or external age progression when required. Those constraints remain explicit; no new AI features were introduced.

