using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        // Called on pending, unregistered goals. Resolve every pure selection first so a
        // missing member rejects the whole candidate before any goal or command is admitted.
        private void PrepareDynamicLocationBindings(CommanderSemanticGraphPlan plan,
            IReadOnlyDictionary<int, CommanderGoal> pending)
        {
            if (plan?.DynamicProgram == null) return;
            if (pending == null) throw new ArgumentException("Dynamic goals are unavailable.");
            var program = plan.DynamicProgram;
            var byId = new Dictionary<string, CommanderDynamicNode>(StringComparer.Ordinal);
            foreach (var node in program.Nodes) byId.Add(node.Id, node);

            var goalsById = new Dictionary<string, CommanderGoal>(StringComparer.Ordinal);
            foreach (var effect in plan.Nodes)
            {
                if (effect.DynamicNodeId == null
                    || !pending.TryGetValue(effect.Index, out CommanderGoal goal)
                    || goal == null || goal.PlayerId != playerId
                    || !goalsById.TryAdd(effect.DynamicNodeId, goal))
                    throw new ArgumentException("A dynamic effect has no owned pending goal.");
            }

            var resources = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            var structures = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            foreach (var node in program.Nodes)
            {
                switch (node.Primitive.Mechanic)
                {
                    case CommanderDynamicMechanic.SelectResources:
                        resources.Add(node.Id, FreezeResources(node));
                        break;
                    case CommanderDynamicMechanic.SelectStructures:
                        structures.Add(node.Id, FreezeStructures(node));
                        break;
                    case CommanderDynamicMechanic.SelectUnits:
                        FreezeUnits(node); // No effect consumes UnitSet yet; still preflight it.
                        break;
                }
            }

            var locations = new Dictionary<string, CommanderBoundLocation>(StringComparer.Ordinal);
            var futureAnchors = new HashSet<BuildStructureGoal>();
            var resolver = new CommanderSemanticReferenceResolver(simulation);
            foreach (var node in program.Nodes)
            {
                if (node.Primitive.Mechanic != CommanderDynamicMechanic.ResolveLocation)
                    continue;
                var relation = ParseRelation(node.Parameter<string>("relation"));
                int gap = node.Parameter<int>("clearGapTiles");
                CommanderBoundLocation bound;
                if (node.Parameters.TryGetValue("anchor", out object semanticAnchor))
                {
                    var selector = (CommanderSemanticAnchorSelector)Enum.Parse(
                        typeof(CommanderSemanticAnchorSelector), (string)semanticAnchor);
                    if (!resolver.TryResolveOwnedAnchor(playerId, selector, null,
                        out BuildingData anchor))
                        throw new ArgumentException("The owned semantic anchor is unavailable.");
                    bound = CommanderBoundLocation.FromBuilding(simulation, this, playerId,
                        anchor, relation, gap);
                }
                else if (node.Inputs.TryGetValue("structures", out string structureNodeId))
                {
                    CommanderDynamicNode source = byId[structureNodeId];
                    if (source.Primitive.Mechanic == CommanderDynamicMechanic.SelectStructures)
                    {
                        IReadOnlyList<int> selected = structures[structureNodeId];
                        if (selected.Count != 1)
                            throw new ArgumentException("The placement needs one exact structure anchor.");
                        bound = CommanderBoundLocation.FromBuilding(simulation, this, playerId,
                            simulation.BuildingRegistry.GetBuilding(selected[0]), relation, gap);
                    }
                    else if (source.Primitive.Mechanic == CommanderDynamicMechanic.Build
                        && goalsById.TryGetValue(structureNodeId, out CommanderGoal sourceGoal)
                        && sourceGoal is BuildStructureGoal future && future.Count == 1)
                    {
                        bound = CommanderBoundLocation.FromFutureBuild(simulation, this,
                            playerId, future, relation, gap);
                        futureAnchors.Add(future);
                    }
                    else throw new ArgumentException("The placement structure result is ambiguous.");
                }
                else if (node.Inputs.TryGetValue("resources", out string resourceNodeId))
                {
                    CommanderDynamicNode source = byId[resourceNodeId];
                    if (source.Primitive.Mechanic != CommanderDynamicMechanic.SelectResources
                        || !resources.TryGetValue(resourceNodeId, out IReadOnlyList<int> selected)
                        || selected.Count != 1)
                        throw new ArgumentException("The placement needs one exact resource anchor.");
                    var type = (ResourceType)Enum.Parse(typeof(ResourceType),
                        source.Parameter<string>("resource"));
                    var sourceKind = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                        source.Parameter<string>("sourceKind"));
                    bool worked = source.Parameter<string>("mode") == "Worked";
                    bound = CommanderBoundLocation.FromResource(simulation, this, playerId,
                        simulation.MapData.GetResourceNode(selected[0]), type, sourceKind,
                        worked, relation, gap);
                }
                else throw new ArgumentException("The semantic placement has no anchor.");
                locations.Add(node.Id, bound);
            }

            var buildBindings = new List<(BuildStructureGoal goal, CommanderBoundLocation location)>();
            var producerBindings = new List<(EnsureUnitCountGoal goal, IReadOnlyList<int> ids)>();
            foreach (var node in program.Nodes)
            {
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.Build
                    && node.Inputs.TryGetValue("location", out string locationId))
                {
                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                        || !(effect is BuildStructureGoal build)
                        || !locations.TryGetValue(locationId, out CommanderBoundLocation bound))
                        throw new ArgumentException("The building location is unavailable.");
                    buildBindings.Add((build, bound));
                }
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.Produce
                    && node.Inputs.TryGetValue("producers", out string producerNodeId)
                    && byId[producerNodeId].Primitive.Mechanic ==
                        CommanderDynamicMechanic.SelectStructures)
                {
                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                        || !(effect is EnsureUnitCountGoal produce)
                        || !structures.TryGetValue(producerNodeId, out IReadOnlyList<int> selected)
                        || selected.Count < 1)
                        throw new ArgumentException("The exact producer set is unavailable.");
                    producerBindings.Add((produce, Array.AsReadOnly(Copy(selected))));
                }
            }

            foreach (BuildStructureGoal source in futureAnchors) source.HasResultConsumer = true;
            foreach (var binding in buildBindings) binding.goal.DynamicLocation = binding.location;
            foreach (var binding in producerBindings) binding.goal.BoundProducerBuildingIds = binding.ids;
        }

        private IReadOnlyList<int> FreezeResources(CommanderDynamicNode node)
        {
            var type = (ResourceType)Enum.Parse(typeof(ResourceType),
                node.Parameter<string>("resource"));
            var source = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                node.Parameter<string>("sourceKind"));
            bool worked = node.Parameter<string>("mode") == "Worked";
            var ids = new List<int>();
            foreach (ResourceNodeData resource in simulation.MapData.GetAllResourceNodes())
                if (CommanderBoundLocation.ValidResource(simulation, playerId, resource,
                    type, source, worked)) ids.Add(resource.Id);
            return ExactFirst(ids, node.Parameter<int>("count"));
        }

        private IReadOnlyList<int> FreezeStructures(CommanderDynamicNode node)
        {
            string id = node.Parameter<string>("building");
            var type = (BuildingType)Enum.Parse(typeof(BuildingType), id.Substring("building:".Length));
            var ids = new List<int>();
            foreach (BuildingData building in simulation.BuildingRegistry.GetAllBuildings())
                if (CommanderBoundLocation.ValidBuilding(simulation, playerId, building, type))
                    ids.Add(building.Id);
            return ExactFirst(ids, node.Parameter<int>("count"));
        }

        private void FreezeUnits(CommanderDynamicNode node)
        {
            int? selectedType = null;
            if (node.Parameters.TryGetValue("unit", out object canonicalId))
            {
                string id = (string)canonicalId;
                int type = int.Parse(id.Substring("unit:".Length));
                selectedType = type;
            }
            string kind = node.Parameters.TryGetValue("kind", out object kindValue)
                ? (string)kindValue : null;
            var ids = new List<int>();
            foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
            {
                if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead
                    || !MatchesUnitSelection(unit, selectedType, kind)) continue;
                Vector2Int tile = simulation.MapData.WorldToTile(unit.SimPosition);
                if (simulation.FogOfWar.GetVisibility(playerId, tile.x, tile.y)
                    == TileVisibility.Visible) ids.Add(unit.Id);
            }
            ExactFirst(ids, node.Parameter<int>("count"));
        }

        private bool MatchesUnitSelection(UnitData unit, int? selectedType, string kind)
        {
            if (selectedType.HasValue) return unit.UnitType == selectedType.Value;
            switch (kind)
            {
                case "Scout": return unit.UnitType == 4;
                case "Villagers": return unit.IsVillager;
                case "Spearman": return unit.UnitType == simulation.ResolveCivUnitType(playerId, 1);
                case "Archer": return unit.UnitType == simulation.ResolveCivUnitType(playerId, 2);
                case "Knight": return unit.UnitType == simulation.ResolveCivUnitType(playerId, 7);
                case "DamagedMilitary":
                    return !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4
                        && unit.CurrentHealth < unit.MaxHealth;
                case "Military":
                    return !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4;
                default: return false;
            }
        }

        private static IReadOnlyList<int> ExactFirst(List<int> candidates, int count)
        {
            candidates.Sort();
            if (count < 1 || candidates.Count < count)
                throw new ArgumentException("The full visible selection is unavailable.");
            var selected = new int[count];
            for (int i = 0; i < count; i++) selected[i] = candidates[i];
            return Array.AsReadOnly(selected);
        }

        private static int[] Copy(IReadOnlyList<int> values)
        {
            var result = new int[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i];
            return result;
        }

        private static CommanderSemanticPlacementRelation ParseRelation(string name)
        {
            if (name == "Near") return CommanderSemanticPlacementRelation.Near;
            if (name == "MapWest") return CommanderSemanticPlacementRelation.MapWest;
            if (name == "MapEast") return CommanderSemanticPlacementRelation.MapEast;
            throw new ArgumentException("Unknown semantic relation.");
        }
    }
}
