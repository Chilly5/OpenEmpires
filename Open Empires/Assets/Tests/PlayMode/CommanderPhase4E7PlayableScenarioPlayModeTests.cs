using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E7")]
    public sealed class CommanderPhase4E7PlayableScenarioPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private SemanticProvider provider;
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
            typeof(MapData).GetField("holeMap", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance).SetValue(simulation.MapData, null);
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            for (int tileX = x - 30; tileX <= x + 30; tileX++)
                for (int tileZ = z - 20; tileZ <= z + 20; tileZ++)
                {
                    simulation.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    simulation.MapData.ForestDensity[tileX, tileZ] = 0;
                    simulation.MapData.FoundationCount[tileX, tileZ] = 0;
                    simulation.FogOfWar.SetVisible(0, tileX, tileZ);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 14, z,
                false, true).AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            for (int i = 0; i < 3; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 18 + i * 3, z, false);
            simulation.CreateBuilding(1, BuildingType.TownCenter, x + 25, z,
                false, true).AutoProduceVillagers = false;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            provider = new SemanticProvider();
            chat = new GameObject("CommanderPhase4E7PlayableChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(provider, simulation, goals, dispatcher);
            chat.enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_NaturalSemanticRequest_ExecutesTenSpearmenThroughNormalCommands()
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "hey I want 10 spearmen");
            while (!request.IsCompleted) yield return null;

            Assert.That(request.Result, Is.Not.Null);
            Assert.That(request.Result.Success, Is.True, request.Result?.DisplayText);
            EnsureUnitCountGoal goal = goals.Goals.OfType<EnsureUnitCountGoal>().Single();
            Assert.That(goal.TargetTotal, Is.EqualTo(10));

            for (int i = 0; i < 15000 && !goal.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 300 == 0) yield return null;
            }

            int spearmen = simulation.UnitRegistry.GetAllUnits().Count(unit =>
                unit.PlayerId == 0 && unit.UnitType == CommanderIntentCatalog.SpearmanUnitType
                && unit.CurrentHealth > 0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(spearmen, Is.GreaterThanOrEqualTo(10));
            Debug.Log($"[Phase4E7 Playable Runtime] PASS semantic natural request -> {spearmen} live spearmen; tick={simulation.CurrentTick}.");
        }

        [UnityTest]
        public IEnumerator Runtime_AmbiguousSemanticReply_ShowsClarificationWithoutGoal()
        {
            provider.Json = "{\"outcome\":\"Clarify\",\"message\":\"Which Town Center should I use?\"}";
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "make my base better");
            while (!request.IsCompleted) yield return null;

            Assert.That(goals.Goals, Is.Empty);
            StringAssert.Contains("Which Town Center should I use?", chat.DisplayedTranscript);
            Assert.That(chat.Conversation.SemanticMemory.Snapshot(), Has.Count.EqualTo(1));
            yield return null;
        }

        private sealed class SemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public string Json { get; set; } = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}";

            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "Legacy route was called."));

            public Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token) =>
                Task.FromResult(CommanderSemanticJson.Parse(Json));
        }
    }
}
