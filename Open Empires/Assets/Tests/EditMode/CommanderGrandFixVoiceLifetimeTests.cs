using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixVoiceLifetimeTests
    {
        private bool voiceEnabled,autoSubmit;
        [SetUp] public void Setup(){voiceEnabled=CommanderVoiceSettings.VoiceEnabled;autoSubmit=CommanderVoiceSettings.AutoSubmit;CommanderVoiceSettings.VoiceEnabled=true;CommanderVoiceSettings.AutoSubmit=false;}
        [TearDown] public void Cleanup(){CommanderVoiceSettings.VoiceEnabled=voiceEnabled;CommanderVoiceSettings.AutoSubmit=autoSubmit;}

        [Test]
        public async Task CancelIgnoringProvider_ReturnsPromptlyAndKeepsLateResultInert()
        {
            var speech=new DeferredSpeech(); using var controller=new CommanderVoiceInputController(speech,new Capture());
            int previews=0;controller.TranscriptPreviewReady+=_=>previews++;
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();controller.Cancel();
            var first=await Task.WhenAny(operation,Task.Delay(250));
            speech.Complete("make four spearmen"); await operation;
            Assert.That(first,Is.SameAs(operation),"Cancellation must not await a provider that ignores the token.");
            Assert.That(previews,Is.Zero);Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [Test]
        public async Task DeadlineIgnoringProvider_DoesNotSubmitSuccessfulLateText()
        {
            var speech=new DeferredSpeech();int submissions=0;
            using var controller=new CommanderVoiceInputController(speech,new Capture(),_=>{submissions++;return Task.FromResult<CommanderAIChatSubmission>(null);}){AutoSubmit=true};
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();
            var cancellation=(CancellationTokenSource)typeof(CommanderVoiceInputController).GetField("activeCts",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);
            cancellation.CancelAfter(30);
            var first=await Task.WhenAny(operation,Task.Delay(250));
            speech.Complete("make four spearmen");await operation;
            Assert.That(first,Is.SameAs(operation));Assert.That(submissions,Is.Zero);
            Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [Test]
        public async Task Dispose_CancelsTokenAndDefersProviderReleaseUntilActualInferenceEnds()
        {
            var speech=new DeferredSpeech();var capture=new Capture();var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();
            controller.Dispose();controller.Dispose();
            bool cancelled=speech.Token.IsCancellationRequested;int before=speech.DisposeCount;
            speech.Complete("stale");await operation;
            await Task.WhenAny(speech.Disposed.Task,Task.Delay(250));
            Assert.That(cancelled,Is.True);Assert.That(before,Is.Zero,"Do not free an inference-owned context before completion.");
            Assert.That(speech.DisposeCount,Is.EqualTo(1));Assert.That(capture.DisposeCount,Is.EqualTo(1));
            Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [Test]
        public async Task CancelledUnfinishedInference_RejectsReplacementCaptureInsteadOfQueuingWork()
        {
            var speech=new DeferredSpeech();var capture=new Capture();using var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();controller.Cancel();
            bool replacement=controller.StartRecording();speech.Complete("stale");await operation;
            Assert.That(replacement,Is.False);Assert.That(capture.Starts,Is.EqualTo(1));
        }
        [Test]
        public void Conversion_DefaultPolicyPreservesThirtySecondTail()
        {
            var samples=new float[16000*30];samples[samples.Length-1]=.75f;
            var converted=CommanderAudioConverter.ConvertToMono16k(new CommanderAudioData(samples,16000,1));
            Assert.That(converted.Samples.Length,Is.EqualTo(16000*30));
            Assert.That(converted.Samples[converted.Samples.Length-1],Is.EqualTo(.75f));
        }
        [Test]
        public void SharedCapturePolicy_BoundsInjectedAdaptersAtSixtySeconds()
        {
            var capture=new Capture();using var controller=new CommanderVoiceInputController(new DeferredSpeech(),capture);
            Assert.That(controller.StartRecording(maxDuration:120f),Is.True);
            Assert.That(capture.MaximumReceived,Is.EqualTo(60f));
        }
        [TestCase(false)] [TestCase(true)]
        public async Task CancelDuringTranscribingNotification_DoesNotInvokeProvider(bool dispose)
        {
            var speech=new DeferredSpeech();var controller=new CommanderVoiceInputController(speech,new Capture());
            controller.StateChanged+=state=>{if(state==CommanderVoiceState.Transcribing){if(dispose)controller.Dispose();else controller.Cancel();}};
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();
            speech.Complete("stale");await operation;controller.Dispose();
            Assert.That(speech.Calls,Is.Zero,"Cancelled state notification must stop before contacting/using a provider.");
            Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public async Task ResetInsideSuccessStateNotification_PreventsOldPreviewOrAutoSubmission(bool auto)
        {
            var speech=new DeferredSpeech();int submissions=0,previews=0;bool reset=false;
            using var controller=new CommanderVoiceInputController(speech,new Capture(),_=>{submissions++;return Task.FromResult<CommanderAIChatSubmission>(null);}){AutoSubmit=auto};
            controller.TranscriptPreviewReady+=_=>previews++;
            controller.StateChanged+=state=>{
                if(!reset&&(state==(auto?CommanderVoiceState.Idle:CommanderVoiceState.Preview)))
                {reset=true;controller.ResetSession();}
            };
            controller.StartRecording();var operation=controller.StopRecordingAndTranscribeAsync();speech.Complete("make four spearmen");await operation;
            Assert.That(reset,Is.True);Assert.That(submissions,Is.Zero);Assert.That(previews,Is.Zero);
            Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public async Task PublicInitialize_InvalidatesInFlightVoiceBeforeAdoptingRuntime(bool auto)
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            var instance=typeof(CommanderChatUI).GetField("instance",BindingFlags.NonPublic|BindingFlags.Static);
            var previous=instance.GetValue(null);instance.SetValue(null,null);
            CommanderChatUI chat=null;
            try
            {
                var sim=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
                using var manager=new CommanderGoalManager(sim,0);using var dispatcher=new CommanderIntentDispatcher(sim,manager);
                var provider=new SemanticProvider();var speech=new DeferredSpeech();
                chat=new GameObject("LifetimeChat").AddComponent<CommanderChatUI>();chat.Initialize(provider,sim,manager,dispatcher);
                chat.SetVoiceProvider(speech,new Capture());chat.VoiceController.AutoSubmit=auto;
                chat.VoiceController.StartRecording();var operation=chat.VoiceController.StopRecordingAndTranscribeAsync();
                var next=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
                using var nextManager=new CommanderGoalManager(next,0);using var nextDispatcher=new CommanderIntentDispatcher(next,nextManager);
                chat.Initialize(provider,next,nextManager,nextDispatcher);
                speech.Complete("make four spearmen");await operation;
                Assert.That(provider.Calls,Is.Zero);Assert.That(chat.InputText,Is.Empty);Assert.That(chat.VoiceState,Is.EqualTo(CommanderVoiceState.Idle));
                Assert.That(manager.Goals,Is.Empty);Assert.That(nextManager.Goals,Is.Empty);
                Assert.That(next.CommandBuffer.FlushCommands(),Is.Empty);
            }
            finally{if(chat!=null)UnityEngine.Object.DestroyImmediate(chat.gameObject);instance.SetValue(null,previous);UnityEngine.Object.DestroyImmediate(config);}
        }
        private sealed class DeferredSpeech:ICommanderSpeechToTextProvider
        {
            private readonly TaskCompletionSource<CommanderSpeechToTextResult> completion=new TaskCompletionSource<CommanderSpeechToTextResult>();
            internal readonly TaskCompletionSource<bool> Disposed=new TaskCompletionSource<bool>();
            internal CancellationToken Token;internal int DisposeCount,Calls;
            public bool IsAvailable=>true;
            public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token){Calls++;Token=token;return completion.Task;}
            internal void Complete(string text)=>completion.TrySetResult(CommanderSpeechToTextResult.Accepted(text));
            public void Dispose(){DisposeCount++;Disposed.TrySetResult(true);}
        }
        private sealed class Capture:ICommanderAudioCapture
        {
            internal int Starts,DisposeCount;internal float MaximumReceived;public bool IsRecording{get;private set;}public string CurrentDevice=>"fixture";
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){Starts++;MaximumReceived=maxDurationSeconds;IsRecording=true;return true;}
            public CommanderAudioData StopRecording(){IsRecording=false;return new CommanderAudioData(new float[1600],16000,1);}
            public void CancelRecording()=>IsRecording=false;public void Dispose(){DisposeCount++;IsRecording=false;}
        }
        private sealed class SemanticProvider:ICommanderAIProvider,ICommanderSemanticProvider
        {
            internal int Calls;
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,CancellationToken token){Calls++;return Task.FromResult(CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}"));}
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,CancellationToken token){Calls++;return Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,"fixture"));}
        }
    }
}
