using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixEvidenceLaneTests
    {
        private string directory,path,run;
        [SetUp] public void SetUp()
        {
            directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"OpenEmpires-Evidence-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);path=System.IO.Path.Combine(directory,"paid-usage-ledger.json");run=Guid.NewGuid().ToString("D");
        }
        [TearDown] public void TearDown()
        {
            string resolved=System.IO.Path.GetFullPath(directory);
            Assert.That(resolved,Does.StartWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath())));
            Assert.That(System.IO.Path.GetFileName(resolved),Does.StartWith("OpenEmpires-Evidence-"));
            if(Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
        private static Type Feature(string name)
        {
            var type=Type.GetType("OpenEmpires.TestSupport."+name+", OpenEmpires.TestSupport",false);
            Assert.That(type,Is.Not.Null,"Persistent evidence-lane implementation is missing: "+name);return type;
        }
        private object Journal(bool create=true,string identity=null)
        {
            try{return Activator.CreateInstance(Feature("EvidenceBudgetJournal"),path,identity??run,create);}
            catch(TargetInvocationException error){throw error.InnerException;}
        }
        private static object Invoke(object target,string method,params object[] args)
        {
            try{return target.GetType().GetMethod(method).Invoke(target,args);}
            catch(TargetInvocationException error){throw error.InnerException;}
        }
        private static object Reserve(object journal,string kind,double seconds=0)=>Invoke(journal,"Reserve",kind,seconds);
        private static int Count(object journal,string field)
        {var snapshot=Invoke(journal,"Snapshot");return Convert.ToInt32(snapshot.GetType().GetField(field).GetValue(snapshot));}

        [Test] public void SixSubmissions_SurviveReloadAndSeventhIsDenied()
        {
            for(int i=0;i<6;i++)Reserve(Journal(),"submission");
            var loaded=Journal(false);Assert.That(Count(loaded,"live_submissions"),Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(()=>Reserve(loaded,"submission"));
            Assert.That(Count(loaded,"semantic_initial_http"),Is.Zero);
        }
        [Test] public void InitialAndRepair_AreSeparateFromOneSubmission()
        {
            var journal=Journal();Reserve(journal,"submission");Reserve(journal,"semantic-initial");Reserve(journal,"semantic-repair");
            var loaded=Journal(false);Assert.That(Count(loaded,"live_submissions"),Is.EqualTo(1));
            Assert.That(Count(loaded,"semantic_initial_http"),Is.EqualTo(1));Assert.That(Count(loaded,"semantic_repair_http"),Is.EqualTo(1));
        }
        [Test] public void UnknownCompletion_RemainsConsumedAfterRestart()
        {
            Reserve(Journal(),"semantic-initial");
            for(int i=0;i<5;i++)Reserve(Journal(false),"semantic-repair");
            Assert.Throws<InvalidOperationException>(()=>Reserve(Journal(false),"semantic-initial"));
        }
        [Test] public void ConcurrentReservations_CannotExceedRunWideHttpLimit()
        {
            Journal();int accepted=0;
            Parallel.For(0,32,_=>{try{Reserve(Journal(false),"semantic-initial");Interlocked.Increment(ref accepted);}catch(InvalidOperationException){} });
            Assert.That(accepted,Is.GreaterThan(0).And.LessThanOrEqualTo(6));
            Assert.That(Count(Journal(false),"semantic_initial_http"),Is.EqualTo(accepted));
        }
        [Test] public void ForeignRunAndCorruptLedger_FailClosedWithoutReset()
        {
            Reserve(Journal(),"semantic-initial");
            Assert.Throws<InvalidOperationException>(()=>{try{Journal(false,Guid.NewGuid().ToString("D"));}catch(TargetInvocationException e){throw e.InnerException;}});
            File.WriteAllText(path,"{broken");
            Assert.Throws<InvalidOperationException>(()=>Reserve(Journal(false),"semantic-initial"));
            Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
        }
        [Test] public void CloudAudioBudget_IsSeparateAndRejectsInvalidDuration()
        {
            var journal=Journal();for(int i=0;i<10;i++)Reserve(journal,"cloud-asr",60);
            Assert.Throws<InvalidOperationException>(()=>Reserve(Journal(false),"cloud-asr",1));
            Assert.Throws<InvalidOperationException>(()=>Reserve(Journal(false),"cloud-asr",double.NaN));
            Assert.That(Count(journal,"semantic_initial_http"),Is.Zero);Assert.That(Count(journal,"cloud_asr_requests"),Is.EqualTo(10));
        }
        [Test] public async Task SeventhHttp_IsBlockedBeforeOrdinaryTransport()
        {
            var journal=Journal();for(int i=0;i<6;i++)Reserve(journal,"semantic-initial");
            var inner=new FakeTransport();var transport=(ICommanderHttpTransport)Activator.CreateInstance(Feature("BudgetedSemanticTransport"),inner,journal);
            using((IDisposable)Invoke(transport,"BeginSubmission"))
                Assert.ThrowsAsync<InvalidOperationException>(async()=>await transport.PostJsonAsync(new Uri("https://example.invalid"),"{}",null,CancellationToken.None));
            Assert.That(inner.Calls,Is.Zero);await Task.CompletedTask;
        }
        [Test] public async Task ActualRepairTransport_ConsumesTwoHttpReservationsAndNoRawData()
        {
            var journal=Journal();var inner=new FakeTransport();var transport=(ICommanderHttpTransport)Activator.CreateInstance(Feature("BudgetedSemanticTransport"),inner,journal);
            using((IDisposable)Invoke(transport,"BeginSubmission"))
            {
                await transport.PostJsonAsync(new Uri("https://example.invalid"),"private request",new Dictionary<string,string>{{"Authorization","fake-secret-marker"}},CancellationToken.None);
                await transport.PostJsonAsync(new Uri("https://example.invalid"),"private repair",null,CancellationToken.None);
            }
            Assert.That(inner.Calls,Is.EqualTo(2));Assert.That(Count(journal,"live_submissions"),Is.EqualTo(1));
            Assert.That(Count(journal,"semantic_initial_http"),Is.EqualTo(1));Assert.That(Count(journal,"semantic_repair_http"),Is.EqualTo(1));
            string persisted=File.ReadAllText(path);Assert.That(persisted,Does.Not.Contain("fake-secret-marker"));Assert.That(persisted,Does.Not.Contain("private request"));
        }
        [TestCase("Answer")][TestCase("Clarify")][TestCase("Unsupported")]
        public void NonEffectfulValidMillResponse_FailsSemanticGate(string outcome)
        {
            var result=CommanderSemanticJson.Parse("{\"outcome\":\""+outcome+"\",\"message\":\"No construction requested.\"}");
            Assert.That(result.IsValid,Is.True,result.SafeExplanation);
            var gate=Feature("EvidenceSemanticGate").GetMethod("HasOnlyStructureEffect");
            Assert.That(gate.Invoke(null,new object[]{result,BuildingType.Mill,1}),Is.False);
        }
        [TestCase(null,false)][TestCase("0",false)][TestCase("true",false)][TestCase("1",true)]
        public void LiveLane_RequiresExactExplicitOptIn(string value,bool expected)
        {Assert.That(Feature("EvidenceLivePolicy").GetMethod("AllowsLive").Invoke(null,new object[]{value}),Is.EqualTo(expected));}
        [TestCase("Mill",1,true)][TestCase("Mill",2,false)][TestCase("House",1,false)]
        public void MillSemanticGate_AcceptsOnlyRequestedStructureAndCount(string structure,int count,bool expected)
        {
            var result=CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\""+structure+"\",\"count\":"+count+"}]}");
            Assert.That(result.IsValid,Is.True,result.SafeExplanation);
            Assert.That(Feature("EvidenceSemanticGate").GetMethod("HasOnlyStructureEffect").Invoke(null,new object[]{result,BuildingType.Mill,1}),Is.EqualTo(expected));
        }
        [Test] public void MillSemanticGate_AcceptsRealDynamicBuildWithPurePlacementNodes()
        {
            var result=CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill);
            Assert.That(Feature("EvidenceSemanticGate").GetMethod("HasOnlyStructureEffect").Invoke(null,new object[]{result,BuildingType.Mill,1}),Is.True);
        }
        [Test] public async Task UnrelatedExecutionContext_CannotBorrowAnotherCallersOperation()
        {
            var journal=Journal();var inner=new FakeTransport();var transport=(ICommanderHttpTransport)Activator.CreateInstance(Feature("BudgetedSemanticTransport"),inner,journal);
            using((IDisposable)Invoke(transport,"BeginSubmission"))
            {
                Task operation;
                using(ExecutionContext.SuppressFlow())operation=Task.Run(async()=>await transport.PostJsonAsync(new Uri("https://example.invalid"),"{}",null,CancellationToken.None));
                bool denied=false;try{await operation;}catch(InvalidOperationException){denied=true;}
                Assert.That(denied,Is.True);Assert.That(inner.Calls,Is.Zero);
            }
        }
        [TestCase(false)][TestCase(true)] public async Task RealOpenRouterSchemaRepair_IsCountedAndDeniedAtItsOwnBoundary(bool limitRepair)
        {
            var journal=Journal();if(limitRepair)for(int i=0;i<5;i++)Reserve(journal,"semantic-initial");
            string farm="{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"f\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":4},\"inputs\":{},\"dependsOn\":[]}]}";
            var inner=new FakeTransport(Completion(farm.Replace("\"count\":4","\"count\":\"4\"")),Completion(farm));
            var transport=(ICommanderHttpTransport)Activator.CreateInstance(Feature("BudgetedSemanticTransport"),inner,journal);
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var simulation=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
                using var manager=new CommanderGoalManager(simulation,0);
                var provider=new OpenRouterCommanderProvider("not-an-api-key",transport);
                CommanderSemanticResult result;
                using((IDisposable)Invoke(transport,"BeginSubmission"))result=await provider.TranslateSemanticAsync(
                    new CommanderSemanticProviderRequest("make four farms",new CommanderContextBuilder().Build(simulation,manager)),CancellationToken.None);
                Assert.That(inner.Calls,Is.EqualTo(limitRepair?1:2));Assert.That(result.IsValid,Is.EqualTo(!limitRepair));
                Assert.That(Count(journal,"live_submissions"),Is.EqualTo(1));
                Assert.That(Count(journal,"semantic_initial_http"),Is.EqualTo(limitRepair?6:1));
                Assert.That(Count(journal,"semantic_repair_http"),Is.EqualTo(limitRepair?0:1));
                Assert.That(manager.Goals,Is.Empty);Assert.That(simulation.CommandBuffer.FlushCommands(),Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        [Test] public void ExhaustedBudget_HasExplicitGuardCategoryNotProviderTimeout()
        {
            var journal=Journal();for(int i=0;i<6;i++)Reserve(journal,"submission");
            var transport=(ICommanderHttpTransport)Activator.CreateInstance(Feature("BudgetedSemanticTransport"),new FakeTransport(),journal);
            Assert.Throws<InvalidOperationException>(()=>Invoke(transport,"BeginSubmission"));
            var property=transport.GetType().GetProperty("LastGuardFailure");Assert.That(property,Is.Not.Null);
            Assert.That(property.GetValue(transport),Is.EqualTo("budget-exhausted"));
        }
        [Test] public void MalformedUtf8Ledger_FailsWithSafeBudgetCategory()
        {
            Journal();File.WriteAllBytes(path,new byte[]{0xff,0xfe,0xff});
            Assert.Throws<InvalidOperationException>(()=>Reserve(Journal(false),"semantic-initial"));
        }
        [Test] public void FailureArtifact_PersistsFirstStageAndNeverStoresAnswerText()
        {
            string artifact=System.IO.Path.Combine(directory,"scenario.json");
            var recorder=Activator.CreateInstance(Feature("ScenarioEvidenceRecorder"),artifact,run);
            var result=CommanderSemanticJson.Parse("{\"outcome\":\"Answer\",\"message\":\"PRIVATE_TEXT_MUST_NOT_BE_PERSISTED\"}");
            Invoke(recorder,"Record","semantic",result,"unexpected-effect",-1L,false,0,0,-1,0);
            Assert.That(File.Exists(artifact),Is.True,"The failing stage must persist before any assertion/teardown.");
            Invoke(recorder,"Record","terminal",result,"test-failed",-1L,false,0,0,-1,0);
            string text=File.ReadAllText(artifact);
            Assert.That(text,Does.Contain("\"first_failing_stage\": \"semantic\""));
            Assert.That(text,Does.Contain("\"outcome\": \"Answer\""));Assert.That(text,Does.Not.Contain("PRIVATE_TEXT_MUST_NOT_BE_PERSISTED"));
        }
        [Test] public void EffectArtifact_StoresBoundedNormalizedFactsNotProviderNodeNames()
        {
            string artifact=System.IO.Path.Combine(directory,"scenario.json");
            var recorder=Activator.CreateInstance(Feature("ScenarioEvidenceRecorder"),artifact,run);
            var result=CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill);
            Invoke(recorder,"Record","semantic",result,"none",42L,false,0,0,-1,0);
            string text=File.ReadAllText(artifact);
            Assert.That(new FileInfo(artifact).Length,Is.LessThanOrEqualTo(16384));
            Assert.That(text,Does.Contain("building:Mill"));Assert.That(text,Does.Contain("Berries"));
            Assert.That(text,Does.Not.Contain("\"id\": \"berries\""));Assert.That(text,Does.Contain("\"request_id\": \"42\""));
        }
        [TestCase("length","length")][TestCase("private-finish-marker","other")]
        public async Task RejectedCompletion_RecordsSafeFinishCategory(string finish,string expected)
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var simulation=new GameSimulation(config,1,new[]{0},Array.Empty<int>());using var manager=new CommanderGoalManager(simulation,0);
                var provider=new OpenRouterCommanderProvider("not-an-api-key",new FakeTransport(Completion("{\"outcome\":\"Answer\",\"message\":\"fixture\"}",finish)));
                var result=await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("a question",new CommanderContextBuilder().Build(simulation,manager)),CancellationToken.None);
                Assert.That(result.IsValid,Is.False);Assert.That(provider.LastRequestTrace,Does.Contain("finish="+expected));
                Assert.That(provider.LastRequestTrace,Does.Not.Contain("private-finish-marker"));
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        private static string Completion(string content,string finish="stop")=>JsonUtility.ToJson(new WireReply{choices=new[]{new WireChoice{finish_reason=finish,message=new WireMessage{content=content}}}});
        [Serializable] private sealed class WireReply{public WireChoice[] choices;}
        [Serializable] private sealed class WireChoice{public string finish_reason;public WireMessage message;}
        [Serializable] private sealed class WireMessage{public string content;}
        private sealed class FakeTransport:ICommanderHttpTransport
        {
            public int Calls;private readonly Queue<string> responses;
            public FakeTransport(params string[] replies){responses=new Queue<string>(replies);}
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string json,IReadOnlyDictionary<string,string> headers,CancellationToken token)
            {Calls++;return Task.FromResult(new CommanderHttpResponse(200,responses.Count>0?responses.Dequeue():"{}"));}
        }
    }
}
