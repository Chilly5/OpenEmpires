using System.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E1")]
    public sealed class CommanderPhase4E1SemanticJsonTests
    {
        // Mutation caught: an accepted unit name/count must survive as detached typed data.
        [Test]
        public void Parse_Request_ProducesTypedSpearmanNode()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(result.Nodes.Count, Is.EqualTo(1));
            Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.EnsureUnitCount));
            Assert.That(result.Nodes[0].UnitType, Is.EqualTo(1));
            Assert.That(result.Nodes[0].Count, Is.EqualTo(10));
            Assert.That(result.Nodes[0].BuildingType, Is.Null);
            Assert.That(result.Nodes[0].ResourceType, Is.Null);
            Assert.That(result.Nodes[0].StrategicObjectiveType, Is.Null);
        }

        // Mutation caught: a strategic objective must remain a recommendation-shaped node, not a tactical unit.
        [Test]
        public void Parse_Request_ProducesTypedRangedStrategicNode()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes.Count, Is.EqualTo(1));
            Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.StrategicObjective));
            Assert.That(result.Nodes[0].StrategicObjectiveType, Is.EqualTo(StrategicObjectiveType.RangedReinforcement));
            Assert.That(result.Nodes[0].Count, Is.Null);
            Assert.That(result.Nodes[0].UnitType, Is.Null);
        }

        [TestCase("Clarify")]
        [TestCase("Unsupported")]
        public void Parse_NonRequestOutcome_ContainsNoNodesAndBoundedExplanation(string outcome)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"" + outcome + "\",\"message\":\"Please choose one.\"}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Outcome.ToString(), Is.EqualTo(outcome));
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Is.EqualTo("Please choose one."));
        }

        // Mutation caught: a valid resource allocation admits zero workers, then admission checks population.
        [Test]
        public void Parse_ResourceAllocation_AcceptsZeroWorkers()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"SetResourceAllocation\",\"resource\":\"Wood\",\"count\":0}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes[0].ResourceType, Is.EqualTo(ResourceType.Wood));
            Assert.That(result.Nodes[0].Count, Is.Zero);
        }

        [Test]
        public void Parse_Structure_ProducesTypedBuildingAndCount()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":20}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes[0].BuildingType, Is.EqualTo(BuildingType.Barracks));
            Assert.That(result.Nodes[0].Count, Is.EqualTo(20));
        }

        // Each fixture catches a distinct parser relaxation; no invalid input may retain a partial node.
        [TestCase("{\"outcome\":\"Request\",\"outcome\":\"Clarify\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}", TestName = "RejectsDuplicateRootProperty")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"count\":11}]}", TestName = "RejectsDuplicateNodeProperty")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}{}", TestName = "RejectsTrailingJson")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}],\"extra\":1}", TestName = "RejectsUnknownRootField")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"playerId\":0}]}", TestName = "RejectsAuthorityField")]
        [TestCase("{\"outcome\":\"Request\",/* comment */\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}", TestName = "RejectsComment")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":NaN}]}", TestName = "RejectsNaN")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":-1}]}", TestName = "RejectsNegativeUnitCount")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":201}]}", TestName = "RejectsUnitCountAbove200")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":21}]}", TestName = "RejectsStructureCountAbove20")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Dragon\",\"count\":10}]}", TestName = "RejectsUnsupportedUnitEnum")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"999\"}]}", TestName = "RejectsNumericEnumString")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"spearman\",\"count\":10}]}", TestName = "RejectsWrongEnumCase")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"resource\":\"Wood\"}]}", TestName = "RejectsWrongFieldForNodeType")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}],\"message\":\"hi\"}", TestName = "RejectsMessageForRequest")]
        [TestCase("{\"outcome\":\"Clarify\",\"nodes\":[]}", TestName = "RejectsNodesPropertyForClarify")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,}]}", TestName = "RejectsTrailingComma")]
        [TestCase("{\"outcome\":\"request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}", TestName = "RejectsWrongOutcomeCase")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"DragonLair\",\"count\":1}]}", TestName = "RejectsUnknownStructureEnum")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"SetResourceAllocation\",\"resource\":\"Mana\",\"count\":1}]}", TestName = "RejectsUnknownResourceEnum")]
        [TestCase("{\"outcome\":\"Unsupported\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}", TestName = "RejectsNonRequestNodes")]
        public void Parse_InvalidPayload_RejectsWithoutPartialRequest(string raw)
        {
            var result = CommanderSemanticJson.Parse(raw);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_RejectsMoreThanFourNodesWithoutPartialRequest()
        {
            const string node = "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}";
            var raw = "{\"outcome\":\"Request\",\"nodes\":[" + string.Join(",", Enumerable.Repeat(node, 5)) + "]}";

            var result = CommanderSemanticJson.Parse(raw);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_RejectsOverlongOrMarkupExplanation()
        {
            var overlong = CommanderSemanticJson.Parse("{\"outcome\":\"Clarify\",\"message\":\"" + new string('x', 181) + "\"}");
            var markup = CommanderSemanticJson.Parse("{\"outcome\":\"Unsupported\",\"message\":\"<b>Denied</b>\"}");

            Assert.That(overlong.IsValid, Is.False);
            Assert.That(markup.IsValid, Is.False);
        }

        [Test]
        public void Parse_RejectsRawResponseOver8192Characters()
        {
            var raw = "{\"outcome\":\"Clarify\"}" + new string(' ', 8193);

            var result = CommanderSemanticJson.Parse(raw);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_RejectsJsonDeeperThanEightLevels()
        {
            var raw = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"extra\":"
                + new string('[', 9) + "0" + new string(']', 9) + "}]}";

            var result = CommanderSemanticJson.Parse(raw);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }
    }
}
