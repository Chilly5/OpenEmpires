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
            if (!Enum.IsDefined(typeof(CommanderProductionQuantityMode), node.QuantityMode)
                || node.Type != CommanderSemanticNodeType.EnsureUnitCount && node.QuantityMode != CommanderProductionQuantityMode.TargetTotal) return false;

            var dto = new CommanderIntentDTO { intentCategory = "Tactical" };
            foreach (var constraint in node.Constraints)
                dto.constraints.Add(CommanderIntentDtoCodec.FromConstraint(constraint));
            switch (node.Type)
            {
                case CommanderSemanticNodeType.WatchFutureUnits:
                    if (!node.UnitType.HasValue || !node.Count.HasValue || !node.BuildingType.HasValue
                        || !node.FutureAction.HasValue || !node.ResourceType.HasValue
                        || node.Count.Value < 1 || node.Count.Value > 50
                        || node.ProducerOrdinal < 1 && node.ProducerOrdinal.HasValue
                        || node.ProducerOrdinal > 8
                        || node.FutureAction == CommanderFutureUnitAction.Gather && node.UnitType != 0)
                        return false;
                    intent = new WatchFutureUnitsIntent(context.PlayerId, node.UnitType.Value,
                        node.Count.Value, node.BuildingType.Value, node.ProducerOrdinal,
                        node.FutureAction.Value, node.ResourceType.Value,
                        node.SourceKind ?? ResourceSourceKind.Any);
                    safeReason = string.Empty;
                    return true;
                case CommanderSemanticNodeType.EnsureUnitCount:
                    if (!node.UnitType.HasValue || !node.Count.HasValue
                        || !CommanderIntentCatalog.IsSupportedUnit(node.UnitType.Value)
                        || node.BuildingType.HasValue || node.ResourceType.HasValue
                        || node.StrategicObjectiveType.HasValue) return false;
                    dto.intentType = nameof(CommanderIntentType.EnsureUnitCount);
                    dto.unit = CommanderIntentCatalog.GetUnitDisplayName(node.UnitType.Value);
                    dto.amount = node.Count.Value;
                    dto.quantityMode = node.QuantityMode.ToString();
                    break;

                case CommanderSemanticNodeType.BuildStructure:
                    if (!node.BuildingType.HasValue || !node.Count.HasValue
                        || !CommanderIntentCatalog.IsSupportedStructure(node.BuildingType.Value)
                        || node.UnitType.HasValue
                        || node.StrategicObjectiveType.HasValue
                        || (node.ResourceType.HasValue
                            && node.PlacementAnchorSelector != CommanderSemanticAnchorSelector.WorkedResource)
                        || (node.PlacementAnchorSelector.HasValue && node.Count.Value != 1)) return false;
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

                case CommanderSemanticNodeType.AllocateWorkers:
                    if (node.WorkerAllocation == null || !node.WorkerAllocation.IsValid(context.MaximumPopulation)) return false;
                    dto = CommanderIntentDtoCodec.FromIntent(new AllocateWorkersIntent(context.PlayerId, node.WorkerAllocation));
                    break;

                case CommanderSemanticNodeType.ReachAge:
                    if (!node.AgeTarget.HasValue || node.UnitType.HasValue || node.BuildingType.HasValue
                        || node.ResourceType.HasValue || node.Count.HasValue
                        || node.StrategicObjectiveType.HasValue) return false;
                    dto.intentType = nameof(CommanderIntentType.ReachAge);
                    dto.targetAge = node.AgeTarget.Value.ToString();
                    break;

                case CommanderSemanticNodeType.MoveUnits:
                case CommanderSemanticNodeType.ScoutArea:
                case CommanderSemanticNodeType.PatrolArea:
                case CommanderSemanticNodeType.SetRallyPoint:
                case CommanderSemanticNodeType.AttackTarget:
                case CommanderSemanticNodeType.DefendArea:
                case CommanderSemanticNodeType.RetreatUnits:
                case CommanderSemanticNodeType.RepairTarget:
                case CommanderSemanticNodeType.ResearchTechnology:
                    if (!node.UnitSelector.HasValue || !node.LocationSelector.HasValue)
                        return false;
                    if (node.Type == CommanderSemanticNodeType.ResearchTechnology && !node.Technology.HasValue)
                        return false;
                    if (node.TargetSelector.HasValue && !node.TargetSelector.Value.IsCompatible(ToCapabilityAction(node.Type),
                        ToLocationSelector(node.LocationSelector.Value))) return false;
                    intent = new CapabilityActionIntent(context.PlayerId,
                        ToCapabilityAction(node.Type), ToUnitSelector(node.UnitSelector.Value, node.UnitType, node.Count),
                        new CommanderLocationSelector(ToLocationSelector(node.LocationSelector.Value),
                            node.ResourceType), node.Technology, node.BuildingType,
                        node.Constraints, node.TargetSelector);
                    safeReason = string.Empty;
                    return true;

                default:
                    return false;
            }

            CommanderIntentInterpretation validated = CommanderIntentDtoCodec.ValidateAndConvert(dto, context);
            if (!validated.Success || validated.Intent == null || validated.StrategicIntent != null)
                return false;
            intent = node.Type == CommanderSemanticNodeType.BuildStructure
                && node.PlacementAnchorSelector.HasValue
                ? new BuildStructureIntent(context.PlayerId, node.BuildingType.Value, node.Count.Value,
                    validated.Intent.Constraints, node.PlacementAnchorSelector,
                    node.PlacementAnchorOrdinal, node.PlacementRelation, node.ClearGapTiles,
                    node.ResourceType)
                : validated.Intent;
            safeReason = string.Empty;
            return true;
        }

        private static CommanderCapabilityActionType ToCapabilityAction(CommanderSemanticNodeType type)
        {
            return (CommanderCapabilityActionType)Enum.Parse(typeof(CommanderCapabilityActionType), type.ToString());
        }

        private static CommanderUnitSelector ToUnitSelector(CommanderSemanticUnitSelector selector,
            int? unitType, int? count)
        {
            CommanderUnitSelectorKind kind;
            switch (selector)
            {
                case CommanderSemanticUnitSelector.Scout: kind = CommanderUnitSelectorKind.Scout; break;
                case CommanderSemanticUnitSelector.Villagers: kind = CommanderUnitSelectorKind.Villagers; break;
                case CommanderSemanticUnitSelector.Spearman: kind = CommanderUnitSelectorKind.UnitType; unitType = 1; break;
                case CommanderSemanticUnitSelector.Archer: kind = CommanderUnitSelectorKind.UnitType; unitType = 2; break;
                case CommanderSemanticUnitSelector.Knight: kind = CommanderUnitSelectorKind.UnitType; unitType = 7; break;
                case CommanderSemanticUnitSelector.DamagedMilitary: kind = CommanderUnitSelectorKind.DamagedMilitary; break;
                default: kind = CommanderUnitSelectorKind.Military; break;
            }
            return new CommanderUnitSelector(kind, Math.Max(1, count ?? 1), unitType ?? -1);
        }

        private static CommanderLocationSelectorKind ToLocationSelector(CommanderSemanticLocationSelector selector)
        {
            return (CommanderLocationSelectorKind)Enum.Parse(typeof(CommanderLocationSelectorKind), selector.ToString());
        }
    }
}
