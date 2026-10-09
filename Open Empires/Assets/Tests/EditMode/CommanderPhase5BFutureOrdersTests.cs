using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BFutureOrders")]
    public sealed class CommanderPhase5BFutureOrdersTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void NextFiveVillagers_IsOneBoundedSemanticNodeAndNotAProductionOrder()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,"
                + "\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\","
                + "\"resource\":\"Wood\",\"sourceKind\":\"Any\"}]}");

            Assert.That(parsed.IsValid, Is.True, "Next-N watching must have its own strict semantic shape.");
            Assert.That(parsed.Nodes, Has.Count.EqualTo(1));
            Assert.That(parsed.Nodes[0].Count, Is.EqualTo(5));
            Assert.That(parsed.Nodes[0].UnitType, Is.EqualTo(0));
            Assert.That(parsed.Nodes[0].BuildingType, Is.EqualTo(BuildingType.TownCenter));
        }

        [TestCase(0)]
        [TestCase(51)]
        public void NextNRejectsUnboundedCount(int count)
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":" + count + ","
                + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
            Assert.That(parsed.IsValid, Is.False);
        }

        [Test]
        public void NextBirthsFromBoundProducer_IncludeOldQueueAndNeverTrainOrSwitchProducers()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int tx = x - 8; tx <= x + 18; tx++)
                    for (int tz = z - 8; tz <= z + 8; tz++)
                    {
                        sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        sim.MapData.ForestDensity[tx, tz] = 0;
                        sim.MapData.FoundationCount[tx, tz] = 0;
                        sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                sim.MapData.AddResourceNode(ResourceType.Wood,
                    sim.MapData.TileToWorldFixed(x + 5, z + 6), 1000);
                var first = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var second = sim.CreateBuilding(0, BuildingType.TownCenter, x + 10, z, false, true);
                first.AutoProduceVillagers = second.AutoProduceVillagers = false;
                first.EnqueueTraining(0, 2); // A human/pre-existing order must still be watched.
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":2,"
                    + "\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\","
                    + "\"resource\":\"Wood\"}]}");
                var candidate = manager.PrepareActionPlan(parsed, "Send the next two villagers from the first Town Center to wood.", 0);
                var graph = manager.ApproveActionPlan(candidate, 0);
                var goal = manager.SubmitSemanticGraph(graph).Single();
                var commands = new List<ICommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander) commands.Add(command); };

                Spawn(sim, second, 0);
                first.TrainingTicksRemaining = 1;
                sim.Tick(); // The old human queue, not a Commander receipt, creates this birth.
                manager.Tick(0);

                Assert.That(goal.GoalType.ToString(), Is.EqualTo("WatchFutureUnits"));
                Assert.That(Count(goal, "ObservedCount"), Is.EqualTo(1));
                Assert.That(Count(goal, "AssignedCount"), Is.Zero,
                    "Enqueue is not native acceptance of a unit assignment.");
                Assert.That(commands.OfType<TrainUnitCommand>(), Is.Empty,
                    "Watching births must never queue extra production.");
                Assert.That(commands.OfType<GatherCommand>().Count(), Is.EqualTo(1));

                sim.Tick(); // Apply the real native command before crediting assignment.
                manager.Tick(15);
                Assert.That(Count(goal, "AssignedCount"), Is.EqualTo(1));

                Spawn(sim, first, 0);
                manager.Tick(30);
                Assert.That(Count(goal, "AssignedCount"), Is.EqualTo(1));
                sim.Tick();
                manager.Tick(45);
                Assert.That(Count(goal, "ObservedCount"), Is.EqualTo(2));
                Assert.That(Count(goal, "AssignedCount"), Is.EqualTo(2));
                Assert.That(commands.OfType<GatherCommand>().Count(), Is.EqualTo(2));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void NewVillagersCanBeBoundToGatherWithoutSelectingOldVillagers()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager\",\"count\":5,\"quantityMode\":\"New\"},"
                + "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"count\":5,\"workers\":{\"state\":\"Any\"},"
                + "\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Any\"},"
                + "\"dependsOn\":[0],\"resultFromNode\":0}]}");

            Assert.That(parsed.IsValid, Is.True, "Allocation must accept a typed exact production result binding.");
            Assert.That(parsed.Nodes[1].ResultFromNode, Is.EqualTo(0));
        }

        [Test]
        public void ForeignUniqueGendarmeCannotBeAdmittedForEnglishCommander()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                sim.SetPlayerCivilizations(new[] { Civilization.English });
                var result = new CommanderIntentValidator().Validate(
                    new EnsureUnitCountIntent(0, 11, 1), sim, 0);
                Assert.That(result.IsValid, Is.False,
                    "A known foreign unique cannot become executable through the native training fallback.");
                StringAssert.Contains("unavailable", result.Reason.ToLowerInvariant());
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void TrainNewVillagersThenGather_BindsOnlyNativeAttributedBirthsEvenWithRallyOrders()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                var wood = sim.MapData.AddResourceNode(ResourceType.Wood,
                    sim.MapData.TileToWorldFixed(x + 5, z + 5), 1000);
                tc.HasRallyPoint = tc.RallyPointOnResource = true;
                tc.RallyPoint = wood.Position;
                tc.RallyPointResourceType = ResourceType.Wood;
                var old = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x - 5, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                old.IsVillager = true;
                old.CurrentHealth = old.MaxHealth = 100;
                old.State = UnitState.Idle;
                sim.ResourceManager.GetPlayerResources(0).Food = 10000;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                    + "\"count\":2,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Wood\"},"
                    + "\"dependsOn\":[0],\"resultFromNode\":0}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Train two new villagers and send only them to wood.", 0), 0);
                var goals = manager.SubmitSemanticGraph(approved);
                var produced = (EnsureUnitCountGoal)goals[0];
                var gathering = (AllocateWorkersGoal)goals[1];
                var gatherCommands = new List<GatherCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is GatherCommand gather) gatherCommands.Add(gather); };
                for (int step = 0; step < 80 && gathering.Status != CommanderGoalStatus.Completed; step++)
                {
                    manager.Tick(step * 15);
                    if (tc.IsTraining) tc.TrainingTicksRemaining = 1;
                    sim.Tick();
                }
                Assert.That(produced.Status, Is.EqualTo(CommanderGoalStatus.Completed), produced.StatusReason);
                Assert.That(produced.ResultUnitIds.Count, Is.EqualTo(2));
                Assert.That(produced.ResultUnitIds, Has.No.Member(old.Id));
                Assert.That(gathering.SelectedWorkerIds.OrderBy(id => id),
                    Is.EqualTo(produced.ResultUnitIds.OrderBy(id => id)),
                    "Only this goal's exact native training results may fill the worker binding.");
                Assert.That(gatherCommands.SelectMany(c => c.UnitIds).Distinct().OrderBy(id => id),
                    Is.EqualTo(produced.ResultUnitIds.OrderBy(id => id)));
                Assert.That(gathering.Status, Is.EqualTo(CommanderGoalStatus.Completed), gathering.StatusReason);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void TrainNewSpearmenThenPatrol_BindsNativeResultsNotOldOrHumanQueue()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                sim.SetPlayerAge(0, 3);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var barracks = sim.CreateBuilding(0, BuildingType.Barracks, x + 10, z, false, true);
                var gold = sim.MapData.AddResourceNode(ResourceType.Gold,
                    sim.MapData.TileToWorldFixed(x + 13, z + 5), 1000);
                var miner = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 11, z + 3),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                miner.IsVillager = true;
                miner.CurrentHealth = miner.MaxHealth = 100;
                miner.State = UnitState.MovingToGather;
                miner.TargetResourceNodeId = gold.Id;
                var old = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 8, z + 4),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                old.UnitType = 1;
                old.CurrentHealth = old.MaxHealth = 100;
                old.State = UnitState.Idle;
                barracks.EnqueueTraining(1, 2);
                var resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = 10000;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,"
                    + "\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Train two new spearmen and patrol my worked gold with them.", 0), 0);
                var goals = manager.SubmitSemanticGraph(approved);
                var produced = (EnsureUnitCountGoal)goals[0];
                var patrols = new List<PatrolCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is PatrolCommand patrol) patrols.Add(patrol); };
                int humanBirthId = -1;
                for (int step = 0; step < 90 && patrols.Count == 0; step++)
                {
                    manager.Tick(step * 15);
                    if (barracks.IsTraining) barracks.TrainingTicksRemaining = 1;
                    sim.Tick();
                    if (step == 0)
                        humanBirthId = sim.UnitRegistry.GetAllUnits()
                            .Where(u => u.UnitType == 1 && u.Id != old.Id).Select(u => u.Id).FirstOrDefault();
                }
                Assert.That(produced.Status, Is.EqualTo(CommanderGoalStatus.Completed), produced.StatusReason);
                Assert.That(produced.ResultUnitIds.Count, Is.EqualTo(2));
                Assert.That(produced.ResultUnitIds, Has.No.Member(old.Id));
                if (humanBirthId > 0) Assert.That(produced.ResultUnitIds, Has.No.Member(humanBirthId));
                Assert.That(patrols, Has.Count.EqualTo(1));
                Assert.That(patrols[0].UnitIds.OrderBy(id => id),
                    Is.EqualTo(produced.ResultUnitIds.OrderBy(id => id)));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void HumanTakeoverStaysInterruptedAfterProtectionTimeoutAndDoesNotReplaceUnit()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                sim.MapData.AddResourceNode(ResourceType.Wood,
                    sim.MapData.TileToWorldFixed(x + 6, z + 5), 1000);
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":2,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Send the next two villagers from my Town Center to wood.", 0), 0);
                var goal = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved).Single();
                Spawn(sim, tc, 0);
                int first = sim.UnitRegistry.GetAllUnits().Single(u => u.IsVillager).Id;
                sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { first }));
                manager.Tick(0);
                manager.Tick(915);
                Assert.That(goal.ObservedCount, Is.EqualTo(1));
                Assert.That(goal.AssignedCount, Is.Zero);
                Assert.That(goal.InterruptedCount, Is.EqualTo(1));
                Assert.That(sim.CommandBuffer.FlushCommands().OfType<GatherCommand>(), Is.Empty,
                    "The elapsed 900-tick human lease must not make this finite order reclaim the unit.");
                Spawn(sim, tc, 0);
                Assert.That(goal.ObservedCount, Is.EqualTo(2),
                    "Only the second requested birth may be counted, not a substitute for the first.");
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void CancelAndProducerLossRemoveSubscriptionWithoutSwitching()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var first = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var second = sim.CreateBuilding(0, BuildingType.TownCenter, x + 11, z, false, true);
                first.AutoProduceVillagers = second.AutoProduceVillagers = false;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":2,"
                    + "\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\","
                    + "\"resource\":\"Wood\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Use the next two villagers from Town Center one.", 0), 0);
                var lost = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved).Single();
                first.CurrentHealth = 0;
                Spawn(sim, second, 0);
                manager.Tick(0);
                Assert.That(lost.Status, Is.EqualTo(CommanderGoalStatus.Failed), lost.StatusReason);
                Assert.That(lost.ObservedCount, Is.Zero);

                // A separate finite watcher is cancelled by conversation reset.
                var next = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":1,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
                var nextApproved = manager.ApproveActionPlan(manager.PrepareActionPlan(next,
                    "Send the next villager from the remaining TC to wood.", 1), 1);
                var cancelled = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(nextApproved).Single();
                Assert.That(manager.CancelFutureSubscriptions(), Is.EqualTo(1));
                Spawn(sim, second, 0);
                Assert.That(cancelled.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
                Assert.That(cancelled.ObservedCount, Is.Zero);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void CancelBeforeNativeFlushSuppressesOnlyWatchersQueuedGatherCommand()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                sim.MapData.AddResourceNode(ResourceType.Wood,
                    sim.MapData.TileToWorldFixed(x + 6, z + 5), 1000);
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":1,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Send the next villager to wood.", 0), 0);
                var goal = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved).Single();
                Spawn(sim, tc, 0);
                int unitId = sim.UnitRegistry.GetAllUnits().Single(u => u.IsVillager).Id;
                manager.Tick(0); // Commander action is queued, not yet processed.
                var processed = new List<ICommand>();
                sim.LocalActionCommandProcessed += processed.Add;
                Assert.That(goal.AssignedCount, Is.Zero);
                Assert.That(manager.CancelGoal(goal.GoalId), Is.True);
                sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { unitId }));
                sim.Tick();
                Assert.That(processed.OfType<GatherCommand>(), Is.Empty,
                    "Cancelling the watcher must suppress its exact still-pending action, not the later human command.");
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void FoodSheepFutureWatcherDispatchesNativeSlaughterAndCreditsActualAssignment()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                var sheep = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 5, z + 4),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                sheep.IsSheep = true;
                sheep.CurrentHealth = sheep.MaxHealth = 100;
                sheep.State = UnitState.Idle;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":1,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Food\","
                    + "\"sourceKind\":\"Sheep\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Send the next villager to my sheep for food.", 0), 0);
                var goal = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved).Single();
                Spawn(sim, tc, 0);
                int villagerId = sim.UnitRegistry.GetAllUnits().Single(u => u.IsVillager && !u.IsSheep).Id;
                var slaughter = new List<SlaughterSheepCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is SlaughterSheepCommand sheepCommand) slaughter.Add(sheepCommand); };
                manager.Tick(0);
                Assert.That(slaughter.Count, Is.EqualTo(1));
                Assert.That(slaughter[0].SheepUnitId, Is.EqualTo(sheep.Id));
                Assert.That(slaughter[0].VillagerIds, Is.EqualTo(new[] { villagerId }));
                Assert.That(goal.AssignedCount, Is.Zero);
                sim.Tick();
                Assert.That(goal.AssignedCount, Is.EqualTo(1));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void FutureWatcherDoesNotCountBirthBeforeItsDependencyActivates()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager\",\"count\":1},"
                    + "{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":1,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\","
                    + "\"dependsOn\":[0]}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Ensure one villager, then watch the next one from this Town Center.", 0), 0);
                var watcher = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved)[1];
                ProducerBirthObservation skipped = null;
                sim.ProducerUnitProduced += birth => skipped = birth;
                Spawn(sim, tc, 0); // This satisfies prerequisite, but happened before watcher activation.
                Assert.That(watcher.ObservedCount, Is.Zero);
                manager.Tick(0);
                typeof(CommanderGoalManager).GetMethod("HandleFutureBirth", PrivateInstance)
                    .Invoke(manager, new object[] { skipped });
                Assert.That(watcher.ObservedCount, Is.Zero,
                    "Replaying a pre-activation birth after the prerequisite completes must not count it.");
                Spawn(sim, tc, 0);
                Assert.That(watcher.ObservedCount, Is.EqualTo(1));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void NativeRejectedAssignmentBlocksExactBirthWithoutCreditingOrSubstitution()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                RevealGrass(sim, x, z);
                var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                tc.AutoProduceVillagers = false;
                var wood = sim.MapData.AddResourceNode(ResourceType.Wood,
                    sim.MapData.TileToWorldFixed(x + 6, z + 5), 1000);
                manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{"
                    + "\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":1,"
                    + "\"producer\":\"TownCenter\",\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "Send the next villager to wood.", 0), 0);
                var goal = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(approved).Single();
                Spawn(sim, tc, 0);
                manager.Tick(0);
                wood.RemainingAmount = 0; // Native command rejects the now-depleted exact destination.
                sim.Tick();
                manager.Tick(15);
                Assert.That(goal.AssignedCount, Is.Zero);
                Assert.That(goal.ObservedCount, Is.EqualTo(1));
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
                Assert.That(sim.CommandBuffer.FlushCommands().OfType<TrainUnitCommand>(), Is.Empty);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void RevealGrass(GameSimulation sim, int x, int z)
        {
            for (int tx = x - 8; tx <= x + 20; tx++)
                for (int tz = z - 8; tz <= z + 10; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(0, tx, tz);
                }
        }

        private static int Count(CommanderGoal goal, string name)
        {
            var property = goal.GetType().GetProperty(name, BindingFlags.Public | PrivateInstance);
            Assert.That(property, Is.Not.Null, name + " is needed for honest task progress.");
            return (int)property.GetValue(goal);
        }

        private static void Spawn(GameSimulation sim, BuildingData producer, int type)
        {
            var spawn = typeof(GameSimulation).GetMethod("SpawnTrainedUnit", PrivateInstance);
            Assert.That(spawn, Is.Not.Null);
            spawn.Invoke(sim, new object[] { producer, type, 0, null });
        }
    }
}
