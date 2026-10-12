using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        // Called only after ordinary age/cost/prerequisite checks in PlanBuilding.
        // The anchor is a frozen game-side identity, never a provider tile or runtime ID.
        private CommanderPlan PlanDynamicSemanticBuilding(BuildStructureGoal goal,
            BuildingType type, int currentTick, int owned, int queued)
        {
            if (goal.ConstructionForbidden)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "This request forbids construction.", owned, queued);
            CommanderBoundLocation location = goal.DynamicLocation;
            if (location == null || !location.TryResolve(simulation, goal.PlayerId,
                out int anchorX, out int anchorZ, out int anchorWidth, out int anchorHeight))
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.AnchorUnavailable;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The exact owned visible placement anchor is unavailable; no substitute was selected.",
                    owned, queued);
            }
            if (SelectBuilder(goal, currentTick) == null)
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.NoEligibleBuilder;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"No eligible owned villager is available to build {type}.", owned, queued);
            }

            GetFootprint(type, out int width, out int height);
            IReadOnlyList<Vector2Int> candidates = CommanderSemanticPlacementCandidates.Generate(
                anchorX, anchorZ, anchorWidth, anchorHeight, width, height,
                simulation.MapData.Width, simulation.MapData.Height,
                location.Relation, location.ClearGapTiles);
            int border = type == BuildingType.Farm ? 0 : 1;
            // Before placing the first building, require enough mutually legal sites
            // for the entire repeated request. Later external occupancy/authority
            // changes may still block honestly; a known impossible prefix is never issued.
            if (goal.Count > 1 && goal.AttributedBuildingIds.Count == 0
                && !HasFullDynamicSiteSet(goal, type, currentTick, candidates,
                    width, height, border))
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"Only a partial set of distinct legal {type} sites is available near the exact anchor; "
                    + $"none of the requested {goal.Count} placements was started.", owned, queued);
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y, width, height,
                    border, type)) continue;
                UnitData builder = FindReachableSemanticBuilder(goal, tile, width,
                    height, currentTick);
                if (builder == null) continue;
                goal.PlacementBlocker = CommanderPlacementBlocker.None;
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Placing {type} at ({tile.x},{tile.y}) from its exact semantic anchor "
                    + $"with villager #{builder.Id}.", owned, queued,
                    new PlaceBuildingCommand(goal.PlayerId, type, tile.x, tile.y,
                        new[] { builder.Id }));
            }
            goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                $"No visible, buildable, reachable location satisfies the exact {type} "
                + "anchor and bounded relation.", owned, queued);
        }

        private bool HasFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
            int currentTick, IReadOnlyList<Vector2Int> candidates,
            int width, int height, int border)
        {
            var selected = new List<Vector2Int>(goal.Count);
            for (int i = 0; i < candidates.Count && selected.Count < goal.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y,
                    width, height, border, type)) continue;
                bool conflicts = false;
                for (int j = 0; j < selected.Count; j++)
                {
                    Vector2Int prior = selected[j];
                    if (tile.x - border < prior.x + width && tile.x + width + border > prior.x
                        && tile.y - border < prior.y + height && tile.y + height + border > prior.y)
                    { conflicts = true; break; }
                }
                if (conflicts || FindReachableSemanticBuilder(goal, tile,
                    width, height, currentTick) == null) continue;
                selected.Add(tile);
            }
            return selected.Count == goal.Count;
        }
    }
}
