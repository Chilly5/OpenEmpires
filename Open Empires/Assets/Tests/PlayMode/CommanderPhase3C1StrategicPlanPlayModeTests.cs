using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C1StrategicPlanPlayModeTests
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
            resources.Food = resources.Wood = resources.Gold = 5000;
            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
        }

        [TearDown]
        public void TearDown()
        {
            strategicPlanner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_CavalryPressurePlan_AdvancesThroughAllScenarios()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Worker(x + 8, z);

            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Economic Foundation"));
            Assert.That(plan.ChildGoalIds, Has.Count.EqualTo(2));
            Debug.Log("[Phase3C-1 Runtime] PASS Scenario 1: CavalryPressurePlan started; Economic Foundation active with food and gold goals.");
            yield return null;

            goalManager.Tick(0);
            BuildStructureGoal stableGoal = plan.ChildGoalIds.Select(goalManager.GetGoal)
                .OfType<BuildStructureGoal>().Single();
            Assert.That(stableGoal.StructureType, Is.EqualTo(BuildingType.Stables));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Infrastructure"));
            Assert.That(sim.CommandBuffer.FlushCommands().Single(), Is.TypeOf<PlaceBuildingCommand>());
            Debug.Log("[Phase3C-1 Runtime] PASS Scenario 2: economic goals completed; Infrastructure activated and Stable construction command began.");
            yield return null;

            sim.CreateBuilding(0, BuildingType.Stables, x + 7, z + 7, false);
            goalManager.Tick(15);
            EnsureUnitCountGoal knightGoal = plan.ChildGoalIds.Select(goalManager.GetGoal)
                .OfType<EnsureUnitCountGoal>().Single();
            Assert.That(knightGoal.TargetTotal, Is.EqualTo(CavalryPressurePlan.KnightTarget));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Army Preparation"));
            Assert.That(sim.CommandBuffer.FlushCommands().Single(), Is.TypeOf<TrainUnitCommand>());
            Debug.Log("[Phase3C-1 Runtime] PASS Scenario 3: Stable completed; Army Preparation activated and Knight production command began.");
            yield return null;

            CreateKnights(CavalryPressurePlan.KnightTarget);
            goalManager.Tick(30);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(plan.OutcomeMessage, Is.EqualTo("Cavalry preparation complete."));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).All(goal => goal.IsTerminal), Is.True);
            Debug.Log("[Phase3C-1 Runtime] PASS Scenario 4: six Knights ready; Ready milestone and StrategicPlan completed with no orphan tactical goals.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_CavalryPressurePlan_ChildFailureStopsPlan()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Worker(x + 8, z);
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            goalManager.Tick(0);
            sim.CommandBuffer.FlushCommands();
            BuildStructureGoal stable = plan.ChildGoalIds.Select(goalManager.GetGoal)
                .OfType<BuildStructureGoal>().Single();

            goalManager.Tick(36000);

            Assert.That(stable.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Failed));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).All(goal => goal.IsTerminal), Is.True);
            Debug.Log("[Phase3C-1 Runtime] PASS failure: timed-out Stable child failed Infrastructure and the strategic plan; no later goals were created.");
            yield return null;
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
