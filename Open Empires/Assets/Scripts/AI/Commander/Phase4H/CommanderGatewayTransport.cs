using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace OpenEmpires
{
    public enum CommanderGatewayRequestKind { Policy, Speech, Semantic }

    public sealed class CommanderGatewayResponse
    {
        public long Status { get; }
        public string Body { get; }
        public string ErrorCode { get; }
        public CommanderGatewayResponse(long status,string body,string errorCode=null)
        {Status=status;Body=body??string.Empty;ErrorCode=errorCode??string.Empty;}
    }

    public interface ICommanderGatewayTransport : IDisposable
    {
        Task<CommanderGatewayResponse> SendAsync(string gateway,Guid jobId,string sessionToken,
            string policyVersion,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken token);
    }

    /// <summary>Main-thread platform transport only; no gameplay state or authority.</summary>
    public sealed class CommanderGatewayTransport : ICommanderGatewayTransport
    {
        public const int MaximumResponseBytes=65536;
        private bool disposed;
#if UNITY_WEBGL && !UNITY_EDITOR
        private CommanderGatewayWebReceiver receiver;
#endif
        public async Task<CommanderGatewayResponse> SendAsync(string gateway,Guid jobId,string sessionToken,
            string policyVersion,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken token)
        {
            if(disposed)throw new ObjectDisposedException(nameof(CommanderGatewayTransport));
            token.ThrowIfCancellationRequested();body??=Array.Empty<byte>();
            if(!Uri.TryCreate(gateway,UriKind.Absolute,out Uri url)||url.UserInfo.Length!=0||url.Query.Length!=0||url.Fragment.Length!=0
                ||url.AbsolutePath!="/"||url.Scheme!="https"&&!(url.Scheme=="http"&&url.IsLoopback)
                ||body.Length>(kind==CommanderGatewayRequestKind.Speech?2097152:65536))
                return new CommanderGatewayResponse(0,null,"GATEWAY_SETUP_REQUIRED");
#if UNITY_WEBGL && !UNITY_EDITOR
            if(receiver==null){var obj=new GameObject("CommanderGateway-"+Guid.NewGuid().ToString("N"));
                UnityEngine.Object.DontDestroyOnLoad(obj);receiver=obj.AddComponent<CommanderGatewayWebReceiver>();}
            return await receiver.SendAsync(url.GetLeftPart(UriPartial.Authority),jobId,sessionToken,policyVersion,language,body,kind,token);
#else
            string path=kind==CommanderGatewayRequestKind.Policy?"/api/commander/voice-policy":kind==CommanderGatewayRequestKind.Speech?"/api/commander/stt":"/api/commander/semantic";
            using var request=new UnityWebRequest(url.GetLeftPart(UriPartial.Authority)+path,kind==CommanderGatewayRequestKind.Policy?"GET":"POST");
            using var download=new BoundedDownload();request.downloadHandler=download;request.redirectLimit=0;request.timeout=30;
            if(kind!=CommanderGatewayRequestKind.Policy){request.uploadHandler=new UploadHandlerRaw(body);request.SetRequestHeader("Authorization","Bearer "+sessionToken);}
            if(kind==CommanderGatewayRequestKind.Speech){request.SetRequestHeader("Content-Type","audio/wav");request.SetRequestHeader("X-Voice-Job",jobId.ToString());
                request.SetRequestHeader("X-Voice-Consent",policyVersion);request.SetRequestHeader("X-Voice-Language",language);}
            if(kind==CommanderGatewayRequestKind.Semantic){request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("X-Commander-Job",jobId.ToString());}
            var operation=request.SendWebRequest();float start=Time.realtimeSinceStartup;
            try {
                while(!operation.isDone){token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException();
                    if(Time.realtimeSinceStartup-start>30)throw new TimeoutException();await Task.Yield();}
                token.ThrowIfCancellationRequested();
                if(download.Overflow)return new CommanderGatewayResponse(request.responseCode,null,"RESPONSE_TOO_LARGE");
                if(request.result==UnityWebRequest.Result.ConnectionError||request.result==UnityWebRequest.Result.DataProcessingError)
                    return new CommanderGatewayResponse(request.responseCode,null,"GATEWAY_NETWORK_ERROR");
                try{return new CommanderGatewayResponse(request.responseCode,new UTF8Encoding(false,true).GetString(download.Bytes));}
                catch(DecoderFallbackException){return new CommanderGatewayResponse(request.responseCode,null,"INVALID_GATEWAY_RESPONSE");}
            }finally{if(!operation.isDone)request.Abort();}
#endif
        }
        public void Dispose(){if(disposed)return;disposed=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(receiver!=null){receiver.CancelAll();UnityEngine.Object.Destroy(receiver.gameObject);receiver=null;}
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        private sealed class BoundedDownload : DownloadHandlerScript
        {
            private readonly MemoryStream content=new MemoryStream();public bool Overflow{get;private set;}
            public byte[] Bytes=>content.ToArray();public BoundedDownload():base(new byte[8192]){}
            protected override void ReceiveContentLengthHeader(ulong length){if(length>MaximumResponseBytes)Overflow=true;}
            protected override bool ReceiveData(byte[] data,int length){if(Overflow||length<0||content.Length+length>MaximumResponseBytes){Overflow=true;return false;}
                if(length>0)content.Write(data,0,length);return true;}
            public override void Dispose(){content.Dispose();base.Dispose();}
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [Preserve]
    public sealed class CommanderGatewayWebReceiver : MonoBehaviour
    {
        [DllImport("__Internal")]private static extern void CommanderGateway_Begin(string receiver,string id,string gateway,string token,string policy,string language,byte[] body,int length,int kind);
        [DllImport("__Internal")]private static extern void CommanderGateway_Cancel(string id);
        [DllImport("__Internal")]private static extern void CommanderGateway_Dispose(string receiver);
        [DllImport("__Internal")]private static extern int CommanderGateway_ResponseSize(string id);
        [DllImport("__Internal")]private static extern int CommanderGateway_ReadResponse(string id,byte[] body,int capacity);
        private readonly Dictionary<string,TaskCompletionSource<CommanderGatewayResponse>> pending=new Dictionary<string,TaskCompletionSource<CommanderGatewayResponse>>();
        private bool disposed;
        public async Task<CommanderGatewayResponse> SendAsync(string gateway,Guid job,string sessionToken,string policy,string language,byte[] body,CommanderGatewayRequestKind kind,CancellationToken token)
        {
            if(disposed||pending.Count>=2)return new CommanderGatewayResponse(0,null,"STT_BUSY");
            string id=job.ToString();if(pending.ContainsKey(id))return new CommanderGatewayResponse(0,null,"DUPLICATE_JOB");
            var source=new TaskCompletionSource<CommanderGatewayResponse>();pending.Add(id,source);
            try {
                CommanderGateway_Begin(gameObject.name,id,gateway,sessionToken??"",policy??"",language??"en",body,body.Length,(int)kind);
                float start=Time.realtimeSinceStartup;
                while(!source.Task.IsCompleted){token.ThrowIfCancellationRequested();if(disposed)throw new OperationCanceledException();
                    if(Time.realtimeSinceStartup-start>31)throw new TimeoutException();await Task.Yield();}
                token.ThrowIfCancellationRequested();return await source.Task;
            }finally{pending.Remove(id);CommanderGateway_Cancel(id);}
        }
        [Preserve] public void OnCommanderGatewayResponse(string value)
        {
            if(disposed||string.IsNullOrEmpty(value)||value.Length>256)return;
            Notification notification;try{notification=JsonConvert.DeserializeObject<Notification>(value,new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error});}catch{return;}
            if(notification==null||!Guid.TryParse(notification.jobId,out Guid _)||!pending.TryGetValue(notification.jobId,out var source)||source.Task.IsCompleted)return;
            var safe=new HashSet<string>{"","GATEWAY_SETUP_REQUIRED","DUPLICATE_JOB","STT_BUSY","TIMEOUT_OR_CANCELLED","RESPONSE_TOO_LARGE","GATEWAY_NETWORK_ERROR"};
            if(!safe.Contains(notification.error??"")||notification.status<0||notification.status>599)return;
            if(!string.IsNullOrEmpty(notification.error)){source.TrySetResult(new CommanderGatewayResponse(notification.status,null,notification.error));return;}
            int length=CommanderGateway_ResponseSize(notification.jobId);
            if(length<0||length>CommanderGatewayTransport.MaximumResponseBytes){source.TrySetResult(new CommanderGatewayResponse(0,null,"INVALID_GATEWAY_RESPONSE"));return;}
            var bytes=new byte[length];if(CommanderGateway_ReadResponse(notification.jobId,bytes,length)!=length)return;
            try{source.TrySetResult(new CommanderGatewayResponse(notification.status,new UTF8Encoding(false,true).GetString(bytes)));}
            catch(DecoderFallbackException){source.TrySetResult(new CommanderGatewayResponse(0,null,"INVALID_GATEWAY_RESPONSE"));}
        }
        public void CancelAll(){if(disposed)return;disposed=true;CommanderGateway_Dispose(gameObject.name);
            foreach(var entry in pending)entry.Value.TrySetCanceled();pending.Clear();}
        private void OnDestroy()=>CancelAll();
        private sealed class Notification {public string jobId;public long status;public string error;}
    }
#endif
}
