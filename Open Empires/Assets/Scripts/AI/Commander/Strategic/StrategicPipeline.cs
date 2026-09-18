using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class StrategicPipeline : IDisposable
    {
        private static readonly IReadOnlyList<StrategicRecommendation> EmptyRecommendations =
            Array.Empty<StrategicRecommendation>();

        private readonly StrategicPlanner strategicPlanner;
        private readonly Func<CommanderContext> commanderContextProvider;
        private readonly StrategicEvaluationTrigger evaluationTrigger;
        private readonly StrategicCommitmentPolicy commitmentPolicy;
        private readonly RecentDecisionHistory decisionHistory;
        private readonly IStrategicEvaluator evaluator;
        private readonly IStrategicDecisionPolicy decisionPolicy;
        private readonly GameSimulation simulation;
        private bool disposed;

        public StrategicPlanner StrategicPlanner => strategicPlanner;
        public StrategicEvaluationTrigger EvaluationTrigger => evaluationTrigger;
        public StrategicCommitmentPolicy CommitmentPolicy => commitmentPolicy;
        public RecentDecisionHistory DecisionHistory => decisionHistory;
        public StrategicContext LastContext { get; private set; }
        public IReadOnlyList<StrategicRecommendation> LastRecommendations { get; private set; }
            = EmptyRecommendations;
        public StrategicDecisionResult LastDecision { get; private set; }
        public StrategicIntentSubmission LastSubmission { get; private set; }

        public event Action<StrategicDecisionRecord> EvaluationCompleted;
        public event Action<StrategicContext> ContextUpdated;

        public StrategicPipeline(StrategicPlanner strategicPlanner,
            Func<CommanderContext> commanderContextProvider,
            StrategicEvaluationTrigger evaluationTrigger = null,
            StrategicCommitmentPolicy commitmentPolicy = null,
            RecentDecisionHistory decisionHistory = null,
            IStrategicEvaluator evaluator = null,
            IStrategicDecisionPolicy decisionPolicy = null)
        {
            this.strategicPlanner = strategicPlanner
                ?? throw new ArgumentNullException(nameof(strategicPlanner));
            this.commanderContextProvider = commanderContextProvider
                ?? throw new ArgumentNullException(nameof(commanderContextProvider));
            this.evaluationTrigger = evaluationTrigger ?? new StrategicEvaluationTrigger();
            this.commitmentPolicy = commitmentPolicy ?? strategicPlanner.CommitmentPolicy;
            this.decisionHistory = decisionHistory ?? new RecentDecisionHistory();
            this.evaluator = evaluator ?? new RuleBasedStrategicEvaluator();
            this.decisionPolicy = decisionPolicy ?? new RuleBasedStrategicDecisionPolicy();

            if (strategicPlanner.Pipeline != null)
                throw new InvalidOperationException("A strategic planner already has a pipeline.");
            strategicPlanner.Pipeline = this;

            strategicPlanner.MilestoneStatusChanged += OnMilestoneStatusChanged;
            strategicPlanner.PlanStatusChanged += OnPlanStatusChanged;
        }

        public StrategicPipeline(GameSimulation simulation,
            CommanderGoalManager goalManager,
            StrategicPlanner strategicPlanner,
            StrategicEvaluationTrigger evaluationTrigger = null,
            StrategicCommitmentPolicy commitmentPolicy = null,
            RecentDecisionHistory decisionHistory = null,
            IStrategicEvaluator evaluator = null,
            IStrategicDecisionPolicy decisionPolicy = null)
            : this(strategicPlanner,
                   () => new CommanderContextBuilder().Build(simulation, goalManager),
                   evaluationTrigger, commitmentPolicy, decisionHistory, evaluator, decisionPolicy)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            simulation.OnUnitDied += OnStrategicUnitChanged;
            simulation.OnBuildingDestroyed += OnStrategicBuildingChanged;
            simulation.OnUnitTrained += OnStrategicUnitTrained;
            simulation.OnBuildingCreated += OnStrategicBuildingCreated;
        }

        public void Tick(int currentTick)
        {
            ThrowIfDisposed();
            strategicPlanner.Tick(currentTick);
            if (!evaluationTrigger.ShouldEvaluate(currentTick,
                out StrategicEvaluationTriggerType triggerType, out string reason))
                return;
            Evaluate(commanderContextProvider(), currentTick, triggerType, reason, null);
        }

        public StrategicDecisionRecord EvaluateNow(StrategicEvaluationTriggerType triggerType,
            string reason = null, int? tick = null)
        {
            ThrowIfDisposed();
            CommanderContext context = commanderContextProvider();
            return Evaluate(context, tick ?? context.SnapshotTick, triggerType,
                reason ?? triggerType.ToString(), null);
        }

        public StrategicDecisionRecord EvaluatePlayerIntentNow(StrategicIntent playerIntent,
            string reason = null, int? tick = null)
        {
            ThrowIfDisposed();
            playerIntent = strategicPlanner.MaterializePlayerInterpretation(playerIntent);
            CommanderContext context = commanderContextProvider();
            return Evaluate(context, tick ?? context.SnapshotTick,
                StrategicEvaluationTriggerType.PlayerRequest,
                reason ?? "Explicit player strategic intent.", playerIntent);
        }

        private StrategicDecisionRecord Evaluate(CommanderContext commanderContext,
            int currentTick, StrategicEvaluationTriggerType triggerType, string reason,
            StrategicIntent playerIntent)
        {
            LastContext = strategicPlanner.BuildContext(commanderContext);
            ContextUpdated?.Invoke(LastContext);
            LastRecommendations = evaluator.Evaluate(LastContext) ?? EmptyRecommendations;
            LastDecision = decisionPolicy.Decide(LastContext, LastRecommendations, playerIntent);
            if (playerIntent == null && LastDecision.HasSelection)
                LastDecision = StrategicDecisionResult.Selected(
                    strategicPlanner.MaterializeRecommendation(LastDecision.SelectedIntent),
                    LastDecision.SourceRecommendation, LastDecision.CreatedTick,
                    LastDecision.PriorityLevel, LastDecision.Reason);
            LastSubmission = null;
            evaluationTrigger.MarkEvaluated(currentTick);

            bool transitionAllowed = true;
            string outcome = LastDecision.Reason;
            if (LastDecision.HasSelection)
            {
                bool isEmergency = triggerType == StrategicEvaluationTriggerType.Emergency
                    || LastDecision.PriorityLevel == StrategicPriorityLevel.Emergency;
                bool isPlayerOverride = playerIntent != null
                    && ReferenceEquals(LastDecision.SelectedIntent, playerIntent);
                transitionAllowed = CanTransition(LastDecision.SelectedIntent, isEmergency,
                    isPlayerOverride, out string transitionReason);
                if (transitionAllowed)
                {
                    LastSubmission = strategicPlanner.SubmitIntent(
                        LastDecision.SelectedIntent, isEmergency, isPlayerOverride);
                    outcome = LastSubmission.CreatedPlan
                        ? $"{LastDecision.Reason} Started plan #{LastSubmission.Plan.StrategicPlanId} ({LastSubmission.Plan.PlanType})."
                        : $"{LastDecision.Reason} Planner rejected the intent: {LastSubmission.Reason}";
                }
                else
                {
                    outcome = $"{LastDecision.Reason} Transition blocked: {transitionReason}";
                }
            }

            StrategicPlan activePlan = LastSubmission?.Plan ?? FindFirstActivePlan();
            StrategicDecisionRecord record = decisionHistory.RecordDecision(
                currentTick, triggerType, reason, activePlan?.StrategicPlanId,
                activePlan?.PlanType, outcome, LastRecommendations.Count,
                LastDecision, LastSubmission, transitionAllowed);
            EvaluationCompleted?.Invoke(record);
            return record;
        }

        public StrategicDecisionRecord EvaluateApprovedIntentNow(StrategicApprovalResult approval)
        {
            ThrowIfDisposed();
            LastContext = strategicPlanner.BuildContext(commanderContextProvider());
            ContextUpdated?.Invoke(LastContext);
            LastRecommendations = evaluator.Evaluate(LastContext) ?? EmptyRecommendations;
            LastSubmission = null;
            StrategicApprovalResult fresh = approval;
            if (approval == null || !approval.Approved || approval.Intent == null)
                fresh = StrategicApprovalResult.Reject(approval?.Reason ?? "Strategic approval is required.");
            else if (!strategicPlanner.IntentIds.Owns(approval.Intent))
                fresh = StrategicApprovalResult.Reject("Strategic intent identity is not owned by this Commander.");
            else
                fresh = new StrategicApprovalLayer().Evaluate(LastContext, approval.Intent, approval.Intent.Source);

            LastDecision = !fresh.Approved
                ? StrategicDecisionResult.Rejected(LastContext.SnapshotTick, fresh.Reason)
                : decisionPolicy is IStrategicApprovedDecisionPolicy approvedPolicy
                    ? approvedPolicy.DecideApproved(LastContext, LastRecommendations, fresh)
                    : StrategicDecisionResult.Rejected(LastContext.SnapshotTick,
                        "The configured decision policy does not support approved strategic requests.");
            if (LastDecision.HasSelection && !ReferenceEquals(LastDecision.SelectedIntent, fresh.Intent))
                LastDecision = StrategicDecisionResult.Rejected(LastContext.SnapshotTick,
                    "Decision policy selected an intent outside this approval.");
            bool allowed = false;
            string outcome = LastDecision.Reason;
            if (LastDecision.HasSelection)
            {
                bool playerOverride = fresh.Authority == StrategicPlanAuthority.PlayerOverride;
                allowed = CanTransition(fresh.Intent, false, playerOverride, out string reason);
                if (allowed)
                {
                    LastSubmission = strategicPlanner.SubmitIntent(fresh.Intent, false, playerOverride);
                    outcome = LastSubmission.CreatedPlan
                        ? "Approved strategic intent started plan #" + LastSubmission.Plan.StrategicPlanId + "."
                        : LastSubmission.Reason;
                }
                else outcome = reason;
            }
            evaluationTrigger.MarkEvaluated(LastContext.SnapshotTick);
            StrategicPlan active = LastSubmission?.Plan ?? FindFirstActivePlan();
            var record = decisionHistory.RecordDecision(LastContext.SnapshotTick,
                StrategicEvaluationTriggerType.PlayerRequest, "Approved strategic request.",
                active?.StrategicPlanId, active?.PlanType, outcome, LastRecommendations.Count,
                LastDecision, LastSubmission, allowed);
            EvaluationCompleted?.Invoke(record);
            return record;
        }

        public StrategicContext CaptureContext()
        {
            ThrowIfDisposed();
            return strategicPlanner.BuildContext(commanderContextProvider());
        }

        private bool CanTransition(StrategicIntent intent, bool isEmergency,
            bool isPlayerOverride, out string reason)
        {
            StrategicPlanType proposed = ToPlanType(intent.ObjectiveType);
            for (int i = 0; i < strategicPlanner.ActivePlans.Count; i++)
            {
                StrategicPlan active = strategicPlanner.ActivePlans[i];
                if (active.IsTerminal) continue;
                if (!commitmentPolicy.CanTransition(active, proposed, intent.Source, isEmergency,
                    isPlayerOverride, out reason))
                    return false;
            }
            reason = "No active plan blocks the selected intent.";
            return true;
        }

        private StrategicPlan FindFirstActivePlan()
        {
            for (int i = 0; i < strategicPlanner.ActivePlans.Count; i++)
                if (!strategicPlanner.ActivePlans[i].IsTerminal)
                    return strategicPlanner.ActivePlans[i];
            return null;
        }

        private static StrategicPlanType ToPlanType(StrategicObjectiveType objectiveType)
        {
            switch (objectiveType)
            {
                case StrategicObjectiveType.AttackPreparation: return StrategicPlanType.CavalryPressure;
                case StrategicObjectiveType.DefensivePreparation: return StrategicPlanType.DefensivePreparation;
                case StrategicObjectiveType.EconomicExpansion: return StrategicPlanType.EconomicExpansion;
                case StrategicObjectiveType.MilitaryReinforcement: return StrategicPlanType.MilitaryReinforcement;
                default: throw new ArgumentOutOfRangeException(nameof(objectiveType));
            }
        }

        private void OnMilestoneStatusChanged(StrategicPlan plan, StrategicMilestone milestone)
        {
            if (plan.IsTerminal) return;
            if (milestone.Status == StrategicMilestoneStatus.Completed)
                evaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.MilestoneCompleted,
                    $"Plan #{plan.StrategicPlanId} completed milestone: {milestone.Name}");
            else if (milestone.Status == StrategicMilestoneStatus.Failed)
                evaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.PlanFailed,
                    $"Plan #{plan.StrategicPlanId} milestone failed: {milestone.Name}");
        }

        private void OnPlanStatusChanged(StrategicPlan plan)
        {
            if (plan.Status == StrategicPlanStatus.Failed)
                evaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.PlanFailed,
                    $"Plan #{plan.StrategicPlanId} failed: {plan.OutcomeMessage}");
        }

        private void OnStrategicUnitChanged(int unitId) => FireWorldStateChange("Unit state changed.");
        private void OnStrategicBuildingChanged(int buildingId) => FireWorldStateChange("Building state changed.");
        private void OnStrategicUnitTrained(int unitId, int unitType, int playerId) =>
            FireWorldStateChange("Unit production changed.");
        private void OnStrategicBuildingCreated(BuildingData building) =>
            FireWorldStateChange("Building production changed.");

        private void FireWorldStateChange(string reason)
        {
            if (!disposed)
                evaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.WorldStateChange, reason);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(strategicPlanner.Pipeline, this)) strategicPlanner.Pipeline = null;
            strategicPlanner.MilestoneStatusChanged -= OnMilestoneStatusChanged;
            strategicPlanner.PlanStatusChanged -= OnPlanStatusChanged;
            if (simulation != null)
            {
                simulation.OnUnitDied -= OnStrategicUnitChanged;
                simulation.OnBuildingDestroyed -= OnStrategicBuildingChanged;
                simulation.OnUnitTrained -= OnStrategicUnitTrained;
                simulation.OnBuildingCreated -= OnStrategicBuildingCreated;
            }
            EvaluationCompleted = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(StrategicPipeline));
        }
    }
}
