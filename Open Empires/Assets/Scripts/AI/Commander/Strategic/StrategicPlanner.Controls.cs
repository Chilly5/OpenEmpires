using System;

namespace OpenEmpires
{
    public sealed partial class StrategicPlanner
    {
        public bool CaptureControlRequest(int trustedPlayerId, int planId,
            StrategicPlanControlType action, out StrategicPlanControlRequest request)
        {
            request = default;
            if (disposed || trustedPlayerId != PlayerId || !Enum.IsDefined(typeof(StrategicPlanControlType), action))
                return false;
            StrategicPlan plan = GetPlan(planId);
            if (plan == null || plan.IsTerminal || plan.OwnerPlayerId != trustedPlayerId)
                return false;
            request = new StrategicPlanControlRequest(trustedPlayerId, plan.StrategicPlanId,
                plan.CreatedTick, plan.Revision, action);
            return true;
        }

        public bool CaptureCurrentControlRequest(int trustedPlayerId,
            StrategicPlanControlType action, out StrategicPlanControlRequest request)
        {
            request = default;
            if (disposed || trustedPlayerId != PlayerId || !Enum.IsDefined(typeof(StrategicPlanControlType), action))
                return false;
            StrategicPlan selected = null;
            for (int i = 0; i < activePlans.Count; i++)
            {
                StrategicPlan plan = activePlans[i];
                if (plan.IsTerminal || plan.OwnerPlayerId != trustedPlayerId) continue;
                if (selected != null) return false;
                selected = plan;
            }
            return selected != null && CaptureControlRequest(trustedPlayerId,
                selected.StrategicPlanId, action, out request);
        }

        public StrategicPlanControlResult ApplyControl(StrategicPlanControlRequest request)
        {
            if (disposed) return Result(StrategicPlanControlStatus.StalePlan, request, null,
                "The plan runtime is no longer active.");
            if (!Enum.IsDefined(typeof(StrategicPlanControlType), request.ControlType))
                return Result(StrategicPlanControlStatus.Rejected, request, null,
                    "Unknown plan control action.");
            if (request.PlayerId != PlayerId)
                return Result(StrategicPlanControlStatus.Unauthorized, request, null,
                    "Plan owner does not match the Commander host.");
            StrategicPlan plan = GetPlan(request.PlanId);
            if (plan == null || plan.OwnerPlayerId != request.PlayerId
                || plan.CreatedTick != request.CreatedTick || plan.Revision != request.ObservedRevision)
                return Result(StrategicPlanControlStatus.StalePlan, request, null,
                    "The captured plan state has changed.");
            if (plan.Status == StrategicPlanStatus.Completed || plan.Status == StrategicPlanStatus.Failed)
                return Result(StrategicPlanControlStatus.AlreadyCompleted, request, plan,
                    "The plan has already ended.");
            if (plan.Status == StrategicPlanStatus.Cancelled)
                return Result(StrategicPlanControlStatus.AlreadyCancelled, request, plan,
                    "The plan has already been cancelled.");

            switch (request.ControlType)
            {
                case StrategicPlanControlType.Status:
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "Plan status observed.");
                case StrategicPlanControlType.Pause:
                    if (plan.Status == StrategicPlanStatus.Paused)
                        return Result(StrategicPlanControlStatus.AlreadyPaused, request, plan,
                            "The plan is already paused.");
                    if (plan.Revision == int.MaxValue)
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "Plan revision exhausted.");
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        goalManager.SuspendGoal(plan.ChildGoalIds[i]);
                    plan.PausedAtTick = goalManager.CurrentTick;
                    plan.Status = StrategicPlanStatus.Paused;
                    plan.AdvanceRevision();
                    PublishPlanStatus(plan);
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "Plan paused; already-dispatched actions may finish.");
                case StrategicPlanControlType.Resume:
                    if (plan.Status != StrategicPlanStatus.Paused)
                        return Result(StrategicPlanControlStatus.AlreadyRunning, request, plan,
                            "The plan is already running.");
                    long elapsed = (long)goalManager.CurrentTick - plan.PausedAtTick;
                    if (elapsed < 0 || elapsed > int.MaxValue || !CanReconcileOnResume(plan))
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "The pause interval cannot be resumed safely.");
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        if (!goalManager.CanResumeGoal(plan.ChildGoalIds[i], (int)elapsed))
                            return Result(StrategicPlanControlStatus.Rejected, request, plan,
                                "A goal tick anchor cannot be resumed safely.");
                    if (!TryEnterRevisionCascade(plan, RemainingCascadeRevisionBudget(plan),
                        out bool ownsResumeCascade))
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "Plan revision exhausted.");
                    try
                    {
                    for (int i = 0; i < plan.ChildGoalIds.Count; i++)
                        goalManager.ResumeGoal(plan.ChildGoalIds[i], (int)elapsed);
                    plan.PausedAtTick = -1;
                    plan.Status = StrategicPlanStatus.Active;
                    plan.AdvanceRevision();
                    PublishPlanStatus(plan);
                    ReconcileDeferredTerminalGoals(plan);
                    if (plan.CurrentMilestone != null
                        && plan.CurrentMilestone.Status == StrategicMilestoneStatus.Active
                        && plan.CurrentMilestone.IsSatisfied)
                        CompleteMilestoneAndAdvance(plan, plan.CurrentMilestone);
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "The same plan resumed.");
                    }
                    finally { ExitRevisionCascade(plan, ownsResumeCascade); }
                case StrategicPlanControlType.Cancel:
                    if (!CancelPlan(plan.StrategicPlanId))
                        return Result(StrategicPlanControlStatus.Rejected, request, plan,
                            "The plan could not be cancelled safely.");
                    return Result(StrategicPlanControlStatus.Applied, request, plan,
                        "The plan was cancelled.");
                default:
                    return Result(StrategicPlanControlStatus.Rejected, request, plan,
                        "Unknown plan control action.");
            }
        }

        private static StrategicPlanControlResult Result(StrategicPlanControlStatus status,
            StrategicPlanControlRequest request, StrategicPlan plan, string message)
        {
            return new StrategicPlanControlResult(status, request, plan, message);
        }
    }
}
