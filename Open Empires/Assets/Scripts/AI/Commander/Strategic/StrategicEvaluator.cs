using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public interface IStrategicEvaluator
    {
        IReadOnlyList<StrategicRecommendation> Evaluate(StrategicContext context);
    }

    // Deterministic rules over one detached StrategicContext snapshot.
    // This evaluator ranks possible objectives but never selects or executes one.
    public sealed class RuleBasedStrategicEvaluator : IStrategicEvaluator
    {
        public const int LowMilitaryThreshold = 8;
        public const int StrongMilitaryThreshold = 12;
        public const int LowDefensiveCapabilityThreshold = 10;
        public const int AvailablePopulationThreshold = 5;
        public const int LowGatheringWorkerThreshold = 8;
        public const int ProductionFoodThreshold = 100;
        public const int ProductionSecondaryResourceThreshold = 100;
        public const int AttackFoodThreshold = 800;
        public const int AttackGoldThreshold = 500;

        public const string MilitaryReason =
            "Military capacity is below current economic capability.";
        public const string DefenseReason =
            "Visible threat detected with insufficient defense.";
        public const string EconomyReason =
            "Economy can support additional growth.";
        public const string AttackReason =
            "Current military and economy support offensive preparation.";

        public IReadOnlyList<StrategicRecommendation> Evaluate(StrategicContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var recommendations = new List<StrategicRecommendation>();
            int food = Available(context, ResourceType.Food);
            int wood = Available(context, ResourceType.Wood);
            int gold = Available(context, ResourceType.Gold);
            bool populationAvailable = context.Population.AvailableCapacity
                >= AvailablePopulationThreshold;

            bool productionResources = food >= ProductionFoodThreshold
                && (wood >= ProductionSecondaryResourceThreshold
                    || gold >= ProductionSecondaryResourceThreshold);
            if (context.TotalMilitaryUnits < LowMilitaryThreshold
                && populationAvailable && productionResources)
            {
                int score = 35 + 20 + 15 + 15;
                if (HasAvailableProduction(context)) score += 15;
                Add(recommendations, context,
                    StrategicObjectiveType.MilitaryReinforcement, score, MilitaryReason);
            }

            if (context.Threat.VisibleEnemyMilitaryUnits > 0
                && context.Defense.CapabilityEstimate < LowDefensiveCapabilityThreshold)
            {
                int threatSupport = Math.Min(25,
                    context.Threat.VisibleEnemyMilitaryStrength * 5);
                Add(recommendations, context,
                    StrategicObjectiveType.DefensivePreparation,
                    45 + 30 + threatSupport, DefenseReason);
            }

            if (populationAvailable
                && context.TotalGatheringWorkers < LowGatheringWorkerThreshold)
            {
                int shortage = LowGatheringWorkerThreshold - context.TotalGatheringWorkers;
                int score = 40 + 35 + Math.Min(25, shortage * 4);
                Add(recommendations, context,
                    StrategicObjectiveType.EconomicExpansion, score, EconomyReason);
            }

            if (context.ArmyStrengthEstimate >= StrongMilitaryThreshold
                && food >= AttackFoodThreshold && gold >= AttackGoldThreshold)
            {
                Add(recommendations, context,
                    StrategicObjectiveType.AttackPreparation, 100, AttackReason);
            }

            recommendations.Sort(CompareRecommendations);
            return recommendations.AsReadOnly();
        }

        private static int Available(StrategicContext context, ResourceType resourceType)
        {
            for (int i = 0; i < context.Economy.Count; i++)
                if (context.Economy[i].ResourceType == resourceType)
                    return context.Economy[i].AvailableAmount;
            return 0;
        }

        private static bool HasAvailableProduction(StrategicContext context)
        {
            for (int i = 0; i < context.Production.Count; i++)
                if (context.Production[i].AvailableCapacity > 0) return true;
            return false;
        }

        private static void Add(List<StrategicRecommendation> recommendations,
            StrategicContext context, StrategicObjectiveType objectiveType,
            int score, string reason)
        {
            int boundedScore = Math.Max(0, Math.Min(100, score));
            recommendations.Add(new StrategicRecommendation(
                (int)objectiveType + 1, context.PlayerId, objectiveType,
                boundedScore, reason, context.SnapshotTick, boundedScore));
        }

        private static int CompareRecommendations(StrategicRecommendation left,
            StrategicRecommendation right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.ObjectiveType.CompareTo(right.ObjectiveType);
        }
    }
}
