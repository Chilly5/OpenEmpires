using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("Phase3Fix12")]
    public class CommanderPhase3Fix12Tests
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

        // =========================================================================
        // Fix 1: Capability Catalog Expansion Tests
        // =========================================================================

        [Test]
        public void Catalog_AllowsTower()
        {
            Assert.That(CommanderIntentCatalog.IsSupportedStructure(BuildingType.Tower), Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("tower", out var b1) && b1 == BuildingType.Tower, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("watch tower", out var b2) && b2 == BuildingType.Tower, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("guard tower", out var b3) && b3 == BuildingType.Tower, Is.True);
            Assert.That(CommanderIntentCatalog.GetStructureDisplayName(BuildingType.Tower), Is.EqualTo("Tower"));
        }

        [Test]
        public void Catalog_AllowsTownCenter()
        {
            Assert.That(CommanderIntentCatalog.IsSupportedStructure(BuildingType.TownCenter), Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("town center", out var b1) && b1 == BuildingType.TownCenter, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("towncenter", out var b2) && b2 == BuildingType.TownCenter, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveStructure("tc", out var b3) && b3 == BuildingType.TownCenter, Is.True);
            Assert.That(CommanderIntentCatalog.GetStructureDisplayName(BuildingType.TownCenter), Is.EqualTo("Town Center"));
        }

        [Test]
        public void Catalog_AllowsVillager()
        {
            Assert.That(CommanderIntentCatalog.IsSupportedUnit(CommanderIntentCatalog.VillagerUnitType), Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveUnit("villager", out var u1) && u1 == CommanderIntentCatalog.VillagerUnitType, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveUnit("worker", out var u2) && u2 == CommanderIntentCatalog.VillagerUnitType, Is.True);
            Assert.That(CommanderIntentCatalog.TryResolveUnit("peasant", out var u3) && u3 == CommanderIntentCatalog.VillagerUnitType, Is.True);
            Assert.That(CommanderIntentCatalog.GetUnitDisplayName(CommanderIntentCatalog.VillagerUnitType), Is.EqualTo("Villager"));
        }

        [Test]
        public void DefensivePlan_CanBuildTower()
        {
            var registry = StrategicPlanRegistry.CreateDefault();
            var intent = new StrategicIntent(1, 0, StrategicObjectiveType.DefensivePreparation, 0,
                new Dictionary<string, string> { { "structure", "Tower" } });
            var template = registry.FindCompatibleTemplate(intent);
            Assert.That(template, Is.Not.Null);
            var validation = template.ValidateParameters(intent);
            Assert.That(validation.IsValid, Is.True, validation.Reason);

            CommanderGoal goal = goalManager.SubmitBuildStructure(BuildingType.Tower, 1);
            Assert.That(goal, Is.Not.Null);
            goalManager.Tick(0);
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Failed));
        }

        [Test]
        public void EconomicPlan_CanCreateVillagers()
        {
            var registry = StrategicPlanRegistry.CreateDefault();
            var intent = new StrategicIntent(1, 0, StrategicObjectiveType.EconomicExpansion, 0,
                new Dictionary<string, string> { { "targetType", "Villager" } });
            var template = registry.FindCompatibleTemplate(intent);
            Assert.That(template, Is.Not.Null);
            var validation = template.ValidateParameters(intent);
            Assert.That(validation.IsValid, Is.True, validation.Reason);

            CommanderGoal goal = goalManager.SubmitEnsureUnitCount(CommanderIntentCatalog.VillagerUnitType, 5);
            Assert.That(goal, Is.Not.Null);
            goalManager.Tick(0);
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Failed));
        }

        // =========================================================================
        // Fix 2: LLM JSON Compatibility Aliases Tests
        // =========================================================================

        [Test]
        public void LLMJson_TypeAliasAccepted()
        {
            string jsonWithType = @"{
                ""type"": ""strategic"",
                ""intentType"": ""DefensivePreparation"",
                ""objectiveType"": ""DefensivePreparation"",
                ""targetCount"": 10,
                ""priority"": 60
            }";

            CommanderContext ctx = new CommanderContextBuilder().Build(sim, goalManager);
            CommanderIntentInterpretation result = CommanderIntentDtoCodec.InterpretJson(jsonWithType, ctx);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.StrategicIntent, Is.Not.Null);
            Assert.That(result.StrategicIntent.ObjectiveType, Is.EqualTo(StrategicObjectiveType.DefensivePreparation));

            string jsonWithCategory = @"{
                ""category"": ""tactical"",
                ""intentType"": ""EnsureUnitCount"",
                ""targetType"": ""Spearman"",
                ""targetCount"": 6
            }";

            CommanderIntentInterpretation resultTactical = CommanderIntentDtoCodec.InterpretJson(jsonWithCategory, ctx);
            Assert.That(resultTactical.Success, Is.True, resultTactical.Reason);
            Assert.That(resultTactical.Intent, Is.TypeOf<EnsureUnitCountIntent>());
        }

        [Test]
        public void LLMJson_ObjectiveAliasAccepted()
        {
            string jsonWithObjective = @"{
                ""intentCategory"": ""strategic"",
                ""intentType"": ""StrategicPlan"",
                ""objective"": ""EconomicExpansion"",
                ""targetCount"": 15,
                ""priority"": 50
            }";

            CommanderContext ctx = new CommanderContextBuilder().Build(sim, goalManager);
            CommanderIntentInterpretation result = CommanderIntentDtoCodec.InterpretJson(jsonWithObjective, ctx);

            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(result.StrategicIntent, Is.Not.Null);
            Assert.That(result.StrategicIntent.ObjectiveType, Is.EqualTo(StrategicObjectiveType.EconomicExpansion));

            string jsonKebab = @"{
                ""type"": ""strategic"",
                ""objective"": ""cavalry-pressure"",
                ""targetCount"": 8
            }";

            CommanderIntentInterpretation resultKebab = CommanderIntentDtoCodec.InterpretJson(jsonKebab, ctx);
            Assert.That(resultKebab.Success, Is.True, resultKebab.Reason);
            Assert.That(resultKebab.StrategicIntent, Is.Not.Null);
            Assert.That(resultKebab.StrategicIntent.ObjectiveType, Is.EqualTo(StrategicObjectiveType.AttackPreparation));
        }

        [Test]
        public void LLMJson_InvalidFieldStillRejected()
        {
            string invalidJson = @"{
                ""type"": ""unsupported_category"",
                ""intentType"": ""Random"",
                ""targetCount"": 5
            }";

            CommanderContext ctx = new CommanderContextBuilder().Build(sim, goalManager);
            CommanderIntentInterpretation result = CommanderIntentDtoCodec.InterpretJson(invalidJson, ctx);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.EqualTo(CommanderIntentErrorCode.None));
        }

        // =========================================================================
        // Fix 3: Relax Strategic Parameter Validation Tests
        // =========================================================================

        [Test]
        public void StrategicIntent_AllowsOptionalParameters()
        {
            var parameters = new Dictionary<string, string>
            {
                { "focus", "flank" },
                { "stance", "aggressive" },
                { "targetSector", "north" },
                { "reserveRatio", "0.2" }
            };

            var registry = StrategicPlanRegistry.CreateDefault();
            foreach (var template in registry.Templates)
            {
                var testIntent = new StrategicIntent(1, 0,
                    template is CavalryPressurePlanTemplate ? StrategicObjectiveType.AttackPreparation
                    : template is DefensivePreparationPlanTemplate ? StrategicObjectiveType.DefensivePreparation
                    : template is EconomicExpansionPlanTemplate ? StrategicObjectiveType.EconomicExpansion
                    : StrategicObjectiveType.MilitaryReinforcement,
                    0, parameters);

                var validation = template.ValidateParameters(testIntent);
                Assert.That(validation.IsValid, Is.True, $"Template {template.TemplateId} should allow optional parameters: {validation.Reason}");
            }

            var strategicIntent = new StrategicIntent(1, 0, StrategicObjectiveType.AttackPreparation, 10,
                parameters: parameters);
            StrategicIntentSubmission submission = strategicPlanner.SubmitIntent(strategicIntent,
                isEmergency: false, isPlayerOverride: true);

            Assert.That(submission.CreatedPlan, Is.True);
            Assert.That(strategicIntent.Parameters["focus"], Is.EqualTo("flank"));
            Assert.That(strategicIntent.Parameters["stance"], Is.EqualTo("aggressive"));
        }

        [Test]
        public void StrategicIntent_StillRejectsInvalidRequiredParameters()
        {
            var registry = StrategicPlanRegistry.CreateDefault();
            var invalidParams = new Dictionary<string, string>
            {
                { "targetCount", "-5" }
            };
            var intent = new StrategicIntent(1, 0, StrategicObjectiveType.AttackPreparation, 0, invalidParams);
            var template = registry.FindCompatibleTemplate(intent);
            Assert.That(template, Is.Not.Null);

            var validation = template.ValidateParameters(intent);
            Assert.That(validation.IsValid, Is.False);
            StringAssert.Contains("targetCount", validation.Reason);
        }

        // =========================================================================
        // Fix 4: Strategic Override Plan Accumulation Tests
        // =========================================================================

        [Test]
        public void StrategicPlanner_CancelsConflictingOverridePlans()
        {
            StrategicIntentSubmission sub1 = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);
            Assert.That(sub1.CreatedPlan, Is.True);
            StrategicPlan plan1 = sub1.Plan;
            Assert.That(plan1.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan1.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));

            var overrideIntent = new StrategicIntent(2, 0,
                StrategicObjectiveType.DefensivePreparation, 0);
            StrategicIntentSubmission sub2 = strategicPlanner.SubmitIntent(
                overrideIntent, isEmergency: false, isPlayerOverride: true);

            Assert.That(sub2.CreatedPlan, Is.True);
            StrategicPlan plan2 = sub2.Plan;
            Assert.That(plan2.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan2.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));

            Assert.That(plan1.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(strategicPlanner.ArchivedPlans.Contains(plan1), Is.True);
            Assert.That(strategicPlanner.ActivePlans.Contains(plan1), Is.False);
            Assert.That(strategicPlanner.ActivePlans.Contains(plan2), Is.True);
        }

        [Test]
        public void EmergencyPlan_SupersedesNormalPlan()
        {
            StrategicPlan plan1 = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(plan1.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan1.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));

            var emergencyIntent = new StrategicIntent(2, 0,
                StrategicObjectiveType.DefensivePreparation, 0);
            StrategicIntentSubmission sub2 = strategicPlanner.SubmitIntent(
                emergencyIntent, isEmergency: true, isPlayerOverride: false);

            Assert.That(sub2.CreatedPlan, Is.True);
            StrategicPlan plan2 = sub2.Plan;
            Assert.That(plan2.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan2.Authority, Is.EqualTo(StrategicPlanAuthority.Emergency));

            Assert.That(plan1.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(strategicPlanner.ActivePlans.Contains(plan1), Is.False);
            Assert.That(strategicPlanner.ActivePlans.Contains(plan2), Is.True);

            var overrideIntent = new StrategicIntent(3, 0,
                StrategicObjectiveType.AttackPreparation, 0);
            StrategicIntentSubmission subOverride = strategicPlanner.SubmitIntent(
                overrideIntent, isEmergency: false, isPlayerOverride: true);
            Assert.That(subOverride.CreatedPlan, Is.True);
            Assert.That(subOverride.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));

            var emergencyIntent2 = new StrategicIntent(4, 0,
                StrategicObjectiveType.MilitaryReinforcement, 0);
            StrategicIntentSubmission sub3 = strategicPlanner.SubmitIntent(
                emergencyIntent2, isEmergency: true, isPlayerOverride: false);
            Assert.That(sub3.CreatedPlan, Is.False, "Emergency cannot override an active PlayerOverride plan.");
        }

        [Test]
        public void CancelledPlan_ReleasesReservations()
        {
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);

            Assert.That(strategicPlanner.Reservations, Is.Not.Empty);
            int woodReserved = strategicPlanner.GetReservedAmount(ResourceType.Wood);
            Assert.That(woodReserved, Is.GreaterThan(0));

            var overrideIntent = new StrategicIntent(2, 0,
                StrategicObjectiveType.AttackPreparation, 0);
            strategicPlanner.SubmitIntent(overrideIntent, isEmergency: false, isPlayerOverride: true);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(r => r.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
        }

        // =========================================================================
        // Fix 5: GameBootstrapper Lifecycle Cleanup Tests
        // =========================================================================

        [Test]
        public void GameBootstrapper_DisposesCommanderSystems()
        {
            var go = new GameObject("TestBootstrapper");
            try
            {
                var bootstrapper = go.AddComponent<GameBootstrapper>();
                Assert.That(bootstrapper.Commander, Is.Null);
                Assert.That(bootstrapper.StrategicCommander, Is.Null);
                Assert.That(bootstrapper.CommanderStrategicPipeline, Is.Null);
                Assert.That(bootstrapper.CommanderDispatcher, Is.Null);

                Assert.DoesNotThrow(() => bootstrapper.DisposeCommanderSystems());
                Assert.DoesNotThrow(() => bootstrapper.ShutdownMatch());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EventsDoNotFireAfterDispose()
        {
            int plannerEventCount = 0;
            strategicPlanner.PlanStatusChanged += _ => plannerEventCount++;
            strategicPlanner.StrategicIntentCreated += _ => plannerEventCount++;

            int goalEventCount = 0;
            goalManager.GoalStatusChanged += _ => goalEventCount++;

            int pipelineEventCount = 0;
            pipeline.EvaluationCompleted += _ => pipelineEventCount++;

            // Dispose systems
            pipeline.Dispose();
            strategicPlanner.Dispose();
            goalManager.Dispose();

            Assert.That(plannerEventCount, Is.Zero);
            Assert.That(goalEventCount, Is.Zero);
            Assert.That(pipelineEventCount, Is.Zero);
        }

        // =========================================================================
        // Scenarios 1-4 Runtime Integration Tests
        // =========================================================================

        [Test]
        public void Scenario1_PlayerAttackedEmergencyDefenseTriggers()
        {
            UnitData enemy = sim.UnitRegistry.CreateUnit(1,
                sim.MapData.TileToWorldFixed(x + 5, z), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            enemy.IsVillager = false;
            enemy.UnitType = 1;
            enemy.MaxHealth = enemy.CurrentHealth = 100;
            enemy.State = UnitState.Idle;
            sim.FogOfWar.SetVisible(0, x + 5, z);

            StrategicIntentSubmission econSub = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.EconomicExpansion);
            Assert.That(econSub.CreatedPlan, Is.True);

            pipeline.EvaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.Emergency, "Enemy raid detected at base");
            pipeline.Tick(0);

            StrategicPlan activeDefense = strategicPlanner.ActivePlans
                .FirstOrDefault(p => p.PlanType == StrategicPlanType.DefensivePreparation);
            Assert.That(activeDefense, Is.Not.Null, "Emergency defense plan should be initiated.");
            Assert.That(activeDefense.Authority, Is.EqualTo(StrategicPlanAuthority.Emergency));
        }

        [Test]
        public void Scenario2_CavalryPlanFailsFallbackOrTransition()
        {
            StrategicPlan cavalryPlan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(cavalryPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));

            foreach (int goalId in cavalryPlan.ChildGoalIds)
                goalManager.CancelGoal(goalId);

            Assert.That(cavalryPlan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(strategicPlanner.Reservations, Is.Empty, "All reservations released on plan failure.");

            var policy = new StrategicCommitmentPolicy();
            bool canTransition = policy.CanTransition(cavalryPlan, StrategicPlanType.EconomicExpansion,
                isEmergency: false, out string reason);
            Assert.That(canTransition, Is.True, reason);

            StrategicIntentSubmission recoverySub = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.EconomicExpansion);
            Assert.That(recoverySub.CreatedPlan, Is.True);
        }

        [Test]
        public void Scenario3_PlayerOverrideCancelsPendingStrategicActions()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);

            strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation);
            Assert.That(strategicPlanner.ActivePlans.Single().PlanType, Is.EqualTo(StrategicPlanType.CavalryPressure));

            CommanderIntentSubmission overrideResult = dispatcher.SubmitText("prepare defense");
            Assert.That(overrideResult.CreatedPlan, Is.True);

            StrategicPlan currentPlan = strategicPlanner.ActivePlans.Single();
            Assert.That(currentPlan.PlanType, Is.EqualTo(StrategicPlanType.DefensivePreparation));
            Assert.That(currentPlan.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));
        }

        [Test]
        public void Scenario4_MatchEndShutsDownCommanderCleanly()
        {
            var go = new GameObject("BootstrapperScenario4");
            try
            {
                var bootstrapper = go.AddComponent<GameBootstrapper>();
                bootstrapper.ShutdownMatch();
                Assert.That(bootstrapper.Commander, Is.Null);
                Assert.That(bootstrapper.StrategicCommander, Is.Null);
                Assert.That(bootstrapper.CommanderStrategicPipeline, Is.Null);
                Assert.That(bootstrapper.CommanderDispatcher, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
