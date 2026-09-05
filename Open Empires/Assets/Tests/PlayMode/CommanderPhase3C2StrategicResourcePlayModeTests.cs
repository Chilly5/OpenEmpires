using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C2StrategicResourcePlayModeTests
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
            foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim))[0] = 3;
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
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true)
                .AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.House, x + 20, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 23, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 26, z, false);
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
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
        public IEnumerator Runtime_PlanReservation_CreatesClaims()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.EqualTo(800));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Gold), Is.EqualTo(500));
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId), Has.Count.EqualTo(2));
            Debug.Log("[Phase3C-2 Runtime] PASS Scenario 1: CavalryPressurePlan created active 800 Food and 500 Gold strategic reservations.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_PlanCompletion_ReleasesClaims()
        {
            CavalryPressurePlan plan = CompletePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Released), Is.True);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.Zero);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Gold), Is.Zero);
            Debug.Log("[Phase3C-2 Runtime] PASS Scenario 2: completed CavalryPressurePlan released every strategic reservation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_StableFailure_ReleasesClaims()
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
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Released), Is.True);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.Zero);
            Debug.Log("[Phase3C-2 Runtime] PASS Scenario 3: forced Stable timeout failed the plan and released every strategic reservation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_ReservationConflict_IsDeterministic()
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 1000;
            resources.Gold = 600;
            StrategicReservationConflict observed = null;
            strategicPlanner.ReservationConflictDetected += conflict => observed = conflict;

            CavalryPressurePlan owner = strategicPlanner.StartCavalryPressurePlan();
            CavalryPressurePlan requester = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(owner.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(requester.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(observed.ResourceType, Is.EqualTo(ResourceType.Food));
            Assert.That(observed.OwnerPlanId, Is.EqualTo(owner.StrategicPlanId));
            Assert.That(observed.OwnerReservationId, Is.EqualTo(1));
            Assert.That(requester.ChildGoalIds, Is.Empty);
            Assert.That(observed.ToString(), Is.EqualTo(
                "Reservation conflict for Food: plan #2 requested 800; current 1000, "
                + "reserved 800, available 200; owner: plan #1 (CavalryPressure), reservation #1."));
            Debug.Log("[Phase3C-2 Runtime] PASS Scenario 4: second plan received a deterministic Food conflict owned by plan #1; no tactical goals were created.");
            yield return null;
        }

        private CavalryPressurePlan CompletePlan()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Worker(x + 8, z);
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            goalManager.Tick(0);
            sim.CommandBuffer.FlushCommands();
            sim.CreateBuilding(0, BuildingType.Stables, x + 7, z + 7, false);
            goalManager.Tick(15);
            sim.CommandBuffer.FlushCommands();
            for (int i = 0; i < CavalryPressurePlan.KnightTarget; i++)
            {
                UnitData knight = Worker(x - 10 + i, z - 5);
                knight.IsVillager = false;
                knight.UnitType = CommanderIntentCatalog.KnightUnitType;
            }
            goalManager.Tick(30);
            return plan;
        }

        private void CreateGatherers(ResourceType resourceType, int count, int startX)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(resourceType,
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
            UnitData unit = sim.UnitRegistry.CreateUnit(0,
                sim.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            unit.UnitType = 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
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
