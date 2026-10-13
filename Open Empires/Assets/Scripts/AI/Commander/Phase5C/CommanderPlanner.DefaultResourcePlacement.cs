using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        // Ordinary deterministic placement, using the same visible woodline ranking as
        // typed resource selectors. No provider intent or extra gathering is inferred.
        private CommanderPlan PlanDefaultWoodBuilding(CommanderGoal goal, BuildingData townCenter,
            int currentTick, int owned, int queued)
        {
            var sources = new List<ResourceNodeData>();
            foreach (var resource in simulation.MapData.GetAllResourceNodes())
                if (CommanderBoundLocation.ValidResource(simulation, goal.PlayerId, resource,
                    ResourceType.Wood, ResourceSourceKind.Tree, false)) sources.Add(resource);
            CommanderGoalManager.RankWoodlineClusters(simulation, goal.PlayerId, sources,
                townCenter.OriginTileX, townCenter.OriginTileZ);
            GetFootprint(BuildingType.LumberYard, out int width, out int height);
            foreach (var resource in sources)
            {
                var candidates = CommanderSemanticPlacementCandidates.Generate(resource.TileX, resource.TileZ,
                    resource.FootprintWidth, resource.FootprintHeight, width, height,
                    simulation.MapData.Width, simulation.MapData.Height,
                    CommanderSemanticPlacementRelation.Near, 1);
                foreach (Vector2Int tile in candidates)
                {
                    if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height, 1,
                        BuildingType.LumberYard)) continue;
                    var builder = FindReachableSemanticBuilder(goal, tile, width, height, currentTick);
                    if (builder == null) continue;
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "Building a Lumber Yard beside the suitable visible woodline with the most assigned villagers.",
                        owned, queued, new PlaceBuildingCommand(goal.PlayerId, BuildingType.LumberYard,
                            tile.x, tile.y, new[] { builder.Id }));
                }
            }
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                "No visible, reachable legal Lumber Yard site was found beside a woodline. Choose a destination marker for a new placement request.",
                owned, queued);
        }
    }
}
