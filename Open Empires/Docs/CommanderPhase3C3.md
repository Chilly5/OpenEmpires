# OpenEmpires AI Commander Phase 3C-3 — Strategic Intent and Plan Selection Framework

## A. Architecture Changes

### StrategicIntent

`StrategicIntent` is a detached, local request value containing a stable `IntentId`,
`PlayerId`, `ObjectiveType`, `CreatedTick`, read-only `Parameters`, optional `Priority`,
`Status`, and `StatusReason`. The intentionally small objective enum contains:

- `AttackPreparation`
- `DefensivePreparation`
- `EconomicExpansion`
- `MilitaryReinforcement`

`StrategicIntentValidator` rejects a player mismatch, an undefined enum value, an
out-of-range priority, unsupported parameters, or the absence of a compatible template.
Rejected submissions publish `StrategicIntentRejected`, remain plan-free, and create no
Commander goals.

The existing tactical `CommanderIntent` and the new `StrategicIntent` implement the
shared `ICommanderIntentRequest` classification contract. Their `IntentLayer` values are
`Tactical` and `Strategic` respectively. This prepares a future dispatcher extension
without changing Phase 2 parsing or resolution.

### Template system

`IStrategicPlanTemplate` owns four deterministic operations: template identity,
compatibility, parameter validation, and plan creation. `CavalryPressurePlanTemplate`
supports `AttackPreparation` and `MilitaryReinforcement`. It currently accepts no
parameters, so arbitrary keys fail explicitly instead of being silently ignored.

Plan-specific tactical requests now live in the plan's milestone blueprint. The planner
submits those typed requests through `CommanderGoalManager`; it no longer switches on a
Cavalry milestone or constructs `CavalryPressurePlan` directly. This lets another
template compose the existing tactical request types without changing `StrategicPlanner`.

### Registry

`StrategicPlanRegistry` registers templates in stable order, rejects duplicate template
IDs, finds the first compatible template deterministically, validates its parameters,
and creates the plan. Its default registry contains `CavalryPressurePlanTemplate`.

Selection occurs only in `SubmitIntent`. There is no `Update`, `Tick`, continuous plan
scan, external provider, or autonomous strategy evaluation.

## B. New Files

| File | Responsibility |
|---|---|
| `Assets/Scripts/AI/Commander/Strategic/StrategicIntent.cs` | Strategic request model, statuses, validation results, validator, and submission result. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlanTemplate.cs` | Template contract, deterministic registry, and CavalryPressure template. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicTacticalGoalRequest.cs` | Typed milestone requests that submit only through `CommanderGoalManager`. |
| `Assets/Tests/EditMode/CommanderPhase3C3StrategicIntentTests.cs` | Thirteen intent, validation, registry, template, and planner tests. |
| `Assets/Tests/PlayMode/CommanderPhase3C3StrategicIntentPlayModeTests.cs` | Three runtime submission/rejection/completion scenarios. |
| `Docs/CommanderPhase3C3-editmode-results.json` | Final focused EditMode and legacy regression evidence. |
| `Docs/CommanderPhase3C3-playmode-results.json` | Final focused and full PlayMode evidence. |
| `Docs/CommanderPhase3C3.md` | Phase report. |

Unity generated matching `.meta` files for all new assets under `Assets`.

## C. Modified Files

| File | Why changed |
|---|---|
| `Assets/Scripts/AI/Commander/CommanderIntent.cs` | Adds the shared Tactical/Strategic classification interface while preserving every existing tactical intent type and constructor. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicMilestone.cs` | Stores the template-defined typed tactical requests for a milestone. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs` | Associates a plan with its source intent, stores generic completion/cancellation messages, and preserves CavalryPressure's four milestones and requirements while defining their existing tactical requests. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` | Adds intent creation/submission/lifecycle events, validation and registry selection, generic milestone request submission, and a registry-backed compatibility wrapper for the old start API. |

`GameSimulation`, `CommandBuffer`, `ICommand`, `CommanderPlanner`, command sources, and
networking sources were not modified for Phase 3C-3.

## D. Strategic Intent Flow

```text
StrategicIntent (local player objective)
    ↓ validate ownership, enum, priority, parameters, template availability
StrategicPlanRegistry
    ↓ deterministic compatible template
CavalryPressurePlanTemplate
    ↓ creates plan and milestone blueprint
CavalryPressurePlan
    ↓ Economic Foundation → Infrastructure → Army Preparation → Ready
typed StrategicTacticalGoalRequest values
    ↓ submit through existing CommanderGoalManager
Commander tactical goals
    ↓ existing CommanderPlanner and normal commands
Simulation / normal network replication
```

The compatibility method `StartCavalryPressurePlan()` now submits an
`AttackPreparation` intent and returns the registry-created plan. It does not instantiate
the plan itself.

## E. Tests

Final focused EditMode job: `0feea8ac761640bbb4fc061edd322ae3` — **13/13 passed**.

