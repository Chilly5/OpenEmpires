# Commander Phase 3C preparation — multi-goal tactical foundation

## What changed

The local goal manager now continues past every no-command waiting state. Goals waiting for resources, production, construction or an age prerequisite no longer stop later runnable goals. Blocked retries retain the existing 150-tick interval and 1800-tick continuous-block timeout. Overall goal duration limits still apply independently.

Scheduling remains deterministic FIFO by registration order. The manager uses its existing 15-simulation-tick cadence and emits at most one normal command in an interval. A goal that emits a command consumes that interval; a goal that waits without a command yields to the next goal. This is bounded tactical scheduling, not parallel gameplay execution, a priority scheduler or strategic planning. `ActiveGoal` remains a compatibility/debug pointer, not an execution lock.

Every goal retains a stable `GoalId`, owning `PlayerId`, creation tick and detailed status. IDs are monotonic and never reused during a manager's lifetime; identity across players/matches is the owner/manager plus goal ID. `GetGoal(id)` supports independent lookup, including terminal goals. Common `Priority` and nullable `ParentGoalId` are metadata only. Parent execution is not implemented and priority does not reorder goals. The new `Lifecycle` property projects existing detailed statuses into Created, Active, Waiting, Blocked, Completed, Failed and Cancelled, without renaming or renumbering the existing status enum.

## Worker ownership and conflicts

`CommanderWorkerReservation` is an immutable local value with WorkerId, PlayerId, GoalId, ReservationType (Gatherer/Builder) and CreatedTick. The authority layer owns the reservation table. The manager exposes read-only lookup and a validated explicit reservation method for future tactical callers.

Before a worker command is enqueued, every subject is checked and then reserved atomically. Existing ownership is exclusive: another live goal cannot select or reserve that worker. The owning goal may reuse the worker or change its role. All three existing worker selection paths—economy, building placement and construction recovery—retain their old human, idle-only, queue, resource-floor and reachability checks, with one additional cross-goal ownership veto.

Human subject commands synchronously release goal ownership at enqueue, before simulation execution, and retain the unchanged 900-tick human-protection window. A reservation cannot bypass a queued human order or protected resource floor. The existing 300-tick recent-gather cooldown remains intact.

Reservations release on completion, cancellation or failure. Dead, removed, foreign, non-villager and garrisoned workers are pruned on the manager's scheduled tick. No worker is stopped or redirected merely because a lease ends. Legacy Commander-control preference remains separate from goal ownership, preserving existing worker-selection behavior after a goal finishes.

When workers are scarce, the earlier reservation wins. A conflicting goal waits/retries under the existing blocked policy rather than fighting over the worker. Cancelling/finishing/failing the owning goal makes the worker available, subject to existing human and gather protections. Allocation goals retain their existing one-shot `count >= target` completion semantics; this phase does not introduce persistent economic maintenance.

Already-buffered or replicated commands are not withdrawn by cancellation or a later human order. The normal command sequence remains authoritative; cancellation stops future planning. This preserves existing execution and multiplayer behavior rather than inventing local-only command retraction.

## Resource ownership decision

No resource reservation or `GoalResourceClaim` DTO was added. Goals already have owner/goal identity and typed targets; concrete costs depend on the current tactical action and canonical simulation queries. Emitting one command per planning interval retains normal spend validation and subsequent replanning against live resources. An unused fixed-cost claim would neither enforce an economic budget nor accurately describe changing prerequisites. Resource claims/budget ownership should be introduced alongside an actual strategic consumer later, not as nonfunctional metadata now.

## Preserved layers

`CommanderIntent → CommanderGoal → CommanderGoalManager → CommanderPlanner → ICommand → CommandBuffer → GameSimulation` remains the execution path. Gameplay rules, command execution/serialization, simulation validation and networking were not changed. Reservations and goals remain local; only ordinary deterministic gameplay commands are replicated.

Phase 3B's async interpreter, DTO validation, mock provider and dispatcher remain intact. Safe goal snapshots now also copy owner, creation tick, priority, parent metadata and coarse lifecycle. Context construction is still submission-only, with no per-frame snapshots or interpreter calls, and no mutable goal/reservation/registry objects exposed.

## Modified files

| File | Responsibility/change |
| --- | --- |
| CommanderGoal.cs | Common priority/parent metadata and compatible coarse lifecycle projection. |
| CommanderGoalManager.cs | Yield all no-command waits; independent lookup/reservation API; reserve command subjects; terminal release and scheduled pruning. |
| CommanderWorkerReservation.cs | New immutable goal-scoped reservation identity/role. |
| CommanderWorkerAuthority.cs | Exclusive reservation table, atomic command acquisition, human release, terminal release and unavailable-worker pruning using a reused scratch list. |
| CommanderPlanner.cs | Cross-goal reservation veto in the shared worker-reassignment gate; tactical planning and command construction otherwise preserved. |
| CommanderContext.cs; CommanderContextBuilder.cs | Detached goal identity/lifecycle metadata in the existing safe snapshot. |
| CommanderPhase3CPreparationTests.cs | Multi-goal, identity, lifecycle, ordering, ownership, release and authority EditMode regressions. |
| CommanderPhase3CPreparationPlayModeTests.cs | Three real-tick integration scenarios required by the brief. |
| New script .meta files | Unity-generated asset identities. |
| This report and test-result artifacts | Design decisions, scope boundaries and verification evidence. |

Runtime files are in `Assets/Scripts/AI/Commander`; tests are in the corresponding `Assets/Tests/EditMode` and `PlayMode` directories. No earlier-phase tests were weakened or removed.

