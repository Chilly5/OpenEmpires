using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private const int MaxGarrisonAllMatching = 200;
        private const int GarrisonNoProgressTicks = 150;

        private CommanderPlan PlanShelterCapability(CommanderCapabilityGoal goal, int currentTick)
        {
            bool enter = goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits;
            BuildingData shelter = goal.GarrisonShelter;
            if (shelter == null)
            {
                shelter = simulation.BuildingRegistry.GetAllBuildings()
                    .Where(b => b.PlayerId == goal.PlayerId && !b.IsDestroyed
                        && !b.IsUnderConstruction && b.Type == goal.Action.StructureType
                        && b.GarrisonCapacity > 0)
                    .OrderByDescending(b => b.IsMainTownCenter).ThenBy(b => b.Id).FirstOrDefault();
                if (shelter == null)
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        "No completed owned shelter of the requested type is available.", 0, 0);
                goal.GarrisonShelter = shelter;
            }
            if (!ReferenceEquals(simulation.BuildingRegistry.GetBuilding(shelter.Id), shelter)
                || shelter.IsDestroyed || shelter.IsUnderConstruction || shelter.PlayerId != goal.PlayerId)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The exact selected shelter was lost or changed owner; no substitute is authorized.", 0, 0);

            if (!enter) return PlanBuildingWideUngarrison(goal, shelter);
            if (goal.CommandIssued) return ObserveExactGarrison(goal, shelter, currentTick);

            if (goal.GarrisonCandidateIds == null)
            {
                var executor = new CommanderCapabilityExecutor(simulation);
                var eligible = simulation.UnitRegistry.GetAllUnits()
                    .Where(unit => unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                        && unit.State != UnitState.Dead && !unit.IsSheep
                    && executor.MatchesSelector(unit, goal.Action.UnitSelector))
                    .OrderBy(unit => unit.Id);
                goal.GarrisonCandidateIds = eligible
                    .Take(goal.Action.UnitSelector.AllMatching ? MaxGarrisonAllMatching + 1
                        : goal.Action.UnitSelector.Count).Select(unit => unit.Id).ToArray();
            }
            if (goal.Action.UnitSelector.AllMatching
                && goal.GarrisonCandidateIds.Length > MaxGarrisonAllMatching)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "AllMatching villager snapshot exceeds the 200-subject safety bound; no prefix was ordered.", 0, 0);
            int requested = goal.Action.UnitSelector.AllMatching
                ? goal.GarrisonCandidateIds.Length : goal.Action.UnitSelector.Count;
            if (requested == 0)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No owned eligible villagers matched the AllMatching snapshot.", 0, 0);

            int protectedCount = 0;
            int unaccommodated = requested - goal.GarrisonCandidateIds.Length;
            int free = Math.Max(0, shelter.GarrisonCapacity - shelter.GarrisonCount);
            var selected = new List<int>();
            foreach (int id in goal.GarrisonCandidateIds)
            {
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                if (unit == null || unit.PlayerId != goal.PlayerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead)
                { unaccommodated++; continue; }
                if (!workerAuthority.CanUseUnit(id, goal.GoalId, currentTick)
                    || unit.CommandQueue.Count > 0)
                { protectedCount++; continue; }
                if (selected.Count >= free || !CanReachBuilding(unit, shelter))
                { unaccommodated++; continue; }
                selected.Add(id);
            }
            goal.GarrisonProtectedCount = protectedCount;
            goal.GarrisonUnaccommodatedCount = unaccommodated;
            if (selected.Count == 0)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    $"Garrisoned 0/{requested}; protected {protectedCount}; unaccommodated {unaccommodated}. No exact owned, reachable capacity can be used.",
                    0, 0);
            goal.GarrisonIssuedIds = selected.ToArray();
            return new CommanderPlan(CommanderGoalStatus.Executing,
                $"Sending {selected.Count}/{requested} owned eligible unit(s) to exact shelter; protected {protectedCount}; unaccommodated {unaccommodated}.",
                0, 0, new GarrisonCommand(goal.PlayerId, goal.GarrisonIssuedIds, shelter.Id));
        }

        private CommanderPlan ObserveExactGarrison(CommanderCapabilityGoal goal, BuildingData shelter,
            int currentTick)
        {
            int requested = goal.Action.UnitSelector.AllMatching
                ? goal.GarrisonCandidateIds.Length : goal.Action.UnitSelector.Count;
            int housed = 0;
            bool moving = false;
            long remainingDistance = 0;
            foreach (int id in goal.GarrisonIssuedIds ?? Array.Empty<int>())
            {
                UnitData occupant = simulation.UnitRegistry.GetGarrisonedUnit(id);
                if (occupant != null && occupant.PlayerId == goal.PlayerId && occupant.CurrentHealth > 0
                    && shelter.GarrisonedUnitIds.Contains(id))
                { housed++; continue; }
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                    && unit.State != UnitState.Dead
                    && workerAuthority.IsHumanProtected(id, currentTick))
                    return new CommanderPlan(CommanderGoalStatus.Blocked,
                        $"Garrisoned {housed}/{requested}; the player replaced an exact subject's order; no reclaim or shelter fallback is authorized.",
                        housed, 0);
                if (unit != null && unit.PlayerId == goal.PlayerId && unit.CurrentHealth > 0
                    && unit.State != UnitState.Dead && unit.TargetGarrisonBuildingId == shelter.Id)
                {
                    moving = true;
                    remainingDistance += Math.Abs((long)unit.SimPosition.x.Raw - shelter.SimPosition.x.Raw)
                        + Math.Abs((long)unit.SimPosition.z.Raw - shelter.SimPosition.z.Raw);
                }
            }
            int remainder = requested - housed - goal.GarrisonProtectedCount;
            string result = $"Garrisoned {housed}/{requested} in the exact owned {shelter.Type}; protected {goal.GarrisonProtectedCount}; unaccommodated {Math.Max(0, remainder)}.";
            if (housed == (goal.GarrisonIssuedIds?.Length ?? 0)
                && simulation.CurrentTick > goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Completed, result, housed, 0);
            if (moving && remainingDistance < goal.GarrisonRemainingDistance)
            {
                goal.GarrisonRemainingDistance = remainingDistance;
                goal.GarrisonLastProgressSimulationTick = simulation.CurrentTick;
            }
            if (moving && goal.GarrisonLastProgressSimulationTick >= 0
                && simulation.CurrentTick - goal.GarrisonLastProgressSimulationTick >= GarrisonNoProgressTicks)
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    result + " Exact native entry made no progress for 150 simulation ticks; no alternate shelter was authorized.",
                    housed, 0);
            if (moving || simulation.CurrentTick <= goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Executing, result + " Native entry is pending.", housed, 0);
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                result + " An exact subject did not enter; no alternate shelter was authorized.", housed, 0);
        }

        private CommanderPlan PlanBuildingWideUngarrison(CommanderCapabilityGoal goal, BuildingData shelter)
        {
            if (!goal.CommandIssued)
            {
                if (shelter.GarrisonCount == 0)
                    return new CommanderPlan(CommanderGoalStatus.Completed,
                        "The exact owned shelter is already empty; building-wide ungarrison released 0 units.", 0, 0);
                goal.UngarrisonInitialIds = shelter.GarrisonedUnitIds.ToArray();
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    $"Building-wide ungarrison will release all {goal.UngarrisonInitialIds.Length} housed unit(s).",
                    0, 0, new UngarrisonCommand(goal.PlayerId, shelter.Id));
            }
            if (simulation.CurrentTick <= goal.CommandIssuedSimulationTick)
                return new CommanderPlan(CommanderGoalStatus.Executing,
                    "Building-wide native ungarrison is pending.", 0, 0);
            int released = 0;
            foreach (int id in goal.UngarrisonInitialIds ?? Array.Empty<int>())
                if (simulation.UnitRegistry.GetUnit(id) != null
                    && !shelter.GarrisonedUnitIds.Contains(id)) released++;
            if (released == (goal.UngarrisonInitialIds?.Length ?? 0))
                return new CommanderPlan(CommanderGoalStatus.Completed,
                    $"Building-wide ungarrison released {released}/{goal.UngarrisonInitialIds.Length} from the exact owned shelter.",
                    released, 0);
            return new CommanderPlan(CommanderGoalStatus.Blocked,
                $"Building-wide ungarrison released {released}/{goal.UngarrisonInitialIds?.Length ?? 0}; native egress is incomplete.",
                released, 0);
        }
    }
}