| Test | Purpose | Result |
|---|---|---|
| `StrategicIntent_CreatesCorrectly` | Fields, detached parameters, priority, and Tactical/Strategic classification. | Passed |
| `StrategicIntent_RejectsUnknownObjective` | Undefined objective enum values fail validation. | Passed |
| `StrategicIntent_PreservesOwnership` | Player ownership is retained and mismatches are rejected. | Passed |
| `StrategicIntent_RejectsUnsupportedParameter` | Unsupported keys fail explicitly. | Passed |
| `PlanTemplate_RegistersCorrectly` | Registration succeeds and duplicate IDs fail. | Passed |
| `PlanTemplate_SelectsCompatibleTemplate` | AttackPreparation selects CavalryPressure deterministically. | Passed |
| `PlanTemplate_NoMatchFailsSafely` | Known but unsupported objectives have no plan. | Passed |
| `CavalryTemplate_CreatesCorrectPlan` | Template creates the right owner/type/source-intent plan. | Passed |
| `CavalryTemplate_PreservesMilestones` | Four milestone names and tactical request blueprint are unchanged. | Passed |
| `CavalryTemplate_ExecutesExistingGoals` | Economic requests create existing resource-allocation goals. | Passed |
| `StrategicPlanner_CreatesPlanFromIntent` | Submission records the intent and creates an active plan through the registry. | Passed |
| `StrategicPlanner_RejectsUnsupportedIntent` | Rejection event/reason fires with no plan or goals. | Passed |
| `StrategicPlanner_DoesNotBypassCommanderGoals` | Every created child goal is observed from `CommanderGoalManager`; submission emits no direct command. | Passed |

### Regression evidence

| Required phase | Evidence | Result |
|---|---|---|
| Phase 1 | `88cbd86ae09940edadaad7d1f18b95ce` | 29/29 passed |
| Phase 2 | Current clean `CommanderIntent*` plus response-generator suite run | 40/40 passed |
| Phase 3A | `600902aab6d24e959db5d6aa4116cdc3` | 29/29 passed |
| Phase 3A.1 | `1eca8db94fc840139fd221332e276919` plus isolated `0514d69b657e4f228aea5ea634a4d3d7` | All 20 product cases covered; two MCP-log-contaminated cases passed 2/2 isolated |
| Phase 3B | `36c7c6e8d7e448808cd40fce54d34cde` | 31/31 passed |
| Phase 3C Preparation | `2ad1ef864c694cb0bbde870832c7782c` plus isolated `140ef7325a954a908e615e9b9b9d20e3` | All 21 product cases covered; one MCP-log-contaminated case passed isolated |
| Phase 3C-1 and 3C-2 final | `521436fbab904416b255bb8e57a8a53d` | 24/24 passed |
| Phase 3C-3 final | `0feea8ac761640bbb4fc061edd322ae3` | 13/13 passed |

The two broad jobs reported above as composite coverage were marked failed only because
the MCP-for-Unity client emitted an unhandled disposed `NetworkStream` error log during
bridge reconnect. Every named affected test passed in its clean isolated rerun; no
product assertion failure remained. A final compiler check found zero `error CS` entries.

## F. Runtime Evidence

Final focused PlayMode job: `20fcea3813c74ac99c8d79ce2175efc2` — **3/3 passed**.

1. `AttackPreparation` created intent #1, selected `CavalryPressurePlanTemplate`, created
   plan #1, reserved 800 Food and 500 Gold, activated `Economic Foundation`, and created
   two resource-allocation goals through `CommanderGoalManager`.
2. Undefined objective value `999` was rejected with `UnknownObjective`; the planner had
   zero plans and the goal manager had zero goals.
3. The intent-created plan completed Food/Gold allocation, Stable construction, six
   Knights, Ready, reservation release, plan completion, and intent completion through
   the existing milestone flow.

Final full PlayMode job: `83219f411152435eab77601957156c1b` — **28/28 passed**.

Representative runtime messages:

```text
[Phase3C-3 Runtime] PASS Scenario 1: AttackPreparation intent selected CavalryPressurePlan and began the Economic Foundation milestone through Commander goals.
[Phase3C-3 Runtime] PASS Scenario 2: unknown strategic objective was rejected; no plan or Commander goal was created.
[Phase3C-3 Runtime] PASS Scenario 3: intent-created CavalryPressurePlan completed the unchanged economy, Stable, Knights, and Ready milestone flow.
```

## G. Remaining Limitations

Completed in Phase 3C-3:

- Strategic requests have stable local identity, ownership, parameters, priority, and lifecycle status.
- Deterministic template registration, validation, selection, and plan creation exist.
- CavalryPressure is a template and retains its existing milestones, reservations, goals, and completion flow.
- Unsupported or invalid objectives fail safely without plans, tactical goals, commands, or network work.
- The planner contains no plan-construction or plan-specific milestone switch.

Future work, intentionally not implemented:

- LLM or natural-language interpretation into `StrategicIntent`.
- Automatic strategy selection or generation.
- Enemy analysis or prediction.
- Strategic scoring/evaluation and autonomous plan discovery.
- GoalGraph, HTN, machine learning, or reinforcement learning.
- Templates for DefensivePreparation and EconomicExpansion.

Strategic intent remains local. Other clients do not create intents or run strategic
reasoning; only the existing normal command path reaches network replication.
