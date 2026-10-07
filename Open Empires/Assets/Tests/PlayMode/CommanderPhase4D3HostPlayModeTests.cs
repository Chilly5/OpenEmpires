using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D3Host")]
    public sealed class CommanderPhase4D3HostPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private HeldStrategicProvider strategic;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            stock.Food = stock.Wood = stock.Gold = stock.Stone = 5000;
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            for (int i = 0; i < 8; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 10 + i, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            strategic = new HeldStrategicProvider();
            chat = new GameObject("Phase4D3HostChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
            chat.InitializeStrategic(strategic, pipeline);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator ActiveRangedToDefensive_PendingCannotUseOrdinaryApprove_ConfirmReplacesThroughPolicy()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            AdvanceToSpendingMilestone(old);
            int oldRevision = old.Revision;
            int oldGoalCount = goals.Goals.Count;
            int oldReservationCount = planner.Reservations.Count;
            int oldDecisionCount = pipeline.DecisionHistory.History.Count;
            Assert.That(oldReservationCount, Is.GreaterThan(0));
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(strategic.Calls, Is.EqualTo(1));
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False);
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            int pendingId = chat.PendingStrategicIntent.IntentId;
            Assert.That(chat.PendingAdaptationProposal?.PendingIntentId, Is.EqualTo(pendingId));
            Assert.That(chat.DisplayedTranscript, Does.Contain("still active"));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Confirm as command"));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(old.Revision, Is.EqualTo(oldRevision));
            Assert.That(goals.Goals.Count, Is.EqualTo(oldGoalCount));
            Assert.That(planner.Reservations.Count, Is.EqualTo(oldReservationCount));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(planner.Intents.Count, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(oldDecisionCount));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(chat.PendingStrategicIntent?.IntentId, Is.EqualTo(pendingId));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Reservations.Count, Is.EqualTo(oldReservationCount));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(oldDecisionCount));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            StrategicDecisionRecord decision = chat.ConfirmStrategicCommand();
            Assert.That(decision?.Submission?.CreatedPlan, Is.True, decision?.Outcome);
            Assert.That(decision.Submission.Intent.IntentId, Is.EqualTo(pendingId));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(oldDecisionCount + 1));
            Assert.That(decision.Submission.Intent.Source,
                Is.EqualTo(StrategicIntentSource.AIConfirmedPlayerCommand));
            Assert.That(decision.Submission.Plan.PlanType, Is.EqualTo(StrategicPlanType.DefensiveTurtle));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.Reservations.Any(value => value.PlanId == old.StrategicPlanId), Is.False);
            Assert.That(strategic.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RevisionWhileProviderWaits_DiscardsResponseAndKeepsOldAuthority()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(strategic.Calls, Is.EqualTo(1));
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            planner.ApplyControl(pause);
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            planner.ApplyControl(resume);
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator MultipleActivePlans_AmbiguousRequestFailsClosed()
        {
            StrategicPlan first = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            StrategicPlan second = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            int reservations = planner.Reservations.Count;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(strategic.Calls, Is.Zero);
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(second.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DirectPlayerPlan_ConfirmationCannotSupersedeIt()
        {
            StrategicIntent directIntent = planner.CreateIntent(StrategicObjectiveType.RangedReinforcement);
            StrategicApprovalResult approval = new StrategicApprovalLayer().Evaluate(
                pipeline.CaptureContext(), directIntent, directIntent.Source);
            StrategicPlan direct = pipeline.EvaluateApprovedIntentNow(approval).Submission?.Plan;
            Assert.That(direct, Is.Not.Null);
            Assert.That(direct.Source, Is.EqualTo(StrategicIntentSource.PlayerDirect));
            Assert.That(direct.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            StrategicDecisionRecord decision = chat.ConfirmStrategicCommand();
            Assert.That(decision?.Submission?.CreatedPlan, Is.Not.True);
            Assert.That(direct.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SupportedCavalryFocusWhileDefenseActive_FormsPendingProposalWithoutExecution()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.DefensivePreparation);
            int goalsBefore = goals.Goals.Count;
            yield return Translate("prepare cavalry attack");
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(chat.PendingAdaptationProposal.PendingIntentId,
                Is.EqualTo(chat.PendingStrategicIntent.IntentId));
            Assert.That(chat.PendingStrategicIntent.Parameters["focus"], Is.EqualTo("cavalry"));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator CompatibleSameObjective_RequiresExplicitChoiceAndMayCoexist()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare ranged reinforcements");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            int pendingId = chat.PendingStrategicIntent.IntentId;
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(chat.PendingStrategicIntent.IntentId, Is.EqualTo(pendingId));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            StrategicDecisionRecord decision = chat.ConfirmStrategicCommand();
            Assert.That(decision?.Submission?.CreatedPlan, Is.True, decision?.Outcome);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(decision.Submission.Plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.ActivePlans.Count, Is.EqualTo(2));
            Assert.That(chat.DisplayedTranscript, Does.Not.Contain("will replace"));
        }

        [UnityTest]
        public IEnumerator NoPlanAtRequestStart_NewPlanBeforePreviewRejectsResponse()
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(strategic.Calls, Is.EqualTo(1));
            StrategicPlan newPlan = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(newPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NoSource_TransientPlanCreatedAndCancelledWhileAwaiting_IsStillStale()
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            StrategicPlan transient = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Assert.That(planner.CaptureControlRequest(0, transient.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Assert.That(planner.ActivePlans, Is.Empty);
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator OneSource_TransientCompatiblePlanWhileAwaiting_InvalidatesSourceSet()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            int oldRevision = old.Revision;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            StrategicPlan transient = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Assert.That(planner.CaptureControlRequest(0, transient.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Assert.That(planner.ActivePlans.Single(), Is.SameAs(old));
            Assert.That(old.Revision, Is.EqualTo(oldRevision));
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(2));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NoSource_TransientPlanAfterPreview_OrdinaryApprovalIsStale()
        {
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            StrategicPlan transient = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Assert.That(planner.CaptureControlRequest(0, transient.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Assert.That(planner.ActivePlans, Is.Empty);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator OneSource_TransientCompatiblePlanAfterPreview_ConfirmIsStale()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            StrategicPlan transient = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Assert.That(planner.CaptureControlRequest(0, transient.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Assert.That(planner.ActivePlans.Single(), Is.SameAs(old));
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(2));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NoPlanAtRequestStart_NewPlanBeforeApprovalRejectsPreview()
        {
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            StrategicPlan newPlan = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(newPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PlanCancelledWhileProviderWaits_DiscardsResponse()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PlanCompletedWhileProviderWaits_DiscardsResponse()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            CompletePlanThroughChildEvents(old);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PlanCancelledAfterPreview_RejectsConfirmation()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            int decisions = pipeline.DecisionHistory.History.Count;
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SourceRevisedAfterPreview_ConfirmationIsStaleWithoutConsumingPlan()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            planner.ApplyControl(pause);
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            planner.ApplyControl(resume);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ResourceBlockerClearsWhileProviderWaits_DiscardsResponse()
        {
            simulation.ResourceManager.GetPlayerResources(0).Wood = 0;
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            AdvanceToSpendingMilestone(old);
            Assert.That(old.CurrentMilestone.Status,
                Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            simulation.ResourceManager.GetPlayerResources(0).Wood = 5000;
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DismissAndReset_ClearProposalAndPendingIdentity()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            chat.DismissStrategicRecommendation();
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            chat.ResetConversation();
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnsupportedTargetParameter_IsRejectedWithoutPlanEffects()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            int goalsBefore = goals.Goals.Count;
            int reservationsBefore = planner.Reservations.Count;
            strategic.Json = "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"DefensiveTurtle\","
                + "\"parameters\":{\"towerTarget\":\"99\"}}";
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservationsBefore));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator CompletedSourceAfterPreview_RejectsConfirmation()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            int pendingId = chat.PendingAdaptationProposal.PendingIntentId;
            CompletePlanThroughChildEvents(old);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            int decisions = pipeline.DecisionHistory.History.Count;
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(planner.Intents.Any(intent => intent.IntentId == pendingId), Is.False);
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReplacementWhileProviderWaits_RejectsOldSourceEvenWithOneNewActivePlan()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            StrategicIntent direct = planner.CreateIntent(StrategicObjectiveType.DefensiveTurtle);
            StrategicApprovalResult approval = new StrategicApprovalLayer().Evaluate(
                pipeline.CaptureContext(), direct, direct.Source);
            StrategicPlan replacement = pipeline.EvaluateApprovedIntentNow(approval).Submission?.Plan;
            Assert.That(replacement, Is.Not.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(replacement.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.ActivePlans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ResourceBlockerClearsAfterPreview_RejectsConfirmation()
        {
            simulation.ResourceManager.GetPlayerResources(0).Wood = 0;
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            AdvanceToSpendingMilestone(old);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            int goalsBefore = goals.Goals.Count;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 5000;
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator OwnerMismatchAfterPreview_RejectsConfirmationBeforeBridgeTransfer()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            typeof(CommanderChatUI).GetField("<Conversation>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(chat, new ConversationState(1));
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PipelineReplacementAndReinitialize_ClearProposal()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            chat.InitializeStrategic(strategic, pipeline);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            yield return Translate("prepare fortified defenses");
            var laterGoals = new CommanderGoalManager(simulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, Resource);
            var laterPipeline = new StrategicPipeline(simulation, laterGoals, laterPlanner);
            try
            {
                chat.InitializeStrategic(strategic, laterPipeline);
                Assert.That(chat.PendingStrategicIntent, Is.Null);
                Assert.That(chat.PendingAdaptationProposal, Is.Null);
                Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
                Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
                Assert.That(laterPlanner.Plans, Is.Empty);
            }
            finally
            {
                chat.InitializeStrategic(strategic, pipeline);
                laterPipeline.Dispose();
                laterPlanner.Dispose();
                laterGoals.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator NewLifecycleMessageAndFailedTranslation_ClearPreviousProposal()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Task<CommanderAIChatSubmission> status = chat.SubmitMessageAsync("strategy status");
            while (!status.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            yield return Translate("prepare fortified defenses");
            strategic.Json = "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"DefensiveTurtle\","
                + "\"parameters\":{\"budget\":\"99999\"}}";
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator NewPreferenceMessage_DismissesActivePlanProposalWithoutProviderCall()
        {
            StrategicPlan old = StartAIPlan(StrategicObjectiveType.RangedReinforcement);
            yield return Translate("prepare fortified defenses");
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            int calls = strategic.Calls;
            Task<CommanderAIChatSubmission> preference = chat.SubmitMessageAsync("focus cavalry");
            while (!preference.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Null);
            Assert.That(strategic.Calls, Is.EqualTo(calls));
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private StrategicPlan StartAIPlan(StrategicObjectiveType objective)
        {
            string phrase = objective == StrategicObjectiveType.RangedReinforcement
                ? "prepare ranged reinforcements"
                : objective == StrategicObjectiveType.DefensivePreparation
                    ? "prepare defenses" : "prepare fortified defenses";
            var request = new StrategicAIRequest(phrase, pipeline.CaptureContext(), planner.IntentIds);
            StrategicIntent intent = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, CancellationToken.None).Result.Intent;
            intent.Authorize(planner.IntentIds, "Trusted player strategic request.");
            StrategicIntentSubmission submitted = planner.SubmitIntent(intent);
            Assert.That(submitted.CreatedPlan, Is.True, submitted.Reason);
            return submitted.Plan;
        }

        private IEnumerator Translate(string phrase)
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(phrase);
            strategic.Release();
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False);
        }

        private void CompletePlanThroughChildEvents(StrategicPlan plan)
        {
            for (int stage = 0; stage < 8 && !plan.IsTerminal; stage++)
            {
                StrategicMilestone milestone = plan.CurrentMilestone;
                int[] children = milestone.RequiredChildGoals.ToArray();
                if (children.Length == 0)
                {
                    Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
                    continue;
                }
                foreach (int id in children)
                {
                    CommanderGoal child = goals.GetGoal(id);
                    child.SetStatus(CommanderGoalStatus.Completed, "Deterministic test fixture completion.");
                    typeof(StrategicPlanner).GetMethod("HandleGoalEvent",
                        BindingFlags.NonPublic | BindingFlags.Instance).Invoke(planner,
                            new object[] { new CommanderGoalEvent(CommanderGoalEventType.GoalCompleted,
                                simulation.CurrentTick, child) });
                }
                Assert.That(plan.CurrentMilestone, Is.Not.SameAs(milestone));
            }
        }

        private void AdvanceToSpendingMilestone(StrategicPlan plan)
        {
            StrategicMilestone economy = plan.CurrentMilestone;
            foreach (int id in economy.RequiredChildGoals.ToArray())
            {
                CommanderGoal child = goals.GetGoal(id);
                child.SetStatus(CommanderGoalStatus.Completed, "Deterministic test fixture completion.");
                typeof(StrategicPlanner).GetMethod("HandleGoalEvent",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(planner,
                        new object[] { new CommanderGoalEvent(CommanderGoalEventType.GoalCompleted,
                            simulation.CurrentTick, child) });
            }
            Assert.That(plan.CurrentMilestone, Is.Not.SameAs(economy));
        }

        private int Resource(ResourceType type)
        {
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return stock.Food;
                case ResourceType.Wood: return stock.Wood;
                case ResourceType.Gold: return stock.Gold;
                case ResourceType.Stone: return stock.Stone;
                default: return 0;
            }
        }

        private sealed class HeldStrategicProvider : IStrategicAIInterpreter
        {
            private TaskCompletionSource<StrategicAIProviderResult> gate;
            private StrategicAIRequest request;
            public int Calls { get; private set; }
            public string Json { get; set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest value, CancellationToken token)
            {
                Calls++;
                request = value;
                gate = new TaskCompletionSource<StrategicAIProviderResult>();
                return gate.Task;
            }
            public void Release() => gate.TrySetResult(Json == null
                ? new MockStrategicAIProvider()
                    .InterpretStrategicIntentAsync(request, CancellationToken.None).Result
                : StrategicAIJson.Parse(Json, request));
        }
    }
}
