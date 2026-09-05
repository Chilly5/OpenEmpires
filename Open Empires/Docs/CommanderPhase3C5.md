# OpenEmpires AI Commander Phase 3C-5 — Strategic Decision and Intent Resolution Framework

## A. Architecture Changes

### Decision policy

`IStrategicDecisionPolicy` defines the requested direct selection operation:

```text
SelectIntent(StrategicContext, StrategicRecommendation[]) -> StrategicIntent or null
```

It also exposes `Decide(...) -> StrategicDecisionResult` so callers can retain an explanation, source recommendation, creation tick, status, and strategic priority. Both operations have overloads for an already-created player `StrategicIntent`.

`RuleBasedStrategicDecisionPolicy` is stateless and deterministic. It reads only the detached `StrategicContext`, recommendations, and optional player intent supplied to the call. It has no simulation, service, planner, goal-manager, command-buffer, networking, update-loop, or random state.

Selection follows fixed strategic tiers instead of choosing the largest score:

1. A valid explicit player intent wins before recommendation handling.
2. `DefensivePreparation` at score 80 or above is `Emergency` and overrides every AI alternative.
3. `MilitaryReinforcement` at score 85 or above is `High`.
4. `AttackPreparation` at score 90 or above is `Normal`, and is eligible only when the same snapshot still contains army strength 12+, 800 available Food, and 500 available Gold.
5. `EconomicExpansion` is the `Low` fallback.

Within one objective type, ties are resolved by score descending, numeric recommendation priority descending, recommendation ID ascending, and source tick ascending. Generated intent identity is derived deterministically from the snapshot tick, player, and source recommendation. Selection does not mutate its inputs.

### Player override

The player overload accepts an existing `StrategicIntent`; it performs no language parsing. A newly-created intent with matching ownership and a recognized objective bypasses recommendation ranking and is returned unchanged. Mismatched ownership, an unknown objective, or an already-submitted intent produces a rejected result rather than silently falling back to AI selection.

### Decision result

`StrategicDecisionResult` contains:

- `SelectedIntent`;
- `Reason`;
- `SourceRecommendation`;
- `CreatedTick`;
- `Status` (`Selected`, `Rejected`, or `NoDecision`);
- `PriorityLevel` (`Emergency`, `High`, `Normal`, or `Low`).

The result is explanatory data only and has no execution authority.

## B. New Files

- `Assets/Scripts/AI/Commander/Strategic/StrategicDecisionPolicy.cs` — replaceable policy interface, player override, fixed priority rules, attack readiness gate, deterministic tie-breaking, and intent selection.
- `Assets/Scripts/AI/Commander/Strategic/StrategicDecisionResult.cs` — priority model and explainable decision result/status model.
- `Assets/Tests/EditMode/CommanderPhase3C5StrategicDecisionTests.cs` — 13 required policy, override, result, determinism, and safety tests.
- `Assets/Tests/PlayMode/CommanderPhase3C5StrategicDecisionPlayModeTests.cs` — four requested Unity runtime scenarios.
- `Docs/CommanderPhase3C5-editmode-results.json` — focused and regression EditMode evidence.
- `Docs/CommanderPhase3C5-playmode-results.json` — focused and full PlayMode evidence.
- `Docs/CommanderPhase3C5.md` — this report.

Unity generated matching `.meta` files for all four new assets under `Assets/`.

## C. Modified Files

No existing production, test, scene, project-setting, or networking file was modified for Phase 3C-5. The existing `StrategicPlanner.SubmitIntent(StrategicIntent)` API already provides the correct explicit boundary, so integration required no change to planner execution.

Earlier uncommitted Phase 3C-1 through 3C-4 work in the shared worktree was preserved.

## D. Decision Flow

```text
explicit strategic evaluation request / player request / strategic event
                              ↓
                       StrategicContext
                              ↓
                    StrategicEvaluator
                              ↓
               StrategicRecommendation[]
                              ↓
                StrategicDecisionPolicy
                              ↓
       StrategicDecisionResult + selected StrategicIntent
                              ↓
                 explicit caller submission
                              ↓
        StrategicPlanner.SubmitIntent(StrategicIntent)
                              ↓
              StrategicPlanRegistry
                              ↓
                    StrategicPlan
                              ↓
              CommanderGoalManager
                              ↓
                Commander execution
```

Evaluation and decision are request-driven. There is no frame callback, simulation tick hook, continuous strategy switching, automatic submission, or remote-client decision path. Calling the policy creates only a detached intent value. The existing planner must still be called explicitly before a plan or tactical goal can exist.

