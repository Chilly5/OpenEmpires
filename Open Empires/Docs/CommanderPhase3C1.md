# OpenEmpires AI Commander Phase 3C-1 — Strategic Plan Framework

Date: 2026-09-05

## A. Architecture Changes

Phase 3C-1 adds a local, event-driven strategic coordination layer above the existing tactical Commander.

- `StrategicPlan` owns high-level plan identity, owner, type, lifecycle, ordered milestones, all child tactical-goal IDs, creation tick, and the deterministic outcome message.
- `StrategicMilestone` owns a separate milestone lifecycle and explicit required/completed child-goal sets.
- `StrategicPlanner` owns plans, submits typed tactical goals only through `CommanderGoalManager`, observes the existing `GoalEventPublished` stream, and advances or terminates plans only in response to creation or child-goal events.
- `GameBootstrapper` creates one local `StrategicPlanner` beside the local-player `CommanderGoalManager` and disposes its event subscription with the bootstrapper.
- `CommanderGoalManager` exposes its simulation tick as read-only plan-creation metadata. Its tactical scheduling and execution responsibilities are unchanged.

The strategic coordinator has no `Tick`/`Update` loop. It does not access `GameSimulation`, `CommanderPlanner`, `CommandBuffer`, or `ICommand`, and it does not create commands. There are no networking changes; strategic objects and events remain local, while their child goals use the existing ordinary-command path.

No LLM integration, automatic strategy selection, strategic intent dispatch, GoalGraph, HTN, graph search, resource reservation/budgeting, or enemy-strategy prediction was added.

## B. New Files

| File | Responsibility |
|---|---|
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlan.cs` | Strategic plan types/status, shared plan model, and the one deterministic `CavalryPressurePlan`. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicMilestone.cs` | Ordered milestone model, lifecycle, and required/completed child-goal tracking. |
| `Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.cs` | Event-driven plan coordinator, goal submission/tracking, milestone transitions, completion, failure, and cancellation propagation. |
| `Assets/Tests/EditMode/CommanderPhase3C1StrategicPlanTests.cs` | Eleven required lifecycle, milestone, and Cavalry plan tests. |
| `Assets/Tests/PlayMode/CommanderPhase3C1StrategicPlanPlayModeTests.cs` | Runtime transition and failure scenarios using the real Commander goal/event pipeline. |
| `Docs/CommanderPhase3C1-editmode-results.json` | Final full EditMode result summary. |
| `Docs/CommanderPhase3C1-playmode-results.json` | Final full PlayMode result summary and runtime evidence. |

Unity generated the corresponding folder/script `.meta` files.

## C. Modified Files

| File | Change and reason |
|---|---|
| `Assets/Scripts/AI/Commander/CommanderGoalManager.cs` | Added read-only `CurrentTick` so strategic plan creation can record deterministic simulation-tick metadata without accessing `GameSimulation`. No scheduling or tactical behavior changed. |
| `Assets/Scripts/Core/GameBootstrapper.cs` | Owns and disposes the local `StrategicPlanner` beside the existing local Commander. No simulation, command, or networking behavior changed. |

`CommanderPlanner`, `GameSimulation`, `CommandBuffer`, command implementations, serializers, and networking files were not modified in Phase 3C-1.

## D. Strategic Plan Flow

```text
CavalryPressurePlan
  |
  +-- Milestone 1: Economic Foundation
  |     +-- ResourceAllocationGoal(Food, 10 workers)
  |     +-- ResourceAllocationGoal(Gold, 6 workers)
  |
  +-- Milestone 2: Infrastructure
  |     +-- BuildStructureGoal(Stables, 1)
  |
  +-- Milestone 3: Army Preparation
  |     +-- EnsureUnitCountGoal(Knight, 6)
  |
  +-- Milestone 4: Ready
        +-- Plan completes with "Cavalry preparation complete."

StrategicPlanner
  -> CommanderGoalManager
  -> CommanderGoal
  -> CommanderPlanner
  -> ICommand
  -> CommandBuffer
  -> GameSimulation
```

The next milestone is not created early. A child `GoalCompleted` event updates the owning milestone; all required child goals must be complete before advancing. A child `GoalFailed` event fails the milestone and plan and cancels remaining owned tactical goals. Explicit strategic cancellation marks the plan cancelled first, then calls `CommanderGoalManager.CancelGoal` for each nonterminal child so existing reservation release remains authoritative. An unexpected external child cancellation fails the active plan rather than waiting forever.

## E. Tests

Final full Unity results:

