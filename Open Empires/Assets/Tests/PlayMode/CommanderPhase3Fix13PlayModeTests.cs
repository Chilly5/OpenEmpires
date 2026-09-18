using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3Fix13PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goalManager;
        private StrategicPlanner strategicPlanner;
        private int x;
        private int z;

        [SetUp]
        public void SetUp() => SetUpSimulation(true);

        private void SetUpSimulation(bool includeHouses)
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
            if (includeHouses)
            {
            sim.CreateBuilding(0, BuildingType.House, x + 20, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 23, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 26, z, false);
            }
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            var enemy = sim.MapData.BasePositions[1];
            sim.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true).AutoProduceVillagers = false;
            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
        }

        [TearDown]
        public void TearDown()
        {
            strategicPlanner.Dispose();
            goalManager.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_PlayerIntentUsesPipeline()
        {
            using var pipeline = new StrategicPipeline(sim, goalManager, strategicPlanner);
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var result = dispatcher.SubmitText("prepare cavalry attack");
            Assert.That(result.CreatedPlan, Is.True, result.Response);
            Assert.That(pipeline.DecisionHistory.History.Single().Submission, Is.SameAs(result.StrategicSubmission));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Debug.Log("[Fix13 Runtime] Player dispatcher -> pipeline -> decision history -> strategic plan verified.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_DefensivePreparationExecutes()
        {
            CreateGatherers(ResourceType.Food, 8, x - 22);
            CreateGatherers(ResourceType.Wood, 8, x - 10);
            Worker(x + 8, z);
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            yield return Execute(plan);
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Count(b => b.PlayerId == 0 && !b.IsDestroyed
                && !b.IsUnderConstruction && b.Type == BuildingType.Barracks), Is.EqualTo(1));
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Count(b => b.PlayerId == 0 && !b.IsDestroyed
                && !b.IsUnderConstruction && b.Type == BuildingType.Tower), Is.EqualTo(2));
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(u => u.PlayerId == 0 && u.CurrentHealth > 0
                && u.UnitType == CommanderIntentCatalog.SpearmanUnitType), Is.GreaterThanOrEqualTo(8));
            Debug.Log("[Fix13 Runtime] Defense completed via simulation ticks: Barracks, 8 spearmen, 2 towers.");
        }

        [UnityTest]
        public IEnumerator Runtime_EconomicExpansionExecutes()
        {
            CreateGatherers(ResourceType.Food, EconomicExpansionPlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Wood, EconomicExpansionPlan.WoodWorkerTarget, x - 10);
            CreateGatherers(ResourceType.Stone, 4, x);
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.EconomicExpansion).Plan;
            yield return Execute(plan);
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Count(b => b.PlayerId == 0 && !b.IsDestroyed
                && !b.IsUnderConstruction && b.Type == BuildingType.TownCenter), Is.EqualTo(2));
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(u => u.PlayerId == 0 && u.IsVillager
                && u.CurrentHealth > 0), Is.GreaterThanOrEqualTo(EconomicExpansionPlan.WorkerTarget));
            Debug.Log("[Fix13 Runtime] Expansion completed via simulation ticks: additional Town Center and 20 workers.");
        }

        [UnityTest]
        public IEnumerator Runtime_CavalryWaitsResumesAndReleases()
        {
            CreateGatherers(ResourceType.Food, CavalryPressurePlan.FoodWorkerTarget, x - 22);
            CreateGatherers(ResourceType.Gold, CavalryPressurePlan.GoldWorkerTarget, x);
            CreateGatherers(ResourceType.Wood, CavalryPressurePlan.WoodWorkerTarget, x - 10);
            Worker(x + 8, z);
            sim.ResourceManager.GetPlayerResources(0).Wood = 0;
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            goalManager.Tick(0);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            Assert.That(plan.CurrentMilestone.RequiredChildGoals, Is.Empty);
            yield return Execute(plan);
            Assert.That(strategicPlanner.ArchivedReservations.Any(r => r.ResourceType == ResourceType.Wood), Is.True);
            Assert.That(strategicPlanner.ArchivedReservations.All(r => r.Status == StrategicResourceReservationStatus.Released), Is.True);
            Debug.Log("[Fix13 Runtime] Cavalry recovered from zero wood through gathering, built Stables, trained knights, released reservations.");
        }

        private void PrepareFreshDefense()
        {
            TearDown();
            SetUpSimulation(false);
            ((int[])typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim))[0] = 1;
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = config.StartingFood;
            resources.Wood = config.StartingWood;
            resources.Gold = config.StartingGold;
            resources.Stone = config.StartingStone;
            sim.MapData.AddResourceNode(ResourceType.Food, sim.MapData.TileToWorldFixed(x + 8, z - 6), 10000);
            sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x + 8, z + 6), 10000);
            for (int i = 0; i < config.StartingVillagers; i++) Worker(x + 8 + i, z);
        }

        [UnityTest]
        public IEnumerator Runtime_FreshAgeOneDefenseCompletes()
        {
            PrepareFreshDefense();
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var submission = dispatcher.SubmitText("prepare defense");
            Assert.That(submission.CreatedPlan, Is.True, submission.Response);
            yield return Execute(submission.StrategicSubmission.Plan);
            Assert.That(sim.UnitRegistry.GetAllUnits().Count(u => u.PlayerId == 0 && u.CurrentHealth > 0
                && u.UnitType == CommanderIntentCatalog.SpearmanUnitType), Is.GreaterThanOrEqualTo(8));
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Any(b => b.PlayerId == 0 && b.Type == BuildingType.Tower), Is.False);
            Debug.Log("[Fix13 Runtime] Fresh age-one defense completed with 3 villagers and canonical starting resources; no tower age deadlock.");
        }

        [UnityTest]
        public IEnumerator Runtime_CommanderPreservesPairedSimulationDeterminism()
        {
            var mirror = new CommanderPhase3Fix13PlayModeTests();
            mirror.SetUp();
            try
            {
                PrepareFreshDefense();
                mirror.PrepareFreshDefense();
                Assert.That(mirror.sim.ComputeStateChecksum(), Is.EqualTo(sim.ComputeStateChecksum()));
                using var first = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
                using var second = new CommanderIntentDispatcher(mirror.sim, mirror.goalManager, strategicPlanner: mirror.strategicPlanner);
                first.SubmitText("prepare defense");
                second.SubmitText("prepare defense");
                for (int i = 0; i < 1500; i++)
                {
                    strategicPlanner.Tick(sim.CurrentTick);
                    mirror.strategicPlanner.Tick(mirror.sim.CurrentTick);
                    goalManager.Tick(sim.CurrentTick);
                    mirror.goalManager.Tick(mirror.sim.CurrentTick);
                    sim.Tick();
                    mirror.sim.Tick();
                    Assert.That(mirror.sim.ComputeStateChecksum(), Is.EqualTo(sim.ComputeStateChecksum()), "tick " + i);
                    if (i % 100 == 0) yield return null;
                }
                Debug.Log("[Fix13 Runtime] Paired independently commanded simulations matched checksums at every one of 1500 ticks.");
            }
            finally { mirror.TearDown(); }
        }

        private IEnumerator Execute(StrategicPlan plan)
        {
            for (int i = 0; i < 30000 && !plan.IsTerminal; i++)
            {
                strategicPlanner.Tick(sim.CurrentTick);
                goalManager.Tick(sim.CurrentTick);
                sim.Tick();
                if (i % 300 == 0) yield return null;
            }
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed),
                plan.CurrentMilestone.Name + ": " + plan.OutcomeMessage + " | "
                + string.Join("; ", goalManager.ActiveGoals.Select(g => g.StatusReason)));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
        }

        private void CreateGatherers(ResourceType resourceType, int count, int startX)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(resourceType,
                sim.MapData.TileToWorldFixed(startX + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = Worker(startX + 3 + i % 2, z + 7 + i / 2);
                worker.FinalDestination = worker.SimPosition;
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
