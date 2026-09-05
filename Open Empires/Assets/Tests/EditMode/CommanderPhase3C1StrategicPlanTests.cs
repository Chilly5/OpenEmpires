using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C1StrategicPlanTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goalManager;
        private StrategicPlanner strategicPlanner;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            SetAge(3);
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(sim.MapData, null);
            for (int tileX = x - 35; tileX <= x + 35; tileX++)
                for (int tileZ = z - 20; tileZ <= z + 20; tileZ++)
                {
                    sim.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    sim.MapData.ForestDensity[tileX, tileZ] = 0;
                    sim.MapData.FoundationCount[tileX, tileZ] = 0;
                    sim.FogOfWar.SetVisible(0, tileX, tileZ);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true).AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.House, x + 20, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 23, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 26, z, false);
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 5000;
            resources.Wood = 5000;
            resources.Gold = 5000;
            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
        }

        [TearDown]
        public void TearDown()
        {
            strategicPlanner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void StrategicPlan_StartsCorrectly()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(plan.StrategicPlanId, Is.EqualTo(1));
            Assert.That(plan.OwnerPlayerId, Is.EqualTo(0));
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.CavalryPressure));
            Assert.That(plan.CreatedTick, Is.EqualTo(sim.CurrentTick));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Economic Foundation"));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(plan.Milestones.Select(m => m.OrderIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void StrategicPlan_CompletesCorrectly()
        {
            CavalryPressurePlan plan = CompletePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(plan.OutcomeMessage, Is.EqualTo(CavalryPressurePlan.CompletionResponse));
            Assert.That(plan.Milestones.All(m => m.Status == StrategicMilestoneStatus.Completed), Is.True);
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).All(goal => goal.IsTerminal), Is.True);
        }

        [Test]
        public void StrategicPlan_FailsCorrectly()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();
            BuildStructureGoal stable = plan.ChildGoalIds.Select(goalManager.GetGoal)
                .OfType<BuildStructureGoal>().Single();

            goalManager.Tick(36000);

            Assert.That(stable.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            StringAssert.Contains("Tactical goal", plan.OutcomeMessage);
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).All(goal => goal.IsTerminal), Is.True);
        }

        [Test]
        public void StrategicPlan_CancelsChildGoals()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            UnitData worker = Worker(x - 12, z);
            Assert.That(goalManager.TryReserveWorker(plan.ChildGoalIds[0], worker.Id,
                CommanderWorkerReservationType.Gatherer), Is.True);

            Assert.That(strategicPlanner.CancelPlan(plan.StrategicPlanId), Is.True);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal)
                .All(goal => goal.Status == CommanderGoalStatus.Cancelled), Is.True);
            Assert.That(goalManager.GetWorkerReservation(worker.Id), Is.Null,
                "Strategic cancellation must use GoalManager cancellation to release reservations.");
        }

        [Test]
        public void Milestone_AdvancesAfterChildGoalsComplete()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();

            Assert.That(plan.Milestones[0].Status, Is.EqualTo(StrategicMilestoneStatus.Completed));
            Assert.That(plan.Milestones[0].CompletedChildGoals,
                Is.EquivalentTo(plan.Milestones[0].RequiredChildGoals));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Infrastructure"));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
        }

        [Test]
        public void Milestone_DoesNotAdvanceEarly()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 20);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget - 1, x);
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            goalManager.Tick(0);

            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Economic Foundation"));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(plan.CurrentMilestone.CompletedChildGoals, Has.Count.EqualTo(1));
            Assert.That(plan.ChildGoalIds, Has.Count.EqualTo(2));
        }

        [Test]
        public void Milestone_FailureStopsPlan()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            goalManager.Tick(36000);
            goalManager.Tick(36150);

            Assert.That(plan.Milestones[0].Status, Is.EqualTo(StrategicMilestoneStatus.Failed));
            Assert.That(plan.Milestones.Skip(1).All(m => m.Status == StrategicMilestoneStatus.Skipped), Is.True);
            Assert.That(plan.ChildGoalIds, Has.Count.EqualTo(2), "Failure must not create later milestone goals.");
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
        }

        [Test]
        public void CavalryPlan_CreatesEconomicGoals()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            CommanderGoal[] goals = plan.ChildGoalIds.Select(goalManager.GetGoal).ToArray();

            Assert.That(goals, Has.Length.EqualTo(2));
            Assert.That(goals.OfType<ResourceAllocationGoal>().Any(g => g.Resource == ResourceType.Food
                && g.TargetWorkers == CavalryPressurePlan.FoodWorkerTarget), Is.True);
            Assert.That(goals.OfType<ResourceAllocationGoal>().Any(g => g.Resource == ResourceType.Gold
                && g.TargetWorkers == CavalryPressurePlan.GoldWorkerTarget), Is.True);
            Assert.That(plan.CurrentMilestone.RequiredChildGoals, Is.EquivalentTo(plan.ChildGoalIds));
        }

        [Test]
        public void CavalryPlan_BuildsStableAfterEconomy()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();
            BuildStructureGoal stable = plan.ChildGoalIds.Select(goalManager.GetGoal)
                .OfType<BuildStructureGoal>().Single();

            Assert.That(stable.StructureType, Is.EqualTo(BuildingType.Stables));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Infrastructure"));
            Assert.That(plan.CurrentMilestone.RequiredChildGoals, Does.Contain(stable.GoalId));
        }

        [Test]
        public void CavalryPlan_TrainsKnightsAfterStable()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();
            EnsureUnitCountGoal knightGoal = CompleteStable(plan);
            ICommand command = sim.CommandBuffer.FlushCommands().Single();

            Assert.That(knightGoal.RequestedUnitType, Is.EqualTo(CommanderIntentCatalog.KnightUnitType));
            Assert.That(knightGoal.TargetTotal, Is.EqualTo(CavalryPressurePlan.KnightTarget));
            Assert.That(command, Is.TypeOf<TrainUnitCommand>());
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Army Preparation"));
        }

        [Test]
        public void CavalryPlan_CompletesAfterArmyReady()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();
            CompleteStable(plan);
            sim.CommandBuffer.FlushCommands();
            CreateKnights(CavalryPressurePlan.KnightTarget);
            string response = null;
            strategicPlanner.ResponseGenerated += (_, message) => response = message;

            goalManager.Tick(30);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Ready"));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Completed));
            Assert.That(response, Is.EqualTo("Cavalry preparation complete."));
        }

        private CavalryPressurePlan CompletePlan()
        {
            CavalryPressurePlan plan = StartWithCompletedEconomy();
            CompleteStable(plan);
            sim.CommandBuffer.FlushCommands();
            CreateKnights(CavalryPressurePlan.KnightTarget);
            goalManager.Tick(30);
            return plan;
        }

        private CavalryPressurePlan StartWithCompletedEconomy()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Worker(x + 8, z);
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            goalManager.Tick(0);
            sim.CommandBuffer.FlushCommands();
            return plan;
        }

        private EnsureUnitCountGoal CompleteStable(CavalryPressurePlan plan)
        {
            sim.CreateBuilding(0, BuildingType.Stables, x + 7, z + 7, false);
            goalManager.Tick(15);
            return plan.ChildGoalIds.Select(goalManager.GetGoal).OfType<EnsureUnitCountGoal>().Single();
        }

        private void CreateGatherers(ResourceType resource, int count, int startX)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(resource,
                sim.MapData.TileToWorldFixed(startX + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = Worker(startX + i, z);
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private UnitData Worker(int tileX, int tileZ)
        {
            UnitData unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(tileX, tileZ),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            unit.UnitType = 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void CreateKnights(int count)
        {
            for (int i = 0; i < count; i++)
            {
                UnitData unit = Worker(x - 10 + i, z - 5);
                unit.IsVillager = false;
                unit.UnitType = CommanderIntentCatalog.KnightUnitType;
            }
        }

        private void SetAge(int age)
        {
            var field = typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance);
            ((int[])field.GetValue(sim))[0] = age;
        }

        private int CurrentResourceAmount(ResourceType resourceType)
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            switch (resourceType)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }
    }
}
