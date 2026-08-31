using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal readonly struct CommanderPlan
    {
        public readonly CommanderGoalStatus Status;
        public readonly string Reason;
        public readonly ICommand Command;
        public readonly int OwnedCount;
        public readonly int QueuedCount;

        public CommanderPlan(CommanderGoalStatus status, string reason, int ownedCount,
            int queuedCount, ICommand command = null)
        {
            Status = status;
            Reason = reason;
            OwnedCount = ownedCount;
            QueuedCount = queuedCount;
            Command = command;
        }
    }

    internal sealed class CommanderPlanner
    {
        private const int EconomyCommandCooldownTicks = 90;
        private const int SpearmanBaseUnitType = 1;
        private readonly GameSimulation simulation;

        public CommanderPlanner(GameSimulation simulation)
        {
            this.simulation = simulation;
        }

        public CommanderPlan Plan(EnsureUnitCountGoal goal, int currentTick)
        {
            if (goal.RequestedUnitType != SpearmanBaseUnitType)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    $"Phase 1 supports requested unit type {SpearmanBaseUnitType} only.", 0, 0);

            int resolvedUnitType = simulation.ResolveCivUnitType(goal.PlayerId, goal.RequestedUnitType);
            int owned = CountOwnedLivingUnits(goal.PlayerId, resolvedUnitType);
            int queued = CountQueuedUnits(goal.PlayerId, resolvedUnitType);

            if (owned >= goal.TargetTotal)
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"Owned {owned}/{goal.TargetTotal} living units.", owned, queued);

            BuildingData barracks = FindProductionBuilding(goal.PlayerId, BuildingType.Barracks, false);
            if (barracks == null)
            {
                BuildingData unfinished = FindProductionBuilding(goal.PlayerId, BuildingType.Barracks, true);
                if (unfinished != null)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                        $"Barracks #{unfinished.Id} is under construction.", owned, queued);

                return PlanBuilding(goal, BuildingType.Barracks, currentTick, owned, queued);
            }

            int remainingOrders = goal.TargetTotal - owned - queued;
            if (remainingOrders <= 0)
                return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                    $"Owned {owned}, queued {queued}, target {goal.TargetTotal}.", owned, queued);

            int totalQueuedPopulation = CountAllQueuedUnits(goal.PlayerId);
            if (simulation.GetPopulation(goal.PlayerId) + totalQueuedPopulation >= simulation.GetPopulationCap(goal.PlayerId))
            {
                BuildingData house = FindOwnedBuilding(goal.PlayerId, BuildingType.House, true);
                if (house != null)
                    return new CommanderPlan(CommanderGoalStatus.WaitingForConstruction,
                        $"Population blocked; House #{house.Id} is under construction.", owned, queued);
                return PlanBuilding(goal, BuildingType.House, currentTick, owned, queued);
            }

            simulation.GetUnitTrainingSpec(goal.PlayerId, goal.RequestedUnitType,
                out _, out int foodCost, out int woodCost, out int goldCost, out _);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            if (resources.Food < foodCost)
                return PlanGather(goal, ResourceType.Food, currentTick, owned, queued,
                    $"Need {foodCost} food for the next Spearman; have {resources.Food}.");
            if (resources.Wood < woodCost)
                return PlanGather(goal, ResourceType.Wood, currentTick, owned, queued,
                    $"Need {woodCost} wood for the next Spearman; have {resources.Wood}.");
            if (resources.Gold < goldCost)
                return PlanGather(goal, ResourceType.Gold, currentTick, owned, queued,
                    $"Need {goldCost} gold for the next Spearman; have {resources.Gold}.");

            if (barracks.TrainingQueue.Count >= goal.MaxQueueDepth)
                return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                    $"Barracks queue is at Commander limit {goal.MaxQueueDepth}.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Queueing Spearman at Barracks #{barracks.Id}.", owned, queued,
                new TrainUnitCommand(goal.PlayerId, barracks.Id, goal.RequestedUnitType));
        }

        private CommanderPlan PlanBuilding(EnsureUnitCountGoal goal, BuildingType type,
            int currentTick, int owned, int queued)
        {
            int woodCost = simulation.GetBuildingWoodCost(type);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(goal.PlayerId);
            if (resources.Wood < woodCost)
                return PlanGather(goal, ResourceType.Wood, currentTick, owned, queued,
                    $"Need {woodCost} wood for {type}; have {resources.Wood}.");

            UnitData builder = SelectBuilder(goal.PlayerId);
            if (builder == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned living villager is available to build {type}.", owned, queued);

            BuildingData primaryBase = FindPrimaryTownCenter(goal.PlayerId);
            if (primaryBase == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No owned Town Center is available as a {type} placement anchor.", owned, queued);

            if (!TryFindBuildableTile(goal.PlayerId, type, primaryBase, builder, out Vector2Int tile))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No visible, reachable buildable location was found for {type}.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Placing {type} at ({tile.x},{tile.y}) with villager #{builder.Id}.", owned, queued,
                new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y, new[] { builder.Id }));
        }

        private CommanderPlan PlanGather(EnsureUnitCountGoal goal, ResourceType resourceType,
            int currentTick, int owned, int queued, string reason)
        {
            if (currentTick - goal.LastEconomyCommandTick < EconomyCommandCooldownTicks)
                return new CommanderPlan(CommanderGoalStatus.WaitingForResources, reason, owned, queued);

            UnitData worker = SelectEconomyWorker(goal.PlayerId, resourceType);
            if (worker == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{reason} No eligible owned villager is available.", owned, queued);

            ResourceNodeData node = FindKnownResourceNode(goal.PlayerId, worker.SimPosition, resourceType);
            if (node == null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"{reason} No explored non-depleted {resourceType} node is known.", owned, queued);

            return new CommanderPlan(CommanderGoalStatus.WaitingForResources,
                $"{reason} Reassigning villager #{worker.Id} to {resourceType} node #{node.Id}.",
                owned, queued, new GatherCommand(goal.PlayerId, new[] { worker.Id }, node.Id));
        }

        private int CountOwnedLivingUnits(int playerId, int unitType)
        {
            int count = 0;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId == playerId && unit.UnitType == unitType
                    && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                    count++;
            }
            return count;
        }

        private int CountQueuedUnits(int playerId, int unitType)
        {
            int count = 0;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed) continue;
                for (int q = 0; q < building.TrainingQueue.Count; q++)
                    if (building.TrainingQueue[q] == unitType) count++;
            }
            return count;
        }

        private int CountAllQueuedUnits(int playerId)
        {
            int count = 0;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].PlayerId == playerId && !buildings[i].IsDestroyed)
                    count += buildings[i].TrainingQueue.Count;
            return count;
        }

        private BuildingData FindProductionBuilding(int playerId, BuildingType effectiveType, bool underConstruction)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed
                    || building.IsUnderConstruction != underConstruction
                    || simulation.GetEffectiveBuildingType(building) != effectiveType)
                    continue;
                if (best == null || building.Id < best.Id) best = building;
            }
            return best;
        }

        private BuildingData FindOwnedBuilding(int playerId, BuildingType type, bool underConstruction)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed || building.Type != type
                    || building.IsUnderConstruction != underConstruction) continue;
                if (best == null || building.Id < best.Id) best = building;
            }
            return best;
        }

        private BuildingData FindPrimaryTownCenter(int playerId)
        {
            BuildingData best = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData building = buildings[i];
                if (building.PlayerId != playerId || building.IsDestroyed || building.IsUnderConstruction
                    || simulation.GetEffectiveBuildingType(building) != BuildingType.TownCenter) continue;
                if (best == null || (building.IsMainTownCenter && !best.IsMainTownCenter)
                    || (building.IsMainTownCenter == best.IsMainTownCenter && building.Id < best.Id))
                    best = building;
            }
            return best;
        }

        private UnitData SelectBuilder(int playerId)
        {
            UnitData best = null;
            int bestPriority = int.MaxValue;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead) continue;
                int priority = unit.State == UnitState.Idle ? 0
                    : IsGatheringState(unit.State) ? 1 : int.MaxValue;
                if (priority < bestPriority || (priority == bestPriority && (best == null || unit.Id < best.Id)))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
            return bestPriority == int.MaxValue ? null : best;
        }

        private UnitData SelectEconomyWorker(int playerId, ResourceType neededType)
        {
            UnitData best = null;
            int bestPriority = int.MaxValue;
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead) continue;
                if (IsGatheringResource(unit, neededType)) continue;
                int priority = unit.State == UnitState.Idle ? 0
                    : IsGatheringState(unit.State) ? 1 : int.MaxValue;
                if (priority < bestPriority || (priority == bestPriority && (best == null || unit.Id < best.Id)))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
            return bestPriority == int.MaxValue ? null : best;
        }

        private bool IsGatheringResource(UnitData unit, ResourceType type)
        {
            if (!IsGatheringState(unit.State)) return false;
            ResourceNodeData node = simulation.MapData.GetResourceNode(unit.TargetResourceNodeId);
            return node != null && node.Type == type;
        }

        private static bool IsGatheringState(UnitState state)
        {
            return state == UnitState.Gathering || state == UnitState.MovingToGather
                || state == UnitState.MovingToDropoff || state == UnitState.DroppingOff;
        }

        private ResourceNodeData FindKnownResourceNode(int playerId, FixedVector3 position, ResourceType type)
        {
            ResourceNodeData best = null;
            int originX = position.x.Raw >> Fixed32.FractionalBits;
            int originZ = position.z.Raw >> Fixed32.FractionalBits;
            int bestDistance = int.MaxValue;
            IReadOnlyList<ResourceNodeData> nodes = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNodeData node = nodes[i];
                if (node.Type != type || node.IsDepleted) continue;
                if (simulation.FogOfWar.GetVisibility(playerId, node.TileX, node.TileZ) == TileVisibility.Unexplored)
                    continue;
                int dx = node.TileX - originX;
                int dz = node.TileZ - originZ;
                int distance = dx * dx + dz * dz;
                if (distance < bestDistance || (distance == bestDistance && (best == null || node.Id < best.Id)))
                {
                    best = node;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool TryFindBuildableTile(int playerId, BuildingType type, BuildingData anchor,
            UnitData builder, out Vector2Int result)
        {
            GetFootprint(type, out int width, out int height);
            int centerX = anchor.OriginTileX + anchor.TileFootprintWidth / 2;
            int centerZ = anchor.OriginTileZ + anchor.TileFootprintHeight / 2;
            int border = type == BuildingType.Farm ? 0 : 1;
            Vector2Int start = simulation.MapData.WorldToTile(builder.SimPosition);

            for (int radius = 4; radius <= 20; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                        int tileX = centerX + dx;
                        int tileZ = centerZ + dz;
                        if (!IsVisibleBuildableArea(playerId, tileX, tileZ, width, height, border, type)) continue;
                        if (!HasReachableAdjacentTile(start, playerId, tileX, tileZ, width, height)) continue;
                        result = new Vector2Int(tileX, tileZ);
                        return true;
                    }
                }
            }
            result = new Vector2Int(-1, -1);
            return false;
        }

        private bool IsVisibleBuildableArea(int playerId, int tileX, int tileZ, int width,
            int height, int border, BuildingType type)
        {
            bool farm = type == BuildingType.Farm;
            for (int x = tileX - border; x < tileX + width + border; x++)
            {
                for (int z = tileZ - border; z < tileZ + height + border; z++)
                {
                    if (simulation.FogOfWar.GetVisibility(playerId, x, z) == TileVisibility.Unexplored)
                        return false;
                    if (farm ? !simulation.MapData.IsBuildableForFarm(x, z) : !simulation.MapData.IsBuildable(x, z))
                        return false;
                }
            }
            return true;
        }

        private bool HasReachableAdjacentTile(Vector2Int start, int playerId, int tileX, int tileZ,
            int width, int height)
        {
            for (int x = tileX - 1; x <= tileX + width; x++)
            {
                for (int z = tileZ - 1; z <= tileZ + height; z++)
                {
                    if (x >= tileX && x < tileX + width && z >= tileZ && z < tileZ + height) continue;
                    if (!simulation.MapData.IsWalkable(x, z, playerId, simulation.BuildingRegistry)) continue;
                    if (start.x == x && start.y == z) return true;
                    if (GridPathfinder.FindPath(simulation.MapData, start, new Vector2Int(x, z),
                        playerId, simulation.BuildingRegistry).Count > 0) return true;
                }
            }
            return false;
        }

        private void GetFootprint(BuildingType type, out int width, out int height)
        {
            SimulationConfig config = simulation.Config;
            switch (type)
            {
                case BuildingType.Barracks:
                    width = config.BarracksFootprintWidth; height = config.BarracksFootprintHeight; break;
                case BuildingType.House:
                    width = config.HouseFootprintWidth; height = config.HouseFootprintHeight; break;
                default:
                    width = 2; height = 2; break;
            }
        }
    }
}
