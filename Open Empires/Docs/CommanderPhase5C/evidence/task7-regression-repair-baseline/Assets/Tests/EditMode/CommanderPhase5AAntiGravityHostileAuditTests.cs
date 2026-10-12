using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    [Category("AntiGravityAudit")]
    public sealed class CommanderPhase5AAntiGravityHostileAuditTests
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

        private void Setup(int owner, int[] computerOwners, int food = 5000, int wood = 5000, int gold = 5000, int stone = 5000)
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, computerOwners);
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var resources = sim.ResourceManager.GetPlayerResources(owner);
            resources.Food = food;
            resources.Wood = wood;
            resources.Gold = gold;
            resources.Stone = stone;
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            for (int tx = x - 25; tx <= x + 25; tx++)
                for (int tz = z - 15; tz <= z + 15; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(owner, tx, tz);
                }
            sim.CreateBuilding(owner, BuildingType.TownCenter, x + 12, z, false, true).AutoProduceVillagers = false;
            sim.CreateBuilding(owner, BuildingType.House, x + 18, z, false);
            sim.CreateBuilding(owner, BuildingType.House, x + 22, z, false);
            manager = new CommanderGoalManager(sim, owner);
            planner = new StrategicPlanner(manager, type => type == ResourceType.Food
                ? resources.Food : type == ResourceType.Wood ? resources.Wood
                : type == ResourceType.Gold ? resources.Gold : resources.Stone);
            pipeline = new StrategicPipeline(sim, manager, planner);
        }

        #region 4. Strategic Authority Repair Hostile Tests

        [Test]
        public void HumanEvaluation_AllRecommendationFamilies_RemainAdvisoryWithoutWork()
        {
            Setup(0, Array.Empty<int>(), food: 1000, wood: 1000, gold: 1000, stone: 1000);
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            var enemy = sim.UnitRegistry.CreateUnit(1, sim.MapData.TileToWorldFixed(x + 5, z + 5),
                Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = 1;
            enemy.CurrentHealth = enemy.MaxHealth = 100;
            enemy.State = UnitState.Idle;

            var record = pipeline.EvaluateNow(StrategicEvaluationTriggerType.Emergency, "Hostile evaluation matrix");

            Assert.That(pipeline.LastRecommendations, Is.Not.Empty);
            Assert.That(pipeline.LastDecision.HasSelection, Is.True);
            Assert.That(pipeline.LastSubmission?.CreatedPlan ?? false, Is.False);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(record.Outcome, Does.Contain("Suggested strategy — not started"));
        }

        [Test]
        public void EmergencyDefensivePreparation_CannotReplaceOrMutateActiveHumanAuthorizedPlan()
        {
            Setup(0, Array.Empty<int>());
            var submitted = planner.SubmitIntent(StrategicObjectiveType.MilitaryReinforcement);
            Assert.That(submitted.CreatedPlan, Is.True);
            int initialPlans = planner.Plans.Count;
            int initialGoals = manager.Goals.Count;
            int initialReservations = planner.Reservations.Count;

            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            var enemy = sim.UnitRegistry.CreateUnit(1, sim.MapData.TileToWorldFixed(x + 2, z + 2),
                Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = 1;
            enemy.CurrentHealth = enemy.MaxHealth = 100;

            pipeline.EvaluateNow(StrategicEvaluationTriggerType.Emergency, "Surprise enemy assault");

            Assert.That(pipeline.LastDecision.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Emergency));
            Assert.That(pipeline.LastSubmission?.CreatedPlan ?? false, Is.False);
            Assert.That(submitted.Plan.IsTerminal, Is.False);
            Assert.That(submitted.Plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Plans.Count, Is.EqualTo(initialPlans));
            Assert.That(manager.Goals.Count, Is.EqualTo(initialGoals));
            Assert.That(planner.Reservations.Count, Is.EqualTo(initialReservations));
        }

        [TestCase(-1, false)]
        [TestCase(0, false)] // Human player
        [TestCase(1, true)]  // AI computer owner
        [TestCase(2, false)] // Out of bounds
        public void OwnerMatrix_ComputerOwnershipGating(int testPlayerId, bool expectedAutonomy)
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, new[] { 1 });
            using var testManager = new CommanderGoalManager(sim, testPlayerId < 0 || testPlayerId >= 2 ? 0 : testPlayerId);
            using var testPlanner = new StrategicPlanner(testManager, _ => 5000);

            typeof(StrategicPlanner).GetProperty("PlayerId", BindingFlags.Public | BindingFlags.Instance)
                ?.GetSetMethod(true);
            FieldInfo playerIdField = typeof(StrategicPlanner).GetField("playerId", BindingFlags.NonPublic | BindingFlags.Instance);
            if (playerIdField != null) playerIdField.SetValue(testPlanner, testPlayerId);

            Assert.That(testPlanner.HasComputerOwner, Is.EqualTo(expectedAutonomy));

            var recommendation = new StrategicIntent(99, testPlayerId, StrategicObjectiveType.MilitaryReinforcement, 0,
                null, null, StrategicIntentSource.AIRecommendation);
            bool canCommit = testPlanner.CanCommitIntent(recommendation, out string reason);
            Assert.That(canCommit, Is.EqualTo(expectedAutonomy), reason);
        }

        [Test]
        public void SubmissionBoundary_ForgedCredentials_FailClosed()
        {
            Setup(0, Array.Empty<int>());

            // Forged AI recommendation claiming emergency + player override
            var forged1 = new StrategicIntent(101, 0, StrategicObjectiveType.MilitaryReinforcement, 0, null, 100, StrategicIntentSource.AIRecommendation);
            var sub1 = planner.SubmitIntent(forged1, isEmergency: true, isPlayerOverride: true);
            Assert.That(sub1.CreatedPlan, Is.False);
            Assert.That(sub1.Error, Is.EqualTo(StrategicIntentValidationError.CommitmentBlocked));

            // Intent with unowned/unregistered ID
            var forged2 = new StrategicIntent(9999, 0, StrategicObjectiveType.AttackPreparation, 0);
            var sub2 = planner.SubmitIntent(forged2, isEmergency: false, isPlayerOverride: false);
            Assert.That(sub2.CreatedPlan, Is.False);

            // Intent from foreign player
            var forged3 = new StrategicIntent(102, 1, StrategicObjectiveType.EconomicExpansion, 0);
            var sub3 = planner.SubmitIntent(forged3, false, false);
            Assert.That(sub3.CreatedPlan, Is.False);

            // Intent with forged AuthorizationOwner pointing to a new provider
            var forged4 = new StrategicIntent(103, 0, StrategicObjectiveType.DefensivePreparation, 0);
            var fakeProvider = new StrategicIntentIdProvider();
            int fakeId = fakeProvider.Allocate();
            fakeProvider.BindAllocated(fakeId, forged4);
            try { forged4.Authorize(fakeProvider, "Forged external authorization credential"); } catch { }
            var sub4 = planner.SubmitIntent(forged4, false, false);
            Assert.That(sub4.CreatedPlan, Is.False);
        }

        #endregion

        #region 5 & 12. DynamicPlan Parser Hostile Matrix

        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"approved\":true,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"playerAuthorized\":true,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"emergencyOverride\":true,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"ownerId\":0,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"trustedRoot\":true,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"requiredPrerequisite\":true,\"nodes\":[]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"playerDirect\":true},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"runtimeId\":12},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"tileX\":50},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"worldZ\":120.5},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"command\":\"PlaceBuilding\"},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1,\"csharp\":\"System.Console.WriteLine()\"},\"inputs\":{},\"dependsOn\":[]}]}")]
        public void ParserHostileMatrix_AuthorityAndInjectionFields_RejectWholeCandidate(string json)
        {
            var result = CommanderSemanticJson.Parse(json);
            Assert.That(result.IsValid, Is.False, "Unknown, injected, coordinate, or authority fields must be rejected.");
        }

        [Test]
        public void ParserHostileMatrix_StructuralAttacks_RejectWholeCandidate()
        {
            string duplicateProperty = "{\"outcome\":\"DynamicPlan\",\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"f\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(duplicateProperty).IsValid, Is.False);

            string cyclic = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"a\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"b\"]},"
                + "{\"id\":\"b\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"a\"]}]}";
            Assert.That(CommanderSemanticJson.Parse(cyclic).IsValid, Is.False);

            string selfRef = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"a\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"a\"]}]}";
            Assert.That(CommanderSemanticJson.Parse(selfRef).IsValid, Is.False);

            string overlongId = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"" + new string('a', 33) + "\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(overlongId).IsValid, Is.False);

            string tooManyNodes = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + string.Join(",", Enumerable.Range(0, 13).Select(i => "{\"id\":\"n" + i + "\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}"))
                + "]}";
            Assert.That(CommanderSemanticJson.Parse(tooManyNodes).IsValid, Is.False);

            string depthOverflow = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"n0\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"n1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n0\"]},"
                + "{\"id\":\"n2\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n1\"]},"
                + "{\"id\":\"n3\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n2\"]},"
                + "{\"id\":\"n4\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n3\"]},"
                + "{\"id\":\"n5\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n4\"]},"
                + "{\"id\":\"n6\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"n5\"]}]}";
            Assert.That(CommanderSemanticJson.Parse(depthOverflow).IsValid, Is.False);

            string negativeCount = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"a\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":-1},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(negativeCount).IsValid, Is.False);

            string zeroCount = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"a\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":0},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(zeroCount).IsValid, Is.False);

            string aggregateOverflow = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"u1\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Military\",\"count\":50},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"u2\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Military\",\"count\":50},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"u3\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Military\",\"count\":50},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"u4\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Military\",\"count\":50},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"u5\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Military\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(aggregateOverflow).IsValid, Is.False);

            string overlappingWorkers = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"w\",\"mechanic\":\"select-workers\",\"parameters\":{\"count\":3},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"p1\",\"mechanic\":\"partition-workers\",\"parameters\":{\"offset\":0,\"count\":2},\"inputs\":{\"workers\":\"w\"},\"dependsOn\":[\"w\"]},"
                + "{\"id\":\"p2\",\"mechanic\":\"partition-workers\",\"parameters\":{\"offset\":1,\"count\":2},\"inputs\":{\"workers\":\"w\"},\"dependsOn\":[\"w\"]},"
                + "{\"id\":\"a1\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"inputs\":{\"workers\":\"p1\"},\"dependsOn\":[\"p1\"]},"
                + "{\"id\":\"a2\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"inputs\":{\"workers\":\"p2\"},\"dependsOn\":[\"p2\"]}]}";
            Assert.That(CommanderSemanticJson.Parse(overlappingWorkers).IsValid, Is.False);
        }

        #endregion

        #region 7. Confirmation Integrity and Replay Attacks

        [Test]
        public void ConfirmationIntegrity_CannotReplayOrCommitAcrossRuntimes()
        {
            Setup(0, Array.Empty<int>());
            var interpretation = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Farms);
            Assert.That(interpretation.IsValid, Is.True);

            var candidate = manager.PrepareActionPlan(interpretation, "make 4 farms", 1);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);

            var approvedGraph = manager.ApproveActionPlan(candidate, 1);
            Assert.That(approvedGraph, Is.Not.Null);

            // Re-approval fails
            Assert.That(manager.ApproveActionPlan(candidate, 1), Is.Null);

            // Commit once succeeds
            var admitted = manager.SubmitSemanticGraph(approvedGraph);
            Assert.That(admitted, Has.Count.EqualTo(1));

            // Re-commit fails
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(approvedGraph));

            // Foreign goal manager cannot commit approvedGraph
            using var foreignManager = new CommanderGoalManager(sim, 0);
            Assert.Throws<InvalidOperationException>(() => foreignManager.SubmitSemanticGraph(approvedGraph));
        }

        [Test]
        public void ConfirmationIntegrity_CancelInvalidatesCommitment()
        {
            Setup(0, Array.Empty<int>());
            var interpretation = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Farms);
            var candidate = manager.PrepareActionPlan(interpretation, "make 4 farms", 1);
            candidate.Cancel();

            Assert.That(manager.ApproveActionPlan(candidate, 1), Is.Null);
            Assert.That(manager.Goals, Is.Empty);
        }

        #endregion

        #region 13. Graph Atomicity

        [Test]
        public void GraphAtomicity_PartialInvalid_ExecutesZeroNodes()
        {
            Setup(0, Array.Empty<int>());
            // Node 0 valid build, Node 1 valid build, Node 2 invalid unknown building
            string mixed = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"f1\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"f2\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"f3\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Unobtanium\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}]}";

            var result = CommanderSemanticJson.Parse(mixed);
            Assert.That(result.IsValid, Is.False);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        #endregion

        #region 14. Primitive Catalog Audit

        [Test]
        public void PrimitiveCatalog_ExactlyNineRegisteredPrimitives()
        {
            var primitives = CommanderDynamicPrimitiveRegistry.All;
            Assert.That(primitives.Count, Is.EqualTo(9));

            var expected = new[]
            {
                "select-workers",
                "partition-workers",
                "select-units",
                "select-structures",
                "select-resources",
                "resolve-location",
                "build",
                "allocate-workers",
                "produce"
            };

            Assert.That(primitives.Select(p => p.Id), Is.EquivalentTo(expected));

            // Verify non-effect vs effect classification
            var nonEffects = primitives.Where(p => p.Effect == CommanderDynamicEffectClass.Selection
                || p.Effect == CommanderDynamicEffectClass.Location).Select(p => p.Id).ToArray();
            Assert.That(nonEffects, Is.EquivalentTo(new[]
            {
                "select-workers", "partition-workers", "select-units", "select-structures",
                "select-resources", "resolve-location"
            }));

            var effects = primitives.Where(p => p.Effect == CommanderDynamicEffectClass.Construction
                || p.Effect == CommanderDynamicEffectClass.Allocation
                || p.Effect == CommanderDynamicEffectClass.Production).Select(p => p.Id).ToArray();
            Assert.That(effects, Is.EquivalentTo(new[] { "build", "allocate-workers", "produce" }));
        }

        #endregion

        #region 23 & 25. Questions and Unsupported Mechanics

        [Test]
        public void Questions_AreNonEffectfulAndCreateZeroAttributableGoals()
        {
            Setup(0, Array.Empty<int>());
            // Unknown fields like "text" are strictly rejected by CheckFields
            Assert.That(CommanderSemanticJson.Parse("{\"outcome\":\"Answer\",\"text\":\"Castle Age info\"}").IsValid, Is.False);

            var answerResult = CommanderSemanticJson.Parse("{\"outcome\":\"Answer\",\"message\":\"You reach Castle Age by building a Landmark with 1200 Food and 600 Gold.\"}");
            Assert.That(answerResult.IsValid, Is.True);
            Assert.That(answerResult.Outcome, Is.EqualTo(CommanderSemanticOutcome.Answer));
            Assert.That(answerResult.Nodes, Is.Empty);
            Assert.That(answerResult.DynamicPlan, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void UnsupportedMechanics_CreateZeroGoalsAndZeroCommands()
        {
            Setup(0, Array.Empty<int>());
            // Unknown fields like "reason" are strictly rejected by CheckFields
            Assert.That(CommanderSemanticJson.Parse("{\"outcome\":\"Unsupported\",\"reason\":\"Teleportation\"}").IsValid, Is.False);

            var unsupported = CommanderSemanticJson.Parse("{\"outcome\":\"Unsupported\",\"message\":\"Teleportation is not a supported mechanic.\"}");
            Assert.That(unsupported.IsValid, Is.True);
            Assert.That(unsupported.Outcome, Is.EqualTo(CommanderSemanticOutcome.Unsupported));
            Assert.That(unsupported.Nodes, Is.Empty);
            Assert.That(unsupported.DynamicPlan, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        #endregion
    }
}
