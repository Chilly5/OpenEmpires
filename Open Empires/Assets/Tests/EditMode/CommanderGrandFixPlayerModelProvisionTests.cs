using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixPlayerModelProvisionTests
    {
        private string root,source,cache;private readonly List<IDisposable> owned=new List<IDisposable>();
        private const string Sha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        private IReadOnlyList<WhisperTrustedModel> Models()=>WhisperModelCatalog.Parse("{\"schema\":\"openempires-whisper-models@1\",\"repository\":\"ggerganov/whisper.cpp\",\"revision\":\"5359861c739e955e79d9a303bcbc70fb988958b1\",\"models\":[{\"file\":\"ggml-tiny.bin\",\"language\":\"multilingual\",\"bytes\":3,\"sha256\":\""+Sha+"\",\"role\":\"fixture\"}]}");
        [SetUp]public void Setup(){root=Path.Combine(Path.GetTempPath(),"OpenEmpires-player-model-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);source=Path.Combine(root,"ggml-tiny.bin");File.WriteAllText(source,"abc");cache=Path.Combine(root,"cache");}
        [TearDown]public void Cleanup(){foreach(var value in owned)value.Dispose();owned.Clear();string p=Path.GetFullPath(root);if(Path.GetDirectoryName(p)==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)&&Path.GetFileName(p).StartsWith("OpenEmpires-player-model-"))Directory.Delete(p,true);}
        private object Create(HttpMessageHandler handler=null)
        {
            var type=typeof(WhisperModelLocator).Assembly.GetType("OpenEmpires.CommanderLocalModelProvisioner");
            Assert.That(type,Is.Not.Null,"Player provisioning cannot depend on Node/Python/developer tools.");
            var value=type.GetConstructor(new[]{typeof(string),typeof(IReadOnlyList<WhisperTrustedModel>),typeof(HttpMessageHandler)}).Invoke(new object[]{cache,Models(),handler});owned.Add((IDisposable)value);return value;
        }
        private async Task<object> Run(object value,string method,string model,string input=null,CancellationToken token=default)
        {
            object[] args=method=="ImportAsync"?new object[]{model,input,token}:new object[]{model,token};
            var task=(Task)value.GetType().GetMethod(method).Invoke(value,args);await task;return task.GetType().GetProperty("Result").GetValue(task);
        }
        private bool Success(object result)=>(bool)result.GetType().GetProperty("Success").GetValue(result);
        private string Code(object result)=>(string)result.GetType().GetProperty("Code").GetValue(result);
        private string Installed=>Path.Combine(cache,Sha,"ggml-tiny.bin");
        [Test]public async Task ManualImport_VerifiesAndPreservesSource_WithoutActivatingModel()
        {var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Success(result),Is.True);Assert.That(File.ReadAllText(source),Is.EqualTo("abc"));Assert.That(File.ReadAllText(Installed),Is.EqualTo("abc"));Assert.That(File.Exists(Path.Combine(cache,".provision.lock")),Is.False);}
        [Test]public async Task CorruptImport_IsRejectedWithNoPromotedModelOrPartial()
        {File.WriteAllText(source,"bad");var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Success(result),Is.False);Assert.That(Code(result),Is.EqualTo("MODEL_HASH_MISMATCH"));Assert.That(File.Exists(Installed),Is.False);Assert.That(Directory.GetFiles(Path.Combine(cache,Sha)),Is.Empty);}
        [Test]public async Task Download_UsesOnlyPinnedModelUrl_AndNeverSendsAuthorization()
        {
            var handler=new Handler(request=>{Assert.That(request.RequestUri.ToString(),Is.EqualTo("https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-tiny.bin"));Assert.That(request.Headers.Authorization,Is.Null);return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("abc")};});
            var result=await Run(Create(handler),"DownloadAsync","ggml-tiny.bin");Assert.That(Success(result),Is.True);Assert.That(File.ReadAllText(Installed),Is.EqualTo("abc"));
        }
        [Test]public async Task UnknownName_IsRejectedBeforeFileOrNetworkWork()
        {int calls=0;var handler=new Handler(_=>{calls++;throw new InvalidOperationException("fixture");});var result=await Run(Create(handler),"DownloadAsync","../anything.bin");Assert.That(Code(result),Is.EqualTo("MODEL_UNKNOWN"));Assert.That(calls,Is.Zero);Assert.That(Directory.Exists(cache),Is.False);}
        [Test]public async Task OversizedResponse_IsRejectedBeforePromotion()
        {var handler=new Handler(_=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("abcd")});var result=await Run(Create(handler),"DownloadAsync","ggml-tiny.bin");Assert.That(Code(result),Is.EqualTo("MODEL_SIZE_MISMATCH"));Assert.That(File.Exists(Installed),Is.False);}
        [Test]public async Task CorruptInstalledModel_IsNotOverwrittenByAnotherImport()
        {Directory.CreateDirectory(Path.GetDirectoryName(Installed));File.WriteAllText(Installed,"bad");var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Code(result),Is.EqualTo("MODEL_HASH_MISMATCH"));Assert.That(File.ReadAllText(Installed),Is.EqualTo("bad"));}
        [Test]public async Task PrecancelledOperation_ProducesNoFilesOrRequests()
        {using var cts=new CancellationTokenSource();cts.Cancel();var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source,cts.Token);Assert.That(Code(result),Is.EqualTo("MODEL_CANCELLED"));Assert.That(Directory.Exists(cache),Is.False);}
        [Test]public async Task ExistingAcquisitionLock_IsNotStolenOrDeleted()
        {Directory.CreateDirectory(cache);string path=Path.Combine(cache,".provision.lock");File.WriteAllText(path,"fixture lock");using(var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Code(result),Is.EqualTo("MODEL_BUSY"));}Assert.That(File.ReadAllText(path),Is.EqualTo("fixture lock"));}
        [Test]public async Task AbandonedLockFile_DoesNotPermanentlyBlockPlayerSetup()
        {Directory.CreateDirectory(cache);File.WriteAllText(Path.Combine(cache,".provision.lock"),"abandoned fixture marker");var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Success(result),Is.True);Assert.That(File.ReadAllText(Installed),Is.EqualTo("abc"));}
        [Test]public async Task CrashLeftover_CleansOnlyOwnedStagingFilesAfterExclusiveLease()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Installed));string partial=Path.Combine(Path.GetDirectoryName(Installed),".download-0123456789abcdef0123456789abcdef.partial.bin");
            string unrelated=Path.Combine(Path.GetDirectoryName(Installed),"operator-note.txt");File.WriteAllText(partial,"unfinished");File.WriteAllText(unrelated,"preserve");
            var result=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Success(result),Is.True);Assert.That(File.Exists(partial),Is.False);Assert.That(File.ReadAllText(unrelated),Is.EqualTo("preserve"));
        }
        [Test]public async Task SafeNetworkCategory_DoesNotExposeRawExceptionOrUrl()
        {var handler=new Handler(_=>throw new HttpRequestException("fixture-only private upstream detail"));var result=await Run(Create(handler),"DownloadAsync","ggml-tiny.bin");Assert.That(Code(result),Is.EqualTo("MODEL_NETWORK_FAILED"));Assert.That(File.Exists(Installed),Is.False);}
        [Test]public async Task CancelIgnoringTransport_RetainsActualSlotUntilItTerminates()
        {
            var handler=new IgnoringHandler();using var cts=new CancellationTokenSource();var first=Create(handler);
            var pending=Run(first,"DownloadAsync","ggml-tiny.bin",token:cts.Token);await handler.Entered.Task;cts.Cancel();
            var second=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Code(second),Is.EqualTo("MODEL_BUSY"));Assert.That(pending.IsCompleted,Is.False);
            handler.Completed.SetResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("abc")});
            Assert.That(Code(await pending),Is.EqualTo("MODEL_CANCELLED"));var third=await Run(Create(),"ImportAsync","ggml-tiny.bin",source);Assert.That(Success(third),Is.True);
        }
        private sealed class Handler:HttpMessageHandler
        {private readonly Func<HttpRequestMessage,HttpResponseMessage> callback;public Handler(Func<HttpRequestMessage,HttpResponseMessage> value){callback=value;}protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(callback(request));}
        private sealed class IgnoringHandler:HttpMessageHandler
        {public readonly TaskCompletionSource<bool> Entered=new TaskCompletionSource<bool>();public readonly TaskCompletionSource<HttpResponseMessage> Completed=new TaskCompletionSource<HttpResponseMessage>();protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Entered.TrySetResult(true);return Completed.Task;}}
    }
}
