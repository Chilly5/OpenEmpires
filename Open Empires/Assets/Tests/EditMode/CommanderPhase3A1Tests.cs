using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3A1Tests
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
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
            // Deterministic test arena; fixture mutation only, never Commander gameplay.
            typeof(MapData).GetField("holeMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sim.MapData, null);
            for (int a = x - 35; a <= x + 35; a++)
                for (int b = z - 15; b <= z + 15; b++)
                {
                    sim.MapData.Tiles[a, b] = TileType.Grass;
                    sim.MapData.ForestDensity[a, b] = 0;
                    sim.MapData.FoundationCount[a, b] = 0;
                }
            manager = new CommanderGoalManager(sim, 0);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [TestCase(TileVisibility.Explored, true)]
        [TestCase(TileVisibility.Unexplored, false)]
        public void FogPath_VisibleMiddleVisible_UsesKnowledgeBoundary(TileVisibility middle, bool accepted)
        {
            if (middle == TileVisibility.Explored)
            {
                sim.FogOfWar.SetVisible(0, x + 1, z);
                sim.FogOfWar.DemoteAllVisible(0);
            }
            sim.FogOfWar.SetVisible(0, x, z);
            sim.FogOfWar.SetVisible(0, x + 2, z);
            Assert.That(sim.FogOfWar.GetVisibility(0, x + 1, z), Is.EqualTo(middle));
            object planner = typeof(CommanderGoalManager).GetField("planner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            bool result = (bool)planner.GetType().GetMethod("IsKnownPath", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(planner, new object[] { 0, new Vector2Int(x, z), new List<Vector2Int> { new(x + 1, z), new(x + 2, z) } });
            Assert.That(result, Is.EqualTo(accepted));
            Assert.That(sim.FogOfWar.GetVisibility(0, x + 1, z), Is.EqualTo(middle), "Planning must not reveal terrain.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ResourcePath_ExploredCorridorAccepted_HiddenCorridorRejected(bool explored)
        {
            Worker(x - 20);
            var node = sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x, z), 1000);
            if (explored) { VisibleArena(); sim.FogOfWar.DemoteAllVisible(0); }
            sim.FogOfWar.SetVisible(0, x - 20, z);
            sim.FogOfWar.SetVisible(0, x, z);
            sim.FogOfWar.SetVisible(0, x - 1, z);
            Assert.That(sim.FogOfWar.GetVisibility(0, x - 10, z),
                Is.EqualTo(explored ? TileVisibility.Explored : TileVisibility.Unexplored));
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.Tick(0);
            var commands = sim.CommandBuffer.FlushCommands();
            if (explored)
            {
                Assert.That(commands, Has.Count.EqualTo(1));
                Assert.That(((GatherCommand)commands[0]).ResourceNodeId, Is.EqualTo(node.Id));
            }
            else { Assert.That(commands, Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked)); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceTarget_NotCurrentlyVisible_IsNotSelected(bool previouslySeen)
        {
            Worker(x - 2);
            sim.MapData.AddResourceNode(ResourceType.Gold, sim.MapData.TileToWorldFixed(x, z), 1000);
            if (previouslySeen) { VisibleArena(); sim.FogOfWar.DemoteAllVisible(0); }
            sim.FogOfWar.SetVisible(0, x - 2, z);
            var goal = manager.SubmitResourceAllocation(ResourceType.Gold, 1);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(UnitState.MovingToBuild)]
        [TestCase(UnitState.Constructing)]
        public void ConstructionTravelTime_DoesNotTriggerStall(UnitState travellingState)
        {
            var foundation = Foundation();
            var builder = Assign(Worker(x - 20), foundation, travellingState);
            Worker(x - 2);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks);
            for (int tick = 0; tick <= 600; tick += 15) manager.Tick(tick);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));
            Assert.That(goal.StatusReason, Does.Contain("travelling"));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(builder.ConstructionTargetBuildingId, Is.EqualTo(foundation.Id));
        }

        [Test]
        public void ConstructionAfterArrival_StallRecoveryWorks()
        {
            var foundation = Foundation();
            var builder = Assign(Worker(x - 20), foundation, UnitState.MovingToBuild);
            var backup = Worker(x - 2);
            manager.SubmitBuildStructure(BuildingType.Barracks);
            manager.Tick(0); manager.Tick(600);
            builder.SimPosition = sim.MapData.TileToWorldFixed(x - 1, z);
            builder.State = UnitState.Constructing;
            manager.Tick(615); manager.Tick(750);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Full grace window starts at arrival, not dispatch.");
            manager.Tick(765);
            AssertRecovery(backup, foundation);
            manager.Tick(780);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Recovery retains its cooldown.");
        }

        [Test]
        public void ConstructionDeadBuilder_StillRecovers()
        {
            var foundation = Foundation();
            var builder = Assign(Worker(x - 20), foundation, UnitState.MovingToBuild);
            var backup = Worker(x - 2);
            manager.SubmitBuildStructure(BuildingType.Barracks);
            manager.Tick(0);
            builder.CurrentHealth = 0; builder.State = UnitState.Dead;
            manager.Tick(15);
            AssertRecovery(backup, foundation);
        }

        [Test]
        public void ConstructionArrivedBuilder_NotMaskedByLowerIdTraveller()
        {
            var foundation = Foundation();
            Assign(Worker(x - 20), foundation, UnitState.MovingToBuild);
            Assign(Worker(x - 1), foundation, UnitState.Constructing);
            var backup = Worker(x - 2);
            manager.SubmitBuildStructure(BuildingType.Barracks);
            manager.Tick(0); manager.Tick(150);
            AssertRecovery(backup, foundation);
        }

        [Test]
        public void ConstructionProgress_ResetsArrivalStallWindow()
        {
            var foundation = Foundation();
            Assign(Worker(x - 1), foundation, UnitState.Constructing);
            var backup = Worker(x - 2);
            manager.SubmitBuildStructure(BuildingType.Barracks);
            manager.Tick(0);
            foundation.ConstructionTicksRemaining--;
            manager.Tick(135); manager.Tick(270);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            manager.Tick(285);
            AssertRecovery(backup, foundation);
        }

        [Test]
        public void BlockedGoal_DoesNotFreezeFutureCommands()
        {
            var blocked = BlockedKnight();
            var next = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.Tick(15);
            Assert.That(blocked.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(next.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources));
            Assert.That(sim.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
            Assert.That(manager.Goals, Does.Contain(blocked), "Deferred goals must be retained.");
        }

        [Test]
        public void BlockedGoal_RecoversAfterConditionChanges()
        {
            var blocked = BlockedKnight();
            sim.ResourceManager.AddResource(0, ResourceType.Gold, 1000);
            manager.Tick(135);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Retry is bounded, not every planning tick.");
            manager.Tick(150);
            Assert.That(blocked.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            Assert.That(sim.CommandBuffer.FlushCommands()[0], Is.TypeOf<TrainUnitCommand>());
        }

        [Test]
        public void BlockedGoal_FailsAfterTimeout()
        {
            var blocked = BlockedKnight();
            int failures = 0;
            manager.GoalEventPublished += e => { if (e.EventType == CommanderGoalEventType.GoalFailed) failures++; };
            for (int tick = 150; tick < 1800; tick += 150) manager.Tick(tick);
            Assert.That(blocked.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            manager.Tick(1800); manager.Tick(1815);
            Assert.That(blocked.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(blocked.StatusReason, Does.Contain("Blocked for 1800 ticks"));
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void BlockedGoal_ConditionResolvesAtDeadline_Recovers()
        {
            var goal = BlockedKnight();
            sim.ResourceManager.AddResource(0, ResourceType.Gold, 1000);
            manager.Tick(1800);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
        }

        [Test]
        public void BlockedGoal_Cancelled_DoesNotRetry()
        {
            var goal = BlockedKnight();
            Assert.That(manager.CancelGoal(goal.GoalId), Is.True);
            sim.ResourceManager.AddResource(0, ResourceType.Gold, 1000);
            manager.Tick(150);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void BlockedRetry_AndLaterGoal_EmitAtMostOneCommandPerTick()
        {
            BlockedKnight();
            manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            sim.ResourceManager.AddResource(0, ResourceType.Gold, 1000);
            manager.Tick(150); manager.Tick(150);
            var commands = sim.CommandBuffer.FlushCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<TrainUnitCommand>());
        }

        [Test]
        public void AgePrerequisiteWait_YieldsToLaterResourceRequest()
        {
            var knight = BlockedKnight();
            Ages()[0] = 2;
            var wood = manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.Tick(150);
            Assert.That(knight.Status, Is.EqualTo(CommanderGoalStatus.WaitingForPrerequisite));
            Assert.That(wood.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources));
            Assert.That(sim.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
        }

        [Test]
        public void DeferredAndQueuedGoals_RetainOverallDurationLimit()
        {
            BlockedKnight();
            var queued = manager.SubmitResourceAllocation(ResourceType.Wood, 1, maxDurationTicks: 30);
            manager.Tick(30);
            Assert.That(queued.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(queued.StatusReason, Does.Contain("duration limit"));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private EnsureUnitCountGoal BlockedKnight()
        {
            Worker(x - 2);
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true).AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.Stables, x + 8, z, false);
            sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x, z), 10000);
            VisibleArena(); Ages()[0] = 3;
            sim.ResourceManager.GetPlayerResources(0).Food = 1000;
            sim.ResourceManager.GetPlayerResources(0).Gold = 0;
            // Only expose our wood node; generated gold nodes must remain unknown.
            sim.FogOfWar.DemoteAllVisible(0);
            sim.FogOfWar.SetVisible(0, x, z);
            var goal = manager.SubmitEnsureUnitCount(7, 1, maxDurationTicks: 0);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            return goal;
        }
        private int[] Ages() => (int[])typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim);
        private void VisibleArena() { for (int a = x - 35; a <= x + 35; a++) for (int b = z - 15; b <= z + 15; b++) sim.FogOfWar.SetVisible(0, a, b); }
        private BuildingData Foundation() { VisibleArena(); return sim.CreateBuilding(0, BuildingType.Barracks, x, z, true); }
        private UnitData Worker(int tileX)
        {
            var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(tileX, z), Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true; unit.UnitType = 0; unit.MaxHealth = unit.CurrentHealth = 100; unit.State = UnitState.Idle;
            return unit;
        }
        private static UnitData Assign(UnitData worker, BuildingData building, UnitState state) { worker.ConstructionTargetBuildingId = building.Id; worker.State = state; return worker; }
        private void AssertRecovery(UnitData backup, BuildingData building)
        {
            var commands = sim.CommandBuffer.FlushCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<ConstructBuildingCommand>());
            var command = (ConstructBuildingCommand)commands[0];
            Assert.That(command.UnitIds, Is.EqualTo(new[] { backup.Id }));
            Assert.That(command.TargetBuildingId, Is.EqualTo(building.Id));
        }
    }
}
