using System;

namespace OpenEmpires
{
    public enum StrategicPriorityLevel
    {
        Low,
        Normal,
        High,
        Emergency
    }

    public enum StrategicDecisionStatus
    {
        Selected,
        Rejected,
        NoDecision
    }

    // An explainable decision value. It grants no plan or command authority.
    public sealed class StrategicDecisionResult
    {
        public StrategicIntent SelectedIntent { get; }
        public string Reason { get; }
        public StrategicRecommendation SourceRecommendation { get; }
        public int CreatedTick { get; }
        public StrategicDecisionStatus Status { get; }
        public StrategicPriorityLevel PriorityLevel { get; }
        public bool HasSelection => Status == StrategicDecisionStatus.Selected
            && SelectedIntent != null;

        private StrategicDecisionResult(StrategicIntent selectedIntent, string reason,
            StrategicRecommendation sourceRecommendation, int createdTick,
            StrategicDecisionStatus status, StrategicPriorityLevel priorityLevel)
        {
            if (createdTick < 0) throw new ArgumentOutOfRangeException(nameof(createdTick));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A strategic decision reason is required.",
                    nameof(reason));
            if (status == StrategicDecisionStatus.Selected && selectedIntent == null)
                throw new ArgumentException("A selected decision requires an intent.",
                    nameof(selectedIntent));
            if (status != StrategicDecisionStatus.Selected && selectedIntent != null)
                throw new ArgumentException("Only a selected decision can contain an intent.",
                    nameof(selectedIntent));

            SelectedIntent = selectedIntent;
            Reason = reason;
            SourceRecommendation = sourceRecommendation;
            CreatedTick = createdTick;
            Status = status;
            PriorityLevel = priorityLevel;
        }

        internal static StrategicDecisionResult Selected(StrategicIntent intent,
            StrategicRecommendation source, int createdTick, StrategicPriorityLevel priority,
            string reason)
        {
            return new StrategicDecisionResult(intent, reason, source, createdTick,
                StrategicDecisionStatus.Selected, priority);
        }

        internal static StrategicDecisionResult Rejected(int createdTick, string reason)
        {
            return new StrategicDecisionResult(null, reason, null, createdTick,
                StrategicDecisionStatus.Rejected, StrategicPriorityLevel.Low);
        }

        internal static StrategicDecisionResult None(int createdTick, string reason)
        {
            return new StrategicDecisionResult(null, reason, null, createdTick,
                StrategicDecisionStatus.NoDecision, StrategicPriorityLevel.Low);
        }
    }
}
