using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixNativeCaptureTests
    {
        private Backend backend;
        private UnityMicrophoneAudioCapture capture;
        [SetUp]public void SetUp(){backend=new Backend();}
        [TearDown]public void TearDown(){backend.ThrowEnd=false;backend.Recording=false;capture?.Dispose();backend.Cleanup();}
        private UnityMicrophoneAudioCapture Create()
        {
            var ctor=typeof(UnityMicrophoneAudioCapture).GetConstructor(new[]{typeof(ICommanderMicrophoneBackend)});
            Assert.That(ctor,Is.Not.Null,"A typed native boundary must let tests exercise capture without opening a real microphone.");
            return capture=(UnityMicrophoneAudioCapture)ctor.Invoke(new object[]{backend});
        }
        private ICommanderCaptureReadiness Ready=>capture as object as ICommanderCaptureReadiness;

        [Test]public void ActualStereoClipRateAndExactPrefix_ArePreservedInsteadOfRequestedRate()
        {
            backend.Rate=48000;backend.Channels=2;backend.ClipSeconds=1;backend.Position=24000;
            Assert.That(Create().StartRecording("fixture",1),Is.True);
            var audio=capture.StopRecording();
            Assert.That(audio.SampleRate,Is.EqualTo(48000));Assert.That(audio.Channels,Is.EqualTo(2));
            Assert.That(audio.Samples.Length,Is.EqualTo(48000));Assert.That(audio.DurationSeconds,Is.EqualTo(.5f));
            Assert.That(audio.Samples[0],Is.EqualTo(.125f));Assert.That(audio.Samples[47999],Is.EqualTo(.75f));
            Assert.That(backend.EndCalls,Is.EqualTo(1));Assert.That(backend.Releases,Is.EqualTo(1));
        }
        [Test]public void ExplicitMissingDevice_DoesNotSilentlyRecordAnotherDevice()
        {Assert.That(Create().StartRecording("missing",3),Is.False);Assert.That(backend.Starts,Is.Zero);}
        [TestCase(float.NaN)][TestCase(float.PositiveInfinity)][TestCase(float.NegativeInfinity)]
        public void InvalidDuration_IsRejectedBeforeMicrophoneStart(float duration)
        {Assert.That(Create().StartRecording("fixture",duration),Is.False);Assert.That(backend.Starts,Is.Zero);}
        [Test]public void DeviceCapabilityFailure_IsSafeAndDoesNotStartCapture()
        {backend.ThrowCaps=true;bool started=true;Assert.DoesNotThrow(()=>started=Create().StartRecording("fixture",3));Assert.That(started,Is.False);Assert.That(backend.Starts,Is.Zero);}
        [Test]public void NoPositivePosition_IsOpeningNotListeningAndDoesNotInventFullAudio()
        {
            backend.Position=0;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            Assert.That(Ready.IsListening,Is.False);backend.Now=4;backend.Recording=false;
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(backend.Reads,Is.Zero);
        }
        [Test]public void ZeroEndpointAfterObservedNearFullNonLoopRecording_PreservesFullTail()
        {
            backend.Position=47000;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            backend.Now=2.9375;Assert.That(Ready.IsListening,Is.True);
            backend.Position=0;backend.Recording=false;backend.Now=3.05;
            var audio=capture.StopRecording();Assert.That(audio.Samples.Length,Is.EqualTo(48000));
            Assert.That(audio.Samples[47999],Is.EqualTo(.75f));Assert.That(audio.DurationSeconds,Is.EqualTo(3));
        }
        [Test]public void EarlyUnexpectedZeroEndpoint_IsFailureNotGuessedSilencePadding()
        {
            backend.Position=8000;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            backend.Now=.5;Assert.That(Ready.IsListening,Is.True);
            backend.Position=0;backend.Recording=false;backend.Now=1;
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(backend.Reads,Is.Zero);
            Assert.That(Ready.CaptureError,Is.Not.Empty);
        }
        [Test]public void PositionResetWhileStillRecording_FailsClosed()
        {
            backend.Position=1600;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            backend.Now=.1;Assert.That(Ready.IsListening,Is.True);backend.Position=0;backend.Now=.2;
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(Ready.CaptureError,Is.Not.Empty);
        }
        [Test]public void OpeningTimeout_StopsOwnedDeviceAndRequiresFreshAction()
        {
            backend.Position=0;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            backend.Now=6;Assert.That(Ready.CaptureEnded,Is.True);Assert.That(Ready.CaptureError,Is.Not.Empty);
            Assert.That(backend.Recording,Is.False);Assert.That(capture.StopRecording().Samples,Is.Empty);
        }
        [Test]public void ConfiguredFractionalLimit_EndsCaptureAndKeepsExactlyVisibleDuration()
        {
            backend.ClipSeconds=4;backend.Position=52000;Create().StartRecording("fixture",3.25f);
            Assert.That(Ready,Is.Not.Null);Assert.That(Ready.CaptureEnded,Is.True);
            var audio=capture.StopRecording();Assert.That(audio.Samples.Length,Is.EqualTo(52000));
            Assert.That(audio.DurationSeconds,Is.EqualTo(3.25f));
        }
        [Test]public void ReadFailure_ReturnsNoFabricatedZeroSamplesAndReleasesClip()
        {
            backend.ReadSucceeds=false;backend.Position=16000;Create().StartRecording("fixture",3);
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(backend.Releases,Is.EqualTo(1));
        }
        [Test]public void ExactZeroSignal_DoesNotBecomeSpeechButQuietSignalIsNotAmplifiedOrDiscarded()
        {
            backend.Fill=0;backend.Position=16000;Create().StartRecording("fixture",3);
            Assert.That(capture.StopRecording().Samples,Is.Empty);
            backend.Fill=.00001f;backend.Position=16000;Assert.That(capture.StartRecording("fixture",3),Is.True);
            var audio=capture.StopRecording();Assert.That(audio.Samples[0],Is.EqualTo(.00001f));
            Assert.That((capture as object as ICommanderCaptureProgress).PeakLevel,Is.EqualTo(.00001f));
        }
        [Test]public void DeviceDisconnection_IsFailureWithoutReadingOrSwitchingDevices()
        {
            backend.Position=16000;Create().StartRecording("fixture",3);Assert.That(Ready,Is.Not.Null);
            backend.DeviceList=Array.Empty<string>();Assert.That(Ready.CaptureEnded,Is.True);
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(backend.Reads,Is.Zero);
        }
        [Test]public void FailedNativeStop_DoesNotReadOrFreeClipInUseOrPermitReplacementCapture()
        {
            backend.Position=16000;Create().StartRecording("fixture",3);backend.ThrowEnd=true;
            Assert.That(capture.StopRecording().Samples,Is.Empty);Assert.That(backend.Reads,Is.Zero);Assert.That(backend.Releases,Is.Zero);
            using var replacement=(UnityMicrophoneAudioCapture)typeof(UnityMicrophoneAudioCapture).GetConstructor(new[]{typeof(ICommanderMicrophoneBackend)}).Invoke(new object[]{new Backend()});
            Assert.That(replacement.StartRecording("fixture",3),Is.False);
            backend.ThrowEnd=false;backend.Recording=false;capture.Dispose();Assert.That(backend.Releases,Is.EqualTo(1));
        }
        [Test]public void CancelAndRepeatedDispose_ReleaseExactlyOnceAndCannotStartAgain()
        {
            Create().StartRecording("fixture",3);capture.CancelRecording();capture.Dispose();capture.Dispose();
            Assert.That(backend.EndCalls,Is.EqualTo(1));Assert.That(backend.Releases,Is.EqualTo(1));
            Assert.That(capture.StartRecording("fixture",3),Is.False);Assert.That(capture.StopRecording().Samples,Is.Empty);
        }
        [Test]public async Task CaptureReadFailure_IsVisibleErrorAndNeverStartsSttOrSubmission()
        {
            backend.Position=16000;backend.ReadSucceeds=false;var speech=new Speech();bool prior=CommanderVoiceSettings.VoiceEnabled;
            try
            {
                CommanderVoiceSettings.VoiceEnabled=true;using var controller=new CommanderVoiceInputController(speech,Create());
                Assert.That(controller.StartRecording("fixture",3),Is.True);var result=await controller.StopRecordingAndTranscribeAsync();
                Assert.That(result.Success,Is.False);Assert.That(speech.Calls,Is.Zero);
                Assert.That(controller.State,Is.EqualTo(CommanderVoiceState.Error));Assert.That(controller.LastError,Is.Not.Empty);
            }finally{CommanderVoiceSettings.VoiceEnabled=prior;}
        }

        [TestCase(float.NaN)][TestCase(float.PositiveInfinity)]
        public void CorruptStoredDuration_IsNormalizedBeforeCapture(float value)
        {
            const string key="voice_commander_max_duration";bool had=PlayerPrefs.HasKey(key);
            float prior=PlayerPrefs.GetFloat(key,15);bool enabled=CommanderVoiceSettings.VoiceEnabled;
            try
            {
                PlayerPrefs.SetFloat(key,value);CommanderVoiceSettings.VoiceEnabled=true;
                backend.ClipSeconds=15;using var controller=new CommanderVoiceInputController(new Speech(),Create());
                Assert.That(controller.StartRecording("fixture"),Is.True);
                Assert.That(backend.RequestedSeconds,Is.EqualTo(15));controller.Cancel();
            }
            finally {if(had)PlayerPrefs.SetFloat(key,prior);else PlayerPrefs.DeleteKey(key);CommanderVoiceSettings.VoiceEnabled=enabled;}
        }

        [TestCase(float.NaN)][TestCase(float.PositiveInfinity)][TestCase(float.NegativeInfinity)]
        public void ControllerRejectsNonfiniteExplicitDuration_BeforeOpeningCapture(float value)
        {
            bool prior=CommanderVoiceSettings.VoiceEnabled;
            try
            {
                CommanderVoiceSettings.VoiceEnabled=true;
                using var controller=new CommanderVoiceInputController(new Speech(),Create());
                Assert.That(controller.StartRecording("fixture",value),Is.False);
                Assert.That(backend.Starts,Is.Zero);
            }finally{CommanderVoiceSettings.VoiceEnabled=prior;}
        }

        [Test]public void QuarantinedNativeStop_IsExplainedAsBusyNotWrongConnection()
        {
            bool prior=CommanderVoiceSettings.VoiceEnabled;
            try
            {
                CommanderVoiceSettings.VoiceEnabled=true;backend.Position=16000;
                Create().StartRecording("fixture",3);backend.ThrowEnd=true;capture.StopRecording();
                var otherBackend=new Backend();
                using var other=(UnityMicrophoneAudioCapture)typeof(UnityMicrophoneAudioCapture).GetConstructor(new[]{typeof(ICommanderMicrophoneBackend)}).Invoke(new object[]{otherBackend});
                using var controller=new CommanderVoiceInputController(new Speech(),other);
                Assert.That(controller.StartRecording("fixture",3),Is.False);
                Assert.That(otherBackend.Starts,Is.Zero);Assert.That(controller.LastError,Does.Contain("finishing"));
            }finally{CommanderVoiceSettings.VoiceEnabled=prior;}
        }

        [Test]public void NativeOpeningMayInitiallyReportNotRecording_ThenBecomesListening()
        {
            backend.Position=0;Create().StartRecording("fixture",3);backend.Recording=false;
            Assert.That(Ready.CaptureEnded,Is.False,"A pending native start is not an early terminal capture.");
            backend.Now=.1;backend.Recording=true;backend.Position=1600;
            Assert.That(Ready.IsListening,Is.True);
        }

        private sealed class Speech:ICommanderSpeechToTextProvider
        {public int Calls;public bool IsAvailable=>true;public void Dispose(){}public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token){Calls++;return Task.FromResult(CommanderSpeechToTextResult.Accepted("fixture"));}}
        private sealed class Backend:ICommanderMicrophoneBackend
        {
            public string[] DeviceList={"fixture"};public string[] Devices=>DeviceList;public double Now;public double RealtimeSeconds=>Now;
            public int Rate=16000,Channels=1,ClipSeconds=3,Position=16000,Starts,EndCalls,Reads,Releases,RequestedSeconds;
            public bool Recording,ThrowCaps,ThrowEnd,ReadSucceeds=true;public float? Fill;private AudioClip clip;
            public void GetDeviceCaps(string device,out int minimum,out int maximum){if(ThrowCaps)throw new InvalidOperationException("fixture caps failure");minimum=maximum=0;}
            public AudioClip Start(string device,bool loop,int seconds,int requestedRate)
            {
                Starts++;RequestedSeconds=seconds;Assert.That(loop,Is.False,"Hardware capture is bounded and must not wrap/re-record indefinitely.");Recording=true;
                clip=AudioClip.Create("synthetic-native-fixture",Rate*ClipSeconds,Channels,Rate,false);var data=new float[clip.samples*Channels];
                for(int i=0;i<data.Length;i++)data[i]=Fill??(i==data.Length-1||i==47999?.75f:.125f);
                Assert.That(clip.SetData(data,0),Is.True);return clip;
            }
            public int GetPosition(string device)=>Position;public bool IsRecording(string device)=>Recording;
            public void End(string device){EndCalls++;if(ThrowEnd)throw new InvalidOperationException("fixture end failure");Recording=false;}
            public bool ReadSamples(AudioClip value,float[] samples){Reads++;Assert.That(Recording,Is.False,"Read only after native stop.");return ReadSucceeds&&value.GetData(samples,0);}
            public void ReleaseClip(AudioClip value){Releases++;Assert.That(Recording,Is.False,"Never free a clip still in use.");UnityEngine.Object.DestroyImmediate(value);clip=null;}
            public void Cleanup(){if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);clip=null;}
        }
    }
}
