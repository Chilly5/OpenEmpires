using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4A")]
    public sealed class CommanderPhase4APlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderAIIntentAdapter adapter;
        private ResourceNodeData woodNode;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);

            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            for (int tileX = x - 25; tileX <= x + 25; tileX++)
                for (int tileZ = z - 18; tileZ <= z + 18; tileZ++)
                {
                    simulation.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    simulation.MapData.ForestDensity[tileX, tileZ] = 0;
                    simulation.MapData.FoundationCount[tileX, tileZ] = 0;
                    simulation.FogOfWar.SetVisible(0, tileX, tileZ);
                }

            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 14, z,
                false, true).AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 21, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 24, z, false);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;

            woodNode = simulation.MapData.AddResourceNode(ResourceType.Wood,
                simulation.MapData.TileToWorldFixed(x - 8, z + 5), 10000);
            for (int i = 0; i < 8; i++) CreateWorker(x - 3 + i % 4, z + i / 4);

            goalManager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goalManager);
            adapter = new CommanderAIIntentAdapter(new MockAIProvider(), simulation,
                goalManager, dispatcher);
        }

        [TearDown]
        public void TearDown()
        {
            dispatcher?.Dispose();
            goalManager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_MockNaturalLanguageProducesTenSpearmen()
        {
            var submitTask = adapter.SubmitAsync("make 10 spearmen");
            while (!submitTask.IsCompleted) yield return null;
            CommanderAIChatSubmission submission = submitTask.Result;
            Assert.That(submission.Success, Is.True, submission.DisplayText);
            StringAssert.Contains("\"intentType\":\"EnsureUnitCount\"",
                submission.ProviderResult.IntentJson);

            EnsureUnitCountGoal goal = goalManager.Goals.OfType<EnsureUnitCountGoal>().Single();
            for (int i = 0; i < 20000 && !goal.IsTerminal; i++)
            {
                goalManager.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 300 == 0) yield return null;
            }

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(simulation.UnitRegistry.GetAllUnits().Count(unit =>
                unit.PlayerId == 0 && unit.CurrentHealth > 0
                && unit.UnitType == CommanderIntentCatalog.SpearmanUnitType),
                Is.GreaterThanOrEqualTo(10));
            Debug.Log("[Phase4A Runtime] 'make 10 spearmen' -> "
                + submission.ProviderResult.IntentJson + " -> 10 spearmen produced.");
        }

        [UnityTest]
        public IEnumerator Runtime_MockNaturalLanguageAssignsEightVillagersToWood()
        {
            var submitTask = adapter.SubmitAsync("put 8 villagers on wood");
            while (!submitTask.IsCompleted) yield return null;
            CommanderAIChatSubmission submission = submitTask.Result;
            Assert.That(submission.Success, Is.True, submission.DisplayText);
            StringAssert.Contains("\"intentType\":\"SetResourceAllocation\"",
                submission.ProviderResult.IntentJson);

            ResourceAllocationGoal goal = goalManager.Goals.OfType<ResourceAllocationGoal>().Single();
            for (int i = 0; i < 5000 && !goal.IsTerminal; i++)
            {
                goalManager.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 120 == 0) yield return null;
            }

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(simulation.UnitRegistry.GetAllUnits().Count(unit =>
                unit.PlayerId == 0 && unit.IsVillager && unit.CurrentHealth > 0
                && unit.TargetResourceNodeId == woodNode.Id), Is.EqualTo(8));
            Debug.Log("[Phase4A Runtime] 'put 8 villagers on wood' -> "
                + submission.ProviderResult.IntentJson + " -> 8 villagers assigned through ResourceAllocationGoal.");
        }

        private UnitData CreateWorker(int tileX, int tileZ)
        {
            UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = CommanderIntentCatalog.VillagerUnitType;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            return worker;
        }
    }
}
