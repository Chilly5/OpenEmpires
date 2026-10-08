using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixQuantityTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private BuildingData barracks;
        private int x, z;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French });
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            barracks = sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false, true);
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown] public void TearDown()
        { manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        private UnitData Existing()
        {
            var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x - 4, z),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
            return unit;
        }

        private static CommanderSemanticResult Request(int count, int? consumer, bool newUnits = false)
        {
            string tail = consumer.HasValue ? ",{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":" + consumer
                + ",\"location\":\"PlayerBase\",\"dependsOn\":[1],\"resultFromNode\":1}" : "";
            return CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":" + count
                + (newUnits ? ",\"quantityMode\":\"New\"" : "") + "}" + tail + "]}");
        }

        private CommanderActionPlanCandidate Candidate(int count, int? consumer, bool newUnits = false)
        {
            var parsed = Request(count, consumer, newUnits);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            return manager.PrepareActionPlan(parsed, "Trusted quantity fixture", 0);
        }

        [TestCase(4, 0, 5, 5)][TestCase(3, 1, 5, 2)][TestCase(0, 5, 5, 5)]
        [TestCase(0, 0, 5, 4)][TestCase(0, 0, 5, 6)]
        public void TotalResultCardinalityMismatch_RejectsWholeGraphBeforeHouseOrProduction(int owned, int queued, int total, int exact)
        {
            for (int i = 0; i < owned; i++) Existing();
            for (int i = 0; i < queued; i++) barracks.EnqueueTraining(1, 100);
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(Request(total, exact), context,
                out var graph, out var reason), Is.False, "A total goal cannot promise an incompatible exact-new result.");
            Assert.That(graph, Is.Null); Assert.That(reason, Does.Contain("new"));
            Assert.That(manager.Goals, Is.Empty); Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void TotalResult_AccountsForUnrelatedQueuedUnitsWithoutClaimingThemAsNewResults()
        {
            Existing(); Existing(); Existing(); barracks.EnqueueTraining(1, 100);
            var candidate = Candidate(5, 1);
            var graph = manager.ApproveActionPlan(candidate, 0);
            Assert.That(graph, Is.Not.Null);
            var source = manager.SubmitSemanticGraph(graph).OfType<EnsureUnitCountGoal>().Single();
            Assert.That(source.RequiredNewProductionCount, Is.EqualTo(1));
            Assert.That(source.ResultUnitIds, Is.Empty);
        }

        [Test] public void ExplicitNew_PreservesFiveNewWithFourExistingAndHumanQueue()
        {
            for (int i = 0; i < 4; i++) Existing();
            barracks.EnqueueTraining(1, 100);
            var candidate = Candidate(5, 5, true);
            var graph = manager.ApproveActionPlan(candidate, 0);
            Assert.That(graph, Is.Not.Null);
            var source = manager.SubmitSemanticGraph(graph).OfType<EnsureUnitCountGoal>().Single();
            Assert.That(source.RequiredNewProductionCount, Is.EqualTo(5));
            Assert.That(source.BaselineUnitIds.Count, Is.EqualTo(4));
        }

        [TestCase(false)][TestCase(true)]
        public void MaterialTotalBaselineChange_RequiresNewPreviewBeforeApproval(bool linked)
        {
            var candidate = Candidate(3, linked ? 3 : (int?)null);
            Existing();
            Assert.That(manager.ApproveActionPlan(candidate, 0), Is.Null,
                "Changed new-production effects cannot use the old preview even without a result consumer.");
            Assert.That(manager.Goals, Is.Empty); Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(false)][TestCase(true)]
        public void MaterialTotalBaselineChangeAfterApproval_RejectsCommitAtomically(bool linked)
        {
            var graph = manager.ApproveActionPlan(Candidate(3, linked ? 3 : (int?)null), 0);
            Assert.That(graph, Is.Not.Null);
            Existing();
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(graph));
            Assert.That(manager.Goals, Is.Empty); Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void QueueMaturationWithSameExpectedNewCount_DoesNotInvalidateApproval()
        {
            Existing(); barracks.EnqueueTraining(1, 100);
            var candidate = Candidate(3, 1);
            // Read-only approval-state test: emulate an ordinary queue becoming owned.
            // This is not native runtime proof or a forced Commander completion.
            barracks.TrainingQueue.Clear(); Existing();
            var graph = manager.ApproveActionPlan(candidate, 0);
            Assert.That(graph, Is.Not.Null);
            Assert.That(manager.SubmitSemanticGraph(graph).OfType<EnsureUnitCountGoal>().Single().RequiredNewProductionCount, Is.EqualTo(1));
        }

        [Test] public void ResourceWaitAndExplicitNewBaselineGrowth_DoNotInvalidateUnchangedEffects()
        {
            var candidate = Candidate(2, 2, true);
            Existing(); sim.ResourceManager.GetPlayerResources(0).Food = 0;
            var graph = manager.ApproveActionPlan(candidate, 0);
            Assert.That(graph, Is.Not.Null);
            Assert.That(manager.SubmitSemanticGraph(graph).OfType<EnsureUnitCountGoal>().Single().RequiredNewProductionCount, Is.EqualTo(2));
        }

        [Test] public void NewMode_SurvivesDtoRoundTripWithoutBecomingTargetTotal()
        {
            var parsed = Request(2, 2, true);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[1], context, out var intent, out _), Is.True);
            var dto = CommanderIntentDtoCodec.FromIntent(intent);
            var interpreted = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.Serialize(dto), context);
            Assert.That(interpreted.Success, Is.True);
            Assert.That(((EnsureUnitCountIntent)interpreted.Intent).NewProductionCount, Is.EqualTo(2));
        }

        [Test] public void SingleKnownIntentNew_CarriesExactNewCountIntoOrdinaryRegistration()
        {
            for (int i = 0; i < 4; i++) Existing();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"}]}");
            var ticket = manager.BeginSemanticRequest("produce two new spearmen", 0, () => true);
            var scope = manager.PrepareKnownIntentScope(parsed, ticket, null, false);
            using var dispatcher = new CommanderIntentDispatcher(sim, manager);
            var intent = scope.Graph.Nodes[0].Intent;
            var submitted = manager.SubmitKnownScopedIntent(scope, intent, () => dispatcher.SubmitIntent(intent));
            var goal = (EnsureUnitCountGoal)submitted.Resolution.Goal;
            Assert.That(goal.RequiredNewProductionCount, Is.EqualTo(2));
            Assert.That(goal.HasResultConsumer, Is.True);
            Assert.That(goal.BaselineUnitIds.Count, Is.EqualTo(4));
        }

        [Test] public void SequentialTotalProduction_QuotesOnlyRemainingNewForItsDependentResult()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"dependsOn\":[0]},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerBase\",\"dependsOn\":[1],\"resultFromNode\":1}]}");
            var candidate = manager.PrepareActionPlan(parsed, "Ensure two, then three total, use that one new unit", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(candidate, 0));
            var second = (EnsureUnitCountGoal)goals[1];
            Assert.That(second.RequiredNewProductionCount, Is.EqualTo(1));
            Assert.That(second.ExpectedOtherUnitContribution, Is.EqualTo(2));
        }

        [Test] public void ParallelOverlappingTotalResultProducers_RejectUnfulfillableReceiptPromises()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerBase\",\"dependsOn\":[1],\"resultFromNode\":1}]}");
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed, new CommanderContextBuilder().Build(sim, manager), out _, out _), Is.False);
            Assert.That(manager.Goals, Is.Empty); Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(false, 1)][TestCase(true, 1)][TestCase(true, 2)]
        public void DelayedTrainingAcceptance_DoesNotDuplicateExactCountOrExceedQueueLimit(bool newUnits, int count)
        {
            sim.SetPlayerAge(0, 3);
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = 10000;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":" + count
                + (newUnits ? ",\"quantityMode\":\"New\"" : "")
                + ",\"constraints\":[{\"type\":\"MaximumQueue\",\"amount\":1}]},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":" + count
                + ",\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            var graph = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed, "Delayed ordinary training", 0), 0);
            var goal = (EnsureUnitCountGoal)manager.SubmitSemanticGraph(graph)[0];
            manager.Tick(0);
            var delayed = sim.CommandBuffer.FlushCommands().Single(command => command is TrainUnitCommand);
            Assert.That(sim.HasPendingTrainingOrigin(delayed), Is.True);
            Assert.That(goal.TrackedTrainingOrders, Is.Empty);
            // Mirrors planning before acceptance at the 15-tick input-delay horizon.
            manager.Tick(15);
            Assert.That(sim.CommandBuffer.FlushCommands().OfType<TrainUnitCommand>(), Is.Empty,
                "An in-flight order consumes both new-result and producer queue capacity.");
        }

        [Test] public void ParallelAlreadySatisfiedTotal_DoesNotRejectAnOtherwiseExactNewResult()
        {
            for (int i = 0; i < 4; i++) Existing();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":5},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerBase\",\"dependsOn\":[1],\"resultFromNode\":1}]}");
            var candidate = manager.PrepareActionPlan(parsed, "Already have two; reach five and use the one new result", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(candidate, 0));
            Assert.That(((EnsureUnitCountGoal)goals[1]).RequiredNewProductionCount, Is.EqualTo(1));
        }
    }
}
