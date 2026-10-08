using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5AGroundedScopeTests
    {
        [Test]
        public void KnownFarmPayload_ReusesTheImplementedCanonicalConstructionAdapter()
        {
            WithManager((sim, manager) =>
            {
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"BuildStructure\",\"structure\":\"Farm\",\"count\":4}]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var context = new CommanderContextBuilder().Build(sim, manager);
                Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context,
                    out var intent, out var reason), Is.True, reason);
                Assert.That(((BuildStructureIntent)intent).StructureType, Is.EqualTo(BuildingType.Farm));
                Assert.That(((BuildStructureIntent)intent).Count, Is.EqualTo(4));
                Assert.That(manager.Goals, Is.Empty);
            });
        }

        [Test]
        public void ProviderContext_ProjectsCanonicalIdsFromExistingGameDataWithoutRuntimeIdentities()
        {
            WithManager((sim, manager) =>
            {
                var context = new CommanderContextBuilder().Build(sim, manager);
                var request = new CommanderSemanticProviderRequest("make four farms", context);
                Assert.That(request.SerializedContext, Does.Contain("\"canonicalUnitIds\""));
                Assert.That(request.SerializedContext, Does.Contain("\"unit:1\""));
                Assert.That(request.SerializedContext, Does.Contain("\"canonicalBuildingIds\""));
                Assert.That(request.SerializedContext, Does.Contain("\"building:Farm\""));
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(request.SerializedContext,
                    "\"structureCapabilities\"\\s*:\\s*\\[[^\\]]*\"Farm\""), Is.True,
                    "KnownIntent vocabulary must not contradict the actual implemented canonical Farm adapter.");
                Assert.That(request.SerializedContext, Does.Not.Contain("buildingId"));
                Assert.That(request.SerializedContext, Does.Not.Contain("TileX"));
                Assert.That(request.SerializedContext, Does.Not.Contain("PlayerId"));
                Assert.That(manager.Goals, Is.Empty);
            });
        }

        [TestCase("{\"type\":\"AttackTarget\",\"unitSelector\":\"Spearman\",\"count\":4,\"location\":\"VisibleEnemy\"}")]
        [TestCase("{\"type\":\"ReachAge\",\"targetAge\":\"Castle\"}")]
        [TestCase("{\"type\":\"SetResourceAllocation\",\"resource\":\"Wood\",\"count\":4}")]
        public void IndependentlyTypedFourSpearmen_RejectsAdditionalProviderEffects(string extra)
        {
            WithManager((sim, manager) =>
            {
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}," + extra + "]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var method = GroundedMethod();
                var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(manager,
                    new object[] { parsed, new CommanderIntent[] { new EnsureUnitCountIntent(0, 1, 4) },
                        "make four spearmen", 1 }));
                Assert.That(failure.InnerException, Is.TypeOf<ArgumentException>());
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            });
        }

        [Test]
        public void IndependentlyTypedRoot_RejectsDroppedNoConstructionConstraint()
        {
            WithManager((sim, manager) =>
            {
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}");
                var typed = new EnsureUnitCountIntent(0, 1, 4,
                    new CommanderConstraint[] { new NoConstructionConstraint() });
                var failure = Assert.Throws<TargetInvocationException>(() => GroundedMethod().Invoke(manager,
                    new object[] { parsed, new CommanderIntent[] { typed }, "four spearmen without construction", 1 }));
                Assert.That(failure.InnerException, Is.TypeOf<ArgumentException>());
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            });
        }

        [Test]
        public void MatchingIndependentlyTypedRoot_CommitsExactlyOnceWithoutModelApproval()
        {
            WithManager((sim, manager) =>
            {
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}");
                var candidate = (CommanderActionPlanCandidate)GroundedMethod().Invoke(manager,
                    new object[] { parsed, new CommanderIntent[] { new EnsureUnitCountIntent(0, 1, 4) },
                        "four spearmen from a trusted typed control", 1 });
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                var goal = (EnsureUnitCountGoal)manager.SubmitSemanticGraph(candidate.Graph)[0];
                Assert.That(goal.TargetTotal, Is.EqualTo(4));
                Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(candidate.Graph));
                Assert.That(manager.Goals.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void NormalizedDynamicPreview_ShowsSourcePartitionAndLocationRestrictions()
        {
            WithManager((sim, manager) =>
            {
                var mill = manager.PrepareActionPlan(CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill),
                    "make a mill", 1);
                Assert.That(mill.Preview, Does.Contain("Berries"));
                Assert.That(mill.Preview, Does.Contain("visible"));
                Assert.That(mill.Preview, Does.Contain("Near"));
                var shared = manager.PrepareActionPlan(CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers),
                    "share three idle villagers", 1);
                Assert.That(shared.Preview, Does.Contain("3 eligible idle villagers"));
                Assert.That(shared.Preview, Does.Contain("villagers 1–2"));
                Assert.That(shared.Preview, Does.Contain("villager 3"));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            });
        }

        private static MethodInfo GroundedMethod()
        {
            var method = typeof(CommanderGoalManager).GetMethod("PrepareGroundedActionPlan",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Independent typed root scope must be enforced before candidate admission.");
            return method;
        }

        private static void WithManager(Action<GameSimulation, CommanderGoalManager> check)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                check(sim, manager);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
