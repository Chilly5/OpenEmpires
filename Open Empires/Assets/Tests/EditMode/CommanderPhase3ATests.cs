using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3ATests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private Vector2Int baseTile;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            baseTile = simulation.MapData.BasePositions[0];
            MakeVisible(0);
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            SetAge(2);
            simulation.CreateBuilding(0, BuildingType.TownCenter, baseTile.x, baseTile.y, false, true).AutoProduceVillagers = false;
            Vector2Int opponent = simulation.MapData.BasePositions[1];
            simulation.CreateBuilding(1, BuildingType.TownCenter, opponent.x, opponent.y, false, true);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void EnsureUnitCount_Archer_Works()
        {
            CreateVillager(1); CreateBuilding(BuildingType.ArcheryRange); GiveResources(1000, 1000, 0);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(CommanderIntentCatalog.ArcherUnitType, 1);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            simulation.Tick();
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Food, Is.EqualTo(1000 - config.ArcherFoodCost));
            RunUntilComplete(manager, goal, 1500);
            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void EnsureUnitCount_Knight_Works()
        {
            SetAge(3); CreateVillager(1); CreateBuilding(BuildingType.Stables); GiveResources(1000, 0, 1000);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(CommanderIntentCatalog.KnightUnitType, 1);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            simulation.Tick();
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Gold, Is.EqualTo(1000 - config.KnightGoldCost));
            RunUntilComplete(manager, goal, 2000);
            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void EnsureUnitCount_Knight_WaitsForAgeThenResumes()
        {
            SetAge(2); CreateBuilding(BuildingType.Stables); CreateVillager(1); GiveResources(1000, 0, 1000);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(CommanderIntentCatalog.KnightUnitType, 1);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForPrerequisite));
            SetAge(3); manager.Tick(15);
            Assert.That(simulation.CommandBuffer.FlushCommands()[0], Is.TypeOf<TrainUnitCommand>());
        }

        [Test]
        public void EnsureUnitCount_MissingArcherRangeRequestsProductionBuildingAfterAge2()
        {
            SetAge(2); CreateVillager(1); GiveResources(0, 1000, 0);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(CommanderIntentCatalog.ArcherUnitType, 1);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            Assert.That(simulation.CommandBuffer.FlushCommands()[0], Is.TypeOf<PlaceBuildingCommand>());
        }

        [Test]
        public void EnsureUnitCount_AlreadySatisfied_CompletesImmediately()
        {
            for (int i = 0; i < 2; i++) CreateUnit(1, i + 1);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(1, 2);
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void EnsureUnitCount_AccountsForExistingTrainingQueue()
        {
            var barracks = CreateBuilding(BuildingType.Barracks); CreateVillager(1); GiveResources(1000, 1000, 0);
            CreateUnit(1, 1); barracks.TrainingQueue.Add(1);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(1, 2);
            manager.Tick(0);
            Assert.That(goal.LastObservedQueuedCount, Is.EqualTo(1));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForProduction));
        }

        [Test]
        public void ProductionPlanner_DistributesAcrossAvailableBuildings()
        {
            CreateVillager(1); CreateBuilding(BuildingType.Barracks, 7); CreateBuilding(BuildingType.Barracks, 14);
            GiveResources(1000, 1000, 0); var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitEnsureUnitCount(1, 3);
            manager.Tick(0); var first = (TrainUnitCommand)simulation.CommandBuffer.FlushCommands()[0];
            simulation.Tick(new List<ICommand> { first }); manager.Tick(15);
            var second = (TrainUnitCommand)simulation.CommandBuffer.FlushCommands()[0];
            Assert.That(second.BuildingId, Is.Not.EqualTo(first.BuildingId));
        }

        [Test]
        public void BuildStructureGoal_BuildsBarracks()
        {
            CreateVillager(1); GiveResources(0, 1000, 0); var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            simulation.Tick();
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Wood, Is.EqualTo(1000 - config.BarracksWoodCost));
            RunUntilComplete(manager, goal, 4000);
            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void BuildStructureGoal_RejectsInvalidBuilding()
        {
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitBuildStructure((BuildingType)9999); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed));
        }

        [Test]
        public void BuildStructureGoal_WaitsForResources()
        {
            CreateVillager(1); AddResource(ResourceType.Wood); var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForResources));
            Assert.That(simulation.CommandBuffer.FlushCommands()[0], Is.TypeOf<GatherCommand>());
        }

        [Test]
        public void BuildStructureGoal_RecoversFromBuilderDeath()
        {
            var foundation = simulation.CreateBuilding(0, BuildingType.Barracks, baseTile.x + 12, baseTile.y, true);
            var dead = CreateVillager(1); dead.ConstructionTargetBuildingId = foundation.Id; dead.State = UnitState.Dead; dead.CurrentHealth = 0;
            var backup = CreateVillager(2); var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitBuildStructure(BuildingType.Barracks); manager.Tick(0);
            var command = (ConstructBuildingCommand)simulation.CommandBuffer.FlushCommands()[0];
            Assert.That(command.UnitIds, Is.EqualTo(new[] { backup.Id }));
        }

        [Test]
        public void BuildStructureGoal_RejectsUnreachablePlacement()
        {
            var worker = CreateVillagerAt(baseTile.x + 30, baseTile.y + 30); Enclose(worker.SimPosition);
            GiveResources(0, 1000, 0); var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [TestCase(ResourceType.Food)]
        [TestCase(ResourceType.Wood)]
        [TestCase(ResourceType.Gold)]
        [TestCase(ResourceType.Stone)]
        public void ResourceAllocation_AssignsIdleWorkers(ResourceType resource)
        {
            CreateVillager(1); var node = AddResource(resource); var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitResourceAllocation(resource, 1); manager.Tick(0);
            var command = (GatherCommand)simulation.CommandBuffer.FlushCommands()[0];
            Assert.That(command.ResourceNodeId, Is.EqualTo(node.Id));
        }

        [Test]
        public void ResourceAllocation_IncreaseSnapshotsOneAdditionalWorker()
        {
            var node = AddResource(ResourceType.Gold);
            var worker = CreateVillager(1);
            worker.State = UnitState.Gathering;
            worker.TargetResourceNodeId = node.Id;
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitResourceAllocation(ResourceType.Gold, null, ResourceAllocationMode.Increase);
            Assert.That(goal.TargetWorkers, Is.EqualTo(2));
            var second = CreateVillager(2);
            second.State = UnitState.Gathering;
            second.TargetResourceNodeId = node.Id;
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(goal.TargetWorkers, Is.EqualTo(2));
        }

        [Test]
        public void ResourceAllocation_RespectsHumanControlledWorkers()
        {
            var human = CreateVillager(1); CreateVillager(2); var node = AddResource(ResourceType.Wood); AddResource(ResourceType.Food); human.State = UnitState.Gathering; human.TargetResourceNodeId = node.Id;
            var manager = new CommanderGoalManager(simulation, 0);
            simulation.CommandBuffer.EnqueueCommand(new GatherCommand(0, new[] { human.Id }, node.Id));
            var goal = manager.SubmitResourceAllocation(ResourceType.Food, 1); manager.Tick(0);
            var commands = simulation.CommandBuffer.FlushCommands();
            Assert.That(((GatherCommand)commands[commands.Count - 1]).UnitIds, Is.Not.Contains(human.Id));
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
        }

        [Test]
        public void ResourceAllocation_CompletesWhenTargetReached()
        {
            var node = AddResource(ResourceType.Wood); var worker = CreateVillager(1); worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id;
            var manager = new CommanderGoalManager(simulation, 0); var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 1); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
        }

        [Test]
        public void ResourceAllocation_TargetNineOfEightWorkersIsAlreadyComplete()
        {
            var node = AddResource(ResourceType.Wood);
            for (int i = 0; i < 9; i++) { var worker = CreateVillager(i + 1); worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id; }
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitResourceAllocation(ResourceType.Wood, 8); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(9));
        }

        [Test]
        public void ResourceAllocation_DoesNotThrashWorkers()
        {
            CreateVillager(1); AddResource(ResourceType.Wood); var manager = new CommanderGoalManager(simulation, 0); manager.SubmitResourceAllocation(ResourceType.Wood, 1);
            manager.Tick(0); simulation.CommandBuffer.FlushCommands(); manager.Tick(15);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void Constraint_ProtectsResourceWorkers()
        {
            var node = AddResource(ResourceType.Wood); AddResource(ResourceType.Food); var worker = CreateVillager(1); CreateVillager(2); worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id;
            var manager = new CommanderGoalManager(simulation, 0); manager.SubmitResourceAllocation(ResourceType.Food, 1, constraints: new[] { new ProtectedResourceConstraint(ResourceType.Wood) }); manager.Tick(0);
            Assert.That(((GatherCommand)simulation.CommandBuffer.FlushCommands()[0]).UnitIds, Is.Not.Contains(worker.Id));
        }

        [Test]
        public void Constraint_UsesIdleWorkersOnly()
        {
            var active = CreateVillager(1); active.State = UnitState.Gathering; var idle = CreateVillager(2); AddResource(ResourceType.Wood);
            var manager = new CommanderGoalManager(simulation, 0); manager.SubmitResourceAllocation(ResourceType.Wood, 1, constraints: new[] { new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly) }); manager.Tick(0);
            Assert.That(((GatherCommand)simulation.CommandBuffer.FlushCommands()[0]).UnitIds, Is.EqualTo(new[] { idle.Id }));
        }

        [Test]
        public void Constraint_ActiveWorkersOnlyBlocksWithoutIdleFallback()
        {
            var node = AddResource(ResourceType.Wood); var active = CreateVillager(1); active.State = UnitState.Gathering; active.TargetResourceNodeId = node.Id;
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitResourceAllocation(ResourceType.Food, 1,
                constraints: new[] { new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly) });
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void Constraint_ExplicitProtectedFloorAllowsSurplusButNotBelowFloor()
        {
            var wood = AddResource(ResourceType.Wood); AddResource(ResourceType.Food);
            var first = CreateVillager(1); first.State = UnitState.Gathering; first.TargetResourceNodeId = wood.Id;
            var second = CreateVillager(2); second.State = UnitState.Gathering; second.TargetResourceNodeId = wood.Id;
            var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitResourceAllocation(ResourceType.Food, 1,
                constraints: new[] { new ProtectedResourceConstraint(ResourceType.Wood, 1) });
            manager.Tick(0);
            var command = (GatherCommand)simulation.CommandBuffer.FlushCommands()[0];
            Assert.That(command.UnitIds, Has.Length.EqualTo(1));
            Assert.That(new[] { first.Id, second.Id }, Does.Contain(command.UnitIds[0]));
            simulation.Tick(new List<ICommand> { command });
            manager.SubmitResourceAllocation(ResourceType.Food, 2,
                constraints: new[] { new ProtectedResourceConstraint(ResourceType.Wood, 1) });
            manager.Tick(15);
            manager.Tick(30);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void BuildStructureGoal_DoesNotCompleteOnEnqueue()
        {
            CreateVillager(1); GiveResources(0, 1000, 0); var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitBuildStructure(BuildingType.Barracks); manager.Tick(0);
            simulation.CommandBuffer.FlushCommands();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            Assert.That(simulation.BuildingRegistry.GetAllBuildings().FindAll(b => b.Type == BuildingType.Barracks), Is.Empty);
        }

        [Test]
        public void BuildStructureGoal_TwoQueuedRequestsUseDistinctTargets()
        {
            CreateBuilding(BuildingType.Barracks); CreateVillager(1); GiveResources(0, 2000, 0);
            var manager = new CommanderGoalManager(simulation, 0);
            var first = manager.SubmitBuildStructure(BuildingType.Barracks);
            var second = manager.SubmitBuildStructure(BuildingType.Barracks);
            Assert.That(first.TargetTotal, Is.EqualTo(2));
            Assert.That(second.TargetTotal, Is.EqualTo(3));
            Assert.That(first.GoalId, Is.Not.EqualTo(second.GoalId));
        }

        [Test]
        public void EnsureUnitCount_FullPopulationWithQueuedTargetRequestsHouse()
        {
            var barracks = CreateBuilding(BuildingType.Barracks); CreateVillager(1); GiveResources(10000, 10000, 0);
            int offset = 20;
            while (simulation.GetPopulation(0) < simulation.GetPopulationCap(0)) CreateUnit(1, offset++);
            barracks.TrainingQueue.Add(1);
            var manager = new CommanderGoalManager(simulation, 0);
            var goal = manager.SubmitEnsureUnitCount(1, simulation.GetPopulation(0));
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            Assert.That(simulation.CommandBuffer.FlushCommands()[0], Is.TypeOf<PlaceBuildingCommand>());
        }

        [Test]
        public void Constraint_MaxQueueLimitsProduction()
        {
            var barracks = CreateBuilding(BuildingType.Barracks); CreateVillager(1); GiveResources(1000, 1000, 0); barracks.TrainingQueue.Add(1);
            var manager = new CommanderGoalManager(simulation, 0); var goal = manager.SubmitEnsureUnitCount(1, 3, constraints: new[] { new MaximumQueueConstraint(1) }); manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForProduction));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private BuildingData CreateBuilding(BuildingType type, int offset = 8) => simulation.CreateBuilding(0, type, baseTile.x + offset, baseTile.y, false);
        private void RunUntilComplete(CommanderGoalManager manager, CommanderGoal goal, int maximumTicks)
        {
            for (int i = 0; i < maximumTicks && !goal.IsTerminal; i++)
            {
                manager.Tick(simulation.CurrentTick);
                simulation.Tick();
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
        }
        private UnitData CreateVillager(int offset) => CreateVillagerAt(baseTile.x + 5 + offset, baseTile.y + 2);
        private UnitData CreateVillagerAt(int x, int z) { var u = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x, z), Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One); u.IsVillager = true; u.UnitType = 0; u.MaxHealth = u.CurrentHealth = 100; u.State = UnitState.Idle; return u; }
        private UnitData CreateUnit(int type, int offset) { var u = CreateVillagerAt(baseTile.x + 5 + offset, baseTile.y + 2); u.IsVillager = false; u.UnitType = type; return u; }
        private ResourceNodeData AddResource(ResourceType type)
        {
            var start = new Vector2Int(baseTile.x + 6, baseTile.y + 2);
            for (int x = baseTile.x + 5; x < baseTile.x + 18; x++)
                for (int z = baseTile.y + 5; z < baseTile.y + 18; z++)
                {
                    if (!simulation.MapData.IsBuildable(x, z) || !simulation.MapData.IsWalkable(x - 1, z)) continue;
                    if (!GridPathfinder.TryFindCompletePath(simulation.MapData, start, new Vector2Int(x - 1, z),
                        out _, 0, simulation.BuildingRegistry)) continue;
                    return simulation.MapData.AddResourceNode(type, simulation.MapData.TileToWorldFixed(x, z), 10000);
                }
            throw new InvalidOperationException("No reachable test resource site.");
        }
        private void GiveResources(int food, int wood, int gold) { simulation.ResourceManager.AddResource(0, ResourceType.Food, food); simulation.ResourceManager.AddResource(0, ResourceType.Wood, wood); simulation.ResourceManager.AddResource(0, ResourceType.Gold, gold); }
        private void SetAge(int age) { var field = typeof(GameSimulation).GetField("playerAges", BindingFlags.Instance | BindingFlags.NonPublic); var ages = (int[])field.GetValue(simulation); ages[0] = age; }
        private void MakeVisible(int player) { for (int x = 0; x < simulation.MapData.Width; x++) for (int z = 0; z < simulation.MapData.Height; z++) simulation.FogOfWar.SetVisible(player, x, z); }
        private void Enclose(FixedVector3 position) { var center = simulation.MapData.WorldToTile(position); for (int x = center.x - 2; x <= center.x + 2; x++) for (int z = center.y - 2; z <= center.y + 2; z++) if (Mathf.Abs(x - center.x) == 2 || Mathf.Abs(z - center.y) == 2) simulation.MapData.Tiles[x, z] = TileType.Water; }
    }
}
