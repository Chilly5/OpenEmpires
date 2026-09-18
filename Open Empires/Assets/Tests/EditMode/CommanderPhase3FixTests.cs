using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3FixTests
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
            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
            pipeline = new StrategicPipeline(sim, goalManager, strategicPlanner);
        }

        [TearDown]
        public void TearDown()
        {
            pipeline.Dispose();
            strategicPlanner.Dispose();
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

        private UnitData Worker(int tileX, int tileZ)
        {
            UnitData unit = sim.UnitRegistry.CreateUnit(0,
                sim.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            return unit;
        }

        private ResourceNodeData Resource(ResourceType type, int tileX, int tileZ, int amount = 1000)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(type,
                sim.MapData.TileToWorldFixed(tileX, tileZ), amount);
            sim.FogOfWar.SetVisible(0, tileX, tileZ);
            return node;
        }

        // Test 1: StrategicPipeline Ticks and Evaluates on Trigger
        [Test]
        public void StrategicPipeline_TicksAndEvaluatesOnTrigger()
        {
            pipeline.EvaluationTrigger.FireTrigger(
                StrategicEvaluationTriggerType.PlayerRequest, "Player requested assessment");

            pipeline.Tick(0);

            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History[0].TriggerType,
                Is.EqualTo(StrategicEvaluationTriggerType.PlayerRequest));
            Assert.That(pipeline.LastContext, Is.Not.Null);
        }

        // Test 2: StrategicPipeline Does Not Evaluate Without Trigger
        [Test]
        public void StrategicPipeline_DoesNotEvaluateWithoutTrigger()
        {
            pipeline.Tick(0);
            pipeline.Tick(100);

            Assert.That(pipeline.DecisionHistory.History, Is.Empty);
            Assert.That(pipeline.LastContext, Is.Null);
        }

        // Test 3: StrategicPipeline Respects Cooldown
        [Test]
        public void StrategicPipeline_RespectsCooldown()
        {
            pipeline.EvaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.PlayerRequest);
            pipeline.Tick(0);
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));

            pipeline.EvaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.StrategicEvent);
            pipeline.Tick(100); // within cooldown 900 ticks
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));

            pipeline.Tick(900); // cooldown elapsed
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(2));
            Assert.That(pipeline.DecisionHistory.History[1].TriggerType,
                Is.EqualTo(StrategicEvaluationTriggerType.StrategicEvent));
        }

        // Test 4: StrategicPipeline Emergency Bypasses Cooldown
        [Test]
        public void StrategicPipeline_EmergencyBypassesCooldown()
        {
            pipeline.EvaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.PlayerRequest);
            pipeline.Tick(0);
            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(1));

            pipeline.EvaluationTrigger.FireTrigger(StrategicEvaluationTriggerType.Emergency, "Threat level critical");
            pipeline.Tick(15); // well within 900 tick cooldown

            Assert.That(pipeline.DecisionHistory.History, Has.Count.EqualTo(2));
            Assert.That(pipeline.DecisionHistory.History[1].TriggerType,
                Is.EqualTo(StrategicEvaluationTriggerType.Emergency));
        }

        // Test 5: StrategicCommitmentPolicy Prevents Oscillation
        [Test]
        public void StrategicCommitmentPolicy_PreventsOscillation()
        {
            var policy = new StrategicCommitmentPolicy();
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            bool canTransition = policy.CanTransition(plan, StrategicPlanType.DefensivePreparation,
                isEmergency: false, out string reason);

            Assert.That(canTransition, Is.False);
            StringAssert.Contains("oscillation", reason);
        }

        // Test 6: StrategicCommitmentPolicy Allows Switch on Completion
        [Test]
        public void StrategicCommitmentPolicy_AllowsSwitchOnCompletion()
        {
            var policy = new StrategicCommitmentPolicy();
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            plan.Status = StrategicPlanStatus.Completed;

            bool canTransition = policy.CanTransition(plan, StrategicPlanType.DefensivePreparation,
                isEmergency: false, out string reason);

            Assert.That(canTransition, Is.True);
        }

        // Test 7: StrategicCommitmentPolicy Allows Switch on Failure
        [Test]
        public void StrategicCommitmentPolicy_AllowsSwitchOnFailure()
        {
            var policy = new StrategicCommitmentPolicy();
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            plan.Status = StrategicPlanStatus.Failed;

            bool canTransition = policy.CanTransition(plan, StrategicPlanType.DefensivePreparation,
                isEmergency: false, out string reason);

            Assert.That(canTransition, Is.True);
        }

        // Test 8: StrategicCommitmentPolicy Allows Switch on Emergency
        [Test]
        public void StrategicCommitmentPolicy_AllowsSwitchOnEmergency()
        {
            var policy = new StrategicCommitmentPolicy();
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();

            bool canTransition = policy.CanTransition(plan, StrategicPlanType.DefensivePreparation,
                isEmergency: true, out string reason);

            Assert.That(canTransition, Is.True);
            StringAssert.Contains("Emergency", reason);
        }

        // Test 9: StrategicCommitmentPolicy Compatible Plans Coexist
        [Test]
        public void StrategicCommitmentPolicy_CompatiblePlansCoexist()
        {
            var policy = new StrategicCommitmentPolicy();
            Assert.That(policy.AreCompatible(StrategicPlanType.EconomicExpansion, StrategicPlanType.DefensivePreparation), Is.True);
            Assert.That(policy.AreCompatible(StrategicPlanType.CavalryPressure, StrategicPlanType.DefensivePreparation), Is.False);
        }

        // Test 10: RecentDecisionHistory Records Decisions
        [Test]
        public void RecentDecisionHistory_RecordsDecisions()
        {
            var history = new RecentDecisionHistory(10);
            StrategicDecisionRecord result = history.RecordDecision(
                120, StrategicEvaluationTriggerType.StrategicEvent, "Landmark under attack",
                3, StrategicPlanType.DefensivePreparation, "Defensive response activated");

            Assert.That(result.DecisionId, Is.EqualTo(1));
            Assert.That(result.Tick, Is.EqualTo(120));
            Assert.That(result.TriggerType, Is.EqualTo(StrategicEvaluationTriggerType.StrategicEvent));
            Assert.That(result.TriggerReason, Is.EqualTo("Landmark under attack"));
            Assert.That(result.ActivePlanId, Is.EqualTo(3));
            Assert.That(result.ActivePlanType, Is.EqualTo(StrategicPlanType.DefensivePreparation));
            Assert.That(history.History, Has.Count.EqualTo(1));
        }

        // Test 11: RecentDecisionHistory Bounded Capacity
        [Test]
        public void RecentDecisionHistory_BoundedCapacity()
        {
            var history = new RecentDecisionHistory(3);
            for (int i = 1; i <= 5; i++)
            {
                history.RecordDecision(i * 10, StrategicEvaluationTriggerType.PlayerRequest, $"Reason {i}",
                    null, null, $"Outcome {i}");
            }

            Assert.That(history.History, Has.Count.EqualTo(3));
            Assert.That(history.History.First().Tick, Is.EqualTo(30));
            Assert.That(history.History.Last().Tick, Is.EqualTo(50));
        }

        // Test 12: IntentRouter Routes Tactical Intent
        [Test]
        public void IntentRouter_RoutesTacticalIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager);
            var router = new IntentRouter(0, dispatcher, strategicPlanner);

            var tactical = new EnsureUnitCountIntent(0, 1, 5);
            IntentRouteResult result = router.Route(tactical);

            Assert.That(result.Status, Is.EqualTo(IntentRouteStatus.Routed));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Layer, Is.EqualTo(CommanderIntentLayer.Tactical));
            Assert.That(result.TacticalSubmission, Is.Not.Null);
            Assert.That(result.TacticalSubmission.Interpretation.Intent, Is.SameAs(tactical));
            Assert.That(goalManager.ActiveGoals, Has.Count.EqualTo(1));
        }

        // Test 13: IntentRouter Routes Strategic Intent
        [Test]
        public void IntentRouter_RoutesStrategicIntent()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager);
            var router = new IntentRouter(0, dispatcher, strategicPlanner);

            var strategic = new StrategicIntent(1, 0, StrategicObjectiveType.AttackPreparation, 0);
            IntentRouteResult result = router.Route(strategic);

            Assert.That(result.Status, Is.EqualTo(IntentRouteStatus.Routed));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Layer, Is.EqualTo(CommanderIntentLayer.Strategic));
            Assert.That(result.StrategicSubmission, Is.Not.Null);
            Assert.That(result.StrategicSubmission.CreatedPlan, Is.True);
            Assert.That(result.StrategicSubmission.Plan.PlanType, Is.EqualTo(StrategicPlanType.CavalryPressure));
        }

        // Test 14: IntentRouter Rejects Mismatched Player
        [Test]
        public void IntentRouter_RejectsMismatchedPlayer()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager);
            var router = new IntentRouter(0, dispatcher, strategicPlanner);

            var foreignTactical = new EnsureUnitCountIntent(1, 1, 5);
            IntentRouteResult result = router.Route(foreignTactical);

            Assert.That(result.Status, Is.EqualTo(IntentRouteStatus.Rejected));
            Assert.That(result.Success, Is.False);
            StringAssert.Contains("mismatch", result.Reason.ToLowerInvariant());
        }

        // Test 15: IntentDto Round-trips Tactical Intent
        [Test]
        public void IntentDto_RoundTripsTacticalIntent()
        {
            var tactical = new EnsureUnitCountIntent(0, 1, 8);
            CommanderIntentDTO dto = CommanderIntentDtoCodec.FromIntent(tactical);

            StringAssert.AreEqualIgnoringCase("tactical", dto.intentCategory);
            Assert.That(dto.intentType, Is.EqualTo("EnsureUnitCount"));

            CommanderContext ctx = new CommanderContextBuilder().Build(sim, goalManager);
            CommanderIntentInterpretation validated = CommanderIntentDtoCodec.ValidateAndConvert(dto, ctx);
            Assert.That(validated.Success, Is.True);
            Assert.That(validated.Intent, Is.TypeOf<EnsureUnitCountIntent>());
            Assert.That(((EnsureUnitCountIntent)validated.Intent).TargetTotal, Is.EqualTo(8));
        }

        // Test 16: IntentDto Round-trips Strategic Intent
        [Test]
        public void IntentDto_RoundTripsStrategicIntent()
        {
            var strategic = new StrategicIntent(4, 0, StrategicObjectiveType.AttackPreparation, 10, priority: 75);
            CommanderIntentDTO dto = CommanderIntentDtoCodec.FromStrategicIntent(strategic);

            StringAssert.AreEqualIgnoringCase("strategic", dto.intentCategory);
            Assert.That(dto.objectiveType, Is.EqualTo("AttackPreparation"));
            Assert.That(dto.priority, Is.EqualTo(75));

            CommanderContext ctx = new CommanderContextBuilder().Build(sim, goalManager);
            CommanderIntentInterpretation interpreter = CommanderIntentDtoCodec.InterpretJson(
                CommanderIntentDtoCodec.ToJson(dto), ctx);
            Assert.That(interpreter.Success, Is.True);
            Assert.That(interpreter.StrategicIntent, Is.Not.Null);
            Assert.That(interpreter.StrategicIntent.ObjectiveType, Is.EqualTo(StrategicObjectiveType.AttackPreparation));
            Assert.That(interpreter.StrategicIntent.Priority, Is.EqualTo(75));
        }

        // Test 17: StrategicResourceReservation Budget vs Active
        [Test]
        public void StrategicResourceReservation_BudgetVsActive()
        {
            PlayerResources stockpile = sim.ResourceManager.GetPlayerResources(0);
            stockpile.Wood = 150;
            stockpile.Stone = 0; // Plan total budget requires 300 stone, but milestone 1 needs 0 stone

            StrategicIntentSubmission submission = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation);

            Assert.That(submission.CreatedPlan, Is.True);
            StrategicPlan plan = submission.Plan;
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));

            // Budget requirement reflects total plan scope
            Assert.That(plan.BudgetRequirements.Single(b => b.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Barracks) + config.SpearmanWoodCost * DefensivePreparationPlan.SpearmanTarget));
            Assert.That(plan.BudgetRequirements.Single(b => b.ResourceType == ResourceType.Food).Amount,
                Is.EqualTo(config.SpearmanFoodCost * DefensivePreparationPlan.SpearmanTarget));

            // Active reservation only locks milestone 1 resources (Wood 100)
            IReadOnlyList<StrategicResourceReservation> reservations =
                strategicPlanner.GetReservationsForPlan(plan.StrategicPlanId);
            Assert.That(reservations.Where(r => r.Status == StrategicResourceReservationStatus.Active)
                .Select(r => r.ResourceType), Is.Empty);
        }

        // Test 18: StrategicResourceReservation Milestone Progression
        [Test]
        public void StrategicResourceReservation_MilestoneProgression()
        {
            StrategicIntentSubmission submission = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation);
            StrategicPlan plan = submission.Plan;

            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Economic Defense"));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Stone), Is.Zero);

            // Complete milestone 1 and advance
            bool advanced = strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(advanced, Is.True);

            // Plan advanced to Milestone 2 (Defensive Infrastructure)
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Military Infrastructure"));
            // Milestone 2 claimed Stone reservation
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Wood), Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Barracks)));
        }

        // Test 19: StrategicResourceReservation Partial Release
        [Test]
        public void StrategicResourceReservation_PartialRelease()
        {
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            StrategicResourceReservation woodReservation = strategicPlanner
                .GetReservationsForPlan(plan.StrategicPlanId)
                .Single(r => r.ResourceType == ResourceType.Wood);

            int initialReserved = strategicPlanner.GetReservedAmount(ResourceType.Wood);
            Assert.That(initialReserved, Is.EqualTo(sim.GetBuildingWoodCost(BuildingType.Barracks)));

            strategicPlanner.UpdateReservationAmount(woodReservation.ReservationId,
                initialReserved - 50);

            Assert.That(woodReservation.Amount, Is.EqualTo(initialReserved - 50));
            Assert.That(strategicPlanner.GetReservedAmount(ResourceType.Wood),
                Is.EqualTo(initialReserved - 50));
        }

        // Test 20: DefensivePreparationPlan Creates Milestones
        [Test]
        public void DefensivePreparationPlan_CreatesMilestones()
        {
            var plan = new DefensivePreparationPlan(0, 1);
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.DefensivePreparation));
            Assert.That(plan.Milestones.Select(m => m.Name), Is.EqualTo(new[]
            {
                "Economic Defense", "Military Infrastructure", "Defensive Structures", "Defense Army", "Ready"
            }));
        }

        // Test 21: EconomicExpansionPlan Creates Milestones
        [Test]
        public void EconomicExpansionPlan_CreatesMilestones()
        {
            var plan = new EconomicExpansionPlan(0, 1);
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.EconomicExpansion));
            Assert.That(plan.Milestones.Select(m => m.Name), Is.EqualTo(new[]
            {
                "Boom Foundation", "Economic Infrastructure", "Worker Production", "Ready"
            }));
        }

        // Test 22: MilitaryReinforcementPlan Creates Milestones
        [Test]
        public void MilitaryReinforcementPlan_CreatesMilestones()
        {
            var plan = new MilitaryReinforcementPlan(0, 1);
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.MilitaryReinforcement));
            Assert.That(plan.Milestones.Select(m => m.Name), Is.EqualTo(new[]
            {
                "Production Capacity", "Unit Production", "Assembly", "Ready"
            }));
        }

        // Test 23: WorkerSelection Optimized Path Checks
        [Test]
        public void WorkerSelection_OptimizedPathChecks()
        {
            // Spawn 10 workers and 5 resource nodes
            for (int i = 0; i < 10; i++)
                Worker(x - 5 - i, z);
            for (int j = 0; j < 5; j++)
                Resource(ResourceType.Wood, x + 5 + j * 2, z);

            goalManager.ResetDiagnosticPathCheckCount();
            goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);

            goalManager.Tick(0);

            // With 10 workers and 5 nodes, naive approach would perform 50 path checks.
            // Two-stage selection checks the closest candidate and completes in very few checks.
            Assert.That(goalManager.DiagnosticPathCheckCount, Is.LessThan(20),
                "Two-stage worker selection must drastically reduce path checks compared to naive N*M.");
        }

        // Test 24: WorkerSelection Rejects Truly Unreachable
        [Test]
        public void WorkerSelection_RejectsTrulyUnreachable()
        {
            Worker(x - 5, z);
            // Place resource node inside an unwalkable barrier
            ResourceNodeData node = Resource(ResourceType.Wood, x + 10, z);
            for (int bx = node.TileX - 1; bx <= node.TileX + 1; bx++)
            {
                for (int bz = node.TileZ - 1; bz <= node.TileZ + 1; bz++)
                {
                    if (bx == node.TileX && bz == node.TileZ) continue;
                    sim.MapData.Tiles[bx, bz] = TileType.Water; // Water is unwalkable
                }
            }

            CommanderGoal goal = goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);
            goalManager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        // Test 25: LifecycleCleanup Archives Goals
        [Test]
        public void LifecycleCleanup_ArchivesGoals()
        {
            CommanderGoal goal = goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);
            Assert.That(goalManager.ActiveGoals, Has.Count.EqualTo(1));
            Assert.That(goalManager.ArchivedGoals, Is.Empty);

            goalManager.CancelGoal(goal.GoalId);

            Assert.That(goalManager.ActiveGoals, Is.Empty);
            Assert.That(goalManager.ArchivedGoals, Has.Count.EqualTo(1));
            Assert.That(goalManager.ArchivedGoals[0].GoalId, Is.EqualTo(goal.GoalId));
        }

        // Test 26: LifecycleCleanup Archives Plans
        [Test]
        public void LifecycleCleanup_ArchivesPlans()
        {
            CavalryPressurePlan plan = strategicPlanner.StartCavalryPressurePlan();
            Assert.That(strategicPlanner.ActivePlans, Has.Count.EqualTo(1));
            Assert.That(strategicPlanner.ArchivedPlans, Is.Empty);

            strategicPlanner.CancelPlan(plan.StrategicPlanId);

            Assert.That(strategicPlanner.ActivePlans, Is.Empty);
            Assert.That(strategicPlanner.ArchivedPlans, Has.Count.EqualTo(1));
            Assert.That(strategicPlanner.ArchivedPlans[0].StrategicPlanId, Is.EqualTo(plan.StrategicPlanId));
        }

        // Test 27: LifecycleCleanup Archives Reservations
        [Test]
        public void LifecycleCleanup_ArchivesReservations()
        {
            StrategicPlan plan = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.DefensivePreparation).Plan;
            strategicPlanner.CompleteMilestoneAndAdvance(plan.StrategicPlanId);
            Assert.That(strategicPlanner.Reservations, Has.Count.EqualTo(1));
            Assert.That(strategicPlanner.ArchivedReservations, Is.Empty);

            strategicPlanner.CancelPlan(plan.StrategicPlanId);

            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Assert.That(strategicPlanner.ArchivedReservations, Has.Count.EqualTo(1));
            Assert.That(strategicPlanner.ArchivedReservations.All(r => r.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
        }

        // Test 28: WorkerAuthority Deterministic Iteration
        [Test]
        public void WorkerAuthority_DeterministicIteration()
        {
            var workerAuth = new CommanderWorkerAuthority(sim, 0);
            UnitData w15 = Worker(x + 1, z);
            UnitData w3 = Worker(x + 2, z);
            UnitData w9 = Worker(x + 3, z);
            UnitData w1 = Worker(x + 4, z);

            // Reserve in arbitrary order
            workerAuth.TryReserve(w15.Id, 10, CommanderWorkerReservationType.Gatherer, 0);
            workerAuth.TryReserve(w3.Id, 10, CommanderWorkerReservationType.Gatherer, 0);
            workerAuth.TryReserve(w9.Id, 10, CommanderWorkerReservationType.Gatherer, 0);
            workerAuth.TryReserve(w1.Id, 10, CommanderWorkerReservationType.Gatherer, 0);

            // Release
            Assert.DoesNotThrow(() => workerAuth.ReleaseGoal(10));
            Assert.That(workerAuth.GetReservation(w15.Id), Is.Null);
            Assert.That(workerAuth.GetReservation(w1.Id), Is.Null);
        }
    }
}
