using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C5StrategicDecisionTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private StrategicPlanner planner;
        private RuleBasedStrategicDecisionPolicy policy;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            goalManager = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goalManager, CurrentResourceAmount);
            policy = new RuleBasedStrategicDecisionPolicy();
        }

        [TearDown]
        public void TearDown()
        {
            planner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void DecisionPolicy_SelectsHighestPriority()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();
            var recommendations = new[]
            {
                Recommendation(1, StrategicObjectiveType.AttackPreparation, 100),
                Recommendation(4, StrategicObjectiveType.MilitaryReinforcement, 85),
                Recommendation(3, StrategicObjectiveType.EconomicExpansion, 100)
            };

            StrategicDecisionResult result = policy.Decide(context, recommendations);

            Assert.That(result.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.MilitaryReinforcement));
            Assert.That(result.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.High));
        }

        [Test]
        public void DecisionPolicy_DefenseOverridesAttack()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();
            var recommendations = new[]
            {
                Recommendation(1, StrategicObjectiveType.AttackPreparation, 100),
                Recommendation(2, StrategicObjectiveType.DefensivePreparation, 90)
            };

            StrategicDecisionResult result = policy.Decide(context, recommendations);

            Assert.That(result.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.DefensivePreparation));
            Assert.That(result.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Emergency));
        }

        [Test]
        public void DecisionPolicy_AttackRequiresConditions()
        {
            StrategicRecommendation attack = Recommendation(1,
                StrategicObjectiveType.AttackPreparation, 100);

            StrategicDecisionResult unavailable = policy.Decide(Context(),
                new[] { attack });
            PrepareAttackConditions();
            StrategicDecisionResult available = policy.Decide(Context(),
                new[] { attack });

            Assert.That(unavailable.Status, Is.EqualTo(StrategicDecisionStatus.Rejected));
            Assert.That(unavailable.SelectedIntent, Is.Null);
            Assert.That(available.Status, Is.EqualTo(StrategicDecisionStatus.Selected));
            Assert.That(available.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.AttackPreparation));
        }

        [Test]
        public void DecisionPolicy_EconomicFallback()
        {
            StrategicContext context = Context();
            var recommendations = new[]
            {
                Recommendation(1, StrategicObjectiveType.AttackPreparation, 100),
                Recommendation(3, StrategicObjectiveType.EconomicExpansion, 40)
            };

            StrategicDecisionResult result = policy.Decide(context, recommendations);

            Assert.That(result.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.EconomicExpansion));
            Assert.That(result.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Low));
        }

        [Test]
        public void PlayerStrategicIntent_OverridesRecommendations()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();
            var playerIntent = new StrategicIntent(900, 0,
                StrategicObjectiveType.EconomicExpansion, context.SnapshotTick);

            StrategicDecisionResult result = policy.Decide(context,
                new[] { Recommendation(1, StrategicObjectiveType.AttackPreparation, 100) },
                playerIntent);

            Assert.That(result.SelectedIntent, Is.SameAs(playerIntent));
            Assert.That(result.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.EconomicExpansion));
            Assert.That(result.SourceRecommendation, Is.Null);
            Assert.That(result.Reason,
                Is.EqualTo(RuleBasedStrategicDecisionPolicy.PlayerOverrideReason));
        }

        [Test]
        public void PlayerIntentPreservesOwnership()
        {
            StrategicContext context = Context();
            var playerIntent = new StrategicIntent(901, context.PlayerId,
                StrategicObjectiveType.DefensivePreparation, context.SnapshotTick);

            StrategicIntent selected = policy.SelectIntent(context,
                Array.Empty<StrategicRecommendation>(), playerIntent);

            Assert.That(selected, Is.SameAs(playerIntent));
            Assert.That(selected.PlayerId, Is.EqualTo(context.PlayerId));
            Assert.That(selected.Status, Is.EqualTo(StrategicIntentStatus.Created));
        }

        [Test]
        public void DecisionResult_ContainsReason()
        {
            StrategicRecommendation economy = Recommendation(3,
                StrategicObjectiveType.EconomicExpansion, 40,
                "Measured economy supports growth.");

            StrategicDecisionResult result = policy.Decide(Context(),
                new[] { economy });

            Assert.That(result.Status, Is.EqualTo(StrategicDecisionStatus.Selected));
            Assert.That(result.Reason, Does.Contain(economy.Reason));
            Assert.That(result.CreatedTick, Is.EqualTo(Context().SnapshotTick));
        }

        [Test]
        public void DecisionResult_TracksSourceRecommendation()
        {
            StrategicRecommendation economy = Recommendation(3,
                StrategicObjectiveType.EconomicExpansion, 40);

            StrategicDecisionResult result = policy.Decide(Context(),
                new[] { economy });

            Assert.That(result.SourceRecommendation, Is.SameAs(economy));
            Assert.That(result.SelectedIntent.ObjectiveType,
                Is.EqualTo(economy.ObjectiveType));
        }

        [Test]
        public void DecisionPolicy_SameInputProducesSameOutput()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();
            var recommendations = new[]
            {
                Recommendation(1, StrategicObjectiveType.AttackPreparation, 100),
                Recommendation(3, StrategicObjectiveType.EconomicExpansion, 40)
            };

            StrategicDecisionResult first = policy.Decide(context, recommendations);
            StrategicDecisionResult second = policy.Decide(context, recommendations);

            Assert.That(Describe(second), Is.EqualTo(Describe(first)));
            Assert.That(recommendations.Select(item => item.Status),
                Has.All.EqualTo(StrategicRecommendationStatus.Proposed));
        }

        [Test]
        public void DecisionPolicy_HasNoHiddenState()
        {
            FieldInfo[] fields = typeof(RuleBasedStrategicDecisionPolicy)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo select = typeof(IStrategicDecisionPolicy).GetMethod("SelectIntent",
                new[]
                {
                    typeof(StrategicContext),
                    typeof(IReadOnlyList<StrategicRecommendation>)
                });

            Assert.That(fields, Is.Empty);
            Assert.That(select, Is.Not.Null);
            Assert.That(select.ReturnType, Is.EqualTo(typeof(StrategicIntent)));
        }

        [Test]
        public void DecisionPolicy_DoesNotCreatePlans()
        {
            StrategicDecisionResult result = policy.Decide(Context(),
                new[] { Recommendation(3, StrategicObjectiveType.EconomicExpansion, 40) });

            Assert.That(result.HasSelection, Is.True);
            AssertNoExecution();
        }

        [Test]
        public void DecisionPolicy_DoesNotCreateCommands()
        {
            policy.Decide(Context(),
                new[] { Recommendation(3, StrategicObjectiveType.EconomicExpansion, 40) });

            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
        }

        [Test]
        public void DecisionPolicy_DoesNotBypassStrategicPlanner()
        {
            PrepareAttackConditions();
            StrategicDecisionResult decision = policy.Decide(Context(),
                new[] { Recommendation(1, StrategicObjectiveType.AttackPreparation, 100) });

            AssertNoExecution();
            StrategicIntentSubmission submission = planner.SubmitIntent(decision.SelectedIntent);

            Assert.That(submission.Status,
                Is.EqualTo(StrategicIntentSubmissionStatus.PlanCreated));
            Assert.That(submission.Plan, Is.Not.Null);
            Assert.That(planner.Intents, Has.Count.EqualTo(1));
            Assert.That(planner.Plans, Has.Count.EqualTo(1));
        }

        private StrategicContext Context()
        {
            return planner.BuildContext(
                new CommanderContextBuilder().Build(simulation, goalManager));
        }

        private StrategicRecommendation Recommendation(int id,
            StrategicObjectiveType objectiveType, int score, string reason = "Measured input.")
        {
            return new StrategicRecommendation(id, 0, objectiveType, score, reason,
                simulation.CurrentTick, score);
        }

        private static string Describe(StrategicDecisionResult result)
        {
            StrategicIntent intent = result.SelectedIntent;
            return result.Status + ":" + result.PriorityLevel + ":" + result.Reason + ":"
                + result.CreatedTick + ":" + intent.IntentId + ":" + intent.PlayerId + ":"
                + intent.ObjectiveType + ":" + intent.CreatedTick + ":" + intent.Priority;
        }

        private void PrepareAttackConditions()
        {
            SetResources(1000, 0, 700, 0);
            while (OwnedMilitaryCount() < RuleBasedStrategicEvaluator.StrongMilitaryThreshold)
                Unit(0, 1, x - 20 + OwnedMilitaryCount(), z + 5);
        }

        private int OwnedMilitaryCount()
        {
            return simulation.UnitRegistry.GetAllUnits()
                .Count(unit => unit.PlayerId == 0 && unit.CurrentHealth > 0
                    && !unit.IsVillager && unit.UnitType != 5);
        }

        private UnitData Unit(int playerId, int unitType, int tileX, int tileZ)
        {
            UnitData unit = simulation.UnitRegistry.CreateUnit(playerId,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.One, Fixed32.One);
            unit.UnitType = unitType;
            unit.IsVillager = unitType == 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void SetResources(int food, int wood, int gold, int stone)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = food;
            resources.Wood = wood;
            resources.Gold = gold;
            resources.Stone = stone;
        }

        private void AssertNoExecution()
        {
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
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
