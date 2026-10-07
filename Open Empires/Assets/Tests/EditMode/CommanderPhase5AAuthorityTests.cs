using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public class CommanderPhase5AAuthorityTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;

        [TearDown]
        public void TearDown()
        {
            pipeline?.Dispose();
            planner?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        private void Setup(int owner, int[] computerOwners)
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, computerOwners);
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var resources = sim.ResourceManager.GetPlayerResources(owner);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 10; tz <= z + 10; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(owner, tx, tz);
                }
            sim.CreateBuilding(owner, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            sim.CreateBuilding(owner, BuildingType.House, x + 18, z, false);
            sim.CreateBuilding(owner, BuildingType.House, x + 22, z, false);
            manager = new CommanderGoalManager(sim, owner);
            planner = new StrategicPlanner(manager, type => type == ResourceType.Food
                ? resources.Food : type == ResourceType.Wood ? resources.Wood
                : type == ResourceType.Gold ? resources.Gold : resources.Stone);
            pipeline = new StrategicPipeline(sim, manager, planner);
        }

        // Removing the human authority gate lets this qualifying route create a plan.
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(0, true)]
        public void HumanBackground_IsAdvisoryWithoutWork(int owner, bool emergency)
        {
            Setup(owner, Array.Empty<int>());
            pipeline.EvaluateNow(emergency ? StrategicEvaluationTriggerType.Emergency
                : StrategicEvaluationTriggerType.WorldStateChange, "Qualifying no-input snapshot.");
            Assert.That(pipeline.LastRecommendations, Is.Not.Empty);
            Assert.That(pipeline.LastDecision.HasSelection, Is.True);
            Assert.That(pipeline.LastSubmission?.CreatedPlan ?? false, Is.False);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Checking only the pipeline caller would leave this lower entry point open.
        [TestCase(false)]
        [TestCase(true)]
        public void HumanRecommendation_DirectCommitRejectsEvenEmergency(bool emergency)
        {
            Setup(0, Array.Empty<int>());
            var recommendation = new StrategicIntent(17, 0,
                StrategicObjectiveType.MilitaryReinforcement, 0, null, null,
                StrategicIntentSource.AIRecommendation);
            var result = planner.SubmitIntent(recommendation, emergency, false);
            Assert.That(result.CreatedPlan, Is.False);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(manager.Goals, Is.Empty);
        }

        // A public direct-source value alone is not evidence of a trusted input route.
        [Test]
        public void ForgedPlayerDirectLabel_CannotCommit()
        {
            Setup(0, Array.Empty<int>());
            var forged = new StrategicIntent(18, 0,
                StrategicObjectiveType.MilitaryReinforcement, 0);
            Assert.That(planner.SubmitIntent(forged, true, true).CreatedPlan, Is.False);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(manager.Goals, Is.Empty);
        }

        [Test]
        public void ExplicitComputerOwner_BackgroundStillCommits()
        {
            Setup(1, new[] { 1 });
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.WorldStateChange);
            Assert.That(pipeline.LastSubmission?.CreatedPlan, Is.True);
            Assert.That(planner.Plans, Has.Count.EqualTo(1));
            Assert.That(manager.Goals, Is.Not.Empty);
        }

        [Test]
        public void TrustedDirectRoot_SurvivesBackgroundEmergency()
        {
            Setup(0, Array.Empty<int>());
            var submitted = planner.SubmitIntent(StrategicObjectiveType.MilitaryReinforcement);
            Assert.That(submitted.CreatedPlan, Is.True);
            var goals = manager.Goals.Select(g => g.GoalId).ToArray();
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.Emergency);
            Assert.That(submitted.Plan.IsTerminal, Is.False);
            Assert.That(planner.Plans, Has.Count.EqualTo(1));
            Assert.That(manager.Goals.Select(g => g.GoalId), Is.EqualTo(goals));
            Assert.That(planner.SubmitIntent(submitted.Intent).CreatedPlan, Is.False);
        }

        [Test]
        public void ApprovalRequiresDisplayedSingleUseCredential_NotJustIdentityOrLabel()
        {
            Setup(0, Array.Empty<int>());
            sim.SetPlayerAge(0, 3);
            var worker = sim.UnitRegistry.CreateUnit(0,
                sim.MapData.TileToWorldFixed(sim.MapData.Width / 2, sim.MapData.Height / 2),
                Fixed32.One, Fixed32.One, Fixed32.One);
            worker.UnitType = 0;
            worker.IsVillager = true;
            worker.CurrentHealth = worker.MaxHealth = 100;
            worker.State = UnitState.Idle;
            using var bridge = new StrategicAIApprovalBridge(new NoProvider(), planner.IntentIds,
                () => pipeline.CaptureContext());
            var staged = bridge.StageValidatedSemanticObjective(
                StrategicObjectiveType.MilitaryReinforcement, 0, bridge.Generation);
            Assert.That(staged.Success, Is.True);
            var merelyValid = new StrategicApprovalLayer().Evaluate(pipeline.CaptureContext(),
                staged.Intent, staged.Intent.Source);
            Assert.That(pipeline.EvaluateApprovedIntentNow(merelyValid).Submission?.CreatedPlan ?? false,
                Is.False, "Schema/feasibility approval is not the player's consent.");
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(manager.Goals, Is.Empty);
            var approved = bridge.TakeRecommendation(staged.Intent.IntentId);
            Assert.That(approved, Is.SameAs(staged.Intent));
            Assert.That(bridge.TakeRecommendation(approved.IntentId), Is.Null);
            var permission = new StrategicApprovalLayer().Evaluate(pipeline.CaptureContext(),
                approved, approved.Source);
            Assert.That(permission.Approved, Is.True, permission.Reason);
            var decision = pipeline.EvaluateApprovedIntentNow(permission);
            var committed = decision.Submission;
            Assert.That(committed?.CreatedPlan, Is.True, decision.Outcome);
            Assert.That(pipeline.EvaluateApprovedIntentNow(permission).Submission?.CreatedPlan ?? false,
                Is.False);
            Assert.That(planner.Plans, Has.Count.EqualTo(1));
            Assert.That(manager.Goals, Is.Not.Empty);
            planner.Tick(30);
            Assert.That(committed.Plan.IsTerminal, Is.False, "Approval still authorizes its live milestones.");
            var enemy = sim.UnitRegistry.CreateUnit(1,
                sim.MapData.TileToWorldFixed(sim.MapData.Width / 2, sim.MapData.Height / 2),
                Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = 1;
            enemy.CurrentHealth = enemy.MaxHealth = 100;
            enemy.State = UnitState.Idle;
            int goalCount = manager.Goals.Count;
            pipeline.EvaluateNow(StrategicEvaluationTriggerType.Emergency);
            Assert.That(pipeline.LastDecision.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Emergency));
            Assert.That(pipeline.LastSubmission?.CreatedPlan ?? false, Is.False);
            Assert.That(committed.Plan.IsTerminal, Is.False,
                "Even an older AIRecommendation-labelled but player-approved root cannot be replaced without consent.");
            Assert.That(manager.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(planner.Plans, Has.Count.EqualTo(1));
        }

        private sealed class NoProvider : IStrategicAIInterpreter
        {
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("The test must use the actual typed staging/approval path.");
        }
    }
}
