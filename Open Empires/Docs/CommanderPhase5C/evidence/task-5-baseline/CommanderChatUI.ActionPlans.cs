using System;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private CommanderActionPlanCandidate pendingActionPlan;
        public CommanderActionPlanCandidate PendingActionPlan => pendingActionPlan;

        private bool HandleActionPlanControl(string message)
        {
            string control = NormalizeWholeForm(message);
            bool approve = control == "approve plan" || control == "confirm plan"
                || pendingActionPlan != null && control == "yes";
            bool cancel = control == "cancel plan" || control == "dismiss plan"
                || pendingActionPlan != null && (control == "no" || control == "cancel" || control == "never mind");
            if (!approve && !cancel) return false;
            if (pendingActionPlan == null)
            {
                if (cancel && semanticGoalManager != null)
                {
                    var active = semanticGoalManager.SingleLiveConfirmedActionPlan(out bool ambiguous);
                    if (ambiguous)
                    {
                        AppendLine("Commander", "Multiple confirmed action plans are active; select a specific plan before cancelling.");
                        return true;
                    }
                    if (active != null && semanticGoalManager.CancelActionPlan(active))
                    {
                        AppendLine("Commander", "Action plan cancelled and reservations released. Already completed or issued gameplay is not rolled back.");
                        return true;
                    }
                }
                AppendLine("Commander", "No action plan is awaiting approval.");
                return true;
            }
            if (cancel)
            {
                ClearActionPlanPreview();
                AppendLine("Commander", "Action plan dismissed; nothing started.");
                return true;
            }
            var candidate = pendingActionPlan;
            pendingActionPlan = null;
            UpdateStrategicControls();
            try
            {
                var graph = semanticGoalManager?.ApproveActionPlan(candidate, runtimeGeneration, strategicPipeline);
                if (graph == null)
                {
                    candidate.Cancel();
                    AppendLine("Commander", "Action plan is stale or changed; nothing started. Request a fresh preview.");
                    return true;
                }
                var submission = semanticDispatcher.SubmitSemanticGraph(graph);
                if (submission?.CreatedGoal == true) RecordAcceptedSemanticNodes(candidate.Interpretation.Nodes);
                TrackCurrentRequest(submission);
                AppendLine("Commander", submission?.Response ?? "Action plan could not be admitted safely.");
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                candidate.Cancel();
                AppendLine("Commander", "Action plan admission did not finish safely. Check its current goal status before retrying.");
            }
            return true;
        }

        private void ClearActionPlanPreview()
        {
            pendingActionPlan?.Cancel();
            pendingActionPlan = null;
            UpdateStrategicControls();
        }
    }
}
