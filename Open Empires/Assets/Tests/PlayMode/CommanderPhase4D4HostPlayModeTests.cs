using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D4Host")]
    public sealed class CommanderPhase4D4HostPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CountingStrategicProvider provider;

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
            provider = new CountingStrategicProvider();
            chat = new GameObject("Phase4D4HostChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
            chat.InitializeStrategic(provider, pipeline);
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
        public IEnumerator Advisory_DoesNotExecuteAnything()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int reservations = planner.Reservations.Count;
            int providerCalls = provider.Calls;
            Pause(plan);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(provider.Calls, Is.EqualTo(providerCalls));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_DoesNotCallProvider()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            int calls = provider.Calls;
            Pause(plan);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(provider.Calls, Is.EqualTo(calls));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_PauseResumeTransition()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int calls = provider.Calls;
            Pause(plan);
            int pausedAt = chat.DisplayedTranscript.IndexOf("Plan paused.", StringComparison.Ordinal);
            Assert.That(pausedAt, Is.GreaterThanOrEqualTo(0));
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            Assert.That(planner.ApplyControl(resume).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Assert.That(chat.DisplayedTranscript.IndexOf("Plan resumed.", StringComparison.Ordinal),
                Is.GreaterThan(pausedAt));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(provider.Calls, Is.EqualTo(calls));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_PopulationRecovery()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            for (int i = 0; i < 2; i++)
                Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Force"));
            Observe();
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            while (simulation.GetPopulation(0) < simulation.GetPopulationCap(0))
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            StrategicPlanHealthSnapshot waiting = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(waiting.PrimaryHealthCategory == StrategicPlanHealthCategory.WaitingForPopulation
                || System.Linq.Enumerable.Contains(waiting.SecondaryHealthCategories,
                    StrategicPlanHealthCategory.WaitingForPopulation), Is.True);
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int reservations = planner.Reservations.Count;
            simulation.Tick();
            Observe();
            Assert.That(chat.DisplayedTranscript, Does.Contain("waiting for population"));
            simulation.CreateBuilding(0, BuildingType.House, x + 20, z + 12, false, true);
            simulation.Tick();
            Observe();
            Assert.That(chat.DisplayedTranscript, Does.Contain("recovered from population wait"));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_CancelAndComplete()
        {
            StrategicPlan cancelled = null;
            yield return ApproveRanged(value => cancelled = value);
            Observe();
            Assert.That(planner.CaptureControlRequest(0, cancelled.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan cancelled."));
            Assert.That(planner.ActivePlans, Is.Empty);
            StrategicPlan completed = StartDirectPlan();
            Observe();
            for (int i = 0; i < 8 && !completed.IsTerminal; i++)
            {
                if (completed.CurrentMilestone.Status != StrategicMilestoneStatus.Active) break;
                Assert.That(planner.CompleteMilestoneAndAdvance(completed.StrategicPlanId), Is.True);
            }
            Assert.That(completed.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan completed."));
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(1));
            Assert.That(planner.Plans.Count, Is.EqualTo(2));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_NonSelectedPlanTransition()
        {
            StrategicPlan selected = null;
            yield return ApproveRanged(value => selected = value);
            StrategicPlan other = StartDirectPlan();
            Observe();
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int reservations = planner.Reservations.Count;
            Pause(other);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(selected.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_NonSelectedHealthOnlyChangeUsesPeriodicScan()
        {
            StrategicPlan selected = null;
            yield return ApproveRanged(value => selected = value);
            StrategicPlan other = StartDirectPlan();
            for (int i = 0; i < 2; i++)
                Assert.That(planner.CompleteMilestoneAndAdvance(other.StrategicPlanId), Is.True);
            Assert.That(other.CurrentMilestone.Name, Is.EqualTo("Force"));
            typeof(CommanderChatUI).GetField("selectedPlanId",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(chat, selected.StrategicPlanId);
            Observe();
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int reservations = planner.Reservations.Count;
            int providerCalls = provider.Calls;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            while (simulation.GetPopulation(0) < simulation.GetPopulationCap(0))
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(0, other.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory == StrategicPlanHealthCategory.WaitingForPopulation
                || System.Linq.Enumerable.Contains(health.SecondaryHealthCategories,
                    StrategicPlanHealthCategory.WaitingForPopulation), Is.True);
            string before = chat.DisplayedTranscript;
            simulation.Tick(); // no planner status, milestone, or goal callback
            Observe();
            Assert.That(chat.DisplayedTranscript, Is.Not.EqualTo(before));
            Assert.That(chat.DisplayedTranscript,
                Does.Contain("Plan " + other.StrategicPlanId + " waiting for population"));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(provider.Calls, Is.EqualTo(providerCalls));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_ForeignSimulationPipelineRejectedBeforeHostMutation()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            string transcript = chat.DisplayedTranscript;
            ConversationState conversation = chat.Conversation;
            int decisions = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int goalsBefore = goals.Goals.Count;
            int reservations = planner.Reservations.Count;
            int calls = provider.Calls;
            var foreignSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            var foreignGoals = new CommanderGoalManager(foreignSimulation, 0);
            var foreignPlanner = new StrategicPlanner(foreignGoals, _ => 5000);
            var foreignPipeline = new StrategicPipeline(foreignSimulation, foreignGoals,
                foreignPlanner);
            try
            {
                Assert.Throws<ArgumentException>(() => chat.InitializeStrategic(provider,
                    foreignPipeline));
                Assert.That(chat.Conversation, Is.SameAs(conversation));
                Assert.That(chat.DisplayedTranscript, Is.EqualTo(transcript));
                Pause(plan); // existing subscription survives rejection
                Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
                Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
                Assert.That(planner.Plans.Count, Is.EqualTo(plans));
                Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
                Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
                Assert.That(provider.Calls, Is.EqualTo(calls));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally
            {
                foreignPipeline.Dispose();
                foreignPlanner.Dispose();
                foreignGoals.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Advisory_UnboundContextPipelineRejectedBeforeHostMutation()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            string transcript = chat.DisplayedTranscript;
            ConversationState conversation = chat.Conversation;
            var foreignSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreignSimulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            using var foreignGoals = new CommanderGoalManager(foreignSimulation, 0);
            using var otherPlanner = new StrategicPlanner(goals, Resource);
            using var unboundPipeline = new StrategicPipeline(otherPlanner,
                () => new CommanderContextBuilder().Build(foreignSimulation, foreignGoals));

            Assert.Throws<ArgumentException>(() => chat.InitializeStrategic(provider, unboundPipeline));
            Assert.That(chat.Conversation, Is.SameAs(conversation));
            Assert.That(chat.DisplayedTranscript, Is.EqualTo(transcript));
            Pause(plan);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."),
                "The original pipeline must stay bound after rejecting an unverifiable context.");
        }

        [UnityTest]
        public IEnumerator Advisory_SameTickEventsAreNotLost()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            Observe(); // periodic scan has already run in this simulation tick
            int tick = simulation.CurrentTick;
            int decisions = pipeline.DecisionHistory.History.Count;
            Pause(plan);
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            planner.ApplyControl(resume);
            Assert.That(simulation.CurrentTick, Is.EqualTo(tick));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan resumed."));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisions));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_SamePipelineReinitializeClearsState()
        {
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            Observe();
            Pause(plan);
            chat.InitializeStrategic(provider, pipeline);
            string before = chat.DisplayedTranscript;
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            planner.ApplyControl(resume);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan resumed."),
                "A real paused-to-running transition immediately after rebind must be observed.");
            Assert.That(chat.DisplayedTranscript.Length, Is.GreaterThan(before.Length));
            Pause(plan);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(1));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Advisory_ResetRejectsOldProviderAndEvent()
        {
            StrategicPlan old = null;
            yield return ApproveRanged(value => old = value);
            Observe();
            var held = new HeldStrategicProvider();
            chat.InitializeStrategic(held, pipeline);
            Task<CommanderAIChatSubmission> pending = chat.SubmitMessageAsync("prepare defenses");
            Assert.That(held.Calls, Is.EqualTo(1));
            chat.ResetConversation();
            string resetTranscript = chat.DisplayedTranscript;
            held.Release();
            while (!pending.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Is.EqualTo(resetTranscript));
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(planner.CaptureControlRequest(0, old.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            planner.ApplyControl(pause);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."),
                "A real lifecycle transition immediately after reset must not be lost.");
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            CommanderGoalManager oldGoals = goals;
            StrategicPlanner oldPlanner = planner;
            StrategicPipeline oldPipeline = pipeline;
            CommanderIntentDispatcher oldDispatcher = dispatcher;
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
            chat.InitializeStrategic(provider, pipeline);
            StrategicPlan replacement = StartDirectPlan();
            Assert.That(replacement.StrategicPlanId, Is.EqualTo(old.StrategicPlanId));
            Observe();
            string replacementTranscript = chat.DisplayedTranscript;
            typeof(CommanderChatUI).GetMethod("OnHostPlanStatusChanged",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chat, new object[] { old });
            Assert.That(chat.DisplayedTranscript, Is.EqualTo(replacementTranscript));
            Pause(replacement);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Plan paused."));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.Zero);
            Assert.That(provider.Calls, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            oldDispatcher.Dispose();
            oldPipeline.Dispose();
            oldPlanner.Dispose();
            oldGoals.Dispose();
        }

        private IEnumerator ApproveRanged(Action<StrategicPlan> receive)
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare ranged reinforcements");
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False, request.Exception?.ToString());
            StrategicDecisionRecord decision = chat.ApproveStrategicRecommendation();
            Assert.That(decision?.Submission?.CreatedPlan, Is.True, decision?.Outcome);
            receive(decision.Submission.Plan);
        }

        private void Observe() => typeof(CommanderChatUI).GetMethod("LateUpdate",
            BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chat, null);

        private void Pause(StrategicPlan plan)
        {
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest request), Is.True);
            Assert.That(planner.ApplyControl(request).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
        }

        private StrategicPlan StartDirectPlan()
        {
            var request = new StrategicAIRequest("prepare ranged reinforcements",
                pipeline.CaptureContext(), planner.IntentIds);
            StrategicIntent intent = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, CancellationToken.None).Result.Intent;
            StrategicIntentSubmission submitted = planner.SubmitIntent(intent);
            Assert.That(submitted.CreatedPlan, Is.True, submitted.Reason);
            return submitted.Plan;
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

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int Calls { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken token)
            {
                Calls++;
                return new MockStrategicAIProvider().InterpretStrategicIntentAsync(request, token);
            }
        }

        private sealed class HeldStrategicProvider : IStrategicAIInterpreter
        {
            private readonly TaskCompletionSource<StrategicAIProviderResult> gate =
                new TaskCompletionSource<StrategicAIProviderResult>();
            private StrategicAIRequest request;
            public int Calls { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest value, CancellationToken token)
            {
                request = value;
                Calls++;
                return gate.Task;
            }
            public void Release() => gate.TrySetResult(new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, CancellationToken.None).Result);
        }
    }
}
