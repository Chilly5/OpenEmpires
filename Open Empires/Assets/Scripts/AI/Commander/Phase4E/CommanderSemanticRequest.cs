using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderSemanticOutcome
    {
        Request,
        Clarify,
        Unsupported
    }

    public enum CommanderSemanticNodeType
    {
        EnsureUnitCount,
        BuildStructure,
        SetResourceAllocation,
        ReachAge,
        MoveUnits,
        ScoutArea,
        PatrolArea,
        SetRallyPoint,
        AttackTarget,
        DefendArea,
        RetreatUnits,
        RepairTarget,
        ResearchTechnology,
        StrategicObjective
    }

    // Provider-facing age vocabulary. Numeric game ages remain game-side data.
    public enum CommanderSemanticAgeTarget
    {
        Next,
        Feudal = 2,
        Castle = 3,
        Imperial = 4
    }

    public enum CommanderSemanticAnchorSelector
    {
        MyTownCenter,
        MyBarracks,
        WorkedResource
    }

    // Near is nondirectional: deterministic placement considers cardinal sides and defaults
    // to one clear tile when no explicit gap was supplied. This enum contains no world position.
    public enum CommanderSemanticPlacementRelation
    {
        MapWest,
        MapEast,
        Near
    }

    public enum CommanderSemanticUnitSelector
    {
        Military,
        Scout,
        Villagers,
        Spearman,
        Archer,
        Knight,
        DamagedMilitary
    }

    public enum CommanderSemanticLocationSelector
    {
        PlayerBase,
        WorkedResource,
        VisibleResource,
        VisibleEnemy,
        RelativeToSelectedUnits
    }

    // Parsed provider data only. No player, entity, position, provenance, or command authority lives here.
    public sealed class CommanderSemanticNode
    {
        public CommanderSemanticNodeType Type { get; }
        public int? UnitType { get; }
        public BuildingType? BuildingType { get; }
        public ResourceType? ResourceType { get; }
        public int? Count { get; }
        public StrategicObjectiveType? StrategicObjectiveType { get; }
        public CommanderSemanticAgeTarget? AgeTarget { get; }
        public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
        public int? PlacementAnchorOrdinal { get; }
        public CommanderSemanticPlacementRelation? PlacementRelation { get; }
        public int? ClearGapTiles { get; }
        public CommanderSemanticUnitSelector? UnitSelector { get; }
        public CommanderSemanticLocationSelector? LocationSelector { get; }
        public TechnologyType? Technology { get; }
        // References are bounded semantic node indices, never game/entity IDs.
        public IReadOnlyList<int> DependsOn { get; }
        public int? ProducerFromNode { get; }
        public int? ResultFromNode { get; }

        internal CommanderSemanticNode(CommanderSemanticNodeType type, int? unitType = null,
            BuildingType? buildingType = null, ResourceType? resourceType = null,
            int? count = null, StrategicObjectiveType? strategicObjectiveType = null,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null,
            CommanderSemanticAgeTarget? ageTarget = null,
            CommanderSemanticUnitSelector? unitSelector = null,
            CommanderSemanticLocationSelector? locationSelector = null,
            TechnologyType? technology = null,
            IReadOnlyList<int> dependsOn = null,
            int? producerFromNode = null,
            int? resultFromNode = null)
        {
            Type = type;
            UnitType = unitType;
            BuildingType = buildingType;
            ResourceType = resourceType;
            Count = count;
            StrategicObjectiveType = strategicObjectiveType;
            PlacementAnchorSelector = placementAnchorSelector;
            PlacementAnchorOrdinal = placementAnchorOrdinal;
            PlacementRelation = placementRelation;
            ClearGapTiles = clearGapTiles;
            AgeTarget = ageTarget;
            UnitSelector = unitSelector;
            LocationSelector = locationSelector;
            Technology = technology;
            ProducerFromNode = producerFromNode;
            ResultFromNode = resultFromNode;
            var normalizedDependencies = dependsOn != null
                ? new List<int>(dependsOn)
                : new List<int>();
            if (producerFromNode.HasValue && !normalizedDependencies.Contains(producerFromNode.Value))
                normalizedDependencies.Add(producerFromNode.Value);
            DependsOn = normalizedDependencies.AsReadOnly();
        }
    }

    public sealed partial class CommanderSemanticResult
    {
        public bool IsValid { get; }
        public CommanderSemanticOutcome Outcome { get; }
        public IReadOnlyList<CommanderSemanticNode> Nodes { get; }
        public string SafeExplanation { get; }

        private CommanderSemanticResult(bool isValid, CommanderSemanticOutcome outcome,
            IReadOnlyList<CommanderSemanticNode> nodes, string safeExplanation)
        {
            IsValid = isValid;
            Outcome = outcome;
            Nodes = nodes ?? Array.Empty<CommanderSemanticNode>();
            SafeExplanation = safeExplanation ?? string.Empty;
        }
    }
}
