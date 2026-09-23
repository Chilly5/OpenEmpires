using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class StrategicPlanner : IDisposable
    {
        public const int MaxActivePlans = 4;
        public const int MaxArchivedPlans = 50;
        public const int MaxIntentHistory = 100;
        private const int ResourceRetryIntervalTicks = 30;
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
        private readonly StrategicCommitmentPolicy commitmentPolicy;
        private readonly List<StrategicPlan> plans = new List<StrategicPlan>();
        private readonly List<StrategicPlan> activePlans = new List<StrategicPlan>();
        private readonly List<StrategicPlan> archivedPlans = new List<StrategicPlan>();
        private readonly List<StrategicIntent> intents = new List<StrategicIntent>();
        private readonly Dictionary<int, StrategicIntent> intentsById =
            new Dictionary<int, StrategicIntent>();
        private readonly Dictionary<int, StrategicIntent> intentsByPlanId =
            new Dictionary<int, StrategicIntent>();
        private readonly Dictionary<int, ChildGoalLink> childGoalLinks = new Dictionary<int, ChildGoalLink>();
        private int nextPlanId = 1;
        public StrategicIntentIdProvider IntentIds { get; } = new StrategicIntentIdProvider();
        private int lastResourceRetryTick = -1;
        private StrategicPlan submittingPlan;
        private StrategicMilestone submittingMilestone;
        private bool disposed;

        public IReadOnlyList<StrategicPlan> Plans => plans;
        public IReadOnlyList<StrategicPlan> ActivePlans => activePlans;
        public IReadOnlyList<StrategicPlan> ArchivedPlans => archivedPlans;
        public IReadOnlyList<StrategicIntent> Intents => intents;
        public IReadOnlyList<StrategicResourceReservation> Reservations => reservationManager.Reservations;
        public IReadOnlyList<StrategicResourceReservation> ArchivedReservations => reservationManager.ArchivedReservations;
        public StrategicPlanRegistry PlanRegistry => planRegistry;
        public StrategicCommitmentPolicy CommitmentPolicy => commitmentPolicy;
        public int PlayerId => goalManager.PlayerId;
        internal StrategicPipeline Pipeline { get; set; }
        internal StrategicPipeline GetOrCreatePipeline() => Pipeline
            ?? new StrategicPipeline(goalManager.Simulation, goalManager, this);
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
            StrategicIntentValidator intentValidator = null,
            StrategicCommitmentPolicy commitmentPolicy = null,
            int maxArchivedReservations = StrategicResourceReservationManager.DefaultMaxArchivedReservations)
        {
            this.goalManager = goalManager ?? throw new ArgumentNullException(nameof(goalManager));
            this.planRegistry = planRegistry ?? StrategicPlanRegistry.CreateDefault();
            this.intentValidator = intentValidator ?? new StrategicIntentValidator();
            this.commitmentPolicy = commitmentPolicy ?? new StrategicCommitmentPolicy();
            reservationManager = new StrategicResourceReservationManager(currentResourceProvider,
                maxArchivedReservations);
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
            var intent = new StrategicIntent(IntentIds.Allocate(), goalManager.PlayerId, objectiveType,
                goalManager.CurrentTick, parameters, priority);
            IntentIds.BindAllocated(intent.IntentId, intent);
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
            return SubmitIntent(intent, isEmergency: false, isPlayerOverride: false);
        }

        public StrategicIntentSubmission SubmitIntent(StrategicIntent intent,
            bool isEmergency, bool isPlayerOverride)
        {
            ThrowIfDisposed();
            if (intent != null && isPlayerOverride && intent.Source == StrategicIntentSource.AIRecommendation)
                return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
                    intent, null, StrategicIntentValidationError.CommitmentBlocked,
                    "An AI recommendation cannot request player override authority.");
            StrategicIntentValidationResult validation = intentValidator.Validate(
                intent, goalManager.PlayerId, planRegistry);
            if (!validation.IsValid)
                return RejectIntent(intent, validation.Error, validation.Reason);
            if (intent != null && intent.Status != StrategicIntentStatus.Created)
                return RejectIntent(intent, StrategicIntentValidationError.DuplicateIntent,
                    "Only a newly created strategic intent can be submitted.");
            if (intent != null && !IntentIds.TryRegister(intent))
                return RejectIntent(intent, StrategicIntentValidationError.DuplicateIntent,
                    "This strategic identity is reserved or has already been used.");
            if (intent != null && intentsById.TryGetValue(intent.IntentId, out StrategicIntent existing) && !ReferenceEquals(existing, intent))
            {
                return RejectIntent(intent, StrategicIntentValidationError.DuplicateIntent,
                    "A different strategic intent already owns this identity.");
            }

            if (intent != null && !intentsById.ContainsKey(intent.IntentId)) RegisterIntent(intent);
            else if (intent != null && intent.Status != StrategicIntentStatus.Created)
                return new StrategicIntentSubmission(StrategicIntentSubmissionStatus.Rejected,
                    intent, null, StrategicIntentValidationError.DuplicateIntent,
                    $"Strategic intent #{intent.IntentId} was already submitted.");

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

            if (!CanTransition(plan.PlanType, intent.Source, isEmergency, isPlayerOverride,
                out string transitionReason))
            {
                return RejectIntent(intent, StrategicIntentValidationError.CommitmentBlocked,
                    transitionReason);
            }

            var incomingAuthority = isPlayerOverride ? StrategicPlanAuthority.PlayerOverride
                : isEmergency ? StrategicPlanAuthority.Emergency
                : StrategicPlanAuthority.Normal;

            var conflictingPlans = new List<StrategicPlan>();
            for (int i = 0; i < activePlans.Count; i++)
            {
                StrategicPlan active = activePlans[i];
                if (active.IsTerminal) continue;
                if (!commitmentPolicy.AreCompatible(active.PlanType, plan.PlanType))
                {
                    conflictingPlans.Add(active);
                }
            }

            for (int i = 0; i < conflictingPlans.Count; i++)
            {
                StrategicPlan conflicting = conflictingPlans[i];
                if (isPlayerOverride || (isEmergency && conflicting.Authority < StrategicPlanAuthority.Emergency))
                {
                    CancelPlan(conflicting.StrategicPlanId);
                }
            }

            if (activePlans.Count >= MaxActivePlans)
            {
                return RejectIntent(intent, StrategicIntentValidationError.ActivePlanLimitReached,
                    $"The active strategic plan limit of {MaxActivePlans} has been reached.");
            }

            if (plan is CavalryPressurePlan || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan)
            {
                var totals = new SortedDictionary<ResourceType, int>();
                foreach (StrategicMilestone stage in plan.Milestones)
                    foreach (StrategicResourceRequirement cost in ComputeRequirements(stage))
                        totals[cost.ResourceType] = (totals.TryGetValue(cost.ResourceType, out int amount) ? amount : 0) + cost.Amount;
                var budget = new List<StrategicResourceRequirement>();
                foreach (var total in totals) budget.Add(new StrategicResourceRequirement(total.Key, total.Value));
                plan.SetBudgetFromSimulation(budget);
            }
            plan.Authority = incomingAuthority;
            plan.Source = intent.Source;
            FitEconomyToAvailableWorkers(plan);
            plan.StrategicPlanId = nextPlanId++;
            plan.CreatedTick = goalManager.CurrentTick;
            plans.Add(plan);
            activePlans.Add(plan);
            intentsByPlanId.Add(plan.StrategicPlanId, intent);

            plan.Status = StrategicPlanStatus.Active;
            SetIntentStatus(intent, StrategicIntentStatus.Active, string.Empty);
            StrategicMilestone milestone = plan.ActivateFirstMilestone();
            Debug.Log($"[StrategicPlanner] Intent #{intent.IntentId} selected template "
                + $"'{validation.Template.TemplateId}' and started plan #{plan.StrategicPlanId}: "
                + $"{plan.PlanType}.");
            PublishPlanStatus(plan);
            StartOrWaitForMilestone(plan, milestone);
            return CreatedSubmission(intent, plan);
        }

        internal StrategicIntent MaterializeRecommendation(StrategicIntent selected)
        {
            var parameters = new Dictionary<string, string>();
            foreach (var pair in selected.Parameters) parameters.Add(pair.Key, pair.Value);
            var intent = new StrategicIntent(IntentIds.Allocate(), selected.PlayerId,
                selected.ObjectiveType, selected.CreatedTick, parameters, selected.Priority,
                StrategicIntentSource.AIRecommendation);
            IntentIds.BindAllocated(intent.IntentId, intent);
            return intent;
        }

        internal StrategicIntent MaterializePlayerInterpretation(StrategicIntent interpretation)
        {
            if (interpretation == null || !interpretation.NeedsPlayerIdentity
                || interpretation.Source != StrategicIntentSource.PlayerDirect
                || interpretation.Status != StrategicIntentStatus.Created
                || !intentValidator.Validate(interpretation, PlayerId, planRegistry).IsValid)
                return interpretation;
            var parameters = new Dictionary<string, string>();
            foreach (var pair in interpretation.Parameters) parameters.Add(pair.Key, pair.Value);
            var intent = new StrategicIntent(IntentIds.Allocate(), interpretation.PlayerId,
                interpretation.ObjectiveType, interpretation.CreatedTick, parameters, interpretation.Priority);
            IntentIds.BindAllocated(intent.IntentId, intent);
            interpretation.Status = StrategicIntentStatus.Cancelled;
            interpretation.StatusReason = "Materialized as a trusted Commander request.";
            return intent;
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
            activePlans.Remove(plan);
            ArchivePlan(plan);
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
            Pipeline?.Dispose();
            disposed = true;
            goalManager.GoalEventPublished -= HandleGoalEvent;
            reservationManager.ReservationCreated -= HandleReservationCreated;
            reservationManager.ReservationReleased -= HandleReservationReleased;
            reservationManager.ReservationConflictDetected -= HandleReservationConflict;
            StrategicIntentCreated = null;
            StrategicIntentStatusChanged = null;
            StrategicIntentRejected = null;
            PlanStatusChanged = null;
            MilestoneStatusChanged = null;
            ChildGoalEventObserved = null;
            ResponseGenerated = null;
            ReservationCreated = null;
            ReservationReleased = null;
            ReservationConflictDetected = null;
        }

        public void Tick(int currentTick)
        {
            ThrowIfDisposed();
            if (currentTick == lastResourceRetryTick
                || (lastResourceRetryTick >= 0
                    && currentTick - lastResourceRetryTick < ResourceRetryIntervalTicks))
                return;
            lastResourceRetryTick = currentTick;

            var waiting = new List<StrategicPlan>();
            for (int i = 0; i < activePlans.Count; i++)
            {
                StrategicMilestone milestone = activePlans[i].CurrentMilestone;
                if (milestone != null
                    && (milestone.Status == StrategicMilestoneStatus.WaitingForResources
                        || milestone.Status == StrategicMilestoneStatus.WaitingForPrerequisite))
                    waiting.Add(activePlans[i]);
            }

            for (int i = 0; i < waiting.Count; i++)
            {
                StrategicPlan plan = waiting[i];
                if (!plan.IsTerminal && plan.CurrentMilestone != null)
                    StartOrWaitForMilestone(plan, plan.CurrentMilestone);
            }
        }

        public bool CanTransition(StrategicIntent intent, bool isEmergency,
            bool isPlayerOverride, out string reason)
        {
            if (intent == null)
            {
                reason = "A strategic intent is required.";
                return false;
            }
            return CanTransition(ToPlanType(intent.ObjectiveType), intent.Source, isEmergency,
                isPlayerOverride, out reason);
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

        public bool UpdateReservationAmount(int reservationId, int newAmount)
        {
            ThrowIfDisposed();
            return reservationManager.UpdateReservationAmount(reservationId, newAmount);
        }

        public bool ReleaseReservation(int reservationId, bool cancelled = false)
        {
            ThrowIfDisposed();
            return reservationManager.ReleaseReservation(reservationId, cancelled);
        }

        public bool CompleteMilestoneAndAdvance(int strategicPlanId)
        {
            ThrowIfDisposed();
            StrategicPlan plan = GetPlan(strategicPlanId);
            if (plan == null || plan.IsTerminal)
                return false;
            if (plan.CurrentMilestone == null) return false;
            CompleteMilestoneAndAdvance(plan, plan.CurrentMilestone);
            return true;
        }

        private void CreateGoalsForMilestone(StrategicPlan plan, StrategicMilestone milestone,
            bool economyOnly = false)
        {
            if (milestone.TacticalGoalsStarted) return;
            if (!economyOnly) milestone.MarkTacticalGoalsStarted();
            if (milestone.TacticalGoals.Count == 0)
            {
                if (milestone.Status == StrategicMilestoneStatus.Active)
                    CompleteMilestoneAndAdvance(plan, milestone);
                return;
            }

            for (int i = 0; i < milestone.TacticalGoals.Count; i++)
            {
                StrategicTacticalGoalRequest request = milestone.TacticalGoals[i];
                if (economyOnly && !(request is StrategicResourceAllocationGoalRequest)) continue;
                if (!milestone.StartRequest(i)) continue;
                if (ShouldSkipRequest(request)) continue;
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
                    if (link.Milestone.Status == StrategicMilestoneStatus.Active
                        && link.Milestone.IsSatisfied)
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
            if (goalEvent.Goal.IsTerminal) childGoalLinks.Remove(goalId);
        }

        private void CompleteMilestoneAndAdvance(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (plan.IsTerminal || milestone.Status != StrategicMilestoneStatus.Active) return;
            milestone.SetStatus(StrategicMilestoneStatus.Completed);
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone completed: {milestone.Name}.");
            MilestoneStatusChanged?.Invoke(plan, milestone);

            if (milestone.ResourceReservationIds.Count > 0)
            {
                for (int r = 0; r < milestone.ResourceReservationIds.Count; r++)
                    reservationManager.ReleaseReservation(milestone.ResourceReservationIds[r], cancelled: false);
            }

            StrategicMilestone next = plan.AdvanceMilestone();
            if (next == null)
            {
                CompletePlan(plan);
                return;
            }

            StartOrWaitForMilestone(plan, next);
        }

        private void StartOrWaitForMilestone(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (plan == null || milestone == null || plan.IsTerminal) return;
            if (plan is EconomicExpansionPlan && goalManager.Simulation.GetPlayerAge(PlayerId)
                < LandmarkDefinitions.GetBuildingRequiredAge(BuildingType.TownCenter))
            {
                bool changed = milestone.Status != StrategicMilestoneStatus.WaitingForPrerequisite;
                milestone.SetStatus(StrategicMilestoneStatus.WaitingForPrerequisite);
                plan.OutcomeMessage = $"TownCenter requires age {LandmarkDefinitions.GetBuildingRequiredAge(BuildingType.TownCenter)}. Advance age to resume economic expansion.";
                if (changed) MilestoneStatusChanged?.Invoke(plan, milestone);
                return;
            }
            ResolveRequirements(plan, milestone);

            if (milestone.HasActiveRequirements
                && milestone.ResourceReservationIds.Count == 0)
            {
                if (!reservationManager.TryReserveRequirements(plan,
                    milestone.RequiredResources, out StrategicReservationConflict conflict,
                    out List<StrategicResourceReservation> created))
                {
                    bool changed = milestone.Status
                        != StrategicMilestoneStatus.WaitingForResources;
                    milestone.SetStatus(StrategicMilestoneStatus.WaitingForResources);
                    plan.OutcomeMessage = conflict.ToString();
                    if (changed)
                    {
                        Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone "
                            + $"waiting for resources: {milestone.Name}.");
                        MilestoneStatusChanged?.Invoke(plan, milestone);
                    }
                    // These plans establish income before spending. Preserve custom/legacy
                    // templates whose tactical goals also perform their resource recovery.
                    bool preparedEconomy = plan is CavalryPressurePlan
                        || plan is DefensivePreparationPlan || plan is EconomicExpansionPlan;
                    CreateGoalsForMilestone(plan, milestone, economyOnly: preparedEconomy);
                    if (preparedEconomy) EnsureRecoveryGatherers(plan, milestone);
                    return;
                }

                for (int r = 0; r < created.Count; r++)
                    milestone.AddResourceReservation(created[r].ReservationId);
            }

            milestone.SetStatus(StrategicMilestoneStatus.Active);
            plan.OutcomeMessage = string.Empty;
            Debug.Log($"[StrategicPlanner] Plan #{plan.StrategicPlanId} milestone active: {milestone.Name}.");
            MilestoneStatusChanged?.Invoke(plan, milestone);
            CreateGoalsForMilestone(plan, milestone);
            if (!plan.IsTerminal && milestone.Status == StrategicMilestoneStatus.Active
                && milestone.IsSatisfied && milestone.TacticalGoalsStarted)
                CompleteMilestoneAndAdvance(plan, milestone);
        }

        private bool ShouldSkipRequest(StrategicTacticalGoalRequest request)
        {
            if (!(request is StrategicBuildStructureGoalRequest build)) return false;
            GameSimulation sim = goalManager.Simulation;
            if (build.SkipIfAgeUnavailable && sim.GetPlayerAge(PlayerId)
                < LandmarkDefinitions.GetBuildingRequiredAge(build.StructureType)) return true;
            if (build.EnsureExisting)
                foreach (BuildingData building in sim.BuildingRegistry.GetAllBuildings())
                    if (building.PlayerId == PlayerId && !building.IsDestroyed
                        && building.Type == build.StructureType && !building.IsUnderConstruction) return true;
            return false;
        }

        // Resolve the immediate spending milestone from the same queries used by CommanderPlanner.
        // Economy milestones have no stockpile gate, so workers can establish income first.
        private void ResolveRequirements(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (!milestone.RequirementsResolved)
            {
                milestone.UsesCanonicalRequirements = !milestone.HasActiveRequirements;
                milestone.RequirementsResolved = true;
            }
            if (!milestone.UsesCanonicalRequirements || milestone.ResourceReservationIds.Count > 0) return;
            milestone.ClearRequirements();
            foreach (StrategicResourceRequirement cost in ComputeRequirements(milestone))
                milestone.AddRequiredResource(cost.ResourceType, cost.Amount);
            if (plan is CavalryPressurePlan && milestone.OrderIndex == 0)
            {
                foreach (StrategicResourceRequirement cost in ComputeRequirements(plan.Milestones[2]))
                    milestone.AddRequiredResource(cost.ResourceType, cost.Amount);
            }
        }

        private void FitEconomyToAvailableWorkers(StrategicPlan plan)
        {
            if (!(plan is DefensivePreparationPlan || plan is CavalryPressurePlan || plan is EconomicExpansionPlan)) return;
            int available = 0;
            foreach (UnitData unit in goalManager.Simulation.UnitRegistry.GetAllUnits())
                if (unit.PlayerId == PlayerId && unit.IsVillager && unit.CurrentHealth > 0
                    && unit.State != UnitState.Dead) available++;
            if (available == 0) return;
            var allocations = new List<StrategicResourceAllocationGoalRequest>();
            int total = 0;
            foreach (StrategicTacticalGoalRequest request in plan.Milestones[0].TacticalGoals)
                if (request is StrategicResourceAllocationGoalRequest allocation)
                {
                    allocations.Add(allocation);
                    total += allocation.WorkerTarget;
                }
            // Do not demand more simultaneous gatherers than a fresh settlement owns.
            while (total > available)
            {
                StrategicResourceAllocationGoalRequest largest = null;
                foreach (var allocation in allocations)
                    if (allocation.WorkerTarget > 1 && (largest == null || allocation.WorkerTarget > largest.WorkerTarget))
                        largest = allocation;
                if (largest == null) break;
                largest.WorkerTarget--;
                total--;
            }
        }

        private void EnsureRecoveryGatherers(StrategicPlan plan, StrategicMilestone milestone)
        {
            CommanderContext context = new CommanderContextBuilder().Build(goalManager.Simulation, goalManager);
            foreach (StrategicResourceRequirement requirement in milestone.RequiredResources)
            {
                if (reservationManager.CanAllocate(requirement.ResourceType, requirement.Amount)) continue;
                bool gathering = false;
                foreach (var allocation in context.WorkerAllocation)
                    if (allocation.ResourceType == requirement.ResourceType && allocation.AssignedWorkers > 0)
                        gathering = true;
                foreach (int goalId in plan.ChildGoalIds)
                    if (goalManager.GetGoal(goalId) is ResourceAllocationGoal goal && !goal.IsTerminal
                        && goal.Resource == requirement.ResourceType) gathering = true;
                if (!gathering)
                    SubmitTrackedGoal(plan, milestone,
                        () => goalManager.SubmitResourceAllocation(requirement.ResourceType, 1));
            }
        }

        private List<StrategicResourceRequirement> ComputeRequirements(StrategicMilestone milestone)
        {
            GameSimulation sim = goalManager.Simulation;
            int food = 0, wood = 0, gold = 0, stone = 0;
            foreach (StrategicTacticalGoalRequest request in milestone.TacticalGoals)
            {
                if (ShouldSkipRequest(request)) continue;
                if (request is StrategicBuildStructureGoalRequest build)
                {
                    int count = build.Count;
                    if (build.EnsureExisting)
                        foreach (BuildingData existing in sim.BuildingRegistry.GetAllBuildings())
                            if (existing.PlayerId == PlayerId && !existing.IsDestroyed && existing.Type == build.StructureType) count--;
                    count = Math.Max(0, count);
                    food += sim.GetBuildingFoodCost(build.StructureType) * count;
                    wood += sim.GetBuildingWoodCost(build.StructureType) * count;
                    gold += sim.GetBuildingGoldCost(build.StructureType) * count;
                    stone += sim.GetBuildingStoneCost(build.StructureType) * count;
                }
                else if (request is StrategicEnsureUnitCountGoalRequest units)
                {
                    sim.GetUnitTrainingSpec(PlayerId, units.UnitType, out int resolved,
                        out int f, out int w, out int g, out _);
                    int remaining = units.TargetTotal;
                    foreach (UnitData unit in sim.UnitRegistry.GetAllUnits())
                        if (unit.PlayerId == PlayerId && unit.UnitType == resolved
                            && unit.CurrentHealth > 0 && unit.State != UnitState.Dead) remaining--;
                    foreach (BuildingData building in sim.BuildingRegistry.GetAllBuildings())
                        if (building.PlayerId == PlayerId && !building.IsDestroyed)
                            foreach (int queued in building.TrainingQueue)
                                if (queued == resolved) remaining--;
                    remaining = Math.Max(0, remaining);
                    food += f * remaining;
                    wood += w * remaining;
                    gold += g * remaining;
                }
            }
            var result = new List<StrategicResourceRequirement>();
            if (food > 0) result.Add(new StrategicResourceRequirement(ResourceType.Food, food));
            if (wood > 0) result.Add(new StrategicResourceRequirement(ResourceType.Wood, wood));
            if (gold > 0) result.Add(new StrategicResourceRequirement(ResourceType.Gold, gold));
            if (stone > 0) result.Add(new StrategicResourceRequirement(ResourceType.Stone, stone));
            return result;
        }

        private void CompletePlan(StrategicPlan plan)
        {
            plan.Status = StrategicPlanStatus.Completed;
            plan.OutcomeMessage = plan.CompletionMessage;
            activePlans.Remove(plan);
            ArchivePlan(plan);
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
            activePlans.Remove(plan);
            ArchivePlan(plan);
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
                childGoalLinks.Remove(plan.ChildGoalIds[i]);
            }
        }

        private bool CanTransition(StrategicPlanType proposed, StrategicIntentSource source, bool isEmergency,
            bool isPlayerOverride, out string reason)
        {
            for (int i = 0; i < activePlans.Count; i++)
            {
                StrategicPlan active = activePlans[i];
                if (active.IsTerminal) continue;
                if (!commitmentPolicy.CanTransition(active, proposed, source, isEmergency,
                    isPlayerOverride, out reason))
                    return false;
            }
            reason = isPlayerOverride
                ? "Explicit player strategic intent bypassed commitment restrictions."
                : "No active plan blocks the proposed transition.";
            return true;
        }

        private void ArchivePlan(StrategicPlan plan)
        {
            if (archivedPlans.Contains(plan)) return;
            if (archivedPlans.Count >= MaxArchivedPlans)
            {
                StrategicPlan removed = archivedPlans[0];
                archivedPlans.RemoveAt(0);
                plans.Remove(removed);
                if (intentsByPlanId.TryGetValue(removed.StrategicPlanId,
                    out StrategicIntent removedIntent))
                {
                    intentsByPlanId.Remove(removed.StrategicPlanId);
                    if (removedIntent.IsTerminal)
                    {
                        intents.Remove(removedIntent);
                        intentsById.Remove(removedIntent.IntentId);
                    }
                }
            }
            archivedPlans.Add(plan);
        }

        private static StrategicPlanType ToPlanType(StrategicObjectiveType objectiveType)
        {
            switch (objectiveType)
            {
                case StrategicObjectiveType.AttackPreparation:
                    return StrategicPlanType.CavalryPressure;
                case StrategicObjectiveType.DefensivePreparation:
                    return StrategicPlanType.DefensivePreparation;
                case StrategicObjectiveType.EconomicExpansion:
                    return StrategicPlanType.EconomicExpansion;
                case StrategicObjectiveType.MilitaryReinforcement:
                    return StrategicPlanType.MilitaryReinforcement;
                default:
                    throw new ArgumentOutOfRangeException(nameof(objectiveType));
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
                    || plan.Milestones[i].Status == StrategicMilestoneStatus.Active
                    || plan.Milestones[i].Status == StrategicMilestoneStatus.WaitingForResources
                    || plan.Milestones[i].Status == StrategicMilestoneStatus.WaitingForPrerequisite)
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
            IntentIds.Observe(intent.IntentId);
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
            TrimIntentHistory();
        }

        private void TrimIntentHistory()
        {
            while (intents.Count > MaxIntentHistory)
            {
                int removableIndex = -1;
                for (int i = 0; i < intents.Count; i++)
                {
                    if (intents[i].IsTerminal)
                    {
                        removableIndex = i;
                        break;
                    }
                }
                if (removableIndex < 0) return;
                StrategicIntent removed = intents[removableIndex];
                intents.RemoveAt(removableIndex);
                intentsById.Remove(removed.IntentId);
            }
        }
    }
}
