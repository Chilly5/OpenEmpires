using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D4")]
    public sealed class CommanderPhase4D4Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicAdvisoryFeed feed;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            feed = new StrategicAdvisoryFeed();
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        // Mutation caught: a pipeline cannot observe one match while its planner executes another.
        [Test]
        public void Pipeline_RejectsForeignSimulationBeforeBindingPlanner()
        {
            var foreignSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreignSimulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            using var foreignGoals = new CommanderGoalManager(foreignSimulation, 0);

            Assert.Throws<ArgumentException>(() =>
                new StrategicPipeline(foreignSimulation, foreignGoals, planner));
            Assert.That(planner.Pipeline, Is.Null,
                "Rejected composition must not leave the planner bound to a foreign pipeline.");
        }

        // Mutation caught: a second manager on the same simulation is still a different authority owner.
        [Test]
        public void Pipeline_RejectsForeignGoalManagerBeforeBindingPlanner()
        {
            using var foreignGoals = new CommanderGoalManager(simulation, 0);

            Assert.Throws<ArgumentException>(() =>
                new StrategicPipeline(simulation, foreignGoals, planner));
            Assert.That(planner.Pipeline, Is.Null,
                "Rejected composition must not register planner callbacks or a pipeline reference.");
        }

        // Mutation caught: dropping a real typed resource-wait transition or copying the wrong identity.
        [Test]
        public void Advisory_EmitsOnMeaningfulTransition()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy));
            var output = feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12));
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].Transition, Is.EqualTo(StrategicAdvisoryTransition.WaitingForResources));
            Assert.That(output[0].Display, Does.Contain("Wood"));
            Assert.That(output[0].Display, Does.Contain("12"));
            Assert.That(output[0].PlayerId, Is.EqualTo(0));
            Assert.That(output[0].PlanId, Is.EqualTo(plan.StrategicPlanId));
            Assert.That(output[0].CreatedTick, Is.EqualTo(plan.CreatedTick));
            Assert.That(output[0].ObservedTick, Is.EqualTo(2));
        }

        // Mutation caught: treating changed tick or deficit amount as a new transition.
        [Test]
        public void Advisory_DoesNotRepeatEveryTick()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy));
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12)).Count, Is.EqualTo(1));
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 9)), Is.Empty);
        }

        // Mutation caught: missing recovery or repeating it after a wait disappears.
        [Test]
        public void Advisory_RecoveryEmitsOnce()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.WaitingForPopulation));
            var recovery = feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Healthy));
            Assert.That(recovery.Select(x => x.Transition), Is.EqualTo(new[] {
                StrategicAdvisoryTransition.Recovered }));
            Assert.That(recovery[0].Display, Does.Contain("population"));
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.Healthy)), Is.Empty);
        }

        // Mutation caught: an Unknown sample clears a known blocker and then repeats or loses it.
        [Test]
        public void Advisory_UnknownDoesNotSettleKnownWait()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12));
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Unknown)), Is.Empty);
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 9)), Is.Empty, "Unknown must not rearm the same Wood wait.");
            Assert.That(feed.Observe(Health(plan, 4, StrategicPlanHealthCategory.Unknown)), Is.Empty);
            var recovery = feed.Observe(Health(plan, 5, StrategicPlanHealthCategory.Healthy));
            Assert.That(recovery.Select(x => x.Transition), Is.EqualTo(new[] {
                StrategicAdvisoryTransition.Recovered }));
            Assert.That(recovery.Single().Display, Does.Contain("Wood"));
            Assert.That(feed.Observe(Health(plan, 6, StrategicPlanHealthCategory.Healthy)), Is.Empty);
        }

        // Mutation caught: category-only comparison misses one resource recovering while another remains.
        [Test]
        public void Advisory_ResourceRecoveryWhileAnotherWaits()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(HealthWithResources(plan, 1, ResourceType.Wood, ResourceType.Gold));
            var recovery = feed.Observe(HealthWithResources(plan, 2, ResourceType.Gold));
            Assert.That(recovery.Select(x => x.Transition), Is.EqualTo(new[] {
                StrategicAdvisoryTransition.Recovered }));
            Assert.That(recovery.Single().Display, Does.Contain("Wood"));
            Assert.That(recovery.Single().Display, Does.Not.Contain("Gold"));
            Assert.That(feed.Observe(HealthWithResources(plan, 3, ResourceType.Gold)), Is.Empty);
        }

        // Mutation caught: category-only comparison suppresses a new typed resource blocker.
        [Test]
        public void Advisory_ResourceIdentityChangeIsTransition()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(HealthWithResources(plan, 1, ResourceType.Wood));
            var changed = feed.Observe(HealthWithResources(plan, 2, ResourceType.Gold));
            Assert.That(changed.Select(x => x.Transition), Is.EqualTo(new[] {
                StrategicAdvisoryTransition.Recovered,
                StrategicAdvisoryTransition.WaitingForResources }));
            Assert.That(changed[0].Display, Does.Contain("Wood"));
            Assert.That(changed[1].Display, Does.Contain("Gold"));
            Assert.That(feed.Observe(HealthWithResources(plan, 3, ResourceType.Gold)), Is.Empty);
        }

        // Mutation caught: terminal completion omitted or repeated at each sampled tick.
        [Test]
        public void Advisory_CompletionEmitsOnce()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy));
            plan.Status = StrategicPlanStatus.Completed;
            var completed = feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Completed));
            Assert.That(completed.Select(x => x.Transition), Is.EqualTo(new[] {
                StrategicAdvisoryTransition.Completed }));
            Assert.That(completed[0].Display, Does.Contain("completed"));
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.Completed)), Is.Empty);
        }

        // Mutation caught: Reset leaves old plan identity/dedup state behind.
        [Test]
        public void Advisory_ResetClearsDeduplication()
        {
            StrategicPlan plan = StartPlan();
            var waiting = Health(plan, 1, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12);
            Assert.That(feed.Observe(waiting), Is.Empty);
            feed.Reset();
            Assert.That(feed.Observe(waiting), Is.Empty, "The first sample after reset seeds silently.");
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Healthy))
                .Single().Transition, Is.EqualTo(StrategicAdvisoryTransition.Recovered));
        }

        // Mutation caught: unbounded output or retaining the 33rd identity without oldest eviction.
        [Test]
        public void Advisory_IsBounded()
        {
            StrategicPlan plan = StartPlan();
            int originalId = plan.StrategicPlanId;
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy));
            var many = feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.WaitingForPopulation,
                StrategicPlanHealthCategory.WaitingForResources,
                StrategicPlanHealthCategory.WaitingForPrerequisite,
                StrategicPlanHealthCategory.WaitingForConstruction,
                StrategicPlanHealthCategory.WaitingForProduction,
                StrategicPlanHealthCategory.TemporarilyBlocked));
            Assert.That(many.Count, Is.EqualTo(4));
            for (int i = 1; i <= 32; i++)
            {
                plan.StrategicPlanId = originalId + i;
                Assert.That(feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy)), Is.Empty);
            }
            plan.StrategicPlanId = originalId;
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.WaitingForPopulation)),
                Is.Empty, "The oldest identity was evicted, so this is a silent first sample.");
        }

        // Mutation caught: accepting an older revision/tick or conflating a reused numeric ID.
        [Test]
        public void Advisory_IsPlanVersionSafe()
        {
            StrategicPlan plan = StartPlan();
            var old = Health(plan, 1, StrategicPlanHealthCategory.Healthy);
            feed.Observe(old);
            plan.AdvanceRevision();
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.WaitingForPopulation))
                .Single().Transition, Is.EqualTo(StrategicAdvisoryTransition.WaitingForPopulation));
            Assert.That(feed.Observe(old), Is.Empty);
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Healthy)), Is.Empty);
            Assert.That(feed.Observe(Health(plan, 4, StrategicPlanHealthCategory.WaitingForPopulation)),
                Is.Empty);
            plan.CreatedTick++;
            Assert.That(feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy)), Is.Empty);
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12)).Single().Transition,
                Is.EqualTo(StrategicAdvisoryTransition.WaitingForResources));
        }

        // Mutation caught: emitting a hidden-enemy fact or inventing a cause for Unknown.
        [Test]
        public void Advisory_RemainsFogSafe()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(planner.CapturePlanHealth(0, plan.StrategicPlanId));
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            var enemy = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(x - 30, z - 30),
                Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = CommanderIntentCatalog.KnightUnitType;
            enemy.CurrentHealth = enemy.MaxHealth = 100;
            Assert.That(feed.Observe(planner.CapturePlanHealth(0, plan.StrategicPlanId)), Is.Empty);
            feed.Reset();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.Healthy));
            var unknown = feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Unknown,
                StrategicPlanHealthCategory.WaitingForResources));
            Assert.That(unknown, Is.Empty);
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.Healthy)), Is.Empty,
                "An unknown primary cannot seed a causal wait from secondary hints.");
            foreach (FieldInfo field in typeof(StrategicAdvisoryFeed).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Concat(typeof(StrategicAdvisory).GetFields(BindingFlags.Instance
                    | BindingFlags.NonPublic | BindingFlags.Public)))
                Assert.That(new[] { typeof(GameSimulation), typeof(StrategicPlan),
                    typeof(StrategicPlanHealthSnapshot), typeof(CommanderGoalManager),
                    typeof(StrategicPlanner), typeof(CommanderContext) }
                    .Any(type => type == field.FieldType), Is.False, field.Name);
        }

        // Mutation caught: pause is mistaken for recovery from an earlier blocker.
        [Test]
        public void Advisory_PauseResumeDoesNotInventRecovery()
        {
            StrategicPlan plan = StartPlan();
            feed.Observe(Health(plan, 1, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 12));
            plan.Status = StrategicPlanStatus.Paused;
            Assert.That(feed.Observe(Health(plan, 2, StrategicPlanHealthCategory.Paused))
                .Select(x => x.Transition), Is.EqualTo(new[] { StrategicAdvisoryTransition.Paused }));
            plan.Status = StrategicPlanStatus.Active;
            Assert.That(feed.Observe(Health(plan, 3, StrategicPlanHealthCategory.WaitingForResources,
                resourceDeficit: 9)).Select(x => x.Transition),
                Is.EqualTo(new[] { StrategicAdvisoryTransition.Resumed }));
        }

        private StrategicPlanHealthSnapshot Health(StrategicPlan plan, int tick,
            StrategicPlanHealthCategory primary, params StrategicPlanHealthCategory[] secondary) =>
            Health(plan, tick, primary, 0, secondary);

        private StrategicPlanHealthSnapshot Health(StrategicPlan plan, int tick,
            StrategicPlanHealthCategory primary, int resourceDeficit,
            params StrategicPlanHealthCategory[] secondary)
        {
            var context = new CommanderContextBuilder().Build(simulation, goals);
            return new StrategicPlanHealthSnapshot(plan, plan.CurrentMilestone, tick, 0, 0, 0,
                context, 0, primary, secondary.ToList(),
                new List<StrategicPlanHealthResource> {
                    new StrategicPlanHealthResource(ResourceType.Wood, 100, true, 12, 0,
                        resourceDeficit, 0, 0)
                }, new List<StrategicPlanHealthChild>(),
                new List<StrategicPlanHealthStatusCount>());
        }

        private StrategicPlanHealthSnapshot HealthWithResources(StrategicPlan plan, int tick,
            params ResourceType[] blockers)
        {
            var context = new CommanderContextBuilder().Build(simulation, goals);
            var resources = blockers.Select(type => new StrategicPlanHealthResource(type,
                100, true, 12, 0, 12, 0, 0)).ToList();
            return new StrategicPlanHealthSnapshot(plan, plan.CurrentMilestone, tick, 0, 0, 0,
                context, 0, StrategicPlanHealthCategory.WaitingForResources,
                new List<StrategicPlanHealthCategory>(), resources,
                new List<StrategicPlanHealthChild>(),
                new List<StrategicPlanHealthStatusCount>());
        }

        private StrategicPlan StartPlan()
        {
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private int Resource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            return type == ResourceType.Food ? resources.Food
                : type == ResourceType.Wood ? resources.Wood
                : type == ResourceType.Gold ? resources.Gold : resources.Stone;
        }
    }
}
