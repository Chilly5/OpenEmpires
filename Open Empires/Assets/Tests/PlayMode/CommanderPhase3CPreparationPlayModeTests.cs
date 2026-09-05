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
    public class CommanderPhase3CPreparationPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sim.MapData, null);
            for (int a = x - 30; a <= x + 30; a++)
                for (int b = z - 20; b <= z + 20; b++)
                {
                    sim.MapData.Tiles[a, b] = TileType.Grass;
                    sim.MapData.ForestDensity[a, b] = 0;
                    sim.MapData.FoundationCount[a, b] = 0;
                    sim.FogOfWar.SetVisible(0, a, b);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true).AutoProduceVillagers = false;
            var enemy = sim.MapData.BasePositions[1];
            sim.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true).AutoProduceVillagers = false;
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [UnityTest]
        public IEnumerator Runtime_BarracksAndWood_BothProgressAndComplete()
        {
            var builder = Worker(x - 5); Worker(x - 6); Worker(x - 7);
            Resource(ResourceType.Wood, x, z + 8);
            sim.ResourceManager.GetPlayerResources(0).Wood = 200;
            var build = manager.SubmitBuildStructure(BuildingType.Barracks);
            var gather = manager.SubmitResourceAllocation(ResourceType.Wood, 2);
            int firstGatherTick = -1; var commandsPerTick = new Dictionary<int, int>();
            sim.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source != CommandEnqueueSource.Commander) return;
                commandsPerTick[sim.CurrentTick] = commandsPerTick.TryGetValue(sim.CurrentTick, out int count) ? count + 1 : 1;
                if (command is GatherCommand && firstGatherTick < 0) firstGatherTick = sim.CurrentTick;
            };
            manager.Tick(0);
            Assert.That(manager.GetWorkerReservation(builder.Id).Value.GoalId, Is.EqualTo(build.GoalId));
            for (int i = 0; i < 2400 && (!build.IsTerminal || !gather.IsTerminal); i++)
            { sim.Tick(); manager.Tick(sim.CurrentTick); if (i % 15 == 0) yield return null; }
            Assert.That(firstGatherTick, Is.EqualTo(15), "Wood must start while construction is still in progress.");
            Assert.That(build.Status, Is.EqualTo(CommanderGoalStatus.Completed), build.StatusReason);
            Assert.That(gather.Status, Is.EqualTo(CommanderGoalStatus.Completed), gather.StatusReason);
            Assert.That(commandsPerTick.Values.All(count => count == 1), Is.True);
            Assert.That(commandsPerTick.Keys.All(tick => tick % 15 == 0), Is.True);
            Assert.That(manager.GetWorkerReservation(builder.Id), Is.Null);
            Debug.Log($"[Phase3C Prep Runtime] PASS Barracks + wood: wood command tick={firstGatherTick}, both completed by tick={sim.CurrentTick}, max one command per 15-tick interval.");
        }

        [UnityTest]
        public IEnumerator Runtime_GoldAndWood_NoInfiniteReassignmentLoop()
        {
            var workers = new[] { Worker(x - 5), Worker(x - 6), Worker(x - 7), Worker(x - 8) };
            var gold = Resource(ResourceType.Gold, x, z + 6);
            var wood = Resource(ResourceType.Wood, x + 6, z + 6);
            var goldGoal = manager.SubmitResourceAllocation(ResourceType.Gold, 2);
            var woodGoal = manager.SubmitResourceAllocation(ResourceType.Wood, 2);
            var assignments = new Dictionary<int, int>();
            sim.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source != CommandEnqueueSource.Commander || !(command is GatherCommand gather)) return;
                foreach (int workerId in gather.UnitIds) assignments[workerId] = assignments.TryGetValue(workerId, out int count) ? count + 1 : 1;
            };
            for (int i = 0; i < 1200; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 30 == 0) yield return null; }
            Assert.That(goldGoal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goldGoal.StatusReason);
            Assert.That(woodGoal.Status, Is.EqualTo(CommanderGoalStatus.Completed), woodGoal.StatusReason);
            Assert.That(assignments, Has.Count.EqualTo(4));
            Assert.That(assignments.Values.All(count => count == 1), Is.True);
            Assert.That(workers.Count(worker => worker.TargetResourceNodeId == gold.Id), Is.EqualTo(2));
            Assert.That(workers.Count(worker => worker.TargetResourceNodeId == wood.Id), Is.EqualTo(2));
            Assert.That(workers.All(worker => !manager.GetWorkerReservation(worker.Id).HasValue), Is.True);
            Debug.Log("[Phase3C Prep Runtime] PASS gold + wood: two workers each; four assignments total across 1200 ticks; no oscillation; completed reservations released.");
        }

        [UnityTest]
        public IEnumerator Runtime_ManualAssignmentOverridesGoalReservation()
        {
            var worker = Worker(x - 5);
            var gold = Resource(ResourceType.Gold, x, z + 6);
            Resource(ResourceType.Wood, x + 6, z + 6);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
            sim.CommandBuffer.EnqueueCommand(new GatherCommand(0, new[] { worker.Id }, gold.Id));
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.False);
            int commanderCommands = 0;
            sim.CommandBuffer.CommandEnqueued += (command, source) => { if (source == CommandEnqueueSource.Commander) commanderCommands++; };
            for (int i = 0; i < 600; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 30 == 0) yield return null; }
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(gold.Id));
            Assert.That(commanderCommands, Is.Zero);
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null);
            Debug.Log("[Phase3C Prep Runtime] PASS human priority: manual gold assignment released reservation; no Commander reclaim through 600 ticks of the unchanged 900-tick protection window.");
        }

        private UnitData Worker(int tileX)
        {
            var worker = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(tileX, z), Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100; worker.State = UnitState.Idle;
            return worker;
        }
        private ResourceNodeData Resource(ResourceType type, int tileX, int tileZ) =>
            sim.MapData.AddResourceNode(type, sim.MapData.TileToWorldFixed(tileX, tileZ), 10000);
    }
}
