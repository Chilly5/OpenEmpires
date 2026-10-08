using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5AProductionBindingTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void ProducerReference_UsesEveryExactNewProducerAndNeverExistingBarracks(int producerCount)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                sim.SetPlayerAge(0, 3);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int tx = x - 25; tx <= x + 25; tx++)
                    for (int tz = z - 20; tz <= z + 20; tz++)
                    {
                        sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        sim.MapData.ForestDensity[tx, tz] = 0;
                        sim.MapData.FoundationCount[tx, tz] = 0;
                        sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var oldProducer = sim.CreateBuilding(0, BuildingType.Barracks, x + 18, z + 8, false);
                var resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
                var worker = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x - 3, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":" + producerCount + "},"
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,"
                    + "\"dependsOn\":[0],\"producerFromNode\":0}]}" );
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Build and use only those new Barracks.", 0), 0);
                var goals = manager.SubmitSemanticGraph(graph);
                var source = (BuildStructureGoal)goals[0];
                Assert.That(source.HasResultConsumer, Is.True,
                    "A producer dependency requires exact construction attribution, not a baseline heuristic.");
                var orders = new List<TrainUnitCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is TrainUnitCommand train) orders.Add(train); };
                for (int step = 0; step < 12; step++)
                {
                    manager.Tick(step * 15);
                    sim.Tick();
                    foreach (var building in sim.BuildingRegistry.GetAllBuildings().Where(b =>
                        b.PlayerId == 0 && b.Type == BuildingType.Barracks && b.IsUnderConstruction))
                        building.IsUnderConstruction = false; // Isolate attribution/producer dispatch from travel time.
                    worker.State = UnitState.Idle;
                }
                Assert.That(source.Status, Is.EqualTo(CommanderGoalStatus.Completed), source.StatusReason);
                Assert.That(source.ResultBuildingIds.Count, Is.EqualTo(producerCount));
                Assert.That(source.ResultBuildingIds, Has.No.Member(oldProducer.Id));
                Assert.That(orders.Count, Is.GreaterThanOrEqualTo(producerCount));
                Assert.That(orders.All(o => source.ResultBuildingIds.Contains(o.BuildingId)), Is.True);
                Assert.That(orders.Select(o => o.BuildingId).Distinct().Count(), Is.EqualTo(producerCount));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void StructureResult_ContainsOnlyThisRootsActualPlacementCommands()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
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
                var res = sim.ResourceManager.GetPlayerResources(0);
                res.Food = res.Wood = res.Gold = res.Stone = 10000;
                var worker = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x - 3, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":2},"
                    + "{\"type\":\"SetRallyPoint\",\"unitSelector\":\"Military\",\"count\":1,\"structure\":\"Barracks\",\"location\":\"PlayerBase\","
                    + "\"dependsOn\":[0],\"resultFromNode\":0}]}" );
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Build two Barracks and use their result.", 0), 0);
                var build = (BuildStructureGoal)manager.SubmitSemanticGraph(graph)[0];
                var unrelated = sim.CreateBuilding(0, BuildingType.Barracks, x + 12, z + 7, false);
                manager.Tick(0);
                sim.Tick();
                foreach (var foundation in sim.BuildingRegistry.GetAllBuildings().Where(b => b.PlayerId == 0 && b.Type == BuildingType.Barracks && b.IsUnderConstruction))
                    foundation.IsUnderConstruction = false;
                manager.Tick(15);
                Assert.That(build.ResultBuildingIds, Has.No.Member(unrelated.Id),
                    "An unrelated post-baseline Barracks cannot fill the root's exact construction result.");
                Assert.That(build.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelledOrderOrLostProducedUnit_IsNotSilentlyReplaced(bool destroyUnit)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                sim.SetPlayerAge(0, 3);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var barracks = sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
                var resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = 10000;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,"
                    + "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );
                var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Produce two new units then patrol.", 0), 0);
                var source = (EnsureUnitCountGoal)manager.SubmitSemanticGraph(graph)[0];
                manager.Tick(0);
                sim.Tick();
                Assert.That(source.TrackedTrainingOrders, Has.Count.EqualTo(1));
                if (destroyUnit)
                {
                    barracks.TrainingTicksRemaining = 1;
                    sim.Tick();
                    sim.UnitRegistry.GetUnit(source.AttributedUnitIds.Single()).CurrentHealth = 0;
                }
                else
                {
                    barracks.TrainingQueue.Clear(); // Direct edit cancels that exact tracked order.
                }
                manager.Tick(15);
                Assert.That(sim.CommandBuffer.FlushCommands().OfType<TrainUnitCommand>(), Is.Empty,
                    "A cancelled/missing exact result is a blocker, not authorization for replacement production.");
                Assert.That(source.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PreExistingAndHumanTraining_DoNotFillGoalExclusiveUnitResult()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                sim.SetPlayerAge(0, 3);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int tx = x - 20; tx <= x + 20; tx++)
                    for (int tz = z - 10; tz <= z + 10; tz++)
                    {
                        sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        sim.MapData.ForestDensity[tx, tz] = 0;
                        sim.MapData.FoundationCount[tx, tz] = 0;
                        sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var barracks = sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
                var resource = sim.ResourceManager.GetPlayerResources(0);
                resource.Food = resource.Wood = resource.Gold = resource.Stone = 10000;
                var existing = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x - 3, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                existing.UnitType = 1;
                existing.CurrentHealth = existing.MaxHealth = 100;
                existing.State = UnitState.Idle;
                barracks.EnqueueTraining(1, 2); // An older order, not this request's production.
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,"
                    + "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );
                var candidate = manager.PrepareActionPlan(parsed, "Produce two new Spearmen, then patrol with them.", 1);
                var graph = manager.ApproveActionPlan(candidate, 1);
                var goals = manager.SubmitSemanticGraph(graph);
                var producer = (EnsureUnitCountGoal)goals[0];
                var issued = new List<ICommand>();
                sim.CommandBuffer.CommandEnqueued += (command, source) =>
                { if (source == CommandEnqueueSource.Commander && command is TrainUnitCommand) issued.Add(command); };
                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0, barracks.Id, 1)); // Human, same producer/type.
                manager.Tick(0);
                sim.Tick();
                sim.Tick();
                int oldQueuedUnit = sim.UnitRegistry.GetAllUnits().Single(u => u.Id != existing.Id && u.UnitType == 1).Id;
                // Accelerate native completion, not spawn or Commander result capture.
                barracks.TrainingTicksRemaining = 1;
                sim.Tick();
                int humanUnit = sim.UnitRegistry.GetAllUnits().Where(u => u.Id != existing.Id && u.Id != oldQueuedUnit && u.UnitType == 1).Single().Id;
                manager.Tick(15);
                Assert.That(producer.ResultUnitIds, Has.No.Member(oldQueuedUnit));
                Assert.That(producer.ResultUnitIds, Has.No.Member(humanUnit));
                Assert.That(producer.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
                    "Unrelated new units cannot satisfy an exact result-producing root.");
                for (int i = 0; i < 900 && producer.Status != CommanderGoalStatus.Completed; i++)
                {
                    if (barracks.IsTraining) barracks.TrainingTicksRemaining = 1;
                    manager.Tick(sim.CurrentTick);
                    sim.Tick();
                }
                Assert.That(producer.Status, Is.EqualTo(CommanderGoalStatus.Completed), producer.StatusReason);
                Assert.That(producer.ResultUnitIds.Count, Is.EqualTo(2));
                Assert.That(issued, Has.Count.EqualTo(2));
                Assert.That(producer.ResultUnitIds, Has.No.Member(existing.Id));
                Assert.That(producer.ResultUnitIds, Has.No.Member(oldQueuedUnit));
                Assert.That(producer.ResultUnitIds, Has.No.Member(humanUnit));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
