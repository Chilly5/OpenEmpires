using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenEmpires
{
    public sealed class StrategicContextInsights
    {
        public bool IncomeTrendAvailable { get; }
        public bool WorkerActivityAvailable { get; }
        public int TotalWorkers { get; }
        public int GatheringWorkers { get; }
        public int WorkerActivityBasisPoints { get; }
        public IReadOnlyList<StrategicArmyCompositionInsight> ArmyComposition { get; }
        public IReadOnlyList<StrategicProductionPressureInsight> ProductionPressure { get; }
        public IReadOnlyList<StrategicPlanProgressInsight> PlanProgress { get; }

        internal StrategicContextInsights(bool workerActivityAvailable, int totalWorkers,
            int gatheringWorkers, int workerActivityBasisPoints,
            IList<StrategicArmyCompositionInsight> armyComposition,
            IList<StrategicProductionPressureInsight> productionPressure,
            IList<StrategicPlanProgressInsight> planProgress)
        {
            IncomeTrendAvailable = false;
            WorkerActivityAvailable = workerActivityAvailable;
            TotalWorkers = totalWorkers;
            GatheringWorkers = gatheringWorkers;
            WorkerActivityBasisPoints = workerActivityBasisPoints;
            ArmyComposition = new ReadOnlyCollection<StrategicArmyCompositionInsight>(
                new List<StrategicArmyCompositionInsight>(armyComposition));
            ProductionPressure = new ReadOnlyCollection<StrategicProductionPressureInsight>(
                new List<StrategicProductionPressureInsight>(productionPressure));
            PlanProgress = new ReadOnlyCollection<StrategicPlanProgressInsight>(
                new List<StrategicPlanProgressInsight>(planProgress));
        }
    }

    public sealed class StrategicArmyCompositionInsight
    {
        public int UnitType { get; }
        public int OwnedCount { get; }
        public int QueuedCount { get; }
        public int OwnedShareBasisPoints { get; }

        internal StrategicArmyCompositionInsight(int unitType, int ownedCount,
            int queuedCount, int ownedShareBasisPoints)
        {
            UnitType = unitType;
            OwnedCount = ownedCount;
            QueuedCount = queuedCount;
            OwnedShareBasisPoints = ownedShareBasisPoints;
        }
    }

    public sealed class StrategicProductionPressureInsight
    {
        public string BuildingType { get; }
        public int CompletedCount { get; }
        public int UnderConstructionCount { get; }
        public int ActiveQueueCount { get; }
        public int QueuedUnitCount { get; }
        public int IdleCompletedCount { get; }

        internal StrategicProductionPressureInsight(string buildingType, int completedCount,
            int underConstructionCount, int activeQueueCount, int queuedUnitCount,
            int idleCompletedCount)
        {
            BuildingType = buildingType;
            CompletedCount = completedCount;
            UnderConstructionCount = underConstructionCount;
            ActiveQueueCount = activeQueueCount;
            QueuedUnitCount = queuedUnitCount;
            IdleCompletedCount = idleCompletedCount;
        }
    }

    public sealed class StrategicPlanProgressInsight
    {
        public int StrategicPlanId { get; }
        public string PlanType { get; }
        public string Status { get; }
        public int CompletedMilestoneCount { get; }
        public int TotalMilestoneCount { get; }
        public string CurrentMilestone { get; }
        public string MilestoneStatus { get; }

        internal StrategicPlanProgressInsight(int strategicPlanId, string planType,
            string status, int completedMilestoneCount, int totalMilestoneCount,
            string currentMilestone, string milestoneStatus)
        {
            StrategicPlanId = strategicPlanId;
            PlanType = planType;
            Status = status;
            CompletedMilestoneCount = completedMilestoneCount;
            TotalMilestoneCount = totalMilestoneCount;
            CurrentMilestone = currentMilestone;
            MilestoneStatus = milestoneStatus;
        }
    }
}
