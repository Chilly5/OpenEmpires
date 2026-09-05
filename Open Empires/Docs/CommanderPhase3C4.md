# OpenEmpires AI Commander Phase 3C-4 — Strategic Evaluation Framework

## A. Architecture Changes

### Strategic recommendation model

`StrategicRecommendation` is a detached local result containing a stable recommendation ID, owner, the existing `StrategicObjectiveType`, deterministic score, reason, source snapshot tick, priority, and lifecycle status. A proposed recommendation can be converted explicitly to a `StrategicIntent`, but conversion does not submit the intent to `StrategicPlanner`.

### Strategic evaluator

`IStrategicEvaluator` defines one operation:

```text
Evaluate(StrategicContext) -> IReadOnlyList<StrategicRecommendation>
```

`RuleBasedStrategicEvaluator` is stateless and deterministic. It retains no simulation or service references. Results are sorted by descending score and then by objective enum value, so identical contexts produce identical ordering, IDs, scores, priorities, reasons, and ticks.

### Rule system

- `MilitaryReinforcement`: fewer than 8 owned military units, at least 5 free population, at least 100 available Food, and at least 100 available Wood or Gold. Score is 85, plus 15 when idle production capacity exists.
- `DefensivePreparation`: currently visible enemy military exists and own defensive capability is below 10. Score is 75 plus up to 25 from visible enemy strength, so a larger visible threat scores higher.
- `EconomicExpansion`: at least 5 free population and fewer than 8 workers currently assigned to gather. Score rises with the measured allocation shortfall.
- `AttackPreparation`: army strength at least 12, at least 800 available Food, and at least 500 available Gold. Score is 100.

The score is a bounded sum of rule signals, not learned inference. Multiple rules can produce multiple recommendations; the evaluator does not select one.

### Context extension

`CommanderContextBuilder` now captures two detached aggregates when a context is requested:

- worker assignment counts by resource;
- enemy military counts by unit type, gated to alive non-allied military on tiles that are `Visible` now.

`StrategicContext` exposes those aggregates plus total workers, gathering workers, owned army size/strength, defensive building capability, and visible threat size/strength. Hidden and explored-only enemy data never enters the context.

## B. New Files

- `Assets/Scripts/AI/Commander/Strategic/StrategicRecommendation.cs` — recommendation DTO, lifecycle, and explicit intent conversion.
- `Assets/Scripts/AI/Commander/Strategic/StrategicEvaluator.cs` — evaluator interface, deterministic rules, scoring, and ranking.
- `Assets/Tests/EditMode/CommanderPhase3C4StrategicEvaluationTests.cs` — 15 model, rules, scoring, fog-safety, and integration tests.
- `Assets/Tests/PlayMode/CommanderPhase3C4StrategicEvaluationPlayModeTests.cs` — four requested runtime scenarios.
- `Docs/CommanderPhase3C4-editmode-results.json` — focused EditMode result summary.
- `Docs/CommanderPhase3C4-playmode-results.json` — focused PlayMode result summary and scenario evidence.
- `Docs/CommanderPhase3C4.md` — this report.

Unity generated matching `.meta` files for every new asset under `Assets/`.

## C. Modified Files

- `Assets/Scripts/AI/Commander/CommanderContext.cs` — added detached worker-allocation and currently-visible enemy-military snapshot contracts; updated the information-boundary description.
- `Assets/Scripts/AI/Commander/CommanderContextBuilder.cs` — builds the two new aggregates on demand and applies current-visibility, ownership, alliance, life-state, villager, and sheep filters.
- `Assets/Scripts/AI/Commander/Strategic/StrategicContext.cs` — added evaluation-only economy, army, defense, and threat facts.
- `Assets/Scripts/AI/Commander/Strategic/StrategicContextBuilder.cs` — maps the new Commander facts and derives simple deterministic aggregate estimates.
- `Assets/Tests/EditMode/CommanderPhase3C2StrategicContextTests.cs` — replaced an obsolete assertion that banned the word `Enemy` from the schema with direct assertions that hidden enemy values and counts remain absent.

No Phase 3C-4 changes were made to `GameSimulation`, `CommandBuffer`, command types, `CommanderPlanner`, `CommanderGoalManager`, `StrategicPlanner`, strategic plan execution, or networking.

## D. Evaluation Flow

```text
Game state snapshot request
          ↓
CommanderContextBuilder
  owned facts + visible-only facts
          ↓
StrategicContextBuilder
          ↓
StrategicContext
          ↓
IStrategicEvaluator
          ↓
ranked StrategicRecommendation list
          ↓
explicit optional ToStrategicIntent(...)
          ↓
StrategicIntent (created only; not submitted)
          ↓ future explicit caller action
StrategicPlanRegistry → StrategicPlan → CommanderGoalManager
```

Evaluation is request-driven. There is no `Update`, tick hook, world scan owned by the evaluator, automatic strategy selection, or remote-client evaluation path.

## E. Tests

