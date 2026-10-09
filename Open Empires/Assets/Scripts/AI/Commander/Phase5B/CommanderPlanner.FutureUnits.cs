using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private CommanderPlan PlanFutureUnits(WatchFutureUnitsGoal goal, int currentTick)
        {
            BuildingData producer = goal.BoundProducer;
            if (producer == null || producer.IsDestroyed || producer.PlayerId != goal.PlayerId
                || !ReferenceEquals(simulation.BuildingRegistry.GetBuilding(goal.ProducerId), producer)
                || simulation.GetEffectiveBuildingType(producer) != goal.Order.ProducerType)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The bound producer was lost; no other producer or replacement birth will substitute.",
                    goal.ObservedCount, 0);
            foreach (int id in goal.observedUnitIds)
            {
                if (goal.issuedUnitIds.Contains(id) || goal.interruptedUnitIds.Contains(id)) continue;
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead)
                    return FutureBlocked(goal, "An observed unit was lost; a later birth will not replace it.");
                if (workerAuthority.IsHumanProtected(id, currentTick) || unit.CommandQueue.Count > 0)
                {
                    goal.interruptedUnitIds.Add(id);
                    continue;
                }
                if (!TryCreateFutureCommand(goal, unit, out ICommand command))
                    return FutureBlocked(goal, "No currently visible legal destination is available for the bound unit.");
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Assigning an actual birth from the bound producer.", goal.ObservedCount, 0, command);
            }
            if (goal.interruptedUnitIds.Count > 0)
                return FutureBlocked(goal, "The player took control of an observed unit; it will not be reclaimed or replaced.");
            if (goal.rejectedUnitIds.Count > 0)
                return FutureBlocked(goal, "A native command did not assign an observed unit; that birth will not be replaced.");
            if (goal.pendingCommands.Count > 0)
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Waiting for the exact queued action to be processed by simulation.", goal.ObservedCount, 0);
            if (goal.ObservedCount == goal.Order.Count && goal.AssignedCount == goal.Order.Count)
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    "All requested next births received the authorized order.", goal.ObservedCount, 0);
            return new CommanderPlan(CommanderGoalStatus.WaitingForProduction,
                "Watching actual births from the bound producer; no training was requested.",
                goal.ObservedCount, 0);
        }

        private static CommanderPlan FutureBlocked(WatchFutureUnitsGoal goal, string reason)
            => new CommanderPlan(CommanderGoalStatus.Blocked, reason, goal.ObservedCount, 0);

        private bool TryCreateFutureCommand(WatchFutureUnitsGoal goal, UnitData unit,
            out ICommand command)
        {
            command = null;
            var position = simulation.MapData.WorldToTile(unit.SimPosition);
            if (goal.Order.Action == CommanderFutureUnitAction.Gather
                && goal.Order.Resource == ResourceType.Food
                && (goal.Order.SourceKind == ResourceSourceKind.Any
                    || goal.Order.SourceKind == ResourceSourceKind.Sheep))
            {
                foreach (UnitData sheep in simulation.UnitRegistry.GetAllUnits()
                    .Where(candidate => candidate != null && candidate.IsSheep
                        && candidate.PlayerId == goal.PlayerId && candidate.CurrentHealth > 0
                        && candidate.State != UnitState.Dead)
                    .OrderBy(candidate =>
                    {
                        Vector2Int target = simulation.MapData.WorldToTile(candidate.SimPosition);
                        return (long)(target.x - position.x) * (target.x - position.x)
                            + (long)(target.y - position.y) * (target.y - position.y);
                    }).ThenBy(candidate => candidate.Id).Take(pathValidationCandidates))
                {
                    Vector2Int target = simulation.MapData.WorldToTile(sheep.SimPosition);
                    if (simulation.FogOfWar.GetVisibility(goal.PlayerId, target.x, target.y)
                            != TileVisibility.Visible
                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, position, target,
                            out var path, goal.PlayerId, simulation.BuildingRegistry)
                        || !IsKnownPath(goal.PlayerId, position, path)) continue;
                    command = new SlaughterSheepCommand { PlayerId = goal.PlayerId,
                        VillagerIds = new[] { unit.Id }, SheepUnitId = sheep.Id,
                        SourceKind = goal.Order.SourceKind };
                    return true;
                }
                if (goal.Order.SourceKind == ResourceSourceKind.Sheep) return false;
            }
            IEnumerable<ResourceNodeData> nodes = simulation.MapData.GetAllResourceNodes()
                .Where(node => node != null && !node.IsDepleted && node.Type == goal.Order.Resource
                    && simulation.FogOfWar.GetVisibility(goal.PlayerId, node.TileX, node.TileZ)
                        == TileVisibility.Visible);
            if (goal.Order.Action == CommanderFutureUnitAction.Gather)
                nodes = nodes.Where(node => ResourceSourceRules.Matches(node, goal.Order.SourceKind)
                    && LegalFarm(node, goal.PlayerId));
            else
            {
                var worked = new HashSet<int>(simulation.UnitRegistry.GetAllUnits()
                    .Where(worker => worker != null && worker.PlayerId == goal.PlayerId && worker.IsVillager
                        && worker.CurrentHealth > 0 && worker.State != UnitState.Dead
                        && worker.TargetResourceNodeId >= 0)
                    .Select(worker => worker.TargetResourceNodeId));
                nodes = nodes.Where(node => worked.Contains(node.Id));
            }
            foreach (ResourceNodeData node in nodes.OrderBy(node =>
                (long)(node.TileX - position.x) * (node.TileX - position.x)
                + (long)(node.TileZ - position.y) * (node.TileZ - position.y)).ThenBy(node => node.Id)
                .Take(pathValidationCandidates))
            {
                if (!HasReachableAdjacentTile(position, goal.PlayerId, node.TileX, node.TileZ,
                    node.FootprintWidth, node.FootprintHeight)) continue;
                command = goal.Order.Action == CommanderFutureUnitAction.Gather
                    ? (ICommand)new GatherCommand(goal.PlayerId, new[] { unit.Id }, node.Id,
                        goal.Order.SourceKind)
                    : new PatrolCommand(goal.PlayerId, new[] { unit.Id }, node.Position);
                return true;
            }
            return false;
        }
    }
}
