using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixEvidenceIntegrationTests
    {
        private string directory;private EvidenceBudgetJournal journal;
        [SetUp]public void SetUp()
        {directory=Path.Combine(Path.GetTempPath(),"OpenEmpires-Evidence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);journal=new EvidenceBudgetJournal(Path.Combine(directory,"paid-usage-ledger.json"),Guid.NewGuid().ToString("D"),true);}
        [TearDown]public void TearDown()
        {string path=Path.GetFullPath(directory);Assert.That(path,Does.StartWith(Path.GetFullPath(Path.GetTempPath())));Assert.That(Path.GetFileName(path),Does.StartWith("OpenEmpires-Evidence-"));if(Directory.Exists(path))Directory.Delete(path,true);}
        private ICommanderGatewayTransport Wrap(FakeGateway inner,double minimum=2,double quantum=1)
        {
            var type=Type.GetType("OpenEmpires.TestSupport.BudgetedSpeechTransport, OpenEmpires.TestSupport");
            Assert.That(type,Is.Not.Null,"Actual audio-upload boundary guard is missing.");
            return (ICommanderGatewayTransport)Activator.CreateInstance(type,inner,journal,minimum,quantum,"mock-billing-v1");
        }
        [Test]public async Task SpeechUpload_ReservesConfiguredMinimumBeforeForwarding()
        {
            var inner=new FakeGateway();using var transport=Wrap(inner);
            await transport.SendAsync("https://example.invalid",Guid.NewGuid(),"fake-session","fixture-policy","en",Wave(.5),CommanderGatewayRequestKind.Speech,CancellationToken.None);
            Assert.That(inner.Calls,Is.EqualTo(1));Assert.That(journal.Snapshot().cloud_asr_billable_seconds,Is.EqualTo(2));
            Assert.That(journal.Snapshot().semantic_initial_http,Is.Zero);
            string persisted=File.ReadAllText(Path.Combine(directory,"paid-usage-ledger.json"));
            Assert.That(persisted,Does.Contain("mock-billing-v1"));Assert.That(persisted,Does.Contain("audio_duration_seconds"));Assert.That(persisted,Does.Not.Contain("fake-session"));
        }
        [Test]public async Task ExhaustedSpeechBudget_BlocksBeforeUpload()
        {
            for(int i=0;i<10;i++)journal.Reserve("cloud-asr",60);
            var inner=new FakeGateway();using var transport=Wrap(inner);bool denied=false;
            try{await transport.SendAsync("https://example.invalid",Guid.NewGuid(),"fake-session","fixture-policy","en",Wave(.5),CommanderGatewayRequestKind.Speech,CancellationToken.None);}catch(InvalidOperationException){denied=true;}
            Assert.That(denied,Is.True);Assert.That(inner.Calls,Is.Zero);
        }
        [Test]public async Task MalformedAudio_NeverAllocatesBudgetOrUploads()
        {
            var inner=new FakeGateway();using var transport=Wrap(inner);bool denied=false;
            try{await transport.SendAsync("https://example.invalid",Guid.NewGuid(),"fake-session","fixture-policy","en",new byte[44],CommanderGatewayRequestKind.Speech,CancellationToken.None);}catch(InvalidOperationException){denied=true;}
            Assert.That(denied,Is.True);Assert.That(inner.Calls,Is.Zero);Assert.That(journal.Snapshot().cloud_asr_requests,Is.Zero);
        }
        [Test]public async Task FailedSpeechAttempt_RemainsConsumedAcrossReload()
        {
            var inner=new FakeGateway{Fail=true};using var transport=Wrap(inner);try{await transport.SendAsync("https://example.invalid",Guid.NewGuid(),"fake-session","fixture-policy","en",Wave(2.25),CommanderGatewayRequestKind.Speech,CancellationToken.None);}catch(IOException){}
            var loaded=new EvidenceBudgetJournal(Path.Combine(directory,"paid-usage-ledger.json"),journal.Snapshot().run_id,false);
            Assert.That(loaded.Snapshot().cloud_asr_billable_seconds,Is.EqualTo(3));Assert.That(loaded.Snapshot().cloud_asr_requests,Is.EqualTo(1));
        }
        [Test]public void RichArtifact_ContainsOnlyExplicitDiagnosticAndAttributionFields()
        {
            string path=Path.Combine(directory,"scenario.json");var recorder=new ScenarioEvidenceRecorder(path,journal.Snapshot().run_id);
            var method=typeof(ScenarioEvidenceRecorder).GetMethod("SetDiagnostics");Assert.That(method,Is.Not.Null);
            method.Invoke(recorder,new object[]{1,1,200,"stop",new[]{11},new[]{"PlaceBuildingCommand"},new[]{21},new[]{31}});
            recorder.Record("native",null,"none",42,true,1,1,2,100);
            string text=File.ReadAllText(path);Assert.That(text,Does.Contain("initial_http_attempts"));Assert.That(text,Does.Contain("repair_http_attempts"));Assert.That(text,Does.Contain("PlaceBuildingCommand"));Assert.That(text,Does.Contain("native_building_ids"));Assert.That(text,Does.Contain("native_unit_ids"));
        }
        [TestCase(false)][TestCase(true)]public async Task ActualSpeechProvider_RequiresConsentAndUsesBudgetedUpload(bool consent)
        {
            var inner=new FakeGateway();var transport=Wrap(inner);
            var type=typeof(CommanderVoiceInputController).Assembly.GetType("OpenEmpires.CommanderGatewaySpeechToTextProvider");
            using var provider=(ICommanderSpeechToTextProvider)Activator.CreateInstance(type,"https://example.invalid",
                (Func<string>)(()=>CommanderGrandFixOnlineVoiceTests.FixtureBackendToken),CommanderGrandFixOnlineVoiceTests.FixturePolicy,transport,"en");
            if(consent)type.GetMethod("ConsentToOnlineAudio").Invoke(provider,null);
            bool started=((ICommanderVoiceSessionProvider)provider).TryBeginVoiceSession(out _);
            Assert.That(started,Is.EqualTo(consent));
            if(started)
            {
                var samples=new float[1600];for(int i=0;i<samples.Length;i++)samples[i]=.2f;
                var result=await provider.TranscribeAsync(new CommanderAudioData(samples,16000,1),CancellationToken.None);
                Assert.That(result.Success,Is.True,result.UserFacingError);
            }
            Assert.That(inner.Calls,Is.EqualTo(consent?1:0));Assert.That(journal.Snapshot().cloud_asr_requests,Is.EqualTo(consent?1:0));
            Assert.That(journal.Snapshot().cloud_asr_billable_seconds,Is.EqualTo(consent?2:0));
        }
        [Test]public async Task IdleConfiguredCastleInformation_UsesOneProviderCallAndNoGameplay()
        {
            var field=typeof(CommanderChatUI).GetField("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            object previous=field.GetValue(null);field.SetValue(null,null);
            var config=ScriptableObject.CreateInstance<SimulationConfig>();GameObject host=null;
            try
            {
                var simulation=new GameSimulation(config,1,new[]{0},Array.Empty<int>());using var manager=new CommanderGoalManager(simulation,0);
                using var dispatcher=new CommanderIntentDispatcher(simulation,manager);var provider=new InformationProvider();
                host=new GameObject("EvidenceQuestionFixture");var chat=host.AddComponent<CommanderChatUI>();chat.enabled=false;
                chat.Initialize(provider,simulation,manager,dispatcher);
                await chat.SubmitMessageAsync("What do I need to reach Castle age? I am asking for information, not an order.");
                Assert.That(provider.Calls,Is.EqualTo(1));Assert.That(provider.ReadOnly,Is.True);
                Assert.That(manager.Goals,Is.Empty);Assert.That(simulation.CommandBuffer.FlushCommands(),Is.Empty);Assert.That(chat.PendingActionPlan,Is.Null);
            }
            finally{if(host!=null)UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(config);field.SetValue(null,previous);}
        }
        private static byte[] Wave(double seconds)
        {
            var bytes=new byte[44+(int)Math.Round(seconds*32000)];using var stream=new MemoryStream(bytes);using var writer=new BinaryWriter(stream,Encoding.ASCII,true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(bytes.Length-8);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(16000);writer.Write(32000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes.Length-44);return bytes;
        }
        private sealed class FakeGateway:ICommanderGatewayTransport
        {
            public int Calls;public bool Fail;
            public Task<CommanderGatewayResponse> SendAsync(string gateway,Guid id,string token,string policy,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken cancel)
            {Calls++;if(Fail)throw new IOException("synthetic transport failure");return Task.FromResult(new CommanderGatewayResponse(200,"{\"jobId\":\""+id+"\",\"policyVersion\":\""+policy+"\",\"text\":\"make four spearmen\"}"));}
            public void Dispose(){}
        }
        private sealed class InformationProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            public int Calls;public bool ReadOnly;
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token)=>throw new InvalidOperationException("Legacy path is not the information fixture.");
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token)
            {Calls++;ReadOnly=request.IsReadOnlyQuestion;return Task.FromResult(CommanderSemanticJson.Parse("{\"outcome\":\"Answer\",\"message\":\"fixture information\"}"));}
        }
    }
}
