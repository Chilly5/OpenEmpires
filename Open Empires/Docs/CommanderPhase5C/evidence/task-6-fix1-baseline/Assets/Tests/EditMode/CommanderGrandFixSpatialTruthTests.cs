using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixSpatialTruthTests
    {
        private SimulationConfig config;private GameSimulation sim;private CommanderGoalManager manager;
        [SetUp]public void SetUp()
        {
            config=ScriptableObject.CreateInstance<SimulationConfig>();sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
            int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
            sim.CreateBuilding(0,BuildingType.TownCenter,x,z,false,true);
            var unit=sim.UnitRegistry.CreateUnit(0,sim.MapData.TileToWorldFixed(x-4,z),Fixed32.One,Fixed32.One,Fixed32.One);
            unit.UnitType=1;unit.CurrentHealth=unit.MaxHealth=100;
            manager=new CommanderGoalManager(sim,0);
        }
        [TearDown]public void TearDown(){manager.Dispose();UnityEngine.Object.DestroyImmediate(config);}
        private static CapabilityActionIntent Point(CommanderCapabilityActionType action)
            =>new CapabilityActionIntent(0,action,new CommanderUnitSelector(CommanderUnitSelectorKind.Military),new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
        private static CapabilityActionIntent Radius(CommanderCapabilityActionType action,int radius)
            =>new CapabilityActionIntent(0,action,new CommanderUnitSelector(CommanderUnitSelectorKind.Military),new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase,radiusTiles:radius));
        private static string Preview(CommanderIntent intent)=>(string)typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderPlanPreview")
            .GetMethod("RenderIntent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{intent});

        [Test]public void DefaultPatrolPreview_DescribesPointRouteAndNeverInventsRadius()
        {
            string text=Preview(Point(CommanderCapabilityActionType.PatrolArea)).ToLowerInvariant();
            Assert.That(text,Does.Not.Contain("radius"));Assert.That(text,Does.Not.Contain("perimeter"));
            Assert.That(text,Does.Contain("point"));
        }
        [TestCase(CommanderCapabilityActionType.PatrolArea)][TestCase(CommanderCapabilityActionType.MoveUnits)]
        [TestCase(CommanderCapabilityActionType.DefendArea)]
        public void ExplicitRadius_IsRejectedByValidationAndExecutorBeforeEffects(CommanderCapabilityActionType action)
        {
            var intent=Radius(action,5);
            var validation=new CommanderIntentValidator().Validate(intent,sim,0);
            Assert.That(validation.IsValid,Is.False);
            Assert.That(validation.Reason.ToLowerInvariant(),Does.Contain("radius"));
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent,out var command,out var reason),Is.False);
            Assert.That(command,Is.Null);Assert.That(reason.ToLowerInvariant(),Does.Contain("radius"));
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
        [Test]public void ExplicitLegacyFour_IsNotEquivalentToOmittedPointSemantics()
        {
            var method=typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderScopeEquivalence").GetMethod("SameIntent",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method.Invoke(null,new object[]{Point(CommanderCapabilityActionType.PatrolArea),Radius(CommanderCapabilityActionType.PatrolArea,4)}),Is.False);
        }
        [Test]public void UnsupportedRadius_CannotDisappearDuringDtoProjection()
        { Assert.Throws<ArgumentException>(()=>CommanderIntentDtoCodec.FromIntent(Radius(CommanderCapabilityActionType.PatrolArea,5))); }
        [Test]public void PointPatrol_UsesNormalPatrolCommandWithNoAroundPromise()
        {
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(Point(CommanderCapabilityActionType.PatrolArea),out var command,out var reason),Is.True);
            Assert.That(command,Is.TypeOf<PatrolCommand>());Assert.That(reason.ToLowerInvariant(),Does.Not.Contain("around"));
        }
        [TestCase("radius")][TestCase("radiusTiles")][TestCase("perimeter")]
        public void ProviderExplicitSpatialRestrictions_RejectRatherThanDropUnknownConstraint(string field)
        {
            var result=CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"PatrolArea\",\"unitSelector\":\"Military\",\"count\":1,\"location\":\"PlayerBase\",\""+field+"\":5}]}");
            Assert.That(result.IsValid,Is.False);Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
        [Test]public void BuildingFootprintGap_RemainsSeparateFromUnsupportedActionRadius()
        {
            var result=CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}]}");
            Assert.That(result.IsValid,Is.True,result.SafeExplanation);
            Assert.That(result.Nodes[0].ClearGapTiles,Is.EqualTo(5));
        }

        [TestCase("radius")][TestCase("radiusTiles")][TestCase("perimeter")]
        public void DirectDtoTacticalParameters_RejectInsteadOfLosingRestriction(string field)
        {
            var context=new CommanderContextBuilder().Build(sim,manager);
            var result=CommanderIntentDtoCodec.InterpretJson("{\"intentType\":\"CapabilityAction\",\"action\":\"PatrolArea\",\"unit\":\"Military\",\"location\":\"PlayerBase\",\"amount\":1,\"parameters\":{\""+field+"\":\"5\"}}",context);
            Assert.That(result.Success,Is.False);
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
        [TestCase("[]")][TestCase("\"radius 5\"")]
        public void DirectDtoMalformedParameters_RejectInsteadOfBecomingPointPatrol(string value)
        {
            var context=new CommanderContextBuilder().Build(sim,manager);
            var result=CommanderIntentDtoCodec.InterpretJson("{\"intentType\":\"CapabilityAction\",\"action\":\"PatrolArea\",\"unit\":\"Military\",\"location\":\"PlayerBase\",\"amount\":1,\"parameters\":"+value+"}",context);
            Assert.That(result.Success,Is.False);
        }
        [Test]public void DirectDtoEmptyTacticalParameters_PreservePointPatrol()
        {
            var context=new CommanderContextBuilder().Build(sim,manager);
            var result=CommanderIntentDtoCodec.InterpretJson("{\"intentType\":\"CapabilityAction\",\"action\":\"PatrolArea\",\"unit\":\"Military\",\"location\":\"PlayerBase\",\"amount\":1,\"parameters\":{}}",context);
            Assert.That(result.Success,Is.True);
            Assert.That(result.Intent,Is.TypeOf<CapabilityActionIntent>());
        }
        [Test]public void DirectDtoStrategicParameters_ArePreservedWithoutGameplayEffects()
        {
            var context=new CommanderContextBuilder().Build(sim,manager);
            var result=CommanderIntentDtoCodec.InterpretJson("{\"intentCategory\":\"Strategic\",\"objectiveType\":\"AttackPreparation\",\"parameters\":{\"targetCount\":\"5\"}}",context);
            Assert.That(result.Success,Is.True);
            Assert.That(result.StrategicIntent.Parameters["targetCount"],Is.EqualTo("5"));
            Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
        }
    }
}
