using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase1Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private Vector2Int baseTile;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            baseTile = simulation.MapData.BasePositions != null && simulation.MapData.BasePositions.Length > 0
                ? simulation.MapData.BasePositions[0]
                : new Vector2Int(simulation.MapData.Width / 2, simulation.MapData.Height / 2);
            MakeAreaVisible(0);
            simulation.CreateBuilding(0, BuildingType.TownCenter, baseTile.x, baseTile.y,
                underConstruction: false, isMainTownCenter: true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void AlreadySatisfied_CompletesWithoutIssuingTraining()
        {
            for (int i = 0; i < 10; i++) CreateUnit(0, 1, false, i + 1);
            var manager = new CommanderGoalManager(simulation, 0);
            EnsureUnitCountGoal goal = manager.SubmitEnsureUnitCount(1, 10);

            manager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void PartialCount_PlansOnlyRemainingProduction()
        {
            CreateVillager(0);
            CreateBarracks(0);
            GiveResources(0, food: 1000, wood: 1000);
            for (int i = 0; i < 4; i++) CreateUnit(0, 1, false, i + 2);
            var manager = new CommanderGoalManager(simulation, 0);
            EnsureUnitCountGoal goal = manager.SubmitEnsureUnitCount(1, 10);

            manager.Tick(0);
            List<ICommand> commands = simulation.CommandBuffer.FlushCommands();

            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(4));
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<TrainUnitCommand>());
        }

        [Test]
        public void MissingBarracks_EmitsLegitimatePlaceBuildingCommand()
        {
            UnitData villager = CreateVillager(0);
            GiveResources(0, food: 1000, wood: config.BarracksWoodCost);
            var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitEnsureUnitCount(1, 10);

            manager.Tick(0);
            List<ICommand> commands = simulation.CommandBuffer.FlushCommands();

            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<PlaceBuildingCommand>());
            var place = (PlaceBuildingCommand)commands[0];
            Assert.That(place.BuildingType, Is.EqualTo(BuildingType.Barracks));
            Assert.That(place.PlayerId, Is.EqualTo(0));
            Assert.That(place.VillagerUnitIds, Is.EqualTo(new[] { villager.Id }));
        }

        [Test]
        public void InsufficientBarracksWood_ReassignsOwnedVillagerWithoutCheatingResources()
        {
            UnitData villager = CreateVillager(0);
            ResourceNodeData wood = AddKnownResource(ResourceType.Wood, 10);
            var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitEnsureUnitCount(1, 10);
            int woodBefore = simulation.ResourceManager.GetPlayerResources(0).Wood;

            manager.Tick(0);
            List<ICommand> commands = simulation.CommandBuffer.FlushCommands();

            Assert.That(commands, Has.Count.EqualTo(1));
            var gather = (GatherCommand)commands[0];
            Assert.That(gather.UnitIds, Is.EqualTo(new[] { villager.Id }));
            Assert.That(gather.ResourceNodeId, Is.EqualTo(wood.Id));
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Wood, Is.EqualTo(woodBefore));
        }

        [Test]
        public void PopulationBlocked_EmitsHousePlacementBeforeMoreTraining()
        {
            CreateVillager(0);
            CreateBarracks(0);
            GiveResources(0, food: 1000, wood: 1000);
            int sequence = 1;
            while (simulation.GetPopulation(0) < simulation.GetPopulationCap(0))
                CreateUnit(0, 4, false, ++sequence);
            var manager = new CommanderGoalManager(simulation, 0);
            manager.SubmitEnsureUnitCount(1, 10);

            manager.Tick(0);
            List<ICommand> commands = simulation.CommandBuffer.FlushCommands();

            Assert.That(commands, Has.Count.EqualTo(1));
            var place = (PlaceBuildingCommand)commands[0];
            Assert.That(place.BuildingType, Is.EqualTo(BuildingType.House));
        }

        [Test]
        public void ExistingTrainingQueue_IsCountedAndPreventsBlindOverQueueing()
        {
            CreateVillager(0);
            BuildingData barracks = CreateBarracks(0);
            GiveResources(0, food: 1000, wood: 1000);
            for (int i = 0; i < 5; i++) CreateUnit(0, 1, false, i + 1);
            barracks.TrainingQueue.Add(1);
            barracks.TrainingQueue.Add(1);
            barracks.TrainingQueue.Add(1);
            var manager = new CommanderGoalManager(simulation, 0);
            EnsureUnitCountGoal goal = manager.SubmitEnsureUnitCount(1, 10);

            manager.Tick(0);
            List<ICommand> commands = simulation.CommandBuffer.FlushCommands();

            Assert.That(goal.LastObservedQueuedCount, Is.EqualTo(3));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForProduction));
            Assert.That(commands, Is.Empty);
        }

        [Test]
        public void UnitDeathBeforeCompletion_ReevaluationRestoresMissingOrder()
        {
            CreateVillager(0);
            BuildingData barracks = CreateBarracks(0);
            GiveResources(0, food: 1000, wood: 1000);
            UnitData first = null;
            for (int i = 0; i < 8; i++)
            {
                UnitData unit = CreateUnit(0, 1, false, i + 1);
                first ??= unit;
            }
            barracks.TrainingQueue.Add(1);
            barracks.TrainingQueue.Add(1);
            var manager = new CommanderGoalManager(simulation, 0);
            EnsureUnitCountGoal goal = manager.SubmitEnsureUnitCount(1, 10);
            manager.Tick(0);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            first.CurrentHealth = 0;
            first.State = UnitState.Dead;
            manager.Tick(15);

            Assert.That(goal.LastObservedOwnedCount, Is.EqualTo(7));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
        }

        [Test]
        public void Cancellation_StopsFutureCommanderActions()
        {
            CreateVillager(0);
            AddKnownResource(ResourceType.Wood, 10);
            var manager = new CommanderGoalManager(simulation, 0);
            EnsureUnitCountGoal goal = manager.SubmitEnsureUnitCount(1, 10);

            Assert.That(manager.CancelGoal(goal.GoalId), Is.True);
            manager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void GatherCommand_CannotControlForeignVillager()
        {
            UnitData foreign = CreateVillager(1);
            ResourceNodeData wood = AddKnownResource(ResourceType.Wood, 10);

            simulation.Tick(new List<ICommand>
            {
                new GatherCommand(0, new[] { foreign.Id }, wood.Id)
            });

            Assert.That(foreign.TargetResourceNodeId, Is.EqualTo(-1));
            Assert.That(foreign.State, Is.EqualTo(UnitState.Idle));
        }

        [Test]
        public void ForeignControlledUnits_AreRejectedByMoveStopAndAttack()
        {
            UnitData foreign = CreateUnit(1, 1, false, 3);
            UnitData enemyTarget = CreateUnit(1, 1, false, 4);
            FixedVector3 originalPosition = foreign.SimPosition;

            simulation.Tick(new List<ICommand>
            {
                new MoveCommand(0, new[] { foreign.Id }, simulation.MapData.TileToWorldFixed(baseTile.x + 1, baseTile.y + 1)),
                new StopCommand(0, new[] { foreign.Id }),
                new AttackUnitCommand(0, new[] { foreign.Id }, enemyTarget.Id)
            });

            Assert.That(foreign.SimPosition, Is.EqualTo(originalPosition));
            Assert.That(foreign.State, Is.EqualTo(UnitState.Idle));
            Assert.That(foreign.CombatTargetId, Is.EqualTo(-1));
        }

        [Test]
        public void ForeignTowerUpgrade_IsRejectedWithoutSpendingOwnerResources()
        {
            BuildingData tower = simulation.CreateBuilding(1, BuildingType.Tower,
                baseTile.x + 12, baseTile.y, underConstruction: false);
            GiveResources(1, food: 0, wood: 1000);
            int before = simulation.ResourceManager.GetPlayerResources(1).Wood;

            simulation.Tick(new List<ICommand>
            {
                new UpgradeTowerCommand(0, tower.Id, TowerUpgradeType.ArrowSlits)
            });

            Assert.That(tower.UpgradeQueue, Is.Empty);
            Assert.That(simulation.ResourceManager.GetPlayerResources(1).Wood, Is.EqualTo(before));
        }

        [Test]
        public void ForeignBuilder_CannotPlaceBuildingOrSpendIssuerResources()
        {
            UnitData foreign = CreateVillager(1);
            GiveResources(0, food: 0, wood: 1000);
            int buildingCount = simulation.BuildingRegistry.Count;
            int woodBefore = simulation.ResourceManager.GetPlayerResources(0).Wood;

            simulation.Tick(new List<ICommand>
            {
                new PlaceBuildingCommand(0, BuildingType.Barracks,
                    baseTile.x + 14, baseTile.y, new[] { foreign.Id })
            });

            Assert.That(simulation.BuildingRegistry.Count, Is.EqualTo(buildingCount));
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Wood, Is.EqualTo(woodBefore));
        }

        [Test]
        public void CommanderCommands_RoundTripThroughActiveJsonSerializer()
        {
            ICommand[] source =
            {
                new GatherCommand(4, new[] { 3, 8 }, 12) { IsQueued = true },
                new PlaceBuildingCommand(4, BuildingType.Barracks, 20, 21, new[] { 3 }) { IsQueued = true },
                new TrainUnitCommand(4, 9, 1),
                new RepairBuildingCommand(4, new[] { 3 }, 22) { IsQueued = true },
                new TributeCommand(4, 2, (int)ResourceType.Gold, 500),
                new AiIntentCommand(1, 4, AiIntentKind.TrainUnits, 1, 3, 0, 0, 600)
            };

            for (int i = 0; i < source.Length; i++)
            {
                (string type, string payload) = CommandSerializer.ToJson(source[i]);
                ICommand restored = CommandSerializer.FromJson(type, payload, 4);
                Assert.That(restored, Is.Not.Null, type);
                Assert.That(restored.Type, Is.EqualTo(source[i].Type), type);
                if (!(restored is AiIntentCommand))
                    Assert.That(restored.PlayerId, Is.EqualTo(4), type);

                if (restored is RepairBuildingCommand repair)
                {
                    Assert.That(repair.UnitIds, Is.EqualTo(new[] { 3 }));
                    Assert.That(repair.TargetBuildingId, Is.EqualTo(22));
                    Assert.That(repair.IsQueued, Is.True);
                }
                else if (restored is TributeCommand tribute)
                {
                    Assert.That(tribute.RecipientPlayerId, Is.EqualTo(2));
                    Assert.That(tribute.ResourceType, Is.EqualTo((int)ResourceType.Gold));
                    Assert.That(tribute.Amount, Is.EqualTo(500));
                }
                else if (restored is AiIntentCommand intent)
                {
                    Assert.That(intent.PlayerId, Is.EqualTo(1));
                    Assert.That(intent.IssuerPlayerId, Is.EqualTo(4));
                    Assert.That(intent.IntentKind, Is.EqualTo((int)AiIntentKind.TrainUnits));
                }
            }
        }

        private UnitData CreateVillager(int playerId)
        {
            UnitData unit = CreateUnit(playerId, 0, true, playerId + 1);
            unit.CarryCapacity = config.VillagerCarryCapacity;
            return unit;
        }

        private UnitData CreateUnit(int playerId, int unitType, bool villager, int offset)
        {
            FixedVector3 position = simulation.MapData.TileToWorldFixed(baseTile.x + 6 + offset, baseTile.y + 2);
            UnitData unit = simulation.UnitRegistry.CreateUnit(playerId, position,
                Fixed32.One, Fixed32.FromFloat(0.4f), Fixed32.One);
            unit.UnitType = unitType;
            unit.IsVillager = villager;
            unit.MaxHealth = 100;
            unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private BuildingData CreateBarracks(int playerId)
        {
            return simulation.CreateBuilding(playerId, BuildingType.Barracks,
                baseTile.x + 8, baseTile.y, underConstruction: false);
        }

        private void GiveResources(int playerId, int food, int wood)
        {
            simulation.ResourceManager.AddResource(playerId, ResourceType.Food, food);
            simulation.ResourceManager.AddResource(playerId, ResourceType.Wood, wood);
        }

        private ResourceNodeData AddKnownResource(ResourceType type, int offset)
        {
            FixedVector3 position = simulation.MapData.TileToWorldFixed(baseTile.x + offset, baseTile.y + offset);
            ResourceNodeData node = simulation.MapData.AddResourceNode(type, position, 10000);
            simulation.FogOfWar.SetVisible(0, node.TileX, node.TileZ);
            return node;
        }

        private void MakeAreaVisible(int playerId)
        {
            for (int x = 0; x < simulation.MapData.Width; x++)
                for (int z = 0; z < simulation.MapData.Height; z++)
                    simulation.FogOfWar.SetVisible(playerId, x, z);
        }
    }
}
