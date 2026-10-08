#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Whisper;
using Whisper.Native;

namespace OpenEmpires
{
    // Reuse pinned public native ABI/types/plugins: no private pointer reflection,
    // ABI-layout duplication, cache mutation or new native library.
    internal static class CommanderWhisperNativeApi
    {
        internal static unsafe IntPtr Create(string path,bool useGpu)
        {
            var options=WhisperContextParams.GetDefaultParams();options.UseGpu=useGpu;
            byte[] model=File.ReadAllBytes(path);
            fixed(byte* data=model)return WhisperNative.whisper_init_from_buffer_with_params((IntPtr)data,new UIntPtr((uint)model.Length),options.NativeParams);
        }
        internal static unsafe CommanderSpeechToTextResult Transcribe(IntPtr context,CommanderAudioData audio,string language)
        {
            var options=WhisperParams.GetDefaultParams(WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY);
            options.Language=language;options.Translate=false;options.NoContext=true;options.SingleSegment=true;
            var native=options.NativeParams;
            // Bounded final transcript needs no managed callbacks. Do not allocate the
            // opaque wrapper's leaking GCHandle or invent access to private abort slots.
            native.new_segment_callback=null;native.new_segment_callback_user_data=IntPtr.Zero;
            native.progress_callback=null;native.progress_callback_user_data=IntPtr.Zero;
            int code;
            try { fixed(float* data=audio.Samples)code=WhisperNative.whisper_full(context,native,data,audio.Samples.Length); }
            finally { GC.KeepAlive(options); } // Own language/prompt pointers for the ENTIRE native call.
            if(code!=0)return CommanderSpeechToTextResult.Failure("TRANSCRIPTION_FAILED","The local voice engine could not process this recording.");
            int count=WhisperNative.whisper_full_n_segments(context);
            if(count<0||count>2048)return CommanderSpeechToTextResult.Failure("TRANSCRIPTION_TOO_LARGE","Record a shorter request.");
            var text=new StringBuilder();
            for(int i=0;i<count;i++)
            {
                string segment=ReadUtf8(WhisperNative.whisper_full_get_segment_text(context,i),16384);
                if(text.Length+segment.Length>4096)return CommanderSpeechToTextResult.Failure("TRANSCRIPTION_TOO_LARGE","Record a shorter request.");
                text.Append(segment);
            }
            string transcript=CommanderVoiceInputController.SafeCleanTranscript(text.ToString());
            if(string.IsNullOrWhiteSpace(transcript))return CommanderSpeechToTextResult.Empty();
            string detected=ReadUtf8(WhisperNative.whisper_lang_str(WhisperNative.whisper_full_lang_id(context)),32);
            return CommanderSpeechToTextResult.Accepted(transcript,detected,float.NaN);
        }
        internal static void Free(IntPtr context){if(context!=IntPtr.Zero)WhisperNative.whisper_free(context);}
        private static string ReadUtf8(IntPtr text,int maxBytes)
        {
            if(text==IntPtr.Zero)return string.Empty;
            int length=0;while(length<maxBytes&&Marshal.ReadByte(text,length)!=0)length++;
            if(length==maxBytes)throw new InvalidDataException("Native transcript exceeded its bound.");
            var bytes=new byte[length];Marshal.Copy(text,bytes,0,length);return new UTF8Encoding(false,true).GetString(bytes);
        }
    }
}
#endif
