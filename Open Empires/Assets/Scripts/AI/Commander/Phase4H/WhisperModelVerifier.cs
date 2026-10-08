using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public static class WhisperModelVerifier
    {
        /// <summary>Hold this read lease across native model creation. Windows
        /// sharing denies mutation/deletion between checksum and loader open.</summary>
        public static IDisposable OpenVerified(string path,long expectedBytes,string expectedSha256)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("Native model files are unavailable in Web.");
#else
            if(expectedBytes<1||expectedBytes>1600000000||expectedSha256==null||!Regex.IsMatch(expectedSha256,"^[a-f0-9]{64}$"))
                throw new InvalidDataException("MODEL_MANIFEST_INVALID");
            if(string.IsNullOrWhiteSpace(path)||Path.GetExtension(path)!=".bin")throw new InvalidDataException("MODEL_PATH_INVALID");
            string absolute=Path.GetFullPath(path);
            if((File.GetAttributes(absolute)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("MODEL_PATH_INVALID");
            var stream=new FileStream(absolute,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.SequentialScan);
            try
            {
                if(stream.Length!=expectedBytes)throw new InvalidDataException("MODEL_SIZE_MISMATCH");
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                if(hash!=expectedSha256)throw new InvalidDataException("MODEL_HASH_MISMATCH");
                stream.Position=0;return stream;
            }
            catch{stream.Dispose();throw;}
#endif
        }
    }
}
