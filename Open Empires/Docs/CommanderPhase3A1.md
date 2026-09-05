# Commander Phase 3A.1 — stability hardening

## Scope and architecture

This patch addresses only the three Phase 3A audit issues. The pipeline remains:

`CommanderIntent → validator → resolver → CommanderGoal → CommanderGoalManager → CommanderPlanner → normal ICommand → CommandBuffer → GameSimulation`.

Only three runtime Commander files changed in this phase. No parser, intent, UI, simulation, networking, serializer, bot AI, or gameplay command implementation changed. Existing Phase 3A work was preserved. The Unity MCP skill supplied the compile/readiness/test verification workflow; one read-only sub-agent investigated construction arrival and fog semantics. Architecture and implementation stayed with the primary agent.

## Audit issues and fixes

| Issue | Confirmed cause | Correction |
|---|---|---|
| Explored paths rejected | Path check required current `Visible` visibility | Accept `Visible` and `Explored`; reject hidden (`Unexplored` in this game's enum). Resource targets and placement footprint/border still require current visibility. |
| Hidden corridor skipped by path smoothing | Shared pathfinder returns smoothed waypoints without the start; inspecting only waypoints misses intermediate tiles | Inspect every integer segment, including start-to-first-waypoint and diagonal corner checks, using the same line rules as the existing pathfinder. No fog is revealed or mutated. |
| Travelling builders considered stalled | Progress timer started on foundation observation, irrespective of arrival | Begin the 150-tick stall window at observed arrival within fixed-point footprint-edge reach. Reset on progress; suspend while travelling. Preserve immediate missing/dead-builder recovery and recovery cooldown. Prefer an arrived builder over a lower-ID traveller when several are assigned. |
| Blocked goals freeze later requests | Manager retained one nonterminal blocked active goal | Retain blocked goals in history, yield to later requests, retry every 150 ticks, fail after 1,800 continuously unresolved ticks. Re-plan at the deadline before failing. |

At 30 simulation ticks/second, retry is five seconds and blocked timeout is one minute. Planning still occurs every 15 ticks and emits at most one ordinary command per planning tick. Duplicate calls at the same tick do nothing. FIFO order is retained among runnable goals; deferred goals retain their original position for retries. A successful re-plan resets the blocked episode. Cancellation prevents subsequent retries.

An age-prerequisite wait is not an impossible goal: it yields without becoming `Failed` under the blocked timeout, and is rechecked at the normal planning cadence. The existing overall goal duration limit still applies, now also checked for deferred and queued goals. With default settings, this remains 36,000 ticks. Unsupported goals retain the existing explicit failure path. A travelling builder that never arrives remains subject to that overall duration limit; this patch does not add movement-rescue capabilities.

“Don't touch gold” is unchanged: it protects the goal's captured gold-worker floor, not the gold stockpile. No stockpile constraint or new capability was introduced.

## Files changed in this phase

| File | Purpose and reason |
|---|---|
| `Assets/Scripts/AI/Commander/CommanderGoal.cs` | Store per-goal arrival observation and blocked retry/episode timing; all state stays local to Commander. |
| `Assets/Scripts/AI/Commander/CommanderGoalManager.cs` | Deterministic yield/retry/failure lifecycle and global duration checks, retaining the one-command budget. |
| `Assets/Scripts/AI/Commander/CommanderPlanner.cs` | Known-terrain segment validation and arrival-aware construction stall recovery. |
| `Assets/Tests/EditMode/CommanderPhase1Tests.cs` | Move the existing stalled-builder fixture adjacent to its foundation: its old `Constructing` worker was actually about 4.5 tiles outside reach. Original recovery assertions are retained. |
| `Assets/Tests/EditMode/CommanderPhase3A1Tests.cs` | Add focused fog, construction, and lifecycle regression cases. |
| `Assets/Tests/EditMode/CommanderPhase3A1Tests.cs.meta` | Unity-generated stable asset identity for the new test file. |
| `Assets/Tests/PlayMode/CommanderPhase3A1PlayModeTests.cs` | Reproducible MCP-driven PlayMode runtime scenarios through text dispatcher, normal commands, and actual simulation ticks. |
| `Assets/Tests/PlayMode/CommanderPhase3A1PlayModeTests.cs.meta` | Unity-generated stable asset identity for the new PlayMode test file. |
| `Docs/CommanderPhase3A1.md` | This audit, behavior policy, file inventory, and acceptance report. |
| `Docs/CommanderPhase3A1-test-results.json` | Final named test results and captured runtime evidence. |

## New EditMode regression coverage

All 20 cases below passed in the final full EditMode run; parameterized rows represent separate cases. Every expected result below was asserted against current code. Individual named results are also recorded in `CommanderPhase3A1-test-results.json`.

| Test name | Scenario / expected result |
|---|---|
| `FogPath_VisibleMiddleVisible_UsesKnowledgeBoundary` (2 cases) | Visible–Explored–Visible accepted; Visible–Unexplored–Visible rejected; visibility unchanged. |
| `ResourcePath_ExploredCorridorAccepted_HiddenCorridorRejected` (2 cases) | A real long smoothed resource route works through explored terrain; the same hidden corridor produces no gather command. |
| `ResourceTarget_NotCurrentlyVisible_IsNotSelected` (2 cases) | Neither never-seen nor previously-seen-but-not-visible gold is selected. |
| `ConstructionTravelTime_DoesNotTriggerStall` (2 cases) | Builder 20 tiles away in `MovingToBuild` or out-of-range `Constructing` receives no replacement through tick 600. |
| `ConstructionAfterArrival_StallRecoveryWorks` | Arrival at tick 615 starts the full grace period; no early command, recovery at tick 765, no immediate duplicate. |
| `ConstructionDeadBuilder_StillRecovers` | Builder dies during travel; reachable backup receives the normal construct command at the next planning tick. |
| `ConstructionArrivedBuilder_NotMaskedByLowerIdTraveller` | Lowest-ID traveller does not hide an arrived stalled builder; backup recovery remains possible. |
| `ConstructionProgress_ResetsArrivalStallWindow` | Genuine construction progress resets the grace window. |
| `BlockedGoal_DoesNotFreezeFutureCommands` | Knight goal lacks known gold; later wood goal issues a normal command while the knight goal is retained. |
| `BlockedGoal_RecoversAfterConditionChanges` | Gold becomes available; no retry spam before tick 150, then normal training resumes. |
| `BlockedGoal_FailsAfterTimeout` | Persistent block fails at tick 1,800, with exactly one failure event and no command. |
| `BlockedGoal_ConditionResolvesAtDeadline_Recovers` | Condition resolves exactly at the deadline; final re-plan succeeds instead of failing. |
| `BlockedGoal_Cancelled_DoesNotRetry` | Cancelled blocked goal never resumes when resources arrive. |
| `BlockedRetry_AndLaterGoal_EmitAtMostOneCommandPerTick` | Due retry, later request, and duplicate same-tick call still emit only one command. |
| `AgePrerequisiteWait_YieldsToLaterResourceRequest` | Age-gated knight request retains truthful wait status while wood request can execute. |
| `DeferredAndQueuedGoals_RetainOverallDurationLimit` | Queued goal's explicit duration expires even while another goal is deferred. |

## Verification

Baseline Unity MCP EditMode run: 119/119 passed (`7d641866765f4849ad01875e148d6152`). The first hardening run exposed the hidden smoothed-segment gap; it was not accepted as a final pass. Early fixture/compiler errors were corrected before acceptance runs.

Final acceptance on Unity **6000.5.9f1**, through Unity MCP:

| Gate | Expected | Actual |
|---|---|---|
| C# compilation | Zero compiler errors | **Pass**: final force/all refresh succeeded; latest Bee compiler record has no `error CS` diagnostics. Unrelated existing warnings remain. |
| Phase 1 | No regressions | **29/29 passed** |
| Phase 2 intent/parser/resolver/response | No regressions | **40/40 passed** |
| Phase 3A | No regressions | **29/29 passed** |
| Phase 3A.1 | All new hardening tests | **20/20 passed** |
| Other EditMode tests | No regressions | **21/21 passed** |
| Full EditMode | All tests, no skips | **139/139 passed**, 0 failed, 0 skipped; job `fffca5ee123540beae24beec78263fc6` |
| Full PlayMode | New scenarios plus existing animation test | **4/4 passed**, 0 failed, 0 skipped; job `ad7a317fac2642758e7842a2d4cb6e40` |
| Whitespace validation | Clean patch | `git diff --check` passed; only LF/CRLF notices. |

### Runtime expected versus actual

| PlayMode test | Expected | Actual |
|---|---|---|
| `Runtime_VisibleExploredVisible_CommanderPlansAndWorkerMoves` | Plan a resource route with visible endpoints and explored middle; execute through normal commands | **Passed**: worker moved toward the requested node; allocation completed at tick 15 and remained complete at tick 60. |
| `Runtime_FarFoundation_BuilderTravelsWithoutEarlyReplacement` | Builder 20 tiles away travels past the old stall window without replacement, then builds normally | **Passed**: at tick 211 builder was `MovingToBuild`, all 900 construction ticks remained, zero recovery commands. Foundation completed normally by tick 1,516, still zero recovery commands. |
| `Runtime_ImpossibleGoal_DoesNotBlockLaterWoodRequest` | Impossible knight request at 200/200 population must not freeze a later wood request | **Passed**: wood command issued at tick 15, allocation completed at tick 30; at tick 60 the first goal remained retained and `Blocked`, while wood was `Completed`. |

Captured runtime evidence:

```text
[Phase3A.1 Runtime] PASS far foundation: tick=211, remaining=900, recoveryCommands=0, builderState=MovingToBuild.
[Phase3A.1 Runtime] PASS far foundation completed normally: tick=1516, recoveryCommands=0.
[Phase3A.1 Runtime] PASS blocked queue: tick=60, first=Blocked, wood=Completed, target=0.
[Phase3A.1 Runtime] PASS explored corridor: tick=60, goal=Completed, worker moved, target=0.
```

To reproduce, run all EditMode tests and all PlayMode tests through Unity MCP `run_tests`, then poll the returned job IDs with `get_test_job(include_details: true)`. New runtime tests are in `OpenEmpires.Tests.CommanderPhase3A1PlayModeTests`. Existing tests also retain coverage for ownership, normal command serialization, hidden enemy placement, protected workers, and partial/unreachable paths.

The runtime fixtures intentionally flatten a small deterministic arena, deplete generated resource nodes before adding their own, and create starting assets. The impossible-goal fixture uses the real 200 population ceiling, not altered configuration. After setup, the text dispatcher, command buffer, simulation validation, movement, fog updates, and construction run normally. Tests advance simulation ticks in Unity PlayMode and yield between batches; they do not manually mark goals complete or change construction progress. This is PlayMode simulation proof, not an interactive SampleScene visual test or a two-client multiplayer test.

## Phase boundary

**Phase 3A.1 is complete**, with all acceptance gates above passed. **Phase 3B remains future work and was not started.** No LLM, GPT/DeepSeek/Gemini integration, provider/API calls, voice, memory, or personality system was added.
