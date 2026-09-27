using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E3")]
    public sealed class CommanderPhase4E3GraphContractTests
    {
        [Test]
        public void Parse_CompoundBarracksThenSpearmen_ProducesBoundedTypedEdge()
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"dependsOn\":[0],\"producerFromNode\":0}]}" );

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes, Has.Count.EqualTo(2));
            Assert.That(result.Nodes[0].DependsOn, Is.Empty);
            Assert.That(result.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
            Assert.That(result.Nodes[1].ProducerFromNode, Is.EqualTo(0));
        }

        [Test]
        public void Parse_CompoundWithoutExplicitDependency_NormalizesProducerEdge()
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
        }

        [TestCase("dependsOn\":[2]")]
        [TestCase("dependsOn\":[-1]")]
        [TestCase("dependsOn\":[0,0]")]
        [TestCase("producerFromNode\":1")]
        [TestCase("producerFromNode\":-1")]
        [TestCase("producerFromNode\":99")]
        public void Parse_InvalidReferenceShape_RejectsAtomically(string reference)
        {
            var raw = "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10," + reference + "}] }";
            var result = CommanderSemanticJson.Parse(raw);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_CyclicDependencies_RejectsAtomically()
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"dependsOn\":[1]}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"dependsOn\":[0],\"producerFromNode\":0}]}" );
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_IncompatibleProducerReference_RejectsAtomically()
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test]
        public void Parse_ConcreteAuthorityFieldsRemainRejected()
        {
            var result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"entityId\":42}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }
    }
}
