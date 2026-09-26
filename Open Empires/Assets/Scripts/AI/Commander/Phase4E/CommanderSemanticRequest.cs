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
        StrategicObjective
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

        internal CommanderSemanticNode(CommanderSemanticNodeType type, int? unitType = null,
            BuildingType? buildingType = null, ResourceType? resourceType = null,
            int? count = null, StrategicObjectiveType? strategicObjectiveType = null)
        {
            Type = type;
            UnitType = unitType;
            BuildingType = buildingType;
            ResourceType = resourceType;
            Count = count;
            StrategicObjectiveType = strategicObjectiveType;
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
