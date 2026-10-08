using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixVoiceConsentTests
    {
        private bool previousEnabled;
        [SetUp] public void Setup(){previousEnabled=CommanderVoiceSettings.VoiceEnabled;CommanderVoiceSettings.VoiceEnabled=true;}
        [TearDown] public void Cleanup(){CommanderVoiceSettings.VoiceEnabled=previousEnabled;}

        [Test] public void MissingOnlineConsent_DoesNotStartMicrophone()
        {
            var provider=new SessionSpeech{Allowed=false};var capture=new Capture();
            using var controller=new CommanderVoiceInputController(provider,capture);
            Assert.That(controller.StartRecording(),Is.False);Assert.That(capture.Starts,Is.Zero);
            Assert.That(controller.LastError,Does.Contain("consent"));Assert.That(provider.Begins,Is.EqualTo(1));
        }
        [Test] public void RecordingGate_FreezesSessionBeforeCapture()
        {
            var provider=new SessionSpeech();var capture=new Capture{OnStart=()=>Assert.That(provider.Begins,Is.EqualTo(1))};
            using var controller=new CommanderVoiceInputController(provider,capture);Assert.That(controller.StartRecording(),Is.True);
        }
        [Test] public void FailedCapture_ReleasesFrozenProviderJob()
        {
            var provider=new SessionSpeech();var capture=new Capture{StartSucceeds=false};
            using var controller=new CommanderVoiceInputController(provider,capture);Assert.That(controller.StartRecording(),Is.False);
            Assert.That(provider.Cancels,Is.EqualTo(1));
        }
        [Test] public void ConsentRevocation_StopsRecordingAndRequiresFreshAction()
        {
            var provider=new SessionSpeech();var capture=new Capture();
            using var controller=new CommanderVoiceInputController(provider,capture);controller.StartRecording();provider.Invalidate();
            Assert.That(capture.IsRecording,Is.False);Assert.That(controller.State,Is.EqualTo(CommanderVoiceState.Idle));
            Assert.That(provider.Cancels,Is.EqualTo(1));Assert.That(capture.Starts,Is.EqualTo(1));
        }
        [Test] public async Task ConsentRevocation_DuringIgnoringInferenceNeverDeliversText()
        {
            var provider=new SessionSpeech();using var controller=new CommanderVoiceInputController(provider,new Capture());
            int previews=0;controller.TranscriptPreviewReady+=_=>previews++;
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();provider.Invalidate();
            var first=await Task.WhenAny(task,Task.Delay(250));provider.Complete();await task;
            Assert.That(first,Is.SameAs(task));Assert.That(previews,Is.Zero);Assert.That(controller.CurrentTranscript,Is.Empty);
        }
        [Test] public void Dispose_DetachesSessionInvalidationSubscription()
        {
            var provider=new SessionSpeech();var controller=new CommanderVoiceInputController(provider,new Capture());
            Assert.That(provider.Subscribers,Is.EqualTo(1));controller.Dispose();Assert.That(provider.Subscribers,Is.Zero);
            Assert.DoesNotThrow(provider.Invalidate);
        }
        [Test] public void SynchronousConsentInvalidation_DoesNotStartAfterRevocation()
        {
            var provider=new SessionSpeech();var capture=new Capture();provider.OnBegin=provider.Invalidate;
            using var controller=new CommanderVoiceInputController(provider,capture);
            Assert.That(controller.StartRecording(),Is.False);Assert.That(capture.Starts,Is.Zero);
        }
        [Test] public async Task EmptyCapture_DoesNotKeepAnUnusedFrozenOnlineJob()
        {
            var provider=new SessionSpeech();using var controller=new CommanderVoiceInputController(provider,new Capture{SamplesCount=0});
            controller.StartRecording();var result=await controller.StopRecordingAndTranscribeAsync();
            Assert.That(result.Success,Is.False);Assert.That(provider.Cancels,Is.EqualTo(1));Assert.That(provider.Transcriptions,Is.Zero);
        }
        [TestCase(false)][TestCase(true)] public async Task ConfirmPreview_CallbackCancellationCannotSubmitOldText(bool dispose)
        {
            var provider=new SessionSpeech();int submitted=0;
            using var controller=new CommanderVoiceInputController(provider,new Capture(),_=>{submitted++;return Task.FromResult<CommanderAIChatSubmission>(null);}){AutoSubmit=false};
            provider.Complete();controller.StartRecording();await controller.StopRecordingAndTranscribeAsync();
            Assert.That(controller.State,Is.EqualTo(CommanderVoiceState.Preview));
            controller.StateChanged+=state=>{if(state==CommanderVoiceState.Idle){if(dispose)controller.Dispose();else provider.Invalidate();}};
            await controller.ConfirmPreviewAndSubmitAsync();Assert.That(submitted,Is.Zero);
        }
        [Test] public void Dispose_ReentrantIdleCallbackCleansEachOwnedResourceOnce()
        {
            var provider=new SessionSpeech();var capture=new Capture();var controller=new CommanderVoiceInputController(provider,capture);
            controller.StartRecording();bool reentered=false;
            controller.StateChanged+=state=>{if(state==CommanderVoiceState.Idle&&!reentered){reentered=true;controller.Dispose();}};
            controller.Dispose();Assert.That(reentered,Is.True);Assert.That(provider.DisposeCount,Is.EqualTo(1));Assert.That(capture.DisposeCount,Is.EqualTo(1));
        }
        private sealed class SessionSpeech:ICommanderSpeechToTextProvider,ICommanderVoiceSessionProvider
        {
            private Action invalidated;public bool Allowed=true;public int Begins,Cancels,Transcriptions,DisposeCount;public Action OnBegin;
            private readonly TaskCompletionSource<CommanderSpeechToTextResult> pending=new TaskCompletionSource<CommanderSpeechToTextResult>();
            public bool IsAvailable=>true;public int Subscribers=>invalidated?.GetInvocationList().Length??0;
            public event Action SessionInvalidated{add=>invalidated+=value;remove=>invalidated-=value;}
            public bool TryBeginVoiceSession(out CommanderSpeechToTextResult blocker){Begins++;OnBegin?.Invoke();blocker=Allowed?null:CommanderSpeechToTextResult.Failure("CONSENT_REQUIRED","Explicit online consent is required.");return Allowed;}
            public void CancelVoiceSession(){Cancels++;}public void Invalidate()=>invalidated?.Invoke();
            public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token){Transcriptions++;return pending.Task;}
            public void Complete()=>pending.TrySetResult(CommanderSpeechToTextResult.Accepted("make four spearmen"));public void Dispose(){DisposeCount++;}
        }
        private sealed class Capture:ICommanderAudioCapture
        {
            public int Starts,SamplesCount=1600,DisposeCount;public bool StartSucceeds=true;public Action OnStart;
            public bool IsRecording{get;private set;}public string CurrentDevice=>"fixture";
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){OnStart?.Invoke();Starts++;return IsRecording=StartSucceeds;}
            public CommanderAudioData StopRecording(){IsRecording=false;return new CommanderAudioData(new float[SamplesCount],16000,1);}
            public void CancelRecording()=>IsRecording=false;public void Dispose(){DisposeCount++;CancelRecording();}
        }
    }
}
