using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenEmpires
{
    public interface ICommanderCaptureReadiness
    {
        bool PermissionReady{get;}bool IsListening{get;}bool CaptureEnded{get;}
        string CaptureError{get;}void SetHeldKey(string code);
        Task<bool> RequestPermissionAsync(CancellationToken token);
    }
    public static class CommanderAudioCaptureFactory
    {
        public static ICommanderAudioCapture Create(){
#if UNITY_WEBGL && !UNITY_EDITOR
            return new CommanderBrowserAudioCapture();
#else
            return new UnityMicrophoneAudioCapture();
#endif
        }
    }
    /// <summary>Owns browser mic/PCM only; shares normal controller/transcript pipeline.</summary>
    public sealed class CommanderBrowserAudioCapture : ICommanderAudioCapture,ICommanderAsyncAudioCapture,ICommanderCaptureReadiness
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private CommanderBrowserMicReceiver receiver;
        private CommanderBrowserMicReceiver Receiver{get{if(receiver==null){var obj=new GameObject("CommanderMic-"+Guid.NewGuid().ToString("N"));
            UnityEngine.Object.DontDestroyOnLoad(obj);receiver=obj.AddComponent<CommanderBrowserMicReceiver>();}return receiver;}}
#endif
        private bool disposed,started;private string job,permissionJob,heldKey="";
        public bool IsRecording=>started&&!disposed;public string CurrentDevice{get;private set;}="Browser default microphone";
        public bool PermissionReady{
            get{
#if UNITY_WEBGL && !UNITY_EDITOR
                return !disposed&&CommanderBrowserMicReceiver.PermissionReady();
#else
                return false;
#endif
            }}
        public bool IsListening=>!disposed&&Stage=="listening";
        public bool CaptureEnded=>!disposed&&(Stage=="done"||Stage=="cancelled"||Stage=="error");
        public string CaptureError{
            get{
#if UNITY_WEBGL && !UNITY_EDITOR
                return receiver!=null?receiver.Code:"";
#else
                return "MIC_PLATFORM_UNAVAILABLE";
#endif
            }}
        private string Stage{
            get{
#if UNITY_WEBGL && !UNITY_EDITOR
                return receiver!=null?receiver.Stage:"";
#else
                return "";
#endif
            }}
        public void SetHeldKey(string code)=>heldKey=code??"";
        public async Task<bool> RequestPermissionAsync(CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed||started)return false;if(PermissionReady)return true;
            permissionJob=Guid.NewGuid().ToString();string id=permissionJob;Receiver.Expect(id);CommanderBrowserMicReceiver.Permission(Receiver.gameObject.name,id);
            float start=Time.realtimeSinceStartup;
            try{while(Stage!="permission"&&Stage!="error"&&Stage!="cancelled"){
                    token.ThrowIfCancellationRequested();if(disposed||id!=permissionJob||Time.realtimeSinceStartup-start>15)return false;await Task.Yield();}
                token.ThrowIfCancellationRequested();return Stage=="permission"&&PermissionReady;
            }finally{CommanderBrowserMicReceiver.Cancel(id);if(permissionJob==id)permissionJob=null;}
#else
            await Task.CompletedTask;return false;
#endif
        }
        public bool StartRecording(string deviceName=null,float maxDurationSeconds=15)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed||started||!PermissionReady||float.IsNaN(maxDurationSeconds)||float.IsInfinity(maxDurationSeconds))return false;
            maxDurationSeconds=Mathf.Clamp(maxDurationSeconds,1,60);job=Guid.NewGuid().ToString();started=true;
            CurrentDevice=string.IsNullOrEmpty(deviceName)?"Browser default microphone":deviceName;Receiver.Expect(job);
            CommanderBrowserMicReceiver.Begin(Receiver.gameObject.name,job,Application.streamingAssetsPath.TrimEnd('/')+"/CommanderVoice/commander-pcm-worklet.js",deviceName??"",heldKey,maxDurationSeconds);
            if(Stage=="error"||Stage=="cancelled"){CancelRecording();return false;}return true;
#else
            return false;
