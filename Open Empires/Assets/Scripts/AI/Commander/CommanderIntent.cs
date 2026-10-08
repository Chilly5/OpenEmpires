using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum IntentCategory
    {
        Tactical,
        Strategic
    }

    public enum CommanderIntentLayer
    {
        Tactical,
        Strategic
    }

    public interface ICommanderIntentRequest
    {
        int PlayerId { get; }
        CommanderIntentLayer IntentLayer { get; }
        IntentCategory Category => IntentLayer == CommanderIntentLayer.Tactical ? IntentCategory.Tactical : IntentCategory.Strategic;
    }

    public enum CommanderIntentType
    {
        EnsureUnitCount,
        SetResourceAllocation,
        BuildStructure,
        ReachAge,
        CapabilityAction,
        AllocateWorkers
    }

    public enum CommanderConstraintType
    {
        ProtectedResource,
        PreferredWorkers,
        MaximumQueue,
        NoConstruction,
        ResourceSource
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

    public sealed class NoConstructionConstraint : CommanderConstraint
    {
        public NoConstructionConstraint() : base(CommanderConstraintType.NoConstruction) { }
    }

    public sealed class ResourceSourceConstraint : CommanderConstraint
    {
        public ResourceType Resource { get; }
        public ResourceSourceKind SourceKind { get; }
        public ResourceSourceConstraint(ResourceType resource, ResourceSourceKind sourceKind)
            : base(CommanderConstraintType.ResourceSource) { Resource = resource; SourceKind = sourceKind; }
    }

    public abstract class CommanderIntent : ICommanderIntentRequest
    {
        private readonly List<CommanderConstraint> constraints;

        public CommanderIntentType Type { get; }
        public int PlayerId { get; }
        public CommanderIntentLayer IntentLayer => CommanderIntentLayer.Tactical;
        public IReadOnlyList<CommanderConstraint> Constraints => constraints.AsReadOnly();

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

    public enum CommanderProductionQuantityMode { TargetTotal, New }

    public sealed class EnsureUnitCountIntent : CommanderIntent
    {
        public int UnitType { get; }
        public int TargetTotal { get; }
        internal int? NewProductionCount { get; }

        public EnsureUnitCountIntent(int playerId, int unitType, int targetTotal,
            IEnumerable<CommanderConstraint> constraints = null, int? newProductionCount = null)
            : base(CommanderIntentType.EnsureUnitCount, playerId, constraints)
        {
            UnitType = unitType;
            TargetTotal = targetTotal;
            if (newProductionCount.HasValue && (newProductionCount.Value < 1 || newProductionCount.Value > 200))
                throw new ArgumentOutOfRangeException(nameof(newProductionCount));
            NewProductionCount = newProductionCount;
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
        public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
        public int? PlacementAnchorOrdinal { get; }
        public CommanderSemanticPlacementRelation? PlacementRelation { get; }
        public int? ClearGapTiles { get; }
        public ResourceType? PlacementResourceType { get; }

        public BuildStructureIntent(int playerId, BuildingType structureType, int count = 1,
            IEnumerable<CommanderConstraint> constraints = null,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null,
            ResourceType? placementResourceType = null)
            : base(CommanderIntentType.BuildStructure, playerId, constraints)
        {
            StructureType = structureType;
            Count = count;
            PlacementAnchorSelector = placementAnchorSelector;
            PlacementAnchorOrdinal = placementAnchorOrdinal;
            PlacementRelation = placementRelation;
            ClearGapTiles = clearGapTiles;
            PlacementResourceType = placementResourceType;
        }
    }

    public sealed class ReachAgeIntent : CommanderIntent
    {
        // Resolved game age. The provider can only supply the bounded semantic target
        // token; admission resolves Next against the owning simulation.
        public int TargetAge { get; }
        public CommanderSemanticAgeTarget RequestedTarget { get; }

        public ReachAgeIntent(int playerId, CommanderSemanticAgeTarget requestedTarget, int targetAge,
            IEnumerable<CommanderConstraint> constraints = null)
            : base(CommanderIntentType.ReachAge, playerId, constraints)
        {
            if (targetAge < 2 || targetAge > 4) throw new ArgumentOutOfRangeException(nameof(targetAge));
            RequestedTarget = requestedTarget;
            TargetAge = targetAge;
        }
    }

    // Generic Phase 4G action. The provider supplies only bounded semantic selectors;
    // the executor resolves concrete units, buildings, resources and positions.
    public sealed class CapabilityActionIntent : CommanderIntent
    {
        public CommanderCapabilityActionType ActionType { get; }
        public CommanderUnitSelector UnitSelector { get; }
        public CommanderLocationSelector LocationSelector { get; }
        public TechnologyType? Technology { get; }
        public BuildingType? StructureType { get; }

        public CommanderTargetSelector? TargetSelector { get; }

        public CapabilityActionIntent(int playerId, CommanderCapabilityActionType actionType,
            CommanderUnitSelector unitSelector, CommanderLocationSelector locationSelector,
            TechnologyType? technology = null, BuildingType? structureType = null,
            IEnumerable<CommanderConstraint> constraints = null,
            CommanderTargetSelector? targetSelector = null)
            : base(CommanderIntentType.CapabilityAction, playerId, constraints)
        {
            if (!Enum.IsDefined(typeof(CommanderCapabilityActionType), actionType))
                throw new ArgumentOutOfRangeException(nameof(actionType));
            ActionType = actionType;
            UnitSelector = unitSelector;
            LocationSelector = locationSelector;
            Technology = technology;
            StructureType = structureType;
            TargetSelector = targetSelector;
        }
    }
}
