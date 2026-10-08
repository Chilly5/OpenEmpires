using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    /// <summary>Explicitly consented bounded audio→plain text. Never a planner.</summary>
    public sealed class CommanderGatewaySpeechToTextProvider : ICommanderSpeechToTextProvider,ICommanderVoiceSessionProvider
    {
        // Actual work, not a UI/provider instance, owns this slot. No replacement queue.
        private static readonly SemaphoreSlim OnlineSlot=new SemaphoreSlim(1,1);
        private readonly string gateway,language,policyVersion;
        private readonly Func<string> tokenSource;
        private readonly ICommanderGatewayTransport transport;
        private bool consent,disposed;private string approvedToken;private int generation;
        private VoiceJob activeJob;private Task<CommanderSpeechToTextResult> actual;private Task release;
        public string Disclosure{get;}
        public bool HasConsent=>consent&&!disposed;
        public bool IsAvailable=>HasConsent&&ValidToken(ReadToken())&&(actual==null||actual.IsCompleted);
        public event Action SessionInvalidated;

        public CommanderGatewaySpeechToTextProvider(string gateway,Func<string> tokenSource,string policyJson,
            ICommanderGatewayTransport transport=null,string language="en")
        {
            if(!Uri.TryCreate(gateway,UriKind.Absolute,out var uri)||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0
                ||uri.AbsolutePath!="/"||uri.Scheme!="https"&&!(uri.Scheme=="http"&&uri.IsLoopback))throw new ArgumentException("GATEWAY_SETUP_REQUIRED");
            this.gateway=uri.GetLeftPart(UriPartial.Authority);this.tokenSource=tokenSource??throw new ArgumentNullException(nameof(tokenSource));
            if(!new[]{"en","fr","de","es","it","pt","ja","ko","zh"}.Contains(language))throw new ArgumentException("INVALID_LANGUAGE");
            this.language=language;
            var policy=ReadObject(policyJson,16384);
            RequireFields(policy,"version","operator","provider","model","retention","purpose","applicationRetention","maxDurationSeconds","audioFormat");
            policyVersion=Text(policy,"version",80);
            if(!Regex.IsMatch(policyVersion,@"^[a-zA-Z0-9.-]+$")||Text(policy,"provider",64)!="OpenAI"||Text(policy,"model",80)!="gpt-transcribe"
                ||policy["maxDurationSeconds"]?.Type!=JTokenType.Integer||policy["maxDurationSeconds"].Value<int>()!=60
                ||Text(policy,"audioFormat",64)!="pcm16-wav-mono16k")throw new ArgumentException("INVALID_VOICE_POLICY");
            Disclosure="Destination: "+this.gateway+"\nOperator: "+Text(policy,"operator",128)+"\nProvider: OpenAI — gpt-transcribe (rolling alias; quality not yet measured)"
                +"\nPurpose: "+Text(policy,"purpose",2048)+"\nUpstream retention: "+Text(policy,"retention",2048)
                +"\nApplication retention: "+Text(policy,"applicationRetention",2048)
                +"\nMicrophone permission is NOT audio-upload consent. On-device Windows is an alternative."
                +"\nTranscripts still require review; semantic translation and plan approval are separate.";
            this.transport=transport??new CommanderGatewayTransport();
        }
        public bool ConsentToOnlineAudio()
        {
            if(disposed||!ValidToken(ReadToken()))return false;
            if(!consent){approvedToken=ReadToken();generation++;consent=true;}return true;
        }
        public void RevokeConsent(){if(disposed)return;consent=false;approvedToken=null;generation++;CancelVoiceSession();SessionInvalidated?.Invoke();}
        public void RefreshSessionBinding(){if(consent&&ReadToken()!=approvedToken)RevokeConsent();}
        public bool TryBeginVoiceSession(out CommanderSpeechToTextResult blocker)
        {
            RefreshSessionBinding();blocker=null;
            if(disposed){blocker=CommanderSpeechToTextResult.Cancelled();return false;}
            if(!consent){blocker=Failure("CONSENT_REQUIRED","Open Voice setup and explicitly allow audio transcription online, or choose on-device/text input.");return false;}
            string token=ReadToken();if(!ValidToken(token)){blocker=Failure("BACKEND_SESSION_REQUIRED","Enter the operator-issued service session, not a provider API key. Multiplayer sign-in is not required.");return false;}
            if(activeJob!=null||actual!=null&&!actual.IsCompleted||!OnlineSlot.Wait(0)){blocker=Failure("STT_BUSY","The previous online transcription is still finishing. No replacement recording was started.");return false;}
            activeJob=new VoiceJob(Guid.NewGuid(),generation,token);return true;
        }
        public Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken token)
        {
            var job=activeJob;
            if(job==null||job.Started)return Task.FromResult(Failure("VOICE_SESSION_REQUIRED","Start a new explicitly consented recording before transcription."));
            job.Started=true;actual=RunAsync(job,audio,token);return actual;
        }
        private async Task<CommanderSpeechToTextResult> RunAsync(VoiceJob job,CommanderAudioData audio,CancellationToken token)
        {
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,job.Cancel.Token);
            try {
                if(!Current(job,linked.Token))return CommanderSpeechToTextResult.Cancelled();
                var bytes=await EncodeWavAsync(audio,linked.Token);
                if(bytes==null)return CommanderSpeechToTextResult.Empty();
                if(!Current(job,linked.Token))return CommanderSpeechToTextResult.Cancelled();
                var response=await transport.SendAsync(gateway,job.Id,job.Token,policyVersion,language,bytes,CommanderGatewayRequestKind.Speech,linked.Token);
                if(!Current(job,linked.Token))return CommanderSpeechToTextResult.Cancelled();
                if(response==null||response.ErrorCode.Length!=0||response.Status!=200)return ResponseFailure(response);
                var result=ReadObject(response.Body,65536);RequireFields(result,"jobId","policyVersion","text");
                if(Text(result,"jobId",36)!=job.Id.ToString()||Text(result,"policyVersion",80)!=policyVersion)throw new ArgumentException("INVALID_GATEWAY_RESPONSE");
                string text=Text(result,"text",4096,true);
                return string.IsNullOrWhiteSpace(text)?CommanderSpeechToTextResult.Empty():CommanderSpeechToTextResult.Accepted(text,language,float.NaN);
            }catch(OperationCanceledException){return CommanderSpeechToTextResult.Cancelled();}
            catch(InvalidAudioException){return Failure("INVALID_AUDIO","Recording format, samples or length is invalid. No audio was uploaded.");}
            catch(ArgumentException){return Failure("INVALID_GATEWAY_RESPONSE","The gateway returned an invalid or mismatched transcript. No order was submitted; reconnect in Voice setup.");}
            catch(Exception){return Failure("GATEWAY_UNAVAILABLE","Online transcription is unavailable. No order was submitted; check setup or choose on-device/text input.");}
            finally {if(ReferenceEquals(activeJob,job))activeJob=null;ReleaseJob(job);}
        }
        private bool Current(VoiceJob job,CancellationToken token)=>!disposed&&consent&&job.Generation==generation&&!job.Cancel.IsCancellationRequested
            &&!token.IsCancellationRequested&&ReadToken()==job.Token&&approvedToken==job.Token;
        public void CancelVoiceSession(){var job=activeJob;if(job==null)return;activeJob=null;generation++;job.Cancel.Cancel();if(!job.Started)ReleaseJob(job);}
        private static void ReleaseJob(VoiceJob job){if(job.Released)return;job.Released=true;job.Cancel.Dispose();OnlineSlot.Release();}
        private string ReadToken(){try{return tokenSource()??"";}catch{return "";}}
        private static bool ValidToken(string token)=>token!=null&&token.Length==36&&Guid.TryParseExact(token,"D",out _);
        private static CommanderSpeechToTextResult Failure(string code,string message)=>CommanderSpeechToTextResult.Failure(code,message);
        private static CommanderSpeechToTextResult ResponseFailure(CommanderGatewayResponse response)
        {
            long code=response?.Status??0;
            return code==401?Failure("GATEWAY_AUTH","Gateway session expired. Sign in again and reconnect in Voice setup.")
                :code==403?Failure("GATEWAY_NOT_ENABLED","The operator has not enabled this service session for funded transcription.")
                :code==429?Failure("GATEWAY_QUOTA_OR_BUSY","Transcription is busy or its budget is exhausted. Wait or use on-device/text input.")
                :code==409?Failure("GATEWAY_JOB_OR_POLICY","Recording was already used or processing policy changed. Review Voice setup; do not retry old audio.")
                :Failure("GATEWAY_UNAVAILABLE","Online transcription failed. No order was submitted; check Voice setup or use on-device/text input.");
        }
        public void Dispose(){if(disposed)return;consent=false;CancelVoiceSession();disposed=true;
            if(actual!=null&&!actual.IsCompleted)release=ReleaseTransportAfter(actual);else transport.Dispose();}
        private async Task ReleaseTransportAfter(Task pending){try{await pending;}catch{}transport.Dispose();}
        private sealed class VoiceJob
        {
            public readonly Guid Id;public readonly int Generation;public readonly string Token;public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
            public bool Started,Released;public VoiceJob(Guid id,int generation,string token){Id=id;Generation=generation;Token=token;}
        }
        private static JObject ReadObject(string text,int maxBytes)
        {
            if(text==null||text.Length>maxBytes||Encoding.UTF8.GetByteCount(text)>maxBytes)throw new ArgumentException("INVALID_GATEWAY_RESPONSE");
            try{using var reader=new JsonTextReader(new StringReader(text)){MaxDepth=8,DateParseHandling=DateParseHandling.None};
                var result=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                if(reader.Read())throw new ArgumentException("INVALID_GATEWAY_RESPONSE");return result;
            }catch(Exception){throw new ArgumentException("INVALID_GATEWAY_RESPONSE");}
        }
        private static void RequireFields(JObject value,params string[] fields){if(value.Count!=fields.Length||value.Properties().Any(p=>!fields.Contains(p.Name)))throw new ArgumentException("INVALID_GATEWAY_RESPONSE");}
        private static string Text(JObject value,string name,int max,bool empty=false){var item=value[name];if(item?.Type!=JTokenType.String)throw new ArgumentException("INVALID_GATEWAY_RESPONSE");
            string text=item.Value<string>();if(text==null||text.Length>max||!empty&&string.IsNullOrWhiteSpace(text))throw new ArgumentException("INVALID_GATEWAY_RESPONSE");return text;}
        private sealed class InvalidAudioException:Exception{}
        private static async Task<byte[]> EncodeWavAsync(CommanderAudioData audio,CancellationToken token)
        {
            if(audio==null||audio.SampleRate<8000||audio.SampleRate>96000||audio.Channels<1||audio.Channels>2||audio.Samples.Length%audio.Channels!=0
                ||audio.DurationSeconds<.1f||audio.DurationSeconds>60f)throw new InvalidAudioException();
            bool nonzero=false;for(int i=0;i<audio.Samples.Length;i++){float value=audio.Samples[i];if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidAudioException();
                nonzero|=value!=0;if(i>0&&(i&65535)==0){token.ThrowIfCancellationRequested();await Task.Yield();}}
            token.ThrowIfCancellationRequested();if(!nonzero)return null;
            int frames=audio.Samples.Length/audio.Channels,count=(int)Math.Round((double)frames*16000/audio.SampleRate);
            if(count<1600||count>960000)throw new InvalidAudioException();var bytes=new byte[44+count*2];
            using var stream=new MemoryStream(bytes);using var writer=new BinaryWriter(stream,Encoding.ASCII,true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(bytes.Length-8);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(16000);writer.Write(32000);writer.Write((short)2);writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
            for(int i=0;i<count;i++){
                double position=(double)i*frames/count;int low=(int)position,high=Math.Min(low+1,frames-1);float fraction=(float)(position-low);
                float a=Mono(audio,low),b=Mono(audio,high),value=Math.Max(-1f,Math.Min(1f,a+(b-a)*fraction));
                writer.Write(value<=-1?short.MinValue:(short)Math.Round(value*short.MaxValue));
                if(i>0&&(i&65535)==0){token.ThrowIfCancellationRequested();await Task.Yield();}
            }
            token.ThrowIfCancellationRequested();return bytes;
        }
        private static float Mono(CommanderAudioData audio,int frame){int offset=frame*audio.Channels;return audio.Channels==1?audio.Samples[offset]:(audio.Samples[offset]+audio.Samples[offset+1])*.5f;}
    }
}
