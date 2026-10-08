using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires.TestSupport
{
    // Benchmark transport only. The caller must supply a recorded, verified
    // operator billing basis/minimum/quantum; there is no guessed/default pricing.
    // Existing speech provider still owns explicit consent, encoding and affinity.
    public sealed class BudgetedSpeechTransport:ICommanderGatewayTransport
    {
        private readonly ICommanderGatewayTransport inner;private readonly EvidenceBudgetJournal journal;
        private readonly double minimum,quantum;private readonly string basis;private bool disposed;
        public BudgetedSpeechTransport(ICommanderGatewayTransport inner,EvidenceBudgetJournal journal,double minimum,double quantum,string billingBasis)
        {
            if(inner==null||journal==null||double.IsNaN(minimum)||double.IsInfinity(minimum)||minimum<=0||minimum>600
                ||double.IsNaN(quantum)||double.IsInfinity(quantum)||quantum<=0||quantum>600
                ||billingBasis==null||!System.Text.RegularExpressions.Regex.IsMatch(billingBasis,@"^[a-z0-9][a-z0-9-]{0,59}$")||billingBasis.StartsWith("sk-"))throw new InvalidOperationException("A verified bounded billing basis is required before a benchmark upload.");
            this.inner=inner;this.journal=journal;this.minimum=minimum;this.quantum=quantum;basis=billingBasis;
        }
        public async Task<CommanderGatewayResponse> SendAsync(string gateway,Guid jobId,string sessionToken,string policyVersion,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken token)
        {
            if(disposed)throw new ObjectDisposedException(nameof(BudgetedSpeechTransport));token.ThrowIfCancellationRequested();
            if(kind!=CommanderGatewayRequestKind.Speech)throw new InvalidOperationException("The speech benchmark transport cannot proxy another service.");
            if(jobId==Guid.Empty)throw new InvalidOperationException("A recording job is required.");
            double duration=Duration(body),charge=Math.Max(minimum,Math.Ceiling(duration/quantum)*quantum);
            string reservation=journal.Reserve("cloud-asr",charge);journal.RecordAudioDetails(reservation,duration,minimum,quantum,basis);
            int status=0;string category="attempt-error";
            try
            {
                var response=await inner.SendAsync(gateway,jobId,sessionToken,policyVersion,language,body,kind,token).ConfigureAwait(false);
                status=(int)(response?.Status??0);category="response";return response;
            }
            catch(OperationCanceledException){category="cancelled";throw;}
            catch(System.Net.Http.HttpRequestException){category="network-error";throw;}
            finally{journal.Complete(reservation,status,category);}
        }
        private static double Duration(byte[] wav)
        {
            if(wav==null||wav.Length<3244||wav.Length>1920044||Tag(wav,0)!="RIFF"||Tag(wav,8)!="WAVE"
                ||Tag(wav,12)!="fmt "||Tag(wav,36)!="data"||UInt(wav,4)!=(uint)wav.Length-8||UInt(wav,16)!=16
                ||Short(wav,20)!=1||Short(wav,22)!=1||UInt(wav,24)!=16000||UInt(wav,28)!=32000
                ||Short(wav,32)!=2||Short(wav,34)!=16||UInt(wav,40)!=(uint)wav.Length-44||(wav.Length-44)%2!=0)
                throw new InvalidOperationException("Benchmark audio must be the existing bounded mono16k PCM16 WAV format.");
            return (wav.Length-44)/32000d;
        }
        private static uint UInt(byte[] value,int i)=>(uint)(value[i]|value[i+1]<<8|value[i+2]<<16|value[i+3]<<24);
        private static int Short(byte[] value,int i)=>value[i]|value[i+1]<<8;
        private static string Tag(byte[] value,int i)=>Encoding.ASCII.GetString(value,i,4);
        public void Dispose(){if(disposed)return;disposed=true;inner.Dispose();}
    }
}
