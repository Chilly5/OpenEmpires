using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E5")]
    public sealed class CommanderPhase4E5ConcurrencyPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private int centerX;
        private int centerZ;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            centerX = simulation.MapData.Width / 2;
            centerZ = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);

            for (int x = centerX - 35; x <= centerX + 35; x++)
                for (int z = centerZ - 20; z <= centerZ + 20; z++)
                {
                    simulation.MapData.Tiles[x, z] = TileType.Grass;
                    simulation.MapData.ForestDensity[x, z] = 0;
                    simulation.MapData.FoundationCount[x, z] = 0;
                    simulation.FogOfWar.SetVisible(0, x, z);
                }
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;

            BuildingData townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter,
                centerX, centerZ, false, true);
            townCenter.AutoProduceVillagers = false;
            BuildingData enemyTownCenter = simulation.CreateBuilding(1, BuildingType.TownCenter,
                centerX + 25, centerZ, false, true);
            enemyTownCenter.AutoProduceVillagers = false;

            Worker(centerX - 5);
            Worker(centerX - 6);
            Worker(centerX - 7);
            Worker(centerX - 8);
            for (int i = 0; i < 10; i++)
            {
                UnitData pressure = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(centerX + 2, centerZ + i + 1),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                pressure.UnitType = CommanderIntentCatalog.ArcherUnitType;
                pressure.MaxHealth = pressure.CurrentHealth = 100;
                pressure.State = UnitState.Idle;
            }
            simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(centerX, centerZ + 8), 10000);

            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = 0;
            resources.Wood = 1000;
            resources.Gold = 1000;
            goals = new CommanderGoalManager(simulation, 0);
        }

        [TearDown]
        public void TearDown()
        {
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_EnsureSpearmen_OverlapsCapacityProducerAndFoodPreparation()
        {
            EnsureUnitCountGoal goal = goals.SubmitEnsureUnitCount(
                CommanderIntentCatalog.SpearmanUnitType, 10);
            var commanderCommands = new List<(int Tick, ICommand Command)>();
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source == CommandEnqueueSource.Commander)
                    commanderCommands.Add((simulation.CurrentTick, command));
            };

            goals.Tick(0);
            simulation.Tick();
            BuildingData house = OwnedUnderConstruction(BuildingType.House);
            Assert.That(house, Is.Not.Null, "Population planning must start a House before production is complete.");

            goals.Tick(15);
            simulation.Tick();
            BuildingData barracks = OwnedUnderConstruction(BuildingType.Barracks);
            Assert.That(barracks, Is.Not.Null, "Producer planning must proceed while the House foundation is active.");
            Assert.That(barracks.IsUnderConstruction, Is.True);

            goals.Tick(30);
            simulation.Tick();
            UnitData foodGatherer = simulation.UnitRegistry.GetAllUnits()
                .FirstOrDefault(unit => unit.PlayerId == 0 && unit.IsVillager
                    && unit.TargetResourceNodeId >= 0);
            Assert.That(foodGatherer, Is.Not.Null,
                "Food preparation must start while the Barracks foundation is still under construction.");
            Assert.That(foodGatherer.TargetResourceNodeId, Is.GreaterThanOrEqualTo(0));
            Assert.That(barracks.IsUnderConstruction, Is.True);
            Assert.That(simulation.BuildingRegistry.GetAllBuildings().Count(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks && !building.IsDestroyed),
                Is.EqualTo(1), "Re-evaluation must not duplicate the Barracks prerequisite.");
            Assert.That(commanderCommands.Select(entry => entry.Tick).Distinct().Count(), Is.EqualTo(commanderCommands.Count),
                "The manager may issue at most one Commander command per planning tick.");
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Failed));

            Debug.Log($"[Phase4E5 Concurrency Runtime] PASS House #{house.Id}, Barracks #{barracks.Id}, "
                + $"food gatherer #{foodGatherer.Id} started before producer completion; commands={commanderCommands.Count}.");
            yield return null;
        }

        [Test]
        public void TemporaryHumanWorkerProtection_KeepsUnitGoalRetryable()
        {
            BuildingData house = simulation.CreateBuilding(0, BuildingType.House,
                centerX + 4, centerZ, false, true);
            BuildingData barracks = simulation.CreateBuilding(0, BuildingType.Barracks,
                centerX + 8, centerZ, false, true);
            ResourceNodeData food = simulation.MapData.GetAllResourceNodes()
                .First(node => node.Type == ResourceType.Food);
            food.RemainingAmount = 10000;
            simulation.FogOfWar.SetVisionCheat(0, true);

            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = 0;
            resources.Wood = 1000;
            resources.Gold = 1000;

            List<UnitData> villagers = simulation.UnitRegistry.GetAllUnits()
                .Where(unit => unit.PlayerId == 0 && unit.IsVillager).ToList();
            foreach (UnitData villager in villagers)
                simulation.CommandBuffer.EnqueueCommand(
                    new GatherCommand(0, new[] { villager.Id }, food.Id));

            EnsureUnitCountGoal goal = goals.SubmitEnsureUnitCount(
                CommanderIntentCatalog.SpearmanUnitType, 1);
            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources),
                "Temporary human worker ownership must remain retryable instead of entering a terminal-looking blocked state. "
                + goal.StatusReason + $" node=({food.TileX},{food.TileZ}) vis="
                + simulation.FogOfWar.GetVisibility(0, food.TileX, food.TileZ)
                + $" amount={food.RemainingAmount} nodes={simulation.MapData.GetAllResourceNodes().Count}");
            Assert.That(goal.StatusReason, Does.Contain("No eligible owned villager"));
            Assert.That(house.IsDestroyed, Is.False);
            Assert.That(barracks.IsDestroyed, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnreachableVisibleResource_BlocksAndRecoversOnlyWhenRouteOpens(bool reopenRoute)
        {
            simulation.CreateBuilding(0, BuildingType.House, centerX + 4, centerZ, false, true);
            simulation.CreateBuilding(0, BuildingType.Barracks, centerX + 8, centerZ, false, true);
            ResourceNodeData food = simulation.MapData.GetAllResourceNodes()
                .Single(node => node.Type == ResourceType.Food && !node.IsDepleted);
            for (int x = food.TileX - 4; x <= food.TileX + 4; x++)
                for (int z = food.TileZ - 4; z <= food.TileZ + 4; z++)
                    if (Math.Abs(x - food.TileX) == 4 || Math.Abs(z - food.TileZ) == 4)
                        simulation.MapData.Tiles[x, z] = TileType.Water;

            int gatherCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source == CommandEnqueueSource.Commander && command is GatherCommand)
                    gatherCommands++;
            };
            EnsureUnitCountGoal goal = goals.SubmitEnsureUnitCount(
                CommanderIntentCatalog.SpearmanUnitType, 1);
            goals.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("reachable"));
            Assert.That(gatherCommands, Is.Zero);

            if (reopenRoute)
            {
                for (int x = food.TileX - 4; x <= food.TileX + 4; x++)
                    for (int z = food.TileZ - 4; z <= food.TileZ + 4; z++)
                        if (Math.Abs(x - food.TileX) == 4 || Math.Abs(z - food.TileZ) == 4)
                            simulation.MapData.Tiles[x, z] = TileType.Grass;
                goals.Tick(300);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources));
                Assert.That(gatherCommands, Is.EqualTo(1));
            }
            else
            {
                goals.Tick(1800);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed));
                Assert.That(gatherCommands, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Runtime_ZeroWoodEnsureSpearmen_GathersWoodAndConverges()
        {
            simulation.CreateBuilding(0, BuildingType.House, centerX - 14, centerZ - 6, false);
            ResourceNodeData wood = simulation.MapData.AddResourceNode(ResourceType.Wood,
                simulation.MapData.TileToWorldFixed(centerX - 12, centerZ), 10000);
            simulation.FogOfWar.SetVisionCheat(0, true);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = 10000;
            resources.Wood = 0;
            resources.Gold = 10000;

            EnsureUnitCountGoal goal = goals.SubmitEnsureUnitCount(
                CommanderIntentCatalog.SpearmanUnitType, 10);
            bool woodGatherCommandSeen = false;
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source == CommandEnqueueSource.Commander
                    && command is GatherCommand gather
                    && gather.ResourceNodeId == wood.Id)
                    woodGatherCommandSeen = true;
            };

            for (int i = 0; i < 30000 && !goal.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 300 == 0) yield return null;
            }

            int spearmen = simulation.UnitRegistry.GetAllUnits().Count(unit =>
                unit.PlayerId == 0 && unit.UnitType == CommanderIntentCatalog.SpearmanUnitType
                && unit.CurrentHealth > 0);
            Assert.That(woodGatherCommandSeen, Is.True,
                "A zero-wood Spearman request must issue a Commander gather command for visible wood.");
            int completedBarracks = simulation.BuildingRegistry.GetAllBuildings().Count(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && !building.IsDestroyed && !building.IsUnderConstruction);
            int pendingHouses = simulation.BuildingRegistry.GetAllBuildings().Count(building =>
                building.PlayerId == 0 && building.Type == BuildingType.House
                && !building.IsDestroyed && building.IsUnderConstruction);
            Assert.That(completedBarracks, Is.GreaterThanOrEqualTo(1),
                $"The request must construct a completed Barracks after gathering wood. "
                + $"status={goal.Status} reason={goal.StatusReason} owned={spearmen} "
                + $"tick={simulation.CurrentTick} cap={simulation.GetPopulationCap(0)} "
                + $"housesPending={pendingHouses}");
            int queued = simulation.BuildingRegistry.GetAllBuildings()
                .Where(building => building.PlayerId == 0 && !building.IsDestroyed)
                .Sum(building => building.TrainingQueue.Count);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed),
                goal.StatusReason + $" owned={spearmen} queued={queued} tick={simulation.CurrentTick}");
            Assert.That(spearmen, Is.GreaterThanOrEqualTo(10));
            Debug.Log($"[Phase4E5 ZeroWood Runtime] PASS wood node #{wood.Id}, spearmen={spearmen}, tick={simulation.CurrentTick}.");
        }

        private UnitData Worker(int tileX)
        {
            UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, centerZ),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            return worker;
        }

        private BuildingData OwnedUnderConstruction(BuildingType type) =>
            simulation.BuildingRegistry.GetAllBuildings().FirstOrDefault(building =>
                building.PlayerId == 0 && building.Type == type && !building.IsDestroyed
                && building.IsUnderConstruction);
    }
}
