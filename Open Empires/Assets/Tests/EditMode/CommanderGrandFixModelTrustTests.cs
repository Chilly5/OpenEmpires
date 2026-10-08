using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixModelTrustTests
    {
        private string root,file;
        private const string AbcSha="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        [SetUp]public void Setup(){root=Path.Combine(Path.GetTempPath(),"OpenEmpires-model-trust-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);file=Path.Combine(root,"ggml-tiny.bin");File.WriteAllText(file,"abc");}
        [TearDown]public void Cleanup(){string absolute=Path.GetFullPath(root);if(Path.GetDirectoryName(absolute)==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)&&Path.GetFileName(absolute).StartsWith("OpenEmpires-model-trust-"))Directory.Delete(absolute,true);}
        private IDisposable Open(string path,long bytes=3,string sha=AbcSha)
        {
            var type=typeof(WhisperModelLocator).Assembly.GetType("OpenEmpires.WhisperModelVerifier");
            var method=type?.GetMethod("OpenVerified",BindingFlags.Public|BindingFlags.Static);
            Assert.That(method,Is.Not.Null,"Native model loading requires a bounded trusted-verification lease.");
            try{return (IDisposable)method.Invoke(null,new object[]{path,bytes,sha});}
            catch(TargetInvocationException e){throw e.InnerException;}
        }
        [Test]public void CorrectTrustedBytes_AreAcceptedWithoutChangingTheFile(){using var lease=Open(file);Assert.That(lease,Is.Not.Null);Assert.That(File.ReadAllText(file),Is.EqualTo("abc"));}
        [Test]public void WrongHash_IsRejected(){Assert.Throws<InvalidDataException>(()=>Open(file,3,new string('0',64)));}
        [Test]public void WrongSize_IsRejectedBeforeLoading(){Assert.Throws<InvalidDataException>(()=>Open(file,4));}
        [Test]public void MissingModel_IsRejected(){Assert.Throws<FileNotFoundException>(()=>Open(Path.Combine(root,"missing.bin")));}
        [Test]public void VerificationLease_PreventsMutationBetweenHashAndNativeLoad()
        {using(var lease=Open(file)){Assert.Throws<IOException>(()=>{using var write=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);});}using var after=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);Assert.That(after.CanWrite,Is.True);}
        [Test]public void UnsafeModelName_DoesNotResolveAnArbitraryExistingFile()
        {Assert.That(WhisperModelLocator.TryResolveModelPath("../ggml-tiny.bin",out _,file),Is.False);}
        [Test]public void UnknownModelName_DoesNotGrantAnExplicitUnverifiedLoadPath()
        {Assert.That(WhisperModelLocator.TryResolveModelPath("unknown.bin",out _,file),Is.False);}
        [Test]public void ExplicitMissingPath_DoesNotSilentlyFallbackToAnotherModelCopy()
        {Assert.That(WhisperModelLocator.TryResolveModelPath("ggml-tiny.bin",out _,Path.Combine(root,"missing.bin")),Is.False);}
        private void CheckWebDistribution()
        {
            var type=typeof(WhisperModelLocator).Assembly.GetType("OpenEmpires.WhisperDistributionPolicy");
            var method=type?.GetMethod("ValidateWebStreamingAssets",BindingFlags.Public|BindingFlags.Static);
            Assert.That(method,Is.Not.Null,"Web packaging needs a native payload exclusion gate.");
            try{method.Invoke(null,new object[]{root});}catch(TargetInvocationException e){throw e.InnerException;}
        }
        [Test]public void WebDistribution_NativeModelPayloadIsRejected(){Assert.Throws<InvalidDataException>(CheckWebDistribution);}
        [Test]public void WebDistribution_WorkletWithoutNativePayloadIsAllowed()
        {File.Delete(file);File.WriteAllText(Path.Combine(root,"commander-pcm-worklet.js"),"fixture worklet");Assert.DoesNotThrow(CheckWebDistribution);}
    }
}
