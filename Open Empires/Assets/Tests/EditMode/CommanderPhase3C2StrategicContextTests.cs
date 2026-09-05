using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C2StrategicContextTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goalManager;
        private StrategicPlanner strategicPlanner;
        private int x;
        private int z;
        private bool playableAreaPrepared;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
            playableAreaPrepared = false;
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 5000;
            resources.Wood = 5000;
            resources.Gold = 5000;
            resources.Stone = 5000;
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
        public void StrategicContext_ContainsEconomyState()
        {
            PlayerResources stockpile = sim.ResourceManager.GetPlayerResources(0);
            stockpile.Food = 1200;
            stockpile.Wood = 300;
            stockpile.Gold = 1000;
            stockpile.Stone = 75;
            BuildingData activeBarracks = sim.CreateBuilding(0, BuildingType.Barracks,
                x - 4, z, false);
            activeBarracks.TrainingQueue.Add(1);
            sim.CreateBuilding(0, BuildingType.Barracks, x - 8, z, false);
            sim.CreateBuilding(0, BuildingType.Barracks, x - 12, z, true);
            Unit(0, 1, x - 6, z + 2);
            strategicPlanner.StartCavalryPressurePlan();

            StrategicContext context = Context();
            StrategicResourceState food = Resource(context, ResourceType.Food);
            StrategicResourceState wood = Resource(context, ResourceType.Wood);
            StrategicResourceState gold = Resource(context, ResourceType.Gold);

            Assert.That(new[] { food.CurrentAmount, food.ReservedAmount, food.AvailableAmount },
                Is.EqualTo(new[] { 1200, CavalryPressurePlan.FoodRequirement, 400 }));
            Assert.That(new[] { gold.CurrentAmount, gold.ReservedAmount, gold.AvailableAmount },
                Is.EqualTo(new[] { 1000, CavalryPressurePlan.GoldRequirement, 500 }));
            Assert.That(new[] { wood.CurrentAmount, wood.ReservedAmount, wood.AvailableAmount },
                Is.EqualTo(new[] { 300, 0, 300 }));
            Assert.That(context.Economy, Has.Count.EqualTo(4));
            Assert.That(context.Population.CurrentPopulation, Is.EqualTo(sim.GetPopulation(0)));
            Assert.That(context.Population.PopulationCap, Is.EqualTo(sim.GetPopulationCap(0)));
            Assert.That(context.Population.MaximumPopulation, Is.EqualTo(config.MaxPopulation));
            Assert.That(context.Population.AvailableCapacity,
                Is.EqualTo(Math.Max(0, sim.GetPopulationCap(0) - sim.GetPopulation(0))));
            StrategicMilitaryState spearmen = context.Military.Single(item => item.UnitType == 1);
            Assert.That(new[] { spearmen.OwnedCount, spearmen.QueuedCount },
                Is.EqualTo(new[] { 1, 1 }));
            StrategicProductionState barracks = context.Production
                .Single(item => item.BuildingType == BuildingType.Barracks.ToString());
            Assert.That(new[] { barracks.ProductionBuildingCount, barracks.UnderConstructionCount,
                barracks.ActiveQueueCount, barracks.QueuedUnitCount, barracks.AvailableCapacity },
                Is.EqualTo(new[] { 3, 1, 1, 1, 1 }));
        }

        [Test]
        public void StrategicContext_DoesNotLeakHiddenInformation()
        {
            int hiddenX = x;
            int hiddenZ = z;
            UnitData enemy = Unit(1, CommanderIntentCatalog.KnightUnitType, hiddenX, hiddenZ);
            sim.CreateBuilding(1, BuildingType.Stables, hiddenX + 2, hiddenZ, false);
            ResourceNodeData hidden = sim.MapData.AddResourceNode(ResourceType.Gold,
                sim.MapData.TileToWorldFixed(hiddenX, hiddenZ + 4), 777);
            Assert.That(sim.FogOfWar.GetVisibility(0, hidden.TileX, hidden.TileZ),
                Is.EqualTo(TileVisibility.Unexplored));

            string before = Context().ToJson();
            enemy.UnitType = 99;
            hidden.RemainingAmount = 333;
            sim.CreateBuilding(1, BuildingType.Barracks, hiddenX + 8, hiddenZ, false);
            string after = Context().ToJson();

            Assert.That(after, Is.EqualTo(before),
                "Hidden enemy and resource changes must not affect strategic context.");
            StringAssert.DoesNotContain("\"UnitType\":99", after);
            Assert.That(Context().Military, Is.Empty);
            Assert.That(Context().Production, Is.Empty);
            Assert.That(Context().VisibleResources, Is.Empty);
            Assert.That(Context().Threat.VisibleEnemyMilitary, Is.Empty);
            Assert.That(Context().Threat.VisibleEnemyMilitaryUnits, Is.Zero);

            sim.FogOfWar.SetVisible(0, hidden.TileX, hidden.TileZ);
            Assert.That(Context().VisibleResources.Single().RemainingAmount, Is.EqualTo(333));
        }

        [Test]
        public void StrategicContext_TracksActivePlans()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            StrategicPlanState snapshot = Context().ActivePlans.Single();

            Assert.That(snapshot.StrategicPlanId, Is.EqualTo(plan.StrategicPlanId));
            Assert.That(snapshot.Status, Is.EqualTo(StrategicPlanStatus.Active.ToString()));
            Assert.That(snapshot.CurrentMilestone, Is.EqualTo("Economic Foundation"));
            Assert.That(snapshot.RequiredResources, Has.Count.EqualTo(2));
            Assert.That(snapshot.Reservations, Has.Count.EqualTo(2));

            strategicPlanner.CancelPlan(plan.StrategicPlanId);
            Assert.That(Context().ActivePlans, Is.Empty);
        }

        [Test]
        public void ResourceReservation_CreatesCorrectly()
        {
            var events = new List<int>();
            strategicPlanner.ReservationCreated += reservation => events.Add(reservation.ReservationId);
            PlayerResources stockpile = sim.ResourceManager.GetPlayerResources(0);
            int startingFood = stockpile.Food;
            int startingGold = stockpile.Gold;

            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            StrategicResourceReservation[] reservations = strategicPlanner
                .GetReservationsForPlan(plan.StrategicPlanId).ToArray();

            Assert.That(reservations.Select(item => item.ReservationId), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(events, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(reservations.Select(item => item.ResourceType),
                Is.EqualTo(new[] { ResourceType.Food, ResourceType.Gold }));
            Assert.That(reservations.Select(item => item.Amount),
                Is.EqualTo(new[] { CavalryPressurePlan.FoodRequirement,
                    CavalryPressurePlan.GoldRequirement }));
            Assert.That(reservations.All(item => item.PlanId == plan.StrategicPlanId
                && item.Status == StrategicResourceReservationStatus.Active), Is.True);
            Assert.That(plan.ResourceReservationIds, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(stockpile.Food, Is.EqualTo(startingFood), "Reservations do not consume stockpile.");
            Assert.That(stockpile.Gold, Is.EqualTo(startingGold));
        }

        [Test]
        public void ResourceReservation_ReleasesOnPlanCompletion()
        {
            var released = new List<int>();
            strategicPlanner.ReservationReleased += reservation => released.Add(reservation.ReservationId);
            CavalryPressurePlan plan = CompletePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Released), Is.True);
            Assert.That(released, Is.EqualTo(plan.ResourceReservationIds));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.Zero);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Gold), Is.Zero);
        }

        [Test]
        public void ResourceReservation_ReleasesOnPlanFailure()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            goalManager.Tick(36000);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Released), Is.True);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.Zero);
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).All(goal => goal.IsTerminal), Is.True);
        }

        [Test]
        public void ResourceReservation_ReleasesOnCancellation()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(strategicPlanner.CancelPlan(plan.StrategicPlanId), Is.True);

            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.Zero);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Gold), Is.Zero);
        }

        [Test]
        public void ResourceAvailability_AccountsForReservations()
        {
            sim.ResourceManager.GetPlayerResources(0).Gold = 1000;
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            StrategicResourceAvailability result = strategicPlanner
                .CheckResourceAvailability(ResourceType.Gold, 600);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(result.CurrentAmount, Is.EqualTo(1000));
            Assert.That(result.ReservedAmount, Is.EqualTo(500));
            Assert.That(result.AvailableAmount, Is.EqualTo(500));
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(strategicPlanner.CanAllocate(ResourceType.Gold, 500), Is.True);
        }

        [Test]
        public void ResourceAvailability_PreventsOverCommitment()
        {
            sim.ResourceManager.GetPlayerResources(0).Food =
                CavalryPressurePlan.FoodRequirement - 1;
            StrategicReservationConflict observed = null;
            strategicPlanner.ReservationConflictDetected += conflict => observed = conflict;

            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(plan.ChildGoalIds, Is.Empty);
            Assert.That(plan.ResourceReservationIds, Is.Empty,
                "Atomic validation must not create partial reservations.");
            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Assert.That(observed.ResourceType, Is.EqualTo(ResourceType.Food));
            Assert.That(observed.OwnerPlanId, Is.Null);
        }

        [Test]
        public void ResourceReservation_DetectsConflict()
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
            Assert.That(observed.OwnerPlanType, Is.EqualTo(StrategicPlanType.CavalryPressure));
            Assert.That(observed.RequestingPlanId, Is.EqualTo(requester.StrategicPlanId));
            Assert.That(requester.ChildGoalIds, Is.Empty);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food),
                Is.EqualTo(CavalryPressurePlan.FoodRequirement));
        }

        [Test, Repeat(3)]
        public void ResourceReservation_DeterministicConflictResult()
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 1000;
            resources.Gold = 600;
            StrategicReservationConflict observed = null;
            strategicPlanner.ReservationConflictDetected += conflict => observed = conflict;

            strategicPlanner.StartCavalryPressurePlan();
            strategicPlanner.StartCavalryPressurePlan();

            Assert.That(observed.ToString(), Is.EqualTo(
                "Reservation conflict for Food: plan #2 requested 800; current 1000, "
                + "reserved 800, available 200; owner: plan #1 (CavalryPressure), reservation #1."));
        }

        [Test]
        public void CavalryPlan_CreatesResourceRequirements()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            Assert.That(plan.RequiredResources.Select(item => item.ResourceType),
                Is.EqualTo(new[] { ResourceType.Food, ResourceType.Gold }));
            Assert.That(plan.RequiredResources.Select(item => item.Amount),
                Is.EqualTo(new[] { 800, 500 }));
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Economic Foundation"));
            Assert.That(plan.Milestones.Select(item => item.Name), Is.EqualTo(new[]
            {
                "Economic Foundation", "Infrastructure", "Army Preparation", "Ready"
            }));
        }

        [Test]
        public void CavalryPlan_ReleasesResourcesWhenComplete()
        {
            PlayerResources stockpile = sim.ResourceManager.GetPlayerResources(0);
            int initialFood = stockpile.Food;
            int initialGold = stockpile.Gold;

            CavalryPressurePlan plan = CompletePlan();

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(strategicPlanner.CheckResourceAvailability(ResourceType.Food, initialFood)
                .IsAvailable, Is.True);
            Assert.That(strategicPlanner.CheckResourceAvailability(ResourceType.Gold, initialGold)
                .IsAvailable, Is.True);
            Assert.That(stockpile.Food, Is.EqualTo(initialFood),
                "Strategic accounting must not consume simulation resources.");
            Assert.That(stockpile.Gold, Is.EqualTo(initialGold));
        }

        private StrategicContext Context()
        {
            CommanderContext tactical = new CommanderContextBuilder().Build(sim, goalManager);
            return strategicPlanner.BuildContext(tactical);
        }

        private static StrategicResourceState Resource(StrategicContext context,
            ResourceType resourceType)
        {
            return context.Economy.Single(item => item.ResourceType == resourceType);
        }

        private CavalryPressurePlan CompletePlan()
        {
            PreparePlayableArea();
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            Unit(0, 0, x + 8, z).IsVillager = true;
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            goalManager.Tick(0);
            sim.CommandBuffer.FlushCommands();
            sim.CreateBuilding(0, BuildingType.Stables, x + 7, z + 7, false);
            goalManager.Tick(15);
            sim.CommandBuffer.FlushCommands();
            for (int i = 0; i < CavalryPressurePlan.KnightTarget; i++)
                Unit(0, CommanderIntentCatalog.KnightUnitType, x - 10 + i, z - 5);
            goalManager.Tick(30);
            return plan;
        }

        private void PreparePlayableArea()
        {
            if (playableAreaPrepared) return;
            playableAreaPrepared = true;
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
            var ages = (int[])typeof(GameSimulation)
                .GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim);
            ages[0] = 3;
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true)
                .AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.House, x + 20, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 23, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 26, z, false);
        }

        private void CreateGatherers(ResourceType resourceType, int count, int startX)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(resourceType,
                sim.MapData.TileToWorldFixed(startX + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = Unit(0, 0, startX + i, z);
                worker.IsVillager = true;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private UnitData Unit(int playerId, int unitType, int tileX, int tileZ)
        {
            UnitData unit = sim.UnitRegistry.CreateUnit(playerId,
                sim.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.UnitType = unitType;
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
