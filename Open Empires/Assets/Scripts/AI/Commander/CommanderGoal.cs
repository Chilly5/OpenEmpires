using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderGoalType
    {
        EnsureUnitCount,
        BuildStructure,
        ResourceAllocation
    }

    public enum CommanderGoalStatus
    {
        Pending,
        Planning,
        Executing,
        WaitingForResources,
        WaitingForPrerequisite,
        WaitingForConstruction,
        WaitingForProduction,
        Completed,
        Blocked,
        Failed,
        Cancelled
    }

    // Stable coarse lifecycle for consumers; detailed tactical statuses remain compatible.
    public enum CommanderGoalLifecycle { Created, Active, Waiting, Blocked, Completed, Failed, Cancelled }

    public enum CommanderGoalEventType
    {
        GoalStarted,
        GoalProgressChanged,
        GoalBlocked,
        GoalCompleted,
        GoalFailed,
        GoalCancelled
    }

    public readonly struct CommanderGoalEvent
    {
        public readonly CommanderGoalEventType EventType;
        public readonly int Tick;
        public readonly CommanderGoal Goal;

        public CommanderGoalEvent(CommanderGoalEventType eventType, int tick, CommanderGoal goal)
        {
            EventType = eventType;
            Tick = tick;
            Goal = goal;
        }
    }

    public abstract class CommanderGoal
    {
        public int GoalId { get; internal set; }
        public int PlayerId { get; }
        public CommanderGoalType GoalType { get; }
        public CommanderGoalStatus Status { get; private set; }
        public int CreatedTick { get; internal set; }
        // Metadata only: evaluation remains FIFO. Parent execution is not implemented.
        public int Priority { get; }
        public int? ParentGoalId { get; internal set; }
        public CommanderGoalLifecycle Lifecycle => Status switch
        {
            CommanderGoalStatus.Pending => CommanderGoalLifecycle.Created,
            CommanderGoalStatus.Planning => CommanderGoalLifecycle.Active,
            CommanderGoalStatus.Executing => CommanderGoalLifecycle.Active,
            CommanderGoalStatus.Blocked => CommanderGoalLifecycle.Blocked,
            CommanderGoalStatus.Completed => CommanderGoalLifecycle.Completed,
            CommanderGoalStatus.Failed => CommanderGoalLifecycle.Failed,
            CommanderGoalStatus.Cancelled => CommanderGoalLifecycle.Cancelled,
            _ => CommanderGoalLifecycle.Waiting
        };
        public string StatusReason { get; private set; }
        public int LastObservedOwnedCount { get; internal set; }
        public int LastObservedQueuedCount { get; internal set; }
        public int LastEconomyCommandTick { get; internal set; } = int.MinValue / 2;
        public int MaxDurationTicks { get; }
        public bool UseIdleWorkersOnly { get; internal set; }
        internal readonly Dictionary<ResourceType, int> ProtectedWorkerMinimums = new Dictionary<ResourceType, int>();
        internal int ObservedConstructionBuildingId { get; set; } = -1;
        internal int LastConstructionTicksRemaining { get; set; } = -1;
        internal int LastConstructionProgressTick { get; set; }
        internal bool ConstructionBuilderInRange { get; set; }
        internal int LastConstructionRecoveryTick { get; set; } = int.MinValue / 2;
        internal int BlockedSinceTick { get; set; } = -1;
        internal int NextBlockedRetryTick { get; set; }

        public bool IsTerminal => Status == CommanderGoalStatus.Completed
            || Status == CommanderGoalStatus.Failed
            || Status == CommanderGoalStatus.Cancelled;

        protected CommanderGoal(int playerId, CommanderGoalType goalType, int maxDurationTicks, int priority = 0)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (maxDurationTicks < 0) throw new ArgumentOutOfRangeException(nameof(maxDurationTicks));
            PlayerId = playerId;
            GoalType = goalType;
            Priority = priority;
            MaxDurationTicks = maxDurationTicks;
            Status = CommanderGoalStatus.Pending;
            StatusReason = string.Empty;
        }

        internal bool SetStatus(CommanderGoalStatus status, string reason)
        {
            reason ??= string.Empty;
            if (Status == status && StatusReason == reason) return false;
            Status = status;
            StatusReason = reason;
            return true;
        }
    }

    public sealed class BuildStructureGoal : CommanderGoal
    {
        public BuildingType StructureType { get; }
        public int Count { get; }
        public int TargetTotal { get; internal set; }

        public BuildStructureGoal(int playerId, BuildingType structureType, int count = 1,
            int maxDurationTicks = 36000) : base(playerId, CommanderGoalType.BuildStructure, maxDurationTicks)
        {
            if (count < 1 || count > CommanderIntentValidator.MaximumStructureCount)
                throw new ArgumentOutOfRangeException(nameof(count));
            StructureType = structureType;
            Count = count;
        }
    }

    public sealed class ResourceAllocationGoal : CommanderGoal
    {
        public ResourceType Resource { get; }
        public int TargetWorkers { get; }

        public ResourceAllocationGoal(int playerId, ResourceType resource, int targetWorkers,
            int maxDurationTicks = 36000) : base(playerId, CommanderGoalType.ResourceAllocation, maxDurationTicks)
        {
            if (targetWorkers < 0) throw new ArgumentOutOfRangeException(nameof(targetWorkers));
            Resource = resource;
            TargetWorkers = targetWorkers;
        }
    }

    public sealed class EnsureUnitCountGoal : CommanderGoal
    {
        public int RequestedUnitType { get; }
        public int TargetTotal { get; }
        public int MaxQueueDepth { get; internal set; }

        public EnsureUnitCountGoal(int playerId, int requestedUnitType, int targetTotal,
            int maxQueueDepth = 3, int priority = 0, int maxDurationTicks = 36000)
            : base(playerId, CommanderGoalType.EnsureUnitCount, maxDurationTicks, priority)
        {
            if (targetTotal < 0) throw new ArgumentOutOfRangeException(nameof(targetTotal));
            if (maxQueueDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxQueueDepth));
            RequestedUnitType = requestedUnitType;
            TargetTotal = targetTotal;
            MaxQueueDepth = maxQueueDepth;
        }
    }
}
