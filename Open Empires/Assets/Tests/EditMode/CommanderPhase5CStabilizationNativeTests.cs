using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CStabilizationNativeTests
    {
        private sealed class Fixture : IDisposable
        {
            internal readonly SimulationConfig Config = ScriptableObject.CreateInstance<SimulationConfig>();
            internal readonly GameSimulation Sim;
            internal readonly CommanderGoalManager Manager;
            internal readonly int X,Z;
            internal readonly System.Collections.Generic.List<int> AcceptedProducerIds = new System.Collections.Generic.List<int>();
            internal Fixture()
            {
                Sim=new GameSimulation(Config,1,new[]{0},Array.Empty<int>());
                Sim.SetPlayerAge(0,3);
                X=Sim.MapData.Width/2;Z=Sim.MapData.Height/2;
                typeof(MapData).GetField("holeMap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(Sim.MapData,null);
                foreach(var node in Sim.MapData.GetAllResourceNodes())node.RemainingAmount=0;
                for(int x=X-40;x<=X+40;x++)for(int z=Z-35;z<=Z+35;z++)
                {
                    Sim.MapData.Tiles[x,z]=TileType.Grass;Sim.MapData.ForestDensity[x,z]=0;
                    Sim.MapData.FoundationCount[x,z]=0;Sim.FogOfWar.SetVisible(0,x,z);
                }
                Sim.CreateBuilding(0,BuildingType.TownCenter,X,Z,false,true).AutoProduceVillagers=false;
                for(int i=0;i<13;i++)Sim.CreateBuilding(0,BuildingType.House,X-36+(i%7)*5,Z-29+(i/7)*5,false,true);
                for(int i=0;i<8;i++)
                {
                    var worker=Sim.UnitRegistry.CreateUnit(0,Sim.MapData.TileToWorldFixed(X+6+i,Z+6),Fixed32.One,Fixed32.One,Fixed32.One);
                    worker.UnitType=0;worker.IsVillager=true;worker.MaxHealth=worker.CurrentHealth=100;worker.State=UnitState.Idle;
                }
                var resources=Sim.ResourceManager.GetPlayerResources(0);
                resources.Wood=resources.Food=resources.Gold=resources.Stone=20000;
                Manager=new CommanderGoalManager(Sim,0);
                Sim.TrainingOrderAccepted += receipt => AcceptedProducerIds.Add(receipt.ProducerId);
            }
            internal CommanderGoal[] Admit(string response,string utterance)
            {
                var parsed=CommanderSemanticJson.ParseProviderResponse(response);
                Assert.That(parsed.IsValid,Is.True,parsed.SafeExplanation);
                var candidate=Manager.PrepareActionPlan(parsed,utterance,1);
                Assert.That(Manager.Goals,Is.Empty);
                var graph=Manager.ApproveActionPlan(candidate,1);
                Assert.That(graph,Is.Not.Null);
                return Manager.SubmitSemanticGraph(graph).ToArray();
            }
            internal void Complete(CommanderGoal[] goals,int maxTicks=20000)
            {
                for(int i=0;i<maxTicks&&goals.Any(g=>!g.IsTerminal);i++){Manager.Tick(Sim.CurrentTick);Sim.Tick();}
                foreach(var goal in goals)Assert.That(goal.Status,Is.EqualTo(CommanderGoalStatus.Completed),goal.GoalType+": "+goal.StatusReason);
                TestContext.WriteLine("Native normal-timer completion tick="+Sim.CurrentTick+"; goals="+string.Join(",",goals.Select(g=>g.GoalType+"/"+g.Status)));
            }
            public void Dispose(){Manager.Dispose();UnityEngine.Object.DestroyImmediate(Config);}
        }

        private static JObject Build(BuildingType type,int count)=>new JObject{
            ["type"]="BuildStructure",["structure"]=type.ToString(),["count"]=count,
            ["builders"]=new JObject{["state"]="Eligible",["count"]=1}};

        [TestCase(BuildingType.Stables)]
        [TestCase(BuildingType.ArcheryRange)]
        public void ThreeOrdinaryBuildings_CompleteDistinctLegalFootprints(BuildingType type)
        {
            using var f=new Fixture();
            var goals=f.Admit(new JObject{["outcome"]="Request",["nodes"]=new JArray(Build(type,3))}.ToString(),"Build 3 "+type);
            f.Complete(goals);
            var buildings=f.Sim.BuildingRegistry.GetAllBuildings().Where(b=>b.Type==type&&!b.IsDestroyed).ToArray();
            Assert.That(buildings,Has.Length.EqualTo(3));
            Assert.That(buildings.All(b=>!b.IsUnderConstruction),Is.True);
            AssertDistinct(buildings);
        }

        [Test]
        public void OrdinaryMillAndEightFarms_CompleteFullIndependentRequest()
        {
            using var f=new Fixture();
            var goals=f.Admit(new JObject{["outcome"]="Request",["executionOrder"]="Independent",
                ["nodes"]=new JArray(Build(BuildingType.Mill,1),Build(BuildingType.Farm,8))}.ToString(),"Build a Mill and 8 Farms");
            f.Complete(goals);
            var farms=f.Sim.BuildingRegistry.GetAllBuildings().Where(b=>b.Type==BuildingType.Farm&&!b.IsDestroyed).ToArray();
            Assert.That(farms,Has.Length.EqualTo(8));AssertDistinct(farms);
            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Count(b=>b.Type==BuildingType.Mill&&!b.IsUnderConstruction),Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OneNewBarracksThenFourSpearmen_WaitsForExactNewProducerAndPreservesQuantity(bool explicitlyNew)
        {
            using var f=new Fixture();
            var oldProducer=f.Sim.CreateBuilding(0,BuildingType.Barracks,f.X-20,f.Z+15,false,true);
            int baseline=explicitlyNew?2:0;
            for(int i=0;i<baseline;i++)
            {
                var old=f.Sim.UnitRegistry.CreateUnit(0,f.Sim.MapData.TileToWorldFixed(f.X+4+i,f.Z+3),Fixed32.One,Fixed32.One,Fixed32.One);
                old.UnitType=1;old.IsVillager=false;old.CurrentHealth=old.MaxHealth=100;old.State=UnitState.Idle;
            }
            var train=new JObject{["type"]="EnsureUnitCount",["unit"]="Spearman",["count"]=4,
                ["quantityMode"]=explicitlyNew?"New":"TargetTotal",["producerFromNode"]=0,["dependsOn"]=new JArray(0)};
            var goals=f.Admit(new JObject{["outcome"]="Request",["executionOrder"]="Sequential",
                ["nodes"]=new JArray(Build(BuildingType.Barracks,1),train)}.ToString(),"Build a barracks then train four spearmen from it");
            int trainTick=-1,buildingCompletedTick=-1;
            f.Sim.TrainingOrderAccepted += receipt=> {
                if(trainTick<0)trainTick=f.Sim.CurrentTick;
                Assert.That(goals[0].Status,Is.EqualTo(CommanderGoalStatus.Completed));
                Assert.That(f.Sim.BuildingRegistry.GetBuilding(receipt.ProducerId).IsUnderConstruction,Is.False);
            };
            for(int i=0;i<12000&&goals.Any(g=>!g.IsTerminal);i++)
            {
                f.Manager.Tick(f.Sim.CurrentTick);
                if(goals[0].Status==CommanderGoalStatus.Completed&&buildingCompletedTick<0)buildingCompletedTick=f.Sim.CurrentTick;
                f.Sim.Tick();
            }
            Assert.That(goals.All(g=>g.Status==CommanderGoalStatus.Completed),Is.True,string.Join(";",goals.Select(g=>g.StatusReason)));
            var build=(BuildStructureGoal)goals[0];
            Assert.That(build.ResultBuildingIds,Has.Count.EqualTo(1));
            Assert.That(f.AcceptedProducerIds,Has.Count.EqualTo(4));
            Assert.That(f.AcceptedProducerIds.All(id=>id==build.ResultBuildingIds[0]&&id!=oldProducer.Id),Is.True);
            Assert.That(trainTick,Is.GreaterThanOrEqualTo(buildingCompletedTick));
            Assert.That(f.Sim.UnitRegistry.GetAllUnits().Count(u=>u.PlayerId==0&&u.UnitType==1&&u.State!=UnitState.Dead),Is.EqualTo(baseline+4));
        }

        [Test]
        public void FiveNewBarracks_TrainFiftyNewSpearmenOnlyFromExactBuildResults()
        {
            using var f=new Fixture();
            var old=f.Sim.CreateBuilding(0,BuildingType.Barracks,f.X-20,f.Z+15,false,true);
            var train=new JObject{["type"]="EnsureUnitCount",["unit"]="Spearman",["count"]=50,["quantityMode"]="New",["dependsOn"]=new JArray(0),["producerFromNode"]=0};
            var goals=f.Admit(new JObject{["outcome"]="Request",["executionOrder"]="Sequential",
                ["nodes"]=new JArray(Build(BuildingType.Barracks,5),train)}.ToString(),"Build 5 Barracks then train 50 new Spearmen using those Barracks");
            f.Complete(goals);
            var built=(BuildStructureGoal)goals[0];var produced=(EnsureUnitCountGoal)goals[1];
            Assert.That(built.ResultBuildingIds.Count,Is.EqualTo(5));
            Assert.That(produced.ResultUnitIds.Count,Is.EqualTo(50));
            Assert.That(built.ResultBuildingIds,Has.No.Member(old.Id));
            Assert.That(f.AcceptedProducerIds,Has.Count.EqualTo(50));
            Assert.That(f.AcceptedProducerIds.All(built.ResultBuildingIds.Contains),Is.True);
            AssertDistinct(built.ResultBuildingIds.Select(f.Sim.BuildingRegistry.GetBuilding).ToArray());
        }

        [Test]
        public void ExistingFiveBarracksRestriction_DoesNotConstructOrSubstituteSixthProducer()
        {
            using var f=new Fixture();
            var eligible=Enumerable.Range(0,6).Select(i=>f.Sim.CreateBuilding(0,BuildingType.Barracks,
                f.X-20+(i%3)*9,f.Z+15+(i/3)*8,false,true)).ToArray();
            const string json="{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"sources\",\"mechanic\":\"select-structures\",\"parameters\":{\"building\":\"building:Barracks\",\"count\":5},\"inputs\":{},\"dependsOn\":[]},{\"id\":\"train\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":50,\"quantityMode\":\"New\"},\"inputs\":{\"producers\":\"sources\"},\"dependsOn\":[\"sources\"]}]}";
            var goals=f.Admit(json,"Train 50 new Spearmen out of 5 existing Barracks");
            f.Complete(goals);
            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Count(b=>b.Type==BuildingType.Barracks),Is.EqualTo(6));
            var produced=(EnsureUnitCountGoal)goals.Single();
            Assert.That(produced.ResultUnitIds.Count,Is.EqualTo(50));
            Assert.That(f.AcceptedProducerIds,Has.Count.EqualTo(50));
            Assert.That(f.AcceptedProducerIds.All(id=>eligible.Take(5).Any(b=>b.Id==id)),Is.True);
        }

        private static void AssertDistinct(BuildingData[] buildings)
        {
            for(int i=0;i<buildings.Length;i++)for(int j=i+1;j<buildings.Length;j++)
                Assert.That(buildings[i].OriginTileX+buildings[i].TileFootprintWidth<=buildings[j].OriginTileX
                    ||buildings[j].OriginTileX+buildings[j].TileFootprintWidth<=buildings[i].OriginTileX
                    ||buildings[i].OriginTileZ+buildings[i].TileFootprintHeight<=buildings[j].OriginTileZ
                    ||buildings[j].OriginTileZ+buildings[j].TileFootprintHeight<=buildings[i].OriginTileZ,Is.True,"Native footprints overlap.");
        }
    }
}
