using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("Phase3Fix11")]
    public class CommanderPhase3Fix11Tests
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
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
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
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void StrategicPipeline_EvaluatesContext()
        {
            var evaluator = new RecordingEvaluator();
            ReplacePipeline(evaluator);

            pipeline.EvaluateNow(StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(evaluator.LastContext, Is.SameAs(pipeline.LastContext));
            Assert.That(pipeline.LastContext.PlayerId, Is.EqualTo(0));
        }

        [Test]
        public void StrategicPipeline_CreatesRecommendation()
        {
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(pipeline.LastRecommendations, Is.Not.Empty);
            Assert.That(pipeline.DecisionHistory.History.Single().RecommendationCount,
                Is.EqualTo(pipeline.LastRecommendations.Count));
        }

        [Test]
        public void StrategicPipeline_SelectsIntent()
        {
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(pipeline.LastDecision.HasSelection, Is.True);
            Assert.That(pipeline.LastDecision.SelectedIntent, Is.Not.Null);
        }

        [Test]
        public void StrategicPipeline_SubmitsAllowedIntent()
        {
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(pipeline.LastSubmission.CreatedPlan, Is.True);
            Assert.That(strategicPlanner.ActivePlans, Has.Count.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Single().Submission,
                Is.SameAs(pipeline.LastSubmission));
        }

        [Test]
        public void StrategicPipeline_RejectsBlockedTransition()
        {
            StrategicPlan attack = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation).Plan;
            ReplacePipeline(new FixedEvaluator(StrategicObjectiveType.MilitaryReinforcement));

            StrategicDecisionRecord record = pipeline.EvaluateNow(
                StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(attack.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(record.TransitionAllowed, Is.False);
            Assert.That(pipeline.LastSubmission, Is.Null);
            Assert.That(strategicPlanner.ActivePlans, Has.Count.EqualTo(1));
            StringAssert.Contains("blocked", record.Outcome.ToLowerInvariant());
        }

        [Test]
        public void StrategicPipeline_DoesNotCreateCommandsDirectly()
        {
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.StrategicEvent);

            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(strategicPlanner.ActivePlans, Is.Not.Empty);
        }

        [Test]
        public void Dispatcher_SubmitsTacticalIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);

            CommanderIntentSubmission result = dispatcher.SubmitText("make 5 spearmen");

            Assert.That(result.CreatedGoal, Is.True);
            Assert.That(result.Interpretation.Intent, Is.TypeOf<EnsureUnitCountIntent>());
        }

        [Test]
        public void Dispatcher_SubmitsStrategicIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);

            CommanderIntentSubmission result = dispatcher.SubmitText("prepare cavalry attack");

            Assert.That(result.CreatedPlan, Is.True);
            Assert.That(result.Interpretation.StrategicIntent, Is.Not.Null);
            Assert.That(result.StrategicSubmission.Plan.PlanType,
                Is.EqualTo(StrategicPlanType.CavalryPressure));
        }

        [Test]
        public void Dispatcher_RejectsInvalidStrategicIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);
            var foreign = new StrategicIntent(1, 1,
                StrategicObjectiveType.AttackPreparation, 0);

            CommanderIntentSubmission result = dispatcher.SubmitIntent(foreign);

            Assert.That(result.CreatedPlan, Is.False);
            StringAssert.Contains("mismatch", result.Response.ToLowerInvariant());
            Assert.That(strategicPlanner.Plans, Is.Empty);
        }

        [Test]
        public void Dispatcher_DoesNotCreateCommandsFromStrategicIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);

            CommanderIntentSubmission result = dispatcher.SubmitText("prepare cavalry attack");

            Assert.That(result.CreatedPlan, Is.True);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void PlanStartsWithoutFullFutureBudget()
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;

            StrategicIntentSubmission result = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            Assert.That(result.CreatedPlan, Is.True);
            Assert.That(result.Plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(result.Plan.BudgetRequirements.Select(item => item.Amount),
                Is.EqualTo(new[] { config.KnightFoodCost * CavalryPressurePlan.KnightTarget, sim.GetBuildingWoodCost(BuildingType.Stables), config.KnightGoldCost * CavalryPressurePlan.KnightTarget }));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
        }

        [Test]
        public void MilestoneWaitsForResources()
        {
            sim.ResourceManager.GetPlayerResources(0).Wood = 0;

            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan.CurrentMilestone.Status,
                Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            Assert.That(plan.ChildGoalIds, Is.Not.Empty,
                "Resource-gathering goals remain able to run while the milestone waits.");
        }

        [Test]
        public void MilestoneContinuesAfterResourcesAvailable()
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Wood = 0;
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);

            resources.Wood = 500;
            strategicPlanner.Tick(30);

            Assert.That(plan.CurrentMilestone.Status,
                Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(strategicPlanner.GetReservedAmountForPlan(
                plan.StrategicPlanId, ResourceType.Wood), Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Barracks)));
        }

        [Test]
        public void FailedPlanReleasesReservations()
        {
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(strategicPlanner.Reservations, Is.Not.Empty);

            goalManager.CancelGoal(plan.ChildGoalIds[0]);

            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status != StrategicResourceReservationStatus.Active), Is.True);
        }

        [Test]
        public void CancelledPlanReleasesReservations()
        {
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);

            strategicPlanner.CancelPlan(plan.StrategicPlanId);

            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Assert.That(strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(item => item.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
        }

        [Test]
        public void CommitmentBlocksRapidSwitching()
        {
            strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation);

            StrategicIntentSubmission blocked = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.MilitaryReinforcement);

            Assert.That(blocked.CreatedPlan, Is.False);
            Assert.That(blocked.Error,
                Is.EqualTo(StrategicIntentValidationError.CommitmentBlocked));
        }

        [Test]
        public void EmergencyOverridesCommitment()
        {
            strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation);
            var defense = new StrategicIntent(2, 0,
                StrategicObjectiveType.DefensivePreparation, 0);

            StrategicIntentSubmission allowed = strategicPlanner.SubmitIntent(
                defense, isEmergency: true, isPlayerOverride: false);

            Assert.That(allowed.CreatedPlan, Is.True);
        }

        [Test]
        public void PlayerOverrideBypassesCommitment()
        {
            strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation);
            var reinforcement = new StrategicIntent(2, 0,
                StrategicObjectiveType.MilitaryReinforcement, 0);

            StrategicIntentSubmission allowed = strategicPlanner.SubmitIntent(
                reinforcement, isEmergency: false, isPlayerOverride: true);

            Assert.That(allowed.CreatedPlan, Is.True);
        }

        [Test]
        public void WorkerSelectionUsesDistanceRanking()
        {
            UnitData far = Worker(x - 12, z);
            UnitData near = Worker(x + 1, z);
            Resource(ResourceType.Wood, x + 3, z);
            goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);

            goalManager.Tick(0);

            GatherCommand command = sim.CommandBuffer.FlushCommands()
                .OfType<GatherCommand>().Single();
            Assert.That(command.UnitIds.Single(), Is.EqualTo(near.Id));
            Assert.That(command.UnitIds.Single(), Is.Not.EqualTo(far.Id));
        }

        [Test]
        public void WorkerSelectionLimitsPathQueries()
        {
            for (int i = 0; i < 12; i++) Worker(x - 15 + i, z);
            for (int i = 0; i < 8; i++) Resource(ResourceType.Wood, x + 2 + i, z);
            goalManager.ResetDiagnosticPathCheckCount();
            goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);

            goalManager.Tick(0);

            Assert.That(goalManager.DiagnosticPathCheckCount,
                Is.LessThanOrEqualTo(CommanderPlanner.MaximumPathValidationCandidates));
        }

        [Test]
        public void WorkerSelectionStillRejectsUnreachableWorkers()
        {
            Worker(x - 5, z);
            ResourceNodeData node = Resource(ResourceType.Wood, x + 5, z);
            for (int tileX = node.TileX - 1; tileX <= node.TileX + 1; tileX++)
                for (int tileZ = node.TileZ - 1; tileZ <= node.TileZ + 1; tileZ++)
                    if (tileX != node.TileX || tileZ != node.TileZ)
                        sim.MapData.Tiles[tileX, tileZ] = TileType.Water;
            CommanderGoal goal = goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);

            goalManager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void CompletedGoalsAreArchived()
        {
            CommanderGoal goal = goalManager.SubmitResourceAllocation(ResourceType.Wood, 0);

            goalManager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(goalManager.ActiveGoals.Contains(goal), Is.False);
            Assert.That(goalManager.ArchivedGoals.Contains(goal), Is.True);
        }

        [Test]
        public void ArchivedGoalsAreBounded()
        {
            for (int i = 0; i < CommanderGoalManager.MaxArchivedGoals + 5; i++)
            {
                CommanderGoal goal = goalManager.SubmitResourceAllocation(ResourceType.Wood, 0);
                goalManager.CancelGoal(goal.GoalId);
            }

            Assert.That(goalManager.ArchivedGoals,
                Has.Count.EqualTo(CommanderGoalManager.MaxArchivedGoals));
            Assert.That(goalManager.Goals,
                Has.Count.EqualTo(CommanderGoalManager.MaxArchivedGoals));
        }

        [Test]
        public void ArchivedPlansAreBounded()
        {
            for (int i = 0; i < StrategicPlanner.MaxArchivedPlans + 5; i++)
            {
                StrategicIntentSubmission submission = strategicPlanner.SubmitIntent(
                    new StrategicIntent(i + 1, 0,
                        StrategicObjectiveType.AttackPreparation, 0),
                    isEmergency: false, isPlayerOverride: true);
                strategicPlanner.CompleteMilestoneAndAdvance(submission.Plan.StrategicPlanId);
                strategicPlanner.CancelPlan(submission.Plan.StrategicPlanId);
            }

            Assert.That(strategicPlanner.ArchivedPlans,
                Has.Count.EqualTo(StrategicPlanner.MaxArchivedPlans));
            Assert.That(strategicPlanner.Plans,
                Has.Count.EqualTo(StrategicPlanner.MaxArchivedPlans));
        }

        [Test]
        public void ArchivedReservationsAreBounded()
        {
            pipeline.Dispose();
            pipeline = null;
            strategicPlanner.Dispose();
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount,
                maxArchivedReservations: 4);
            for (int i = 0; i < 7; i++)
            {
                StrategicIntentSubmission submission = strategicPlanner.SubmitIntent(
                    new StrategicIntent(i + 1, 0,
                        StrategicObjectiveType.DefensivePreparation, 0),
                    isEmergency: false, isPlayerOverride: true);
                strategicPlanner.CompleteMilestoneAndAdvance(submission.Plan.StrategicPlanId);
                strategicPlanner.CancelPlan(submission.Plan.StrategicPlanId);
            }

            Assert.That(strategicPlanner.ArchivedReservations, Has.Count.EqualTo(4));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
        }

        private void ReplacePipeline(IStrategicEvaluator evaluator)
        {
            pipeline.Dispose();
            pipeline = new StrategicPipeline(sim, goalManager, strategicPlanner,
                evaluator: evaluator);
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

        private UnitData Worker(int tileX, int tileZ)
        {
            UnitData unit = sim.UnitRegistry.CreateUnit(0,
                sim.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private ResourceNodeData Resource(ResourceType type, int tileX, int tileZ)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(type,
                sim.MapData.TileToWorldFixed(tileX, tileZ), 10000);
            sim.FogOfWar.SetVisible(0, node.TileX, node.TileZ);
            return node;
        }

        private sealed class RecordingEvaluator : IStrategicEvaluator
        {
            public StrategicContext LastContext { get; private set; }

            public IReadOnlyList<StrategicRecommendation> Evaluate(StrategicContext context)
            {
                LastContext = context;
                return Array.Empty<StrategicRecommendation>();
            }
        }

        private sealed class FixedEvaluator : IStrategicEvaluator
        {
            private readonly StrategicObjectiveType objective;

            public FixedEvaluator(StrategicObjectiveType objective)
            {
                this.objective = objective;
            }

            public IReadOnlyList<StrategicRecommendation> Evaluate(StrategicContext context)
            {
                return new[]
                {
                    new StrategicRecommendation(99, context.PlayerId, objective, 100,
                        "Fixed test recommendation.", context.SnapshotTick, 100)
                };
            }
        }
    }
}
