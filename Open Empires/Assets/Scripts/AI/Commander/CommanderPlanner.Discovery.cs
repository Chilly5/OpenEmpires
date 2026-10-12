using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private const int MaximumDiscoveryMoves = 8;
        private const int MaximumFrontierCandidatesPerMove = 64;
        private const int MaximumFrontierCellsPerObservation = 4096;
        private const int MaximumDiscoveryCandidates = 256;
        private const int MaximumDiscoveryRadius = 64;

        private CommanderPlan PlanDiscovery(CommanderCapabilityGoal goal, int currentTick)
        {
            if (goal.RuntimeOwner == null || goal.RuntimeOwner.IsDisposed
                || !ReferenceEquals(goal.RuntimeOwner.Simulation, simulation))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The discovery goal is detached from its original Commander runtime.", 0, 0);
            if (goal.ResultHumanOverride || goal.RequiresResultBinding || goal.ResultSourceGoal != null)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "Result-bound discovery is unsupported; no unrelated Scout may substitute.", 0, 0);
            if (goal.DiscoveryScoutId < 0)
            {
                UnitData selected = simulation.UnitRegistry.GetAllUnits()
                    .Where(unit => unit != null && unit.PlayerId == goal.PlayerId && unit.UnitType == 4
                        && unit.CurrentHealth > 0 && unit.State != UnitState.Dead
                        && workerAuthority.CanUseUnit(unit.Id, goal.GoalId, currentTick))
                    .OrderBy(unit => unit.Id).FirstOrDefault();
                if (selected == null)
                    return new CommanderPlan(CommanderGoalStatus.Failed,
                        "No eligible owned Scout is available for this finite discovery request.", 0, 0);
                goal.DiscoveryScoutId = selected.Id;
            }
            UnitData scout = simulation.UnitRegistry.GetUnit(goal.DiscoveryScoutId);
            if (scout == null)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    simulation.UnitRegistry.GetGarrisonedUnit(goal.DiscoveryScoutId) != null
                        ? "The exact discovery Scout is garrisoned; no substitute is authorized."
                        : "The exact discovery Scout is missing; no substitute is authorized.", 0, 0);
            if (scout.PlayerId != goal.PlayerId)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery Scout changed ownership; no substitute is authorized.", 0, 0);
            if (scout.CurrentHealth <= 0 || scout.State == UnitState.Dead)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery Scout died; no substitute is authorized.", 0, 0);
            if (scout.UnitType != 4)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The exact discovery actor is no longer a Scout; no substitute is authorized.", 0, 0);
            if (workerAuthority.IsHumanProtected(scout.Id, currentTick))
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The player took control of the exact discovery Scout; Commander will not reclaim it.", 0, 0);
            if (!workerAuthority.CanUseUnit(scout.Id, goal.GoalId, currentTick))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The exact discovery Scout is temporarily reserved; no substitute is authorized.", 0, 0);

            if (DiscoveryTargetVisible(goal))
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    "The requested enemy target is now visible to the player.", 1, 0);

            if (goal.CommandIssued)
            {
                string reason = "Waiting for the exact Scout's native move to reach its known frontier waypoint.";
                if (goal.IssuedCommand is MoveCommand move
                    && ObserveNativeMovement(goal, move, new[] { scout.Id }, currentTick, out reason))
                {
                    goal.CommandIssued = false;
                    goal.IssuedCommand = null;
                    goal.DiscoveryScanCenterX = -1;
                    goal.DiscoveryScanCenterZ = -1;
                    goal.DiscoveryScanRadius = 0;
                    goal.DiscoveryScanOffset = 0;
                    return new CommanderPlan(CommanderGoalStatus.Executing,
                        "The Scout reached a known frontier waypoint; the requested enemy is not yet visible.", 1, 0);
                }
                if (reason.StartsWith("The exact native formation made no progress", StringComparison.Ordinal)
                    || reason.StartsWith("A native route stopped short", StringComparison.Ordinal)
                    || reason.StartsWith("An exact subject has a replacement", StringComparison.Ordinal)
                    || reason.StartsWith("An exact movement subject is temporarily unavailable", StringComparison.Ordinal))
                    return new CommanderPlan(CommanderGoalStatus.Failed, reason, 1, 0);
                return new CommanderPlan(CommanderGoalStatus.Executing, reason, 1, 0);
            }
            if (goal.DiscoveryMovesIssued >= MaximumDiscoveryMoves)
                return new CommanderPlan(CommanderGoalStatus.Failed,
                    "The finite discovery route was exhausted without a visible requested enemy target.", 1, 0);

            Vector2Int current = simulation.MapData.WorldToTile(scout.SimPosition);
            if (goal.DiscoveryScanCenterX < 0)
            {
                goal.DiscoveryScanCenterX = current.x;
                goal.DiscoveryScanCenterZ = current.y;
            }
            int width = simulation.MapData.Width, height = simulation.MapData.Height;
            int checkedCells = 0, checkedCandidates = 0;
            while (goal.DiscoveryScanRadius <= MaximumDiscoveryRadius
                && checkedCells < MaximumFrontierCellsPerObservation
                && checkedCandidates < MaximumFrontierCandidatesPerMove
                && goal.DiscoveryCandidatesChecked < MaximumDiscoveryCandidates)
            {
                int radius = goal.DiscoveryScanRadius;
                int ringLength = radius == 0 ? 1 : 8 * radius;
                if (goal.DiscoveryScanOffset >= ringLength)
                { goal.DiscoveryScanRadius++; goal.DiscoveryScanOffset = 0; continue; }
                Vector2Int tile = DiscoveryRingTile(goal.DiscoveryScanCenterX,
                    goal.DiscoveryScanCenterZ, radius, goal.DiscoveryScanOffset++);
                checkedCells++;
                if (!simulation.MapData.IsInBounds(tile.x, tile.y)
                    || !IsKnownTile(goal.PlayerId, tile.x, tile.y)
                    || !simulation.MapData.IsWalkable(tile.x, tile.y, goal.PlayerId, simulation.BuildingRegistry)
                    || goal.DiscoveryVisitedWaypoints.Contains(tile.x * height + tile.y)
                    || !BordersUnknownPlayableTile(goal.PlayerId, tile.x, tile.y)) continue;
                goal.DiscoveryVisitedWaypoints.Add(tile.x * height + tile.y);
                checkedCandidates++;
                goal.DiscoveryCandidatesChecked++;
                var requested = new MoveCommand(goal.PlayerId, new[] { scout.Id },
                    simulation.MapData.TileToWorldFixed(tile.x, tile.y));
                if (!TryResolveNativeMovementSlots(goal, requested, currentTick, out MoveCommand resolved, out _))
                    continue;
                goal.DiscoveryMovesIssued++;
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Moving the exact Scout to known frontier waypoint {goal.DiscoveryMovesIssued}/{MaximumDiscoveryMoves}.",
                    1, 0, resolved);
            }
            if (goal.DiscoveryScanRadius <= MaximumDiscoveryRadius
                && goal.DiscoveryCandidatesChecked < MaximumDiscoveryCandidates)
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Scanning only known frontier tiles within the finite per-observation budget.", 1, 0);
            return new CommanderPlan(CommanderGoalStatus.Failed,
                "No reachable known frontier waypoint remains; the requested enemy was not visibly discovered.", 1, 0);
        }

        private static Vector2Int DiscoveryRingTile(int centerX, int centerZ, int radius, int offset)
        {
            if (radius == 0) return new Vector2Int(centerX, centerZ);
            int side = 2 * radius;
            if (offset < side) return new Vector2Int(centerX - radius + offset, centerZ - radius);
            offset -= side;
            if (offset < side) return new Vector2Int(centerX + radius, centerZ - radius + offset);
            offset -= side;
            if (offset < side) return new Vector2Int(centerX + radius - offset, centerZ + radius);
            offset -= side;
            return new Vector2Int(centerX - radius, centerZ + radius - offset);
        }

        private bool BordersUnknownPlayableTile(int playerId, int x, int z)
        {
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dz) > 3) continue;
                    int neighborX = x + dx, neighborZ = z + dz;
                    if (simulation.MapData.IsInBounds(neighborX, neighborZ)
                        && !simulation.MapData.IsOutsideCircle(neighborX, neighborZ)
                        && simulation.FogOfWar.GetVisibility(playerId, neighborX, neighborZ)
                            == TileVisibility.Unexplored) return true;
                }
            return false;
        }

        private bool DiscoveryTargetVisible(CommanderCapabilityGoal goal)
        {
            if (!goal.Action.TargetSelector.HasValue) return false;
            CommanderTargetSelector target = goal.Action.TargetSelector.Value;
            int player = goal.PlayerId;
            if (target.Kind == CommanderTargetSelectorKind.UnitType)
                return simulation.UnitRegistry.GetAllUnits().Any(unit =>
                {
                    if (unit == null || unit.PlayerId < 0 || unit.PlayerId == player
                        || simulation.AreAllies(player, unit.PlayerId) || unit.CurrentHealth <= 0
                        || unit.State == UnitState.Dead
                        || unit.UnitType != simulation.ResolveCivUnitType(unit.PlayerId, target.UnitType)) return false;
                    Vector2Int tile = simulation.MapData.WorldToTile(unit.SimPosition);
                    return simulation.FogOfWar.GetVisibility(player, tile.x, tile.y) == TileVisibility.Visible;
                });
            return simulation.BuildingRegistry.GetAllBuildings().Any(building =>
                building != null && building.PlayerId >= 0 && building.PlayerId != player
                && !simulation.AreAllies(player, building.PlayerId) && !building.IsDestroyed
                && simulation.GetEffectiveBuildingType(building) == target.StructureType
                && simulation.FogOfWar.GetVisibility(player, building.OriginTileX, building.OriginTileZ)
                    == TileVisibility.Visible);
        }
    }
}
