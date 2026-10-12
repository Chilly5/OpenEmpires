using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private const int MaximumDynamicSiteSearchNodes = 8192;

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
            IReadOnlyList<Vector2Int> planned = null;
            bool exhausted = false;
            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null
                && !TrySelectFullDynamicSiteSet(goal, type, currentTick, candidates,
                    width, height, border, out planned, out exhausted))
            {
                goal.PlacementBlocker = CommanderPlacementBlocker.NoLegalCandidate;
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    (exhausted ? "The bounded compatible-site search budget was exhausted; "
                        : $"Only a partial set of distinct legal {type} sites is available near the exact anchor; ")
                    + $"none of the requested {goal.Count} placements was started.", owned, queued);
            }
            if (goal.Count > 1 && goal.DynamicPlannedSiteSet == null)
                goal.DynamicPlannedSiteSet = planned;
            IReadOnlyList<Vector2Int> placementCandidates = goal.DynamicPlannedSiteSet != null
                ? new[] { goal.DynamicPlannedSiteSet[goal.AttributedBuildingIds.Count] } : candidates;
            for (int i = 0; i < placementCandidates.Count; i++)
            {
                Vector2Int tile = placementCandidates[i];
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
                goal.DynamicPlannedSiteSet != null
                    ? "A preflight-selected exact site became unavailable; no alternate placement was started."
                    : $"No visible, buildable, reachable location satisfies the exact {type} "
                        + "anchor and bounded relation.", owned, queued);
        }

        private bool TrySelectFullDynamicSiteSet(BuildStructureGoal goal, BuildingType type,
            int currentTick, IReadOnlyList<Vector2Int> candidates,
            int width, int height, int border, out IReadOnlyList<Vector2Int> planned,
            out bool exhausted)
        {
            planned = null;
            exhausted = false;
            bool budgetExceeded = false;
            var legal = new List<Vector2Int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int tile = candidates[i];
                if (!IsVisibleBuildableArea(goal.PlayerId, tile.x, tile.y,
                    width, height, border, type)) continue;
                if (FindReachableSemanticBuilder(goal, tile, width, height, currentTick) != null)
                    legal.Add(tile);
            }
            var selected = new List<Vector2Int>(goal.Count);
            int checkedNodes = 0;
            bool Search(int start)
            {
                if (selected.Count == goal.Count) return true;
                if (legal.Count - start < goal.Count - selected.Count) return false;
                for (int i = start; i < legal.Count; i++)
                {
                    if (++checkedNodes > MaximumDynamicSiteSearchNodes)
                    { budgetExceeded = true; return false; }
                    Vector2Int tile = legal[i];
                    bool conflicts = false;
                    for (int j = 0; j < selected.Count; j++)
                    {
                        Vector2Int prior = selected[j];
                        if (tile.x - border < prior.x + width + border
                            && tile.x + width + border > prior.x - border
                            && tile.y - border < prior.y + height + border
                            && tile.y + height + border > prior.y - border)
                        { conflicts = true; break; }
                    }
                    if (conflicts) continue;
                    selected.Add(tile);
                    if (Search(i + 1)) return true;
                    selected.RemoveAt(selected.Count - 1);
                    if (budgetExceeded) return false;
                }
                return false;
            }
            if (!Search(0))
            { exhausted = budgetExceeded; return false; }
            planned = selected.ToArray();
            return true;
        }
    }
}
