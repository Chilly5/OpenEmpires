using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    /// <summary>Preserves the existing Luna provider/strict decoder; swaps transport only.
    /// Never forwards a client service key or audio to semantic interpretation.</summary>
    public sealed class CommanderGatewaySemanticTransport:ICommanderHttpTransport,IDisposable
    {
        private static readonly SemaphoreSlim Slots=new SemaphoreSlim(2,2);
        private readonly string gateway;private readonly Func<string> tokenSource;private readonly ICommanderGatewayTransport transport;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();private int active;private bool disposed;
        internal bool IsConfigured => !disposed && !string.IsNullOrWhiteSpace(gateway) && Guid.TryParseExact(ReadToken(),"D",out _);
        public CommanderGatewaySemanticTransport(string gateway,Func<string> tokenSource,ICommanderGatewayTransport transport=null)
        {this.gateway=gateway??"";this.tokenSource=tokenSource??(()=>"");this.transport=transport??new CommanderGatewayTransport();}
        public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string json,IReadOnlyDictionary<string,string> headers,CancellationToken token)
        {
            if(uri==null||uri.Scheme!="https"||uri.Host!="openrouter.ai"||uri.AbsolutePath!="/api/v1/chat/completions"||uri.Query.Length!=0||uri.UserInfo.Length!=0)
                return new CommanderHttpResponse(400,"{\"error\":\"PROVIDER_NOT_SUPPORTED\"}");
            if(disposed||string.IsNullOrWhiteSpace(gateway))return new CommanderHttpResponse(503,"{\"error\":\"GATEWAY_SETUP_REQUIRED\"}");
            string session=ReadToken();if(!Guid.TryParseExact(session,"D",out _))return new CommanderHttpResponse(401,"{\"error\":\"SESSION_REQUIRED\"}");
            if(json==null||json.Length>65536||Encoding.UTF8.GetByteCount(json)>65536)return new CommanderHttpResponse(413,"{\"error\":\"REQUEST_TOO_LARGE\"}");
            if(!Slots.Wait(0))return new CommanderHttpResponse(429,"{\"error\":\"BUSY\"}");active++;
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
            try{
                var response=await transport.SendAsync(gateway,Guid.NewGuid(),session,"","en",Encoding.UTF8.GetBytes(json),CommanderGatewayRequestKind.Semantic,cancel.Token);
                cancel.Token.ThrowIfCancellationRequested();
                if(disposed||session!=ReadToken())return new CommanderHttpResponse(401,"{\"error\":\"SESSION_CHANGED\"}");
                if(response?.ErrorCode=="GATEWAY_NETWORK_ERROR")throw new System.Net.Http.HttpRequestException("Commander gateway connection failed.");
                if(response==null||response.ErrorCode.Length!=0)return new CommanderHttpResponse(503,"{\"error\":\"GATEWAY_UNAVAILABLE\"}");
                if(response.Body.Length>65536||Encoding.UTF8.GetByteCount(response.Body)>65536)throw new CommanderHttpResponseLimitException((int)response.Status);
                return new CommanderHttpResponse((int)response.Status,response.Body);
            }finally{Slots.Release();active--;if(disposed&&active==0)Release();}
        }
        private string ReadToken(){try{var value=tokenSource()??"";return value.Length==36?value:"";}catch{return "";}}
        private bool released;private void Release(){if(released)return;released=true;transport.Dispose();lifetime.Dispose();}
        public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();if(active==0)Release();}
    }
}
