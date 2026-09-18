using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenEmpires
{
    public enum ExplanationOutcome
    {
        NoDecision,
        Rejected,
        TransitionRefused,
        PlannerRejected,
        PlanCreated,
        SelectionNotSubmitted
    }

    public enum CommanderExplanationQuery
    {
        LastDecision,
        LastRejection,
        AttackReason,
        CurrentPlan
    }

    public sealed class ExplanationPlanState
    {
        public const int MaximumTextLength = 512;
        public int PlanId { get; }
        public string PlanType { get; }
        public string Status { get; }
        public string CurrentMilestone { get; }
        public string MilestoneStatus { get; }
        public string Reason { get; }

        public ExplanationPlanState(int planId, string planType, string status,
            string currentMilestone, string milestoneStatus, string reason)
        {
            if (planId < 0) throw new ArgumentOutOfRangeException(nameof(planId));
            PlanId = planId;
            PlanType = Bound(planType);
            Status = Bound(status);
            CurrentMilestone = Bound(currentMilestone);
            MilestoneStatus = Bound(milestoneStatus);
            Reason = Bound(reason);
        }

        private static string Bound(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength
                ? value : value.Substring(0, MaximumTextLength);
        }
    }

    public sealed class ExplanationContext
    {
        public const int MaximumPlans = 32;
        public const int MaximumTextLength = 512;
        public int PlayerId { get; }
        public int? DecisionId { get; }
        public int? DecisionTick { get; }
        public ExplanationOutcome Outcome { get; }
        public string Reason { get; }
        public string RequestedObjective { get; }
        public int? AcceptedPlanId { get; }
        public string AcceptedPlanType { get; }
        public int? CurrentSnapshotTick { get; }
        public IReadOnlyList<ExplanationPlanState> CurrentPlans { get; }

        public ExplanationContext(int playerId, int? decisionId = null,
            int? decisionTick = null, ExplanationOutcome outcome = ExplanationOutcome.NoDecision,
            string reason = null, string requestedObjective = null,
            int? acceptedPlanId = null, string acceptedPlanType = null,
            int? currentSnapshotTick = null,
            IReadOnlyList<ExplanationPlanState> currentPlans = null)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (decisionId.HasValue && decisionId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionId));
            if (decisionTick.HasValue && decisionTick.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionTick));
            if (acceptedPlanId.HasValue && acceptedPlanId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(acceptedPlanId));
            if (currentSnapshotTick.HasValue && currentSnapshotTick.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSnapshotTick));
            if (!Enum.IsDefined(typeof(ExplanationOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            PlayerId = playerId;
            DecisionId = decisionId;
            DecisionTick = decisionTick;
            Outcome = outcome;
            Reason = Bound(reason);
            RequestedObjective = Bound(requestedObjective);
            AcceptedPlanId = acceptedPlanId;
            AcceptedPlanType = Bound(acceptedPlanType);
            CurrentSnapshotTick = currentSnapshotTick;
            var plans = new List<ExplanationPlanState>();
            if (currentPlans != null)
                for (int i = 0; i < currentPlans.Count && plans.Count < MaximumPlans; i++)
                    if (currentPlans[i] != null)
                        plans.Add(currentPlans[i]);
            CurrentPlans = new ReadOnlyCollection<ExplanationPlanState>(plans);
        }

        private static string Bound(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength
                ? value : value.Substring(0, MaximumTextLength);
        }
    }
}
