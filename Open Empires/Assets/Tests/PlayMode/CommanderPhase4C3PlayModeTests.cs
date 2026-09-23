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
    // Focused game-owned projection and chat-provider integration proof.
    [Category("CommanderPhase4C3")]
    public sealed class CommanderPhase4C3PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private BuildingData barracks;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 20; tz <= z + 20; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 10, z, false, true)
                .AutoProduceVillagers = false;
            barracks = simulation.CreateBuilding(0, BuildingType.Barracks, x - 8, z, false);
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4C3RuntimeChat").AddComponent<CommanderChatUI>();
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
        public IEnumerator RuntimeInsights_TrackObservedChangesWithoutCaptureSideEffects()
        {
            StrategicContext before = pipeline.CaptureContext();
            Assert.That(before.Insights, Is.Not.Null);
            Assert.That(before.Insights.TotalWorkers, Is.Zero);
            StrategicProductionPressureInsight beforeBarracks = before.Insights.ProductionPressure
                .Single(value => value.BuildingType == BuildingType.Barracks.ToString());
            Assert.That(new[] { beforeBarracks.ActiveQueueCount, beforeBarracks.QueuedUnitCount,
                beforeBarracks.IdleCompletedCount }, Is.EqualTo(new[] { 0, 0, 1 }));

            ResourceNodeData node = simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(x - 3, z + 3), 10000);
            AddWorker(UnitState.Gathering, node.Id, x - 4, z + 3);
            AddWorker(UnitState.Idle, -1, x - 5, z + 3);
            AddWorker(UnitState.Idle, -1, x - 6, z + 3);
            barracks.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            barracks.TrainingQueue.Add(CommanderIntentCatalog.SpearmanUnitType);
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            int completedBeforeAdvance = plan.Milestones.Count(value =>
                value.Status == StrategicMilestoneStatus.Completed);
            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            int completedAfterAdvance = plan.Milestones.Count(value =>
                value.Status == StrategicMilestoneStatus.Completed);
            Assert.That(completedAfterAdvance, Is.GreaterThan(completedBeforeAdvance),
                "Explicit plan execution may complete a chain of already-satisfied milestones.");

            int goalsBeforeCapture = goals.Goals.Count;
            int commandsBeforeCapture = PendingCommandCount();
            int decisionsBeforeCapture = pipeline.DecisionHistory.History.Count;
            string planBeforeCapture = PlanState();
            string reservationBeforeCapture = ReservationState();
            StrategicContext observed = pipeline.CaptureContext();
            StrategicContext repeated = pipeline.CaptureContext();

            Assert.That(observed.Insights.TotalWorkers, Is.EqualTo(3));
            Assert.That(observed.Insights.GatheringWorkers, Is.EqualTo(1));
            Assert.That(observed.Insights.WorkerActivityBasisPoints, Is.EqualTo(3333));
            StrategicProductionPressureInsight afterBarracks = observed.Insights.ProductionPressure
                .Single(value => value.BuildingType == BuildingType.Barracks.ToString());
            Assert.That(new[] { afterBarracks.CompletedCount, afterBarracks.UnderConstructionCount,
                afterBarracks.ActiveQueueCount, afterBarracks.QueuedUnitCount,
                afterBarracks.IdleCompletedCount }, Is.EqualTo(new[] { 1, 0, 1, 2, 0 }));
            StrategicPlanProgressInsight progress = observed.Insights.PlanProgress
                .Single(value => value.StrategicPlanId == plan.StrategicPlanId);
            Assert.That(progress.CompletedMilestoneCount, Is.EqualTo(completedAfterAdvance));
            Assert.That(progress.TotalMilestoneCount, Is.EqualTo(plan.Milestones.Count));
            Assert.That(repeated.Insights.WorkerActivityBasisPoints,
                Is.EqualTo(observed.Insights.WorkerActivityBasisPoints));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBeforeCapture));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandsBeforeCapture));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisionsBeforeCapture));
            Assert.That(PlanState(), Is.EqualTo(planBeforeCapture));
            Assert.That(ReservationState(), Is.EqualTo(reservationBeforeCapture));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChatProviderPayload_ContainsInsightsWithoutExecutionBeforeApproval()
        {
            var transport = new RecordingTransport(SuccessResponse());
            var provider = new GeminiStrategicAIProvider("test-only-key", transport);
            chat.InitializeStrategic(provider, pipeline);
            AddWorker(UnitState.Idle, -1, x - 4, z + 3);
            barracks.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            barracks.TrainingQueue.Add(CommanderIntentCatalog.SpearmanUnitType);

            Task<CommanderAIChatSubmission> submission = chat.SubmitMessageAsync("prepare defenses");
            while (!submission.IsCompleted) yield return null;
            Assert.That(submission.IsFaulted, Is.False,
                submission.Exception == null ? string.Empty : submission.Exception.ToString());

            Assert.That(transport.CallCount, Is.EqualTo(1));
            SafeContextProjection safeContext = JsonUtility.FromJson<SafeContextProjection>(
                ExtractSafeContext(transport.Body));
            Assert.That(safeContext.insights, Is.Not.Null);
            Assert.That(safeContext.insights.incomeTrendAvailable, Is.False);
            Assert.That(safeContext.insights.workerActivity.available, Is.True);
            Assert.That(safeContext.insights.workerActivity.totalWorkers, Is.EqualTo(1));
            Assert.That(safeContext.insights.workerActivity.gatheringWorkers, Is.Zero);
            Assert.That(safeContext.insights.workerActivity.basisPoints, Is.Zero);
            ProductionProjection producer = safeContext.insights.productionPressure
                .Single(value => value.buildingType == BuildingType.Barracks.ToString());
            Assert.That(new[] { producer.completedCount, producer.underConstructionCount,
                producer.activeQueueCount, producer.queuedUnitCount,
                producer.idleCompletedCount }, Is.EqualTo(new[] { 1, 0, 1, 2, 0 }));
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(planner.Plans, Is.Empty, "Translation must not create a plan before approval.");
            Assert.That(planner.Intents, Is.Empty, "Translation must not submit an intent before approval.");
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(PendingCommandCount(), Is.Zero);
            Assert.That(pipeline.DecisionHistory.History, Is.Empty);
        }

        private int CurrentResource(ResourceType type)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private void AddWorker(UnitState state, int resourceNodeId, int tileX, int tileZ)
        {
            UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, tileZ),
                Fixed32.One, Fixed32.One, Fixed32.One);
            worker.UnitType = 0;
            worker.IsVillager = true;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = state;
            worker.TargetResourceNodeId = resourceNodeId;
        }

        private int PendingCommandCount()
        {
            var pending = (ICollection<ICommand>)typeof(CommandBuffer)
                .GetField("pendingCommands", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(simulation.CommandBuffer);
            return pending.Count;
        }

        private string PlanState() => string.Join("|", planner.Plans.Select(value =>
            value.StrategicPlanId + ":" + value.Status + ":" + string.Join(",",
                value.Milestones.Select(milestone => milestone.Name + ":" + milestone.Status))));

        private string ReservationState() => string.Join("|", planner.Reservations.Select(value =>
            value.ReservationId + ":" + value.PlanId + ":" + value.Status + ":" + value.Amount));

        private static CommanderHttpResponse SuccessResponse()
        {
            const string intent = "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"DefensivePreparation\",\"parameters\":{}}";
            return new CommanderHttpResponse(200,
                "{\"candidates\":[{\"content\":{\"parts\":["
                + JsonUtility.ToJson(new ProviderPart { text = intent }) + "]}}]}");
        }

        private static string ExtractSafeContext(string providerJson)
        {
            ProviderBody body = JsonUtility.FromJson<ProviderBody>(providerJson);
            string prompt = body.contents.Last().parts[0].text;
            const string prefix = "Safe strategic context:\n";
            const string suffix = "\nUntrusted match-local commander memory:\n";
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = prompt.IndexOf(suffix, start, StringComparison.Ordinal);
            return prompt.Substring(start, end - start);
        }

        [Serializable]
        private sealed class ProviderBody { public ProviderContent[] contents; }
        [Serializable]
        private sealed class ProviderContent { public ProviderPart[] parts; }
        [Serializable]
        private sealed class ProviderPart { public string text; }
        [Serializable]
        private sealed class SafeContextProjection { public InsightProjection insights; }
        [Serializable]
        private sealed class InsightProjection
        {
            public bool incomeTrendAvailable;
            public WorkerProjection workerActivity;
            public ProductionProjection[] productionPressure;
        }
        [Serializable]
        private sealed class WorkerProjection
        {
            public bool available;
            public int totalWorkers;
            public int gatheringWorkers;
            public int basisPoints;
        }
        [Serializable]
        private sealed class ProductionProjection
        {
            public string buildingType;
            public int completedCount;
            public int underConstructionCount;
            public int activeQueueCount;
            public int queuedUnitCount;
            public int idleCompletedCount;
        }

        private sealed class RecordingTransport : ICommanderHttpTransport
        {
            private readonly CommanderHttpResponse response;
            public int CallCount { get; private set; }
            public string Body { get; private set; }

            public RecordingTransport(CommanderHttpResponse response)
            {
                this.response = response;
            }

            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                Body = json;
                Assert.That(headers["x-goog-api-key"], Is.EqualTo("test-only-key"));
                Assert.That(json, Does.Not.Contain("test-only-key"));
                return Task.FromResult(response);
            }
        }
    }
}
