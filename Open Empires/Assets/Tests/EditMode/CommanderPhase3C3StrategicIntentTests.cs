using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C3StrategicIntentTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
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

        [Test]
        public void StrategicIntent_CreatesCorrectly()
        {
            var source = new Dictionary<string, string>();
            var intent = new StrategicIntent(7, 0,
                StrategicObjectiveType.AttackPreparation, 42, source, 80);
            source["lateMutation"] = "ignored";

            Assert.That(intent.IntentId, Is.EqualTo(7));
            Assert.That(intent.PlayerId, Is.Zero);
            Assert.That(intent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.AttackPreparation));
            Assert.That(intent.CreatedTick, Is.EqualTo(42));
            Assert.That(intent.Priority, Is.EqualTo(80));
            Assert.That(intent.Parameters, Is.Empty, "Intent parameters must be detached values.");
            Assert.That(intent.Status, Is.EqualTo(StrategicIntentStatus.Created));
            Assert.That(intent.IntentLayer, Is.EqualTo(CommanderIntentLayer.Strategic));
            Assert.That(new EnsureUnitCountIntent(0, 1, 1).IntentLayer,
                Is.EqualTo(CommanderIntentLayer.Tactical));
        }

        [Test]
        public void StrategicIntent_RejectsUnknownObjective()
        {
            var intent = new StrategicIntent(1, 0, (StrategicObjectiveType)999, 0);

            StrategicIntentValidationResult result = new StrategicIntentValidator().Validate(
                intent, 0, StrategicPlanRegistry.CreateDefault());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Is.EqualTo(StrategicIntentValidationError.UnknownObjective));
        }

        [Test]
        public void StrategicIntent_PreservesOwnership()
        {
            var intent = new StrategicIntent(1, 1,
                StrategicObjectiveType.AttackPreparation, 0);

            StrategicIntentValidationResult result = new StrategicIntentValidator().Validate(
                intent, 0, StrategicPlanRegistry.CreateDefault());

            Assert.That(intent.PlayerId, Is.EqualTo(1));
            Assert.That(result.Error, Is.EqualTo(StrategicIntentValidationError.PlayerMismatch));
        }

        [Test]
        public void StrategicIntent_RejectsUnsupportedParameter()
        {
            var intent = new StrategicIntent(1, 0,
                StrategicObjectiveType.AttackPreparation, 0,
                new Dictionary<string, string> { { "enemyPrediction", "true" } });

            StrategicIntentValidationResult result = new StrategicIntentValidator().Validate(
                intent, 0, StrategicPlanRegistry.CreateDefault());

            Assert.That(result.Error,
                Is.EqualTo(StrategicIntentValidationError.UnsupportedParameter));
            StringAssert.Contains("enemyPrediction", result.Reason);
        }

        [Test]
        public void PlanTemplate_RegistersCorrectly()
        {
            var registry = new StrategicPlanRegistry();

            registry.Register(new CavalryPressurePlanTemplate());

            Assert.That(registry.Templates, Has.Count.EqualTo(1));
            Assert.That(registry.Templates[0].TemplateId,
                Is.EqualTo(CavalryPressurePlanTemplate.Id));
            Assert.Throws<InvalidOperationException>(() =>
                registry.Register(new CavalryPressurePlanTemplate()));
        }

        [Test]
        public void PlanTemplate_SelectsCompatibleTemplate()
        {
            StrategicPlanRegistry registry = StrategicPlanRegistry.CreateDefault();
            var intent = new StrategicIntent(1, 0,
                StrategicObjectiveType.AttackPreparation, 0);

            IStrategicPlanTemplate selected = registry.FindCompatibleTemplate(intent);

            Assert.That(selected, Is.TypeOf<CavalryPressurePlanTemplate>());
        }

        [Test]
        public void PlanTemplate_NoMatchFailsSafely()
        {
            StrategicPlanRegistry registry = StrategicPlanRegistry.CreateDefault();
            var intent = new StrategicIntent(1, 0,
                StrategicObjectiveType.DefensivePreparation, 0);

            Assert.That(registry.FindCompatibleTemplate(intent), Is.Null);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => registry.CreatePlan(intent));
            Assert.That(error.Message, Is.EqualTo("No available strategic plan template."));
        }

        [Test]
        public void CavalryTemplate_CreatesCorrectPlan()
        {
            var intent = new StrategicIntent(9, 0,
                StrategicObjectiveType.AttackPreparation, 12);

            StrategicPlan plan = new CavalryPressurePlanTemplate().CreatePlan(intent);

            Assert.That(plan, Is.TypeOf<CavalryPressurePlan>());
            Assert.That(plan.SourceIntentId, Is.EqualTo(9));
            Assert.That(plan.OwnerPlayerId, Is.Zero);
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.CavalryPressure));
        }

        [Test]
        public void CavalryTemplate_PreservesMilestones()
        {
            var intent = new StrategicIntent(1, 0,
                StrategicObjectiveType.AttackPreparation, 0);
            StrategicPlan plan = new CavalryPressurePlanTemplate().CreatePlan(intent);

            Assert.That(plan.Milestones.Select(item => item.Name), Is.EqualTo(new[]
            {
                "Economic Foundation", "Infrastructure", "Army Preparation", "Ready"
            }));
            Assert.That(plan.Milestones.Select(item => item.TacticalGoals.Count),
                Is.EqualTo(new[] { 2, 1, 1, 0 }));
            Assert.That(plan.Milestones[0].TacticalGoals,
                Has.All.TypeOf<StrategicResourceAllocationGoalRequest>());
            Assert.That(plan.Milestones[1].TacticalGoals.Single(),
                Is.TypeOf<StrategicBuildStructureGoalRequest>());
            Assert.That(plan.Milestones[2].TacticalGoals.Single(),
                Is.TypeOf<StrategicEnsureUnitCountGoalRequest>());
        }

        [Test]
        public void CavalryTemplate_ExecutesExistingGoals()
        {
            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            CommanderGoal[] goals = submission.Plan.ChildGoalIds
                .Select(goalManager.GetGoal).ToArray();
            Assert.That(goals, Has.Length.EqualTo(2));
            Assert.That(goals.OfType<ResourceAllocationGoal>().Select(item => item.Resource),
                Is.EquivalentTo(new[] { ResourceType.Food, ResourceType.Gold }));
        }

        [Test]
        public void StrategicPlanner_CreatesPlanFromIntent()
        {
            StrategicIntent created = null;
            planner.StrategicIntentCreated += intent => created = intent;

            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            Assert.That(submission.CreatedPlan, Is.True);
            Assert.That(submission.Plan, Is.TypeOf<CavalryPressurePlan>());
            Assert.That(submission.Plan.SourceIntentId, Is.EqualTo(submission.Intent.IntentId));
            Assert.That(submission.Intent.Status, Is.EqualTo(StrategicIntentStatus.Active));
            Assert.That(created, Is.SameAs(submission.Intent));
            Assert.That(planner.GetIntent(submission.Intent.IntentId), Is.SameAs(submission.Intent));
        }

        [Test]
        public void StrategicPlanner_RejectsUnsupportedIntent()
        {
            string rejected = null;
            planner.StrategicIntentRejected += (_, reason) => rejected = reason;

            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation);

            Assert.That(submission.CreatedPlan, Is.False);
            Assert.That(submission.Error,
                Is.EqualTo(StrategicIntentValidationError.NoCompatibleTemplate));
            Assert.That(submission.Intent.Status, Is.EqualTo(StrategicIntentStatus.Rejected));
            Assert.That(rejected, Is.EqualTo("No available strategic plan template."));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
        }

        [Test]
        public void StrategicPlanner_DoesNotBypassCommanderGoals()
        {
            var startedGoalIds = new List<int>();
            goalManager.GoalEventPublished += goalEvent =>
            {
                if (goalEvent.EventType == CommanderGoalEventType.GoalStarted)
                    startedGoalIds.Add(goalEvent.Goal.GoalId);
            };

            StrategicIntentSubmission submission = planner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            Assert.That(startedGoalIds, Is.EqualTo(submission.Plan.ChildGoalIds));
            Assert.That(submission.Plan.ChildGoalIds.Select(goalManager.GetGoal),
                Is.EqualTo(goalManager.Goals));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty,
                "Strategic submission creates Commander goals; Commander evaluation owns commands.");
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
