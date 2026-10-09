using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BReliability")]
    public sealed class CommanderPhase5BReliabilitySemanticTests
    {
        [TestCase("2", CommanderSemanticAgeTarget.Feudal)]
        [TestCase("3", CommanderSemanticAgeTarget.Castle)]
        [TestCase("4", CommanderSemanticAgeTarget.Imperial)]
        [TestCase("\"2\"", CommanderSemanticAgeTarget.Feudal)]
        [TestCase("\"3\"", CommanderSemanticAgeTarget.Castle)]
        [TestCase("\"4\"", CommanderSemanticAgeTarget.Imperial)]
        public void AbsoluteNumericalAge_UsesTheSameCanonicalTargetAsNamedAge(string literal, CommanderSemanticAgeTarget expected)
        {
            var parsed = CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":" + literal + "}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.Nodes[0].AgeTarget, Is.EqualTo(expected));
            Assert.That(parsed.Nodes[0].AgeTarget, Is.Not.EqualTo(CommanderSemanticAgeTarget.Next));
        }

        private const string Berries = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":4,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Berries\"},\"resourceAmount\":400,\"resourceAmountMode\":\"AdditionalGathered\"}]}";

        [Test] public void BerriesAmountCountContract_PreservesIndependentSlots()
        {
            var parsed = CommanderSemanticJson.ParseProviderResponse(Berries);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var allocation = parsed.Nodes[0].WorkerAllocation;
            Assert.That(allocation.Count, Is.EqualTo(4));
            Assert.That(allocation.ResourceAmount, Is.EqualTo(400));
            Assert.That(allocation.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
            Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
        }

        [TestCase("resourceAmount", "\"400\"")]
        [TestCase("resourceAmountMode", "7")]
        [TestCase("countMode", "7")]
        public void ResourceSlotSchemaFailure_ReportsExactAllowlistedField(string field, string literal)
        {
            var json = JObject.Parse(Berries); json["nodes"][0][field] = JToken.Parse(literal);
            var parsed = CommanderSemanticJson.ParseProviderResponse(json.ToString());
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(parsed.SchemaDiagnostic, Does.Contain("field=nodes[0]." + field));
        }
    }
}
