using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private bool hasPlayerMarkerSnapshot;
        private int playerMarkerTileX;
        private int playerMarkerTileZ;

        internal bool TrySetPlayerMarkerSnapshot(int tileX, int tileZ)
        {
            if (IsDisposed || simulation == null || tileX < 0 || tileZ < 0
                || tileX >= simulation.MapData.Width || tileZ >= simulation.MapData.Height
                || simulation.FogOfWar.GetVisibility(playerId, tileX, tileZ)
                    != TileVisibility.Visible) return false;
            playerMarkerTileX = tileX;
            playerMarkerTileZ = tileZ;
            hasPlayerMarkerSnapshot = true;
            return true;
        }

        internal void ClearPlayerMarkerSnapshot() => hasPlayerMarkerSnapshot = false;

        internal bool TryGetPlayerMarkerSnapshot(out int tileX, out int tileZ)
        {
            tileX = playerMarkerTileX;
            tileZ = playerMarkerTileZ;
            return hasPlayerMarkerSnapshot && !IsDisposed
                && simulation.FogOfWar.GetVisibility(playerId, tileX, tileZ)
                    == TileVisibility.Visible;
        }

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
            var units = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            foreach (var node in program.Nodes)
            {
                switch (node.Primitive.Mechanic)
                {
                    case CommanderDynamicMechanic.SelectStructures:
                        structures.Add(node.Id, FreezeStructures(node));
                        break;
                    case CommanderDynamicMechanic.SelectUnits:
                        units.Add(node.Id, FreezeUnits(node));
                        break;
                }
            }

            foreach (var node in program.Nodes)
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.SelectResources)
                    resources.Add(node.Id, FreezeResources(node, program, goalsById, units));

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
                    if ((string)semanticAnchor == "PlayerMarker")
                    {
                        if (!TryGetPlayerMarkerSnapshot(out int markerX, out int markerZ))
                            throw new ArgumentException("Where? Place a marker or describe the location.");
                        bound = CommanderBoundLocation.FromPlayerMarker(simulation, this, playerId,
                            markerX, markerZ, relation, gap);
                        locations.Add(node.Id, bound);
                        continue;
                    }
                    var selector = (CommanderSemanticAnchorSelector)Enum.Parse(
                        typeof(CommanderSemanticAnchorSelector), (string)semanticAnchor);
                    if (!resolver.TryResolveOwnedAnchor(playerId, selector, null,
                        out BuildingData anchor))
                        throw new ArgumentException("The owned semantic anchor is unavailable.");
                    if (node.Parameters.TryGetValue("resource", out object resourceValue))
                        bound = CommanderBoundLocation.FromResourceSide(simulation, this, playerId,
                            anchor, (ResourceType)Enum.Parse(typeof(ResourceType), (string)resourceValue),
                            (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                                node.Parameter<string>("sourceKind")), gap);
                    else bound = CommanderBoundLocation.FromBuilding(simulation, this, playerId,
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
            var allocationBindings = new List<(AllocateWorkersGoal goal, CommanderBoundLocation location)>();
            var moveBindings = new List<(CommanderCapabilityGoal goal, CommanderBoundLocation location,
                IReadOnlyList<int> actorIds)>();
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
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers
                    && node.Inputs.TryGetValue("location", out string resourceLocationId))
                {
                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                        || !(effect is AllocateWorkersGoal allocation)
                        || !locations.TryGetValue(resourceLocationId, out CommanderBoundLocation bound))
                        throw new ArgumentException("The resource-side location is unavailable.");
                    allocationBindings.Add((allocation, bound));
                }
                if (node.Primitive.Mechanic == CommanderDynamicMechanic.MoveWorkers
                    || node.Primitive.Mechanic == CommanderDynamicMechanic.MoveUnits)
                {
                    bool unitMove = node.Primitive.Mechanic == CommanderDynamicMechanic.MoveUnits;
                    if (!goalsById.TryGetValue(node.Id, out CommanderGoal effect)
                        || !(effect is CommanderCapabilityGoal move)
                        || !node.Inputs.TryGetValue("location", out string moveLocationId)
                        || !locations.TryGetValue(moveLocationId, out CommanderBoundLocation bound)
                        || !(bound.IsResourceSide || unitMove && bound.IsPlayerMarkerFor(this)))
                        throw new ArgumentException("The exact selected move location is unavailable.");
                    IReadOnlyList<int> actorIds = null;
                    if (unitMove && (!node.Inputs.TryGetValue("units", out string selectedUnitId)
                        || !units.TryGetValue(selectedUnitId, out actorIds)
                        || actorIds.Count != move.Action.UnitSelector.Count))
                        throw new ArgumentException("The exact selected action units are unavailable.");
                    moveBindings.Add((move, bound, actorIds));
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
            foreach (var binding in allocationBindings) binding.goal.DynamicResourceLocation = binding.location;
            foreach (var binding in moveBindings)
            {
                if (binding.location.IsPlayerMarkerFor(this)) binding.goal.MarkerMoveLocation = binding.location;
                else binding.goal.DynamicMoveLocation = binding.location;
                binding.goal.DynamicMoveUnitIds = binding.actorIds;
            }
            foreach (var binding in producerBindings) binding.goal.BoundProducerBuildingIds = binding.ids;
        }

        private IReadOnlyList<int> FreezeResources(CommanderDynamicNode node,
            CommanderDynamicPlan program, IReadOnlyDictionary<string, CommanderGoal> goalsById,
            IReadOnlyDictionary<string, IReadOnlyList<int>> units)
        {
            var type = (ResourceType)Enum.Parse(typeof(ResourceType),
                node.Parameter<string>("resource"));
            var source = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                node.Parameter<string>("sourceKind"));
            bool worked = node.Parameter<string>("mode") == "Worked";
            IReadOnlyList<int> actorIds = null;
            if (node.Inputs.TryGetValue("units", out string unitId)) actorIds = units[unitId];
            else if (node.Inputs.TryGetValue("workers", out string workerId))
            {
                foreach (CommanderDynamicNode effectNode in program.Nodes)
                    if (effectNode.Inputs.TryGetValue("workers", out string sourceId)
                        && sourceId == workerId && goalsById.TryGetValue(effectNode.Id, out CommanderGoal goal)
                        && goal.FrozenWorkerIds != null)
                    { actorIds = goal.FrozenWorkerIds; break; }
                if (actorIds == null) throw new ArgumentException("The selected action workers are unavailable.");
            }

            int anchorX, anchorZ, anchorWidth, anchorHeight;
            if (actorIds != null)
            {
                long xSum = 0, zSum = 0;
                foreach (int id in actorIds)
                {
                    UnitData actor = simulation.UnitRegistry.GetUnit(id);
                    if (actor == null || actor.PlayerId != playerId || actor.CurrentHealth <= 0
                        || actor.State == UnitState.Dead)
                        throw new ArgumentException("The selected action units are unavailable.");
                    Vector2Int tile = simulation.MapData.WorldToTile(actor.SimPosition);
                    xSum += tile.x; zSum += tile.y;
                }
                if (actorIds.Count == 0) throw new ArgumentException("The selected action units are unavailable.");
                anchorX = (int)(xSum / actorIds.Count);
                anchorZ = (int)(zSum / actorIds.Count);
                anchorWidth = anchorHeight = 1;
            }
            else
            {
                var resolver = new CommanderSemanticReferenceResolver(simulation);
                if (!node.Parameters.ContainsKey("anchor")
                    && simulation.BuildingRegistry.GetAllBuildings().FindAll(b =>
                        CommanderBoundLocation.ValidBuilding(simulation, playerId, b,
                            BuildingType.TownCenter)).Count != 1)
                    throw new ArgumentException("Which location? Place a marker or describe the location.");
                if (!resolver.TryResolveOwnedAnchor(playerId,
                    CommanderSemanticAnchorSelector.MyTownCenter, null, out BuildingData tc))
                    throw new ArgumentException("Your Town Center location is unavailable.");
                anchorX = tc.OriginTileX; anchorZ = tc.OriginTileZ;
                anchorWidth = tc.TileFootprintWidth; anchorHeight = tc.TileFootprintHeight;
            }
            string direction = node.Parameters.TryGetValue("relation", out object relationValue)
                ? (string)relationValue : null;
            var ids = new List<ResourceNodeData>();
            foreach (ResourceNodeData resource in simulation.MapData.GetAllResourceNodes())
                if (CommanderBoundLocation.ValidResource(simulation, playerId, resource,
                    type, source, worked)
                    && MatchesResourceSide(resource, anchorX, anchorZ, anchorWidth,
                        anchorHeight, direction)
                    && ReachableResource(resource, actorIds)) ids.Add(resource);
            if (type == ResourceType.Wood && source == ResourceSourceKind.Tree
                && direction == null && actorIds == null && !worked
                && ids.TrueForAll(resource =>
                    CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, resource) == 0))
                throw new ArgumentException("Which woodline? Place a marker or describe the location.");
            if (type == ResourceType.Wood && source == ResourceSourceKind.Tree)
                RankWoodlineClusters(simulation, playerId, ids, anchorX, anchorZ);
            else ids.Sort((a, b) =>
            {
                int workers = CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, b)
                    .CompareTo(CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, a));
                if (workers != 0) return workers;
                long distanceA = ResourceDistance(a, anchorX, anchorZ);
                long distanceB = ResourceDistance(b, anchorX, anchorZ);
                int distance = distanceA.CompareTo(distanceB);
                return distance != 0 ? distance : a.Id.CompareTo(b.Id);
            });
            int count = node.Parameter<int>("count");
            if (ids.Count < count)
            {
                string missing = worked
                    ? "I couldn't find a visible reachable woodline your villagers are working. Place a marker."
                    : type == ResourceType.Wood && source == ResourceSourceKind.Tree
                        && direction == "MapEast" && actorIds == null
                        ? "I couldn't find a visible reachable woodline right of your Town Center. Place a marker."
                        : type == ResourceType.Wood && source == ResourceSourceKind.Tree
                            && direction == "MapNorth" && actorIds != null
                            ? "I couldn't find a visible reachable woodline above your selected units. Place a marker."
                            : "I couldn't find a visible reachable resource at that location. Place a marker.";
                throw new ArgumentException(missing);
            }
            var selected = new int[count];
            for (int i = 0; i < count; i++) selected[i] = ids[i].Id;
            return Array.AsReadOnly(selected);
        }

        private static long ResourceDistance(ResourceNodeData resource, int x, int z)
        {
            long dx = (long)resource.TileX - x;
            long dz = (long)resource.TileZ - z;
            return dx * dx + dz * dz;
        }

        // The eligible set has already passed ownership-assignment, visibility, side,
        // and reachability filters. Rank connected visible woodlines as a whole, never
        // use hidden trees to bridge two otherwise separate candidates.
        internal static void RankWoodlineClusters(GameSimulation simulation, int playerId,
            List<ResourceNodeData> nodes, int anchorX, int anchorZ)
        {
            nodes.Sort((a, b) =>
            {
                int x = a.TileX.CompareTo(b.TileX);
                return x != 0 ? x : a.Id.CompareTo(b.Id);
            });
            int[] parent = new int[nodes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Root(int index)
            {
                while (parent[index] != index)
                {
                    parent[index] = parent[parent[index]];
                    index = parent[index];
                }
                return index;
            }
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    ResourceNodeData a = nodes[i], b = nodes[j];
                    if (b.TileX > a.TileX + a.FootprintWidth + 1) break;
                    if (b.TileZ > a.TileZ + a.FootprintHeight + 1
                        || a.TileZ > b.TileZ + b.FootprintHeight + 1) continue;
                    int first = Root(i), second = Root(j);
                    if (first != second) parent[second] = first;
                }
            var groups = new Dictionary<int, (int workers, long distance, int identity)>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int root = Root(i);
                int workers = CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, nodes[i]);
                long distance = ResourceDistance(nodes[i], anchorX, anchorZ);
                if (!groups.TryGetValue(root, out var group))
                    groups.Add(root, (workers, distance, nodes[i].Id));
                else groups[root] = (group.workers + workers,
                    Math.Min(group.distance, distance), Math.Min(group.identity, nodes[i].Id));
            }
            var ranking = new Dictionary<int, (int workers, long distance, int identity)>();
            for (int i = 0; i < nodes.Count; i++) ranking[nodes[i].Id] = groups[Root(i)];
            nodes.Sort((a, b) =>
            {
                var left = ranking[a.Id];
                var right = ranking[b.Id];
                int workers = right.workers.CompareTo(left.workers);
                if (workers != 0) return workers;
                int distance = left.distance.CompareTo(right.distance);
                if (distance != 0) return distance;
                int identity = left.identity.CompareTo(right.identity);
                if (identity != 0) return identity;
                int nodeWorkers = CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, b)
                    .CompareTo(CommanderBoundLocation.CountOwnedWorkers(simulation, playerId, a));
                if (nodeWorkers != 0) return nodeWorkers;
                int nodeDistance = ResourceDistance(a, anchorX, anchorZ)
                    .CompareTo(ResourceDistance(b, anchorX, anchorZ));
                return nodeDistance != 0 ? nodeDistance : a.Id.CompareTo(b.Id);
            });
        }

        private static bool MatchesResourceSide(ResourceNodeData resource, int x, int z,
            int width, int height, string direction)
        {
            switch (direction)
            {
                case null: return true;
                case "MapWest": return resource.TileX + resource.FootprintWidth <= x;
                case "MapEast": return resource.TileX >= x + width;
                case "MapNorth": return resource.TileZ >= z + height;
                case "MapSouth": return resource.TileZ + resource.FootprintHeight <= z;
                default: return false;
            }
        }

        private bool ReachableResource(ResourceNodeData resource, IReadOnlyList<int> actors)
        {
            var candidates = new List<UnitData>();
            if (actors != null)
            {
                foreach (int id in actors) candidates.Add(simulation.UnitRegistry.GetUnit(id));
            }
            else
            {
                foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
                    if (unit != null && unit.PlayerId == playerId && unit.IsVillager
                        && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                        candidates.Add(unit);
            }
            foreach (UnitData actor in candidates)
            {
                if (actor == null || actor.PlayerId != playerId || actor.CurrentHealth <= 0
                    || actor.State == UnitState.Dead) continue;
                Vector2Int start = simulation.MapData.WorldToTile(actor.SimPosition);
                for (int x = resource.TileX - 1; x <= resource.TileX + resource.FootprintWidth; x++)
                    for (int z = resource.TileZ - 1; z <= resource.TileZ + resource.FootprintHeight; z++)
                    {
                        if (x >= resource.TileX && x < resource.TileX + resource.FootprintWidth
                            && z >= resource.TileZ && z < resource.TileZ + resource.FootprintHeight)
                            continue;
                        if (!simulation.MapData.IsWalkable(x, z, playerId,
                            simulation.BuildingRegistry)
                            || simulation.FogOfWar.GetVisibility(playerId, x, z)
                                != TileVisibility.Visible) continue;
                        if (GridPathfinder.TryFindCompletePath(simulation.MapData, start,
                            new Vector2Int(x, z), out List<Vector2Int> path, playerId,
                            simulation.BuildingRegistry)) return true;
                    }
            }
            return false;
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

        private IReadOnlyList<int> FreezeUnits(CommanderDynamicNode node)
        {
            var knowledge = GameKnowledgeCatalog.Build(simulation);
            if (!CommanderDynamicCompiler.TryCapabilityUnitSelector(node, knowledge, null,
                simulation, playerId, out CommanderUnitSelector selector))
                throw new ArgumentException("No owned visible units match the requested selection, or the selection exceeds 50 units.");
            var executor = new CommanderCapabilityExecutor(simulation);
            var ids = new List<int>();
            foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
            {
                if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead
                    || !executor.MatchesSelector(unit, selector)) continue;
                Vector2Int tile = simulation.MapData.WorldToTile(unit.SimPosition);
                if (simulation.FogOfWar.GetVisibility(playerId, tile.x, tile.y)
                    == TileVisibility.Visible) ids.Add(unit.Id);
            }
            return ExactFirst(ids, node.Parameters.ContainsKey("countMode")
                ? ids.Count : node.Parameter<int>("count"));
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
