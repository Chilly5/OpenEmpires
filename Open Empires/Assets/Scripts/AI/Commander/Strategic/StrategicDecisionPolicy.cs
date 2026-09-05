using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public interface IStrategicDecisionPolicy
    {
        StrategicIntent SelectIntent(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations);

        StrategicIntent SelectIntent(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations,
            StrategicIntent playerIntent);

        StrategicDecisionResult Decide(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations);

        StrategicDecisionResult Decide(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations,
            StrategicIntent playerIntent);
    }

    // Stateless priority rules over detached strategic values. Selection creates an
    // intent value only; an explicit caller must still submit it to StrategicPlanner.
    public sealed class RuleBasedStrategicDecisionPolicy : IStrategicDecisionPolicy
    {
        public const int CriticalDefenseScoreThreshold = 80;
        public const int MilitaryReinforcementScoreThreshold = 85;
        public const int AttackPreparationScoreThreshold = 90;

        public const string PlayerOverrideReason =
            "Explicit player strategic intent overrides AI recommendations.";
        public const string NoRecommendationReason =
            "No strategic recommendations were available.";
        public const string NoEligibleRecommendationReason =
            "No recommendation satisfied the deterministic decision rules.";

        public StrategicIntent SelectIntent(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations)
        {
            return Decide(context, recommendations).SelectedIntent;
        }

        public StrategicIntent SelectIntent(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations,
            StrategicIntent playerIntent)
        {
            return Decide(context, recommendations, playerIntent).SelectedIntent;
        }

        public StrategicDecisionResult Decide(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations)
        {
            return Decide(context, recommendations, null);
        }

        public StrategicDecisionResult Decide(StrategicContext context,
            IReadOnlyList<StrategicRecommendation> recommendations,
            StrategicIntent playerIntent)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (recommendations == null)
                throw new ArgumentNullException(nameof(recommendations));

            if (playerIntent != null)
                return DecidePlayerOverride(context, playerIntent);

            StrategicRecommendation selected = FindBest(recommendations, context.PlayerId,
                StrategicObjectiveType.DefensivePreparation,
                CriticalDefenseScoreThreshold);
            if (selected != null)
                return Select(context, selected, StrategicPriorityLevel.Emergency);

            selected = FindBest(recommendations, context.PlayerId,
                StrategicObjectiveType.MilitaryReinforcement,
                MilitaryReinforcementScoreThreshold);
            if (selected != null)
                return Select(context, selected, StrategicPriorityLevel.High);

            selected = FindBest(recommendations, context.PlayerId,
                StrategicObjectiveType.AttackPreparation,
                AttackPreparationScoreThreshold);
            if (selected != null && AttackConditionsAreMet(context))
                return Select(context, selected, StrategicPriorityLevel.Normal);

            selected = FindBest(recommendations, context.PlayerId,
                StrategicObjectiveType.EconomicExpansion, 0);
            if (selected != null)
                return Select(context, selected, StrategicPriorityLevel.Low);

            return recommendations.Count == 0
                ? StrategicDecisionResult.None(context.SnapshotTick,
                    NoRecommendationReason)
                : StrategicDecisionResult.Rejected(context.SnapshotTick,
                    NoEligibleRecommendationReason);
        }

        public static StrategicPriorityLevel GetPriorityLevel(
            StrategicObjectiveType objectiveType)
        {
            switch (objectiveType)
            {
                case StrategicObjectiveType.DefensivePreparation:
                    return StrategicPriorityLevel.Emergency;
                case StrategicObjectiveType.MilitaryReinforcement:
                    return StrategicPriorityLevel.High;
                case StrategicObjectiveType.AttackPreparation:
                    return StrategicPriorityLevel.Normal;
                case StrategicObjectiveType.EconomicExpansion:
                    return StrategicPriorityLevel.Low;
                default:
                    throw new ArgumentOutOfRangeException(nameof(objectiveType));
            }
        }

        private static StrategicDecisionResult DecidePlayerOverride(
            StrategicContext context, StrategicIntent playerIntent)
        {
            if (playerIntent.PlayerId != context.PlayerId)
                return StrategicDecisionResult.Rejected(context.SnapshotTick,
                    "Player strategic intent ownership does not match the strategic context.");
            if (!Enum.IsDefined(typeof(StrategicObjectiveType), playerIntent.ObjectiveType))
                return StrategicDecisionResult.Rejected(context.SnapshotTick,
                    "Player strategic intent has an unknown objective.");
            if (playerIntent.Status != StrategicIntentStatus.Created)
                return StrategicDecisionResult.Rejected(context.SnapshotTick,
                    "Only a newly created player strategic intent can be selected.");

            return StrategicDecisionResult.Selected(playerIntent, null,
                context.SnapshotTick, GetPriorityLevel(playerIntent.ObjectiveType),
                PlayerOverrideReason);
        }

        private static StrategicDecisionResult Select(StrategicContext context,
            StrategicRecommendation recommendation, StrategicPriorityLevel priority)
        {
            int intentId = CreateDeterministicIntentId(context, recommendation);
            var intent = new StrategicIntent(intentId, context.PlayerId,
                recommendation.ObjectiveType, context.SnapshotTick,
                priority: recommendation.Priority);
            string reason = $"Selected {priority} {recommendation.ObjectiveType} "
                + $"recommendation #{recommendation.RecommendationId} "
                + $"(score {recommendation.Score}): {recommendation.Reason}";
            return StrategicDecisionResult.Selected(intent, recommendation,
                context.SnapshotTick, priority, reason);
        }

        private static StrategicRecommendation FindBest(
            IReadOnlyList<StrategicRecommendation> recommendations, int playerId,
            StrategicObjectiveType objectiveType, int minimumScore)
        {
            StrategicRecommendation best = null;
            for (int i = 0; i < recommendations.Count; i++)
            {
                StrategicRecommendation candidate = recommendations[i];
                if (candidate == null
                    || candidate.PlayerId != playerId
                    || candidate.Status != StrategicRecommendationStatus.Proposed
                    || candidate.ObjectiveType != objectiveType
                    || candidate.Score < minimumScore) continue;
                if (best == null || Compare(candidate, best) < 0) best = candidate;
            }
            return best;
        }

        private static int Compare(StrategicRecommendation left,
            StrategicRecommendation right)
        {
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0) return priority;
            int id = left.RecommendationId.CompareTo(right.RecommendationId);
            return id != 0 ? id : left.CreatedTick.CompareTo(right.CreatedTick);
        }

        private static bool AttackConditionsAreMet(StrategicContext context)
        {
            return context.ArmyStrengthEstimate
                    >= RuleBasedStrategicEvaluator.StrongMilitaryThreshold
                && Available(context, ResourceType.Food)
                    >= RuleBasedStrategicEvaluator.AttackFoodThreshold
                && Available(context, ResourceType.Gold)
                    >= RuleBasedStrategicEvaluator.AttackGoldThreshold;
        }

        private static int Available(StrategicContext context, ResourceType resourceType)
        {
            for (int i = 0; i < context.Economy.Count; i++)
                if (context.Economy[i].ResourceType == resourceType)
                    return context.Economy[i].AvailableAmount;
            return 0;
        }

        private static int CreateDeterministicIntentId(StrategicContext context,
            StrategicRecommendation recommendation)
        {
            const long idRange = int.MaxValue - 1L;
            long value = (long)context.SnapshotTick * 31L
                + (long)context.PlayerId * 17L
                + recommendation.RecommendationId;
            value %= idRange;
            if (value < 0) value += idRange;
            return (int)value + 1;
        }
    }
}