#endif
        }
        public CommanderAudioData StopRecording()=>throw new InvalidOperationException("Browser capture requires asynchronous final-tail collection.");
        public async Task<CommanderAudioData> StopRecordingAsync(CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed||!started||job==null)return CommanderAudioData.Empty;string id=job;CommanderBrowserMicReceiver.Stop(id);
            float start=Time.realtimeSinceStartup;
            try{
                while(Stage!="done"&&Stage!="error"&&Stage!="cancelled"){
                    token.ThrowIfCancellationRequested();if(disposed||id!=job)throw new OperationCanceledException();
                    if(Time.realtimeSinceStartup-start>6)throw new TimeoutException();await Task.Yield();}
                token.ThrowIfCancellationRequested();if(disposed||id!=job)throw new OperationCanceledException();
                if(Stage=="cancelled")return CommanderAudioData.Empty;if(Stage!="done")throw new InvalidOperationException("Browser microphone could not finalize audio.");
                int frames=CommanderBrowserMicReceiver.FrameCount(id),rate=CommanderBrowserMicReceiver.Rate(id);
                if(rate<8000||rate>96000||frames<0||frames>rate*60)throw new InvalidOperationException("Invalid browser audio metadata.");
                if(frames==0)return CommanderAudioData.Empty;var samples=new float[frames];
                if(CommanderBrowserMicReceiver.Read(id,samples,frames)!=frames)throw new InvalidOperationException("Invalid browser audio transfer.");
                return new CommanderAudioData(samples,rate,1);
            }finally{CommanderBrowserMicReceiver.Cancel(id);if(job==id){started=false;job=null;}}
#else
            await Task.CompletedTask;return CommanderAudioData.Empty;
#endif
        }
        public void CancelRecording(){
#if UNITY_WEBGL && !UNITY_EDITOR
            if(job!=null)CommanderBrowserMicReceiver.Cancel(job);if(permissionJob!=null)CommanderBrowserMicReceiver.Cancel(permissionJob);
#endif
            started=false;job=permissionJob=null;}
        public void Dispose(){if(disposed)return;CancelRecording();disposed=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(receiver!=null){receiver.DisposeOwned();UnityEngine.Object.Destroy(receiver.gameObject);receiver=null;}
#endif
        }
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    [Preserve]public sealed class CommanderBrowserMicReceiver:MonoBehaviour
    {
        [DllImport("__Internal",EntryPoint="CommanderMic_Permission")]public static extern void Permission(string receiver,string id);
        [DllImport("__Internal",EntryPoint="CommanderMic_Begin")]public static extern void Begin(string receiver,string id,string url,string device,string key,float seconds);
        [DllImport("__Internal",EntryPoint="CommanderMic_Stop")]public static extern void Stop(string id);
        [DllImport("__Internal",EntryPoint="CommanderMic_Cancel")]public static extern void Cancel(string id);
        [DllImport("__Internal",EntryPoint="CommanderMic_Dispose")]private static extern void Dispose(string receiver);
        [DllImport("__Internal",EntryPoint="CommanderMic_PermissionReady")]private static extern int IsPermissionReady();
        [DllImport("__Internal",EntryPoint="CommanderMic_FrameCount")]public static extern int FrameCount(string id);
        [DllImport("__Internal",EntryPoint="CommanderMic_Rate")]public static extern int Rate(string id);
        [DllImport("__Internal",EntryPoint="CommanderMic_Read")]public static extern int Read(string id,float[] samples,int capacity);
        private string expected;private bool disposed;public string Stage{get;private set;}="";public string Code{get;private set;}="";
        public static bool PermissionReady()=>IsPermissionReady()!=0;
        public void Expect(string id){expected=id;Stage=Code="";}
        [Preserve]public void OnCommanderMicrophoneState(string message){
            if(disposed||string.IsNullOrEmpty(message)||message.Length>256)return;Notice notice;
            try{notice=JsonConvert.DeserializeObject<Notice>(message,new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error,MaxDepth=4});}catch{return;}
            if(notice==null||notice.jobId!=expected||Array.IndexOf(new[]{"permission","opening","listening","done","error","cancelled"},notice.stage)<0)return;
            if(notice.code!=null&&notice.code.Length>48)return;if(Stage=="done"||Stage=="cancelled"||Stage=="error")return;
            Stage=notice.stage;Code=notice.code??"";
        }
        public void DisposeOwned(){if(disposed)return;disposed=true;Dispose(gameObject.name);expected=null;}
        private void OnDestroy()=>DisposeOwned();
        private sealed class Notice{public string jobId;public string stage;public string code;}
    }
#endif
}
