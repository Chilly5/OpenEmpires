using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixNativeOwnershipTests
    {
        private static object Context(Func<IntPtr> create,Func<IntPtr,CommanderAudioData,CommanderSpeechToTextResult> infer,Action<IntPtr> free)
        {
            var type=typeof(CommanderVoiceInputController).Assembly.GetType("OpenEmpires.CommanderWhisperContext");
            Assert.That(type,Is.Not.Null,"Native context needs explicit call-owned, bounded, nonblocking lifetime.");
            return Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{create,infer,free},null);
        }
        private static object Call(object context,string method,params object[] args)=>context.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,args);
        private static Task Released(object context)=>(Task)context.GetType().GetProperty("ReleaseCompletion",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(context);
        private static CommanderAudioData Audio()=>new CommanderAudioData(new float[1600],16000,1);

        [Test]
        public async Task DisposeWhileNativeCallRuns_ReturnsAndFreesExactlyAfterCall()
        {
            using var entered=new ManualResetEventSlim();using var exit=new ManualResetEventSlim();int freed=0;bool premature=false;
            var context=Context(()=>new IntPtr(7),(ptr,a)=>{entered.Set();exit.Wait();return CommanderSpeechToTextResult.Accepted("fixture");},ptr=>{premature=!exit.IsSet;Interlocked.Increment(ref freed);});
            try
            {
                Assert.That((bool)Call(context,"Initialize"),Is.True);
                var inference=Task.Run(()=>(CommanderSpeechToTextResult)Call(context,"Transcribe",Audio()));
                Assert.That(await Task.Run(()=>entered.Wait(2000)),Is.True);
                var disposal=Task.Run(()=>((IDisposable)context).Dispose());
                var first=await Task.WhenAny(disposal,Task.Delay(200));
                int before=Volatile.Read(ref freed);exit.Set();await inference;await disposal;await Released(context);
                Assert.That(first,Is.SameAs(disposal));Assert.That(before,Is.Zero);Assert.That(premature,Is.False);Assert.That(freed,Is.EqualTo(1));
                ((IDisposable)context).Dispose();Assert.That(freed,Is.EqualTo(1));
            }
            finally{exit.Set();((IDisposable)context).Dispose();await Released(context);}
        }
        [Test]
        public async Task ActiveContext_RejectsSecondContextUntilReleaseCompletes()
        {
            using var releasing=new ManualResetEventSlim();using var releaseDone=new ManualResetEventSlim();int secondCreates=0;
            var first=Context(()=>new IntPtr(1),(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{releasing.Set();releaseDone.Wait();});
            var second=Context(()=>{secondCreates++;return new IntPtr(2);},(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{});
            try
            {
                Assert.That((bool)Call(first,"Initialize"),Is.True);
                ((IDisposable)first).Dispose();Assert.That(await Task.Run(()=>releasing.Wait(2000)),Is.True);
                Assert.That((bool)Call(second,"Initialize"),Is.False);Assert.That(secondCreates,Is.Zero);
                releaseDone.Set();await Released(first);
                Assert.That((bool)Call(second,"Initialize"),Is.True);Assert.That(secondCreates,Is.EqualTo(1));
            }
            finally{releaseDone.Set();((IDisposable)first).Dispose();((IDisposable)second).Dispose();await Released(first);await Released(second);}
        }
        [Test]
        public async Task FailedInferenceAndDisposedContext_DoNotDoubleFreeOrInvokeAgain()
        {
            int calls=0,freed=0;
            var context=Context(()=>new IntPtr(7),(p,a)=>{calls++;throw new InvalidOperationException("fixture failure");},p=>freed++);
            try
            {
                Assert.That((bool)Call(context,"Initialize"),Is.True);
                var result=(CommanderSpeechToTextResult)Call(context,"Transcribe",Audio());Assert.That(result.Success,Is.False);
                ((IDisposable)context).Dispose();await Released(context);
                var late=(CommanderSpeechToTextResult)Call(context,"Transcribe",Audio());Assert.That(late.Success,Is.False);
                Assert.That(calls,Is.EqualTo(1));Assert.That(freed,Is.EqualTo(1));
            }
            finally{((IDisposable)context).Dispose();await Released(context);}
        }
        [Test]
        public async Task DisposedDuringInitialization_DropsNewContextInsteadOfPublishingIt()
        {
            using var entered=new ManualResetEventSlim();using var exit=new ManualResetEventSlim();int freed=0;
            var context=Context(()=>{entered.Set();exit.Wait();return new IntPtr(7);},(p,a)=>CommanderSpeechToTextResult.Empty(),p=>freed++);
            try
            {
                var load=Task.Run(()=>(bool)Call(context,"Initialize"));Assert.That(await Task.Run(()=>entered.Wait(2000)),Is.True);
                ((IDisposable)context).Dispose();exit.Set();Assert.That(await load,Is.False);await Released(context);
                Assert.That(freed,Is.EqualTo(1));Assert.That((bool)Call(context,"Initialize"),Is.False);
            }
            finally{exit.Set();((IDisposable)context).Dispose();await Released(context);}
        }
        [Test]
        public async Task RetryAfterBusy_ReportsActualInitializationFailureNotOldBusyStatus()
        {
            var first=Context(()=>new IntPtr(1),(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{});
            var failed=Context(()=>IntPtr.Zero,(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{});
            try
            {
                Assert.That((bool)Call(first,"Initialize"),Is.True);
                Assert.That((bool)Call(failed,"Initialize"),Is.False);
                ((IDisposable)first).Dispose();await Released(first);
                Assert.That((bool)Call(failed,"Initialize"),Is.False);
                var status=(string)failed.GetType().GetProperty("FailureCode",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(failed);
                Assert.That(status,Is.EqualTo("STT_UNAVAILABLE"));
            }
            finally{((IDisposable)first).Dispose();((IDisposable)failed).Dispose();await Released(first);await Released(failed);}
        }
        [Test]
        public async Task UnknownNativeReleaseFailure_FailsClosedInsteadOfAllocatingAnotherModel()
        {
            var first=Context(()=>new IntPtr(1),(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{throw new InvalidOperationException("fixture unknown free result");});
            var next=Context(()=>new IntPtr(2),(p,a)=>CommanderSpeechToTextResult.Empty(),p=>{});
            bool rejected=false;
            try
            {
                Assert.That((bool)Call(first,"Initialize"),Is.True);((IDisposable)first).Dispose();await Released(first);
                rejected=!(bool)Call(next,"Initialize");
                Assert.That(rejected,Is.True,"Unknown native free outcome must not permit unbounded model replacements.");
                string failure=(string)first.GetType().GetProperty("FailureCode",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(first);
                Assert.That(failure,Is.EqualTo("STT_RELEASE_FAILED"));
            }
            finally
            {
                ((IDisposable)first).Dispose();((IDisposable)next).Dispose();await Released(next);
                // ONLY controlled fake-pointer fixture recovery; product code must
                // never expose a reset that could free/reuse an unknown native owner.
                if(rejected)
                {
                    var budget=(SemaphoreSlim)first.GetType().GetField("ContextBudget",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                    budget.Release();
                }
            }
        }
    }
}
