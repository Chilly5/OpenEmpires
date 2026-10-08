using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixQuantityPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private BuildingData barracks;
        private int x, z;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French }); sim.SetPlayerAge(0, 3);
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sim.MapData, null);
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 20; tz <= z + 20; tz++)
                { sim.MapData.Tiles[tx, tz] = TileType.Grass; sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0; sim.FogOfWar.SetVisible(0, tx, tz); }
            sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            barracks = sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false, true);
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown] public void TearDown()
        { manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        private void InitialSpearmen(int count)
        {
            for (int i = 0; i < count; i++)
                typeof(GameSimulation).GetMethod("CreateTrainedUnit", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sim, new object[] { 0, 1, sim.MapData.TileToWorldFixed(x - 5 + i, z + 5) });
        }

        [UnityTest] public IEnumerator TotalWithOldAndHumanQueuedUnits_ProducesOnlyOneExactNewPatroller()
        {
            InitialSpearmen(4);
            int[] oldIds = sim.UnitRegistry.GetAllUnits().Select(u => u.Id).ToArray();
            // Ordinary human command is accepted BEFORE preview. No synthetic completion.
            sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0, barracks.Id, 1)); sim.Tick();
            Assert.That(barracks.TrainingQueue.Count, Is.EqualTo(1));
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":6},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Six total; patrol with the one new result", 0), 0);
            var source = (EnsureUnitCountGoal)manager.SubmitSemanticGraph(graph)[0];
            int trains = 0; PatrolCommand? patrol = null;
            sim.CommandBuffer.CommandEnqueued += (command, origin) =>
            {
                if (origin != CommandEnqueueSource.Commander) return;
                if (command is TrainUnitCommand) trains++;
                if (command is PatrolCommand value) patrol = value;
            };
            for (int tick = 0; tick < 3600 && !patrol.HasValue; tick++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (tick % 30 == 0) yield return null; }
            Assert.That(source.Status, Is.EqualTo(CommanderGoalStatus.Completed), source.StatusReason);
            Assert.That(trains, Is.EqualTo(1));
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(u => u.UnitType == 1 && u.CurrentHealth > 0), Is.EqualTo(6));
            Assert.That(source.ResultUnitIds.Count, Is.EqualTo(1));
            Assert.That(oldIds, Has.No.Member(source.ResultUnitIds[0]));
            Assert.That(source.TrackedTrainingOrders.Single().Issuer, Is.SameAs(source));
            Assert.That(source.TrackedTrainingOrders.Single().IsCompleted, Is.True);
            Assert.That(patrol.HasValue, Is.True);
            Assert.That(patrol.Value.UnitIds, Is.EqualTo(source.ResultUnitIds));
        }

        [UnityTest] public IEnumerator SequentialTotals_TwoThenThree_UsesOnlySecondGoalsOneNewResult()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"dependsOn\":[0]},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerBase\",\"dependsOn\":[1],\"resultFromNode\":1}]}");
            var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Two then three total with exact final new unit", 0), 0);
            var goals = manager.SubmitSemanticGraph(graph);
            var second = (EnsureUnitCountGoal)goals[1]; PatrolCommand? patrol = null;
            sim.CommandBuffer.CommandEnqueued += (command, origin) =>
            { if (origin == CommandEnqueueSource.Commander && command is PatrolCommand value) patrol = value; };
            for (int tick = 0; tick < 3600 && !patrol.HasValue; tick++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (tick % 30 == 0) yield return null; }
            Assert.That(goals[0].Status, Is.EqualTo(CommanderGoalStatus.Completed), goals[0].StatusReason);
            Assert.That(second.Status, Is.EqualTo(CommanderGoalStatus.Completed), second.StatusReason);
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(u => u.UnitType == 1 && u.CurrentHealth > 0), Is.EqualTo(3));
            Assert.That(second.ResultUnitIds.Count, Is.EqualTo(1));
            Assert.That(patrol.HasValue, Is.True);
            Assert.That(patrol.Value.UnitIds, Is.EqualTo(second.ResultUnitIds));
        }
    }
}
