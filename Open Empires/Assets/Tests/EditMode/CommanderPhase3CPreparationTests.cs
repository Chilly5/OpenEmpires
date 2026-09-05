using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3CPreparationTests
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
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sim.MapData, null);
            for (int a = x - 30; a <= x + 30; a++)
                for (int b = z - 20; b <= z + 20; b++)
                {
                    sim.MapData.Tiles[a, b] = TileType.Grass;
                    sim.MapData.ForestDensity[a, b] = 0;
                    sim.MapData.FoundationCount[a, b] = 0;
                    sim.FogOfWar.SetVisible(0, a, b);
                }
            manager = new CommanderGoalManager(sim, 0);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [TestCase(CommanderGoalStatus.WaitingForProduction)]
        [TestCase(CommanderGoalStatus.WaitingForConstruction)]
        [TestCase(CommanderGoalStatus.WaitingForPrerequisite)]
        [TestCase(CommanderGoalStatus.WaitingForResources)]
        public void MultipleGoals_WaitingGoalDoesNotBlockRunnableGoal(CommanderGoalStatus waitingStatus)
        {
            var waiting = WaitingGoal(waitingStatus, out int tick);
            var available = Worker(x - 8);
            var food = Resource(ResourceType.Food, x - 2);
            var runnable = manager.SubmitResourceAllocation(ResourceType.Food, 1);
            manager.Tick(tick);
            var command = SingleGather();
            Assert.That(waiting.Status, Is.EqualTo(waitingStatus));
            Assert.That(command.UnitIds, Is.EqualTo(new[] { available.Id }));
            Assert.That(command.ResourceNodeId, Is.EqualTo(food.Id));
            Assert.That(manager.GetWorkerReservation(available.Id).Value.GoalId, Is.EqualTo(runnable.GoalId));
            Assert.That(manager.ActiveGoal, Is.SameAs(runnable));
        }

        [Test]
        public void MultipleGoals_CompletesIndependentGoals()
        {
            var firstWorker = Worker(x - 10); var secondWorker = Worker(x - 9);
            var wood = Resource(ResourceType.Wood, x); var food = Resource(ResourceType.Food, x + 5);
            var first = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            var second = manager.SubmitResourceAllocation(ResourceType.Food, 1);
            manager.Tick(0); var firstCommand = SingleGather();
            Assert.That(firstCommand.UnitIds, Is.EqualTo(new[] { firstWorker.Id }));
            AssignGather(firstWorker, wood);
            manager.Tick(15); var secondCommand = SingleGather();
            Assert.That(first.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(secondCommand.UnitIds, Is.EqualTo(new[] { secondWorker.Id }));
            Assert.That(manager.GetWorkerReservation(firstWorker.Id), Is.Null);
            AssignGather(secondWorker, food);
            manager.Tick(30);
            Assert.That(second.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(manager.GetWorkerReservation(secondWorker.Id), Is.Null);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test, Repeat(3)]
        public void MultipleGoals_DeterministicOrdering()
        {
            var workers = new[] { Worker(x - 10), Worker(x - 9), Worker(x - 8) };
            var resources = new[] { Resource(ResourceType.Wood, x), Resource(ResourceType.Food, x + 5), Resource(ResourceType.Gold, x + 10) };
            var goals = resources.Select(node => manager.SubmitResourceAllocation(node.Type, 1)).ToArray();
            for (int i = 0; i < goals.Length; i++)
            {
                manager.Tick(i * 15);
                var command = SingleGather();
                Assert.That(command.UnitIds, Is.EqualTo(new[] { workers[i].Id }), "FIFO goals and lowest eligible worker ID must be stable.");
                Assert.That(command.ResourceNodeId, Is.EqualTo(resources[i].Id));
                Assert.That(manager.GetWorkerReservation(workers[i].Id).Value.GoalId, Is.EqualTo(goals[i].GoalId));
                AssignGather(workers[i], resources[i]);
                manager.Tick(i * 15);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Repeated evaluation of one tick must not emit another command.");
                manager.Tick(i * 15 + 1);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Only the existing 15-tick cadence may emit commands.");
            }
        }

        [Test]
        public void MultipleGoals_ConflictingWorkerCommandsResolveSafely()
        {
            var worker = Worker(x - 8);
            Resource(ResourceType.Wood, x); var food = Resource(ResourceType.Food, x + 5);
            var first = manager.SubmitResourceAllocation(ResourceType.Wood, 2);
            var second = manager.SubmitResourceAllocation(ResourceType.Food, 1);
            manager.Tick(0); SingleGather();
            // The first command need not execute for its reservation to exclude another goal.
            Assert.That(worker.State, Is.EqualTo(UnitState.Idle));
            manager.Tick(15);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(second.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.GoalId, Is.EqualTo(first.GoalId));
            manager.CancelGoal(first.GoalId);
            manager.Tick(165);
            Assert.That(SingleGather().ResourceNodeId, Is.EqualTo(food.Id));
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.GoalId, Is.EqualTo(second.GoalId));
        }

        [Test]
        public void Goals_HaveStableIdentity()
        {
            var first = manager.SubmitEnsureUnitCount(1, 1);
            var second = manager.SubmitBuildStructure(BuildingType.House);
            var third = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            Assert.That(new[] { first.GoalId, second.GoalId, third.GoalId }, Is.EqualTo(new[] { 1, 2, 3 }));
            manager.CancelGoal(second.GoalId);
            var fourth = manager.SubmitBuildStructure(BuildingType.Barracks);
            Assert.That(fourth.GoalId, Is.EqualTo(4));
            foreach (var goal in manager.Goals)
            {
                Assert.That(manager.GetGoal(goal.GoalId), Is.SameAs(goal));
                Assert.That(goal.PlayerId, Is.Zero);
                Assert.That(goal.CreatedTick, Is.EqualTo(sim.CurrentTick));
                Assert.That(goal.ParentGoalId, Is.Null);
                Assert.That(goal.Priority, Is.Zero);
            }
            Assert.That(manager.GetGoal(-1), Is.Null);
            Assert.That(manager.GetGoal(999), Is.Null);
            Assert.That(new EnsureUnitCountGoal(0, 1, 1, priority: 7).Priority, Is.EqualTo(7));
        }

        [Test]
        public void Goals_CanBeTrackedIndependently()
        {
            var first = manager.SubmitResourceAllocation(ResourceType.Wood, 0);
            var second = manager.SubmitResourceAllocation(ResourceType.Food, 1);
            manager.CancelGoal(second.GoalId);
            manager.Tick(0);
            Assert.That(manager.GetGoal(first.GoalId).Lifecycle, Is.EqualTo(CommanderGoalLifecycle.Completed));
            Assert.That(manager.GetGoal(second.GoalId).Lifecycle, Is.EqualTo(CommanderGoalLifecycle.Cancelled));
            Assert.That(manager.Goals, Has.Count.EqualTo(2), "Terminal identities remain queryable.");
        }

        [Test]
        public void GoalLifecycle_RemainsDeterministic()
        {
            var worker = Worker(x - 8); var node = Resource(ResourceType.Wood, x);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            var lifecycle = new List<CommanderGoalLifecycle> { goal.Lifecycle };
            manager.GoalStatusChanged += changed => { if (changed.GoalId == goal.GoalId) lifecycle.Add(changed.Lifecycle); };
            manager.Tick(0); SingleGather();
            AssignGather(worker, node);
            manager.Tick(15); manager.Tick(15); manager.Tick(30);
            Assert.That(lifecycle, Is.EqualTo(new[] { CommanderGoalLifecycle.Created, CommanderGoalLifecycle.Waiting, CommanderGoalLifecycle.Completed }));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
        }

        [Test]
        public void WorkerReservation_AssignsGoalOwnership()
        {
            var worker = Worker(x - 8); Resource(ResourceType.Wood, x);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.Tick(0); SingleGather();
            var reservation = manager.GetWorkerReservation(worker.Id).Value;
            Assert.That(reservation.WorkerId, Is.EqualTo(worker.Id));
            Assert.That(reservation.PlayerId, Is.Zero);
            Assert.That(reservation.GoalId, Is.EqualTo(goal.GoalId));
            Assert.That(reservation.ReservationType, Is.EqualTo(CommanderWorkerReservationType.Gatherer));
            Assert.That(reservation.CreatedTick, Is.Zero);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.CreatedTick, Is.EqualTo(reservation.CreatedTick));
        }

        [Test]
        public void WorkerReservation_BuilderCommandAutomaticallyReservesWorker()
        {
            var worker = Worker(x - 8);
            var foundation = sim.CreateBuilding(0, BuildingType.Barracks, x, z, true);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks);
            manager.Tick(0);
            var commands = sim.CommandBuffer.FlushCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<ConstructBuildingCommand>());
            Assert.That(((ConstructBuildingCommand)commands[0]).TargetBuildingId, Is.EqualTo(foundation.Id));
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.ReservationType, Is.EqualTo(CommanderWorkerReservationType.Builder));
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.GoalId, Is.EqualTo(goal.GoalId));
        }

        [Test]
        public void WorkerReservation_DoesNotOverrideHumanCommand()
        {
            var worker = Worker(x - 8); var node = Resource(ResourceType.Wood, x);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
            sim.CommandBuffer.EnqueueCommand(new GatherCommand(0, new[] { worker.Id }, node.Id));
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null, "Human enqueue releases ownership before command execution.");
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.False);
            manager.Tick(0);
            Assert.That(sim.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1), "Only the human command should remain.");
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [Test]
        public void WorkerReservation_PreventsGoalConflict()
        {
            var worker = Worker(x - 8);
            var first = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            var second = manager.SubmitBuildStructure(BuildingType.House);
            Assert.That(manager.TryReserveWorker(first.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
            Assert.That(manager.TryReserveWorker(second.GoalId, worker.Id, CommanderWorkerReservationType.Builder), Is.False);
            Assert.That(manager.GetWorkerReservation(worker.Id).Value.GoalId, Is.EqualTo(first.GoalId));
        }

        [Test]
        public void WorkerReservation_ReleasesAfterGoalCompletion()
        {
            var worker = Worker(x - 8); var node = Resource(ResourceType.Wood, x);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer);
            AssignGather(worker, node); manager.Tick(0);
            Assert.That(goal.Lifecycle, Is.EqualTo(CommanderGoalLifecycle.Completed));
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorkerReservation_ReleasesAfterCancellationOrFailure(bool fail)
        {
            var worker = Worker(x - 8);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1, maxDurationTicks: 15);
            manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer);
            if (fail) manager.Tick(15); else manager.CancelGoal(goal.GoalId);
            Assert.That(goal.Lifecycle, Is.EqualTo(fail ? CommanderGoalLifecycle.Failed : CommanderGoalLifecycle.Cancelled));
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null);
            var next = manager.SubmitResourceAllocation(ResourceType.Food, 1);
            Assert.That(manager.TryReserveWorker(next.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorkerReservation_ReleasesDeadOrGarrisonedWorker(bool garrison)
        {
            var worker = Worker(x - 8);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer);
            if (garrison) sim.UnitRegistry.GarrisonUnit(worker.Id);
            else { worker.CurrentHealth = 0; worker.State = UnitState.Dead; }
            manager.Tick(0);
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null);
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.False);
        }

        [Test]
        public void WorkerReservation_RejectsForeignNonWorkerAndUnknownGoal()
        {
            var foreign = Worker(x - 8, 1); var soldier = Worker(x - 9); soldier.IsVillager = false; soldier.UnitType = 1;
            var owned = Worker(x - 10); var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            Assert.That(manager.TryReserveWorker(goal.GoalId, foreign.Id, CommanderWorkerReservationType.Gatherer), Is.False);
            Assert.That(manager.TryReserveWorker(goal.GoalId, soldier.Id, CommanderWorkerReservationType.Gatherer), Is.False);
            Assert.That(manager.TryReserveWorker(999, owned.Id, CommanderWorkerReservationType.Gatherer), Is.False);
            Assert.That(manager.TryReserveWorker(goal.GoalId, owned.Id, (CommanderWorkerReservationType)999), Is.False);
            Assert.That(manager.GetWorkerReservation(owned.Id), Is.Null);
        }

        [Test]
        public void WorkerReservation_DoesNotBypassProtectedResourceFloor()
        {
            var worker = Worker(x - 8); var wood = Resource(ResourceType.Wood, x); Resource(ResourceType.Food, x + 5);
            AssignGather(worker, wood);
            var goal = manager.SubmitResourceAllocation(ResourceType.Food, 1,
                constraints: new[] { new ProtectedResourceConstraint(ResourceType.Wood, 1) });
            Assert.That(manager.TryReserveWorker(goal.GoalId, worker.Id, CommanderWorkerReservationType.Gatherer), Is.True);
            manager.Tick(0);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(wood.Id));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        private CommanderGoal WaitingGoal(CommanderGoalStatus status, out int tick)
        {
            tick = 0;
            if (status == CommanderGoalStatus.WaitingForConstruction)
            {
                var foundation = sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, true);
                var builder = Worker(x + 4); builder.State = UnitState.MovingToBuild; builder.ConstructionTargetBuildingId = foundation.Id;
                return manager.SubmitBuildStructure(BuildingType.Barracks);
            }
            if (status == CommanderGoalStatus.WaitingForResources)
            {
                Worker(x - 12); Resource(ResourceType.Wood, x + 10);
                var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 2);
                manager.Tick(0); SingleGather(); tick = 15;
                return goal;
            }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true).AutoProduceVillagers = false;
            if (status == CommanderGoalStatus.WaitingForPrerequisite) return manager.SubmitEnsureUnitCount(7, 1);
            var producer = sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            producer.TrainingQueue.Add(1);
            return manager.SubmitEnsureUnitCount(1, 1);
        }

        private UnitData Worker(int tileX, int player = 0)
        {
            var unit = sim.UnitRegistry.CreateUnit(player, sim.MapData.TileToWorldFixed(tileX, z), Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true; unit.UnitType = 0; unit.CurrentHealth = unit.MaxHealth = 100; unit.State = UnitState.Idle;
            return unit;
        }
        private ResourceNodeData Resource(ResourceType type, int tileX) => sim.MapData.AddResourceNode(type, sim.MapData.TileToWorldFixed(tileX, z + 6), 10000);
        private static void AssignGather(UnitData worker, ResourceNodeData node) { worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id; }
        private GatherCommand SingleGather()
        {
            var commands = sim.CommandBuffer.FlushCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<GatherCommand>());
            return (GatherCommand)commands[0];
        }
    }
}
