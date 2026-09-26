using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E1")]
    public sealed class CommanderPhase4E1StrategicPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private SemanticStrategicProvider provider;
        private SimulationConfig replacementConfig;
        private GameSimulation replacementSimulation;
        private CommanderGoalManager replacementGoals;
        private CommanderIntentDispatcher replacementDispatcher;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(simulation))[0] = 3;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true)
                .AutoProduceVillagers = false;
            var enemy = simulation.MapData.BasePositions[1];
            simulation.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true)
                .AutoProduceVillagers = false;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            for (int i = 0; i < 6; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
            Gatherers(ResourceType.Food, 8, x - 22, z);
            Gatherers(ResourceType.Wood, 8, x - 10, z);

            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("CommanderPhase4E1StrategicChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            provider = new SemanticStrategicProvider();
            chat.Initialize(provider, simulation, goals, dispatcher);
            chat.InitializeStrategic(provider, pipeline);
            chat.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            replacementDispatcher?.Dispose();
            replacementGoals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            if (replacementConfig != null) UnityEngine.Object.DestroyImmediate(replacementConfig);
        }

        [UnityTest]
        public IEnumerator StrategicParaphrase_StagesRecommendationUntilOrdinaryApproval()
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "Could we prepare a stronger ranged force? ");
            while (!request.IsCompleted) yield return null;

            StrategicIntent pending = chat.PendingStrategicIntent;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(pending.PlayerId, Is.EqualTo(0));
            Assert.That(planner.Plans, Is.Empty, "Semantic interpretation must not create a plan.");
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            StrategicDecisionRecord approval = chat.ApproveStrategicRecommendation();
            Assert.That(approval, Is.Not.Null);
            Assert.That(approval.Submission?.CreatedPlan, Is.True, approval.Outcome);
            Assert.That(approval.Submission.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActiveAiPlan_RequiresExplicitConfirmForSemanticAdaptation()
        {
            Task<CommanderAIChatSubmission> firstRequest = chat.SubmitMessageAsync(
                "Build up a stronger ranged force.");
            while (!firstRequest.IsCompleted) yield return null;
            StrategicDecisionRecord firstApproval = chat.ApproveStrategicRecommendation();
            Assert.That(firstApproval?.Submission?.CreatedPlan, Is.True, firstApproval?.Outcome);
            StrategicPlan ranged = firstApproval.Submission.Plan;
            Assert.That(ranged.PlanType, Is.EqualTo(StrategicPlanType.RangedReinforcement));

            provider.Json = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"StrategicObjective\",\"objective\":\"DefensivePreparation\"}]}";
            Task<CommanderAIChatSubmission> secondRequest = chat.SubmitMessageAsync(
                "Please fortify our defenses.");
            while (!secondRequest.IsCompleted) yield return null;

            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null,
                "Ordinary Approve must not replace an active AI plan.");
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Active));

            StrategicDecisionRecord confirmation = chat.ConfirmStrategicCommand();
            Assert.That(confirmation?.Submission?.CreatedPlan, Is.True, confirmation?.Outcome);
            Assert.That(confirmation.Submission.Intent.Source,
                Is.EqualTo(StrategicIntentSource.AIConfirmedPlayerCommand));
            Assert.That(confirmation.Submission.Plan.PlanType,
                Is.EqualTo(StrategicPlanType.DefensivePreparation));
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.Plans.Count, Is.EqualTo(2),
                "The cancelled source plan remains in history alongside its approved replacement.");
        }

        [UnityTest]
        public IEnumerator ResetDuringSemanticTranslation_RejectsLateStrategicResult()
        {
            provider.Hold = true;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "Prepare our ranged units.");
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));

            chat.ResetConversation();
            provider.Release();
            while (!request.IsCompleted) yield return null;

            Assert.That(request.IsFaulted, Is.False, request.Exception?.ToString());
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.LatestStrategicInterpretation, Is.Null);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator OwnerRuntimeReplacement_RejectsLateSemanticStrategicResult()
        {
            provider.Hold = true;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "Prepare our ranged units.");
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));

            replacementConfig = ScriptableObject.CreateInstance<SimulationConfig>();
            replacementSimulation = new GameSimulation(replacementConfig, 2,
                new[] { 0, 1 }, Array.Empty<int>());
            replacementGoals = new CommanderGoalManager(replacementSimulation, 1);
            replacementDispatcher = new CommanderIntentDispatcher(replacementSimulation,
                replacementGoals);
            chat.Initialize(provider, replacementSimulation, replacementGoals,
                replacementDispatcher);
            provider.Release();
            while (!request.IsCompleted) yield return null;

            Assert.That(request.IsFaulted, Is.False, request.Exception?.ToString());
            Assert.That(chat.Conversation.PlayerId, Is.EqualTo(1));
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.LatestStrategicInterpretation, Is.Null);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(replacementGoals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(replacementSimulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator BusyHost_DoesNotStartASecondSemanticRequest()
        {
            provider.Hold = true;
            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync(
                "Prepare our ranged units.");
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));

            Task<CommanderAIChatSubmission> second = chat.SubmitMessageAsync(
                "We need a stronger ranged force.");

            Assert.That(second.IsCompleted, Is.True);
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            provider.Release();
            while (!first.IsCompleted) yield return null;
            Assert.That(first.IsFaulted, Is.False, first.Exception?.ToString());
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(provider.SemanticCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MixedSemanticRequest_AdmitsNoStrategicNode()
        {
            provider.Json = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}";

            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("Prepare this and recruit spearmen.");
            while (!request.IsCompleted) yield return null;

            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private void Gatherers(ResourceType resource, int count, int x, int z)
        {
            ResourceNodeData node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(x + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                var unit = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + i, z - 12), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 0;
                unit.IsVillager = true;
                unit.MaxHealth = unit.CurrentHealth = 100;
                unit.State = UnitState.Gathering;
                unit.FinalDestination = unit.SimPosition;
                unit.TargetResourceNodeId = node.Id;
            }
        }

        private int Resource(ResourceType resource)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (resource)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private sealed class SemanticStrategicProvider : ICommanderAIProvider,
            ICommanderSemanticProvider, IStrategicAIInterpreter
        {
            private TaskCompletionSource<bool> release;
            public string Json = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}";
            public bool Hold { get; set; }
            public int SemanticCalls { get; private set; }

            public async Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                SemanticCalls++;
                if (Hold)
                {
                    release = new TaskCompletionSource<bool>();
                    await release.Task;
                }
                return CommanderSemanticJson.Parse(Json);
            }

            public void Release() => release?.TrySetResult(true);

            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken) =>
                Task.FromResult(StrategicAIProviderResult.Rejected("The legacy strategic route must not run."));

            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "The tactical route must not run."));
        }
    }
}
