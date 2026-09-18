using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class StrategicDecisionRecord
    {
        public int DecisionId { get; }
        public int Tick { get; }
        public StrategicEvaluationTriggerType TriggerType { get; }
        public string TriggerReason { get; }
        public int? ActivePlanId { get; }
        public StrategicPlanType? ActivePlanType { get; }
        public string Outcome { get; }
        public int RecommendationCount { get; }
        public StrategicDecisionResult Decision { get; }
        public StrategicIntentSubmission Submission { get; }
        public bool TransitionAllowed { get; }

        public StrategicDecisionRecord(int decisionId, int tick,
            StrategicEvaluationTriggerType triggerType, string triggerReason,
            int? activePlanId, StrategicPlanType? activePlanType, string outcome,
            int recommendationCount = 0, StrategicDecisionResult decision = null,
            StrategicIntentSubmission submission = null, bool transitionAllowed = true)
        {
            DecisionId = decisionId;
            Tick = tick;
            TriggerType = triggerType;
            TriggerReason = triggerReason ?? string.Empty;
            ActivePlanId = activePlanId;
            ActivePlanType = activePlanType;
            Outcome = outcome ?? string.Empty;
            RecommendationCount = Math.Max(0, recommendationCount);
            Decision = decision;
            Submission = submission;
            TransitionAllowed = transitionAllowed;
        }
    }

    public sealed class RecentDecisionHistory
    {
        private readonly List<StrategicDecisionRecord> history = new List<StrategicDecisionRecord>();
        private readonly int capacity;
        private int nextDecisionId = 1;

        public IReadOnlyList<StrategicDecisionRecord> History => history;

        public RecentDecisionHistory(int capacity = 20)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public StrategicDecisionRecord RecordDecision(int tick,
            StrategicEvaluationTriggerType triggerType, string triggerReason,
            int? activePlanId, StrategicPlanType? activePlanType, string outcome,
            int recommendationCount = 0, StrategicDecisionResult decision = null,
            StrategicIntentSubmission submission = null, bool transitionAllowed = true)
        {
            var record = new StrategicDecisionRecord(nextDecisionId++, tick,
                triggerType, triggerReason, activePlanId, activePlanType, outcome,
                recommendationCount, decision, submission, transitionAllowed);
            if (history.Count >= capacity)
            {
                history.RemoveAt(0);
            }
            history.Add(record);
            return record;
        }

        public void Clear()
        {
            history.Clear();
        }
    }
}
