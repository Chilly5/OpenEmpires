using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixAsyncCaptureTests
    {
        private bool enabled,auto;
        [SetUp]public void Setup(){enabled=CommanderVoiceSettings.VoiceEnabled;auto=CommanderVoiceSettings.AutoSubmit;CommanderVoiceSettings.VoiceEnabled=true;CommanderVoiceSettings.AutoSubmit=false;}
        [TearDown]public void Cleanup(){CommanderVoiceSettings.VoiceEnabled=enabled;CommanderVoiceSettings.AutoSubmit=auto;}
        [Test]public async Task AsyncStop_LoadsFinalAudioBeforeAnyProviderCall()
        {
            var capture=new DeferredCapture();var speech=new Speech();using var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();int before=speech.Calls;
            capture.Complete();var result=await task;
            Assert.That(before,Is.Zero);Assert.That(capture.SyncStops,Is.Zero);Assert.That(capture.AsyncStops,Is.EqualTo(1));
            Assert.That(result.Success,Is.True);Assert.That(speech.Calls,Is.EqualTo(1));Assert.That(speech.Audio.Samples[1599],Is.EqualTo(.75f));
        }
        [Test]public async Task CancellationDuringLoad_ReturnsPromptlyAndSuppressesLateAudio()
        {
            var capture=new DeferredCapture();var speech=new Speech();using var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();controller.Cancel();
            bool cancelled=capture.Token.IsCancellationRequested;var first=await Task.WhenAny(task,Task.Delay(250));capture.Complete();await task;
            Assert.That(cancelled,Is.True);Assert.That(first,Is.SameAs(task));Assert.That(speech.Calls,Is.Zero);Assert.That(capture.Cancels,Is.GreaterThan(0));
        }
        [Test]public async Task IgnoringCancelledLoad_BlocksReplacementCaptureUntilActualCompletion()
        {
            var capture=new DeferredCapture();using var controller=new CommanderVoiceInputController(new Speech(),capture);
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();controller.Cancel();bool replacement=controller.StartRecording();
            capture.Complete();await task;Assert.That(replacement,Is.False);Assert.That(capture.Starts,Is.EqualTo(1));
        }
        [Test]public async Task DisposeDuringLoad_RejectsLateCompletion()
        {
            var capture=new DeferredCapture();var speech=new Speech();var controller=new CommanderVoiceInputController(speech,capture);int previews=0;
            controller.TranscriptPreviewReady+=_=>previews++;controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();controller.Dispose();
            capture.Complete();await task;Assert.That(previews,Is.Zero);Assert.That(speech.Calls,Is.Zero);Assert.That(capture.Cancels,Is.GreaterThan(0));
        }
        [Test]public async Task FailedAsyncRead_DoesNotTranscribeZeroOrStaleBuffer()
        {
            var capture=new DeferredCapture();var speech=new Speech();using var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();capture.Fail();var result=await task;
            Assert.That(result.Success,Is.False);Assert.That(speech.Calls,Is.Zero);
        }
        [Test]public async Task EmptyAsyncRead_IsNoSpeechAndNeverCallsProvider()
        {
            var capture=new DeferredCapture();var speech=new Speech();using var controller=new CommanderVoiceInputController(speech,capture);
            controller.StartRecording();var task=controller.StopRecordingAndTranscribeAsync();capture.Empty();var result=await task;
            Assert.That(result.ErrorCode,Is.EqualTo("EMPTY_TRANSCRIPTION"));Assert.That(speech.Calls,Is.Zero);
        }
        private sealed class DeferredCapture:ICommanderAudioCapture,ICommanderAsyncAudioCapture
        {
            private readonly TaskCompletionSource<CommanderAudioData> pending=new TaskCompletionSource<CommanderAudioData>();
            public int SyncStops,AsyncStops,Starts,Cancels;public CancellationToken Token;
            public bool IsRecording{get;private set;}public string CurrentDevice=>"synthetic";
            public bool StartRecording(string deviceName=null,float maxDurationSeconds=15){Starts++;return IsRecording=true;}
            public CommanderAudioData StopRecording(){SyncStops++;IsRecording=false;return Data();}
            public Task<CommanderAudioData> StopRecordingAsync(CancellationToken token){AsyncStops++;IsRecording=false;Token=token;return pending.Task;}
            public void CancelRecording(){Cancels++;IsRecording=false;}public void Dispose()=>CancelRecording();
            public void Complete()=>pending.TrySetResult(Data());public void Empty()=>pending.TrySetResult(CommanderAudioData.Empty);
            public void Fail(){pending.TrySetException(new InvalidOperationException("Synthetic final-read failure"));var observed=pending.Task.Exception;}
            private static CommanderAudioData Data(){var samples=new float[1600];samples[1599]=.75f;return new CommanderAudioData(samples,16000,1);}
        }
        private sealed class Speech:ICommanderSpeechToTextProvider
        {
            public int Calls;public CommanderAudioData Audio;public bool IsAvailable=>true;
            public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token){Calls++;Audio=audio;return Task.FromResult(CommanderSpeechToTextResult.Accepted("make four spearmen"));}
            public void Dispose(){}
        }
    }
}
