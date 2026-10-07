using System;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5ADynamicPlanTests
    {
        internal const string Farms = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":4},\"inputs\":{},\"dependsOn\":[]}]}";
        internal const string Mill = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"berries\",\"mechanic\":\"select-resources\",\"parameters\":{\"resource\":\"Food\",\"sourceKind\":\"Berries\",\"mode\":\"Visible\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"resources\":\"berries\"},\"dependsOn\":[\"berries\"]},"
            + "{\"id\":\"mill\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Mill\",\"count\":1},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
        internal const string Producers = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"barracks\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Barracks\",\"count\":2},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"spears\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":10,\"quantityMode\":\"New\"},\"inputs\":{\"producers\":\"barracks\"},\"dependsOn\":[\"barracks\"]}]}";

        [TestCase(Farms)]
        [TestCase(Mill)]
        [TestCase(Producers)]
        public void ExistingSemanticEntry_ParsesTypedCompositionWithoutAuthority(string json)
        {
            var result = CommanderSemanticJson.Parse(json);
            Assert.That(result.IsValid, Is.True, "Typed composition is a real semantic outcome, not executable code.");
            Assert.That(result.Outcome.ToString(), Is.EqualTo("DynamicPlan"));
            var property = result.GetType().GetProperty("DynamicPlan");
            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetValue(result), Is.Not.Null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var goals = new CommanderGoalManager(sim, 0);
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result,
                    new CommanderContextBuilder().Build(sim, goals), out _, out _), Is.False);
                Assert.That(goals.Goals, Is.Empty, "Structural parsing must not confer execution authority.");
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PrimitiveBounds_DoNotRelaxExistingConstructionOrUnitSelectionLimits()
        {
            Assert.That(CommanderSemanticJson.Parse(Farms.Replace("\"count\":4", "\"count\":21")).IsValid, Is.False);
            const string select = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"units\",\"mechanic\":\"select-units\",\"parameters\":{\"unit\":\"unit:1\",\"count\":51},\"inputs\":{},\"dependsOn\":[]}]}";
            Assert.That(CommanderSemanticJson.Parse(select).IsValid, Is.False);
            Assert.That(CommanderSemanticJson.Parse(Farms.Replace("\"id\":\"farms\"", "\"id\":\"farms:1\"")).IsValid,
                Is.False, "Graph identifiers use an unambiguous bounded ASCII grammar.");
        }

        [Test]
        public void StrictGraphAttackMatrix_RejectsWholeDataGraph()
        {
            foreach (string json in new[] {
                Farms.Replace("\"version\":1", "\"version\":2"),
                Farms.Replace("\"version\":1", "\"version\":\"1\""),
                Farms.Replace("\"version\":1", "\"version\":1,\"approved\":true"),
                Farms.Replace("\"count\":4", "\"count\":4,\"count\":5"),
                Farms.Replace("\"building:Farm\"", "\"building:0\""),
                Farms.Replace("\"count\":4", "\"count\":4,\"tileX\":5"),
                Farms.Replace("\"build\"", "\"InvokeMethod\""),
                Farms.Replace("\"farms\"", "\"farms\\uD800\""),
                Mill.Replace("\"dependsOn\":[\"berries\"]", "\"dependsOn\":[]"),
                Mill.Replace("\"resources\":\"berries\"", "\"resources\":\"missing\""),
                Mill.Replace("\"resources\":\"berries\"", "\"structures\":\"berries\""),
                Producers.Replace("\"dependsOn\":[]", "\"dependsOn\":[\"spears\"]"),
                Producers.Replace("\"unit:1\"", "\"unit:01\"") })
                Assert.That(CommanderSemanticJson.Parse(json).IsValid, Is.False, json);
        }

        [Test]
        public void DynamicCandidate_CompilesCanonicalFarmWithoutInitialWork()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var goals = new CommanderGoalManager(sim, 0);
                CommanderActionPlanCandidate candidate = null;
                Assert.DoesNotThrow(() => candidate = goals.PrepareActionPlan(
                    CommanderSemanticJson.Parse(Farms), "make 4 farms", 1));
                Assert.That(candidate, Is.Not.Null);
                Assert.That(candidate.Preview, Does.Contain("Farm"));
                Assert.That(candidate.Preview, Does.Contain("4"));
                Assert.That(goals.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                var graph = goals.ApproveActionPlan(candidate, 1);
                Assert.That(graph, Is.Not.Null);
                var admitted = goals.SubmitSemanticGraph(graph);
                Assert.That(admitted, Has.Count.EqualTo(1));
                Assert.That(((BuildStructureGoal)admitted[0]).StructureType, Is.EqualTo(BuildingType.Farm));
                Assert.That(((BuildStructureGoal)admitted[0]).Count, Is.EqualTo(4));
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Admission is not gameplay proof.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
