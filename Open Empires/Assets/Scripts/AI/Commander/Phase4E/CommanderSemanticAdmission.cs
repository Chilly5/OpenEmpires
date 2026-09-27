using System;

namespace OpenEmpires
{
    // Converts parsed semantic data to a tactical intent using the existing trusted DTO gate.
    // Strategic objectives deliberately have no path through this converter.
    public static class CommanderSemanticAdmission
    {
        private const string RejectedReason = "The semantic order was rejected by Commander validation.";

        public static bool TryCreateTacticalIntent(CommanderSemanticNode node,
            CommanderContext context, out CommanderIntent intent, out string safeReason)
        {
            intent = null;
            safeReason = RejectedReason;
            if (node == null || context == null || context.PlayerId < 0) return false;

            var dto = new CommanderIntentDTO { intentCategory = "Tactical" };
            switch (node.Type)
            {
                case CommanderSemanticNodeType.EnsureUnitCount:
                    if (!node.UnitType.HasValue || !node.Count.HasValue
                        || !CommanderIntentCatalog.IsSupportedUnit(node.UnitType.Value)
                        || node.BuildingType.HasValue || node.ResourceType.HasValue
                        || node.StrategicObjectiveType.HasValue) return false;
                    dto.intentType = nameof(CommanderIntentType.EnsureUnitCount);
                    dto.unit = CommanderIntentCatalog.GetUnitDisplayName(node.UnitType.Value);
                    dto.amount = node.Count.Value;
                    break;

                case CommanderSemanticNodeType.BuildStructure:
                    if (!node.BuildingType.HasValue || !node.Count.HasValue
                        || !CommanderIntentCatalog.IsSupportedStructure(node.BuildingType.Value)
                        || node.UnitType.HasValue || node.ResourceType.HasValue
                        || node.StrategicObjectiveType.HasValue
                        // Placement is parsed for the upcoming deterministic resolver, but this
                        // admission path cannot carry it. Fail closed until spatial planning is wired.
                        || node.PlacementAnchorSelector.HasValue
                        || node.PlacementAnchorOrdinal.HasValue
                        || node.PlacementRelation.HasValue
                        || node.ClearGapTiles.HasValue) return false;
                    dto.intentType = nameof(CommanderIntentType.BuildStructure);
                    dto.structure = node.BuildingType.Value.ToString();
                    dto.amount = node.Count.Value;
                    break;

                case CommanderSemanticNodeType.SetResourceAllocation:
                    if (!node.ResourceType.HasValue || !node.Count.HasValue
                        || !Enum.IsDefined(typeof(ResourceType), node.ResourceType.Value)
                        || node.UnitType.HasValue || node.BuildingType.HasValue
                        || node.StrategicObjectiveType.HasValue) return false;
                    dto.intentType = nameof(CommanderIntentType.SetResourceAllocation);
                    dto.resource = node.ResourceType.Value.ToString();
                    dto.mode = nameof(ResourceAllocationMode.SetExact);
                    dto.amount = node.Count.Value;
                    break;

                default:
                    return false;
            }

            CommanderIntentInterpretation validated = CommanderIntentDtoCodec.ValidateAndConvert(dto, context);
            if (!validated.Success || validated.Intent == null || validated.StrategicIntent != null)
                return false;
            intent = validated.Intent;
            safeReason = string.Empty;
            return true;
        }
    }
}
