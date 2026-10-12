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

    public enum CommanderTargetSelectorKind { UnitType, BuildingType }

    // Content identity only. Actors and locations remain separate; no runtime IDs.
    public readonly struct CommanderTargetSelector
    {
        public CommanderTargetSelectorKind Kind { get; }
        public int UnitType { get; }
        public BuildingType? StructureType { get; }

        public CommanderTargetSelector(int unitType)
        {
            if (!CommanderIntentCatalog.IsSupportedUnit(unitType))
                throw new ArgumentOutOfRangeException(nameof(unitType));
            Kind = CommanderTargetSelectorKind.UnitType; UnitType = unitType; StructureType = null;
        }

        public CommanderTargetSelector(BuildingType structureType)
        {
            if (!Enum.IsDefined(typeof(BuildingType), structureType))
                throw new ArgumentOutOfRangeException(nameof(structureType));
            Kind = CommanderTargetSelectorKind.BuildingType; UnitType = -1; StructureType = structureType;
        }

        internal bool IsCompatible(CommanderCapabilityActionType action, CommanderLocationSelectorKind location)
        {
            bool valid = Kind == CommanderTargetSelectorKind.UnitType
                ? CommanderIntentCatalog.IsSupportedUnit(UnitType) && !StructureType.HasValue
                : Kind == CommanderTargetSelectorKind.BuildingType && UnitType == -1
                    && StructureType.HasValue && Enum.IsDefined(typeof(BuildingType), StructureType.Value);
            return valid && (action == CommanderCapabilityActionType.AttackTarget
                && location == CommanderLocationSelectorKind.VisibleEnemy
                || action == CommanderCapabilityActionType.RepairTarget
                && location == CommanderLocationSelectorKind.PlayerBase && Kind == CommanderTargetSelectorKind.BuildingType);
        }
    }

    public readonly struct CommanderLocationSelector
    {
        public CommanderLocationSelectorKind Kind { get; }
        public ResourceType? ResourceType { get; }
        // Null is a point anchor, not an invented four-tile area. Explicit area
        // constraints are retained for fail-closed validation, never approximated.
        public int? RadiusTiles { get; }

        public CommanderLocationSelector(CommanderLocationSelectorKind kind,
            ResourceType? resourceType = null, int? radiusTiles = null)
        {
            if (!Enum.IsDefined(typeof(CommanderLocationSelectorKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (radiusTiles.HasValue && (radiusTiles.Value < 1 || radiusTiles.Value > 20))
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

    // First game-side target resolution is sticky across blocked retries. Runtime
    // object identity prevents destroyed/recycled IDs or another matching type from
    // substituting. This receipt is never serialized to/from provider data.
    internal sealed class CommanderTargetBinding
    {
        internal GameSimulation Runtime { get; }
        internal UnitData Unit { get; }
        internal BuildingData Building { get; }
        internal int OriginalOwner { get; }
        internal CommanderTargetBinding(GameSimulation runtime, UnitData unit)
        { Runtime = runtime; Unit = unit; OriginalOwner = unit.PlayerId; }
        internal CommanderTargetBinding(GameSimulation runtime, BuildingData building)
        { Runtime = runtime; Building = building; OriginalOwner = building.PlayerId; }
    }

    internal readonly struct CommanderResultBinding
    {
        public CommanderResultKind Kind { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public int BuildingId { get; }
        public int SourceGoalId { get; }
        public int SourceCreatedTick { get; }
        public GameSimulation Runtime { get; }
        public CommanderGoalManager SourceOwner { get; }

        private CommanderResultBinding(CommanderResultKind kind, IReadOnlyList<int> unitIds,
            int buildingId, int sourceGoalId, int sourceCreatedTick,
            GameSimulation runtime, CommanderGoalManager sourceOwner)
        {
            Kind = kind;
            UnitIds = unitIds;
            BuildingId = buildingId;
            SourceGoalId = sourceGoalId;
            SourceCreatedTick = sourceCreatedTick;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            SourceOwner = sourceOwner;
        }

        public static CommanderResultBinding ForUnits(IReadOnlyList<int> unitIds,
            int sourceGoalId, int sourceCreatedTick, GameSimulation runtime,
            CommanderGoalManager sourceOwner = null)
        {
            return new CommanderResultBinding(CommanderResultKind.Units,
                Array.AsReadOnly(new List<int>(unitIds ?? throw new ArgumentNullException(nameof(unitIds))).ToArray()), -1,
                sourceGoalId, sourceCreatedTick, runtime, sourceOwner);
        }

        public static CommanderResultBinding ForBuilding(int buildingId,
            int sourceGoalId, int sourceCreatedTick, GameSimulation runtime,
            CommanderGoalManager sourceOwner = null)
        {
            if (buildingId < 0) throw new ArgumentOutOfRangeException(nameof(buildingId));
            return new CommanderResultBinding(CommanderResultKind.Building, null, buildingId,
                sourceGoalId, sourceCreatedTick, runtime, sourceOwner);
        }
    }
}
