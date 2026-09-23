using System;

namespace OpenEmpires
{
    public sealed class StrategicApprovalResult
    {
        public bool Approved { get; }
        public string Reason { get; }
        public StrategicPlanAuthority Authority { get; }
        public StrategicIntent Intent { get; }

        private StrategicApprovalResult(bool approved, string reason,
            StrategicPlanAuthority authority, StrategicIntent intent)
        {
            Approved = approved;
            Reason = reason;
            Authority = authority;
            Intent = intent;
        }

        internal static StrategicApprovalResult Accept(StrategicIntent intent,
            StrategicPlanAuthority authority) => new StrategicApprovalResult(true,
                "Strategic intent approved.", authority, intent);

        public static StrategicApprovalResult Reject(string reason) =>
            new StrategicApprovalResult(false, reason, StrategicPlanAuthority.Normal, null);
    }

    public sealed class StrategicApprovalLayer
    {
        public StrategicApprovalResult Evaluate(StrategicContext context, StrategicIntent intent,
            StrategicIntentSource source)
        {
            if (context == null || intent == null)
                return StrategicApprovalResult.Reject("A trusted context and intent are required.");
            if (!Enum.IsDefined(typeof(StrategicIntentSource), source) || source != intent.Source)
                return StrategicApprovalResult.Reject("Strategic intent source does not match trusted provenance.");
            if (intent.Status != StrategicIntentStatus.Created || intent.CreatedTick > context.SnapshotTick)
                return StrategicApprovalResult.Reject("Only a newly created, current-owner intent may be approved.");
            var validation = new StrategicIntentValidator().Validate(intent, context.PlayerId,
                StrategicPlanRegistry.CreateDefault());
            if (!validation.IsValid) return StrategicApprovalResult.Reject(validation.Reason);
            foreach (var parameter in intent.Parameters)
                if (intent.ObjectiveType != StrategicObjectiveType.AttackPreparation
                    || parameter.Key != "focus" || parameter.Value != "cavalry")
                    return StrategicApprovalResult.Reject("Unsupported strategic approval parameter.");
            if (source != StrategicIntentSource.PlayerDirect && intent.Priority.HasValue)
                return StrategicApprovalResult.Reject("AI requests cannot supply strategic priority.");

            if (source == StrategicIntentSource.AIRecommendation
                && intent.ObjectiveType == StrategicObjectiveType.AttackPreparation
                && HasEmergencyDefense(context))
                return StrategicApprovalResult.Reject("Emergency defense has higher priority.");

            foreach (var active in context.ActivePlans)
            {
                if (!Enum.TryParse(active.PlanType, out StrategicPlanType existing))
                    return StrategicApprovalResult.Reject("Active strategic plan type is unknown.");
                if (new StrategicCommitmentPolicy().AreCompatible(existing, PlanType(intent.ObjectiveType)))
                    continue;
                if (source == StrategicIntentSource.AIRecommendation
                    && active.Authority != StrategicPlanAuthority.Normal)
                    return StrategicApprovalResult.Reject(active.Authority == StrategicPlanAuthority.Emergency
                        ? "Emergency defense has higher priority." : "Player plans have higher priority.");
                if (source == StrategicIntentSource.AIConfirmedPlayerCommand
                    && active.Authority == StrategicPlanAuthority.PlayerOverride
                    && active.Source == StrategicIntentSource.PlayerDirect)
                    return StrategicApprovalResult.Reject("Direct player plans have higher priority than confirmed AI commands.");
            }

            StrategicFeasibility quote = null;
            foreach (var candidate in context.Feasibility)
                if (candidate.Objective == intent.ObjectiveType) { quote = candidate; break; }
            if (quote == null) return StrategicApprovalResult.Reject("Trusted feasibility information is unavailable.");
            if (!quote.Capable) return StrategicApprovalResult.Reject(quote.RejectionReason);
            foreach (var cost in quote.Costs)
                if (Available(context, cost.ResourceType) < cost.Amount)
                    return StrategicApprovalResult.Reject("Insufficient available " + cost.ResourceType + ".");
            if (intent.ObjectiveType == StrategicObjectiveType.AttackPreparation)
            {
                if (Available(context, ResourceType.Food) < RuleBasedStrategicEvaluator.AttackFoodThreshold
                    || Available(context, ResourceType.Gold) < RuleBasedStrategicEvaluator.AttackGoldThreshold)
                    return StrategicApprovalResult.Reject("Insufficient food or gold for attack preparation.");
                if (context.ArmyStrengthEstimate < RuleBasedStrategicEvaluator.StrongMilitaryThreshold)
                    return StrategicApprovalResult.Reject("Insufficient military capability for attack preparation.");
            }
            return StrategicApprovalResult.Accept(intent, source == StrategicIntentSource.AIRecommendation
                ? StrategicPlanAuthority.Normal : StrategicPlanAuthority.PlayerOverride);
        }

        internal static bool HasEmergencyDefense(StrategicContext context)
        {
            foreach (var plan in context.ActivePlans)
                if (plan.Authority == StrategicPlanAuthority.Emergency
                    && plan.PlanType == StrategicPlanType.DefensivePreparation.ToString()) return true;
            foreach (var recommendation in new RuleBasedStrategicEvaluator().Evaluate(context))
                if (recommendation.ObjectiveType == StrategicObjectiveType.DefensivePreparation
                    && recommendation.Score >= RuleBasedStrategicDecisionPolicy.CriticalDefenseScoreThreshold) return true;
            return false;
        }

        internal static StrategicPlanType PlanType(StrategicObjectiveType objective)
        {
            switch (objective)
            {
                case StrategicObjectiveType.AttackPreparation: return StrategicPlanType.CavalryPressure;
                case StrategicObjectiveType.DefensivePreparation: return StrategicPlanType.DefensivePreparation;
                case StrategicObjectiveType.EconomicExpansion: return StrategicPlanType.EconomicExpansion;
                case StrategicObjectiveType.MilitaryReinforcement: return StrategicPlanType.MilitaryReinforcement;
                default: throw new ArgumentOutOfRangeException(nameof(objective));
            }
        }

        private static int Available(StrategicContext context, ResourceType type)
        {
            foreach (var resource in context.Economy) if (resource.ResourceType == type) return resource.AvailableAmount;
            return 0;
        }
    }
}
