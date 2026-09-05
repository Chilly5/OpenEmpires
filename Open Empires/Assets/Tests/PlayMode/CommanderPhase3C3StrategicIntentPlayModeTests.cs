using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C3StrategicIntentPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private StrategicPlanner planner;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            PreparePlayableArea();
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            goalManager = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goalManager, CurrentResourceAmount);
        }

        [TearDown]
        public void TearDown()
        {
            planner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_AttackPreparation_SelectsCavalryAndStartsEconomy()
        {
            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            Assert.That(submission.CreatedPlan, Is.True);
            Assert.That(submission.Intent.Status, Is.EqualTo(StrategicIntentStatus.Active));
            Assert.That(submission.Plan, Is.TypeOf<CavalryPressurePlan>());
            Assert.That(submission.Plan.CurrentMilestone.Name,
                Is.EqualTo("Economic Foundation"));
            Assert.That(submission.Plan.ChildGoalIds, Has.Count.EqualTo(2));
            Debug.Log("[Phase3C-3 Runtime] PASS Scenario 1: AttackPreparation intent selected CavalryPressurePlan and began the Economic Foundation milestone through Commander goals.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_UnknownObjective_IsRejectedWithoutPlan()
        {
            var intent = new StrategicIntent(50, 0, (StrategicObjectiveType)999,
                simulation.CurrentTick);

            StrategicIntentSubmission submission = planner.SubmitIntent(intent);

            Assert.That(submission.Status,
                Is.EqualTo(StrategicIntentSubmissionStatus.Rejected));
            Assert.That(submission.Error,
                Is.EqualTo(StrategicIntentValidationError.UnknownObjective));
            Assert.That(intent.Status, Is.EqualTo(StrategicIntentStatus.Rejected));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
            Debug.Log("[Phase3C-3 Runtime] PASS Scenario 2: unknown strategic objective was rejected; no plan or Commander goal was created.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_CavalryIntent_CompletesExistingMilestoneFlow()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Worker(x + 8, z);
            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);
            goalManager.Tick(0);
            simulation.CommandBuffer.FlushCommands();
            simulation.CreateBuilding(0, BuildingType.Stables, x + 7, z + 7, false);
            goalManager.Tick(15);
            simulation.CommandBuffer.FlushCommands();
            for (int i = 0; i < CavalryPressurePlan.KnightTarget; i++)
            {
                UnitData knight = Worker(x - 10 + i, z - 5);
                knight.IsVillager = false;
                knight.UnitType = CommanderIntentCatalog.KnightUnitType;
            }
            goalManager.Tick(30);

            Assert.That(submission.Plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(submission.Intent.Status, Is.EqualTo(StrategicIntentStatus.Completed));
            Assert.That(submission.Plan.Milestones.All(item =>
                item.Status == StrategicMilestoneStatus.Completed), Is.True);
            Debug.Log("[Phase3C-3 Runtime] PASS Scenario 3: intent-created CavalryPressurePlan completed the unchanged economy, Stable, Knights, and Ready milestone flow.");
            yield return null;
        }

        private void PreparePlayableArea()
        {
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            for (int tileX = x - 35; tileX <= x + 35; tileX++)
                for (int tileZ = z - 20; tileZ <= z + 20; tileZ++)
                {
                    simulation.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    simulation.MapData.ForestDensity[tileX, tileZ] = 0;
                    simulation.MapData.FoundationCount[tileX, tileZ] = 0;
                    simulation.FogOfWar.SetVisible(0, tileX, tileZ);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 20, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 23, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 26, z, false);
        }

        private void CreateGatherers(ResourceType resourceType, int count, int startX)
        {
            ResourceNodeData node = simulation.MapData.AddResourceNode(resourceType,
                simulation.MapData.TileToWorldFixed(startX + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = Worker(startX + i, z);
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private UnitData Worker(int tileX, int tileZ)
        {
            UnitData unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            unit.UnitType = 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private int CurrentResourceAmount(ResourceType resourceType)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
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
