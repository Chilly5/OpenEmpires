using System;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E4")]
    public sealed class CommanderPhase4E4ReachAgeTests
    {
        [TestCase("Feudal", CommanderSemanticAgeTarget.Feudal)]
        [TestCase("Castle", CommanderSemanticAgeTarget.Castle)]
        [TestCase("Imperial", CommanderSemanticAgeTarget.Imperial)]
        [TestCase("Next", CommanderSemanticAgeTarget.Next)]
        public void Parse_ReachAge_UsesOnlyBoundedAgeVocabulary(string token, CommanderSemanticAgeTarget expected)
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":\""
                + token + "\"}]}" );

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes, Has.Count.EqualTo(1));
            Assert.That(result.Nodes[0].AgeTarget, Is.EqualTo(expected));
        }

        [TestCase("Stone")]
        [TestCase("5")]
        [TestCase("{\"targetAge\":3}")]
        public void Parse_ReachAge_InvalidTargetIsRejected(string value)
        {
            string target = value.StartsWith("{") ? value : "\"" + value + "\"";
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":"
                + target + "}]}" );

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Admission_ReachAgeProducesTypedIntentWithoutConcreteAuthority()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":\"Castle\"}]}" );

                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);
                Assert.That(plan.Nodes[0].Intent, Is.TypeOf<ReachAgeIntent>());
                Assert.That(((ReachAgeIntent)plan.Nodes[0].Intent).TargetAge, Is.EqualTo(3));
            }
            finally
            {
                manager.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SubmitSemanticGraph_ReachAgeCreatesDesiredStateGoal()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":\"Next\"}]}" );
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);
                var submitted = manager.SubmitSemanticGraph(plan);

                Assert.That(submitted, Has.Count.EqualTo(1));
                Assert.That(submitted[0], Is.TypeOf<ReachAgeGoal>());
                Assert.That(((ReachAgeGoal)submitted[0]).RequestedTarget, Is.EqualTo(CommanderSemanticAgeTarget.Next));
                Assert.That(((ReachAgeGoal)submitted[0]).TargetAge, Is.EqualTo(2));
            }
            finally
            {
                manager.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void LegacyTacticalProviderShape_AlsoAdmitsReachAge()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                var parsed = CommanderAIJson.ParseTacticalIntent(
                    "{\"intentCategory\":\"Tactical\",\"intentType\":\"ReachAge\",\"parameters\":{\"targetAge\":\"Castle\"}}",
                    context);
                Assert.That(parsed.Success, Is.True, parsed.FailureReason);
                var interpretation = CommanderIntentDtoCodec.ValidateAndConvert(parsed.IntentDto, context);
                Assert.That(interpretation.Success, Is.True, interpretation.Reason);
                Assert.That(interpretation.Intent, Is.TypeOf<ReachAgeIntent>());
                Assert.That(((ReachAgeIntent)interpretation.Intent).TargetAge, Is.EqualTo(3));
            }
            finally
            {
                manager.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
