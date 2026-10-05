using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderGoalType
    {
        EnsureUnitCount,
        BuildStructure,
        ResourceAllocation,
        ReachAge,
        CapabilityAction
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

    public enum CommanderPlacementBlocker
    {
        None,
        AnchorUnavailable,
        NoEligibleBuilder,
        NoLegalCandidate,
        BoundBuildingUnavailable
    }

    public enum CommanderAgeBlocker
    {
        None,
        UnsupportedTarget,
        MissingFood,
        MissingGold,
        NoEligibleBuilder,
        NoLegalCandidate,
        LandmarkUnavailable
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
        private readonly List<CommanderGoal> dependencies = new List<CommanderGoal>();
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
        public IReadOnlyList<CommanderGoal> Dependencies => dependencies.AsReadOnly();

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

        internal void SetDependencies(IEnumerable<CommanderGoal> requiredGoals)
        {
            dependencies.Clear();
            if (requiredGoals == null) return;
            foreach (CommanderGoal required in requiredGoals)
                if (required != null && !dependencies.Contains(required)) dependencies.Add(required);
        }
    }

    public sealed class BuildStructureGoal : CommanderGoal
    {
        public BuildingType StructureType { get; }
        public int Count { get; }
        public int TargetTotal { get; internal set; }
        public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
        public int? PlacementAnchorOrdinal { get; }
        public CommanderSemanticPlacementRelation? PlacementRelation { get; }
        public ResourceType? PlacementResourceType { get; }
        public int ClearGapTiles { get; }
        public int? PlacedTileX { get; internal set; }
        public int? PlacedTileZ { get; internal set; }
        public int PlacedBuildingId { get; internal set; } = -1;
        public CommanderPlacementBlocker PlacementBlocker { get; internal set; }
        internal int PlacementIssuedTick { get; set; } = -1;
        internal int PlacementIssuedSimulationTick { get; set; } = -1;
        internal ICommand PendingPlacementCommand { get; set; }
        // Result-binding state is captured by the game-side manager, never by the provider.
        internal bool HasResultConsumer { get; set; }
        internal readonly HashSet<int> BaselineBuildingIds = new HashSet<int>();
        internal IReadOnlyList<int> ResultBuildingIds { get; private set; } = Array.Empty<int>();
        internal int ResultCaptureTick { get; private set; } = -1;
        public bool HasSemanticPlacement => PlacementAnchorSelector.HasValue;

        internal void CaptureBuildingResult(IReadOnlyList<int> buildingIds, int tick)
        {
            ResultBuildingIds = buildingIds ?? Array.Empty<int>();
            ResultCaptureTick = tick;
        }

        public BuildStructureGoal(int playerId, BuildingType structureType, int count = 1,
            int maxDurationTicks = 36000,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null,
            ResourceType? placementResourceType = null)
            : base(playerId, CommanderGoalType.BuildStructure, maxDurationTicks)
        {
            if (count < 1 || count > CommanderIntentValidator.MaximumStructureCount)
                throw new ArgumentOutOfRangeException(nameof(count));
            StructureType = structureType;
            Count = count;
            PlacementAnchorSelector = placementAnchorSelector;
            PlacementAnchorOrdinal = placementAnchorOrdinal;
            PlacementRelation = placementRelation;
            ClearGapTiles = clearGapTiles ?? 1;
            PlacementResourceType = placementResourceType;
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
        // Game-side dependency only. The provider never supplies this reference.
        public BuildStructureGoal RequiredProducerGoal { get; internal set; }
        // A result consumer receives only units created after this goal was submitted.
        internal bool HasResultConsumer { get; set; }
        internal readonly HashSet<int> BaselineUnitIds = new HashSet<int>();
        internal IReadOnlyList<int> ResultUnitIds { get; private set; } = Array.Empty<int>();
        internal int ResultCaptureTick { get; private set; } = -1;

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

        internal void CaptureUnitResult(IReadOnlyList<int> unitIds, int tick)
        {
            ResultUnitIds = unitIds ?? Array.Empty<int>();
            ResultCaptureTick = tick;
        }
    }

    public sealed class ReachAgeGoal : CommanderGoal
    {
        public CommanderSemanticAgeTarget RequestedTarget { get; }
        public int TargetAge { get; }
        public int AgeUpBuildingId { get; internal set; } = -1;
        public CommanderAgeBlocker Blocker { get; internal set; }
        internal int? PlacedTileX { get; set; }
        internal int? PlacedTileZ { get; set; }
        internal ICommand PendingAgeUpCommand { get; set; }
        internal int AgeUpIssuedSimulationTick { get; set; } = -1;

        public ReachAgeGoal(int playerId, CommanderSemanticAgeTarget requestedTarget, int targetAge,
            int maxDurationTicks = 36000)
            : base(playerId, CommanderGoalType.ReachAge, maxDurationTicks)
        {
            if (!Enum.IsDefined(typeof(CommanderSemanticAgeTarget), requestedTarget))
                throw new ArgumentOutOfRangeException(nameof(requestedTarget));
            if (targetAge < 2 || targetAge > 4)
                throw new ArgumentOutOfRangeException(nameof(targetAge));
            RequestedTarget = requestedTarget;
            TargetAge = targetAge;
        }
    }

    public sealed class CommanderCapabilityGoal : CommanderGoal
    {
        public CapabilityActionIntent Action { get; }
        internal bool CommandIssued { get; set; }
        internal ICommand IssuedCommand { get; set; }
        internal int CommandIssuedSimulationTick { get; set; } = -1;
        // Runtime-only typed result source. This prevents provider node indices from
        // becoming entity references and prevents cross-manager/stale-result reuse.
        internal CommanderGoal ResultSourceGoal { get; set; }

        public CommanderCapabilityGoal(CapabilityActionIntent action, int maxDurationTicks = 36000)
            : base(action?.PlayerId ?? throw new ArgumentNullException(nameof(action)),
                CommanderGoalType.CapabilityAction, maxDurationTicks)
        {
            Action = action;
        }
    }
}
