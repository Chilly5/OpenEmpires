using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixScopeTests
    {
        [Test]
        public void EquivalentConstraintOrder_DoesNotDependOnPreviewFormatting()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"constraints\":["
                    + "{\"type\":\"MaximumQueue\",\"amount\":2},{\"type\":\"NoConstruction\"}]}]}");
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                var typed = new EnsureUnitCountIntent(0, 1, 4,
                    new CommanderConstraint[] { new NoConstructionConstraint(), new MaximumQueueConstraint(2) });
                var candidate = manager.PrepareGroundedActionPlan(parsed, new CommanderIntent[] { typed }, "four spearmen", 1);
                Assert.That(candidate.AuthorizationEvidence, Is.EqualTo("TrustedTypedInput"));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                var goal = manager.SubmitSemanticGraph(candidate.Graph)[0];
                Assert.That(goal, Is.TypeOf<EnsureUnitCountGoal>());
                Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(candidate.Graph));
                Assert.That(manager.Goals.Count, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ProductionScope_DistinguishesEveryCountTypeOwnerAndConstraintField(int mutation)
        {
            var original = new EnsureUnitCountIntent(0, 1, 4, new CommanderConstraint[] { new MaximumQueueConstraint(2) }, 3);
            CommanderIntent altered = mutation == 0 ? new EnsureUnitCountIntent(1, 1, 4, original.Constraints, 3)
                : mutation == 1 ? new EnsureUnitCountIntent(0, 2, 4, original.Constraints, 3)
                : mutation == 2 ? new EnsureUnitCountIntent(0, 1, 5, original.Constraints, 3)
                : mutation == 3 ? new EnsureUnitCountIntent(0, 1, 4, original.Constraints)
                : new EnsureUnitCountIntent(0, 1, 4, new CommanderConstraint[] { new MaximumQueueConstraint(3) }, 3);
            Assert.That(SameIntent(original, altered), Is.False);
            Assert.That(SameIntent(original, new EnsureUnitCountIntent(0, 1, 4, original.Constraints, 3)), Is.True);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void CapabilityScope_DistinguishesActorLocationTypeTechnologyAndRadius(int mutation)
        {
            var original = Action(CommanderCapabilityActionType.PatrolArea, 3, 1,
                CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 4);
            var altered = mutation == 0 ? Action(CommanderCapabilityActionType.MoveUnits, 3, 1, CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 4)
                : mutation == 1 ? Action(CommanderCapabilityActionType.PatrolArea, 2, 1, CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 4)
                : mutation == 2 ? Action(CommanderCapabilityActionType.PatrolArea, 3, 2, CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 4)
                : mutation == 3 ? Action(CommanderCapabilityActionType.PatrolArea, 3, 1, CommanderLocationSelectorKind.VisibleResource, ResourceType.Gold, 4)
                : mutation == 4 ? Action(CommanderCapabilityActionType.PatrolArea, 3, 1, CommanderLocationSelectorKind.WorkedResource, ResourceType.Wood, 4)
                : Action(CommanderCapabilityActionType.PatrolArea, 3, 1, CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 5);
            Assert.That(SameIntent(original, altered), Is.False);
            Assert.That(SameIntent(original, Action(CommanderCapabilityActionType.PatrolArea, 3, 1,
                CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold, 4)), Is.True);
        }

        [Test]
        public void Scope_RejectsUnknownIntentAndConstraintWithoutDisplayOrReflectionSerialization()
        {
            Assert.That(SameIntent(new UnknownIntent(), new UnknownIntent()), Is.False);
            var unknown = new CommanderConstraint[] { new UnknownConstraint() };
            Assert.That(SameIntent(new EnsureUnitCountIntent(0, 1, 4, unknown), new EnsureUnitCountIntent(0, 1, 4, unknown)), Is.False);
        }

        private static CapabilityActionIntent Action(CommanderCapabilityActionType action, int count, int unit,
            CommanderLocationSelectorKind location, ResourceType resource, int radius)
            => new CapabilityActionIntent(0, action, new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, count, unit),
                new CommanderLocationSelector(location, resource, radius));

        private static bool SameIntent(CommanderIntent left, CommanderIntent right)
        {
            var type = typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderScopeEquivalence");
            Assert.That(type, Is.Not.Null, "Authorization needs an explicit structural comparison independent of presentation.");
            var method = type.GetMethod("SameIntent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { left, right });
        }

        private sealed class UnknownIntent : CommanderIntent
        { internal UnknownIntent() : base(CommanderIntentType.EnsureUnitCount, 0) { } }
        private sealed class UnknownConstraint : CommanderConstraint
        { internal UnknownConstraint() : base(CommanderConstraintType.NoConstruction) { } }
    }
}
