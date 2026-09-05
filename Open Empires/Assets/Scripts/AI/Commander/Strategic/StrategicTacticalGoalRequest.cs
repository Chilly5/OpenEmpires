using System;

namespace OpenEmpires
{
    public abstract class StrategicTacticalGoalRequest
    {
        internal abstract CommanderGoal Submit(CommanderGoalManager goalManager);
    }

    public sealed class StrategicResourceAllocationGoalRequest : StrategicTacticalGoalRequest
    {
        public ResourceType ResourceType { get; }
        public int WorkerTarget { get; }

        public StrategicResourceAllocationGoalRequest(ResourceType resourceType, int workerTarget)
        {
            if (workerTarget < 0) throw new ArgumentOutOfRangeException(nameof(workerTarget));
            ResourceType = resourceType;
            WorkerTarget = workerTarget;
        }

        internal override CommanderGoal Submit(CommanderGoalManager goalManager) =>
            goalManager.SubmitResourceAllocation(ResourceType, WorkerTarget);
    }

    public sealed class StrategicBuildStructureGoalRequest : StrategicTacticalGoalRequest
    {
        public BuildingType StructureType { get; }
        public int Count { get; }

        public StrategicBuildStructureGoalRequest(BuildingType structureType, int count = 1)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            StructureType = structureType;
            Count = count;
        }

        internal override CommanderGoal Submit(CommanderGoalManager goalManager) =>
            goalManager.SubmitBuildStructure(StructureType, Count);
    }

    public sealed class StrategicEnsureUnitCountGoalRequest : StrategicTacticalGoalRequest
    {
        public int UnitType { get; }
        public int TargetTotal { get; }
        public int MaximumQueue { get; }

        public StrategicEnsureUnitCountGoalRequest(int unitType, int targetTotal,
            int maximumQueue = 3)
        {
            if (unitType < 0) throw new ArgumentOutOfRangeException(nameof(unitType));
            if (targetTotal < 1) throw new ArgumentOutOfRangeException(nameof(targetTotal));
            if (maximumQueue < 1) throw new ArgumentOutOfRangeException(nameof(maximumQueue));
            UnitType = unitType;
            TargetTotal = targetTotal;
            MaximumQueue = maximumQueue;
        }

        internal override CommanderGoal Submit(CommanderGoalManager goalManager) =>
            goalManager.SubmitEnsureUnitCount(UnitType, TargetTotal, MaximumQueue);
    }
}
