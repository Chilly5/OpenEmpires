using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicMilestoneStatus
    {
        Pending,
        WaitingForResources,
        Active,
        Completed,
        Failed,
        Skipped,
        WaitingForPrerequisite
    }

    public sealed class StrategicMilestone
    {
        private readonly List<int> requiredChildGoals = new List<int>();
        private readonly List<int> completedChildGoals = new List<int>();
        private readonly List<StrategicTacticalGoalRequest> tacticalGoals =
            new List<StrategicTacticalGoalRequest>();
        private readonly List<StrategicResourceRequirement> requiredResources =
            new List<StrategicResourceRequirement>();
        private readonly List<int> resourceReservationIds =
            new List<int>();
        private bool tacticalGoalsStarted;
        private readonly HashSet<int> startedRequests = new HashSet<int>();

        public int MilestoneId { get; }
        public string Name { get; }
        public int OrderIndex { get; }
        public StrategicMilestoneStatus Status { get; private set; }
        public IReadOnlyList<int> RequiredChildGoals => requiredChildGoals;
        public IReadOnlyList<int> CompletedChildGoals => completedChildGoals;
        public IReadOnlyList<StrategicTacticalGoalRequest> TacticalGoals => tacticalGoals;
        public IReadOnlyList<StrategicResourceRequirement> RequiredResources => requiredResources;
        public IReadOnlyList<int> ResourceReservationIds => resourceReservationIds;
        public bool HasActiveRequirements => requiredResources.Count > 0;
        internal bool TacticalGoalsStarted => tacticalGoalsStarted;
        internal bool RequirementsResolved { get; set; }
        internal bool UsesCanonicalRequirements { get; set; }
        internal void ClearRequirements() => requiredResources.Clear();
        internal bool StartRequest(int index) => startedRequests.Add(index);

        internal bool IsSatisfied => requiredChildGoals.Count == completedChildGoals.Count;

        internal StrategicMilestone(int milestoneId, string name, int orderIndex)
        {
            if (milestoneId < 1) throw new ArgumentOutOfRangeException(nameof(milestoneId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A milestone name is required.", nameof(name));
            if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex));
            MilestoneId = milestoneId;
            Name = name;
            OrderIndex = orderIndex;
            Status = StrategicMilestoneStatus.Pending;
        }

        internal void SetStatus(StrategicMilestoneStatus status)
        {
            Status = status;
        }

        internal void AddRequiredChildGoal(int goalId)
        {
            if (goalId < 1) throw new ArgumentOutOfRangeException(nameof(goalId));
            if (!requiredChildGoals.Contains(goalId)) requiredChildGoals.Add(goalId);
        }

        internal void AddTacticalGoal(StrategicTacticalGoalRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            tacticalGoals.Add(request);
        }

        internal void MarkChildGoalCompleted(int goalId)
        {
            if (requiredChildGoals.Contains(goalId) && !completedChildGoals.Contains(goalId))
                completedChildGoals.Add(goalId);
        }

        public void AddRequiredResource(ResourceType resourceType, int amount)
        {
            for (int i = 0; i < requiredResources.Count; i++)
                if (requiredResources[i].ResourceType == resourceType)
                    throw new InvalidOperationException("A milestone can define only one requirement per resource type.");
            requiredResources.Add(new StrategicResourceRequirement(resourceType, amount));
        }

        internal void AddResourceReservation(int reservationId)
        {
            if (reservationId < 1) throw new ArgumentOutOfRangeException(nameof(reservationId));
            if (!resourceReservationIds.Contains(reservationId))
                resourceReservationIds.Add(reservationId);
        }

        internal void MarkTacticalGoalsStarted()
        {
            tacticalGoalsStarted = true;
        }
    }
}
