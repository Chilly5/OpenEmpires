using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C4")]
    public sealed class CommanderPhase4C4Tests
    {
        private const int Ranged = 4;
        private const int Turtle = 5;
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [TestCase("prepare ranged reinforcements", Ranged)]
        [TestCase("prepare fortified defenses", Turtle)]
        public void WholeFormAdmission_UsesDistinctStrictObjectives(string phrase, int objective)
        {
            Assert.That(new CommanderIntentRouter().Classify("  " + phrase + ".  "),
                Is.EqualTo(CommanderTextRoute.Strategic));
            var request = new StrategicAIRequest(phrase, Context(), planner.IntentIds);
            var interpreted = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, default).Result;
            Assert.That(interpreted.Success, Is.True, interpreted.ExplanationText);
            Assert.That((int)interpreted.Intent.ObjectiveType, Is.EqualTo(objective));
            Assert.That(interpreted.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(interpreted.Intent.Parameters, Is.Empty);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
        }

        [TestCase("RangedReinforcement", Ranged)]
        [TestCase("DefensiveTurtle", Turtle)]
        public void ExactJsonName_AcceptsOnlyEmptyParameters(string name, int objective)
        {
            string prefix = "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"" + name + "\"";
            var result = StrategicAIJson.Parse(prefix + ",\"parameters\":{}}",
                new StrategicAIRequest("request", Context(), planner.IntentIds));
            Assert.That(result.Success, Is.True, result.ExplanationText);
            Assert.That((int)result.Intent.ObjectiveType, Is.EqualTo(objective));
            Assert.That(StrategicAIJson.Parse(prefix + ",\"parameters\":{\"targetCount\":\"1\"}}",
                new StrategicAIRequest("request", Context(), planner.IntentIds)).Success, Is.False);
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void ObjectiveHasMeaningfulFeasibilityAndPlan(int objective)
        {
            RichWorld();
            var kind = (StrategicObjectiveType)objective;
            var quote = Context().Feasibility.Single(value => value.Objective == kind);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            int food = quote.Costs.Single(value => value.ResourceType == ResourceType.Food).Amount;
            int wood = quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount;
            Assert.That(food, Is.EqualTo(objective == Ranged ? 300 : 720));
            Assert.That(wood, Is.EqualTo(objective == Ranged ? 650 : 1460));
            var intent = planner.CreateIntent(kind);
            var validation = new StrategicIntentValidator().Validate(intent, 0,
                StrategicPlanRegistry.CreateDefault());
            Assert.That(validation.IsValid, Is.True, validation.Reason);
            var submission = planner.SubmitIntent(intent);
            Assert.That(submission.CreatedPlan, Is.True, submission.Reason);
            Assert.That((int)submission.Plan.PlanType, Is.EqualTo(objective));
            Assert.That(submission.Plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Food).Amount,
                Is.EqualTo(food));
            Assert.That(submission.Plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(wood));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void DirectPlayerIntent_UsesNormalPriorityAndOverridesExistingPlan(int objective)
        {
            RichWorld();
            var previous = planner.SubmitIntent(StrategicObjectiveType.EconomicExpansion).Plan;
            Assert.That(previous, Is.Not.Null);
            Assert.That(previous.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));

            using var pipeline = new StrategicPipeline(simulation, goals, planner);
            var intent = planner.CreateIntent((StrategicObjectiveType)objective);
            var record = pipeline.EvaluatePlayerIntentNow(intent);

            Assert.That(record.Decision.HasSelection, Is.True);
            Assert.That(record.Decision.SelectedIntent, Is.SameAs(intent));
            Assert.That(record.Decision.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Normal));
            Assert.That(record.TransitionAllowed, Is.True);
            Assert.That(record.Submission, Is.Not.Null);
            Assert.That(record.Submission.CreatedPlan, Is.True, record.Submission.Reason);
            Assert.That(record.Submission.Plan.PlanType,
                Is.EqualTo((StrategicPlanType)objective));
            Assert.That(record.Submission.Plan.Authority,
                Is.EqualTo(StrategicPlanAuthority.PlayerOverride));
            Assert.That(previous.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.ActivePlans, Does.Contain(record.Submission.Plan));
        }

        [Test]
        public void UnknownObjective_DoesNotQuoteZeroCostCapability()
        {
            RichWorld();
            Assert.That(new StrategicIntentValidator().Validate(
                new StrategicIntent(91, 0, (StrategicObjectiveType)999, 0), 0,
                StrategicPlanRegistry.CreateDefault()).IsValid, Is.False);
            Assert.That(Context().Feasibility.Any(value => (int)value.Objective == 999), Is.False);
        }

        [Test]
        public void TurtleFundedTowerFoundation_IsChargedOnlyOnceInQuoteAndPlan()
        {
            RichWorld();
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.Tower, x - 20, z + 12, true);
            var quote = Context().Feasibility.Single(value => value.Objective
                == (StrategicObjectiveType)Turtle);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(1160));
            var plan = planner.SubmitIntent((StrategicObjectiveType)Turtle).Plan;
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(1160), "The funded tower foundation must not be budgeted again.");
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void Templates_HaveOrderedFiniteMilestonesAndExactRequests(int objective)
        {
            var intent = new StrategicIntent(7, 0, (StrategicObjectiveType)objective, 0);
            var plan = StrategicPlanRegistry.CreateDefault().CreatePlan(intent);
            Assert.That(plan.Milestones.Select(value => value.Name), Is.EqualTo(objective == Ranged
                ? new[] { "Economy", "Production", "Force", "Ready" }
                : new[] { "Economy", "Production", "Fortifications", "Force", "Ready" }));
            Assert.That(plan.Milestones.Select(value => value.OrderIndex),
                Is.EqualTo(Enumerable.Range(0, plan.Milestones.Count)));
            Assert.That(plan.Milestones.Select(value => value.MilestoneId),
                Is.EqualTo(Enumerable.Range(1, plan.Milestones.Count)));
            Assert.That(plan.Milestones[0].TacticalGoals
                .OfType<StrategicResourceAllocationGoalRequest>()
                .Select(value => new[] { (int)value.ResourceType, value.WorkerTarget }),
                Is.EqualTo(new[] { new[] { (int)ResourceType.Food, 8 },
                    new[] { (int)ResourceType.Wood, 8 } }));
            var structures = plan.Milestones[1].TacticalGoals
                .OfType<StrategicBuildStructureGoalRequest>().ToArray();
            Assert.That(structures.Select(value => value.StructureType), Is.EqualTo(objective == Ranged
                ? new[] { BuildingType.ArcheryRange }
                : new[] { BuildingType.Barracks, BuildingType.ArcheryRange }));
            Assert.That(structures.All(value => value.EnsureExisting && value.Count == 1), Is.True);
            if (objective == Turtle)
            {
                var tower = plan.Milestones[2].TacticalGoals
                    .OfType<StrategicBuildStructureGoalRequest>().Single();
                Assert.That(tower.StructureType, Is.EqualTo(BuildingType.Tower));
                Assert.That(tower.Count, Is.EqualTo(2));
                Assert.That(tower.SkipIfAgeUnavailable, Is.False);
            }
            var force = plan.Milestones[objective == Ranged ? 2 : 3].TacticalGoals
                .OfType<StrategicEnsureUnitCountGoalRequest>().ToArray();
            Assert.That(force.Select(value => new[] { value.UnitType, value.TargetTotal }),
                Is.EqualTo(objective == Ranged
                    ? new[] { new[] { CommanderIntentCatalog.ArcherUnitType, 10 } }
                    : new[] { new[] { CommanderIntentCatalog.SpearmanUnitType, 8 },
                        new[] { CommanderIntentCatalog.ArcherUnitType, 8 } }));
            Assert.That(plan.Milestones.Last().TacticalGoals, Is.Empty);
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void OwnedAndQueuedUnits_AndCompletedProducersReduceCanonicalQuote(int objective)
        {
            RichWorld();
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            if (objective == Turtle)
            {
                var barracks = simulation.CreateBuilding(0, BuildingType.Barracks, x - 8, z, false);
                for (int i = 0; i < 3; i++) AddUnit(CommanderIntentCatalog.SpearmanUnitType);
                barracks.TrainingQueue.Add(CommanderIntentCatalog.SpearmanUnitType);
            }
            var range = simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 14, z, false);
            for (int i = 0; i < (objective == Ranged ? 3 : 2); i++)
                AddUnit(CommanderIntentCatalog.ArcherUnitType);
            range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            var quote = Context().Feasibility.Single(value => (int)value.Objective == objective);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Food).Amount,
                Is.EqualTo(objective == Ranged ? 150 : 360));
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(objective == Ranged ? 250 : 880));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void NoWorkersOrAgeOrResources_CannotApprove(int objective)
        {
            RichWorld();
            var kind = (StrategicObjectiveType)objective;
            SetAge(1);
            var underage = Context().Feasibility.Single(value => value.Objective == kind);
            Assert.That(underage.Capable, Is.False);
            Assert.That(underage.RejectionReason, Does.Contain("current age"));
            SetAge(3);
            foreach (var unit in simulation.UnitRegistry.GetAllUnits())
                if (unit.PlayerId == 0 && unit.IsVillager) unit.CurrentHealth = 0;
            var noWorkers = Context().Feasibility.Single(value => value.Objective == kind);
            Assert.That(noWorkers.Capable, Is.False);
            Assert.That(noWorkers.RejectionReason, Does.Contain("worker"));
            AddUnit(0);
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = 0;
            var intent = AI(kind);
            var approval = new StrategicApprovalLayer().Evaluate(Context(), intent, intent.Source);
            Assert.That(approval.Approved, Is.False);
            Assert.That(approval.Reason, Does.Contain("Insufficient available"));
            Assert.That(planner.Plans, Is.Empty);
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void NormalRecommendation_IsApprovedButCannotDisplaceEmergency(int objective)
        {
            RichWorld();
            var kind = (StrategicObjectiveType)objective;
            var intent = AI(kind);
            var accepted = new StrategicApprovalLayer().Evaluate(Context(), intent, intent.Source);
            Assert.That(accepted.Approved, Is.True, accepted.Reason);
            Assert.That(accepted.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));
            var emergency = planner.SubmitIntent(planner.CreateIntent(
                StrategicObjectiveType.DefensivePreparation), true, false).Plan;
            Assert.That(emergency, Is.Not.Null);
            var blocked = new StrategicApprovalLayer().Evaluate(Context(), intent, intent.Source);
            Assert.That(blocked.Approved, Is.False);
            Assert.That(blocked.Reason, Does.Contain("Emergency"));
            Assert.That(emergency.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void NewTemplatesRejectParametersAndMixedPhrases(int objective)
        {
            var kind = (StrategicObjectiveType)objective;
            var withParameter = new StrategicIntent(91, 0, kind, 0,
                new Dictionary<string, string> { ["targetCount"] = "1" });
            var validation = new StrategicIntentValidator().Validate(withParameter, 0,
                StrategicPlanRegistry.CreateDefault());
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Error, Is.EqualTo(StrategicIntentValidationError.UnsupportedParameter));
            string phrase = objective == Ranged
                ? "prepare ranged reinforcements" : "prepare fortified defenses";
            Assert.That(new CommanderIntentRouter().Classify(phrase + " and attack now"),
                Is.EqualTo(CommanderTextRoute.Rejected));
            Assert.That(new CommanderIntentRouter().Classify(phrase + "!"),
                Is.EqualTo(CommanderTextRoute.Rejected));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void SaturatedRequiredProducerQueue_IsNotMisquotedAsCapable(int objective)
        {
            RichWorld();
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            var range = simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 14, z, false);
            for (int i = 0; i < 3; i++) range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            if (objective == Turtle)
                simulation.CreateBuilding(0, BuildingType.Barracks, x - 8, z, false);
            var quote = Context().Feasibility.Single(value => (int)value.Objective == objective);
            Assert.That(quote.Capable, Is.False);
            Assert.That(quote.RejectionReason, Does.Contain("production capacity"));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void ApprovalUsesAvailableFoodAfterReservations(int objective)
        {
            RichWorld();
            var kind = (StrategicObjectiveType)objective;
            var trusted = Context();
            var intent = AI(kind);
            var economy = trusted.Economy.Select(value => value.ResourceType == ResourceType.Food
                ? new StrategicResourceState(ResourceType.Food, value.CurrentAmount,
                    value.CurrentAmount - 100)
                : value).ToList();
            var afterReservation = new StrategicContext(trusted.PlayerId, trusted.SnapshotTick,
                economy, trusted.Population, trusted.Military.ToList(), trusted.Production.ToList(),
                trusted.ActivePlans.ToList(), trusted.VisibleResources.ToList(),
                trusted.WorkerAllocation.ToList(), trusted.TotalWorkers, trusted.Defense,
                trusted.Threat, trusted.Feasibility, trusted.Insights);
            Assert.That(afterReservation.Economy.Single(value => value.ResourceType == ResourceType.Food)
                .AvailableAmount, Is.EqualTo(100));
            var approval = new StrategicApprovalLayer().Evaluate(afterReservation, intent, intent.Source);
            Assert.That(approval.Approved, Is.False);
            Assert.That(approval.Reason, Does.Contain("Insufficient available Food"));
            Assert.That(planner.Plans, Is.Empty);
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void MaximumPopulationBoundaryRejectsRequiredArmy(int objective)
        {
            RichWorld();
            for (int i = 0; i < 185; i++) AddUnit(CommanderIntentCatalog.KnightUnitType);
            Assert.That(Context().Population.CurrentPopulation + (objective == Ranged ? 10 : 16),
                Is.GreaterThan(config.MaxPopulation));
            var quote = Context().Feasibility.Single(value => (int)value.Objective == objective);
            Assert.That(quote.Capable, Is.False);
            Assert.That(quote.RejectionReason, Does.Contain("maximum population"));
        }

        [Test]
        public void AtCurrentPopulationCap_RangedQuoteIncludesOneHouseAndIsCapable()
        {
            RichWorld(houses: 0, workers: 10);
            var context = Context();
            Assert.That(context.Population.CurrentPopulation, Is.EqualTo(10));
            Assert.That(context.Population.PopulationCap, Is.EqualTo(10));
            Assert.That(context.Population.MaximumPopulation, Is.EqualTo(200));

            var quote = context.Feasibility.Single(value => (int)value.Objective == Ranged);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Food).Amount,
                Is.EqualTo(300));
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(700), "Ten archers (500) + Archery Range (150) + one House (50).");
        }

        [Test]
        public void AtCurrentPopulationCap_FundedHouseFoundationIsNotChargedAgain()
        {
            RichWorld(houses: 0, workers: 10);
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z + 10, true);
            var context = Context();
            Assert.That(context.Population.CurrentPopulation, Is.EqualTo(context.Population.PopulationCap));
            Assert.That(context.Population.PopulationCap, Is.EqualTo(10));

            var quote = context.Feasibility.Single(value => (int)value.Objective == Ranged);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(650), "The funded House already provides the needed future capacity.");
        }

        [Test]
        public void AtCurrentPopulationCap_WithoutWorkersStillRejectsConstruction()
        {
            RichWorld(houses: 0, workers: 0);
            for (int i = 0; i < 10; i++) AddUnit(CommanderIntentCatalog.KnightUnitType);
            var context = Context();
            Assert.That(context.Population.CurrentPopulation, Is.EqualTo(context.Population.PopulationCap));
            Assert.That(context.Population.PopulationCap, Is.EqualTo(10));

            var quote = context.Feasibility.Single(value => (int)value.Objective == Ranged);
            Assert.That(quote.Capable, Is.False);
            Assert.That(quote.RejectionReason, Does.Contain("worker"));
        }

        [TestCase(Ranged)]
        [TestCase(Turtle)]
        public void FundedProducerFoundationsAreReusedInQuoteAndPlan(int objective)
        {
            RichWorld();
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 14, z, true);
            if (objective == Turtle)
                simulation.CreateBuilding(0, BuildingType.Barracks, x - 8, z, true);
            var kind = (StrategicObjectiveType)objective;
            var quote = Context().Feasibility.Single(value => value.Objective == kind);
            Assert.That(quote.Capable, Is.True, quote.RejectionReason);
            int expectedWood = objective == Ranged ? 500 : 1160;
            Assert.That(quote.Costs.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(expectedWood));
            var plan = planner.SubmitIntent(kind).Plan;
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                Is.EqualTo(expectedWood));
        }

        private StrategicIntent AI(StrategicObjectiveType kind)
        {
            var request = new StrategicAIRequest("request", Context(), planner.IntentIds);
            return StrategicAIJson.Parse("{\"intentCategory\":\"Strategic\",\"objectiveType\":\""
                + kind + "\",\"parameters\":{}}", request).Intent;
        }

        private void SetAge(int age) => ((int[])typeof(GameSimulation).GetField("playerAges",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(simulation))[0] = age;

        private void AddUnit(int type)
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 10, z), Fixed32.One,
                Fixed32.One, Fixed32.One);
            unit.UnitType = type;
            unit.IsVillager = type == 0;
            unit.CurrentHealth = unit.MaxHealth = 100;
            unit.State = UnitState.Idle;
        }

        private StrategicContext Context() => new StrategicContextBuilder().Build(
            new CommanderContextBuilder().Build(simulation, goals), planner);

        private int Resource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private void RichWorld(int houses = 4, int workers = 8)
        {
            ((int[])typeof(GameSimulation).GetField("playerAges",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(simulation))[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            for (int i = 0; i < houses; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 18 + i * 3, z + 10, false);
            for (int i = 0; i < workers; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 10 + i, z), Fixed32.One,
                    Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
        }
    }
}
