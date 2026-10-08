using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixStatusPlayModeTests
    {
        [UnityTest] public IEnumerator NativeTrainingContinuesWhileQuestionRemainsReadOnlyAndReportsActualProgress()
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            var sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
            CommanderGoalManager manager=null;CommanderIntentDispatcher dispatcher=null;GameObject host=null;
            try
            {
                sim.SetPlayerCivilizations(new[]{Civilization.French});sim.SetPlayerAge(0,3);
                int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
                typeof(MapData).GetField("holeMap",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sim.MapData,null);
                for(int tx=x-20;tx<=x+20;tx++)for(int tz=z-20;tz<=z+20;tz++)
                {sim.MapData.Tiles[tx,tz]=TileType.Grass;sim.MapData.ForestDensity[tx,tz]=0;sim.MapData.FoundationCount[tx,tz]=0;sim.FogOfWar.SetVisible(0,tx,tz);}
                sim.CreateBuilding(0,BuildingType.TownCenter,x,z,false,true).AutoProduceVillagers=false;
                sim.CreateBuilding(0,BuildingType.Barracks,x+8,z,false,true);
                var stock=sim.ResourceManager.GetPlayerResources(0);stock.Food=1000;stock.Wood=20;
                manager=new CommanderGoalManager(sim,0);dispatcher=new CommanderIntentDispatcher(sim,manager);
                var goal=manager.SubmitEnsureUnitCount(1,2,newProductionCount:2);
                int trainCommands=0;
                sim.CommandBuffer.CommandEnqueued+=(command,origin)=>{if(origin==CommandEnqueueSource.Commander&&command is TrainUnitCommand)trainCommands++;};
                // Native training only. Initial stock funds exactly one unit; no
                // post-submission spawning/resource grants/accelerated completion.
                for(int tick=0;tick<1200&&goal.AttributedUnitIds.Count==0;tick++)
                {manager.Tick(sim.CurrentTick);sim.Tick();if(tick%30==0)yield return null;}
                Assert.That(goal.AttributedUnitIds.Count,Is.EqualTo(1));Assert.That(trainCommands,Is.EqualTo(1));
                host=new GameObject("Native status question host");var chat=host.AddComponent<CommanderChatUI>();
                var provider=new StatusProvider();chat.Initialize(provider,sim,manager,dispatcher);
                var query=chat.SubmitMessageAsync("Why are my 2 new Spearmen still waiting?");
                while(!query.IsCompleted)yield return null;
                Assert.That(query.GetAwaiter().GetResult(),Is.Null);
                Assert.That(manager.Goals.Count,Is.EqualTo(1));Assert.That(trainCommands,Is.EqualTo(1));
                Assert.That(goal.Status,Is.Not.EqualTo(CommanderGoalStatus.Cancelled));
                Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
                string text=chat.Conversation.Memory.ToJson().ToLowerInvariant();
                Assert.That(text,Does.Contain("1/2 new completed"));Assert.That(text,Does.Contain("wood"));
                Assert.That(provider.Last.IsReadOnlyQuestion,Is.True);
            }
            finally{if(host!=null)UnityEngine.Object.DestroyImmediate(host);dispatcher?.Dispose();manager?.Dispose();UnityEngine.Object.DestroyImmediate(config);}
        }
        private sealed class StatusProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            public CommanderSemanticProviderRequest Last;
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {Last=request;return Task.FromResult(CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":9}]}"));}
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)
                =>Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"Unused legacy route"));
        }
    }
}
