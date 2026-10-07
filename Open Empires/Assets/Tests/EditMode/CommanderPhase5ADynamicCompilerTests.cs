using System;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5ADynamicCompilerTests
    {
        internal const string SharedWorkers = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"idle\",\"mechanic\":\"select-workers\",\"parameters\":{\"count\":3,\"state\":\"Idle\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"sheepWorkers\",\"mechanic\":\"partition-workers\",\"parameters\":{\"offset\":0,\"count\":2},\"inputs\":{\"workers\":\"idle\"},\"dependsOn\":[\"idle\"]},"
            + "{\"id\":\"millWorker\",\"mechanic\":\"partition-workers\",\"parameters\":{\"offset\":2,\"count\":1},\"inputs\":{\"workers\":\"idle\"},\"dependsOn\":[\"idle\"]},"
            + "{\"id\":\"sheep\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"inputs\":{\"workers\":\"sheepWorkers\"},\"dependsOn\":[\"sheepWorkers\"]},"
            + "{\"id\":\"mill\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Mill\",\"count\":1},\"inputs\":{\"workers\":\"millWorker\"},\"dependsOn\":[\"millWorker\"]}]}";

        [Test]
        public void PrepareActionPlan_VisibleBerriesLocationCompilesToSymbolicallyBoundMill()
        {
            WithManager((sim, goals) =>
            {
                var interpretation = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill);
                Assert.That(interpretation.IsValid, Is.True);
                var candidate = goals.PrepareActionPlan(interpretation, "build a mill near visible berries", 1);
                Assert.That(candidate.Graph.DynamicProgram, Is.SameAs(interpretation.DynamicPlan));
                Assert.That(candidate.Graph.Nodes, Has.Count.EqualTo(1));
                Assert.That(candidate.Graph.Nodes[0].DynamicNodeId, Is.EqualTo("mill"));
                Assert.That(candidate.Graph.Nodes[0].Intent, Is.TypeOf<BuildStructureIntent>());
                Assert.That(((BuildStructureIntent)candidate.Graph.Nodes[0].Intent).StructureType,
                    Is.EqualTo(BuildingType.Mill));
                Assert.That(candidate.Graph.Nodes[0].DependsOn, Is.Empty,
                    "Pure resource and location nodes remain in DynamicProgram, not tactical effects.");
                AssertNoInitialWork(sim, goals);
            });
        }

        [Test]
        public void PrepareActionPlan_SharedIdleThreePartitionsTwoSheepOneMillWithoutWorkerIds()
        {
            WithManager((sim, goals) =>
            {
                var interpretation = CommanderSemanticJson.Parse(SharedWorkers);
                Assert.That(interpretation.IsValid, Is.True);
                var candidate = goals.PrepareActionPlan(interpretation,
                    "use three idle workers: two on sheep and one on a mill", 1);
                Assert.That(candidate.Graph.DynamicProgram, Is.SameAs(interpretation.DynamicPlan));
                Assert.That(candidate.Graph.Nodes, Has.Count.EqualTo(2));
                Assert.That(candidate.Graph.Nodes[0].DynamicNodeId, Is.EqualTo("sheep"));
                Assert.That(candidate.Graph.Nodes[1].DynamicNodeId, Is.EqualTo("mill"));
                Assert.That(candidate.Graph.Nodes[0].Intent, Is.TypeOf<AllocateWorkersIntent>());
                var allocation = ((AllocateWorkersIntent)candidate.Graph.Nodes[0].Intent).Allocation;
                Assert.That(allocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(allocation.CountMode, Is.EqualTo(CommanderWorkerCountMode.Exact));
                Assert.That(allocation.Count, Is.EqualTo(2));
                Assert.That(allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(allocation.Destination.Resource, Is.EqualTo(ResourceType.Food));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(candidate.Graph.Nodes[1].Intent, Is.TypeOf<BuildStructureIntent>());
                Assert.That(((BuildStructureIntent)candidate.Graph.Nodes[1].Intent).StructureType,
                    Is.EqualTo(BuildingType.Mill));
                Assert.That(candidate.Graph.Nodes[0].DependsOn, Is.Empty);
                Assert.That(candidate.Graph.Nodes[1].DependsOn, Is.Empty);
                AssertNoInitialWork(sim, goals);
            });
        }

        [Test]
        public void PrepareActionPlan_TwoNewBarracksProduceTenNewSpearmenFromExactProducerSet()
        {
            WithManager((sim, goals) =>
            {
                var interpretation = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Producers);
                Assert.That(interpretation.IsValid, Is.True);
                var candidate = goals.PrepareActionPlan(interpretation,
                    "build two barracks, then train ten new spearmen in those barracks", 1);
                Assert.That(candidate.Graph.DynamicProgram, Is.SameAs(interpretation.DynamicPlan));
                Assert.That(candidate.Graph.Nodes, Has.Count.EqualTo(2));
                Assert.That(candidate.Graph.Nodes[0].DynamicNodeId, Is.EqualTo("barracks"));
                Assert.That(candidate.Graph.Nodes[1].DynamicNodeId, Is.EqualTo("spears"));
                Assert.That(candidate.Graph.Nodes[0].Intent, Is.TypeOf<BuildStructureIntent>());
                var build = (BuildStructureIntent)candidate.Graph.Nodes[0].Intent;
                Assert.That(build.StructureType, Is.EqualTo(BuildingType.Barracks));
                Assert.That(build.Count, Is.EqualTo(2));
                Assert.That(candidate.Graph.Nodes[1].Intent, Is.TypeOf<EnsureUnitCountIntent>());
                var produce = (EnsureUnitCountIntent)candidate.Graph.Nodes[1].Intent;
                Assert.That(produce.UnitType, Is.EqualTo(1));
                Assert.That(produce.NewProductionCount, Is.EqualTo(10));
                Assert.That(candidate.Graph.Nodes[1].ProducerFromNode, Is.EqualTo(0));
                Assert.That(candidate.Graph.Nodes[1].DependsOn, Is.EquivalentTo(new[] { 0 }));
                AssertNoInitialWork(sim, goals);
            });
        }

        private static void WithManager(Action<GameSimulation, CommanderGoalManager> check)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var goals = new CommanderGoalManager(simulation, 0);
                check(simulation, goals);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void AssertNoInitialWork(GameSimulation simulation, CommanderGoalManager goals)
        {
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }
    }
}
