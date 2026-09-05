# OpenEmpires AI Commander Phase 3A

Verified 2026-09-04 in Unity 6000.5.9f1. Phase 3A deterministic capability expansion is complete; Phase 3B was not started.

## A. Architecture changes

The existing architecture remains:

```text
local text -> interpreter -> typed intent -> validator -> resolver
           -> goal manager -> planner -> ordinary ICommand -> normal simulation
```

Added typed construction and allocation goals, generic unit production, executable worker constraints, canonical producer/cost queries, and deterministic response templates. Planning remains every 15 simulation ticks (0.5 seconds at 30 Hz), with one active FIFO goal and one normal command per planning pass. Gather orders have a 90-tick cadence; resource-allocation goals also avoid reassigning recent Commander gather assignments for 300 ticks. Stable entity-ID ties and fixed placement iteration are retained.

No AI player policy, command serialization, networking protocol, input ownership semantics, LLM/provider integration, voice, or memory system was added or redesigned. The only shared simulation changes extract the existing producer predicate and expose read-only producer/cost queries, including actual civilization substitution and landmark discounts. Construction and training still pay costs and advance through existing gameplay systems.

Primary agent owned architecture, goal/planner changes, simulation query integration, authority decisions, final source review, tests, and runtime acceptance. Luna agents audited production/construction/worker systems and authored initial regression tests; all findings and test changes were reviewed and strengthened by the primary agent.

## B. New goals

- **EnsureUnitCountGoal:** supports Spearman, Archer, and Knight requests; counts resolved civilization variants, living owned units and existing queues; selects compatible least-loaded producers with ID ties; creates missing normal production buildings and Houses; waits for resources or required age; completes only on living units. Population recovery also handles a fully queued target that cannot finish at the current cap.
- **BuildStructureGoal:** House, Barracks, Stable, and Archery Range. Target is the completed-building count at submission plus requested count, extended for earlier pending same-type build goals. Existing unfinished foundations can be recovered. Placement requires visible buildable footprint/border and a complete visible path. Recovery handles dead/interrupted/stalled builders. Normal 36,000-tick goal timeout prevents endless waiting.
- **ResourceAllocationGoal:** Food, Wood, Gold, Stone. Explicit counts mean **at least** that many assigned workers; 9/8 completes without removing one. The Phase 2 enum name `SetExact` is retained for compatibility, but execution follows the brief's at-least completion rule. `Increase` snapshots the current assigned count plus the requested delta, defaulting to one. Assignment count includes gathering, travel to gathering, and drop-off-cycle states on a live node, not mere enqueued commands.

Worker priority remains idle, Commander-controlled gatherers, then other available gatherers. Queued tasks and recent human commands are respected. Human protection is 900 ticks (30 seconds); protected-resource floors are separate and persist for the goal's lifetime. `don't touch gold` freezes the number of assigned gold workers at submission; typed constraints may instead provide `MinimumWorkers`. This protects workers, not the gold stockpile. Idle-only excludes every active worker. Maximum queue is a per-producer total queue-depth cap.

## C. Intent support

Executable examples:

- `make 10 spearmen`, `make 20 archers`, `train 5 knights`
- `build barracks`, `build a stable`, `create a house`, `build an archery range`
- `put 8 villagers on wood`, `move 5 workers to food`, `increase gold workers`
- `make 10 spearmen don't touch gold`
- `build barracks use idle villagers only`
- `make 10 archers do not queue more than 5`

Parser/interpreter remain language-only. Validation checks schema, ownership, supported identifiers and bounds. Resolver creates goals. Gameplay decisions remain in the goal/planner layer. Queue constraints on non-production intents and invalid constraints are rejected rather than silently ignored.

## D. Tests

