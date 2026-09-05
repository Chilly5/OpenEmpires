using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicMilestoneStatus
    {
        Pending,
        Active,
        Completed,
        Failed,
        Skipped
    }

    public sealed class StrategicMilestone
    {
        private readonly List<int> requiredChildGoals = new List<int>();
        private readonly List<int> completedChildGoals = new List<int>();
        private readonly List<StrategicTacticalGoalRequest> tacticalGoals =
            new List<StrategicTacticalGoalRequest>();

        public int MilestoneId { get; }
        public string Name { get; }
        public int OrderIndex { get; }
        public StrategicMilestoneStatus Status { get; private set; }
        public IReadOnlyList<int> RequiredChildGoals => requiredChildGoals;
        public IReadOnlyList<int> CompletedChildGoals => completedChildGoals;
        public IReadOnlyList<StrategicTacticalGoalRequest> TacticalGoals => tacticalGoals;

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
    }
}
