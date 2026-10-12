using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Static lowering only. The full symbolic program is retained for commit-time binding.
    internal static class CommanderDynamicCompiler
    {
        private const string Rejected = "The action plan uses unavailable Commander content or mechanics.";

        internal static bool TryCompile(CommanderDynamicPlan program, GameSimulation simulation,
            CommanderGoalManager manager, out CommanderSemanticGraphPlan graph, out string reason)
        {
            graph = null;
            reason = Rejected;
            if (program == null || simulation == null || manager == null || manager.IsDisposed
                || !ReferenceEquals(manager.Simulation, simulation) || program.Nodes.Count < 1
                || program.Nodes.Count > CommanderDynamicPlan.MaximumNodes) return false;

            var knowledge = GameKnowledgeCatalog.Build(simulation);
            CivilizationKnowledge civilization = null;
            Civilization current = simulation.GetPlayerCivilization(manager.PlayerId);
            foreach (var candidate in knowledge.Civilizations)
                if (candidate.Civilization == current) { civilization = candidate; break; }
            if (civilization == null) return false;

            bool hasProduction = false, hasConstruction = false;
            foreach (var node in program.Nodes)
            {
                hasProduction |= node.Primitive.Mechanic == CommanderDynamicMechanic.Produce;
                hasConstruction |= node.Primitive.Mechanic == CommanderDynamicMechanic.Build;
            }
            foreach (var constraint in program.Constraints)
                if ((constraint is NoConstructionConstraint && hasConstruction)
                    || (constraint is MaximumQueueConstraint && !hasProduction))
                {
                    reason = "The action plan contradicts its shared construction or production constraints.";
                    return false;
                }

            var byId = new Dictionary<string, CommanderDynamicNode>(StringComparer.Ordinal);
            foreach (var node in program.Nodes)
                if (node == null || !byId.TryAdd(node.Id, node)) return false;

            var validator = new CommanderIntentValidator();
            var effectSources = new List<CommanderDynamicNode>();
            var effectIntents = new List<CommanderIntent>();
            var effectIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var node in program.Nodes)
            {
                if (!TryLowerNode(node, byId, knowledge, civilization, simulation,
                    manager.PlayerId, validator, program.Constraints, out CommanderIntent intent, out reason))
                    return false;
                if (intent == null) continue;
                effectIndices.Add(node.Id, effectSources.Count);
                effectSources.Add(node);
                effectIntents.Add(intent);
            }
            if (effectSources.Count < 1 || effectSources.Count > CommanderGoalManager.MaxActiveGoals)
            {
                reason = "The action plan has no executable effect or exceeds the active goal limit.";
                return false;
            }

            var effects = new List<CommanderSemanticGraphNode>(effectSources.Count);
            for (int i = 0; i < effectSources.Count; i++)
            {
                var source = effectSources[i];
                var nearest = new HashSet<int>();
                foreach (string dependency in source.DependsOn)
                    if (!CollectNearestEffects(dependency, byId, effectIndices, nearest))
                        return false;
                nearest.Remove(i);
                var dependencies = new List<int>(nearest);
                dependencies.Sort();
                int? producer = null;
                if (source.Primitive.Mechanic == CommanderDynamicMechanic.Produce
                    && source.Inputs.TryGetValue("producers", out string producerId)
                    && byId[producerId].Primitive.Mechanic == CommanderDynamicMechanic.Build)
                {
                    if (!effectIndices.TryGetValue(producerId, out int producerIndex)
                        || !dependencies.Contains(producerIndex)) return false;
                    producer = producerIndex;
                }
                effects.Add(new CommanderSemanticGraphNode(i, effectIntents[i],
                    dependencies.AsReadOnly(), producer, null, source.Id));
            }
            if (!TryStableOrder(effects, out var order)) return false;
            if (!CommanderProductionProjection.TryProject(effects, new CommanderContextBuilder().Build(simulation, manager),
                out var quantities, out reason)) return false;
            graph = new CommanderSemanticGraphPlan(effects.AsReadOnly(), order, program, quantities);
            reason = string.Empty;
            return true;
        }

        private static bool TryLowerNode(CommanderDynamicNode node,
            Dictionary<string, CommanderDynamicNode> byId, GameKnowledgeCatalog knowledge,
            CivilizationKnowledge civilization, GameSimulation simulation, int playerId,
            CommanderIntentValidator validator, IReadOnlyList<CommanderConstraint> globalConstraints,
            out CommanderIntent intent, out string reason)
        {
            intent = null;
            reason = Rejected;
            var constraints = new List<CommanderConstraint>();
            foreach (var constraint in globalConstraints)
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.Produce
                    || (!(constraint is MaximumQueueConstraint) && !(constraint is NoConstructionConstraint)
                        && (!(constraint is ResourceSourceConstraint)
                            || node.Primitive.Mechanic == CommanderDynamicMechanic.Build)))
                    constraints.Add(constraint);
            switch (node.Primitive.Mechanic)
            {
                case CommanderDynamicMechanic.SelectWorkers:
                case CommanderDynamicMechanic.PartitionWorkers:
                    if (!TryWorkerSelection(node, byId, simulation.Config.MaxPopulation,
                        out _, out _, out _)) return false;
                    break;
                case CommanderDynamicMechanic.SelectUnits:
                    if (node.Parameters.TryGetValue("unit", out object selectedUnit))
                    {
                        if (!TryUnit((string)selectedUnit, knowledge, civilization, out _))
                            return false;
                    }
                    else if (!SupportedUnitKind(node.Parameter<string>("kind"))) return false;
                    if (node.Parameter<int>("count") > 50) return false;
                    break;
                case CommanderDynamicMechanic.SelectStructures:
                    if (!TryBuilding(node.Parameter<string>("building"), knowledge,
                        civilization, out _)) return false;
                    break;
                case CommanderDynamicMechanic.SelectResources:
                    if (!CompatibleResource(node.Parameter<string>("resource"),
                        node.Parameter<string>("sourceKind"))
                        || !ResourceMode(node.Parameter<string>("mode"))) return false;
                    break;
                case CommanderDynamicMechanic.ResolveLocation:
                    if (!TryLocation(node, byId)) return false;
                    break;
                case CommanderDynamicMechanic.Build:
                    if (!TryBuilding(node.Parameter<string>("building"), knowledge,
                        civilization, out BuildingKnowledge building)
                        || !CommanderIntentCatalog.IsSupportedStructure(building.BuildingType))
                        return false;
                    if (node.Inputs.TryGetValue("workers", out string builders)
                        && (!byId.TryGetValue(builders, out var workers)
                            || !TryWorkerSelection(workers, byId, simulation.Config.MaxPopulation,
                                out _, out _, out _))) return false;
                    if (node.Inputs.TryGetValue("location", out string location)
                        && (!byId.TryGetValue(location, out var site) || !TryLocation(site, byId)))
                        return false;
                    intent = new BuildStructureIntent(playerId, building.BuildingType,
                        node.Parameter<int>("count"), constraints);
                    break;
                case CommanderDynamicMechanic.AllocateWorkers:
                    if (!CompatibleResource(node.Parameter<string>("resource"),
                        node.Parameter<string>("sourceKind"))
                        || !byId.TryGetValue(node.Inputs["workers"], out var source)
                        || !TryWorkerSelection(source, byId, simulation.Config.MaxPopulation,
                            out int selectedCount, out CommanderWorkerState state,
                            out ResourceType? currentResource)) return false;
                    var resource = (ResourceType)Enum.Parse(typeof(ResourceType),
                        node.Parameter<string>("resource"));
                    var sourceKind = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                        node.Parameter<string>("sourceKind"));
                    foreach (var restriction in globalConstraints)
                        if (restriction is ResourceSourceConstraint sourceRestriction
                            && sourceRestriction.Resource == resource && sourceRestriction.SourceKind != ResourceSourceKind.Any)
                        {
                            if (sourceKind != ResourceSourceKind.Any && sourceKind != sourceRestriction.SourceKind)
                            { reason = "Worker destination contradicts the shared resource-source restriction."; return false; }
                            sourceKind = sourceRestriction.SourceKind;
                        }
                    var allocation = new CommanderWorkerAllocation(
                        CommanderWorkerAllocationMode.SelectedCount,
                        CommanderWorkerCountMode.Exact, selectedCount,
                        new CommanderWorkerSelector(state, currentResource),
                        new CommanderResourceDestination(resource, sourceKind));
                    intent = new AllocateWorkersIntent(playerId, allocation, constraints);
                    break;
                case CommanderDynamicMechanic.Produce:
                    if (!TryUnit(node.Parameter<string>("unit"), knowledge, civilization,
                        out UnitKnowledge unit)
                        || !TryResolveTrainingRequest(unit, knowledge, simulation, playerId, out int requestedType)
                        || !simulation.TryGetProductionBuildingType(playerId, requestedType,
                            out BuildingType requiredProducer)) return false;
                    if (node.Inputs.TryGetValue("producers", out string producerId))
                    {
                        if (!byId.TryGetValue(producerId, out var producerNode)
                            || (producerNode.Primitive.Mechanic != CommanderDynamicMechanic.Build
                                && producerNode.Primitive.Mechanic !=
                                    CommanderDynamicMechanic.SelectStructures)
                            || !TryBuilding(producerNode.Parameter<string>("building"),
                                knowledge, civilization, out BuildingKnowledge producerBuilding)
                            || producerBuilding.BuildingType != requiredProducer)
                            return false;
                    }
                    int count = node.Parameter<int>("count");
                    string mode = node.Parameter<string>("quantityMode");
                    if (mode != "New" && mode != "TargetTotal") return false;
                    intent = new EnsureUnitCountIntent(playerId, requestedType, count,
                        constraints, newProductionCount: mode == "New" ? count : (int?)null);
                    break;
                default:
                    return false;
            }
            if (intent != null)
            {
                var validation = validator.Validate(intent, simulation, playerId);
                if (!validation.IsValid) { reason = validation.Reason; return false; }
            }
            reason = string.Empty;
            return true;
        }

        private static bool TryUnit(string id, GameKnowledgeCatalog knowledge,
            CivilizationKnowledge civilization, out UnitKnowledge unit)
        {
            unit = knowledge.FindUnit(id);
            return unit != null && unit.StableId == id
                && Contains(civilization.AvailableUnitIds, id);
        }

        // Map an actual canonical civilization identity onto an existing supported
        // training request through the game's resolver, not a second replacement table.
        internal static bool TryResolveTrainingRequest(UnitKnowledge unit, GameKnowledgeCatalog knowledge,
            GameSimulation simulation, int playerId, out int requestedType)
        {
            requestedType = -1;
            if (unit == null || knowledge == null || simulation == null) return false;
            foreach (var candidate in knowledge.Units)
                if (CommanderIntentCatalog.IsSupportedUnit(candidate.UnitType)
                    && simulation.ResolveCivUnitType(playerId, candidate.UnitType) == unit.UnitType
                    && (requestedType < 0 || candidate.UnitType < requestedType))
                    requestedType = candidate.UnitType;
            return requestedType >= 0;
        }

        private static bool TryBuilding(string id, GameKnowledgeCatalog knowledge,
            CivilizationKnowledge civilization, out BuildingKnowledge building)
        {
            building = knowledge.FindBuilding(id);
            return building != null && building.StableId == id
                && Contains(civilization.AvailableBuildingIds, id);
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (string item in list)
                if (string.Equals(item, value, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool CompatibleResource(string resourceName, string sourceName)
        {
            if (!Enum.TryParse(resourceName, false, out ResourceType resource)
                || resource.ToString() != resourceName
                || !Enum.TryParse(sourceName, false, out ResourceSourceKind source)
                || source.ToString() != sourceName) return false;
            return ResourceSourceRules.IsCompatible(resource, source);
        }

        private static bool ResourceMode(string mode) => mode == "Visible" || mode == "Worked";

        private static bool SupportedUnitKind(string kind)
        {
            switch (kind)
            {
                case "Military":
                case "Scout":
                case "Villagers":
                case "Spearman":
                case "Archer":
                case "Knight":
                case "DamagedMilitary":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryLocation(CommanderDynamicNode node,
            Dictionary<string, CommanderDynamicNode> byId)
        {
            if (node.Primitive.Mechanic != CommanderDynamicMechanic.ResolveLocation)
                return false;
            string relation = node.Parameter<string>("relation");
            int gap = node.Parameter<int>("clearGapTiles");
            if (relation != "Near" && relation != "MapWest" && relation != "MapEast")
                return false;
            if (gap < CommanderSemanticJson.MinimumClearGapTiles
                || gap > CommanderSemanticJson.MaximumClearGapTiles
                || (relation == "Near" && gap != 1)) return false;
            if (node.Parameters.TryGetValue("anchor", out object anchor))
                return (string)anchor == "MyTownCenter" || (string)anchor == "MyBarracks";
            if (node.Inputs.TryGetValue("structures", out string structures))
                return byId.TryGetValue(structures, out var source)
                    && source.Primitive.Result == CommanderDynamicResultKind.StructureSet;
            if (node.Inputs.TryGetValue("resources", out string resources))
                return byId.TryGetValue(resources, out var resourceSource)
                    && resourceSource.Primitive.Result == CommanderDynamicResultKind.ResourceSet;
            return false;
        }

        private static bool TryWorkerSelection(CommanderDynamicNode node,
            Dictionary<string, CommanderDynamicNode> byId, int maximumPopulation,
            out int count, out CommanderWorkerState state, out ResourceType? currentResource)
        {
            count = 0; state = CommanderWorkerState.Any; currentResource = null;
            if (node.Primitive.Mechanic == CommanderDynamicMechanic.PartitionWorkers)
            {
                if (!node.Inputs.TryGetValue("workers", out string sourceId)
                    || !byId.TryGetValue(sourceId, out var source)
                    || !TryWorkerSelection(source, byId, maximumPopulation,
                        out int sourceCount, out state, out currentResource))
                    return false;
                int offset = node.Parameter<int>("offset");
                count = node.Parameter<int>("count");
                return offset >= 0 && count >= 1 && offset <= sourceCount
                    && count <= sourceCount - offset;
            }
            if (node.Primitive.Mechanic != CommanderDynamicMechanic.SelectWorkers)
                return false;
            count = node.Parameter<int>("count");
            if (count < 1 || count > maximumPopulation) return false;
            if (node.Parameters.TryGetValue("state", out object stateName)
                && (!Enum.TryParse((string)stateName, false, out state)
                    || state.ToString() != (string)stateName)) return false;
            if (node.Parameters.TryGetValue("currentResource", out object resourceName))
            {
                if (state != CommanderWorkerState.Gathering
                    || !Enum.TryParse((string)resourceName, false, out ResourceType current)
                    || current.ToString() != (string)resourceName) return false;
                currentResource = current;
            }
            return true;
        }

        private static bool CollectNearestEffects(string id,
            Dictionary<string, CommanderDynamicNode> byId,
            Dictionary<string, int> effectIndices, HashSet<int> found)
        {
            if (effectIndices.TryGetValue(id, out int effect)) { found.Add(effect); return true; }
            if (!byId.TryGetValue(id, out var source)) return false;
            foreach (string dependency in source.DependsOn)
                if (!CollectNearestEffects(dependency, byId, effectIndices, found)) return false;
            return true;
        }

        private static bool TryStableOrder(IReadOnlyList<CommanderSemanticGraphNode> effects,
            out IReadOnlyList<int> order)
        {
            var result = new List<int>(effects.Count);
            var done = new bool[effects.Count];
            for (int step = 0; step < effects.Count; step++)
            {
                int next = -1;
                for (int i = 0; i < effects.Count; i++)
                {
                    if (done[i]) continue;
                    bool ready = true;
                    foreach (int dependency in effects[i].DependsOn)
                        if (dependency < 0 || dependency >= effects.Count || !done[dependency])
                            { ready = false; break; }
                    if (ready) { next = i; break; }
                }
                if (next < 0) { order = null; return false; }
                done[next] = true;
                result.Add(next);
            }
            order = result.AsReadOnly();
            return true;
        }
    }
}
