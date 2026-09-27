using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E2")]
    public sealed class CommanderPhase4E2SemanticPlacementContractTests
    {
        // Mutation caught: placement language is represented as typed semantic data, not provider coordinates.
        [TestCase("MapWest")]
        [TestCase("MapEast")]
        public void Parse_BuildPlacement_AcceptsBoundedRelationTokens(string relation)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"" + relation + "\",\"clearGapTiles\":5}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "PlacementAnchorSelector").ToString(), Is.EqualTo("MyTownCenter"));
            Assert.That(GetNodeProperty(result.Nodes[0], "PlacementRelation").ToString(), Is.EqualTo(relation));
            Assert.That(GetNodeProperty(result.Nodes[0], "ClearGapTiles"), Is.EqualTo(5));
            Assert.That(GetNodeProperty(result.Nodes[0], "PlacementAnchorOrdinal"), Is.Null);
        }

        [Test]
        public void Parse_Near_ExplicitOneClearTileIsConsistentWithApprovedMeaning()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "ClearGapTiles"), Is.EqualTo(1));
        }

        [TestCase("MapWest")]
        [TestCase("MapEast")]
        [TestCase("Near")]
        public void Parse_PlacementWithoutDistance_LeavesGameSideDefaultUnset(string relation)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"" + relation + "\"}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "ClearGapTiles"), Is.Null,
                "An omitted distance must be chosen by deterministic game code, not invented by the model.");
        }

        [Test]
        public void Parse_NearMyBarracks_AcceptsOwnedSemanticAnchorWithoutId()
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"ArcheryRange\",\"count\":1,\"placement\":{\"anchor\":\"MyBarracks\",\"relation\":\"Near\"}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "PlacementAnchorSelector").ToString(),
                Is.EqualTo("MyBarracks"));
            Assert.That(GetNodeProperty(result.Nodes[0], "ClearGapTiles"), Is.Null);
        }

        // Mutation caught: ordinal selection is optional, positive, and bounded before game-side resolution.
        [TestCase(1)]
        [TestCase(8)]
        public void Parse_MyTownCenterOrdinal_AcceptsInclusiveBounds(int ordinal)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"ordinal\":" + ordinal + ",\"relation\":\"MapWest\",\"clearGapTiles\":5}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "PlacementAnchorOrdinal"), Is.EqualTo(ordinal));
        }

        // Mutation caught: the requested distance means clear intervening footprint tiles and remains bounded.
        [TestCase(1)]
        [TestCase(20)]
        public void Parse_ClearGapTiles_AcceptsInclusiveIntegerBounds(int gap)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":" + gap + "}}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(GetNodeProperty(result.Nodes[0], "ClearGapTiles"), Is.EqualTo(gap));
        }

        // Mutation caught: malformed semantic fields and any provider-authored concrete authority are rejected atomically.
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"CameraLeft\",\"clearGapTiles\":5}", TestName = "RejectsUnknownRelation")]
        [TestCase("{\"anchor\":\"NearestEnemyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}", TestName = "RejectsUnknownSelector")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"ordinal\":0,\"relation\":\"MapWest\",\"clearGapTiles\":5}", TestName = "RejectsOrdinalBelowBound")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"ordinal\":9,\"relation\":\"MapWest\",\"clearGapTiles\":5}", TestName = "RejectsOrdinalAboveBound")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":-1}", TestName = "RejectsNegativeClearGap")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":0}", TestName = "RejectsZeroExplicitClearGap")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":21}", TestName = "RejectsClearGapAboveApprovedBound")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":5}", TestName = "RejectsNearFiveTileContradiction")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5.0}", TestName = "RejectsNonIntegerClearGap")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5,\"extra\":1}", TestName = "RejectsExtraPlacementField")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}", TestName = "RejectsDuplicatePlacementField")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5,\"x\":10,\"z\":20}", TestName = "RejectsProviderCoordinates")]
        [TestCase("{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5,\"buildingId\":47}", TestName = "RejectsProviderEntityId")]
        public void Parse_InvalidPlacementContract_RejectsWithoutPartialRequest(string placement)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":" + placement + "}]}");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        // Mutation caught: extending the BuildStructure contract must not break existing tactical or strategic nodes.
        [Test]
        public void Parse_ExistingTacticalAndStrategicForms_RemainValid()
        {
            var tactical = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            var strategic = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");
            var unplacedBuild = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}]}");

            Assert.That(tactical.IsValid, Is.True);
            Assert.That(tactical.Nodes[0].UnitType, Is.EqualTo(1));
            Assert.That(strategic.IsValid, Is.True);
            Assert.That(strategic.Nodes[0].StrategicObjectiveType, Is.EqualTo(StrategicObjectiveType.RangedReinforcement));
            Assert.That(unplacedBuild.IsValid, Is.True);
            Assert.That(GetNodeProperty(unplacedBuild.Nodes[0], "PlacementAnchorSelector"), Is.Null);
        }

        [Test]
        public void Admission_PlacedBuildFailsClosedUntilSpatialPlannerExists()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var goals = new CommanderGoalManager(simulation, 0);
            try
            {
                var context = new CommanderContextBuilder().Build(simulation, goals);
                var placed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}]}").Nodes[0];
                var unplaced = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}]} ").Nodes[0];

                bool placedAdmitted = CommanderSemanticAdmission.TryCreateTacticalIntent(placed, context,
                    out CommanderIntent placedIntent, out string placedReason);
                bool unplacedAdmitted = CommanderSemanticAdmission.TryCreateTacticalIntent(unplaced, context,
                    out CommanderIntent unplacedIntent, out string unplacedReason);

                Assert.That(placedAdmitted, Is.False);
                Assert.That(placedIntent, Is.Null);
                Assert.That(placedReason, Is.Not.Empty);
                Assert.That(unplacedAdmitted, Is.True, unplacedReason);
                Assert.That(unplacedIntent, Is.Not.Null);
            }
            finally
            {
                goals.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        private static object GetNodeProperty(CommanderSemanticNode node, string propertyName)
        {
            var property = typeof(CommanderSemanticNode).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Expected typed semantic node property " + propertyName);
            return property.GetValue(node);
        }
    }
}
