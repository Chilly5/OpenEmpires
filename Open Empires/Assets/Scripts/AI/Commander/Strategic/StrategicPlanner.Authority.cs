namespace OpenEmpires
{
    public sealed partial class StrategicPlanner
    {
        // Controllers are created from actual simulation setup. Remote humans are not
        // computer controllers; neither local ownership nor player0 grants autonomy.
        internal bool HasComputerOwner => PlayerId >= 0
            && PlayerId < goalManager.Simulation.PlayerCount
            && goalManager.Simulation.GetAiPlayer(PlayerId) != null;

        internal bool CanCommitIntent(StrategicIntent intent, out string reason)
        {
            if (intent != null && intent.PlayerId == PlayerId
                && ReferenceEquals(intent.AuthorizationOwner, IntentIds)
                && IntentIds.Owns(intent) && !string.IsNullOrEmpty(intent.AuthorizationEvidence))
            {
                reason = intent.AuthorizationEvidence;
                return true;
            }
            if (intent != null && intent.PlayerId == PlayerId && HasComputerOwner
                && intent.Source == StrategicIntentSource.AIRecommendation)
            {
                reason = "Explicitly computer-controlled simulation owner.";
                return true;
            }
            reason = "Suggested strategy — not started. A trusted player request or explicit approval is required.";
            return false;
        }

        private bool HasLiveRootAuthority(StrategicPlan plan)
        {
            return plan != null && plan.OwnerPlayerId == PlayerId
                && intentsByPlanId.TryGetValue(plan.StrategicPlanId, out StrategicIntent root)
                && root.IntentId == plan.SourceIntentId && CanCommitIntent(root, out _);
        }
    }
}
