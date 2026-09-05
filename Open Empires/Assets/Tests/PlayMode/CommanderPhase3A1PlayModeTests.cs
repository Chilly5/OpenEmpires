using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // Live Unity PlayMode simulations. Fixture setup is explicit; after submission,
    // commands, movement, fog, construction and goal completion use normal game ticks.
    public class CommanderPhase3A1PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sim.MapData, null);
            for (int a = x - 35; a <= x + 35; a++)
                for (int b = z - 15; b <= z + 15; b++)
                {
                    sim.MapData.Tiles[a, b] = TileType.Grass;
                    sim.MapData.ForestDensity[a, b] = 0;
                    sim.MapData.FoundationCount[a, b] = 0;
                    sim.FogOfWar.SetVisible(0, a, b);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true).AutoProduceVillagers = false;
            var enemy = sim.MapData.BasePositions[1];
            sim.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true).AutoProduceVillagers = false;
            manager = new CommanderGoalManager(sim, 0);
            dispatcher = new CommanderIntentDispatcher(sim, manager);
        }

        [TearDown]
        public void TearDown() { dispatcher.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        [UnityTest]
        public IEnumerator Runtime_VisibleExploredVisible_CommanderPlansAndWorkerMoves()
        {
            var worker = Worker(x - 20);
            var node = sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x, z), 10000);
            sim.FogOfWar.DemoteAllVisible(0);
            sim.FogOfWar.SetVisible(0, x - 20, z);
            sim.FogOfWar.SetVisible(0, x, z);
            sim.FogOfWar.SetVisible(0, x - 1, z);
            var start = worker.SimPosition;
            Assert.That(sim.FogOfWar.GetVisibility(0, x - 10, z), Is.EqualTo(TileVisibility.Explored));
            var goal = Submit("put 1 villagers on wood");
            manager.Tick(0);
            // Inspect without removing the normal buffered command.
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources), goal.StatusReason);
            for (int i = 0; i < 60; i++) { sim.Tick(); manager.Tick(sim.CurrentTick); if (i % 15 == 0) yield return null; }
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(node.Id));
            Assert.That(worker.SimPosition, Is.Not.EqualTo(start));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Debug.Log($"[Phase3A.1 Runtime] PASS explored corridor: tick={sim.CurrentTick}, goal={goal.Status}, worker moved, target={node.Id}.");
        }

        [UnityTest]
        public IEnumerator Runtime_FarFoundation_BuilderTravelsWithoutEarlyReplacement()
        {
            var builder = Worker(x - 20);
            Worker(x - 3); // Eligible backup exposes the original reassignment bug.
            var foundation = sim.CreateBuilding(0, BuildingType.Barracks, x, z, true);
            sim.CommandBuffer.EnqueueCommand(new ConstructBuildingCommand(0, new[] { builder.Id }, foundation.Id));
            sim.Tick();
            Assert.That(builder.ConstructionTargetBuildingId, Is.EqualTo(foundation.Id));
            var start = builder.SimPosition;
            int originalRemaining = foundation.ConstructionTicksRemaining;
            int recoveryCommands = 0;
            sim.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source == CommandEnqueueSource.Commander && command is ConstructBuildingCommand) recoveryCommands++;
            };
            var goal = Submit("build barracks");
            for (int i = 0; i < 210; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 15 == 0) yield return null; }
            Assert.That(builder.SimPosition, Is.Not.EqualTo(start));
            Assert.That(builder.SimPosition.x, Is.LessThan(Fixed32.FromInt(x - 1)), "Builder remains outside construction range after the old 150-tick timeout.");
            Assert.That(foundation.ConstructionTicksRemaining, Is.EqualTo(originalRemaining));
            Assert.That(recoveryCommands, Is.Zero);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));
            Debug.Log($"[Phase3A.1 Runtime] PASS far foundation: tick={sim.CurrentTick}, remaining={originalRemaining}, recoveryCommands={recoveryCommands}, builderState={builder.State}.");
            // Continue all the way through arrival and real construction completion.
            for (int i = 0; i < 2400 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 30 == 0) yield return null; }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(foundation.IsUnderConstruction, Is.False);
            Assert.That(recoveryCommands, Is.Zero);
            Debug.Log($"[Phase3A.1 Runtime] PASS far foundation completed normally: tick={sim.CurrentTick}, recoveryCommands={recoveryCommands}.");
        }

        [UnityTest]
        public IEnumerator Runtime_ImpossibleGoal_DoesNotBlockLaterWoodRequest()
        {
            var worker = Worker(x - 2);
            // Saturate the canonical population ceiling without changing gameplay config.
            for (int i = 0; i < 20; i++)
                sim.CreateBuilding(0, BuildingType.House, x + 20 + (i % 5) * 3, z - 14 + (i / 5) * 3, false);
            for (int i = 1; i < config.MaxPopulation; i++)
            {
                var filler = Worker(x + 10);
                filler.IsVillager = false; filler.UnitType = 1;
            }
            var wood = sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x, z), 10000);
            var impossible = Submit("make 1 knight");
            manager.Tick(0);
            Assert.That(impossible.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            var next = Submit("put 1 villagers on wood");
            for (int i = 0; i < 60; i++) { sim.Tick(); manager.Tick(sim.CurrentTick); if (i % 15 == 0) yield return null; }
            Assert.That(next.Status, Is.EqualTo(CommanderGoalStatus.Completed), next.StatusReason);
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(wood.Id));
            Assert.That(impossible.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(manager.Goals, Does.Contain(impossible));
            Debug.Log($"[Phase3A.1 Runtime] PASS blocked queue: tick={sim.CurrentTick}, first={impossible.Status}, wood={next.Status}, target={wood.Id}.");
        }

        private CommanderGoal Submit(string text)
        {
            var result = dispatcher.SubmitText(text);
            Assert.That(result.CreatedGoal, Is.True, result.Response);
            return result.Resolution.Goal;
        }

        private UnitData Worker(int tileX)
        {
            var worker = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(tileX, z), Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100; worker.State = UnitState.Idle;
            return worker;
        }
    }
}
