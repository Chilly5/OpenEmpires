using System;

namespace OpenEmpires
{
    public sealed class StrategicCommitmentPolicy
    {
        public bool AllowSwitchingOnEmergency { get; set; } = true;

        public bool AreCompatible(StrategicPlanType existing, StrategicPlanType proposed)
        {
            if (existing == proposed) return true;
            // Economic expansion can co-exist with preparation plans
            if (existing == StrategicPlanType.EconomicExpansion && proposed == StrategicPlanType.DefensivePreparation)
                return true;
            if (existing == StrategicPlanType.DefensivePreparation && proposed == StrategicPlanType.EconomicExpansion)
                return true;
            // Attack preparation and Defensive preparation conflict
            if (existing == StrategicPlanType.CavalryPressure && proposed == StrategicPlanType.DefensivePreparation)
                return false;
            if (existing == StrategicPlanType.DefensivePreparation && proposed == StrategicPlanType.CavalryPressure)
                return false;
            return false;
        }

        public bool CanTransition(StrategicPlan activePlan, StrategicPlanType proposed,
            bool isEmergency, out string reason)
        {
            return CanTransition(activePlan, proposed, isEmergency,
                isPlayerOverride: false, out reason);
        }

        public bool CanTransition(StrategicPlan activePlan, StrategicPlanType proposed,
            StrategicIntentSource source, bool isEmergency, bool isPlayerOverride, out string reason)
        {
            if (isPlayerOverride && source == StrategicIntentSource.AIRecommendation)
            {
                reason = "AI recommendations cannot use player override authority.";
                return false;
            }
            if (activePlan != null && !activePlan.IsTerminal
                && !AreCompatible(activePlan.PlanType, proposed)
                && source == StrategicIntentSource.AIConfirmedPlayerCommand
                && activePlan.Authority == StrategicPlanAuthority.PlayerOverride
                && activePlan.Source == StrategicIntentSource.PlayerDirect)
            {
                reason = "Direct player plans have higher priority than confirmed AI commands.";
                return false;
            }
            return CanTransition(activePlan, proposed, isEmergency, isPlayerOverride, out reason);
        }

        public bool CanTransition(StrategicPlan activePlan, StrategicPlanType proposed,
            bool isEmergency, bool isPlayerOverride, out string reason)
        {
            if (activePlan == null || activePlan.IsTerminal)
            {
                reason = "No active plan restricting transition.";
                return true;
            }

            if (isPlayerOverride)
            {
                reason = "Explicit player strategic intent bypassed commitment restrictions.";
                return true;
            }

            if (isEmergency && AllowSwitchingOnEmergency)
            {
                if (activePlan.Authority == StrategicPlanAuthority.PlayerOverride)
                {
                    reason = $"Active plan #{activePlan.StrategicPlanId} was ordered by player override and cannot be superseded by emergency AI decision.";
                    return false;
                }
                reason = "Emergency override permitted plan transition.";
                return true;
            }

            if (AreCompatible(activePlan.PlanType, proposed))
            {
                reason = $"Plan types {activePlan.PlanType} and {proposed} are compatible.";
                return true;
            }

            reason = $"Active plan #{activePlan.StrategicPlanId} ({activePlan.PlanType}) has positive commitment momentum. Conflicting plan {proposed} rejected to prevent strategic oscillation.";
            return false;
        }
    }
}
