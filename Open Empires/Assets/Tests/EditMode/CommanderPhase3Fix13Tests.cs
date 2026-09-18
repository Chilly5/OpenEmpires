using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("Phase3Fix13")]
    public class CommanderPhase3Fix13Tests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goalManager;
        private StrategicPlanner strategicPlanner;
        private StrategicPipeline pipeline;
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
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 5000;
            resources.Wood = 5000;
            resources.Gold = 5000;
            resources.Stone = 5000;

            for (int tileX = x - 25; tileX <= x + 25; tileX++)
                for (int tileZ = z - 15; tileZ <= z + 15; tileZ++)
                {
                    sim.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    sim.MapData.ForestDensity[tileX, tileZ] = 0;
                    sim.MapData.FoundationCount[tileX, tileZ] = 0;
                    sim.FogOfWar.SetVisible(0, tileX, tileZ);
                }

            sim.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 22, z, false);

            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
            pipeline = new StrategicPipeline(sim, goalManager, strategicPlanner);
        }

        [TearDown]
        public void TearDown()
        {
            pipeline?.Dispose();
            strategicPlanner?.Dispose();
            goalManager?.Dispose();
            if (config != null)
                UnityEngine.Object.DestroyImmediate(config);
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


        [Test]
        public void PlayerStrategicIntent_UsesStrategicPipeline()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var result = dispatcher.SubmitText("prepare cavalry attack");
            Assert.That(result.CreatedPlan, Is.True, result.Response);
            Assert.That(pipeline.LastSubmission, Is.SameAs(result.StrategicSubmission));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void LLMStrategicIntent_UsesStrategicPipeline()
        {
            var context = new CommanderContextBuilder().Build(sim, goalManager);
            var parsed = CommanderIntentDtoCodec.InterpretJson(
                "{\"type\":\"strategic\",\"objective\":\"EconomicExpansion\"}", context);
            Assert.That(parsed.Success, Is.True, parsed.Reason);
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var result = dispatcher.SubmitIntent(parsed.StrategicIntent);
            Assert.That(result.CreatedPlan, Is.True, result.Response);
            Assert.That(pipeline.LastSubmission, Is.SameAs(result.StrategicSubmission));
        }

        [Test]
        public void StrategicIntent_CreatesDecisionHistory()
        {
            int events = 0;
            pipeline.EvaluationCompleted += _ => events++;
            new IntentRouter(0, strategicPlanner: strategicPlanner).Route(
                new StrategicIntent(1, 0, StrategicObjectiveType.DefensivePreparation, 0));
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History[0].TriggerType,
                Is.EqualTo(StrategicEvaluationTriggerType.PlayerRequest));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void TacticalIntent_DoesNotUseStrategicPipeline()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            Assert.That(dispatcher.SubmitIntent(new EnsureUnitCountIntent(0, 1, 8)).CreatedGoal, Is.True);
            Assert.That(pipeline.DecisionHistory.History, Is.Empty);
        }

        [TestCase("ArcheryRange")]
        [TestCase("archery range")]
        [TestCase("archery_range")]
        [TestCase("archery-range")]
        public void Catalog_ResolvesArcheryRangeAliases(string alias)
        {
            Assert.That(CommanderIntentCatalog.TryResolveStructure(alias, out var type), Is.True);
            Assert.That(type, Is.EqualTo(BuildingType.ArcheryRange));
        }

        [Test]
        public void DefensivePreparation_CreatesBarracksBeforeSpearmen()
        {
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            Assert.That(plan.CurrentMilestone.TacticalGoals.OfType<StrategicResourceAllocationGoalRequest>()
                .Select(r => r.ResourceType), Does.Contain(ResourceType.Food));
            Assert.That(plan.CurrentMilestone.RequiredResources, Is.Empty);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.TacticalGoals.OfType<StrategicBuildStructureGoalRequest>()
                .Single().StructureType, Is.EqualTo(BuildingType.Barracks));
            Assert.That(plan.CurrentMilestone.RequiredResources.Single().Amount,
                Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Barracks)));
        }

        [Test]
        public void DefensivePreparation_UsesCorrectResources()
        {
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            sim.GetUnitTrainingSpec(0, CommanderIntentCatalog.SpearmanUnitType, out _,
                out int food, out int wood, out _, out _);
            Assert.That(plan.CurrentMilestone.RequiredResources.Single(r => r.ResourceType == ResourceType.Food).Amount,
                Is.EqualTo(food * DefensivePreparationPlan.SpearmanTarget));
            Assert.That(plan.CurrentMilestone.RequiredResources.Single(r => r.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(wood * DefensivePreparationPlan.SpearmanTarget));
            Assert.That(plan.CurrentMilestone.RequiredResources.Any(r => r.ResourceType == ResourceType.Stone), Is.False);
        }

        [Test]
        public void EconomicExpansion_AllocatesStoneForTownCenter()
        {
            SetAge(2);
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.EconomicExpansion).Plan;
            Assert.That(plan.CurrentMilestone.TacticalGoals.OfType<StrategicResourceAllocationGoalRequest>()
                .Select(r => r.ResourceType), Does.Contain(ResourceType.Stone));
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.RequiredResources.Single(r => r.ResourceType == ResourceType.Stone).Amount,
                Is.EqualTo(sim.GetBuildingStoneCost(BuildingType.TownCenter)));
            Assert.That(plan.Milestones[2].TacticalGoals.OfType<StrategicEnsureUnitCountGoalRequest>().Single().UnitType,
                Is.EqualTo(CommanderIntentCatalog.VillagerUnitType));
        }

        [Test]
        public void CavalryPlan_ReleasesReservationsOnCancel()
        {
            sim.ResourceManager.GetPlayerResources(0).Wood = 0;
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            Assert.That(plan.CurrentMilestone.RequiredChildGoals.Select(goalManager.GetGoal).OfType<BuildStructureGoal>(), Is.Empty);
            sim.ResourceManager.GetPlayerResources(0).Wood = sim.GetBuildingWoodCost(BuildingType.Stables);
            strategicPlanner.Tick(30);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Wood), Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Stables)));
            strategicPlanner.CancelPlan(plan.StrategicPlanId);
            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Assert.That(strategicPlanner.ArchivedReservations, Is.Not.Empty);
        }
        [Test]
        public void DefensivePreparation_DoesNotDeadlockAgePrerequisite()
        {
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).OfType<BuildStructureGoal>()
                .Any(g => g.StructureType == BuildingType.Tower), Is.False);
        }

        [Test]
        public void DefensivePlan_AvailableTowersUseCanonicalWoodAndNoStone()
        {
            ((int[])typeof(GameSimulation).GetField("playerAges",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sim))[0] = 2;
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Defensive Structures"));
            Assert.That(plan.CurrentMilestone.RequiredResources.Single().ResourceType, Is.EqualTo(ResourceType.Wood));
            Assert.That(plan.CurrentMilestone.RequiredResources.Single().Amount, Is.EqualTo(2 * sim.GetBuildingWoodCost(BuildingType.Tower)));
        }

        [Test]
        public void DefensivePlan_ExistingBarracksIsReused()
        {
            sim.CreateBuilding(0, BuildingType.Barracks, x - 10, z, false);
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Defense Army"));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).OfType<BuildStructureGoal>(), Is.Empty);
        }

        [Test]
        public void CavalryPlan_CreatesMilestoneReservations()
        {
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            sim.GetUnitTrainingSpec(0, CommanderIntentCatalog.KnightUnitType, out _, out int food, out _, out int gold, out _);
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Food), Is.EqualTo(food * CavalryPressurePlan.KnightTarget));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Gold), Is.EqualTo(gold * CavalryPressurePlan.KnightTarget));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Wood), Is.Zero);
        }

        [Test]
        public void StrategicPipeline_PreservesParameterValidation()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var invalid = new StrategicIntent(1, 0, StrategicObjectiveType.DefensivePreparation, 0,
                new Dictionary<string, string> { { "targetCount", "-1" } });
            Assert.That(dispatcher.SubmitIntent(invalid).CreatedPlan, Is.False);
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));
            Assert.That(strategicPlanner.ActivePlans, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private void SetAge(int age) => ((int[])typeof(GameSimulation).GetField("playerAges",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sim))[0] = age;

        [Test]
        public void EconomicExpansion_DoesNotDeadlockOnTownCenterPrerequisite()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager, strategicPlanner: strategicPlanner);
            var result = dispatcher.SubmitIntent(new StrategicIntent(1, 0, StrategicObjectiveType.EconomicExpansion, 0));
            var plan = result.StrategicSubmission.Plan;
            StringAssert.Contains("age 2", result.Response);
            StringAssert.Contains("age 2", strategicPlanner.BuildContext(new CommanderContextBuilder().Build(sim, goalManager)).ActivePlans.Single().Reason);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.WaitingForPrerequisite));
            StringAssert.Contains("age 2", plan.OutcomeMessage);
            for (int tick = 0; tick < 300; tick += 30) strategicPlanner.Tick(tick);
            Assert.That(plan.ChildGoalIds, Is.Empty);
            SetAge(2);
            strategicPlanner.Tick(300);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(plan.ChildGoalIds, Has.Count.EqualTo(3));
        }

        [Test]
        public void EconomicExpansion_CreatesVillagersAfterTownCenter()
        {
            SetAge(2);
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.EconomicExpansion).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).OfType<EnsureUnitCountGoal>(), Is.Empty);
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(plan.CurrentMilestone.RequiredChildGoals.Select(goalManager.GetGoal)
                .OfType<EnsureUnitCountGoal>().Single().RequestedUnitType, Is.EqualTo(CommanderIntentCatalog.VillagerUnitType));
        }

        [Test]
        public void CavalryPlan_WaitsWhenResourcesMissing()
        {
            sim.ResourceManager.GetPlayerResources(0).Food = 0;
            sim.ResourceManager.GetPlayerResources(0).Gold = 0;
            var plan = strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            Assert.That(plan.ChildGoalIds.Select(goalManager.GetGoal).OfType<ResourceAllocationGoal>().Count(), Is.EqualTo(3));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
            sim.ResourceManager.GetPlayerResources(0).Food = 5000;
            sim.ResourceManager.GetPlayerResources(0).Gold = 5000;
            strategicPlanner.Tick(30);
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(strategicPlanner.Reservations, Has.Count.EqualTo(2));
        }

    }
}
