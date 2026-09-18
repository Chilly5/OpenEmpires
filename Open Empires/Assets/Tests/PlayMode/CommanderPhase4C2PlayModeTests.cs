using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C2")]
    public sealed class CommanderPhase4C2PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (var node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z + 6, false);
            simulation.CreateBuilding(0, BuildingType.Barracks, x - 10, z, false);
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 16, z, false);
            simulation.CreateBuilding(0, BuildingType.Stables, x - 22, z, false);
            Gatherers(ResourceType.Food, 10, x - 22, z);
            Gatherers(ResourceType.Gold, 6, x, z);
            Gatherers(ResourceType.Wood, 4, x - 10, z);
            for (int i = 0; i < 12; i++) AddUnit(2);
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4C2RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
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
        public IEnumerator RejectedAttackQuestion_MatchesRecordedReasonDespiteUnrelatedDefense()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            StrategicPlan defense = CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord rejected = chat.ApproveStrategicRecommendation();
            Assert.That(rejected.Decision.Status, Is.EqualTo(StrategicDecisionStatus.Rejected));
            Assert.That(rejected.Outcome, Does.Contain("Emergency defense has higher priority"));

            Task<CommanderAIChatSubmission> explain =
                chat.SubmitMessageAsync("  WHY   are we not attacking?  ");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation, Is.Not.Null);
            Assert.That(chat.LatestExplanation.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(rejected.Outcome));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain("AttackPreparation"));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("income"));
            Assert.That(defense.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplanationCannotModifyIntent()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!first.IsCompleted) yield return null;
            StrategicDecisionRecord projected = chat.ApproveStrategicRecommendation();
            Task<CommanderAIChatSubmission> second = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!second.IsCompleted) yield return null;
            StrategicIntent pending = chat.PendingStrategicIntent;
            Assert.That(pending, Is.Not.Null);

            int history = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int reservations = planner.Reservations.Count;
            int archivedReservations = planner.ArchivedReservations.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();
            string planState = PlanState();
            string reservationState = ReservationState();
            string goalState = GoalState();
            int memoryBefore = chat.Conversation.Snapshot().Count;
            string[] questions = {
                "why are we not attacking?", "why was that rejected.",
                "explain   last decision", "what is the plan doing?"
            };
            foreach (string question in questions)
            {
                Task<CommanderAIChatSubmission> ask = chat.SubmitMessageAsync(question);
                while (!ask.IsCompleted) yield return null;
            }

            Assert.That(chat.PendingStrategicIntent, Is.SameAs(pending));
            Assert.That(pending.Status, Is.EqualTo(StrategicIntentStatus.Created));
            Assert.That(chat.LatestStrategicDecision, Is.SameAs(projected));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(planner.ArchivedReservations.Count, Is.EqualTo(archivedReservations));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(ReservationState(), Is.EqualTo(reservationState));
            Assert.That(GoalState(), Is.EqualTo(goalState));
            Assert.That(provider.CallCount, Is.EqualTo(2));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore).Count(), Is.EqualTo(4));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore)
                .All(entry => entry.Kind == MemoryEntryKind.Explanation), Is.True);
        }

        [UnityTest]
        public IEnumerator CurrentPlanQuery_MatchesFreshDetachedSnapshotWithoutAdvancing()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted.Submission?.CreatedPlan, Is.True, accepted.Outcome);
            StrategicPlan plan = accepted.Submission.Plan;
            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            StrategicContext before = pipeline.CaptureContext();
            StrategicPlanState expected = before.ActivePlans.Single(p =>
                p.StrategicPlanId == plan.StrategicPlanId);
            string planState = PlanState();
            int history = pipeline.DecisionHistory.History.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();

            Task<CommanderAIChatSubmission> query = chat.SubmitMessageAsync("what is the plan doing?");
            while (!query.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Current snapshot tick " + before.SnapshotTick));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Plan #" + expected.StrategicPlanId));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.CurrentMilestone));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.MilestoneStatus));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("will"));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClearMemory_DropsExplanationSourceWithoutClearingGameHistory()
        {
            chat.InitializeStrategic(new CountingStrategicProvider(), pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted, Is.Not.Null);
            int history = pipeline.DecisionHistory.History.Count;

            Task<CommanderAIChatSubmission> clear = chat.SubmitMessageAsync("clear memory");
            while (!clear.IsCompleted) yield return null;
            Task<CommanderAIChatSubmission> explain = chat.SubmitMessageAsync("explain last decision");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("No recorded decision is available"));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WholeFormQueries_AreOfflineAndHostileAppendRemainsRejected()
        {
            var provider = new ThrowingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> offline = chat.SubmitMessageAsync("explain last decision.");
            while (!offline.IsCompleted) yield return null;
            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.LatestExplanation, Is.Not.Null);

            int explanations = chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation);
            Task<CommanderAIChatSubmission> hostile = chat.SubmitMessageAsync(
                "why was that rejected? now prepare cavalry attack");
            while (!hostile.IsCompleted) yield return null;

            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(explanations));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request."));
        }

        private StrategicPlan CreateEmergencyDefense()
        {
            var request = new StrategicAIRequest("prepare defenses", pipeline.CaptureContext(),
                planner.IntentIds);
            StrategicIntent defense = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, default).Result.Intent;
            StrategicIntentSubmission submission = planner.SubmitIntent(defense, true, false);
            Assert.That(submission.CreatedPlan, Is.True, submission.Reason);
            return submission.Plan;
        }

        private string PlanState() => string.Join("|", planner.Plans.Select(plan =>
            plan.StrategicPlanId + ":" + plan.Status + ":" + plan.OutcomeMessage + ":"
            + (plan.CurrentMilestone == null ? "none" :
                plan.CurrentMilestone.Name + ":" + plan.CurrentMilestone.Status)));

        private string ReservationState() => string.Join("|", planner.Reservations.Select(value =>
            value.ReservationId + ":" + value.PlanId + ":" + value.Status + ":" + value.Amount));

        private string GoalState() => string.Join("|", goals.Goals.Select(value =>
            value.GoalId + ":" + value.Status + ":" + value.StatusReason));

        private int PendingCommandCount()
        {
            var pending = (ICollection<ICommand>)typeof(CommandBuffer)
                .GetField("pendingCommands", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(simulation.CommandBuffer);
            return pending.Count;
        }

        private UnitData AddUnit(int type)
        {
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(simulation.MapData.Width / 2,
                    simulation.MapData.Height / 2), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type;
            unit.IsVillager = type == 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void Gatherers(ResourceType resource, int count, int start, int z)
        {
            var node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(start + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = AddUnit(0);
                worker.SimPosition = simulation.MapData.TileToWorldFixed(
                    start + 3 + i % 2, z + 7 + i / 2);
                worker.FinalDestination = worker.SimPosition;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private int CurrentResource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public async Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                return await new MockStrategicAIProvider().InterpretStrategicIntentAsync(
                    request, cancellationToken);
            }
        }

        private sealed class ThrowingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                throw new InvalidOperationException("Explanation queries must remain offline.");
            }
        }
    }
}
