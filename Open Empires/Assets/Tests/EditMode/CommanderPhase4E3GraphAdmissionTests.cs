using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    // Phase 4E.3 atomic capacity guard.
    [Category("CommanderPhase4E3")]
    public sealed class CommanderPhase4E3GraphAdmissionTests
    {
        [Test]
        public void Admission_ValidGraphProducesDependencyFirstPlanWithoutEntitySelection()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                    "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );

                bool admitted = CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason);

                Assert.That(admitted, Is.True, reason);
                Assert.That(plan.TopologicalOrder, Is.EqualTo(new[] { 0, 1 }));
                Assert.That(plan.Nodes[1].ProducerFromNode, Is.EqualTo(0));
                Assert.That(plan.Nodes[1].Intent, Is.TypeOf<EnsureUnitCountIntent>());
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Admission_StrategicNodeInCompoundGraphIsRejectedWithoutMutation()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                    "{\"type\":\"StrategicObjective\",\"objective\":\"MilitaryReinforcement\"}]}" );

                bool admitted = CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out _);

                Assert.That(admitted, Is.False);
                Assert.That(plan, Is.Null);
                Assert.That(manager.ActiveGoals, Is.Empty);
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SubmitSemanticGraph_CommitsBothGoalsAndBindsExactProducerGoal()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}," +
                    "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);

                var submitted = manager.SubmitSemanticGraph(plan);

                Assert.That(submitted, Has.Count.EqualTo(2));
                var build = submitted[0] as BuildStructureGoal;
                var units = submitted[1] as EnsureUnitCountGoal;
                Assert.That(build, Is.Not.Null);
                Assert.That(units, Is.Not.Null);
                Assert.That(units.RequiredProducerGoal, Is.SameAs(build));
                Assert.That(manager.ActiveGoals, Has.Count.EqualTo(2));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SubmitSemanticGraph_WhenCapacityIsFullCreatesNoPartialGoals()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                for (int i = 0; i < CommanderGoalManager.MaxActiveGoals; i++)
                    manager.SubmitEnsureUnitCount(CommanderIntentCatalog.SpearmanUnitType, 0);
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                    "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);

                Assert.Throws< System.InvalidOperationException>(() => manager.SubmitSemanticGraph(plan));
                Assert.That(manager.ActiveGoals, Has.Count.EqualTo(CommanderGoalManager.MaxActiveGoals));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }
    }
}
