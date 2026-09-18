using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private readonly CommanderExplanationService explanationService =
            new CommanderExplanationService();
        private ExplanationContext latestExplanationContext;
        private int? pendingExplanationIntentId;
        private string pendingExplanationObjective;

        public ExplanationResult LatestExplanation { get; private set; }

        private bool TryHandleExplanationQuery(string message)
        {
            string normalized = NormalizeExplanationWholeForm(message);
            CommanderExplanationQuery query;
            switch (normalized)
            {
                case "why are we not attacking":
                    query = CommanderExplanationQuery.AttackReason;
                    break;
                case "why was that rejected":
                    query = CommanderExplanationQuery.LastRejection;
                    break;
                case "explain last decision":
                    query = CommanderExplanationQuery.LastDecision;
                    break;
                case "what is the plan doing":
                    query = CommanderExplanationQuery.CurrentPlan;
                    break;
                default:
                    return false;
            }

            int playerId = Conversation?.PlayerId ?? 0;
            ExplanationContext context = latestExplanationContext
                ?? new ExplanationContext(playerId);
            if (query == CommanderExplanationQuery.CurrentPlan)
                context = CopyCurrentPlanContext(context, playerId);
            LatestExplanation = explanationService.Explain(context, query);
            AppendLine("Player", (message ?? string.Empty).Trim(), false);
            AppendLine("Commander", LatestExplanation.DisplayText, false);
            Conversation?.Memory.RecordExplanation(LatestExplanation.DisplayText);
            return true;
        }

        private ExplanationContext CopyCurrentPlanContext(ExplanationContext source, int playerId)
        {
            int? snapshotTick = null;
            var plans = new List<ExplanationPlanState>();
            if (strategicPipeline != null)
            {
                StrategicContext snapshot = strategicPipeline.CaptureContext();
                snapshotTick = snapshot.SnapshotTick;
                for (int i = 0; i < snapshot.ActivePlans.Count
                    && plans.Count < ExplanationContext.MaximumPlans; i++)
                {
                    StrategicPlanState plan = snapshot.ActivePlans[i];
                    plans.Add(new ExplanationPlanState(plan.StrategicPlanId, plan.PlanType,
                        plan.Status, plan.CurrentMilestone, plan.MilestoneStatus, plan.Reason));
                }
            }
            return new ExplanationContext(playerId, source.DecisionId, source.DecisionTick,
                source.Outcome, source.Reason, source.RequestedObjective,
                source.AcceptedPlanId, source.AcceptedPlanType, snapshotTick, plans);
        }

        private void ProjectStrategicExplanation(StrategicDecisionRecord record)
        {
            if (record == null || Conversation == null) return;
            StrategicIntentSubmission submission = record.Submission;
            ExplanationOutcome outcome;
            if (submission?.CreatedPlan == true)
                outcome = ExplanationOutcome.PlanCreated;
            else if (submission != null)
                outcome = ExplanationOutcome.PlannerRejected;
            else if (record.Decision?.Status == StrategicDecisionStatus.Rejected)
                outcome = ExplanationOutcome.Rejected;
            else if (record.Decision?.Status == StrategicDecisionStatus.NoDecision)
                outcome = ExplanationOutcome.NoDecision;
            else if (record.Decision?.HasSelection == true && !record.TransitionAllowed)
                outcome = ExplanationOutcome.TransitionRefused;
            else if (record.Decision?.HasSelection == true)
                outcome = ExplanationOutcome.SelectionNotSubmitted;
            else
                outcome = ExplanationOutcome.Rejected;

            if (outcome == ExplanationOutcome.NoDecision
                && latestExplanationContext != null
                && latestExplanationContext.Outcome != ExplanationOutcome.NoDecision)
                return;

            StrategicIntent sourceIntent = submission?.Intent ?? record.Decision?.SelectedIntent;
            string objective = string.Empty;
            if (pendingExplanationIntentId.HasValue
                && (sourceIntent == null
                    || sourceIntent.IntentId == pendingExplanationIntentId.Value))
                objective = pendingExplanationObjective ?? string.Empty;
            else if (sourceIntent != null)
                objective = sourceIntent.ObjectiveType.ToString();

            latestExplanationContext = new ExplanationContext(Conversation.PlayerId,
                record.DecisionId, record.Tick, outcome, record.Outcome, objective,
                submission?.CreatedPlan == true ? submission.Plan.StrategicPlanId : (int?)null,
                submission?.CreatedPlan == true ? submission.Plan.PlanType.ToString() : string.Empty);
        }

        private void ResetExplanationState()
        {
            latestExplanationContext = null;
            LatestExplanation = null;
            pendingExplanationIntentId = null;
            pendingExplanationObjective = null;
        }

        private static string NormalizeExplanationWholeForm(string message)
        {
            string text = Regex.Replace((message ?? string.Empty).Trim().ToLowerInvariant(),
                @"\s+", " ");
            if (text.EndsWith("?", StringComparison.Ordinal)
                || text.EndsWith(".", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();
            return text;
        }
    }
}
