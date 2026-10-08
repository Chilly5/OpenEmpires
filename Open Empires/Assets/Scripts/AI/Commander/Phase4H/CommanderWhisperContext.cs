using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    // Short ownership locks never surround native load/inference/free. This is an
    // ASR resource boundary, not provider JSON or gameplay authority.
    internal sealed class CommanderWhisperContext : IDisposable
    {
        private static readonly SemaphoreSlim ContextBudget = new SemaphoreSlim(1,1);
        private readonly object gate=new object();
        private readonly Func<IntPtr> create;
        private readonly Func<IntPtr,CommanderAudioData,CommanderSpeechToTextResult> infer;
        private readonly Action<IntPtr> free;
        private readonly TaskCompletionSource<bool> released=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr context;
        private bool busy,disposed,ownsBudget,releasing;
        internal Task ReleaseCompletion=>released.Task;
        internal bool IsAvailable{get{lock(gate)return !disposed&&context!=IntPtr.Zero;}}
        internal string FailureCode{get;private set;}="STT_UNAVAILABLE";
        internal CommanderWhisperContext(Func<IntPtr> create,Func<IntPtr,CommanderAudioData,CommanderSpeechToTextResult> infer,Action<IntPtr> free)
        {this.create=create??throw new ArgumentNullException(nameof(create));this.infer=infer??throw new ArgumentNullException(nameof(infer));this.free=free??throw new ArgumentNullException(nameof(free));}
        internal bool Initialize()
        {
            lock(gate)
            {
                if(disposed)return false;if(context!=IntPtr.Zero)return true;
                if(busy||!ContextBudget.Wait(0)){FailureCode="STT_BUSY";return false;}
                ownsBudget=true;busy=true;
            }
            IntPtr loaded=IntPtr.Zero;
            try{loaded=create();}catch(Exception){FailureCode="STT_UNAVAILABLE";}
            lock(gate)
            {
                context=loaded;busy=false;
                if(loaded==IntPtr.Zero){FailureCode="STT_UNAVAILABLE";ReleaseBudget();if(disposed)released.TrySetResult(true);return false;}
                if(disposed){ScheduleRelease();return false;}
                FailureCode=string.Empty;return true;
            }
        }
        internal CommanderSpeechToTextResult Transcribe(CommanderAudioData audio)
        {
            IntPtr owned;
            lock(gate)
            {
                if(disposed)return CommanderSpeechToTextResult.Cancelled();
                if(context==IntPtr.Zero)return CommanderSpeechToTextResult.Failure("STT_UNAVAILABLE","Voice engine is not loaded.");
                if(busy)return CommanderSpeechToTextResult.Failure("STT_BUSY","The voice engine is still finishing a recording.");
                busy=true;owned=context;
            }
            try{return infer(owned,audio);}
            catch(Exception){return CommanderSpeechToTextResult.Failure("TRANSCRIPTION_FAILED","Could not transcribe this recording.");}
            finally{lock(gate){busy=false;if(disposed)ScheduleRelease();}}
        }
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(!busy)ScheduleRelease();}}
        private void ScheduleRelease()
        {
            if(releasing)return;releasing=true;IntPtr owned=context;context=IntPtr.Zero;
            if(owned==IntPtr.Zero){ReleaseBudget();released.TrySetResult(true);return;}
            // Keep even expensive idle GPU/model cleanup off UI. Do not let the next
            // model acquire the single slot until native free has genuinely finished.
            _=Task.Run(()=>
            {
                bool success=false;
                try{free(owned);success=true;}catch(Exception){/* Outcome unknown: no retry/double free. */}
                finally
                {
                    lock(gate)
                    {
                        if(success)ReleaseBudget();
                        else FailureCode="STT_RELEASE_FAILED"; // Hold slot fail-closed until process restart.
                        released.TrySetResult(true);
                    }
                }
            });
        }
        private void ReleaseBudget(){if(ownsBudget){ownsBudget=false;ContextBudget.Release();}}
    }
}
