using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires.TestSupport
{
    // Current OpenRouter semantic path makes one initial POST and at most one
    // schema-only repair. An owned operation classifies those actual attempts;
    // arbitrary calls/out-of-operation/third-attempt fail before the transport.
    public sealed class BudgetedSemanticTransport:ICommanderHttpTransport
    {
        private readonly ICommanderHttpTransport inner;
        private readonly EvidenceBudgetJournal journal;
        private readonly object gate=new object();
        private readonly AsyncLocal<Guid> caller=new AsyncLocal<Guid>();
        private Guid active;
        private int attempts;
        private bool sending;
        public string LastGuardFailure { get; private set; }
        public int InitialHttpAttempts { get; private set; }
        public int RepairHttpAttempts { get; private set; }
        public int LastHttpStatus { get; private set; }
        public BudgetedSemanticTransport(ICommanderHttpTransport inner,EvidenceBudgetJournal journal)
        {this.inner=inner??throw new ArgumentNullException(nameof(inner));this.journal=journal??throw new ArgumentNullException(nameof(journal));}
        public IDisposable BeginSubmission()
        {
            lock(gate)
            {
                if(active!=Guid.Empty||sending)throw ScopeDenied();
                LastGuardFailure=null;
                Reserve("submission");active=Guid.NewGuid();caller.Value=active;attempts=0;return new Operation(this,active);
            }
        }
        public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string json,IReadOnlyDictionary<string,string> headers,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();string id;
            lock(gate)
            {
                if(active==Guid.Empty||caller.Value!=active||sending||attempts>=2)throw ScopeDenied();
                bool initial=attempts==0;id=Reserve(initial?"semantic-initial":"semantic-repair");
                if(initial)InitialHttpAttempts++;else RepairHttpAttempts++;attempts++;sending=true;
            }
            int status=0;string category="attempt-error";
            try
            {
                var response=await inner.PostJsonAsync(uri,json,headers,token).ConfigureAwait(false);
                status=response?.StatusCode??0;LastHttpStatus=status;category="response";return response;
            }
            catch(OperationCanceledException){category="cancelled";throw;}
            catch(CommanderHttpResponseLimitException error){status=error.StatusCode;LastHttpStatus=status;category="response-limit";throw;}
            catch(System.Net.Http.HttpRequestException){category="network-error";throw;}
            finally
            {
                // Unknown reservations remain consumed if terminal persistence fails.
                try{journal.Complete(id,status,category);}
                finally{lock(gate)sending=false;}
            }
        }
        private string Reserve(string kind)
        {
            try{return journal.Reserve(kind,0);}
            catch(InvalidOperationException error)
            {LastGuardFailure=error.Data["evidence_guard"] as string=="budget-exhausted"?"budget-exhausted":"budget-unavailable";throw;}
        }
        private static InvalidOperationException ScopeDenied()
        {var error=new InvalidOperationException("No owned bounded semantic HTTP attempt is available.");error.Data["evidence_guard"]="scope-denied";return error;}
        private sealed class Operation:IDisposable
        {
            private BudgetedSemanticTransport owner;private readonly Guid id;
            public Operation(BudgetedSemanticTransport owner,Guid id){this.owner=owner;this.id=id;}
            public void Dispose()
            {
                var current=Interlocked.Exchange(ref owner,null);if(current==null)return;
                lock(current.gate){if(current.active==id)current.active=Guid.Empty;if(current.caller.Value==id)current.caller.Value=Guid.Empty;}
            }
        }
    }
}
