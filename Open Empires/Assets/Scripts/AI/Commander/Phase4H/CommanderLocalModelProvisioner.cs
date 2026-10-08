using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public sealed class CommanderModelProvisionResult
    {
        public bool Success { get; }
        public string Code { get; }
        public string Path { get; }
        private CommanderModelProvisionResult(bool success,string code,string path){Success=success;Code=code;Path=path;}
        internal static CommanderModelProvisionResult Verified(string path)=>new CommanderModelProvisionResult(true,"MODEL_VERIFIED",path);
        internal static CommanderModelProvisionResult Failed(string code)=>new CommanderModelProvisionResult(false,code,null);
    }

    /// <summary>Desktop player acquisition, not recognition or gameplay. Catalog and
    /// Unity paths are captured by caller; file/network/hash work stays off the UI.</summary>
    public sealed class CommanderLocalModelProvisioner:IDisposable
    {
        private static readonly SemaphoreSlim ActualWork=new SemaphoreSlim(1,1);
        private readonly object gate=new object();private readonly string root;
        private readonly IReadOnlyList<WhisperTrustedModel> catalog;private readonly HttpClient client;
        private CancellationTokenSource activeCancellation;private Task<CommanderModelProvisionResult> active;
        private volatile bool disposed;private long received,total;
        public long ReceivedBytes=>Interlocked.Read(ref received);
        public long TotalBytes=>Interlocked.Read(ref total);
        public CommanderLocalModelProvisioner(string cacheRoot,IReadOnlyList<WhisperTrustedModel> catalog,HttpMessageHandler handler=null)
        {
            root=System.IO.Path.GetFullPath(cacheRoot??throw new ArgumentNullException(nameof(cacheRoot)));
            this.catalog=catalog??throw new ArgumentNullException(nameof(catalog));
            if(catalog.Count<1||catalog.Count>8)throw new ArgumentException("MODEL_MANIFEST_INVALID");
            client=handler==null?new HttpClient():new HttpClient(handler,true);client.Timeout=TimeSpan.FromMinutes(15);
        }
        public Task<CommanderModelProvisionResult> ImportAsync(string model,string source,CancellationToken token)
            =>Begin(model,source,false,token);
        public Task<CommanderModelProvisionResult> DownloadAsync(string model,CancellationToken token)
            =>Begin(model,null,false,token);
        public Task<CommanderModelProvisionResult> VerifyAsync(string model,CancellationToken token)
            =>Begin(model,null,true,token);
        private Task<CommanderModelProvisionResult> Begin(string name,string source,bool verifyOnly,CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Task.FromResult(CommanderModelProvisionResult.Failed("MODEL_PLATFORM_UNAVAILABLE"));
#else
            WhisperTrustedModel model=null;foreach(var entry in catalog)if(entry.FileName==name){model=entry;break;}
            if(model==null)return Task.FromResult(CommanderModelProvisionResult.Failed("MODEL_UNKNOWN"));
            if(disposed||token.IsCancellationRequested)return Task.FromResult(CommanderModelProvisionResult.Failed("MODEL_CANCELLED"));
            if(!ActualWork.Wait(0))return Task.FromResult(CommanderModelProvisionResult.Failed("MODEL_BUSY"));
            lock(gate)
            {
                if(disposed){ActualWork.Release();return Task.FromResult(CommanderModelProvisionResult.Failed("MODEL_CANCELLED"));}
                activeCancellation=CancellationTokenSource.CreateLinkedTokenSource(token);activeCancellation.CancelAfter(TimeSpan.FromMinutes(15));
                var cancellation=activeCancellation;Interlocked.Exchange(ref received,0);Interlocked.Exchange(ref total,model.Bytes);
                active=Task.Run(async()=>
                {
                    try{return await Work(model,source,verifyOnly,cancellation.Token).ConfigureAwait(false);}
                    finally
                    {
                        lock(gate){if(ReferenceEquals(activeCancellation,cancellation))activeCancellation=null;}
                        cancellation.Dispose();ActualWork.Release();if(disposed)client.Dispose();
                    }
                });return active;
            }
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        private static void EnsureDirectory(string directory)
        {
            string full=System.IO.Path.GetFullPath(directory);string cursor=System.IO.Path.GetPathRoot(full);
            foreach(string part in full.Substring(cursor.Length).Split(new[]{System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries))
            {
                cursor=System.IO.Path.Combine(cursor,part);
                try{if((File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
                Directory.CreateDirectory(cursor);
            }
        }
        private static void Verify(string path,WhisperTrustedModel model,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using(var lease=WhisperModelVerifier.OpenVerified(path,model.Bytes,model.Sha256)){}
            token.ThrowIfCancellationRequested();
        }
        private void CheckStorage(long bytes)
        {
            long used=0;int count=0;
            foreach(string directory in Directory.EnumerateDirectories(root))
            {
                if(++count>32||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_CACHE_LIMIT");
                int files=0;foreach(string file in Directory.EnumerateFiles(directory))
                {if(++files>8||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");used=checked(used+new FileInfo(file).Length);}
            }
            if(used+bytes>3L*1024*1024*1024)throw new InvalidDataException("MODEL_CACHE_LIMIT");
            if(new DriveInfo(System.IO.Path.GetPathRoot(root)).AvailableFreeSpace<bytes+64L*1024*1024)throw new InvalidDataException("MODEL_DISK_FULL");
        }
        private void CleanupAbandonedStaging()
        {
            int directories=0,files=0;
            foreach(string directory in Directory.EnumerateDirectories(root))
            {
                if(++directories>32||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");
                if(!System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(directory),"^[a-f0-9]{64}$"))continue;
                foreach(string file in Directory.EnumerateFiles(directory))
                {
                    if(++files>256)throw new InvalidDataException("MODEL_CACHE_LIMIT");
                    if(!System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(file),@"^\.download-[a-f0-9]{32}\.partial\.bin$"))continue;
                    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");
                    File.Delete(file); // Exact owned staging name, only after exclusive root lease.
                }
            }
        }
        private async Task<CommanderModelProvisionResult> Work(WhisperTrustedModel model,string source,bool verifyOnly,CancellationToken token)
        {
            FileStream lockFile=null,output=null;Stream input=null;HttpResponseMessage response=null;string temporary=null;
            string lockPath=System.IO.Path.Combine(root,".provision.lock");
            try
            {
                token.ThrowIfCancellationRequested();EnsureDirectory(root);
                // File existence is not liveness: a process crash releases the OS
                // share lease, so an abandoned marker cannot permanently brick setup.
                try{lockFile=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
                catch(IOException){if(File.Exists(lockPath))return CommanderModelProvisionResult.Failed("MODEL_BUSY");throw;}
                CleanupAbandonedStaging();
                string directory=System.IO.Path.Combine(root,model.Sha256);EnsureDirectory(directory);
                string target=System.IO.Path.Combine(directory,model.FileName);
                if(File.Exists(target)){Verify(target,model,token);return CommanderModelProvisionResult.Verified(target);}
                if(verifyOnly)return CommanderModelProvisionResult.Failed("MODEL_MISSING");
                CheckStorage(model.Bytes);token.ThrowIfCancellationRequested();
                if(!string.IsNullOrWhiteSpace(source))
                {
                    string path=System.IO.Path.GetFullPath(source);
                    if(System.IO.Path.GetExtension(path)!=".bin"||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");
                    input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.SequentialScan);
                    if(input.Length!=model.Bytes)throw new InvalidDataException("MODEL_SIZE_MISMATCH");
                }
                else
                {
                    response=await client.GetAsync(model.DownloadUrl,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if(!response.IsSuccessStatusCode)return CommanderModelProvisionResult.Failed("MODEL_DOWNLOAD_FAILED");
                    if(response.Content.Headers.ContentLength.HasValue&&response.Content.Headers.ContentLength.Value!=model.Bytes)throw new InvalidDataException("MODEL_SIZE_MISMATCH");
                    input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                }
                temporary=System.IO.Path.Combine(directory,".download-"+Guid.NewGuid().ToString("N")+".partial.bin");
                output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
                var buffer=new byte[65536];long length=0;
                using(var sha=SHA256.Create())
                {
                    for(;;)
                    {
                        token.ThrowIfCancellationRequested();int n=await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false);if(n==0)break;
                        length=checked(length+n);if(length>model.Bytes)throw new InvalidDataException("MODEL_SIZE_MISMATCH");
                        sha.TransformBlock(buffer,0,n,null,0);await output.WriteAsync(buffer,0,n,token).ConfigureAwait(false);Interlocked.Exchange(ref received,length);
                    }
                    sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
                    if(length!=model.Bytes)throw new InvalidDataException("MODEL_SIZE_MISMATCH");
                    if(BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant()!=model.Sha256)throw new InvalidDataException("MODEL_HASH_MISMATCH");
                }
                await output.FlushAsync(token).ConfigureAwait(false);output.Dispose();output=null;
                Verify(temporary,model,token);token.ThrowIfCancellationRequested();
                File.Move(temporary,target);temporary=null;return CommanderModelProvisionResult.Verified(target);
            }
            catch(OperationCanceledException){return CommanderModelProvisionResult.Failed("MODEL_CANCELLED");}
            catch(HttpRequestException){return CommanderModelProvisionResult.Failed("MODEL_NETWORK_FAILED");}
            catch(FileNotFoundException){return CommanderModelProvisionResult.Failed("MODEL_MISSING");}
            catch(InvalidDataException e){return CommanderModelProvisionResult.Failed(SafeCode(e.Message));}
            catch(Exception){return CommanderModelProvisionResult.Failed("MODEL_PROVISION_FAILED");}
            finally
            {
                output?.Dispose();input?.Dispose();response?.Dispose();
                if(temporary!=null)try{File.Delete(temporary);}catch{}
                if(lockFile!=null){lockFile.Dispose();try{File.Delete(lockPath);}catch{}}
            }
        }
        private static string SafeCode(string code)
        {
            switch(code){case "MODEL_PATH_INVALID":case "MODEL_SIZE_MISMATCH":case "MODEL_HASH_MISMATCH":case "MODEL_CACHE_LIMIT":case "MODEL_DISK_FULL":return code;default:return "MODEL_PROVISION_FAILED";}
        }
#endif
        public void Dispose()
        {
            lock(gate){if(disposed)return;disposed=true;activeCancellation?.Cancel();if(active==null||active.IsCompleted)client.Dispose();}
        }
    }
}
