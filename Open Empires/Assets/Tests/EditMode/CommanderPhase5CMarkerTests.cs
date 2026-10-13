using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CMarkerTests
    {
        [Test]
        public void HiddenAndInvalidMarkers_RetainValidOwnedMarkerAndRejectCrossRuntimeBinding()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            var foreign = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            using var manager = new CommanderGoalManager(sim, 0);
            try
            {
                int x = sim.MapData.Width/2, z = sim.MapData.Height/2;
                sim.FogOfWar.SetVisible(0,x,z);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x,z),Is.True);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(-1,z),Is.False);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(sim.MapData.Width,z),Is.False);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(0,0),Is.False);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out int actualX,out int actualZ),Is.True);
                Assert.That((actualX,actualZ),Is.EqualTo((x,z)));
                var bound=CommanderBoundLocation.FromPlayerMarker(sim,manager,0,x,z,
                    CommanderSemanticPlacementRelation.Near,1);
                Assert.That(bound.TryResolve(foreign,0,out _,out _,out _,out _),Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test]
        public void SymbolicMarkerMovement_ParsesWithoutProviderCoordinates()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerMarker\"}]}");
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Nodes[0].LocationSelector.ToString(), Is.EqualTo("PlayerMarker"));
        }

        [Test]
        public void MarkerCoordinatesInProviderOutput_AreRejected()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerMarker\",\"x\":20,\"z\":30}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void MarkerSnapshot_ChangedAfterPreviewRequiresNewApproval()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.FogOfWar.SetVisible(0, x, z);
                sim.FogOfWar.SetVisible(0, x + 1, z);
                manager = new CommanderGoalManager(sim, 0);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var candidate = manager.PrepareActionPlan(parsed, "Move my Spearman there", 0);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x + 1, z), Is.True);
                Assert.That(manager.ApproveActionPlan(candidate, 0), Is.Null);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void MissingMarker_RetainsRequestForLocationClarification()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var error = Assert.Throws<ArgumentException>(() => manager.PrepareActionPlan(parsed,
                    "Move my Spearman there", 0));
                StringAssert.Contains("Where? Place a marker or describe the location.", error.Message);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void DynamicConstructionAtPlayerMarker_PreparesApprovedGameOwnedBinding()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.FogOfWar.SetVisible(0, x, z);
                manager = new CommanderGoalManager(sim, 0);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                const string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"PlayerMarker\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"yard\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:LumberYard\",\"count\":1},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
                var parsed = CommanderSemanticJson.Parse(json);
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var candidate = manager.PrepareActionPlan(parsed, "Build a Lumber Yard at my marker", 0);
                Assert.That(candidate.Preview, Does.Contain("your placed destination marker"));
                Assert.That(manager.ApproveActionPlan(candidate, 0), Is.Not.Null);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void TenNaturallyProducedSpearmen_MoveToMarkerOrWorkedGold_WithoutOldUnits(
            bool useMarker, bool humanOverride)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                sim.SetPlayerAge(0, 3);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int tx = x - 20; tx <= x + 20; tx++)
                    for (int tz = z - 12; tz <= z + 12; tz++)
                    {
                        sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        sim.MapData.ForestDensity[tx, tz] = 0;
                        sim.MapData.FoundationCount[tx, tz] = 0;
                        sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                sim.CreateBuilding(0, BuildingType.House, x - 9, z, false, true);
                sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false, true);
                var gold = sim.MapData.AddResourceNode(ResourceType.Gold,
                    sim.MapData.TileToWorldFixed(x + 15, z + 4), 1000);
                var miner = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 12, z + 4),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                miner.IsVillager = true;
                miner.CurrentHealth = miner.MaxHealth = 100;
                miner.State = UnitState.MovingToGather;
                miner.TargetResourceNodeId = gold.Id;
                var old = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 6, z + 4),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                old.UnitType = 1;
                old.CurrentHealth = old.MaxHealth = 100;
                old.State = UnitState.Idle;
                var resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = 10000;
                manager = new CommanderGoalManager(sim, 0);
                if (useMarker)
                    Assert.That(manager.TrySetPlayerMarkerSnapshot(x + 15, z + 7), Is.True);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":10,"
                    + "\"location\":\"" + (useMarker ? "PlayerMarker" : "WorkedResource") + "\""
                    + (useMarker ? "" : ",\"resource\":\"Gold\"")
                    + ",\"dependsOn\":[0],\"resultFromNode\":0}]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var candidate = manager.PrepareActionPlan(parsed,
                    useMarker ? "Train ten new Spearmen and send them to my marker"
                        : "Train ten new Spearmen and send them to Gold worked by my villagers", 0);
                if (useMarker) Assert.That(manager.TryGetPlayerMarkerSnapshot(out _, out _), Is.True);
                var graph = manager.ApproveActionPlan(candidate, 0);
                Assert.That(graph, Is.Not.Null);
                var goals = manager.SubmitSemanticGraph(graph);
                if (useMarker) Assert.That(manager.TryGetPlayerMarkerSnapshot(out _, out _), Is.False);
                var produced = (EnsureUnitCountGoal)goals[0];
                var moves = new List<MoveCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is MoveCommand move) moves.Add(move); };
                for (int step = 0; step < 5000 && moves.Count == 0; step++)
                {
                    manager.Tick(sim.CurrentTick);
                    sim.Tick();
                }
                Assert.That(produced.Status, Is.EqualTo(CommanderGoalStatus.Completed), produced.StatusReason);
                Assert.That(produced.ResultUnitIds.Count, Is.EqualTo(10));
                Assert.That(moves, Has.Count.EqualTo(1));
                Assert.That(moves[0].UnitIds.OrderBy(id => id), Is.EqualTo(produced.ResultUnitIds.OrderBy(id => id)));
                Assert.That(moves[0].UnitIds, Has.No.Member(old.Id));
                if (humanOverride)
                {
                    sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { produced.ResultUnitIds[0] }));
                    sim.Tick();
                    for (int step = 0; step < 1000; step++)
                    {
                        manager.Tick(sim.CurrentTick);
                        sim.Tick();
                    }
                    Assert.That(moves, Has.Count.EqualTo(1), "Human override must not cause a replacement move.");
                    Assert.That(goals[1].Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
                    return;
                }
                for (int step = 0; step < 2000 && goals[1].Status != CommanderGoalStatus.Completed; step++)
                {
                    manager.Tick(sim.CurrentTick);
                    sim.Tick();
                }
                Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.Completed), goals[1].StatusReason);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
