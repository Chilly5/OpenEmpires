using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private TMP_Text currentPlanStatusText;
        private TMP_Text cancelPlanLabel;
        private Button selectPlanButton;
        private Button pausePlanButton;
        private Button resumePlanButton;
        private Button cancelPlanButton;
        private int selectedPlanId;
        private int displayedPlanRevision = -1;
        private string selectedPlanStatusPrefix;
        private bool cancelArmed;
        private StrategicPlanControlRequest armedCancel;
        private StrategicPipeline armedPipeline;
        private int armedGeneration;
        private Action displayedPauseCallback;
        private Action displayedResumeCallback;
        private Action displayedCancelCallback;
        private Action displayedSelectCallback;

        private void BuildStrategicHostControls(Transform panel)
        {
            currentPlanStatusText = Text("Current plan status", panel, "No active strategy.", 11,
                TextAlignmentOptions.Left);
            SetRect(currentPlanStatusText.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(10, 116), new Vector2(420, 140));
            selectPlanButton = StrategyButton(panel, "Select plan", 10, 95,
                () => displayedSelectCallback?.Invoke());
            pausePlanButton = StrategyButton(panel, "Pause", 110, 78,
                () => displayedPauseCallback?.Invoke());
            resumePlanButton = StrategyButton(panel, "Resume", 193, 78,
                () => displayedResumeCallback?.Invoke());
            cancelPlanButton = StrategyButton(panel, "Cancel", 276, 144,
                () => displayedCancelCallback?.Invoke());
            MoveHostButton(selectPlanButton, 83, 111);
            MoveHostButton(pausePlanButton, 83, 111);
            MoveHostButton(resumePlanButton, 83, 111);
            MoveHostButton(cancelPlanButton, 83, 111);
            cancelPlanLabel = cancelPlanButton.GetComponentInChildren<TMP_Text>();
        }

        private static void MoveHostButton(Button button, float bottom, float top)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 min = rect.offsetMin;
            Vector2 max = rect.offsetMax;
            rect.offsetMin = new Vector2(min.x, bottom);
            rect.offsetMax = new Vector2(max.x, top);
        }

        private static string NormalizeLifecycleForm(string message)
        {
            string text = Regex.Replace((message ?? string.Empty).Trim().ToLowerInvariant(),
                @"\s+", " ");
            if (text.EndsWith(".", StringComparison.Ordinal)
                || text.EndsWith("?", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();
            return text;
        }

        private bool TryHandleStrategicLifecycle(string message)
        {
            StrategicPlanControlType action;
            switch (NormalizeLifecycleForm(message))
            {
                case "pause strategy":
                case "pause current strategy": action = StrategicPlanControlType.Pause; break;
                case "resume strategy":
                case "resume current strategy": action = StrategicPlanControlType.Resume; break;
                case "cancel strategy":
                case "cancel current strategy": action = StrategicPlanControlType.Cancel; break;
                case "strategy status":
                case "current strategy status": action = StrategicPlanControlType.Status; break;
                default: return false;
            }

            InvalidateLifecycleInterpretation();
            AppendLine("Player", message, false);
            if (strategicPipeline == null || Conversation == null)
            {
                AppendLine("Commander", "Strategic Commander is not ready.", false);
                return true;
            }
            StrategicPlanner planner = strategicPipeline.StrategicPlanner;
            if (planner.PlayerId != Conversation.PlayerId)
            {
                AppendLine("Commander", "Strategy control is unavailable to this player.", false);
                return true;
            }
            if (action == StrategicPlanControlType.Status)
            {
                AppendLine("Commander", CurrentPlansStatus(strategicPipeline, Conversation.PlayerId), false);
                UpdateStrategicHostControls();
                return true;
            }

            int ownedCount = 0;
            foreach (StrategicPlan plan in planner.ActivePlans)
                if (!plan.IsTerminal && plan.OwnerPlayerId == Conversation.PlayerId) ownedCount++;
            if (ownedCount != 1)
            {
                AppendLine("Commander", ownedCount == 0
                    ? "No active strategy."
                    : "Multiple active strategies; select a plan in the Commander panel.", false);
                return true;
            }
            if (!planner.CaptureCurrentControlRequest(Conversation.PlayerId, action,
                out StrategicPlanControlRequest request))
            {
                AppendLine("Commander", "Strategy control is unavailable.", false);
                return true;
            }
            ApplyHostControl(request, strategicPipeline, runtimeGeneration);
            return true;
        }

        private static string CurrentPlansStatus(StrategicPipeline pipeline, int playerId)
        {
            var lines = new List<string>();
            StrategicContext snapshot = pipeline.CaptureContext();
            if (snapshot.PlayerId != playerId) return "Strategy control is unavailable to this player.";
            foreach (StrategicPlanState plan in snapshot.ActivePlans)
            {
                lines.Add("Strategy #" + plan.StrategicPlanId + ": " + plan.Status
                    + ", milestone " + (string.IsNullOrEmpty(plan.CurrentMilestone)
                        ? "none" : plan.CurrentMilestone) + ".");
                if (lines.Count == StrategicPlanner.MaxActivePlans) break;
            }
            return lines.Count == 0 ? "No active strategy." : string.Join(" ", lines);
        }

        private void ApplyHostControl(StrategicPlanControlRequest request,
            StrategicPipeline capturedPipeline, int capturedGeneration)
        {
            if (!ReferenceEquals(strategicPipeline, capturedPipeline)
                || runtimeGeneration != capturedGeneration || Conversation == null
                || Conversation.PlayerId != capturedPipeline.StrategicPlanner.PlayerId)
                return;
            StrategicPlanControlResult result = capturedPipeline.StrategicPlanner.ApplyControl(request);
            AppendLine("Commander", result.Status == StrategicPlanControlStatus.Applied
                ? "Strategy #" + result.PlanId + ": " + result.Message
                : "Strategy control " + result.Status + ": " + result.Message, false);
            if (result.Status == StrategicPlanControlStatus.Applied
                && request.ControlType != StrategicPlanControlType.Status)
                InvalidateLifecycleInterpretation();
            UpdateStrategicHostControls();
        }

        private void InvalidateLifecycleInterpretation()
        {
            runtimeGeneration++;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new System.Threading.CancellationTokenSource();
            strategicBridge?.ClearPending();
            ClearStrategicPreview();
            LatestStrategicInterpretation = null;
            submitting = false;
            if (inputField != null) inputField.interactable = adapter != null;
            if (sendButton != null) sendButton.interactable = adapter != null;
            UpdateStrategicControls();
        }

        private void OnHostPlanStatusChanged(StrategicPlan plan)
        {
            ObserveAdvisoryEvent(plan);
            UpdateStrategicHostControls();
        }
        private void OnHostMilestoneStatusChanged(StrategicPlan plan, StrategicMilestone _)
        {
            ObserveAdvisoryEvent(plan);
            UpdateStrategicHostControls();
        }
        private void OnHostChildGoalEvent(StrategicPlan plan, CommanderGoalEvent _)
        {
            ObserveAdvisoryEvent(plan);
            UpdateStrategicHostControls();
        }
        private void OnHostReservationCreated(StrategicResourceReservation reservation)
        {
            ObserveAdvisoryReservation(reservation);
            UpdateStrategicHostControls();
        }
        private void OnHostReservationReleased(StrategicResourceReservation reservation)
        {
            ObserveAdvisoryReservation(reservation);
            UpdateStrategicHostControls();
        }

        private void LateUpdate()
        {
            UpdatePresentation();
            RefreshPanelSize();
            ScanOwnedAdvisories();
            StrategicPlanner planner = strategicPipeline?.StrategicPlanner;
            if (planner == null || currentPlanStatusText == null || selectedPlanId == 0) return;
            StrategicPlan plan = planner.GetPlan(selectedPlanId);
            if (plan == null || plan.IsTerminal || plan.Revision != displayedPlanRevision)
                UpdateStrategicHostControls();
            else
                RefreshSelectedPlanHealth(plan);
        }

        private void UpdateStrategicHostControls()
        {
            if (currentPlanStatusText == null) return;
            displayedPauseCallback = displayedResumeCallback = displayedCancelCallback = null;
            displayedSelectCallback = null;
            StrategicPlanner planner = strategicPipeline?.StrategicPlanner;
            if (planner == null || Conversation == null || planner.PlayerId != Conversation.PlayerId)
            {
                DisarmCancel();
                selectedPlanStatusPrefix = null;
                currentPlanStatusText.text = "No active strategy.";
                SetHostButtons(false, false, false, false);
                return;
            }
            var plans = new List<StrategicPlan>();
            foreach (StrategicPlan plan in planner.ActivePlans)
                if (!plan.IsTerminal && plan.OwnerPlayerId == Conversation.PlayerId)
                    plans.Add(plan);
            if (plans.Count == 0)
            {
                selectedPlanId = 0;
                DisarmCancel();
                selectedPlanStatusPrefix = null;
                currentPlanStatusText.text = "No active strategy.";
                SetHostButtons(false, false, false, false);
                return;
            }
            int index = plans.FindIndex(plan => plan.StrategicPlanId == selectedPlanId);
            if (index < 0)
            {
                index = 0;
                selectedPlanId = plans[0].StrategicPlanId;
                DisarmCancel();
            }
            StrategicPlan selected = plans[index];
            displayedPlanRevision = selected.Revision;
            selectedPlanStatusPrefix = "Strategy #" + selected.StrategicPlanId + " ("
                + (index + 1) + "/" + plans.Count + "): " + selected.Status
                + ", " + (selected.CurrentMilestone?.Name ?? "none") + ".";
            RefreshSelectedPlanHealth(selected);
            StrategicPipeline capturedPipeline = strategicPipeline;
            int capturedGeneration = runtimeGeneration;
            int capturedPlanId = selected.StrategicPlanId;
            if (plans.Count > 1)
            {
                int nextPlanId = plans[(index + 1) % plans.Count].StrategicPlanId;
                displayedSelectCallback = () =>
                {
                    if (!ReferenceEquals(strategicPipeline, capturedPipeline)
                        || runtimeGeneration != capturedGeneration || selectedPlanId != capturedPlanId)
                        return;
                    selectedPlanId = nextPlanId;
                    DisarmCancel();
                    UpdateStrategicHostControls();
                };
            }
            bool hasPause = planner.CaptureControlRequest(Conversation.PlayerId,
                selected.StrategicPlanId, StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause);
            bool hasResume = planner.CaptureControlRequest(Conversation.PlayerId,
                selected.StrategicPlanId, StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume);
            bool hasCancel = planner.CaptureControlRequest(Conversation.PlayerId,
                selected.StrategicPlanId, StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel);
            if (cancelArmed && (!ReferenceEquals(armedPipeline, capturedPipeline)
                || armedGeneration != capturedGeneration || !SameToken(armedCancel, cancel)))
                DisarmCancel();
            if (hasPause && selected.Status != StrategicPlanStatus.Paused)
                displayedPauseCallback = () => ApplyHostControl(pause, capturedPipeline, capturedGeneration);
            if (hasResume && selected.Status == StrategicPlanStatus.Paused)
                displayedResumeCallback = () => ApplyHostControl(resume, capturedPipeline, capturedGeneration);
            if (hasCancel)
                displayedCancelCallback = () => ArmOrApplyCancel(cancel, capturedPipeline, capturedGeneration);
            if (cancelPlanLabel != null)
                cancelPlanLabel.text = cancelArmed ? "Confirm cancel #" + cancel.PlanId : "Cancel";
            SetHostButtons(plans.Count > 1, displayedPauseCallback != null,
                displayedResumeCallback != null, displayedCancelCallback != null);
        }

        private void ArmOrApplyCancel(StrategicPlanControlRequest request,
            StrategicPipeline capturedPipeline, int capturedGeneration)
        {
            if (!ReferenceEquals(strategicPipeline, capturedPipeline)
                || runtimeGeneration != capturedGeneration || Conversation == null
                || Conversation.PlayerId != capturedPipeline.StrategicPlanner.PlayerId)
                return;
            if (!cancelArmed || !ReferenceEquals(armedPipeline, capturedPipeline)
                || armedGeneration != capturedGeneration || !SameToken(armedCancel, request))
            {
                DisarmCancel();
                if (!capturedPipeline.StrategicPlanner.CaptureControlRequest(Conversation.PlayerId,
                    request.PlanId, StrategicPlanControlType.Cancel, out StrategicPlanControlRequest current)
                    || !SameToken(current, request))
                {
                    UpdateStrategicHostControls();
                    return;
                }
                cancelArmed = true;
                armedCancel = request;
                armedPipeline = capturedPipeline;
                armedGeneration = capturedGeneration;
                UpdateStrategicHostControls();
                return;
            }
            DisarmCancel();
            ApplyHostControl(request, capturedPipeline, capturedGeneration);
        }

        private static bool SameToken(StrategicPlanControlRequest a, StrategicPlanControlRequest b) =>
            a.PlayerId == b.PlayerId && a.PlanId == b.PlanId && a.CreatedTick == b.CreatedTick
            && a.ObservedRevision == b.ObservedRevision && a.ControlType == b.ControlType;

        private void DisarmCancel()
        {
            cancelArmed = false;
            armedCancel = default;
            armedPipeline = null;
            armedGeneration = 0;
            if (cancelPlanLabel != null) cancelPlanLabel.text = "Cancel";
        }

        private void ResetStrategicHostControls()
        {
            selectedPlanId = 0;
            displayedPlanRevision = -1;
            selectedPlanStatusPrefix = null;
            displayedPauseCallback = displayedResumeCallback = displayedCancelCallback = null;
            displayedSelectCallback = null;
            DisarmCancel();
            if (currentPlanStatusText != null) currentPlanStatusText.text = "No active strategy.";
            SetHostButtons(false, false, false, false);
        }

        private StrategicPlanHealthSnapshot CaptureCurrentSelectedHealth(StrategicPlan selected)
        {
            StrategicPipeline source = strategicPipeline;
            int generation = runtimeGeneration;
            int owner = Conversation?.PlayerId ?? -1;
            StrategicPlanner planner = source?.StrategicPlanner;
            if (selected == null || selected.IsTerminal || planner == null
                || planner.PlayerId != owner || selected.OwnerPlayerId != owner
                || selectedPlanId != selected.StrategicPlanId
                || !ReferenceEquals(planner.GetPlan(selected.StrategicPlanId), selected)) return null;
            int id = selected.StrategicPlanId;
            int created = selected.CreatedTick;
            int revision = selected.Revision;
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(owner, id);
            if (health == null || !ReferenceEquals(strategicPipeline, source)
                || runtimeGeneration != generation || Conversation == null
                || Conversation.PlayerId != owner || selectedPlanId != id
                || selected.IsTerminal || selected.Revision != revision
                || !ReferenceEquals(planner.GetPlan(id), selected)
                || health.PlayerId != owner || health.PlanId != id
                || health.CreatedTick != created || health.Revision != revision
                || !Enum.IsDefined(typeof(StrategicPlanHealthCategory),
                    health.PrimaryHealthCategory)) return null;
            return health;
        }

        private void RefreshSelectedPlanHealth(StrategicPlan selected)
        {
            if (currentPlanStatusText == null) return;
            StrategicPlanHealthSnapshot health = CaptureCurrentSelectedHealth(selected);
            if (health == null)
            {
                currentPlanStatusText.text = "Health evidence unavailable.";
                return;
            }
            string summary = HealthCategoryText(health.PrimaryHealthCategory);
            int wood = 0;
            bool hasWood = false;
            foreach (StrategicPlanHealthResource resource in health.Resources)
                if (resource.ResourceType == ResourceType.Wood)
                {
                    wood = resource.Owned;
                    hasWood = true;
                    break;
                }
            currentPlanStatusText.text = selectedPlanStatusPrefix + "\nHealth: " + summary
                + (hasWood ? "; wood " + wood : string.Empty)
                + "; population " + health.Population + "/" + health.PopulationCap
                + "; queued " + health.AllQueuedUnits + ".";
        }

        private bool TryHandlePlanHealthQuery(string message, string normalized)
        {
            bool asksPause = normalized == "why is the strategy paused";
            bool asksWait = normalized == "why is the plan waiting";
            bool asksBlock = normalized == "what is blocking the current strategy";
            bool asksRecovery = normalized == "did the strategy recover";
            bool asksStop = normalized == "why did the plan stop";
            Match terminalForm = Regex.Match(normalized, @"^why did plan #([1-9][0-9]*) stop$",
                RegexOptions.CultureInvariant);
            bool asksExplicitStop = terminalForm.Success;
            if (!asksPause && !asksWait && !asksBlock && !asksRecovery && !asksStop
                && !asksExplicitStop)
                return false;
            AppendLine("Player", (message ?? string.Empty).Trim(), false);
            StrategicPlanHealthSnapshot health = null;
            StrategicPlanner planner = strategicPipeline?.StrategicPlanner;
            if (planner != null && Conversation != null && planner.PlayerId == Conversation.PlayerId
                && selectedPlanId > 0)
                health = CaptureCurrentSelectedHealth(planner.GetPlan(selectedPlanId));
            string answer = "Health evidence unavailable for the current strategy.";
            if (asksExplicitStop
                && int.TryParse(terminalForm.Groups[1].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int terminalId))
            {
                StrategicPlanHealthSnapshot terminal = CaptureExplicitTerminalHealth(terminalId);
                if (terminal != null)
                    answer = "Strategy #" + terminal.PlanId + ": "
                        + HealthCategoryText(terminal.PrimaryHealthCategory)
                        + ". Past blocker cause is not retained in this health snapshot.";
            }
            else if (health != null && !asksRecovery && !asksStop)
            {
                bool matching = asksPause
                    ? health.PrimaryHealthCategory == StrategicPlanHealthCategory.Paused
                    : asksWait
                        ? IsWaitingHealth(health.PrimaryHealthCategory)
                        : IsWaitingHealth(health.PrimaryHealthCategory)
                            || health.PrimaryHealthCategory == StrategicPlanHealthCategory.TemporarilyBlocked;
                if (!matching && !asksPause)
                    foreach (StrategicPlanHealthCategory secondary in health.SecondaryHealthCategories)
                        if (IsWaitingHealth(secondary)
                            || secondary == StrategicPlanHealthCategory.TemporarilyBlocked)
                        {
                            matching = true;
                            break;
                        }
                if (matching) answer = FormatHealthAnswer(health);
            }
            AppendLine("Commander", answer, false);
            Conversation?.Memory.RecordExplanation(answer);
            return true;
        }

        private StrategicPlanHealthSnapshot CaptureExplicitTerminalHealth(int id)
        {
            StrategicPipeline source = strategicPipeline;
            int generation = runtimeGeneration;
            int owner = Conversation?.PlayerId ?? -1;
            StrategicPlanner planner = source?.StrategicPlanner;
            if (planner == null || planner.PlayerId != owner) return null;
            StrategicPlan plan = planner.GetPlan(id);
            if (plan == null || !plan.IsTerminal || plan.OwnerPlayerId != owner) return null;
            int created = plan.CreatedTick;
            int revision = plan.Revision;
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(owner, id);
            if (health == null || !ReferenceEquals(strategicPipeline, source)
                || runtimeGeneration != generation || Conversation == null
                || Conversation.PlayerId != owner || !ReferenceEquals(planner.GetPlan(id), plan)
                || !plan.IsTerminal || plan.CreatedTick != created || plan.Revision != revision
                || health.PlayerId != owner || health.PlanId != id
                || health.CreatedTick != created || health.Revision != revision) return null;
            switch (plan.Status)
            {
                case StrategicPlanStatus.Completed:
                    return health.PrimaryHealthCategory == StrategicPlanHealthCategory.Completed ? health : null;
                case StrategicPlanStatus.Cancelled:
                    return health.PrimaryHealthCategory == StrategicPlanHealthCategory.Cancelled ? health : null;
                case StrategicPlanStatus.Failed:
                    return health.PrimaryHealthCategory == StrategicPlanHealthCategory.Failed ? health : null;
                default: return null;
            }
        }

        private static bool IsWaitingHealth(StrategicPlanHealthCategory category) =>
            category == StrategicPlanHealthCategory.WaitingForResources
            || category == StrategicPlanHealthCategory.WaitingForPopulation
            || category == StrategicPlanHealthCategory.WaitingForPrerequisite
            || category == StrategicPlanHealthCategory.WaitingForProduction
            || category == StrategicPlanHealthCategory.WaitingForConstruction;

        private static string HealthCategoryText(StrategicPlanHealthCategory category)
        {
            switch (category)
            {
                case StrategicPlanHealthCategory.Healthy: return "Active progress";
                case StrategicPlanHealthCategory.Paused: return "Paused";
                case StrategicPlanHealthCategory.WaitingForResources: return "Waiting for resources";
                case StrategicPlanHealthCategory.WaitingForPopulation: return "Waiting for population capacity";
                case StrategicPlanHealthCategory.WaitingForPrerequisite: return "Waiting for a prerequisite";
                case StrategicPlanHealthCategory.WaitingForProduction: return "Waiting for production";
                case StrategicPlanHealthCategory.WaitingForConstruction: return "Waiting for construction";
                case StrategicPlanHealthCategory.TemporarilyBlocked: return "Temporarily blocked";
                case StrategicPlanHealthCategory.Completed: return "Completed";
                case StrategicPlanHealthCategory.Cancelled: return "Cancelled";
                case StrategicPlanHealthCategory.Failed: return "Failed";
                default: return "Unknown";
            }
        }

        private static string FormatHealthAnswer(StrategicPlanHealthSnapshot health)
        {
            var answer = new StringBuilder(256);
            answer.Append("Strategy #").Append(health.PlanId.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .Append(HealthCategoryText(health.PrimaryHealthCategory)).Append('.');
            if (health.PrimaryHealthCategory == StrategicPlanHealthCategory.Unknown
                || !Enum.IsDefined(typeof(StrategicPlanHealthCategory), health.PrimaryHealthCategory))
                return answer.ToString();
            foreach (StrategicPlanHealthCategory category in health.SecondaryHealthCategories)
            {
                answer.Append(" Also: ").Append(HealthCategoryText(category)).Append('.');
            }
            if (health.PrimaryHealthCategory == StrategicPlanHealthCategory.WaitingForPopulation
                || health.SecondaryHealthCategories.Contains(StrategicPlanHealthCategory.WaitingForPopulation))
                answer.Append(" Population ")
                    .Append(health.Population.ToString(CultureInfo.InvariantCulture)).Append('/')
                    .Append(health.PopulationCap.ToString(CultureInfo.InvariantCulture))
                    .Append("; queued ")
                    .Append(health.AllQueuedUnits.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (health.PrimaryHealthCategory == StrategicPlanHealthCategory.WaitingForResources
                || health.SecondaryHealthCategories.Contains(StrategicPlanHealthCategory.WaitingForResources))
            {
                foreach (StrategicPlanHealthResource resource in health.Resources)
                {
                    if (!resource.MilestoneRequirementKnown || resource.MilestoneDeficit <= 0) continue;
                    answer.Append(' ').Append(resource.ResourceType).Append(" owned ")
                        .Append(resource.Owned.ToString(CultureInfo.InvariantCulture))
                        .Append(", milestone deficit ")
                        .Append(resource.MilestoneDeficit.ToString(CultureInfo.InvariantCulture)).Append('.');
                }
            }
            if (answer.Length > 512) answer.Length = 512;
            return answer.ToString();
        }

        private void SetHostButtons(bool select, bool pause, bool resume, bool cancel)
        {
            if (strategicHostRow != null) strategicHostRow.SetActive(select || pause || resume || cancel);
            SetVisibleHostButton(selectPlanButton, select);
            SetVisibleHostButton(pausePlanButton, pause);
            SetVisibleHostButton(resumePlanButton, resume);
            SetVisibleHostButton(cancelPlanButton, cancel);
        }

        private void SetVisibleHostButton(Button button, bool relevant)
        {
            if (button == null) return;
            button.gameObject.SetActive(relevant);
            button.interactable = relevant && !submitting;
        }
    }
}
