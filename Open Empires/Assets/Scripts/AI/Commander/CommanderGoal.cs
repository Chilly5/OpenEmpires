using System;

namespace OpenEmpires
{
    public enum CommanderGoalType
    {
        EnsureUnitCount
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
        public string StatusReason { get; private set; }
        public int LastObservedOwnedCount { get; internal set; }
        public int LastObservedQueuedCount { get; internal set; }
        public int LastEconomyCommandTick { get; internal set; } = int.MinValue / 2;
        public int MaxDurationTicks { get; }
        internal int ObservedConstructionBuildingId { get; set; } = -1;
        internal int LastConstructionTicksRemaining { get; set; } = -1;
        internal int LastConstructionProgressTick { get; set; }
        internal int LastConstructionRecoveryTick { get; set; } = int.MinValue / 2;

        public bool IsTerminal => Status == CommanderGoalStatus.Completed
            || Status == CommanderGoalStatus.Failed
            || Status == CommanderGoalStatus.Cancelled;

        protected CommanderGoal(int playerId, CommanderGoalType goalType, int maxDurationTicks)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (maxDurationTicks < 0) throw new ArgumentOutOfRangeException(nameof(maxDurationTicks));
            PlayerId = playerId;
            GoalType = goalType;
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

    public sealed class EnsureUnitCountGoal : CommanderGoal
    {
        public int RequestedUnitType { get; }
        public int TargetTotal { get; }
        public int MaxQueueDepth { get; }
        public int Priority { get; }

        public EnsureUnitCountGoal(int playerId, int requestedUnitType, int targetTotal,
            int maxQueueDepth = 3, int priority = 0, int maxDurationTicks = 36000)
            : base(playerId, CommanderGoalType.EnsureUnitCount, maxDurationTicks)
        {
            if (targetTotal < 0) throw new ArgumentOutOfRangeException(nameof(targetTotal));
            if (maxQueueDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxQueueDepth));
            RequestedUnitType = requestedUnitType;
            TargetTotal = targetTotal;
            MaxQueueDepth = maxQueueDepth;
            Priority = priority;
        }
    }
}
