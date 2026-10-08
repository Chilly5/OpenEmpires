using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public sealed class WhisperCommanderSpeechToTextProvider : ICommanderSpeechToTextProvider
    {
        private readonly CommanderWhisperContext context;
        private volatile bool disposed;
        private int activeWorker;
        private volatile string modelFailure;
        public bool IsAvailable => !disposed && context.IsAvailable;
        public string ModelPath { get; private set; }
        public long LastInferenceTimeMs { get; private set; }
        internal Task ReleaseCompletion => context.ReleaseCompletion;

        public WhisperCommanderSpeechToTextProvider(string modelName=WhisperModelLocator.DefaultModelName,
            string language="en",string explicitModelPath=null,bool useGpu=false)
        {
            string configuredLanguage=string.IsNullOrWhiteSpace(language)?"en":language.Trim();
#if UNITY_WEBGL && !UNITY_EDITOR
            // This native provider is not a Web adapter. The platform factory must
            // select the browser/gateway route, never desktop DLL/file loading.
            context=new CommanderWhisperContext(()=>IntPtr.Zero,
                (p,a)=>CommanderSpeechToTextResult.Failure("STT_PLATFORM_UNAVAILABLE","Choose browser transcription in voice settings."),p=>{});
#else
            // Capture Unity path properties on the caller thread. Actual model loading,
            // conversion and inference run on the bounded worker, not the UI callback.
            try{if(WhisperModelLocator.TryResolveModelPath(modelName,out string resolved,explicitModelPath))ModelPath=resolved;}
            catch(Exception){ModelPath=null;}
            WhisperModelCatalog.TryGet(modelName,out var model);
            context=new CommanderWhisperContext(
                ()=>
                {
                    if(ModelPath==null||model==null){modelFailure="MODEL_MISSING";return IntPtr.Zero;}
                    if(model.Language=="en"&&configuredLanguage!="en"){modelFailure="MODEL_LANGUAGE_UNSUPPORTED";return IntPtr.Zero;}
                    try{using(var lease=WhisperModelVerifier.OpenVerified(ModelPath,model.Bytes,model.Sha256))
                        return CommanderWhisperNativeApi.Create(ModelPath,useGpu);}
                    catch{modelFailure="MODEL_VERIFICATION_FAILED";return IntPtr.Zero;}
                },
                (p,a)=>CommanderWhisperNativeApi.Transcribe(p,a,configuredLanguage),
                CommanderWhisperNativeApi.Free);
#endif
        }

        // Retained explicit synchronous setup/benchmark API. Gameplay uses the async
        // worker and cannot queue another job behind an ignored cancellation.
        public bool Initialize()=>!disposed&&context.Initialize();

        public async Task<CommanderSpeechToTextResult> TranscribeAsync(CommanderAudioData audio,CancellationToken cancellationToken)
        {
            if(disposed||cancellationToken.IsCancellationRequested)return CommanderSpeechToTextResult.Cancelled();
            if(audio==null||audio.Samples==null||audio.Samples.Length==0)return CommanderSpeechToTextResult.Empty();
            if(audio.SampleRate<8000||audio.SampleRate>192000||audio.Channels<1||audio.Channels>8
                ||audio.Samples.Length%audio.Channels!=0||audio.DurationSeconds>CommanderVoiceSettings.MaximumDurationSeconds)
                return CommanderSpeechToTextResult.Failure("INVALID_AUDIO","Recording format or duration is unsupported; record a shorter clip.");
            if(Interlocked.CompareExchange(ref activeWorker,1,0)!=0)
                return CommanderSpeechToTextResult.Failure("STT_BUSY","The previous transcription is still finishing. Please wait.");
            try
            {
                return await Task.Run(()=>
                {
                    var watch=Stopwatch.StartNew();
                    try
                    {
                        if(disposed||cancellationToken.IsCancellationRequested)return CommanderSpeechToTextResult.Cancelled();
                        if(!context.Initialize())return CommanderSpeechToTextResult.Failure(modelFailure??context.FailureCode,
                            context.FailureCode=="STT_BUSY"?"An earlier voice engine is still releasing its model. Please wait."
                            :"Voice model unavailable. Provision a verified local model or choose the configured online route.");
                        var prepared=CommanderAudioConverter.ConvertToMono16k(audio);
                        bool hasSignal=false;
                        for(int i=0;i<prepared.Samples.Length;i++)
                        {
                            if(float.IsNaN(prepared.Samples[i])||float.IsInfinity(prepared.Samples[i]))
                                return CommanderSpeechToTextResult.Failure("INVALID_AUDIO","Recording contains invalid samples.");
                            hasSignal |= prepared.Samples[i]!=0f;
                        }
                        // Exact digital silence is not speech; some valid native
                        // models hallucinate text for it. No quiet-audio threshold.
                        if(disposed||cancellationToken.IsCancellationRequested)return CommanderSpeechToTextResult.Cancelled();
                        if(!hasSignal)return CommanderSpeechToTextResult.Empty();
                        var result=context.Transcribe(prepared);
                        return disposed||cancellationToken.IsCancellationRequested?CommanderSpeechToTextResult.Cancelled():result;
                    }
                    finally
                    {
                        watch.Stop();LastInferenceTimeMs=watch.ElapsedMilliseconds;
                        Interlocked.Exchange(ref activeWorker,0);
                    }
                }).ConfigureAwait(false);
            }
            catch(Exception)
            {
                Interlocked.Exchange(ref activeWorker,0);
                return CommanderSpeechToTextResult.Failure("TRANSCRIPTION_FAILED","Could not understand the recording.");
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;context.Dispose();}
    }
}
