using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixReadablePreviewTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        [SetUp] public void SetUp()
        {
            config=ScriptableObject.CreateInstance<SimulationConfig>();
            sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
            manager=new CommanderGoalManager(sim,0);
        }
        [TearDown] public void TearDown(){manager.Dispose();UnityEngine.Object.DestroyImmediate(config);}
        private string Prepare(string json)
        {
            var parsed=CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid,Is.True,parsed.SafeExplanation);
            string preview=manager.PrepareActionPlan(parsed,"Explicit focused preview fixture",1).Preview;
            Assert.That(preview.Length,Is.LessThanOrEqualTo(8192));
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
            return preview;
        }
        private static string Render(CommanderIntent intent)=>(string)typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderPlanPreview")
            .GetMethod("RenderIntent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{intent});

        [Test] public void SharedWorkers_ExplainsFixedDisjointRolesWithoutSymbolicDump()
        {
            string preview=Prepare(CommanderPhase5ADynamicCompilerTests.SharedWorkers);
            Assert.That(preview,Does.Contain("3 eligible idle villagers"));
            Assert.That(preview,Does.Contain("villagers 1–2"));Assert.That(preview,Does.Contain("villager 3"));
            Assert.That(preview,Does.Contain("Food only from Sheep"));Assert.That(preview,Does.Contain("Build 1 Mill"));
            Assert.That(preview,Does.Contain("selection 2"));Assert.That(preview,Does.Contain("selection 3"));
            Assert.That(preview,Does.Contain("cannot overlap"));
            Assert.That(preview,Does.Not.Contain("select-workers"));Assert.That(preview,Does.Not.Contain("offset="));
            Assert.That(preview,Does.Not.Contain("result:"));Assert.That(preview,Does.Not.Contain("sheepWorkers"));
        }
        [Test] public void NewProducers_ExplainsNamedExactBuildingsWithoutFallback()
        {
            string preview=Prepare(CommanderPhase5ADynamicPlanTests.Producers);
            Assert.That(preview,Does.Contain("Produce 10 new Spearman"));
            Assert.That(preview,Does.Contain("only the Barracks built in step 1"));
            Assert.That(preview,Does.Contain("all 2 buildings from that step"));
            Assert.That(preview,Does.Contain("no other producer"));
            Assert.That(preview,Does.Not.Contain("producers=result"));Assert.That(preview,Does.Not.Contain("unit:1"));
        }
        [Test] public void Location_ExplainsVisibleResourceAndGapWithoutRawNodeIds()
        {
            string preview=Prepare(CommanderPhase5ADynamicPlanTests.Mill);
            Assert.That(preview,Does.Contain("visible Food only from Berries"));
            Assert.That(preview,Does.Contain("Near"));Assert.That(preview,Does.Contain("1-tile clear gap between footprints"));
            Assert.That(preview,Does.Contain("location 2"));Assert.That(preview,Does.Not.Contain("clearGapTiles="));
        }
        [TestCase(CommanderWorkerAllocationMode.SelectedCount,"Assign exactly 4 eligible idle villagers")]
        [TestCase(CommanderWorkerAllocationMode.Additional,"Assign 4 additional eligible idle villagers")]
        [TestCase(CommanderWorkerAllocationMode.TargetTotal,"Target 4 villagers in total")]
        public void WorkerQuantityModes_HaveDistinctPlainMeanings(CommanderWorkerAllocationMode mode,string expected)
        {
            var state=mode==CommanderWorkerAllocationMode.TargetTotal?CommanderWorkerState.Any:CommanderWorkerState.Idle;
            var intent=new AllocateWorkersIntent(0,new CommanderWorkerAllocation(mode,CommanderWorkerCountMode.Exact,4,
                new CommanderWorkerSelector(state),new CommanderResourceDestination(ResourceType.Food,ResourceSourceKind.Sheep)));
            string preview=Render(intent);
            Assert.That(preview,Does.Contain(expected));Assert.That(preview,Does.Contain("Food only from Sheep"));
            Assert.That(preview,Does.Not.Contain(mode.ToString()));
        }
        [Test] public void AllMatchingWorkers_ExplainsOneTimeSnapshotNotMaintainedTarget()
        {
            var intent=new AllocateWorkersIntent(0,new CommanderWorkerAllocation(CommanderWorkerAllocationMode.SelectedCount,
                CommanderWorkerCountMode.AllMatching,null,new CommanderWorkerSelector(CommanderWorkerState.Idle),
                new CommanderResourceDestination(ResourceType.Wood)));
            string preview=Render(intent);
            Assert.That(preview,Does.Contain("all currently eligible idle villagers"));Assert.That(preview,Does.Contain("one-time snapshot"));
            Assert.That(preview,Does.Not.Contain("AllMatching"));
        }
        [Test] public void NamedAttack_SeparatesOwnedActorsFromVisibleEnemyFamily()
        {
            var intent=new CapabilityActionIntent(0,CommanderCapabilityActionType.AttackTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType,3,1),new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector:new CommanderTargetSelector(2));
            string preview=Render(intent);
            Assert.That(preview,Does.Contain("Attack"));Assert.That(preview,Does.Contain("3 owned Spearman"));
            Assert.That(preview,Does.Contain("visible enemy Archer"));Assert.That(preview,Does.Not.Contain("AttackTarget"));
            Assert.That(preview,Does.Not.Contain("unit type 1"));
        }
        [Test] public void ExactResult_ExplainsNewUnitsAndNeverSubstitutesExistingUnits()
        {
            string preview=Prepare("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"quantityMode\":\"New\"},{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            Assert.That(preview,Does.Contain("only the 3 new Spearman produced in step 1"));
            Assert.That(preview,Does.Contain("no existing or human-produced units"));
            Assert.That(preview,Does.Contain("point patrol"));Assert.That(preview,Does.Contain("after step 1"));
        }
        [Test] public void NegativeAndPreparationConstraints_ArePlayerReadable()
        {
            var intent=new EnsureUnitCountIntent(0,1,4,new CommanderConstraint[]{new NoConstructionConstraint(),
                new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly),new ProtectedResourceConstraint(ResourceType.Wood,2),new MaximumQueueConstraint(1)});
            string preview=Render(intent);
            Assert.That(preview,Does.Contain("construction forbidden (new and resumed)"));
            Assert.That(preview,Does.Contain("use only idle villagers"));Assert.That(preview,Does.Contain("keep at least 2 villagers gathering Wood"));
            Assert.That(preview,Does.Contain("at most 1 queued"));Assert.That(preview,Does.Not.Contain("IdleOnly"));
            Assert.That(preview,Does.Contain("No unrelated strategy"));
        }
        [Test] public void LegacyIncreaseWithoutCount_DisclosesActualDefaultOne()
        {
            string preview=Render(new SetResourceAllocationIntent(0,ResourceType.Wood,ResourceAllocationMode.Increase,null));
            Assert.That(preview,Does.Contain("Assign 1 additional villagers"));
        }
        [Test] public void RelativePoint_DisclosesSelectedGroupCenterNotAnotherGroupRear()
        {
            string preview=Render(new CapabilityActionIntent(0,CommanderCapabilityActionType.MoveUnits,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military,2),new CommanderLocationSelector(CommanderLocationSelectorKind.RelativeToSelectedUnits)));
            Assert.That(preview,Does.Contain("the center of the selected units"));
        }
        [Test] public void ExactResultRally_UsesTheBuiltStructureTypeNotDefaultBarracks()
        {
            string preview=Prepare("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"ArcheryRange\",\"count\":1},{\"type\":\"SetRallyPoint\",\"unitSelector\":\"Military\",\"count\":1,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            Assert.That(preview,Does.Contain("rally point of the exact Archery Range"));
            Assert.That(preview,Does.Not.Contain("Barracks"));
        }
        [Test] public void DynamicSharedSourceRestriction_IsDisclosedEvenWithoutProduction()
        {
            string preview=Prepare("{\"outcome\":\"DynamicPlan\",\"version\":1,\"constraints\":[{\"type\":\"ResourceSource\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}],\"nodes\":[{\"id\":\"workers\",\"mechanic\":\"select-workers\",\"parameters\":{\"count\":1,\"state\":\"Idle\"},\"inputs\":{},\"dependsOn\":[]},{\"id\":\"gather\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Food\",\"sourceKind\":\"Any\"},\"inputs\":{\"workers\":\"workers\"},\"dependsOn\":[\"workers\"]}]}");
            Assert.That(preview,Does.Contain("Shared restrictions"));
            Assert.That(preview,Does.Contain("Wood only from Tree"));
        }
        [Test] public void TypedPlacementOmittedGap_DisclosesActualDefaultOneTile()
        {
            var intent=new BuildStructureIntent(0,BuildingType.Barracks,placementAnchorSelector:CommanderSemanticAnchorSelector.MyTownCenter,
                placementRelation:CommanderSemanticPlacementRelation.MapWest);
            Assert.That(new CommanderIntentValidator().Validate(intent,sim,0).IsValid,Is.True);
            string preview=Render(intent);
            Assert.That(preview,Does.Contain("1-tile clear gap between footprints"));
            Assert.That(preview,Does.Contain("map-west (left)"));
        }
        [Test] public void SelectedExistingProducers_DiscloseVisibleCompletedOwnedRequirement()
        {
            string preview=Prepare("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"b\",\"mechanic\":\"select-structures\",\"parameters\":{\"building\":\"building:Barracks\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},{\"id\":\"p\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":2,\"quantityMode\":\"New\"},\"inputs\":{\"producers\":\"b\"},\"dependsOn\":[\"b\"]}]}");
            Assert.That(preview,Does.Contain("visible completed owned Barracks"));
            Assert.That(preview,Does.Contain("no other producer"));
        }
    }
}
