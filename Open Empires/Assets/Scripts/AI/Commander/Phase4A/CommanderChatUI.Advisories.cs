using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private readonly StrategicAdvisoryFeed advisoryFeed = new StrategicAdvisoryFeed();
        private GameSimulation advisorySimulation;
        private int lastAdvisoryScanTick = int.MinValue;

        private void ResetAdvisories()
        {
            advisoryFeed.Reset();
            lastAdvisoryScanTick = int.MinValue;
        }

        private void ScanOwnedAdvisories()
        {
            StrategicPipeline source = strategicPipeline;
            StrategicPlanner planner = source?.StrategicPlanner;
            int owner = Conversation?.PlayerId ?? -1;
            if (planner == null || planner.PlayerId != owner) return;
            if (advisorySimulation == null) return;
            int tick = advisorySimulation.CurrentTick;
            if (tick == lastAdvisoryScanTick) return;
            lastAdvisoryScanTick = tick;
            var active = new List<StrategicPlan>(planner.ActivePlans);
            foreach (StrategicPlan plan in active)
                ObserveAdvisoryPlan(source, plan);
        }

        private void ObserveAdvisoryEvent(StrategicPlan plan)
        {
            ObserveAdvisoryPlan(strategicPipeline, plan);
        }

        private void ObserveAdvisoryReservation(StrategicResourceReservation reservation)
        {
            StrategicPlanner planner = strategicPipeline?.StrategicPlanner;
            if (reservation == null || planner == null) return;
            ObserveAdvisoryPlan(strategicPipeline, planner.GetPlan(reservation.PlanId));
        }

        private void ObserveAdvisoryPlan(StrategicPipeline source, StrategicPlan plan)
        {
            int generation = runtimeGeneration;
            int owner = Conversation?.PlayerId ?? -1;
            StrategicPlanner planner = source?.StrategicPlanner;
            if (plan == null || planner == null || !ReferenceEquals(strategicPipeline, source)
                || planner.PlayerId != owner || plan.OwnerPlayerId != owner
                || !ReferenceEquals(planner.GetPlan(plan.StrategicPlanId), plan)) return;
            int id = plan.StrategicPlanId;
            int created = plan.CreatedTick;
            int revision = plan.Revision;
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(owner, id);
            if (health == null || runtimeGeneration != generation
                || !ReferenceEquals(strategicPipeline, source)
                || Conversation == null || Conversation.PlayerId != owner
                || planner.PlayerId != owner || plan.OwnerPlayerId != owner
                || !ReferenceEquals(planner.GetPlan(id), plan)
                || plan.CreatedTick != created || plan.Revision != revision
                || health.PlayerId != owner || health.PlanId != id
                || health.CreatedTick != created || health.Revision != revision) return;
            IReadOnlyList<StrategicAdvisory> observations = advisoryFeed.Observe(health);
            foreach (StrategicAdvisory advisory in observations)
            {
                if (runtimeGeneration != generation || !ReferenceEquals(strategicPipeline, source)
                    || Conversation == null || Conversation.PlayerId != owner) return;
                AppendLine("Commander", advisory.Display, false);
            }
        }
    }
}