- Baseline EditMode: 88/88 passed.
- Final EditMode: **119/119 passed**, 0 failed, 0 skipped; job `f8ed074f3a4144d4a3c6b9f8ce9ed3a6`.
- Phase 1: 29 passed; Phase 2 intent tests: 40 passed; Phase 3A: 29 cases passed; other regressions: 21 passed.
- PlayMode: **1/1 passed**, job `461640cd1cb34a339c8f5c19c773c229`.
- Unity compilation: no C# errors in the final build log; loaded API verified by reflection. Existing unrelated obsolete-API/serialization warnings remain.
- `git diff --check`: passed (line-ending warnings only).

The original unit-death test was given spare House capacity to isolate death/queue behavior. Its original assertions remain intact. The new full-population queued-target test separately proves the corrected capacity recovery. Phase 2's intentional deferral assertions were updated to assert typed executable goals. Initial failed/old-assembly runs are not counted as final evidence.

| Test | Purpose | Result |
|---|---|---|
| `BuildStructureGoal_BuildsBarracks` | Normal placement, resource deduction, construction ticks, and completed building. | Passed |
| `BuildStructureGoal_DoesNotCompleteOnEnqueue` | A queued command alone cannot complete construction. | Passed |
| `BuildStructureGoal_RecoversFromBuilderDeath` | An eligible backup receives the normal resume-construction command. | Passed |
| `BuildStructureGoal_RejectsInvalidBuilding` | Unknown building type fails without placement. | Passed |
| `BuildStructureGoal_RejectsUnreachablePlacement` | A partial/disconnected path cannot authorize a foundation. | Passed |
| `BuildStructureGoal_TwoQueuedRequestsUseDistinctTargets` | Repeated build requests reserve distinct completed-count targets. | Passed |
| `BuildStructureGoal_WaitsForResources` | Insufficient wood causes legitimate gathering, not free construction. | Passed |
| `Constraint_ActiveWorkersOnlyBlocksWithoutIdleFallback` | Idle-only does not take any active worker. | Passed |
| `Constraint_ExplicitProtectedFloorAllowsSurplusButNotBelowFloor` | A resource floor permits surplus reassignment but protects its minimum. | Passed |
| `Constraint_MaxQueueLimitsProduction` | The configured per-building queue cap stops additional orders. | Passed |
| `Constraint_ProtectsResourceWorkers` | Submission-time resource-worker snapshot prevents reassignment. | Passed |
| `Constraint_UsesIdleWorkersOnly` | Idle worker is selected instead of active workers. | Passed |
| `EnsureUnitCount_AccountsForExistingTrainingQueue` | Existing queue contributes to the requested total. | Passed |
| `EnsureUnitCount_AlreadySatisfied_CompletesImmediately` | Existing living units complete the goal without more training. | Passed |
| `EnsureUnitCount_Archer_Works` | Normal cost deduction and training produce a living Archer and complete the goal. | Passed |
| `EnsureUnitCount_FullPopulationWithQueuedTargetRequestsHouse` | Capacity recovery still occurs when enough target units are already queued. | Passed |
| `EnsureUnitCount_Knight_WaitsForAgeThenResumes` | Knight training waits for the canonical age and resumes afterward. | Passed |
| `EnsureUnitCount_Knight_Works` | Normal gold cost and training produce a living Knight and complete the goal. | Passed |
| `EnsureUnitCount_MissingArcherRangeRequestsProductionBuildingAfterAge2` | Missing producer is requested through normal construction after eligibility. | Passed |
| `ProductionPlanner_DistributesAcrossAvailableBuildings` | Least-loaded compatible producers receive deterministic distributed orders. | Passed |
| `ResourceAllocation_AssignsIdleWorkers(Food)` | An idle worker receives a normal gather command for the requested resource. | Passed |
| `ResourceAllocation_AssignsIdleWorkers(Wood)` | An idle worker receives a normal gather command for the requested resource. | Passed |
| `ResourceAllocation_AssignsIdleWorkers(Gold)` | An idle worker receives a normal gather command for the requested resource. | Passed |
| `ResourceAllocation_AssignsIdleWorkers(Stone)` | An idle worker receives a normal gather command for the requested resource. | Passed |
| `ResourceAllocation_CompletesWhenTargetReached` | Completion depends on current assignment state. | Passed |
| `ResourceAllocation_DoesNotThrashWorkers` | Planning cadence/cooldown suppresses repeated worker orders. | Passed |
| `ResourceAllocation_IncreaseSnapshotsOneAdditionalWorker` | Unspecified increase fixes current count plus one at submission. | Passed |
| `ResourceAllocation_RespectsHumanControlledWorkers` | Fresh human assignments are excluded from Commander selection. | Passed |
| `ResourceAllocation_TargetNineOfEightWorkersIsAlreadyComplete` | At-least semantics do not remove excess assigned workers. | Passed |

