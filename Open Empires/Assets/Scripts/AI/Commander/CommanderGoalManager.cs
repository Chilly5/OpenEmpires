using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class CommanderGoalManager
    {
        private const int PlanningIntervalTicks = 15;
        private const int BlockedRetryIntervalTicks = 150; // Five seconds at 30 Hz.
        private const int BlockedTimeoutTicks = 1800; // One minute continuously unresolved.
        private readonly GameSimulation simulation;
        private readonly int playerId;
        private readonly CommanderPlanner planner;
        private readonly CommanderWorkerAuthority workerAuthority;
        private readonly List<CommanderGoal> goals = new List<CommanderGoal>();
        private int nextGoalId = 1;
        private int lastEvaluatedTick = -1;

        public IReadOnlyList<CommanderGoal> Goals => goals;
        public int PlayerId => playerId;
        public int CurrentTick => simulation.CurrentTick;
        public CommanderGoal ActiveGoal { get; private set; }
        public event Action<CommanderGoal> GoalStatusChanged;
        public event Action<CommanderGoalEvent> GoalEventPublished;

        public CommanderGoal GetGoal(int goalId)
        {
            for (int i = 0; i < goals.Count; i++) if (goals[i].GoalId == goalId) return goals[i];
            return null;
        }

        public CommanderWorkerReservation? GetWorkerReservation(int workerId) => workerAuthority.GetReservation(workerId);

        public bool TryReserveWorker(int goalId, int workerId, CommanderWorkerReservationType reservationType)
        {
            CommanderGoal goal = GetGoal(goalId);
            return goal != null && !goal.IsTerminal && Enum.IsDefined(typeof(CommanderWorkerReservationType), reservationType)
                && workerAuthority.TryReserve(workerId, goalId, reservationType, simulation.CurrentTick);
        }

        public CommanderGoalManager(GameSimulation simulation, int playerId)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.playerId = playerId;
            workerAuthority = new CommanderWorkerAuthority(simulation, playerId);
            simulation.CommandBuffer.CommandEnqueued += HandleCommandEnqueued;
            planner = new CommanderPlanner(simulation, workerAuthority);
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
            int maxDurationTicks = 36000, IReadOnlyList<CommanderConstraint> constraints = null)
        {
            var goal = new BuildStructureGoal(playerId, type, count, maxDurationTicks);
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

        private T Register<T>(T goal, IReadOnlyList<CommanderConstraint> constraints) where T : CommanderGoal
        {
            goal.GoalId = nextGoalId++;
            goal.CreatedTick = simulation.CurrentTick;
            planner.CaptureConstraints(goal, constraints);
            goals.Add(goal);
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = goal;
            Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: {goal.GoalType}");
            PublishEvent(CommanderGoalEventType.GoalStarted, goal, simulation.CurrentTick);
            return goal;
        }

        public bool CancelGoal(int goalId)
        {
            for (int i = 0; i < goals.Count; i++)
            {
                CommanderGoal goal = goals[i];
                if (goal.GoalId != goalId || goal.IsTerminal) continue;
                goal.SetStatus(CommanderGoalStatus.Cancelled, "Cancelled by the owning player.");
                workerAuthority.ReleaseGoal(goal.GoalId);
                if (ActiveGoal == goal) ActiveGoal = null;
                Debug.Log($"[Commander] Goal #{goal.GoalId} cancelled.");
                GoalStatusChanged?.Invoke(goal);
                PublishEvent(CommanderGoalEventType.GoalCancelled, goal, simulation.CurrentTick);
                return true;
            }
            return false;
        }

        public void Tick(int currentTick)
        {
            if (currentTick == lastEvaluatedTick) return;
            if (lastEvaluatedTick >= 0 && currentTick % PlanningIntervalTicks != 0) return;
            lastEvaluatedTick = currentTick;
            workerAuthority.PruneUnavailableWorkers();

            // Duration limits also apply while goals are deferred or waiting in the queue.
            ActiveGoal = null;
            for (int i = 0; i < goals.Count; i++)
            {
                CommanderGoal goal = goals[i];
                if (!goal.IsTerminal && goal.MaxDurationTicks > 0
                    && currentTick - goal.CreatedTick >= goal.MaxDurationTicks)
                    FailGoal(goal, $"Goal exceeded its {goal.MaxDurationTicks}-tick duration limit.", currentTick);
            }

            // FIFO among runnable goals. Blocked retries keep their original place, but
            // every no-command wait yields immediately to later requests.
            // At most one ordinary ICommand is emitted during a planning tick.
            for (int i = 0; i < goals.Count; i++)
            {
                CommanderGoal goal = goals[i];
                if (goal.IsTerminal || (goal.Status == CommanderGoalStatus.Blocked
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
                if (goal.IsTerminal) workerAuthority.ReleaseGoal(goal.GoalId);
                if (plan.Command != null && !goal.IsTerminal)
                {
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

        private void FailGoal(CommanderGoal goal, string reason, int currentTick)
        {
            goal.SetStatus(CommanderGoalStatus.Failed, reason);
            workerAuthority.ReleaseGoal(goal.GoalId);
            Debug.LogWarning($"[Commander] Goal #{goal.GoalId} failed: {goal.StatusReason}");
            GoalStatusChanged?.Invoke(goal);
            PublishEvent(CommanderGoalEventType.GoalFailed, goal, currentTick);
        }

        private void HandleCommandEnqueued(ICommand command, CommandEnqueueSource source)
        {
            workerAuthority.ObserveEnqueuedCommand(command, source, simulation.CurrentTick);
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

    }
}
