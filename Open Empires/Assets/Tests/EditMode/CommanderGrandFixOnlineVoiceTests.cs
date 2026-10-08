using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixOnlineVoiceTests
    {
        private const string BackendToken="11111111-1111-4111-8111-111111111111";
        private const string Policy="{\"version\":\"fixture-v1\",\"operator\":\"Fixture operator\",\"provider\":\"OpenAI\",\"model\":\"gpt-transcribe\",\"retention\":\"Fixture vendor policy\",\"purpose\":\"Audio transcription only\",\"applicationRetention\":\"No stored audio/transcript\",\"maxDurationSeconds\":60,\"audioFormat\":\"pcm16-wav-mono16k\"}";
        private static ICommanderSpeechToTextProvider Create(FixtureTransport transport,Func<string> token=null)
        {
            var type=typeof(CommanderVoiceInputController).Assembly.GetType("OpenEmpires.CommanderGatewaySpeechToTextProvider");
            Assert.That(type,Is.Not.Null,"The actual shared online provider must exist; no fallback to native or mock.");
            return (ICommanderSpeechToTextProvider)Activator.CreateInstance(type,new object[]{"https://gateway.example.invalid",token??(()=>BackendToken),Policy,transport,"en"});
        }
        public static string FixturePolicy=>Policy;
        public static string FixtureBackendToken=>BackendToken;
        private static void Consent(ICommanderSpeechToTextProvider provider)=>provider.GetType().GetMethod("ConsentToOnlineAudio").Invoke(provider,null);
        private static void Revoke(ICommanderSpeechToTextProvider provider)=>provider.GetType().GetMethod("RevokeConsent").Invoke(provider,null);
        private static bool Begin(ICommanderSpeechToTextProvider provider)=>((ICommanderVoiceSessionProvider)provider).TryBeginVoiceSession(out var _);
        private static CommanderAudioData Audio(){var samples=new float[1600];for(int i=0;i<samples.Length;i++)samples[i]=.2f;return new CommanderAudioData(samples,16000,1);}

        [Test] public async Task MissingConsent_DoesNotFreezeCaptureOrUploadAudio()
        {
            var transport=new FixtureTransport();using var provider=Create(transport);
            Assert.That(Begin(provider),Is.False);var result=await provider.TranscribeAsync(Audio(),CancellationToken.None);
            Assert.That(result.Success,Is.False);Assert.That(transport.Calls,Is.Zero);
        }
        [Test] public async Task ExplicitConsent_BindsOneJobPolicyTokenAndExactPlainTranscript()
        {
            var transport=new FixtureTransport();using var provider=Create(transport);Consent(provider);Assert.That(Begin(provider),Is.True);
            var result=await provider.TranscribeAsync(Audio(),CancellationToken.None);
            Assert.That(result.Transcript,Is.EqualTo("make four spearmen but do not build anything"));
            Assert.That(float.IsNaN(result.Confidence),Is.True,"No fake confidence probability.");
            Assert.That(transport.Gateway,Is.EqualTo("https://gateway.example.invalid"));Assert.That(transport.Token,Is.EqualTo(BackendToken));
            Assert.That(transport.PolicyVersion,Is.EqualTo("fixture-v1"));Assert.That(transport.Kind,Is.EqualTo(CommanderGatewayRequestKind.Speech));
            Assert.That(Encoding.ASCII.GetString(transport.Body,0,4),Is.EqualTo("RIFF"));Assert.That(transport.Body.Length,Is.EqualTo(3244));
            Assert.That(BitConverter.ToInt32(transport.Body,24),Is.EqualTo(16000));Assert.That(transport.Language,Is.EqualTo("en"));
        }
        [Test] public async Task Revocation_CancelsActualUploadAndKeepsIgnoringLateTextInert()
        {
            var transport=new FixtureTransport{Deferred=true};using var provider=Create(transport);Consent(provider);Begin(provider);
            var task=provider.TranscribeAsync(Audio(),CancellationToken.None);Revoke(provider);
            bool cancelled=transport.Cancellation.IsCancellationRequested;transport.Complete();var result=await task;
            Assert.That(cancelled,Is.True);Assert.That(result.Success,Is.False);Assert.That(Begin(provider),Is.False);
        }
        [Test] public async Task CancelledIgnoringUpload_GlobalSlotPreventsReplacementAcrossProviders()
        {
            var transport=new FixtureTransport{Deferred=true};using var old=Create(transport);Consent(old);Begin(old);
            var task=old.TranscribeAsync(Audio(),CancellationToken.None);((ICommanderVoiceSessionProvider)old).CancelVoiceSession();
            using var replacement=Create(new FixtureTransport());Consent(replacement);bool accepted=Begin(replacement);
            transport.Complete();await task;Assert.That(accepted,Is.False,"No new provider instance may bypass actual-work ownership.");
            Assert.That(Begin(replacement),Is.True);
        }
        [TestCase("foreign")][TestCase("policy")][TestCase("extra")][TestCase("typed-text")]
        public async Task UntrustedResponse_MustMatchJobPolicyAndPlainTextSchema(string failure)
        {
            var transport=new FixtureTransport{ResponseFailure=failure};using var provider=Create(transport);Consent(provider);Begin(provider);
            var result=await provider.TranscribeAsync(Audio(),CancellationToken.None);Assert.That(result.Success,Is.False);
            Assert.That(result.Transcript,Is.Empty);Assert.That(result.UserFacingError,Does.Not.Contain(BackendToken));
        }
        [TestCase(false)][TestCase(true)]public async Task InvalidAudio_IsRejectedBeforeEncodingOrUpload(bool tooLong)
        {
            var transport=new FixtureTransport();using var provider=Create(transport);Consent(provider);Begin(provider);
            var samples=tooLong?new float[16000*61]:new[]{float.NaN};
            var result=await provider.TranscribeAsync(new CommanderAudioData(samples,16000,1),CancellationToken.None);
            Assert.That(result.Success,Is.False);Assert.That(transport.Calls,Is.Zero);
        }
        [Test]public async Task ExactZeroAudio_ProducesNoSpeechWithoutUploading()
        {
            var transport=new FixtureTransport();using var provider=Create(transport);Consent(provider);Begin(provider);
            var result=await provider.TranscribeAsync(new CommanderAudioData(new float[1600],16000,1),CancellationToken.None);
            Assert.That(result.ErrorCode,Is.EqualTo("EMPTY_TRANSCRIPTION"));Assert.That(transport.Calls,Is.Zero);
        }
        [Test]public async Task NewRecordingGetsNewJob_AndTokenChangeInvalidatesPendingResult()
        {
            string token=BackendToken;var transport=new FixtureTransport();using var provider=Create(transport,()=>token);Consent(provider);Begin(provider);
            await provider.TranscribeAsync(Audio(),CancellationToken.None);Guid first=transport.Job;transport.Deferred=true;Begin(provider);
            var task=provider.TranscribeAsync(Audio(),CancellationToken.None);Guid second=transport.Job;
            token="33333333-3333-4333-8333-333333333333";transport.Complete();var result=await task;
            Assert.That(second,Is.Not.EqualTo(first));Assert.That(result.Success,Is.False);
        }
        private sealed class FixtureTransport:ICommanderGatewayTransport
        {
            public bool Deferred;public string ResponseFailure;public int Calls;public string Gateway,Token,PolicyVersion,Language;
            public Guid Job;public byte[] Body;public CommanderGatewayRequestKind Kind;public CancellationToken Cancellation;
            private TaskCompletionSource<CommanderGatewayResponse> pending;
            public Task<CommanderGatewayResponse> SendAsync(string gateway,Guid jobId,string sessionToken,string policyVersion,string language,
                byte[] body,CommanderGatewayRequestKind kind,CancellationToken token)
            {
                Calls++;Gateway=gateway;Job=jobId;Token=sessionToken;PolicyVersion=policyVersion;Language=language;Body=body;Kind=kind;Cancellation=token;
                if(!Deferred)return Task.FromResult(Response());pending=new TaskCompletionSource<CommanderGatewayResponse>();return pending.Task;
            }
            private CommanderGatewayResponse Response()
            {
                string job=ResponseFailure=="foreign"?Guid.NewGuid().ToString():Job.ToString();string policy=ResponseFailure=="policy"?"other-policy":PolicyVersion;
                string text=ResponseFailure=="typed-text"?"{\"command\":\"approve\"}":"\"make four spearmen but do not build anything\"";
                string extra=ResponseFailure=="extra"?",\"approve\":true":"";
                return new CommanderGatewayResponse(200,"{\"jobId\":\""+job+"\",\"policyVersion\":\""+policy+"\",\"text\":"+text+extra+"}");
            }
            public void Complete()=>pending?.TrySetResult(Response());public void Dispose(){}
        }
    }
}
