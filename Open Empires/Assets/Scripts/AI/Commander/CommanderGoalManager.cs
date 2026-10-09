using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager : IDisposable
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
        internal bool IsDisposed => disposed;
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
        internal bool ObserveHumanProtection(int unitId)=>workerAuthority.ObserveHumanProtection(unitId,simulation.CurrentTick);
        internal bool ObserveGoalSuspended(int goalId)=>suspendedGoalIds.Contains(goalId);

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
            simulation.TrainingOrderAccepted += HandleTrainingAccepted;
            simulation.TrackedUnitProduced += HandleTrackedUnitProduced;
            simulation.TrainingOriginLost += HandleTrainingOriginLost;
            simulation.ProducerUnitProduced += HandleFutureBirth;
            simulation.LocalActionCommandProcessed += HandleFutureCommandProcessed;
            planner = new CommanderPlanner(simulation, workerAuthority,
                pathValidationCandidates);
        }

        public EnsureUnitCountGoal SubmitEnsureUnitCount(int requestedUnitType, int targetTotal,
            int maxQueueDepth = 3, int maxDurationTicks = 36000,
            IReadOnlyList<CommanderConstraint> constraints = null, int? newProductionCount = null)
        {
            if (newProductionCount.HasValue && (newProductionCount.Value < 1 || newProductionCount.Value > simulation.Config.MaxPopulation))
                throw new ArgumentOutOfRangeException(nameof(newProductionCount));
            var goal = new EnsureUnitCountGoal(playerId, requestedUnitType, targetTotal,
                maxQueueDepth, maxDurationTicks: maxDurationTicks);
            if (newProductionCount.HasValue)
            {
                goal.IsExplicitNewProduction = goal.HasResultConsumer = true;
                goal.RequiredNewProductionCount = newProductionCount.Value;
                goal.BaselineUnitIds.UnionWith(CommanderProductionProjection.LivingOwnedIds(simulation, playerId,
                    simulation.ResolveCivUnitType(playerId, requestedUnitType)));
            }
            return Register(goal, constraints);
        }

        public BuildStructureGoal SubmitBuildStructure(BuildingType type, int count = 1,
            int maxDurationTicks = 36000, IReadOnlyList<CommanderConstraint> constraints = null,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null,
            ResourceType? placementResourceType = null)
        {
            if (placementAnchorSelector.HasValue || placementAnchorOrdinal.HasValue
                || placementRelation.HasValue || clearGapTiles.HasValue || placementResourceType.HasValue)
            {
                var placedIntent = new BuildStructureIntent(playerId, type, count, constraints,
                    placementAnchorSelector, placementAnchorOrdinal, placementRelation, clearGapTiles,
                    placementResourceType);
                CommanderIntentValidationResult validation = new CommanderIntentValidator().Validate(
                    placedIntent, simulation, playerId);
                if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(placementAnchorSelector));
            }
            var goal = new BuildStructureGoal(playerId, type, count, maxDurationTicks,
                placementAnchorSelector, placementAnchorOrdinal, placementRelation, clearGapTiles,
                placementResourceType);
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

        public AllocateWorkersGoal SubmitWorkerAllocation(CommanderWorkerAllocation allocation,
            int maxDurationTicks = 36000, IReadOnlyList<CommanderConstraint> constraints = null)
        {
            if (allocation == null || !allocation.IsValid(simulation.Config.MaxPopulation))
                throw new ArgumentException("Invalid worker allocation criteria.", nameof(allocation));
            return Register(new AllocateWorkersGoal(playerId, allocation, maxDurationTicks), constraints);
        }

        public ReachAgeGoal SubmitReachAge(CommanderSemanticAgeTarget requestedTarget,
            int maxDurationTicks = 36000, IReadOnlyList<CommanderConstraint> constraints = null)
        {
            int targetAge = requestedTarget == CommanderSemanticAgeTarget.Next
                ? simulation.GetPlayerAge(playerId) + 1 : (int)requestedTarget;
            var intent = new ReachAgeIntent(playerId, requestedTarget, targetAge, constraints);
            CommanderIntentValidationResult validation = new CommanderIntentValidator().Validate(
                intent, simulation, playerId);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(requestedTarget));
            return Register(new ReachAgeGoal(playerId, requestedTarget, targetAge, maxDurationTicks), constraints);
        }

        public CommanderCapabilityGoal SubmitCapabilityAction(CapabilityActionIntent action,
            int maxDurationTicks = 36000)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            CommanderIntentValidationResult validation = new CommanderIntentValidator().Validate(
                action, simulation, playerId);
            if (!validation.IsValid) throw new ArgumentException(validation.Reason, nameof(action));
            return Register(new CommanderCapabilityGoal(action, maxDurationTicks), action.Constraints);
        }

        // Atomic game-side commit for a preflighted semantic graph. The graph admission
        // layer owns JSON/reference validation; this method owns goal creation and result
        // linkage. No provider data can reach this method without a trusted plan.
        public IReadOnlyList<CommanderGoal> SubmitSemanticGraph(CommanderSemanticGraphPlan plan,
            int maxDurationTicks = 36000)
        {
            ThrowIfDisposed();
            if (plan == null || plan.Nodes == null || plan.Nodes.Count < 1
                || plan.Nodes.Count > (plan.DynamicProgram != null ? CommanderDynamicPlan.MaximumNodes : CommanderSemanticGraphAdmission.MaximumNodes))
                throw new ArgumentException("The semantic graph is unavailable.", nameof(plan));
            if (plan.Authorization == null || !plan.Authorization.CanCommit(this, plan))
                throw new InvalidOperationException("The exact compound candidate requires trusted player confirmation.");
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
                    if (ensure.NewProductionCount.HasValue)
                    {
                        ((EnsureUnitCountGoal)goal).IsExplicitNewProduction = true;
                        ((EnsureUnitCountGoal)goal).HasResultConsumer = true;
                        ((EnsureUnitCountGoal)goal).RequiredNewProductionCount = ensure.NewProductionCount.Value;
                    }
                }
                else if (node.Intent is BuildStructureIntent build)
                {
                    goal = new BuildStructureGoal(playerId, build.StructureType, build.Count,
                        maxDurationTicks, build.PlacementAnchorSelector, build.PlacementAnchorOrdinal,
                        build.PlacementRelation, build.ClearGapTiles, build.PlacementResourceType);
                    var structureGoal = (BuildStructureGoal)goal;
                    structureGoal.TargetTotal = planner.CountCompletedBuildings(playerId, build.StructureType)
                        + build.Count;
                    for (int i = 0; i < pending.Count; i++)
                        if (pending[i] is BuildStructureGoal earlier && !earlier.IsTerminal
                            && earlier.StructureType == build.StructureType)
                            structureGoal.TargetTotal = Math.Max(structureGoal.TargetTotal,
                                earlier.TargetTotal + build.Count);
                }
                else if (node.Intent is AllocateWorkersIntent workers)
                {
                    goal = new AllocateWorkersGoal(playerId, workers.Allocation, maxDurationTicks);
                }
                else if (node.Intent is WatchFutureUnitsIntent future)
                {
                    if (!plan.Authorization.FutureProducerBindings.TryGetValue(index, out var producer)
                        || producer == null)
                        throw new ArgumentException("The approved producer binding is unavailable.", nameof(plan));
                    goal = new WatchFutureUnitsGoal(future, producer, maxDurationTicks,
                        simulation.LastProducerBirthOrdinal);
                }
                else if (node.Intent is SetResourceAllocationIntent allocation)
                {
                    int target = allocation.Mode == ResourceAllocationMode.Increase
                        ? planner.CountResourceWorkers(playerId, allocation.Resource) + (allocation.WorkerCount ?? 1)
                        : allocation.WorkerCount ?? throw new ArgumentException("Worker count is required.", nameof(plan));
                    goal = new ResourceAllocationGoal(playerId, allocation.Resource, target, maxDurationTicks);
                }
                else if (node.Intent is ReachAgeIntent reachAge)
                {
                    goal = new ReachAgeGoal(playerId, reachAge.RequestedTarget, reachAge.TargetAge,
                        maxDurationTicks);
                }
                else if (node.Intent is CapabilityActionIntent capability)
                {
                    goal = new CommanderCapabilityGoal(capability, maxDurationTicks);
                }
                else throw new ArgumentException("Unsupported compound intent.", nameof(plan));
                planner.CaptureConstraints(goal, node.Intent.Constraints);
                goal.ConstructionForbidden |= plan.ConstructionForbidden;
                pending.Add(goal);
                byIndex.Add(index, goal);
            }

            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                CommanderSemanticGraphNode node = plan.Nodes[i];
                var dependencies = new List<CommanderGoal>(node.DependsOn.Count);
                for (int d = 0; d < node.DependsOn.Count; d++)
                {
                    if (!byIndex.TryGetValue(node.DependsOn[d], out CommanderGoal dependency))
                        throw new ArgumentException("The semantic dependency is unavailable.", nameof(plan));
                    dependencies.Add(dependency);
                }
                byIndex[node.Index].SetDependencies(dependencies);
                if (node.ProducerFromNode.HasValue)
                {
                    var dependent = byIndex[i] as EnsureUnitCountGoal;
                    var producer = byIndex[node.ProducerFromNode.Value] as BuildStructureGoal;
                    if (dependent == null || producer == null)
                        throw new ArgumentException("The producer link is invalid.", nameof(plan));
                    producer.HasResultConsumer = true;
                    dependent.RequiredProducerGoal = producer;
                }
                if (node.ResultFromNode.HasValue)
                {
                    var capability = byIndex[i] as CommanderCapabilityGoal;
                    CommanderGoal source = byIndex[node.ResultFromNode.Value];
                    if (byIndex[i] is AllocateWorkersGoal allocation && source is EnsureUnitCountGoal produced)
                    {
                        allocation.ResultSourceGoal = produced;
                        produced.HasResultConsumer = true;
                        continue;
                    }
                    if (capability == null) throw new ArgumentException(
                        "Only capability actions may consume a result reference.", nameof(plan));
                    capability.RequiresResultBinding = true;
                    if (source is EnsureUnitCountGoal units)
                    {
                        units.HasResultConsumer = true;
                        capability.ResultSourceGoal = units;
                    }
                    else if (source is BuildStructureGoal building)
                    {
                        building.HasResultConsumer = true;
                        capability.ResultSourceGoal = building;
                    }
                    else throw new ArgumentException("The result reference type is invalid.", nameof(plan));
                }
            }

            PrepareDynamicWorkerBindings(plan, byIndex);
            PrepareDynamicLocationBindings(plan, byIndex);
            CaptureResultBaselines(pending, plan);
            // Preflight and reserve the complete shared snapshot only after all nodes
            // validate. Roll back these local leases if any acquisition/commit fails.
            var reservedInitialGoals = new List<int>();
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    var goal = pending[i];
                    if (goal.FrozenWorkerIds == null) continue;
                    int provisionalId = checked(nextGoalId + i);
                    reservedInitialGoals.Add(provisionalId);
                    foreach (int id in goal.FrozenWorkerIds)
                        if (!workerAuthority.TryReserve(id, provisionalId,
                            goal is BuildStructureGoal ? CommanderWorkerReservationType.Builder
                                : CommanderWorkerReservationType.Gatherer, simulation.CurrentTick))
                            throw new InvalidOperationException("The complete request-bound worker selection is unavailable; no goals were admitted.");
                }
                if (!plan.Authorization.Consume(this, plan))
                    throw new InvalidOperationException("Action plan approval is stale or already consumed.");
            }
            catch
            {
                foreach (int id in reservedInitialGoals) workerAuthority.ReleaseGoal(id);
                throw;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                CommanderGoal goal = pending[i];
                goal.GoalId = nextGoalId++;
                goal.RuntimeOwner = this;
                goal.CreatedTick = simulation.CurrentTick;
                goal.RequestAuthority = plan.Authorization;
                CaptureResourceObjectiveBaseline(goal);
                goal.RequestNodeIndex = plan.TopologicalOrder[i];
                goals.Add(goal);
                activeGoals.Add(goal);
            }
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = pending[0];
            TraceActionPlan(plan.Authorization, "goal-admitted");
            for (int i = 0; i < pending.Count; i++)
            {
                CommanderGoal goal = pending[i];
                Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: {goal.GoalType}");
                PublishCommittedGraphEvent(goal);
            }
            return pending.AsReadOnly();
        }

        private void CaptureResultBaselines(IReadOnlyList<CommanderGoal> pending, CommanderSemanticGraphPlan plan)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i] is EnsureUnitCountGoal units && units.HasResultConsumer)
                {
                    int resolvedType = simulation.ResolveCivUnitType(units.PlayerId, units.RequestedUnitType);
                    units.BaselineUnitIds.UnionWith(CommanderProductionProjection.LivingOwnedIds(simulation, units.PlayerId, resolvedType));
                    var quantity = plan.ProductionExpectations.FirstOrDefault(q => q.NodeIndex == plan.TopologicalOrder[i]);
                    if (quantity == null) throw new InvalidOperationException("The approved production quantity is unavailable.");
                    units.RequiredNewProductionCount = quantity.NewCount;
                    units.ExpectedOtherUnitContribution = quantity.OtherContribution;
                }
                else if (pending[i] is BuildStructureGoal building && building.HasResultConsumer)
                {
                    List<BuildingData> all = simulation.BuildingRegistry.GetAllBuildings();
                    for (int b = 0; b < all.Count; b++)
                    {
                        BuildingData candidate = all[b];
                        if (candidate != null && candidate.PlayerId == building.PlayerId
                            && !candidate.IsDestroyed
                            && simulation.GetEffectiveBuildingType(candidate) == building.StructureType)
                            building.BaselineBuildingIds.Add(candidate.Id);
                    }
                }
            }
        }

        private T Register<T>(T goal, IReadOnlyList<CommanderConstraint> constraints) where T : CommanderGoal
        {
            ThrowIfDisposed();
            if (activeGoals.Count >= MaxActiveGoals)
                throw new InvalidOperationException(
                    $"The active Commander goal limit of {MaxActiveGoals} has been reached.");
            BindKnownAuthorityAtRegistration(goal);
            goal.GoalId = nextGoalId++;
            goal.RuntimeOwner = this;
            goal.CreatedTick = simulation.CurrentTick;
            CaptureResourceObjectiveBaseline(goal);
            planner.CaptureConstraints(goal, constraints);
            goals.Add(goal);
            activeGoals.Add(goal);
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = goal;
            Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: {goal.GoalType}");
            if (goal.RequestAuthority != null) PublishCommittedGraphEvent(goal);
            else PublishEvent(CommanderGoalEventType.GoalStarted, goal, simulation.CurrentTick);
            return goal;
        }

        private void CaptureResourceObjectiveBaseline(CommanderGoal goal)
        {
            if (goal is AllocateWorkersGoal workers && workers.Allocation.ResourceAmount.HasValue)
                workers.GatheredIncomeAtActivation = simulation.ResourceManager.GetGatheredIncome(playerId,
                    workers.Allocation.Destination.Resource);
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
                if (goal is ReachAgeGoal cancelledAge)
                    cancelledAge.PendingAgeUpCommand = null;
                if (goal is WatchFutureUnitsGoal cancelledFuture)
                {
                    SuppressFuturePendingActions(cancelledFuture);
                    cancelledFuture.ReleaseObservations();
                }
                goal.SetStatus(CommanderGoalStatus.Cancelled, "Cancelled by the owning player.");
                ReleaseGoalTrainingOrigins(goal);
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
            workerAuthority.PruneUnavailableWorkers(currentTick);

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
                    if (!AreDependenciesReady(goal, out string dependencyReason, out bool dependencyFailed))
                    {
                        goal.LastPlannerObservationTick=currentTick;
                        if (dependencyFailed)
                        {
                            FailGoal(goal, dependencyReason, currentTick);
                            continue;
                        }
                        bool waitingChanged = goal.SetStatus(CommanderGoalStatus.WaitingForPrerequisite,
                            dependencyReason);
                        if (waitingChanged)
                        {
                            GoalStatusChanged?.Invoke(goal);
                            PublishEvent(CommanderGoalEventType.GoalProgressChanged, goal, currentTick);
                        }
                        if (ActiveGoal == null) ActiveGoal = goal;
                        continue;
                    }
                    CommanderPlan plan = planner.Plan(goal, currentTick);
                    if (plan.Command != null && goal is AllocateWorkersGoal workers && !workers.ReservationsAcquired)
                    {
                        if (workerAuthority.TryReserveWorkers(workers.SelectedWorkerIds, goal.GoalId, currentTick))
                            workers.ReservationsAcquired = true;
                        else plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                            "The full selected worker set could not be reserved; no assignments were issued.", 0, 0);
                    }
                    if (plan.Command != null && !workerAuthority.TryReserveCommand(goal, plan.Command, currentTick))
                        plan = new CommanderPlan(CommanderGoalStatus.Blocked,
                            "Worker is protected or reserved by another goal.", plan.OwnedCount, plan.QueuedCount);
                    goal.LastObservedOwnedCount = plan.OwnedCount;
                    goal.LastPlannerObservationTick=currentTick;
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
                        if (goal is WatchFutureUnitsGoal terminalFuture)
                        {
                            SuppressFuturePendingActions(terminalFuture);
                            terminalFuture.ReleaseObservations();
                        }
                        workerAuthority.ReleaseGoal(goal.GoalId);
                        ArchiveGoal(goal);
                    }
                    if (plan.Command != null && !goal.IsTerminal)
                    {
                        if (goal is BuildStructureGoal spatial
                            && plan.Command is PlaceBuildingCommand placement)
                        {
                            spatial.PlacedTileX = placement.TileX;
                            spatial.PlacedTileZ = placement.TileZ;
                            spatial.PlacementIssuedTick = currentTick;
                            spatial.PlacementIssuedSimulationTick = simulation.CurrentTick;
                            spatial.PendingPlacementCommand = plan.Command;
                        }
                        if (goal is ReachAgeGoal reachAge && plan.Command is PlaceBuildingCommand ageUp)
                        {
                            reachAge.PlacedTileX = ageUp.TileX;
                            reachAge.PlacedTileZ = ageUp.TileZ;
                            reachAge.PendingAgeUpCommand = plan.Command;
                            reachAge.AgeUpIssuedSimulationTick = simulation.CurrentTick;
                        }
                        TrackIssuedTraining(goal, plan.Command);
                        simulation.CommandBuffer.EnqueueCommand(plan.Command, CommandEnqueueSource.Commander);
                        planner.ObservePreparationCommand(goal, plan.Command);
                        if (RequestTracingEnabled && goal.RequestAuthority != null)
                            Debug.Log($"[Commander] request={goal.RequestAuthority.RequestId};root={goal.RequestNodeIndex};goal={goal.GoalId};dispatch={plan.Command.GetType().Name}");
                        if (goal is AllocateWorkersGoal allocation)
                        {
                            allocation.NextCommandGroup++;
                            allocation.LastIssuedSimulationTick = simulation.CurrentTick;
                        }
                        if (goal is CommanderCapabilityGoal capability)
                        {
                            capability.CommandIssued = true;
                            capability.IssuedCommand = plan.Command;
                            capability.CommandIssuedSimulationTick = simulation.CurrentTick;
                        }
                        if (goal is WatchFutureUnitsGoal future)
                        {
                            int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(plan.Command);
                            if (subjects != null)
                                foreach (int id in subjects)
                                {
                                    future.issuedUnitIds.Add(id);
                                    future.pendingCommands[id] = plan.Command;
                                }
                        }
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
            if (goal is WatchFutureUnitsGoal failedFuture)
            {
                SuppressFuturePendingActions(failedFuture);
                failedFuture.ReleaseObservations();
            }
            ReleaseGoalTrainingOrigins(goal);
            if (goal is BuildStructureGoal failedBuild)
                failedBuild.PendingPlacementCommand = null;
            if (goal is ReachAgeGoal failedAge)
                failedAge.PendingAgeUpCommand = null;
            workerAuthority.ReleaseGoal(goal.GoalId);
            ArchiveGoal(goal);
            Debug.LogWarning($"[Commander] Goal #{goal.GoalId} failed: {goal.StatusReason}");
            GoalStatusChanged?.Invoke(goal);
            PublishEvent(CommanderGoalEventType.GoalFailed, goal, currentTick);
        }

        private static bool AreDependenciesReady(CommanderGoal goal, out string reason, out bool failed)
        {
            reason = string.Empty;
            failed = false;
            IReadOnlyList<CommanderGoal> dependencies = goal.Dependencies;
            for (int i = 0; i < dependencies.Count; i++)
            {
                CommanderGoal dependency = dependencies[i];
                if (dependency.Status == CommanderGoalStatus.Failed
                    || dependency.Status == CommanderGoalStatus.Cancelled)
                {
                    failed = true;
                    reason = $"Dependency goal #{dependency.GoalId} {dependency.Status.ToString().ToLowerInvariant()}; dependent execution was cancelled.";
                    return false;
                }
                if (dependency.Status != CommanderGoalStatus.Completed)
                {
                    reason = $"Waiting for dependency goal #{dependency.GoalId} to complete.";
                    return false;
                }
            }
            return true;
        }

        private void HandleCommandEnqueued(ICommand command, CommandEnqueueSource source)
        {
            workerAuthority.ObserveEnqueuedCommand(command, source, simulation.CurrentTick);
            if (source == CommandEnqueueSource.Commander || command.PlayerId != playerId) return;
            int[] subjects = CommanderWorkerAuthority.GetSubjectUnitIds(command);
            if (subjects == null) return;
            ObserveFutureHumanOrder(subjects);
            foreach (var boundGoal in activeGoals)
            {
                foreach (var preparation in boundGoal.PreparationAllocators.Values)
                    foreach (int subject in subjects)
                        if (preparation.SelectedWorkerIds.Contains(subject))
                        {
                            preparation.HumanInterrupted = true;
                            workerAuthority.ReleaseGoal(boundGoal.GoalId);
                            break;
                        }
                if (boundGoal.IsTerminal || boundGoal.FrozenWorkerIds == null) continue;
                foreach (int subject in subjects)
                    if (System.Linq.Enumerable.Contains(boundGoal.FrozenWorkerIds, subject))
                    {
                        boundGoal.FrozenWorkerHumanOverride = true;
                        workerAuthority.ReleaseGoal(boundGoal.GoalId);
                        break;
                    }
            }
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (!(activeGoals[i] is AllocateWorkersGoal allocation) || allocation.IsTerminal) continue;
                for (int s = 0; s < subjects.Length; s++)
                {
                    if (!allocation.SelectedWorkerIds.Contains(subjects[s])) continue;
                    allocation.HumanInterrupted = true;
                    workerAuthority.ReleaseGoal(allocation.GoalId);
                    break;
                }
            }
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (!(activeGoals[i] is CommanderCapabilityGoal capability) || capability.IsTerminal
                    || !capability.RequiresResultBinding
                    || !(capability.ResultSourceGoal is EnsureUnitCountGoal producer)) continue;
                for (int s = 0; s < subjects.Length; s++)
                {
                    UnitData unit = simulation.UnitRegistry.GetUnit(subjects[s]);
                    if (unit == null || unit.PlayerId != playerId) continue;
                    bool result = false;
                    if (producer.ResultCaptureTick >= 0)
                    {
                        for (int r = 0; r < producer.ResultUnitIds.Count; r++)
                            if (producer.ResultUnitIds[r] == unit.Id) { result = true; break; }
                    }
                    else result = producer.AttributedUnitIds.Contains(unit.Id);
                    if (result) { capability.ResultHumanOverride = true; break; }
                }
            }
        }

        private void HandleBuildingPlacedFromCommand(ICommand command, BuildingData created)
        {
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (activeGoals[i] is ReachAgeGoal ageGoal && !ageGoal.IsTerminal
                    && ReferenceEquals(ageGoal.PendingAgeUpCommand, command))
                {
                    ageGoal.PendingAgeUpCommand = null;
                    if (created.PlayerId == ageGoal.PlayerId && !created.IsDestroyed
                        && created.Type == BuildingType.Landmark
                        && created.OriginTileX == ageGoal.PlacedTileX
                        && created.OriginTileZ == ageGoal.PlacedTileZ)
                        ageGoal.AgeUpBuildingId = created.Id;
                    return;
                }
                if (!(activeGoals[i] is BuildStructureGoal goal)
                    || goal.IsTerminal || !ReferenceEquals(goal.PendingPlacementCommand, command))
                    continue;
                goal.PendingPlacementCommand = null;
                if (created.PlayerId == goal.PlayerId && !created.IsDestroyed
                    && simulation.GetEffectiveBuildingType(created) == goal.StructureType
                    && created.OriginTileX == goal.PlacedTileX
                    && created.OriginTileZ == goal.PlacedTileZ)
                {
                    goal.PlacedBuildingId = created.Id;
                    if (!goal.AttributedBuildingIds.Contains(created.Id)) goal.AttributedBuildingIds.Add(created.Id);
                }
                return;
            }
        }

        private void PublishEvent(CommanderGoalEventType type, CommanderGoal goal, int tick)
        {
            GoalEventPublished?.Invoke(new CommanderGoalEvent(type, tick, goal));
        }

        private void PublishCommittedGraphEvent(CommanderGoal goal)
        {
            var handlers = GoalEventPublished;
            if (handlers == null) return;
            var notification = new CommanderGoalEvent(CommanderGoalEventType.GoalStarted,
                simulation.CurrentTick, goal);
            // Initial admission has already committed all goals. An observer fault is
            // not a failed transaction and must not prevent other observers seeing it.
            foreach (Action<CommanderGoalEvent> handler in handlers.GetInvocationList())
            {
                if (disposed) break;
                try { handler(notification); }
                catch (Exception error)
                {
                    Debug.LogWarning("[Commander] Committed graph observer failed; work remains admitted; type="
                        + error.GetType().Name);
                }
            }
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
            ReleaseTrainingObservations();
            simulation.ProducerUnitProduced -= HandleFutureBirth;
            simulation.LocalActionCommandProcessed -= HandleFutureCommandProcessed;
            simulation.CommandBuffer.CommandEnqueued -= HandleCommandEnqueued;
            simulation.OnBuildingPlacedFromCommand -= HandleBuildingPlacedFromCommand;
            for (int i = 0; i < activeGoals.Count; i++)
            {
                if (activeGoals[i] is WatchFutureUnitsGoal future)
                    SuppressFuturePendingActions(future);
                if (activeGoals[i] is BuildStructureGoal spatial)
                    spatial.PendingPlacementCommand = null;
                if (activeGoals[i] is ReachAgeGoal reachAge)
                    reachAge.PendingAgeUpCommand = null;
                workerAuthority.ReleaseGoal(activeGoals[i].GoalId);
            }
            suspendedGoalIds.Clear();
            workerAuthority.Clear();
            foreach(var goal in goals)goal.ReleaseRuntimeReferences();
            goals.Clear();activeGoals.Clear();archivedGoals.Clear();ActiveGoal=null;
            GoalStatusChanged = null;
            GoalEventPublished = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(CommanderGoalManager));
        }

    }
}
