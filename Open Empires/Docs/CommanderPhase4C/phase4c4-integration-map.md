# Phase 4C.4 integration map

Read-only source map for the architecture contract in
`Docs/superpowers/specs/2026-09-15-commander-phase4c4-design.md`. This records
existing symbols and dependency points only; it is not an implementation plan.

## Identity, enums, and validation

- `Assets/Scripts/AI/Commander/Strategic/StrategicIntent.cs:7-12` — `StrategicObjectiveType` enum; `StrategicIntentValidator` begins at :131 and rejects undefined enum values at :144.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs:6-12` — `StrategicPlanType`; abstract `StrategicPlan` begins at :31 and owns milestones/budget requirements. Existing concrete plan classes begin at `CavalryPressurePlan`:152, `DefensivePreparationPlan`:187, `EconomicExpansionPlan`:217, `MilitaryReinforcementPlan`:252.
- `Assets/Scripts/AI/Commander/Strategic/StrategicTacticalGoalRequest.cs:10-65` — existing `StrategicResourceAllocationGoalRequest`, `StrategicBuildStructureGoalRequest`, and `StrategicEnsureUnitCountGoalRequest` request types.

## Templates, plans, milestones, and budgets

- `Assets/Scripts/AI/Commander/Strategic/StrategicPlanTemplate.cs:14-64` — `StrategicPlanRegistry`, `CreateDefault`, registration, strict template lookup, parameter validation, and plan creation.
- Same file `:66-200` — current objective-to-template implementations for `CavalryPressure`, `DefensivePreparation`, `EconomicExpansion`, and `MilitaryReinforcement`; common parameter validation is :202-215.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs:85-118` — `AddBudgetRequirement` and `SetBudgetFromSimulation`; concrete constructors add ordered `StrategicMilestone` tactical goals at :152-284.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs:599-675` — `FitEconomyToAvailableWorkers` and existing worker-allocation/build/unit request fitting; plan budget application is :213-217.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.Feasibility.cs:9-18` — `QuoteFeasibility`/`QuoteObjective`; canonical cost helpers `Build` and `Train` are :31-94; objective switch is :98-115; final capacity/housing checks and `StrategicFeasibility` creation are :117-138.

## Objective-to-plan mappings and authority boundaries

- `Assets/Scripts/AI/Commander/Phase4B2/StrategicApprovalLayer.cs:102-110` — `PlanType(StrategicObjectiveType)` mapping used by approval/commitment checks.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPipeline.cs:216-247` — `ToPlanType` mapping and transition path; `EvaluateApprovedIntentNow` and submission/event flow are in the surrounding :100-160 region.
- `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs:803-815` — planner `ToPlanType`; `SubmitIntent`/plan creation begins at :111 and `planRegistry.CreatePlan` is :155.
- `Assets/Scripts/AI/Commander/Strategic/StrategicCommitmentPolicy.cs:9-85` — compatibility and source/priority transition policy.

## Parser, router, mock, and Gemini allowlists

- `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIJson.cs:39-70` — strict objective-name switch and request construction using `StrategicPlanRegistry.CreateDefault` at :71.
- `Assets/Scripts/AI/Commander/Phase4B1/MockStrategicAIProvider.cs:10-45` — normalized phrase switch and current mock objective strings.
- `Assets/Scripts/AI/Commander/Phase4B1/GeminiStrategicAIProvider.cs:17-22` — prompt allowlist/objective phrases; interpreter entry point is :40.
- `Assets/Scripts/AI/Commander/Phase4B2/CommanderIntentRouter.cs:22-35` — whole-form strategic phrase routing to the strategic path.
- `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIInterpreter.cs:69-111` — `IStrategicAIInterpreter` and interpretation result boundary; no execution authority is exposed here.

## Context and runtime projection points

- `Assets/Scripts/AI/Commander/Strategic/StrategicContextBuilder.cs:80-132` — active-plan primitive snapshots and planner `QuoteFeasibility` projection.
- `Assets/Scripts/AI/Commander/Strategic/StrategicContext.cs:15-57,193-213` — detached `ActivePlans`/`Feasibility` collections and `StrategicPlanState`.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:127-222` — strategic initialization and provider-to-pending-preview path; actual approval remains in the existing UI/pipeline path.
