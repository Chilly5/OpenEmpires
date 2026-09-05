using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class StrategicPlanner : IDisposable
    {
        private readonly struct ChildGoalLink
        {
            public readonly StrategicPlan Plan;
            public readonly StrategicMilestone Milestone;

            public ChildGoalLink(StrategicPlan plan, StrategicMilestone milestone)
            {
                Plan = plan;
                Milestone = milestone;
            }
        }

        private readonly CommanderGoalManager goalManager;
        private readonly StrategicResourceReservationManager reservationManager;
        private readonly StrategicPlanRegistry planRegistry;
        private readonly StrategicIntentValidator intentValidator;
        private readonly List<StrategicPlan> plans = new List<StrategicPlan>();
        private readonly List<StrategicIntent> intents = new List<StrategicIntent>();
        private readonly Dictionary<int, StrategicIntent> intentsById =
            new Dictionary<int, StrategicIntent>();
        private readonly Dictionary<int, StrategicIntent> intentsByPlanId =
            new Dictionary<int, StrategicIntent>();
        private readonly Dictionary<int, ChildGoalLink> childGoalLinks = new Dictionary<int, ChildGoalLink>();
        private int nextPlanId = 1;
        private int nextIntentId = 1;
        private StrategicPlan submittingPlan;
        private StrategicMilestone submittingMilestone;
        private bool disposed;

        public IReadOnlyList<StrategicPlan> Plans => plans;
        public IReadOnlyList<StrategicIntent> Intents => intents;
        public IReadOnlyList<StrategicResourceReservation> Reservations => reservationManager.Reservations;
        public StrategicPlanRegistry PlanRegistry => planRegistry;
        public int PlayerId => goalManager.PlayerId;
        public event Action<StrategicIntent> StrategicIntentCreated;
        public event Action<StrategicIntent> StrategicIntentStatusChanged;
        public event Action<StrategicIntent, string> StrategicIntentRejected;
        public event Action<StrategicPlan> PlanStatusChanged;
        public event Action<StrategicPlan, StrategicMilestone> MilestoneStatusChanged;
        public event Action<StrategicPlan, CommanderGoalEvent> ChildGoalEventObserved;
        public event Action<StrategicPlan, string> ResponseGenerated;
        public event Action<StrategicResourceReservation> ReservationCreated;
        public event Action<StrategicResourceReservation> ReservationReleased;
        public event Action<StrategicReservationConflict> ReservationConflictDetected;

        public StrategicPlanner(CommanderGoalManager goalManager,
            Func<ResourceType, int> currentResourceProvider,
            StrategicPlanRegistry planRegistry = null,
            StrategicIntentValidator intentValidator = null)
        {
            this.goalManager = goalManager ?? throw new ArgumentNullException(nameof(goalManager));
            this.planRegistry = planRegistry ?? StrategicPlanRegistry.CreateDefault();
            this.intentValidator = intentValidator ?? new StrategicIntentValidator();
            reservationManager = new StrategicResourceReservationManager(currentResourceProvider);
            goalManager.GoalEventPublished += HandleGoalEvent;
            reservationManager.ReservationCreated += HandleReservationCreated;
            reservationManager.ReservationReleased += HandleReservationReleased;
            reservationManager.ReservationConflictDetected += HandleReservationConflict;
        }

        public StrategicPlan GetPlan(int strategicPlanId)
        {
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].StrategicPlanId == strategicPlanId) return plans[i];
            return null;
        }

        public StrategicIntent GetIntent(int intentId)
        {
            return intentsById.TryGetValue(intentId, out StrategicIntent intent) ? intent : null;
        }

        public StrategicIntent CreateIntent(StrategicObjectiveType objectiveType,
            IDictionary<string, string> parameters = null, int? priority = null)
        {
            ThrowIfDisposed();
            var intent = new StrategicIntent(nextIntentId++, goalManager.PlayerId, objectiveType,
                goalManager.CurrentTick, parameters, priority);
            RegisterIntent(intent);
            return intent;
        }

        public StrategicIntentSubmission SubmitIntent(StrategicObjectiveType objectiveType,
            IDictionary<string, string> parameters = null, int? priority = null)
        {
            return SubmitIntent(CreateIntent(objectiveType, parameters, priority));
        }

        public StrategicIntentSubmission SubmitIntent(StrategicIntent intent)
        {
            ThrowIfDisposed();
            if (intent != null && !intentsById.ContainsKey(intent.IntentId)) RegisterIntent(intent);
            else if (intent != null && !ReferenceEquals(intentsById[intent.IntentId], intent))
                return RejectIntent(intent, StrategicIntentValidationError.DuplicateIntent,
                    $"Strategic intent #{intent.IntentId} is already registered.");
            else if (intent != null && intent.Status != StrategicIntentStatus.Created)
                return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
                    intent, null, StrategicIntentValidationError.DuplicateIntent,
                    $"Strategic intent #{intent.IntentId} was already submitted.");

            StrategicIntentValidationResult validation = intentValidator.Validate(
                intent, goalManager.PlayerId, planRegistry);
            if (!validation.IsValid)
                return RejectIntent(intent, validation.Error, validation.Reason);

            StrategicPlan plan;
            try
            {
                plan = planRegistry.CreatePlan(intent);
                if (plan == null) throw new InvalidOperationException(
                    $"Strategic plan template '{validation.Template.TemplateId}' returned no plan.");
                if (plan.OwnerPlayerId != intent.PlayerId || plan.SourceIntentId != intent.IntentId)
                    throw new InvalidOperationException(
                        "Strategic plan template returned a plan with mismatched ownership or intent identity.");
            }
            catch (Exception error)
            {
                return RejectIntent(intent, StrategicIntentValidationError.TemplateCreationFailed,
                    $"Strategic plan template could not create a plan: {error.Message}");
            }

            plan.StrategicPlanId = nextPlanId++;
            plan.CreatedTick = goalManager.CurrentTick;
            plans.Add(plan);
            intentsByPlanId.Add(plan.StrategicPlanId, intent);
            if (!reservationManager.TryReservePlan(plan, out StrategicReservationConflict conflict))
            {
                FailPlan(plan, null, conflict.ToString());
                return CreatedSubmission(intent, plan);
            }

            plan.Status = StrategicPlanStatus.Active;
            SetIntentStatus(intent, StrategicIntentStatus.Active, string.Empty);
            StrategicMilestone milestone = plan.ActivateFirstMilestone();
            Debug.Log($"[StrategicPlanner] Intent #{intent.IntentId} selected template "
                + $"'{validation.Template.TemplateId}' and started plan #{plan.StrategicPlanId}: "
                + $"{plan.PlanType}.");
            PublishPlanStatus(plan);
            MilestoneStatusChanged?.Invoke(plan, milestone);
            CreateGoalsForMilestone(plan, milestone);
            return CreatedSubmission(intent, plan);
        }

        public CavalryPressurePlan StartCavalryPressurePlan()
        {
            StrategicIntentSubmission submission = SubmitIntent(
                StrategicObjectiveType.AttackPreparation);
            if (submission.Plan is CavalryPressurePlan plan) return plan;
            throw new InvalidOperationException(submission.Reason.Length > 0
                ? submission.Reason
                : "Attack preparation did not create a CavalryPressurePlan.");
        }

        public bool CancelPlan(int strategicPlanId)
        {
            ThrowIfDisposed();
            StrategicPlan plan = GetPlan(strategicPlanId);
            if (plan == null || plan.IsTerminal) return false;
            plan.Status = StrategicPlanStatus.Cancelled;
            plan.OutcomeMessage = plan.CancellationMessage;
            SkipUnfinishedMilestones(plan);
            CancelOwnedNonTerminalGoals(plan);
            reservationManager.ReleasePlanReservations(plan.StrategicPlanId, cancelled: true);
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} cancelled.");
            PublishPlanStatus(plan);
            ResponseGenerated?.Invoke(plan, plan.OutcomeMessage);
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            goalManager.GoalEventPublished -= HandleGoalEvent;
            reservationManager.ReservationCreated -= HandleReservationCreated;
            reservationManager.ReservationReleased -= HandleReservationReleased;
            reservationManager.ReservationConflictDetected -= HandleReservationConflict;
        }

        public StrategicContext BuildContext(CommanderContext commanderContext)
        {
            ThrowIfDisposed();
            return new StrategicContextBuilder().Build(commanderContext, this);
        }

        public StrategicResourceAvailability CheckResourceAvailability(ResourceType resourceType,
            int amount)
        {
            ThrowIfDisposed();
            return reservationManager.CheckAvailability(resourceType, amount);
        }

        public bool CanAllocate(ResourceType resourceType, int amount)
        {
            ThrowIfDisposed();
            return reservationManager.CanAllocate(resourceType, amount);
        }

        public int GetReservedAmount(ResourceType resourceType)
        {
            return reservationManager.GetReservedAmount(resourceType);
        }

        public int GetReservedAmountForPlan(int strategicPlanId, ResourceType resourceType)
        {
            return reservationManager.GetReservedAmountForPlan(strategicPlanId, resourceType);
        }

        public IReadOnlyList<StrategicResourceReservation> GetReservationsForPlan(int strategicPlanId)
        {
            return reservationManager.GetReservationsForPlan(strategicPlanId);
        }

        private void CreateGoalsForMilestone(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (milestone.TacticalGoals.Count == 0)
            {
                CompleteMilestoneAndAdvance(plan, milestone);
                return;
            }

            for (int i = 0; i < milestone.TacticalGoals.Count; i++)
            {
                StrategicTacticalGoalRequest request = milestone.TacticalGoals[i];
                if (!SubmitTrackedGoal(plan, milestone, () => request.Submit(goalManager))) return;
            }
        }

        private bool SubmitTrackedGoal(StrategicPlan plan, StrategicMilestone milestone,
            Func<CommanderGoal> submit)
        {
            if (plan.IsTerminal) return false;
            submittingPlan = plan;
            submittingMilestone = milestone;
            try
            {
                CommanderGoal goal = submit();
                TrackChildGoal(plan, milestone, goal.GoalId);
                return true;
            }
            catch (Exception ex)
            {
                FailPlan(plan, milestone, $"Could not create a tactical goal: {ex.Message}");
                return false;
            }
            finally
            {
                submittingPlan = null;
                submittingMilestone = null;
            }
        }

        private void HandleGoalEvent(CommanderGoalEvent goalEvent)
        {
            if (disposed || goalEvent.Goal == null) return;
            int goalId = goalEvent.Goal.GoalId;
            if (goalEvent.EventType == CommanderGoalEventType.GoalStarted && submittingPlan != null)
                TrackChildGoal(submittingPlan, submittingMilestone, goalId);
            if (!childGoalLinks.TryGetValue(goalId, out ChildGoalLink link)) return;

            ChildGoalEventObserved?.Invoke(link.Plan, goalEvent);
            if (link.Plan.IsTerminal) return;
            switch (goalEvent.EventType)
            {
                case CommanderGoalEventType.GoalStarted:
                case CommanderGoalEventType.GoalProgressChanged:
                case CommanderGoalEventType.GoalBlocked:
                    // These events are observable but do not advance a milestone.
                    break;
                case CommanderGoalEventType.GoalCompleted:
                    link.Milestone.MarkChildGoalCompleted(goalId);
                    if (link.Milestone.Status == StrategicMilestoneStatus.Active && link.Milestone.IsSatisfied)
                        CompleteMilestoneAndAdvance(link.Plan, link.Milestone);
                    break;
                case CommanderGoalEventType.GoalFailed:
                    FailPlan(link.Plan, link.Milestone,
                        $"Tactical goal #{goalId} failed: {goalEvent.Goal.StatusReason}");
                    break;
                case CommanderGoalEventType.GoalCancelled:
                    FailPlan(link.Plan, link.Milestone,
                        $"Tactical goal #{goalId} was cancelled before the milestone completed.");
                    break;
            }
        }

        private void CompleteMilestoneAndAdvance(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (plan.IsTerminal || milestone.Status != StrategicMilestoneStatus.Active) return;
            milestone.SetStatus(StrategicMilestoneStatus.Completed);
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone completed: {milestone.Name}.");
            MilestoneStatusChanged?.Invoke(plan, milestone);

            StrategicMilestone next = plan.AdvanceMilestone();
            if (next == null)
            {
                CompletePlan(plan);
                return;
            }
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone active: {next.Name}.");
            MilestoneStatusChanged?.Invoke(plan, next);
            CreateGoalsForMilestone(plan, next);
        }

        private void CompletePlan(StrategicPlan plan)
        {
            plan.Status = StrategicPlanStatus.Completed;
            plan.OutcomeMessage = plan.CompletionMessage;
            reservationManager.ReleasePlanReservations(plan.StrategicPlanId, cancelled: false);
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} completed. {plan.OutcomeMessage}");
            PublishPlanStatus(plan);
            ResponseGenerated?.Invoke(plan, plan.OutcomeMessage);
        }

        private void FailPlan(StrategicPlan plan, StrategicMilestone milestone, string reason)
        {
            if (plan.IsTerminal) return;
            if (milestone != null && milestone.Status != StrategicMilestoneStatus.Completed)
            {
                milestone.SetStatus(StrategicMilestoneStatus.Failed);
                MilestoneStatusChanged?.Invoke(plan, milestone);
            }
            plan.Status = StrategicPlanStatus.Failed;
            plan.OutcomeMessage = reason ?? "The strategic plan failed.";
            SkipPendingMilestones(plan);
            CancelOwnedNonTerminalGoals(plan);
            reservationManager.ReleasePlanReservations(plan.StrategicPlanId, cancelled: false);
            Debug.LogWarning($"[StrategicPlanner] Plan #{plan.StrategicPlanId} failed: {plan.OutcomeMessage}");
            PublishPlanStatus(plan);
            ResponseGenerated?.Invoke(plan, plan.OutcomeMessage);
        }

        private void TrackChildGoal(StrategicPlan plan, StrategicMilestone milestone, int goalId)
        {
            if (plan == null || milestone == null || childGoalLinks.ContainsKey(goalId)) return;
            childGoalLinks.Add(goalId, new ChildGoalLink(plan, milestone));
            plan.AddChildGoal(goalId);
            milestone.AddRequiredChildGoal(goalId);
        }

        private void HandleReservationCreated(StrategicResourceReservation reservation)
        {
            StrategicPlan plan = GetPlan(reservation.PlanId);
            plan?.AddResourceReservation(reservation.ReservationId);
            Debug.Log($"[StrategicPlanner] Reservation #{reservation.ReservationId} created for "
                + $"plan #{reservation.PlanId}: {reservation.Amount} {reservation.ResourceType}.");
            ReservationCreated?.Invoke(reservation);
        }

        private void HandleReservationReleased(StrategicResourceReservation reservation)
        {
            Debug.Log($"[StrategicPlanner] Reservation #{reservation.ReservationId} "
                + $"{reservation.Status.ToString().ToLowerInvariant()} for plan #{reservation.PlanId}.");
            ReservationReleased?.Invoke(reservation);
        }

        private void HandleReservationConflict(StrategicReservationConflict conflict)
        {
            Debug.LogWarning($"[StrategicPlanner] {conflict}");
            ReservationConflictDetected?.Invoke(conflict);
        }

        private void CancelOwnedNonTerminalGoals(StrategicPlan plan)
        {
            for (int i = 0; i < plan.ChildGoalIds.Count; i++)
            {
                CommanderGoal goal = goalManager.GetGoal(plan.ChildGoalIds[i]);
                if (goal != null && !goal.IsTerminal) goalManager.CancelGoal(goal.GoalId);
            }
        }

        private static void SkipPendingMilestones(StrategicPlan plan)
        {
            for (int i = 0; i < plan.Milestones.Count; i++)
                if (plan.Milestones[i].Status == StrategicMilestoneStatus.Pending)
                    plan.Milestones[i].SetStatus(StrategicMilestoneStatus.Skipped);
        }

        private static void SkipUnfinishedMilestones(StrategicPlan plan)
        {
            for (int i = 0; i < plan.Milestones.Count; i++)
                if (plan.Milestones[i].Status == StrategicMilestoneStatus.Pending
                    || plan.Milestones[i].Status == StrategicMilestoneStatus.Active)
                    plan.Milestones[i].SetStatus(StrategicMilestoneStatus.Skipped);
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(StrategicPlanner));
        }

        private void RegisterIntent(StrategicIntent intent)
        {
            if (intent == null) return;
            intents.Add(intent);
            intentsById.Add(intent.IntentId, intent);
            if (intent.IntentId >= nextIntentId) nextIntentId = intent.IntentId + 1;
            Debug.Log($"[StrategicPlanner] Strategic intent #{intent.IntentId} created: "
                + $"{intent.ObjectiveType} for player {intent.PlayerId}.");
            StrategicIntentCreated?.Invoke(intent);
        }

        private StrategicIntentSubmission RejectIntent(StrategicIntent intent,
            StrategicIntentValidationError error, string reason)
        {
            if (intent != null && intent.Status == StrategicIntentStatus.Created)
                SetIntentStatus(intent, StrategicIntentStatus.Rejected, reason);
            Debug.LogWarning($"[StrategicPlanner] Strategic intent rejected: {reason}");
            StrategicIntentRejected?.Invoke(intent, reason);
            return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
                intent, null, error, reason);
        }

        private static StrategicIntentSubmission CreatedSubmission(StrategicIntent intent,
            StrategicPlan plan)
        {
            return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.PlanCreated,
                intent, plan, StrategicIntentValidationError.None, string.Empty);
        }

        private void PublishPlanStatus(StrategicPlan plan)
        {
            PlanStatusChanged?.Invoke(plan);
            if (!intentsByPlanId.TryGetValue(plan.StrategicPlanId,
                out StrategicIntent intent)) return;
            switch (plan.Status)
            {
                case StrategicPlanStatus.Completed:
                    SetIntentStatus(intent, StrategicIntentStatus.Completed, plan.OutcomeMessage);
                    break;
                case StrategicPlanStatus.Failed:
                    SetIntentStatus(intent, StrategicIntentStatus.Failed, plan.OutcomeMessage);
                    break;
                case StrategicPlanStatus.Cancelled:
                    SetIntentStatus(intent, StrategicIntentStatus.Cancelled, plan.OutcomeMessage);
                    break;
            }
        }

        private void SetIntentStatus(StrategicIntent intent, StrategicIntentStatus status,
            string reason)
        {
            if (intent == null) return;
            intent.Status = status;
            intent.StatusReason = reason ?? string.Empty;
            StrategicIntentStatusChanged?.Invoke(intent);
        }
    }
}
