using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenEmpires
{
    public sealed class CommanderExplanationService
    {
        public ExplanationResult Explain(ExplanationContext context,
            CommanderExplanationQuery query = CommanderExplanationQuery.LastDecision)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(CommanderExplanationQuery), query))
                throw new ArgumentOutOfRangeException(nameof(query));
            string text;
            switch (query)
            {
                case CommanderExplanationQuery.LastDecision:
                    text = RenderDecision(context);
                    break;
                case CommanderExplanationQuery.LastRejection:
                    text = IsRejection(context.Outcome)
                        ? RenderDecision(context)
                        : "The latest recorded outcome was " + context.Outcome
                            + ", not a rejection.";
                    break;
                case CommanderExplanationQuery.AttackReason:
                    text = IsRejection(context.Outcome)
                        && string.Equals(context.RequestedObjective, "AttackPreparation",
                            StringComparison.Ordinal)
                        ? RenderDecision(context)
                        : "No recorded rejection is attributable to AttackPreparation. "
                            + "The latest recorded outcome was " + context.Outcome + ".";
                    break;
                case CommanderExplanationQuery.CurrentPlan:
                    text = RenderCurrentPlans(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(query));
            }
            return new ExplanationResult(text, context.Outcome);
        }

        private static bool IsRejection(ExplanationOutcome outcome)
        {
            return outcome == ExplanationOutcome.Rejected
                || outcome == ExplanationOutcome.TransitionRefused
                || outcome == ExplanationOutcome.PlannerRejected;
        }

        private static string RenderDecision(ExplanationContext context)
        {
            if (context.Outcome == ExplanationOutcome.NoDecision)
                return "No recorded decision is available.";

            var result = new StringBuilder();
            switch (context.Outcome)
            {
                case ExplanationOutcome.Rejected:
                    result.Append("The recorded decision was rejected.");
                    break;
                case ExplanationOutcome.TransitionRefused:
                    result.Append("The selected intent was blocked before submission.");
                    break;
                case ExplanationOutcome.PlannerRejected:
                    result.Append("The planner rejected the submitted intent. No accepted plan was created.");
                    break;
                case ExplanationOutcome.PlanCreated:
                    if (context.AcceptedPlanId.HasValue)
                    {
                        result.Append("The decision created plan #")
                            .Append(context.AcceptedPlanId.Value.ToString(CultureInfo.InvariantCulture));
                        if (context.AcceptedPlanType.Length > 0)
                            result.Append(" (").Append(context.AcceptedPlanType).Append(')');
                        result.Append('.');
                    }
                    else result.Append("The decision created an accepted plan.");
                    break;
                case ExplanationOutcome.SelectionNotSubmitted:
                    result.Append("An intent was selected but was not submitted.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context.Outcome));
            }

            if (context.DecisionId.HasValue)
                result.Append(" Decision ID: ")
                    .Append(context.DecisionId.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.DecisionTick.HasValue)
                result.Append(" Historical decision tick: ")
                    .Append(context.DecisionTick.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.RequestedObjective.Length > 0)
                result.Append(" Requested objective: ").Append(context.RequestedObjective).Append('.');
            result.Append(context.Reason.Length > 0
                ? " Recorded reason: " + context.Reason
                : " No recorded reason is available.");
            return result.ToString();
        }

        private static string RenderCurrentPlans(ExplanationContext context)
        {
            if (!context.CurrentSnapshotTick.HasValue)
                return "Current plan state is unavailable.";
            string tick = context.CurrentSnapshotTick.Value.ToString(CultureInfo.InvariantCulture);
            if (context.CurrentPlans.Count == 0)
                return "No active plan was observed at current snapshot tick " + tick
                    + ". This does not assert that any historical plan completed.";

            var plans = new List<ExplanationPlanState>(context.CurrentPlans);
            plans.Sort((left, right) => left.PlanId.CompareTo(right.PlanId));
            var result = new StringBuilder("Current snapshot tick ").Append(tick)
                .Append(" contains observed plan progress.");
            if (plans.Count == ExplanationContext.MaximumPlans)
                result.Append(" This view is showing up to 32 plans.");
            for (int i = 0; i < plans.Count; i++)
            {
                ExplanationPlanState plan = plans[i];
                result.Append(" Plan #")
                    .Append(plan.PlanId.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(ValueOrUnavailable(plan.PlanType))
                    .Append("; observed status: ").Append(ValueOrUnavailable(plan.Status))
                    .Append("; observed current milestone: ")
                    .Append(ValueOrUnavailable(plan.CurrentMilestone))
                    .Append("; observed milestone status: ")
                    .Append(ValueOrUnavailable(plan.MilestoneStatus))
                    .Append("; observed reason: ").Append(ValueOrUnavailable(plan.Reason)).Append('.');
            }
            return result.ToString();
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrEmpty(value) ? "unavailable" : value;
        }
    }
}
