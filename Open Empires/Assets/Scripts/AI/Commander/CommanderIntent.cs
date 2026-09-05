using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderIntentLayer
    {
        Tactical,
        Strategic
    }

    public interface ICommanderIntentRequest
    {
        int PlayerId { get; }
        CommanderIntentLayer IntentLayer { get; }
    }

    public enum CommanderIntentType
    {
        EnsureUnitCount,
        SetResourceAllocation,
        BuildStructure
    }

    public enum CommanderConstraintType
    {
        ProtectedResource,
        PreferredWorkers,
        MaximumQueue
    }

    public enum CommanderPreferredWorkerSource
    {
        IdleOnly
    }

    public enum ResourceAllocationMode
    {
        SetExact,
        Increase
    }

    public abstract class CommanderConstraint
    {
        public CommanderConstraintType Type { get; }

        protected CommanderConstraint(CommanderConstraintType type)
        {
            Type = type;
        }
    }

    public sealed class ProtectedResourceConstraint : CommanderConstraint
    {
        public ResourceType Resource { get; }
        // Null freezes the actual worker count at submission; an explicit value sets a floor.
        public int? MinimumWorkers { get; }

        public ProtectedResourceConstraint(ResourceType resource, int? minimumWorkers = null)
            : base(CommanderConstraintType.ProtectedResource)
        {
            Resource = resource;
            MinimumWorkers = minimumWorkers;
        }
    }

    public sealed class PreferredWorkersConstraint : CommanderConstraint
    {
        public CommanderPreferredWorkerSource WorkerSource { get; }

        public PreferredWorkersConstraint(CommanderPreferredWorkerSource workerSource)
            : base(CommanderConstraintType.PreferredWorkers)
        {
            WorkerSource = workerSource;
        }
    }

    public sealed class MaximumQueueConstraint : CommanderConstraint
    {
        public int MaximumQueue { get; }

        public MaximumQueueConstraint(int maximumQueue)
            : base(CommanderConstraintType.MaximumQueue)
        {
            MaximumQueue = maximumQueue;
        }
    }

    public abstract class CommanderIntent : ICommanderIntentRequest
    {
        private readonly List<CommanderConstraint> constraints;

        public CommanderIntentType Type { get; }
        public int PlayerId { get; }
        public CommanderIntentLayer IntentLayer => CommanderIntentLayer.Tactical;
        public IReadOnlyList<CommanderConstraint> Constraints => constraints;

        protected CommanderIntent(CommanderIntentType type, int playerId,
            IEnumerable<CommanderConstraint> constraints = null)
        {
            Type = type;
            PlayerId = playerId;
            this.constraints = constraints != null
                ? new List<CommanderConstraint>(constraints)
                : new List<CommanderConstraint>();
        }
    }

    public sealed class EnsureUnitCountIntent : CommanderIntent
    {
        public int UnitType { get; }
        public int TargetTotal { get; }

        public EnsureUnitCountIntent(int playerId, int unitType, int targetTotal,
            IEnumerable<CommanderConstraint> constraints = null)
            : base(CommanderIntentType.EnsureUnitCount, playerId, constraints)
        {
            UnitType = unitType;
            TargetTotal = targetTotal;
        }
    }

    public sealed class SetResourceAllocationIntent : CommanderIntent
    {
        public ResourceType Resource { get; }
        public ResourceAllocationMode Mode { get; }
        public int? WorkerCount { get; }

        public SetResourceAllocationIntent(int playerId, ResourceType resource,
            ResourceAllocationMode mode, int? workerCount,
            IEnumerable<CommanderConstraint> constraints = null)
            : base(CommanderIntentType.SetResourceAllocation, playerId, constraints)
        {
            Resource = resource;
            Mode = mode;
            WorkerCount = workerCount;
        }
    }

    public sealed class BuildStructureIntent : CommanderIntent
    {
        public BuildingType StructureType { get; }
        public int Count { get; }

        public BuildStructureIntent(int playerId, BuildingType structureType, int count = 1,
            IEnumerable<CommanderConstraint> constraints = null)
            : base(CommanderIntentType.BuildStructure, playerId, constraints)
        {
            StructureType = structureType;
            Count = count;
        }
    }
}
