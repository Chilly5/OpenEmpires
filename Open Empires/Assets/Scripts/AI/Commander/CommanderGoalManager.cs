using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class CommanderGoalManager : IDisposable
    {
        public const int MaxActiveGoals = 64;
        public const int MaxArchivedGoals = 50;
        private const int PlanningIntervalTicks = 15;
        private const int BlockedRetryIntervalTicks = 150; // Five seconds at 30 Hz.
        private const int BlockedTimeoutTicks = 1800; // One minute continuously unresolved.
        private readonly GameSimulation simulation;
        private readonly int playerId;
        private readonly CommanderPlanner planner;
        private readonly CommanderWorkerAuthority workerAuthority;
        private readonly List<CommanderGoal> goals = new List<CommanderGoal>();
        private readonly List<CommanderGoal> activeGoals = new List<CommanderGoal>();
        private readonly List<CommanderGoal> archivedGoals = new List<CommanderGoal>();
        private readonly HashSet<int> suspendedGoalIds = new HashSet<int>();
        private int nextGoalId = 1;
        private int lastEvaluatedTick = -1;
        private bool isTicking;
        private bool disposed;

        public IReadOnlyList<CommanderGoal> Goals => goals;
        public IReadOnlyList<CommanderGoal> ActiveGoals => activeGoals;
        public IReadOnlyList<CommanderGoal> ArchivedGoals => archivedGoals;
        public int DiagnosticPathCheckCount => planner.DiagnosticPathCheckCount;
        public void ResetDiagnosticPathCheckCount() => planner.ResetDiagnosticPathCheckCount();
        public int PlayerId => playerId;
        internal GameSimulation Simulation => simulation;
        public int CurrentTick => simulation.CurrentTick;
        public CommanderGoal ActiveGoal { get; private set; }
        public event Action<CommanderGoal> GoalStatusChanged;
        public event Action<CommanderGoalEvent> GoalEventPublished;

        public CommanderGoal GetGoal(int goalId)
        {
            for (int i = 0; i < goals.Count; i++) if (goals[i].GoalId == goalId) return goals[i];
            return null;
        }

        // Called only with child IDs selected by StrategicPlanner, never from player text.
        internal void SuspendGoal(int goalId)
        {
            CommanderGoal goal = GetGoal(goalId);
            if (goal == null || goal.IsTerminal) return;
            suspendedGoalIds.Add(goalId);
            if (ActiveGoal == goal) ActiveGoal = null;
        }

        internal bool CanResumeGoal(int goalId, int pausedTicks)
        {
            CommanderGoal goal = GetGoal(goalId);
            if (pausedTicks < 0 || goal == null || goal.IsTerminal || !suspendedGoalIds.Contains(goalId))
                return pausedTicks >= 0;
            return CanShift(goal.CreatedTick, pausedTicks)
                && (goal.BlockedSinceTick < 0 || CanShift(goal.BlockedSinceTick, pausedTicks))
                && (goal.BlockedSinceTick < 0 || CanShift(goal.NextBlockedRetryTick, pausedTicks))
                && (goal.LastEconomyCommandTick == int.MinValue / 2
                    || CanShift(goal.LastEconomyCommandTick, pausedTicks))
                && (goal.ObservedConstructionBuildingId < 0
                    || CanShift(goal.LastConstructionProgressTick, pausedTicks))
                && (goal.LastConstructionRecoveryTick == int.MinValue / 2
                    || CanShift(goal.LastConstructionRecoveryTick, pausedTicks));
        }

        internal void ResumeGoal(int goalId, int pausedTicks)
        {
            CommanderGoal goal = GetGoal(goalId);
            if (goal == null || goal.IsTerminal || !suspendedGoalIds.Contains(goalId)) return;
            if (!CanResumeGoal(goalId, pausedTicks))
                throw new InvalidOperationException("A suspended goal tick anchor cannot be shifted safely.");
            goal.CreatedTick = checked(goal.CreatedTick + pausedTicks);
            if (goal.BlockedSinceTick >= 0)
            {
                goal.BlockedSinceTick = checked(goal.BlockedSinceTick + pausedTicks);
                goal.NextBlockedRetryTick = checked(goal.NextBlockedRetryTick + pausedTicks);
            }
            if (goal.LastEconomyCommandTick != int.MinValue / 2)
                goal.LastEconomyCommandTick = checked(goal.LastEconomyCommandTick + pausedTicks);
            if (goal.ObservedConstructionBuildingId >= 0)
                goal.LastConstructionProgressTick = checked(goal.LastConstructionProgressTick + pausedTicks);
            if (goal.LastConstructionRecoveryTick != int.MinValue / 2)
                goal.LastConstructionRecoveryTick = checked(goal.LastConstructionRecoveryTick + pausedTicks);
            suspendedGoalIds.Remove(goalId);
        }

        private static bool CanShift(int anchor, int delta) => (long)anchor + delta <= int.MaxValue;

        public CommanderWorkerReservation? GetWorkerReservation(int workerId) => workerAuthority.GetReservation(workerId);

        public bool TryReserveWorker(int goalId, int workerId, CommanderWorkerReservationType reservationType)
        {
            CommanderGoal goal = GetGoal(goalId);
            return goal != null && !goal.IsTerminal && Enum.IsDefined(typeof(CommanderWorkerReservationType), reservationType)
                && workerAuthority.TryReserve(workerId, goalId, reservationType, simulation.CurrentTick);
        }

        public CommanderGoalManager(GameSimulation simulation, int playerId,
            int pathValidationCandidates = CommanderPlanner.DefaultPathValidationCandidates)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.playerId = playerId;
            workerAuthority = new CommanderWorkerAuthority(simulation, playerId);
            simulation.CommandBuffer.CommandEnqueued += HandleCommandEnqueued;
            simulation.OnBuildingPlacedFromCommand += HandleBuildingPlacedFromCommand;
            planner = new CommanderPlanner(simulation, workerAuthority,
                pathValidationCandidates);
        }

        public EnsureUnitCountGoal SubmitEnsureUnitCount(int requestedUnitType, int targetTotal,
            int maxQueueDepth = 3, int maxDurationTicks = 36000,
            IReadOnlyList<CommanderConstraint> constraints = null)
        {
            var goal = new EnsureUnitCountGoal(playerId, requestedUnitType, targetTotal,
                maxQueueDepth, maxDurationTicks: maxDurationTicks);
            return Register(goal, constraints);
        }

        public BuildStructureGoal SubmitBuildStructure(BuildingType type, int count = 1,
            int maxDurationTicks = 36000, IReadOnlyList<CommanderConstraint> constraints = null,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null)
        {
            if (placementAnchorSelector.HasValue || placementAnchorOrdinal.HasValue
                || placementRelation.HasValue || clearGapTiles.HasValue)
            {
                var placedIntent = new BuildStructureIntent(playerId, type, count, constraints,
                    placementAnchorSelector, placementAnchorOrdinal, placementRelation, clearGapTiles);
                CommanderIntentValidationResult validation = new CommanderIntentValidator().Validate(
                    placedIntent, simulation, playerId);
                if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(placementAnchorSelector));
            }
            var goal = new BuildStructureGoal(playerId, type, count, maxDurationTicks,
                placementAnchorSelector, placementAnchorOrdinal, placementRelation, clearGapTiles);
            goal.TargetTotal = planner.CountCompletedBuildings(playerId, type) + count;
            for (int i = 0; i < goals.Count; i++)
                if (goals[i] is BuildStructureGoal earlier && !earlier.IsTerminal && earlier.StructureType == type)
                    goal.TargetTotal = Math.Max(goal.TargetTotal, earlier.TargetTotal + count);
            return Register(goal, constraints);
        }

        public ResourceAllocationGoal SubmitResourceAllocation(ResourceType resource, int? workers,
            ResourceAllocationMode mode = ResourceAllocationMode.SetExact, int maxDurationTicks = 36000,
            IReadOnlyList<CommanderConstraint> constraints = null)
        {
            if (!Enum.IsDefined(typeof(ResourceType), resource) || !Enum.IsDefined(typeof(ResourceAllocationMode), mode))
                throw new ArgumentOutOfRangeException(nameof(resource));
            int target = mode == ResourceAllocationMode.Increase
                ? planner.CountResourceWorkers(playerId, resource) + (workers ?? 1)
                : workers ?? throw new ArgumentNullException(nameof(workers));
            if (target < 0 || target > simulation.Config.MaxPopulation)
                throw new ArgumentOutOfRangeException(nameof(workers));
            return Register(new ResourceAllocationGoal(playerId, resource, target, maxDurationTicks), constraints);
        }

        // Atomic game-side commit for a preflighted semantic graph. The graph admission
        // layer owns JSON/reference validation; this method owns goal creation and result
        // linkage. No provider data can reach this method without a trusted plan.
        public IReadOnlyList<CommanderGoal> SubmitSemanticGraph(CommanderSemanticGraphPlan plan,
            int maxDurationTicks = 36000)
        {
            ThrowIfDisposed();
            if (plan == null || plan.Nodes == null || plan.Nodes.Count < 1
                || plan.Nodes.Count > CommanderSemanticGraphAdmission.MaximumNodes)
                throw new ArgumentException("The semantic graph is unavailable.", nameof(plan));
            if (activeGoals.Count > MaxActiveGoals - plan.Nodes.Count)
                throw new InvalidOperationException("The active Commander goal limit has been reached.");

            var pending = new List<CommanderGoal>(plan.Nodes.Count);
            var byIndex = new Dictionary<int, CommanderGoal>();
            var validator = new CommanderIntentValidator();
            for (int order = 0; order < plan.TopologicalOrder.Count; order++)
            {
                int index = plan.TopologicalOrder[order];
                CommanderSemanticGraphNode node = plan.Nodes[index];
                CommanderIntentValidationResult validation = validator.Validate(node.Intent, simulation, playerId);
                if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(plan));
                CommanderGoal goal;
                if (node.Intent is EnsureUnitCountIntent ensure)
                {
                    int maxQueue = 3;
                    for (int i = 0; i < ensure.Constraints.Count; i++)
                        if (ensure.Constraints[i] is MaximumQueueConstraint queue) maxQueue = queue.MaximumQueue;
                    goal = new EnsureUnitCountGoal(playerId, ensure.UnitType, ensure.TargetTotal,
                        maxQueue, maxDurationTicks: maxDurationTicks);
                }
                else if (node.Intent is BuildStructureIntent build)
                {
                    goal = new BuildStructureGoal(playerId, build.StructureType, build.Count,
                        maxDurationTicks, build.PlacementAnchorSelector, build.PlacementAnchorOrdinal,
                        build.PlacementRelation, build.ClearGapTiles);
                    var structureGoal = (BuildStructureGoal)goal;
                    structureGoal.TargetTotal = planner.CountCompletedBuildings(playerId, build.StructureType)
                        + build.Count;
                    for (int i = 0; i < pending.Count; i++)
                        if (pending[i] is BuildStructureGoal earlier && !earlier.IsTerminal
                            && earlier.StructureType == build.StructureType)
                            structureGoal.TargetTotal = Math.Max(structureGoal.TargetTotal,
                                earlier.TargetTotal + build.Count);
                }
                else if (node.Intent is SetResourceAllocationIntent allocation)
                {
                    int target = allocation.Mode == ResourceAllocationMode.Increase
                        ? planner.CountResourceWorkers(playerId, allocation.Resource) + (allocation.WorkerCount ?? 1)
                        : allocation.WorkerCount ?? throw new ArgumentException("Worker count is required.", nameof(plan));
                    goal = new ResourceAllocationGoal(playerId, allocation.Resource, target, maxDurationTicks);
                }
                else throw new ArgumentException("Unsupported compound intent.", nameof(plan));
                planner.CaptureConstraints(goal, node.Intent.Constraints);
                pending.Add(goal);
                byIndex.Add(index, goal);
            }

            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                CommanderSemanticGraphNode node = plan.Nodes[i];
                if (!node.ProducerFromNode.HasValue) continue;
                var dependent = byIndex[i] as EnsureUnitCountGoal;
                var producer = byIndex[node.ProducerFromNode.Value] as BuildStructureGoal;
                if (dependent == null || producer == null)
                    throw new ArgumentException("The producer link is invalid.", nameof(plan));
                dependent.RequiredProducerGoal = producer;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                CommanderGoal goal = pending[i];
                goal.GoalId = nextGoalId++;
                goal.CreatedTick = simulation.CurrentTick;
                goals.Add(goal);
                activeGoals.Add(goal);
            }
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = pending[0];
            for (int i = 0; i < pending.Count; i++)
            {
                CommanderGoal goal = pending[i];
                Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: {goal.GoalType}");
                PublishEvent(CommanderGoalEventType.GoalStarted, goal, simulation.CurrentTick);
            }
            return pending.AsReadOnly();
        }

        private T Register<T>(T goal, IReadOnlyList<CommanderConstraint> constraints) where T : CommanderGoal
        {
            ThrowIfDisposed();
            if (activeGoals.Count >= MaxActiveGoals)
                throw new InvalidOperationException(
                    $"The active Commander goal limit of {MaxActiveGoals} has been reached.");
            goal.GoalId = nextGoalId++;
            goal.CreatedTick = simulation.CurrentTick;
            planner.CaptureConstraints(goal, constraints);
            goals.Add(goal);
            activeGoals.Add(goal);
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = goal;
            Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: {goal.GoalType}");
            PublishEvent(CommanderGoalEventType.GoalStarted, goal, simulation.CurrentTick);
            return goal;
        }

        private void ArchiveGoal(CommanderGoal goal)
        {
            if (!archivedGoals.Contains(goal))
            {
                if (archivedGoals.Count >= MaxArchivedGoals)
                {
                    CommanderGoal removed = archivedGoals[0];
                    archivedGoals.RemoveAt(0);
                    goals.Remove(removed);
                }
                archivedGoals.Add(goal);
            }
        }

        private void CleanupTerminalGoals()
        {
            for (int i = activeGoals.Count - 1; i >= 0; i--)
            {
                if (activeGoals[i].IsTerminal)
                {
                    ArchiveGoal(activeGoals[i]);
                    activeGoals.RemoveAt(i);
                }
            }
        }

        public bool CancelGoal(int goalId)
        {
            ThrowIfDisposed();
            for (int i = 0; i < goals.Count; i++)
            {
                CommanderGoal goal = goals[i];
                if (goal.GoalId != goalId || goal.IsTerminal) continue;
                suspendedGoalIds.Remove(goalId);
                if (goal is BuildStructureGoal cancelledBuild)
                    cancelledBuild.PendingPlacementCommand = null;
                goal.SetStatus(CommanderGoalStatus.Cancelled, "Cancelled by the owning player.");
                workerAuthority.ReleaseGoal(goal.GoalId);
                ArchiveGoal(goal);
                if (ActiveGoal == goal) ActiveGoal = null;
                Debug.Log($"[Commander] Goal #{goal.GoalId} cancelled.");
                Exception observerError = null;
                try { GoalStatusChanged?.Invoke(goal); }
                catch (Exception error) { observerError = error; }
                try { PublishEvent(CommanderGoalEventType.GoalCancelled, goal, simulation.CurrentTick); }
                catch (Exception error) { if (observerError == null) observerError = error; }
                finally { if (!isTicking) CleanupTerminalGoals(); }
                if (observerError != null) ExceptionDispatchInfo.Capture(observerError).Throw();
                return true;
            }
            return false;
        }

        public void Tick(int currentTick)
        {
            ThrowIfDisposed();
            if (currentTick == lastEvaluatedTick) return;
            if (lastEvaluatedTick >= 0 && currentTick % PlanningIntervalTicks != 0) return;
            lastEvaluatedTick = currentTick;
            workerAuthority.PruneUnavailableWorkers();

            isTicking = true;
            try
            {
                // Duration limits also apply while goals are deferred or waiting in the queue.
                ActiveGoal = null;
                for (int i = 0; i < activeGoals.Count; i++)
                {
                    CommanderGoal goal = activeGoals[i];
                    if (!goal.IsTerminal && !suspendedGoalIds.Contains(goal.GoalId)
                        && goal.MaxDurationTicks > 0
                        && currentTick - goal.CreatedTick >= goal.MaxDurationTicks)
                        FailGoal(goal, $"Goal exceeded its {goal.MaxDurationTicks}-tick duration limit.", currentTick);
                }

                // FIFO among runnable goals. Blocked retries keep their original place, but
                // every no-command wait yields immediately to later requests.
                // At most one ordinary ICommand is emitted during a planning tick.
                for (int i = 0; i < activeGoals.Count; i++)
                {
                    CommanderGoal goal = activeGoals[i];
                    if (goal.IsTerminal || suspendedGoalIds.Contains(goal.GoalId)
                        || (goal.Status == CommanderGoalStatus.Blocked
                        && currentTick < goal.NextBlockedRetryTick)) continue;
                    CommanderPlan plan = planner.Plan(goal, currentTick);
                    if (plan.Command != null && !workerAuthority.TryReserveCommand(goal, plan.Command, currentTick))
                        plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                            "Worker is protected or reserved by another goal.", plan.OwnedCount, plan.QueuedCount);
                    goal.LastObservedOwnedCount = plan.OwnedCount;
                    goal.LastObservedQueuedCount = plan.QueuedCount;
                    if (plan.Status == CommanderGoalStatus.Blocked)
                    {
                        if (goal.BlockedSinceTick < 0) goal.BlockedSinceTick = currentTick;
                        // Re-plan before failing, so a condition resolved at the deadline can recover.
                        if (currentTick - goal.BlockedSinceTick >= BlockedTimeoutTicks)
                        {
                            FailGoal(goal, $"Blocked for {BlockedTimeoutTicks} ticks. {plan.Reason}", currentTick);
                            continue;
                        }
                        goal.NextBlockedRetryTick = currentTick + BlockedRetryIntervalTicks;
                    }
                    else goal.BlockedSinceTick = -1;

                    bool changed = goal.SetStatus(plan.Status, plan.Reason);
                    if (goal.IsTerminal)
                    {
                        workerAuthority.ReleaseGoal(goal.GoalId);
                        ArchiveGoal(goal);
                    }
                    if (plan.Command != null && !goal.IsTerminal)
                    {
                        if (goal is BuildStructureGoal spatial && spatial.HasSemanticPlacement
                            && plan.Command is PlaceBuildingCommand placement)
                        {
                            spatial.PlacedTileX = placement.TileX;
                            spatial.PlacedTileZ = placement.TileZ;
                            spatial.PlacementIssuedTick = currentTick;
                            spatial.PlacementIssuedSimulationTick = simulation.CurrentTick;
                            spatial.PendingPlacementCommand = plan.Command;
                        }
                        simulation.CommandBuffer.EnqueueCommand(plan.Command, CommandEnqueueSource.Commander);
                        if (plan.Command is GatherCommand) goal.LastEconomyCommandTick = currentTick;
                        if (plan.Command is ConstructBuildingCommand)
                        {
                            goal.LastConstructionRecoveryTick = currentTick;
                            goal.ConstructionBuilderInRange = false;
                        }
                    }
                    if (changed || plan.Command != null)
                    {
                        Debug.Log($"[Commander] Goal #{goal.GoalId}: status={goal.Status} "
                            + $"owned={plan.OwnedCount} queued={plan.QueuedCount}; {plan.Reason}");
                        GoalStatusChanged?.Invoke(goal);
                        PublishEvent(GetEventType(goal.Status), goal, currentTick);
                    }
                    if (plan.Command != null)
                    {
                        ActiveGoal = goal;
                        return;
                    }
                    // ActiveGoal is a compatibility/UI pointer, not an execution lock.
                    // Evaluate later goals when this one has no command, including all waiting states.
                    if (!goal.IsTerminal && goal.Status != CommanderGoalStatus.Blocked && ActiveGoal == null)
                        ActiveGoal = goal;
                }
            }
            finally
            {
                isTicking = false;
                CleanupTerminalGoals();
            }
        }

        private void FailGoal(CommanderGoal goal, string reason, int currentTick)
        {
            goal.SetStatus(CommanderGoalStatus.Failed, reason);
            if (goal is BuildStructureGoal failedBuild)
                failedBuild.PendingPlacementCommand = null;
            workerAuthority.ReleaseGoal(goal.GoalId);
            ArchiveGoal(goal);
            Debug.LogWarning($"[Commander] Goal #{goal.GoalId} failed: {goal.StatusReason}");
            GoalStatusChanged?.Invoke(goal);
            PublishEvent(CommanderGoalEventType.GoalFailed, goal, currentTick);
        }

        private void HandleCommandEnqueued(ICommand command, CommandEnqueueSource source)
        {
            workerAuthority.ObserveEnqueuedCommand(command, source, simulation.CurrentTick);
        }

        private void HandleBuildingPlacedFromCommand(ICommand command, BuildingData created)
        {
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (!(activeGoals[i] is BuildStructureGoal goal) || !goal.HasSemanticPlacement
                    || goal.IsTerminal || !ReferenceEquals(goal.PendingPlacementCommand, command))
                    continue;
                goal.PendingPlacementCommand = null;
                if (created.PlayerId == goal.PlayerId && !created.IsDestroyed
                    && simulation.GetEffectiveBuildingType(created) == goal.StructureType
                    && created.OriginTileX == goal.PlacedTileX
                    && created.OriginTileZ == goal.PlacedTileZ)
                    goal.PlacedBuildingId = created.Id;
                return;
            }
        }

        private void PublishEvent(CommanderGoalEventType type, CommanderGoal goal, int tick)
        {
            GoalEventPublished?.Invoke(new CommanderGoalEvent(type, tick, goal));
        }

        private static CommanderGoalEventType GetEventType(CommanderGoalStatus status)
        {
            switch (status)
            {
                case CommanderGoalStatus.Blocked: return CommanderGoalEventType.GoalBlocked;
                case CommanderGoalStatus.Completed: return CommanderGoalEventType.GoalCompleted;
                case CommanderGoalStatus.Failed: return CommanderGoalEventType.GoalFailed;
                case CommanderGoalStatus.Cancelled: return CommanderGoalEventType.GoalCancelled;
                default: return CommanderGoalEventType.GoalProgressChanged;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            simulation.CommandBuffer.CommandEnqueued -= HandleCommandEnqueued;
            simulation.OnBuildingPlacedFromCommand -= HandleBuildingPlacedFromCommand;
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (activeGoals[i] is BuildStructureGoal spatial)
                    spatial.PendingPlacementCommand = null;
                workerAuthority.ReleaseGoal(activeGoals[i].GoalId);
            }
            suspendedGoalIds.Clear();
            GoalStatusChanged = null;
            GoalEventPublished = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CommanderGoalManager));
        }

    }
}
