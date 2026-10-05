using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderCapabilityActionType
    {
        MoveUnits,
        ScoutArea,
        PatrolArea,
        SetRallyPoint,
        AttackTarget,
        DefendArea,
        RetreatUnits,
        RepairTarget,
        ResearchTechnology
    }

    public enum CommanderUnitSelectorKind
    {
        Military,
        Scout,
        Villagers,
        UnitType,
        DamagedMilitary
    }

    public readonly struct CommanderUnitSelector
    {
        public CommanderUnitSelectorKind Kind { get; }
        public int UnitType { get; }
        public int Count { get; }

        public CommanderUnitSelector(CommanderUnitSelectorKind kind, int count = 1, int unitType = -1)
        {
            if (!Enum.IsDefined(typeof(CommanderUnitSelectorKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (count < 1 || count > 50) throw new ArgumentOutOfRangeException(nameof(count));
            if (kind == CommanderUnitSelectorKind.UnitType && unitType < 0)
                throw new ArgumentOutOfRangeException(nameof(unitType));
            Kind = kind;
            Count = count;
            UnitType = unitType;
        }
    }

    public enum CommanderLocationSelectorKind
    {
        PlayerBase,
        WorkedResource,
        VisibleResource,
        VisibleEnemy,
        RelativeToSelectedUnits
    }

    public readonly struct CommanderLocationSelector
    {
        public CommanderLocationSelectorKind Kind { get; }
        public ResourceType? ResourceType { get; }
        public int RadiusTiles { get; }

        public CommanderLocationSelector(CommanderLocationSelectorKind kind,
            ResourceType? resourceType = null, int radiusTiles = 4)
        {
            if (!Enum.IsDefined(typeof(CommanderLocationSelectorKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (radiusTiles < 1 || radiusTiles > 20)
                throw new ArgumentOutOfRangeException(nameof(radiusTiles));
            if ((kind == CommanderLocationSelectorKind.WorkedResource
                || kind == CommanderLocationSelectorKind.VisibleResource) && !resourceType.HasValue)
                throw new ArgumentException("Resource selectors require a resource type.", nameof(resourceType));
            Kind = kind;
            ResourceType = resourceType;
            RadiusTiles = radiusTiles;
        }
    }

    // Internal, typed handoff from a completed game-side producer goal to a dependent
    // capability. Provider JSON never contains these IDs and no fallback is permitted
    // when a result-bound action is requested.
    internal enum CommanderResultKind
    {
        Units,
        Building
    }

    internal readonly struct CommanderResultBinding
    {
        public CommanderResultKind Kind { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public int BuildingId { get; }
        public int SourceGoalId { get; }
        public int SourceCreatedTick { get; }

        private CommanderResultBinding(CommanderResultKind kind, IReadOnlyList<int> unitIds,
            int buildingId, int sourceGoalId, int sourceCreatedTick)
        {
            Kind = kind;
            UnitIds = unitIds;
            BuildingId = buildingId;
            SourceGoalId = sourceGoalId;
            SourceCreatedTick = sourceCreatedTick;
        }

        public static CommanderResultBinding ForUnits(IReadOnlyList<int> unitIds,
            int sourceGoalId, int sourceCreatedTick)
        {
            return new CommanderResultBinding(CommanderResultKind.Units,
                unitIds ?? throw new ArgumentNullException(nameof(unitIds)), -1,
                sourceGoalId, sourceCreatedTick);
        }

        public static CommanderResultBinding ForBuilding(int buildingId,
            int sourceGoalId, int sourceCreatedTick)
        {
            if (buildingId < 0) throw new ArgumentOutOfRangeException(nameof(buildingId));
            return new CommanderResultBinding(CommanderResultKind.Building, null, buildingId,
                sourceGoalId, sourceCreatedTick);
        }
    }
}
