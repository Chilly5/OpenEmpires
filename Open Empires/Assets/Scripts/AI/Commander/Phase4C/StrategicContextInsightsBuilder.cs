using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class StrategicContextInsightsBuilder
    {
        public StrategicContextInsights Build(int totalWorkers,
            IReadOnlyList<StrategicWorkerAllocationState> workerAllocation,
            IReadOnlyList<StrategicMilitaryState> military,
            IReadOnlyList<StrategicProductionState> production,
            IReadOnlyList<StrategicPlanState> plans)
        {
            if (totalWorkers < 0) throw new ArgumentOutOfRangeException(nameof(totalWorkers));
            if (workerAllocation == null) throw new ArgumentNullException(nameof(workerAllocation));
            if (military == null) throw new ArgumentNullException(nameof(military));
            if (production == null) throw new ArgumentNullException(nameof(production));
            if (plans == null) throw new ArgumentNullException(nameof(plans));

            long gathering = 0;
            foreach (StrategicWorkerAllocationState allocation in workerAllocation)
            {
                if (allocation == null || allocation.AssignedWorkers < 0)
                    throw new ArgumentException("Worker allocations must have nonnegative counts.",
                        nameof(workerAllocation));
                gathering = checked(gathering + allocation.AssignedWorkers);
            }
            int gatheringWorkers = ToInt(gathering);
            bool activityAvailable = totalWorkers > 0;
            int activityBasisPoints = activityAvailable
                ? (int)Math.Min(10000L, gathering * 10000L / totalWorkers) : 0;

            var armyByType = new SortedDictionary<int, ArmyCounts>();
            long totalOwned = 0;
            foreach (StrategicMilitaryState state in military)
            {
                if (state == null || state.OwnedCount < 0 || state.QueuedCount < 0)
                    throw new ArgumentException("Military counts must be nonnegative.", nameof(military));
                if (state.UnitType == 0 || state.UnitType == 5) continue;
                armyByType.TryGetValue(state.UnitType, out ArmyCounts counts);
                counts.Owned = checked(counts.Owned + state.OwnedCount);
                counts.Queued = checked(counts.Queued + state.QueuedCount);
                armyByType[state.UnitType] = counts;
                totalOwned = checked(totalOwned + state.OwnedCount);
            }
            ToInt(totalOwned);
            var army = new List<StrategicArmyCompositionInsight>();
            foreach (KeyValuePair<int, ArmyCounts> pair in armyByType)
            {
                int owned = ToInt(pair.Value.Owned);
                army.Add(new StrategicArmyCompositionInsight(pair.Key, owned,
                    ToInt(pair.Value.Queued), totalOwned == 0 ? 0
                        : (int)(pair.Value.Owned * 10000L / totalOwned)));
            }

            var productionByType = new SortedDictionary<string, ProductionCounts>(StringComparer.Ordinal);
            foreach (StrategicProductionState state in production)
            {
                if (state == null || state.BuildingType == null
                    || state.ProductionBuildingCount < 0 || state.UnderConstructionCount < 0
                    || state.ActiveQueueCount < 0 || state.QueuedUnitCount < 0
                    || state.AvailableCapacity < 0
                    || state.UnderConstructionCount > state.ProductionBuildingCount)
                    throw new ArgumentException("Production counts must be consistent and nonnegative.",
                        nameof(production));
                productionByType.TryGetValue(state.BuildingType, out ProductionCounts counts);
                counts.Completed = checked(counts.Completed
                    + state.ProductionBuildingCount - state.UnderConstructionCount);
                counts.UnderConstruction = checked(counts.UnderConstruction
                    + state.UnderConstructionCount);
                counts.ActiveQueues = checked(counts.ActiveQueues + state.ActiveQueueCount);
                counts.QueuedUnits = checked(counts.QueuedUnits + state.QueuedUnitCount);
                counts.IdleCompleted = checked(counts.IdleCompleted + state.AvailableCapacity);
                productionByType[state.BuildingType] = counts;
            }
            var pressure = new List<StrategicProductionPressureInsight>();
            foreach (KeyValuePair<string, ProductionCounts> pair in productionByType)
                pressure.Add(new StrategicProductionPressureInsight(pair.Key,
                    ToInt(pair.Value.Completed), ToInt(pair.Value.UnderConstruction),
                    ToInt(pair.Value.ActiveQueues), ToInt(pair.Value.QueuedUnits),
                    ToInt(pair.Value.IdleCompleted)));

            var sortedPlans = new List<StrategicPlanState>(plans);
            if (sortedPlans.Exists(value => value == null))
                throw new ArgumentException("Plans must not contain null.", nameof(plans));
            sortedPlans.Sort((left, right) => left.StrategicPlanId.CompareTo(right.StrategicPlanId));
            var progress = new List<StrategicPlanProgressInsight>();
            foreach (StrategicPlanState plan in sortedPlans)
            {
                if (plan.CompletedMilestoneCount < 0 || plan.TotalMilestoneCount < 0
                    || plan.CompletedMilestoneCount > plan.TotalMilestoneCount)
                    throw new ArgumentException("Plan milestone counts must be consistent.", nameof(plans));
                progress.Add(new StrategicPlanProgressInsight(plan.StrategicPlanId,
                    plan.PlanType, plan.Status, plan.CompletedMilestoneCount,
                    plan.TotalMilestoneCount, plan.CurrentMilestone, plan.MilestoneStatus));
            }

            return new StrategicContextInsights(activityAvailable, totalWorkers, gatheringWorkers,
                activityBasisPoints, army, pressure, progress);
        }

        private static int ToInt(long value)
        {
            if (value > int.MaxValue || value < int.MinValue)
                throw new OverflowException("Strategic insight count exceeds Int32 range.");
            return (int)value;
        }

        private struct ArmyCounts
        {
            public long Owned;
            public long Queued;
        }

        private struct ProductionCounts
        {
            public long Completed;
            public long UnderConstruction;
            public long ActiveQueues;
            public long QueuedUnits;
            public long IdleCompleted;
        }
    }
}