## Verification

Verified on 2026-09-05 through Unity MCP in Unity 6000.5.9f1 (`Open Empires@6d7310c7`):

| Gate | Result |
| --- | --- |
| Compilation | Zero C# compiler errors; zero failed nodes in the current Bee build log. |
| Full EditMode suite | **191/191 passed**, 0 failed/skipped, 151.937 seconds. Job `a335cec972064ffdadac71342f5d0e52`. |
| Full PlayMode suite | **19/19 passed**, 0 failed/skipped, 10.484 seconds. Job `b7d079a7cdaf41dda94ad890bc0baea6`. |
| New preparation cases | **21 EditMode + 3 PlayMode passed**, including all named preparation tests below. The ordering test also repeats three times. |
| Earlier Commander phases | Phase 1, Phase 2, Phase 3A, Phase 3A.1 and Phase 3B all passed in the full suites. |
| Execution/network preservation | No changes to command execution/serialization or networking; GameSimulation matches the pre-preparation SHA-256 below. |
| Formatting | `git diff --check` passed. |

All test cases listed below passed. Exact names, purposes (below), result states, durations and runtime outputs are available in [EditMode results](CommanderPhase3CPreparation-editmode-results.json) and [PlayMode results](CommanderPhase3CPreparation-playmode-results.json).

Preserved GameSimulation SHA-256: `4DCDB275C3FDF9D229CDDD4F8867D7B6807D6433C2DE66CFFF7653A948AB1CD4`. The existing earlier-phase dirty worktree was preserved; changes already present before this preparation are not attributed to it.

## Future Phase 3C strategic work — not implemented

StrategicPlanner, parent/child execution, GoalGraph/HTN, strategic economic budgets, resource reservations, LLM strategic reasoning, real provider integration, personality and memory are all deferred. This preparation adds only the tactical coexistence, identity and worker-ownership foundation they would need.

## Test inventory

Parameterized case names and exact results are retained in the verification JSON artifacts. The inventory explains each new test's purpose.

| Test | Purpose |
| --- | --- |
| MultipleGoals_WaitingGoalDoesNotBlockRunnableGoal (4 cases) | Production, construction, prerequisite and resource waits all yield to a later eligible command. |
| MultipleGoals_CompletesIndependentGoals | Two resource goals complete independently and release their own workers. |
| MultipleGoals_DeterministicOrdering (repeated 3 times) | FIFO goal order, lowest eligible worker IDs, same-tick deduplication and 15-tick cadence stay deterministic. |
| MultipleGoals_ConflictingWorkerCommandsResolveSafely | A pending command's reservation blocks another goal; cancellation releases the worker for retry. |
| Goals_HaveStableIdentity | Monotonic nonreused IDs, owner/creation metadata, parent placeholder and priority compatibility. |
| Goals_CanBeTrackedIndependently | Completed and cancelled goals remain separately queryable by identity. |
| GoalLifecycle_RemainsDeterministic | Coarse lifecycle events follow deterministic Created → Waiting → Completed transitions. |
| WorkerReservation_AssignsGoalOwnership | Commands associate workers with stable owner/goal/role values; reacquiring the same role is idempotent. |
| WorkerReservation_BuilderCommandAutomaticallyReservesWorker | Construction recovery commands record Builder ownership before execution. |
| WorkerReservation_DoesNotOverrideHumanCommand | Human enqueue releases ownership immediately, rejects reacquisition and prevents a Commander command. |
| WorkerReservation_PreventsGoalConflict | A second goal cannot acquire another live goal's worker. |
| WorkerReservation_ReleasesAfterGoalCompletion | Completion releases reservations and terminal goals cannot reacquire them. |
| WorkerReservation_ReleasesAfterCancellationOrFailure (2 cases) | Both terminal paths release workers for subsequent goals. |
| WorkerReservation_ReleasesDeadOrGarrisonedWorker (2 cases) | Scheduled pruning clears unavailable-worker reservations. |
| WorkerReservation_RejectsForeignNonWorkerAndUnknownGoal | Ownership, unit kind, valid role and registered-goal checks reject invalid claims. |
| WorkerReservation_DoesNotBypassProtectedResourceFloor | Explicit ownership cannot override the existing protected-resource worker floor. |
| Runtime_BarracksAndWood_BothProgressAndComplete | Real construction and wood assignment coexist, with command-volume checks and terminal release. |
| Runtime_GoldAndWood_NoInfiniteReassignmentLoop | Real-tick simultaneous resource targets settle without worker oscillation. |
| Runtime_ManualAssignmentOverridesGoalReservation | Real human command execution supersedes reservation and retains protection. |

## Required runtime scenarios

| Scenario | Expected | Actual |
| --- | --- | --- |
| Build Barracks + assign wood workers | Both progress while construction waits; bounded command volume. | Wood command at tick 15; both completed by tick 1320; never more than one command per 15-tick interval. |
| Need gold workers + need wood workers | No infinite reassignment loop. | Two workers on each resource; exactly four assignments across 1200 ticks; all reservations released after completion. |
| Player manually assigns reserved villager to gold | Human wins; Commander cannot immediately reclaim. | Reservation released at enqueue; manual gold assignment retained and zero Commander commands through 600 ticks of the unchanged 900-tick window. |

These are Unity PlayMode simulations with explicit flat/visible test-arena setup and normal commands/ticks afterward. They are not a visual UI certification or a two-client multiplayer test.