- EditMode job `d7495a0af16e45ed8484f89ea5098709`: 202/202 passed, 0 failed, 0 skipped, 171.8950315 seconds.
- PlayMode job `0ef18b5073224f2cba5e6ba89fc55238`: 21/21 passed, 0 failed, 0 skipped, 10.0739347 seconds.
- Final Phase 3C-1 EditMode job `31a8c19a29ae4678b424358605530360`: 11/11 passed, 0 failed, 0 skipped, 2.7744487 seconds.
- Final Phase 3C-1 PlayMode job `96372e6291a74ff488eaae6865fdb569`: 2/2 passed, 0 failed, 0 skipped, 0.527354 seconds.
- Compiler: successful Tundra build; zero `error CS` console entries.

An earlier full EditMode attempt reached all 202 tests but two Phase 3B cases were contaminated by an MCP bridge `disposed NetworkStream` error log. No tests or product code were suppressed or weakened; the unchanged full suite was rerun to the all-passed result above.

| Test | Purpose | Result |
|---|---|---|
| `StrategicPlan_StartsCorrectly` | Stable plan identity, owner, type, creation tick, active status, and ordered first milestone. | Passed |
| `StrategicPlan_CompletesCorrectly` | Full lifecycle, all milestones complete, deterministic response, and no nonterminal children. | Passed |
| `StrategicPlan_FailsCorrectly` | Stable tactical-goal timeout propagates through Infrastructure to plan failure. | Passed |
| `StrategicPlan_CancelsChildGoals` | Strategic cancellation cancels children through GoalManager and releases reservations. | Passed |
| `Milestone_AdvancesAfterChildGoalsComplete` | All economic children complete before Infrastructure activates. | Passed |
| `Milestone_DoesNotAdvanceEarly` | One incomplete required child keeps Economic Foundation active. | Passed |
| `Milestone_FailureStopsPlan` | Failed milestone skips future milestones and creates no later goals. | Passed |
| `CavalryPlan_CreatesEconomicGoals` | Creates exactly the food-10 and gold-6 tactical allocation goals. | Passed |
| `CavalryPlan_BuildsStableAfterEconomy` | Creates the Stable goal only after the economic milestone completes. | Passed |
| `CavalryPlan_TrainsKnightsAfterStable` | Creates Knight-6 and emits the normal tactical `TrainUnitCommand` after Stable completion. | Passed |
| `CavalryPlan_CompletesAfterArmyReady` | Completes Ready/plan only after six living Knights exist. | Passed |
| `Runtime_CavalryPressurePlan_AdvancesThroughAllScenarios` | PlayMode proof of the four requested start/economy/Stable/Knights transitions. | Passed |
| `Runtime_CavalryPressurePlan_ChildFailureStopsPlan` | PlayMode proof that a failed Stable child fails Infrastructure and the plan. | Passed |

The full results cover Phase 1, Phase 2, Phase 3A, Phase 3A.1, Phase 3B, Phase 3C Preparation, and Phase 3C-1.

## F. Runtime Evidence

The PlayMode fixture used real `GameSimulation`, `CommanderGoalManager`, strategic subscriptions, tactical planner evaluation, and ordinary command-buffer output. It staged deterministic state at each strategic boundary so the test proves coordination rather than spending thousands of ticks retesting construction/training internals already covered by earlier PlayMode suites.

1. Plan start: `CavalryPressurePlan` became Active; Economic Foundation became Active; food and gold goals were tracked.
2. Economy complete: both allocation goals published `GoalCompleted`; Economic Foundation completed; Infrastructure activated; a Stable `PlaceBuildingCommand` was emitted by the tactical Commander.
3. Stable complete: the Stable goal published `GoalCompleted`; Infrastructure completed; Army Preparation activated; a Knight `TrainUnitCommand` was emitted by the tactical Commander.
4. Knights ready: the Knight goal published `GoalCompleted`; Army Preparation and Ready completed; the plan completed with `Cavalry preparation complete.`; every child goal was terminal.
5. Failure: the Stable child reached the existing 36,000-tick tactical duration limit; Infrastructure and the plan failed; no Army/Ready goal was created.

## G. Remaining Limitations

Completed in Phase 3C-1:

- strategic plan and milestone models with stable IDs and independent lifecycles;
- event-driven plan coordinator above the tactical Commander;
- deterministic Cavalry Pressure plan;
- child ownership, completion, failure, and cancellation propagation;
- local bootstrap integration and deterministic response;
- full EditMode and PlayMode regression coverage.

Future Phase 3C work, intentionally not implemented:

- resource budgeting or reservations;
- richer strategic context;
- strategic intent DTO/dispatcher integration;
- automatic or LLM strategy selection;
- additional strategic plan types;
- multiplayer product-flow/UI certification for initiating strategic plans.