Focused Phase 3C-4 EditMode job `aceda10951f94326ab0d0c64a8c4b4a4`: **15/15 passed**, 0 failed, 0 skipped, 6.9062487 seconds.

| Test | Purpose | Result |
|---|---|---|
| `Recommendation_CreatesCorrectly` | Verifies model identity, owner, score, tick, priority, and proposed status. | Passed |
| `Recommendation_UsesExistingObjectiveTypes` | Proves the model reuses `StrategicObjectiveType` and its four objectives. | Passed |
| `Recommendation_PreservesReason` | Proves reason text is retained exactly. | Passed |
| `Evaluator_ReturnsDeterministicResults` | Repeats one context and compares ranked result values. | Passed |
| `Evaluator_ReturnsDefenseRecommendation` | Exercises visible threat plus weak defense. | Passed |
| `Evaluator_ReturnsMilitaryRecommendation` | Exercises low army, free population, and production resources. | Passed |
| `Evaluator_ReturnsEconomicRecommendation` | Exercises low worker allocation plus population capacity. | Passed |
| `Evaluator_ReturnsAttackRecommendation` | Exercises strong army plus sufficient Food and Gold. | Passed |
| `Evaluator_ScoresAreDeterministic` | Repeats scoring and verifies 0–100 bounds. | Passed |
| `Evaluator_HigherThreatProducesHigherDefenseScore` | Proves a larger visible threat raises defense score. | Passed |
| `Evaluator_DoesNotUseHiddenInformation` | Changes hidden enemy units and proves context/results stay equal. | Passed |
| `Evaluator_UsesOnlyStrategicContext` | Verifies the interface input and that the evaluator retains no service state. | Passed |
| `Recommendation_CanConvertToStrategicIntent` | Verifies owner/objective/tick/priority transfer without submission. | Passed |
| `Evaluator_DoesNotCreatePlansDirectly` | Verifies evaluator output leaves intents, plans, and goals empty. | Passed |
| `Evaluator_DoesNotCreateCommands` | Verifies evaluation leaves goals and command buffer empty. | Passed |

Regression evidence:

- Combined Commander EditMode job `145560f516374542b0412a1e4b65a77d` executed **222/222** tests across Phases 1, 2, 3A, 3A.1, 3B, Preparation, 3C-1, 3C-2, 3C-3, and 3C-4. It identified the one obsolete Phase 3C-2 assertion corrected above; four other failures were MCP disposed-`NetworkStream` error-log injections rather than test assertion failures.
- Post-fix affected-suite job `9443ac41d13e44b1b68e911c03303b22` executed **80/80** Phase 3B, Preparation, 3C-2, and 3C-4 cases. All code assertions passed; two Preparation cases were contaminated only by the same MCP disposed-`NetworkStream` log.
- Exact clean rerun job `c87b7772cd4c4fb9b3f6def3fd8c38a5`: **2/2 passed**, covering `GoalLifecycle_RemainsDeterministic` and `WorkerReservation_ReleasesDeadOrGarrisonedWorker(False)`.
- Full PlayMode job `ae1d0baeb3be4cd2861d2727547e5483`: **32/32 passed**, 0 failed, 0 skipped, 13.728095 seconds.
- Final Unity compilation check: zero `error CS` console entries.

Together, the broad jobs and exact reruns cover every requested Commander phase without an unresolved regression.

## F. Runtime Evidence

Focused Phase 3C-4 PlayMode job `29fe48c59b93474ebddb75b281c26664`: **4/4 passed**, 0 failed, 0 skipped, 1.0178323 seconds.

- Defensive scenario: visible enemy military plus capability below threshold returned `DefensivePreparation`; no plan was created.
- Economic scenario: low worker allocation plus free population returned `EconomicExpansion`.
- Military/offensive scenario: 12 owned military units plus 1000 Food and 700 Gold returned `AttackPreparation`.
- Hidden-information scenario: changing and adding an enemy army on unexplored tiles left the serialized evaluation result unchanged, visible threat count at zero, and produced no defensive recommendation.

Every scenario also asserted that `StrategicPlanner.Intents`, `StrategicPlanner.Plans`, and `CommanderGoalManager.Goals` remained empty and that the command buffer contained no commands.

## G. Remaining Limitations

Completed in Phase 3C-4:

- deterministic strategic recommendation model;
- replaceable evaluator interface;
- simple rule-based scoring and stable ranking;
- multiple recommendations without automatic selection;
- explicit recommendation-to-intent conversion without submission;
- request-time worker allocation and visible-only threat awareness;
- EditMode, regression, and runtime verification.

Future work, intentionally excluded:

- LLM reasoning and natural-language understanding;
- automatic strategy selection or intent submission;
- learned, adaptive, predictive, or opponent-modeling systems;
- richer combat-value weights, income-rate history, map-control analysis, and long-horizon planning;
- additional strategic plan templates for objectives that currently have no executable template.

Phase 3C-4 creates measurable strategic awareness, not strategic intelligence.
