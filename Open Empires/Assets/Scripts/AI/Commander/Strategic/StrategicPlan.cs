using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicPlanType
    {
        CavalryPressure
    }

    public enum StrategicPlanStatus
    {
        Created,
        Active,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public abstract class StrategicPlan
    {
        private readonly List<StrategicMilestone> milestones = new List<StrategicMilestone>();
        private readonly List<int> childGoalIds = new List<int>();
        private readonly List<StrategicResourceRequirement> requiredResources =
            new List<StrategicResourceRequirement>();
        private readonly List<int> resourceReservationIds = new List<int>();
        private int currentMilestoneIndex = -1;

        public int StrategicPlanId { get; internal set; }
        public int SourceIntentId { get; }
        public int OwnerPlayerId { get; }
        public StrategicPlanType PlanType { get; }
        public StrategicMilestone CurrentMilestone => currentMilestoneIndex >= 0
            && currentMilestoneIndex < milestones.Count ? milestones[currentMilestoneIndex] : null;
        public StrategicPlanStatus Status { get; internal set; }
        public int CreatedTick { get; internal set; }
        public IReadOnlyList<int> ChildGoalIds => childGoalIds;
        public IReadOnlyList<StrategicMilestone> Milestones => milestones;
        public IReadOnlyList<StrategicResourceRequirement> RequiredResources => requiredResources;
        public IReadOnlyList<int> ResourceReservationIds => resourceReservationIds;
        public string OutcomeMessage { get; internal set; } = string.Empty;
        public string CompletionMessage { get; }
        public string CancellationMessage { get; }
        public bool IsTerminal => Status == StrategicPlanStatus.Completed
            || Status == StrategicPlanStatus.Failed
            || Status == StrategicPlanStatus.Cancelled;

        protected StrategicPlan(int ownerPlayerId, StrategicPlanType planType, int sourceIntentId,
            string completionResponse, string cancellationResponse)
        {
            if (ownerPlayerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerPlayerId));
            if (sourceIntentId < 1) throw new ArgumentOutOfRangeException(nameof(sourceIntentId));
            OwnerPlayerId = ownerPlayerId;
            PlanType = planType;
            SourceIntentId = sourceIntentId;
            CompletionMessage = completionResponse ?? string.Empty;
            CancellationMessage = cancellationResponse ?? string.Empty;
            Status = StrategicPlanStatus.Created;
        }

        protected void AddMilestone(StrategicMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (milestone.OrderIndex != milestones.Count)
                throw new ArgumentException("Milestones must be added in contiguous order.", nameof(milestone));
            milestones.Add(milestone);
        }

        protected void AddRequiredResource(ResourceType resourceType, int amount)
        {
            for (int i = 0; i < requiredResources.Count; i++)
                if (requiredResources[i].ResourceType == resourceType)
                    throw new InvalidOperationException("A plan can define only one requirement per resource type.");
            requiredResources.Add(new StrategicResourceRequirement(resourceType, amount));
        }

        internal StrategicMilestone ActivateFirstMilestone()
        {
            if (milestones.Count == 0) throw new InvalidOperationException("A strategic plan requires at least one milestone.");
            currentMilestoneIndex = 0;
            milestones[0].SetStatus(StrategicMilestoneStatus.Active);
            return milestones[0];
        }

        internal StrategicMilestone AdvanceMilestone()
        {
            if (currentMilestoneIndex + 1 >= milestones.Count) return null;
            currentMilestoneIndex++;
            milestones[currentMilestoneIndex].SetStatus(StrategicMilestoneStatus.Active);
            return milestones[currentMilestoneIndex];
        }

        internal void AddChildGoal(int goalId)
        {
            if (!childGoalIds.Contains(goalId)) childGoalIds.Add(goalId);
        }


        internal void AddResourceReservation(int reservationId)
        {
            if (reservationId < 1) throw new ArgumentOutOfRangeException(nameof(reservationId));
            if (!resourceReservationIds.Contains(reservationId))
                resourceReservationIds.Add(reservationId);
        }
    }

    public sealed class CavalryPressurePlan : StrategicPlan
    {
        public const int FoodWorkerTarget = 10;
        public const int GoldWorkerTarget = 6;
        public const int KnightTarget = 6;
        public const int FoodRequirement = 800;
        public const int GoldRequirement = 500;
        public const string CompletionResponse = "Cavalry preparation complete.";
        public const string CancellationResponse = "Cavalry preparation cancelled.";

        internal CavalryPressurePlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.CavalryPressure, sourceIntentId,
                CompletionResponse, CancellationResponse)
        {
            AddRequiredResource(ResourceType.Food, FoodRequirement);
            AddRequiredResource(ResourceType.Gold, GoldRequirement);

            var economy = new StrategicMilestone(1, "Economic Foundation", 0);
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Food, FoodWorkerTarget));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Gold, GoldWorkerTarget));
            AddMilestone(economy);

            var infrastructure = new StrategicMilestone(2, "Infrastructure", 1);
            infrastructure.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.Stables));
            AddMilestone(infrastructure);

            var army = new StrategicMilestone(3, "Army Preparation", 2);
            army.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.KnightUnitType, KnightTarget));
            AddMilestone(army);
            AddMilestone(new StrategicMilestone(4, "Ready", 3));
        }
    }
}