## E. Tests

Focused Phase 3C-5 EditMode job `90219b840f43470cbab995fea3e1a7fd`: **13/13 passed**, 0 failed, 0 skipped, 3.7055095 seconds.

| Test | Purpose | Result |
|---|---|---|
| `DecisionPolicy_SelectsHighestPriority` | Proves fixed tier priority overrides raw score ordering. | Passed |
| `DecisionPolicy_DefenseOverridesAttack` | Proves emergency defense defeats a higher-scored attack. | Passed |
| `DecisionPolicy_AttackRequiresConditions` | Proves attack needs both threshold score and current snapshot readiness. | Passed |
| `DecisionPolicy_EconomicFallback` | Proves economy is selected when higher-tier candidates are ineligible. | Passed |
| `PlayerStrategicIntent_OverridesRecommendations` | Proves an explicit player intent wins before AI ranking. | Passed |
| `PlayerIntentPreservesOwnership` | Proves the same owned player intent is returned unchanged. | Passed |
| `DecisionResult_ContainsReason` | Proves the selected explanation retains the recommendation reason and tick. | Passed |
| `DecisionResult_TracksSourceRecommendation` | Proves the exact selected recommendation is traceable. | Passed |
| `DecisionPolicy_SameInputProducesSameOutput` | Proves repeated identical inputs return identical values without input mutation. | Passed |
| `DecisionPolicy_HasNoHiddenState` | Proves the policy has no instance fields and exposes the requested selection contract. | Passed |
| `DecisionPolicy_DoesNotCreatePlans` | Proves selection leaves planner intents, plans, goals, and commands empty. | Passed |
| `DecisionPolicy_DoesNotCreateCommands` | Proves selection does not enqueue gameplay commands. | Passed |
| `DecisionPolicy_DoesNotBypassStrategicPlanner` | Proves nothing executes until the selected intent is explicitly submitted through the existing planner. | Passed |

Regression evidence:

- Full EditMode assembly job `969d76b9a7274fa39d6fb19907442e83` executed **256/256** tests, covering all requested Commander phases from Phase 1 through Phase 3C-5 plus the rest of the EditMode assembly. It reported 254 passes and two setup failures caused solely by MCP disposed-`NetworkStream` error-log injection.
- Exact clean rerun job `7786155536364f65a62f890f51ff761c`: **2/2 passed**, covering `GoalLifecycle_RemainsDeterministic` and `WorkerReservation_ReleasesAfterCancellationOrFailure(False)`.
- Full PlayMode assembly job `02a09ec06b1a4332aa877cff6d48841d`: **36/36 passed**, 0 failed, 0 skipped, 15.4242606 seconds.
- Final Unity compilation and console check: zero C# errors.

The broad run plus exact rerun leaves no unresolved Commander regression.

## F. Runtime Evidence

Focused Phase 3C-5 PlayMode job `af4c2f608690472badb2ba7b192ff4f9`: **4/4 passed**, 0 failed, 0 skipped, 1.080046 seconds.

- Defense selection: a currently visible enemy threat, weak defense, and strong economy selected `DefensivePreparation` at `Emergency` priority. No plan started.
- Attack selection: a 12-unit owned army with 1000 Food, 700 Gold, and no emergency selected `AttackPreparation`.
- Player override: an explicit owned `EconomicExpansion` `StrategicIntent` beat the available AI recommendations and was returned unchanged.
- No decision: an empty recommendation set returned `NoDecision`, a null intent, and a null source recommendation.

Every runtime scenario asserted that `StrategicPlanner.Intents`, `StrategicPlanner.Plans`, `CommanderGoalManager.Goals`, and the gameplay command buffer remained empty.

## G. Remaining Limitations

Completed in Phase 3C-5:

- replaceable deterministic strategic decision policy;
- fixed emergency/high/normal/low objective priorities;
- recommendation-to-selected-intent resolution;
- explicit player strategic override;
- explainable selected/rejected/no-decision results;
- attack readiness revalidation;
- explicit-only planner handoff with no uncontrolled planning;
- EditMode, runtime, compilation, and regression verification.

Future work, intentionally excluded:

- LLM intent extraction;
- natural-language understanding;
- strategic personality;
- adaptive or learned strategy;
- autonomous strategy discovery;
- long-term memory;
- automatic event scheduling or continuous reevaluation;
- remote-client strategic decision execution.

Phase 3C-5 creates deterministic strategic choice infrastructure, not AI reasoning.
