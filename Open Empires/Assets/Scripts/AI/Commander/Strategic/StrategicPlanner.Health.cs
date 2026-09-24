using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed partial class StrategicPlanner
    {
        public StrategicPlanHealthSnapshot CapturePlanHealth(int trustedPlayerId, int planId)
        {
            if (disposed || trustedPlayerId != PlayerId || planId < 1) return null;
            StrategicPlan plan = GetPlan(planId);
            if (plan == null || plan.OwnerPlayerId != trustedPlayerId
                || !Enum.IsDefined(typeof(StrategicPlanType), plan.PlanType)
                || !Enum.IsDefined(typeof(StrategicPlanStatus), plan.Status)) return null;
            StrategicMilestone milestone = plan.CurrentMilestone;
            if (milestone != null && !Enum.IsDefined(typeof(StrategicMilestoneStatus), milestone.Status))
                return null;

            // Build a fresh owner-scoped projection only after identity and exact ID validation.
            GameSimulation simulation = goalManager.Simulation;
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goalManager);
            int tick = simulation.CurrentTick;
            int completedMilestones = 0;
            foreach (StrategicMilestone item in plan.Milestones)
                if (item.Status == StrategicMilestoneStatus.Completed) completedMilestones++;

            var children = new List<StrategicPlanHealthChild>();
            var facts = new List<(StrategicPlanHealthCategory Category, int Id)>();
            var statusCounts = new SortedDictionary<CommanderGoalStatus, int>();
            foreach (CommanderGoalStatus value in Enum.GetValues(typeof(CommanderGoalStatus)))
                statusCounts.Add(value, 0);
            int unknownStatusCount = 0;
            int retained = 0, missing = 0, allQueued = 0;
            bool hasProgressEvidence = completedMilestones > 0;
            foreach (BuildingData building in simulation.BuildingRegistry.GetAllBuildings())
                if (building.PlayerId == trustedPlayerId && !building.IsDestroyed)
                    allQueued = checked(allQueued + building.TrainingQueue.Count);

            if (milestone != null)
            {
                var ids = new List<int>(milestone.RequiredChildGoals);
                ids.Sort();
                foreach (int id in ids)
                {
                    bool completed = ContainsId(milestone.CompletedChildGoals, id);
                    CommanderGoal goal = goalManager.GetGoal(id);
                    if (goal == null) missing++; else retained++;
                    CommanderGoalStatus? status = goal != null
                        && Enum.IsDefined(typeof(CommanderGoalStatus), goal.Status)
                        ? goal.Status : (CommanderGoalStatus?)null;
                    if (goal != null)
                    {
                        if (status.HasValue) statusCounts[status.Value]++;
                        else unknownStatusCount++;
                    }
                    if (completed || status == CommanderGoalStatus.Executing
                        || status == CommanderGoalStatus.Planning)
                        hasProgressEvidence = true;
                    StrategicPlanHealthCategory category = CategoryForStatus(status);
                    int? blockedTicks = goal != null && status == CommanderGoalStatus.Blocked
                        ? BlockedDuration(goal.BlockedSinceTick, tick, plan.PausedAtTick,
                            plan.Status == StrategicPlanStatus.Paused) : null;
                    int? requested = null, resolved = null, target = null, owned = null,
                        queued = null, remaining = null;
                    if (goal is EnsureUnitCountGoal units && status.HasValue
                        && !completed && !goal.IsTerminal
                        && CommanderIntentCatalog.IsSupportedUnit(units.RequestedUnitType))
                    {
                        requested = units.RequestedUnitType;
                        resolved = simulation.ResolveCivUnitType(trustedPlayerId, units.RequestedUnitType);
                        target = units.TargetTotal;
                        int ownedCount = 0, matchingQueued = 0;
                        foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
                            if (unit.PlayerId == trustedPlayerId && unit.UnitType == resolved
                                && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                                ownedCount = checked(ownedCount + 1);
                        foreach (BuildingData building in simulation.BuildingRegistry.GetAllBuildings())
                            if (building.PlayerId == trustedPlayerId && !building.IsDestroyed)
                                foreach (int queuedType in building.TrainingQueue)
                                    if (queuedType == resolved) matchingQueued = checked(matchingQueued + 1);
                        owned = ownedCount; queued = matchingQueued;
                        remaining = checked(units.TargetTotal - ownedCount - matchingQueued);
                        long occupied = (long)context.Population + allQueued;
                        // PlanUnits returns Completed before inspecting capacity when the
                        // living-unit target is already satisfied.
                        if (ownedCount < units.TargetTotal
                            && (occupied > context.PopulationCap
                                || (remaining > 0 && occupied >= context.PopulationCap)))
                        {
                            // Capacity is a typed fact, including at the hard maximum; it does not
                            // imply a recoverable House or override a tactical Blocked status.
                            facts.Add((StrategicPlanHealthCategory.WaitingForPopulation, id));
                        }
                    }
                    if (category != StrategicPlanHealthCategory.Unknown)
                        facts.Add((category, id));
                    if (children.Count < 128)
                        children.Add(new StrategicPlanHealthChild(id, completed, goal != null,
                            status, category, blockedTicks, requested, resolved, target,
                            owned, queued, remaining));
                }
                if (milestone.Status == StrategicMilestoneStatus.WaitingForResources)
                    facts.Add((StrategicPlanHealthCategory.WaitingForResources, int.MaxValue));
                else if (milestone.Status == StrategicMilestoneStatus.WaitingForPrerequisite)
                    facts.Add((StrategicPlanHealthCategory.WaitingForPrerequisite, int.MaxValue));
            }

            var resources = CopyResources(plan, milestone, context);
            facts.Sort((a, b) =>
            {
                int order = HealthRank(a.Category).CompareTo(HealthRank(b.Category));
                return order != 0 ? order : a.Id.CompareTo(b.Id);
            });
            StrategicPlanHealthCategory primary = plan.Status switch
            {
                StrategicPlanStatus.Completed => StrategicPlanHealthCategory.Completed,
                StrategicPlanStatus.Cancelled => StrategicPlanHealthCategory.Cancelled,
                StrategicPlanStatus.Failed => StrategicPlanHealthCategory.Failed,
                StrategicPlanStatus.Paused => StrategicPlanHealthCategory.Paused,
                _ => facts.Count > 0 ? facts[0].Category
                    : hasProgressEvidence && unknownStatusCount == 0
                        ? StrategicPlanHealthCategory.Healthy : StrategicPlanHealthCategory.Unknown
            };
            var secondary = new List<StrategicPlanHealthCategory>();
            facts.Sort((a, b) => a.Id != b.Id
                ? a.Id.CompareTo(b.Id)
                : HealthRank(a.Category).CompareTo(HealthRank(b.Category)));
            foreach (var fact in facts)
                if (fact.Category != primary && !secondary.Contains(fact.Category)
                    && secondary.Count < 8) secondary.Add(fact.Category);
            var retainedCounts = new List<StrategicPlanHealthStatusCount>();
            foreach (var entry in statusCounts)
                retainedCounts.Add(new StrategicPlanHealthStatusCount(entry.Key, entry.Value));
            retainedCounts.Add(new StrategicPlanHealthStatusCount(null, unknownStatusCount));
            return new StrategicPlanHealthSnapshot(plan, milestone, tick, completedMilestones,
                retained, missing, context, allQueued, primary, secondary, resources,
                children, retainedCounts);
        }

        private List<StrategicPlanHealthResource> CopyResources(StrategicPlan plan,
            StrategicMilestone milestone, CommanderContext context)
        {
            var result = new List<StrategicPlanHealthResource>();
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int budget = 0, requirement = 0, planReserved = 0, globalReserved = 0;
                foreach (StrategicBudgetRequirement item in plan.BudgetRequirements)
                    if (item.ResourceType == type) budget = checked(budget + item.Amount);
                bool known = milestone != null && milestone.RequirementsResolved;
                if (known)
                    foreach (StrategicResourceRequirement item in milestone.RequiredResources)
                        if (item.ResourceType == type) requirement = checked(requirement + item.Amount);
                foreach (StrategicResourceReservation item in reservationManager.Reservations)
                    if (item.Status == StrategicResourceReservationStatus.Active
                        && item.ResourceType == type)
                    {
                        globalReserved = checked(globalReserved + item.Amount);
                        if (item.PlanId == plan.StrategicPlanId)
                            planReserved = checked(planReserved + item.Amount);
                    }
                int owned = type switch
                {
                    ResourceType.Food => context.Resources.Food,
                    ResourceType.Wood => context.Resources.Wood,
                    ResourceType.Gold => context.Resources.Gold,
                    ResourceType.Stone => context.Resources.Stone,
                    _ => 0
                };
                owned = Math.Max(0, owned);
                int otherReserved = checked(globalReserved - planReserved);
                int usableForPlan = Math.Max(0, owned - otherReserved);
                int deficit = known ? Math.Max(0, requirement - usableForPlan) : 0;
                result.Add(new StrategicPlanHealthResource(type, budget, known, requirement,
                    owned, deficit, planReserved, globalReserved));
            }
            return result;
        }

        private static bool ContainsId(IReadOnlyList<int> ids, int id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true;
            return false;
        }

        private static StrategicPlanHealthCategory CategoryForStatus(CommanderGoalStatus? status)
        {
            if (!status.HasValue) return StrategicPlanHealthCategory.Unknown;
            return status.Value switch
            {
                CommanderGoalStatus.Blocked => StrategicPlanHealthCategory.TemporarilyBlocked,
                CommanderGoalStatus.WaitingForResources => StrategicPlanHealthCategory.WaitingForResources,
                CommanderGoalStatus.WaitingForPrerequisite => StrategicPlanHealthCategory.WaitingForPrerequisite,
                CommanderGoalStatus.WaitingForProduction => StrategicPlanHealthCategory.WaitingForProduction,
                CommanderGoalStatus.WaitingForConstruction => StrategicPlanHealthCategory.WaitingForConstruction,
                _ => StrategicPlanHealthCategory.Unknown
            };
        }

        private static int HealthRank(StrategicPlanHealthCategory category) => category switch
        {
            StrategicPlanHealthCategory.TemporarilyBlocked => 0,
            StrategicPlanHealthCategory.WaitingForPopulation => 1,
            StrategicPlanHealthCategory.WaitingForResources => 2,
            StrategicPlanHealthCategory.WaitingForPrerequisite => 3,
            StrategicPlanHealthCategory.WaitingForProduction => 4,
            StrategicPlanHealthCategory.WaitingForConstruction => 5,
            _ => 6
        };

        private static int? BlockedDuration(int blockedSince, int observed, int pausedAt, bool paused)
        {
            if (blockedSince < 0 || observed < 0 || (paused && pausedAt < 0)) return null;
            long end = paused ? Math.Min((long)observed, pausedAt) : observed;
            long duration = end - blockedSince;
            if (duration < 0 || duration > int.MaxValue) return null;
            return (int)duration;
        }
    }
}