The complete per-test result inventory is in [CommanderPhase3A-test-results.json](CommanderPhase3A-test-results.json).

## E. Runtime QA

All four scenarios ran in the live SampleScene through `CommanderIntentDebugSession` and the normal GameBootstrapper command flow. Editor-only fixture setup uses a French Age 3 player, a starting resource budget, four normally queued villagers, and no enemy AI. No construction/training times are bypassed. Scenario 4 starts with zero wood and waits beyond the human-protection lease before testing the resource floor.

| Input | Observed completion response | Actual result |
|---|---|---|
| `make 10 archers` | Your 10 archers are ready. | Missing Archery Range and capacity built normally; 10 living Archers, completed at tick 4531. |
| `build barracks` | The barracks is complete. | One normal placement order, completed Barracks at tick 5476. |
| `put 8 villagers on wood` | At least 8 villagers are assigned to wood. | Eight gather orders, 8 wood workers at tick 6136; manually gold-assigned worker #0 was not commandeered. |
| `make 10 spearmen don't touch gold` | Your 10 spearmen are ready. | Gathered from zero wood, trained 10 living Spearmen; 2 protected gold workers remained on gold throughout, completed at tick 10276. |

Final observed state: 10 Archers, 10 Spearmen, 8 wood workers, 2 protected gold workers, no human-control or protection violation. Raw logs and reproduction steps: [runtime evidence](CommanderPhase3A-runtime-evidence.md). [Final screenshot](../Assets/Screenshots/screenshot-20260904-134920.png).

## F. Remaining limitations

- No automatic age-up or technology research: unavailable ages wait with an explicit prerequisite response until the player advances; normal timeouts still apply.
- Production vocabulary is Spearman/Archer/Knight and canonical civilization substitutions. Construction vocabulary is House/Barracks/Stable/Archery Range; special landmarks, walls, and arbitrary placement are not part of this phase.
- Goals are one-shot and FIFO, not perpetual army/economy maintenance. Completed goals do not restart after later deaths or player reassignment.
- Allocation completion means an actual accepted gather assignment, including travel/drop-off, not a guarantee that every worker is simultaneously harvesting. It does not reduce workers when already above target.
- The Phase 2 grammar permits one protected-resource constraint per request. No automatic scouting is added; planning uses visible reachable resources and visible construction paths.
- No live two-client multiplayer session was run. Serialization and ownership regressions passed, and intent/goal state remains local. Existing AI policy was not modified; no separate bot-match runtime acceptance is claimed.
- Future Phase 3B provider/readiness work is untouched. No LLM, API, speech, personality, or memory integration exists in this delta.

## Files in this Phase 3A delta

New: `Assets/Editor/CommanderPhase3AQa.cs`, `Assets/Tests/EditMode/CommanderPhase3ATests.cs`, their Unity metadata, this report and evidence files, and QA screenshots.

Modified: `CommanderGoal.cs`, `CommanderGoalManager.cs`, `CommanderPlanner.cs`, `CommanderWorkerAuthority.cs`; Phase 2 `CommanderIntent.cs`, `CommanderIntentCatalog.cs`, `CommanderIntentResolver.cs`, `CommanderIntentValidator.cs`, `CommanderResponseGenerator.cs`, `SimpleTextIntentParser.cs`; `GameSimulation.cs`; `CommanderPhase1Tests.cs` and `CommanderIntentTests.cs`.

Pre-existing Phase 2 uncommitted files were preserved. No commits or unrelated gameplay/asset changes were made.

