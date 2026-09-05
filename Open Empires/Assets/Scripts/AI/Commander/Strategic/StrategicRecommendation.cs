using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicRecommendationStatus
    {
        Proposed,
        ConvertedToIntent,
        Dismissed
    }

    // A local, detached evaluation result. It has no execution authority.
    public sealed class StrategicRecommendation
    {
        public int RecommendationId { get; }
        public int PlayerId { get; }
        public StrategicObjectiveType ObjectiveType { get; }
        public int Score { get; }
        public string Reason { get; }
        public int CreatedTick { get; }
        public int Priority { get; }
        public StrategicRecommendationStatus Status { get; private set; }

        public StrategicRecommendation(int recommendationId, int playerId,
            StrategicObjectiveType objectiveType, int score, string reason,
            int createdTick, int priority)
        {
            if (recommendationId < 1)
                throw new ArgumentOutOfRangeException(nameof(recommendationId));
            if (!Enum.IsDefined(typeof(StrategicObjectiveType), objectiveType))
                throw new ArgumentOutOfRangeException(nameof(objectiveType));
            if (score < 0 || score > 100)
                throw new ArgumentOutOfRangeException(nameof(score));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A strategic recommendation reason is required.",
                    nameof(reason));
            if (createdTick < 0)
                throw new ArgumentOutOfRangeException(nameof(createdTick));
            if (priority < 0 || priority > 100)
                throw new ArgumentOutOfRangeException(nameof(priority));

            RecommendationId = recommendationId;
            PlayerId = playerId;
            ObjectiveType = objectiveType;
            Score = score;
            Reason = reason;
            CreatedTick = createdTick;
            Priority = priority;
            Status = StrategicRecommendationStatus.Proposed;
        }

        public StrategicIntent ToStrategicIntent(int intentId,
            IDictionary<string, string> parameters = null)
        {
            if (Status != StrategicRecommendationStatus.Proposed)
                throw new InvalidOperationException(
                    "Only a proposed strategic recommendation can become an intent.");

            var intent = new StrategicIntent(intentId, PlayerId, ObjectiveType,
                CreatedTick, parameters, Priority);
            Status = StrategicRecommendationStatus.ConvertedToIntent;
            return intent;
        }

        public bool Dismiss()
        {
            if (Status != StrategicRecommendationStatus.Proposed) return false;
            Status = StrategicRecommendationStatus.Dismissed;
            return true;
        }
    }
}
