using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class WhisperTrustedModel
    {
        public string FileName { get; }
        public string Language { get; }
        public long Bytes { get; }
        public string Sha256 { get; }
        public string Revision { get; }
        public string DownloadUrl => "https://huggingface.co/ggerganov/whisper.cpp/resolve/"+Revision+"/"+FileName;
        internal WhisperTrustedModel(string file,string language,long bytes,string sha,string revision)
        {FileName=file;Language=language;Bytes=bytes;Sha256=sha;Revision=revision;}
    }

    /// <summary>One bundled model-acquisition manifest; not a gameplay database or
    /// accuracy ranking. Capture on Unity's caller thread before background I/O.</summary>
    public static class WhisperModelCatalog
    {
        public static IReadOnlyList<WhisperTrustedModel> Load()
        {
            var asset=Resources.Load<TextAsset>("CommanderVoice/whisper-models");
            if(asset==null||asset.text.Length>16384)throw new InvalidOperationException("MODEL_MANIFEST_UNAVAILABLE");
            return Parse(asset.text);
        }
        public static bool TryGet(string name,out WhisperTrustedModel model)
        {
            model=null;string file=string.IsNullOrWhiteSpace(name)?WhisperModelLocator.DefaultModelName:name.Trim();
            if(!Regex.IsMatch(file,@"^ggml-[a-z0-9.]+\.bin$")||file.Length>64)return false;
            try{foreach(var entry in Load())if(entry.FileName==file){model=entry;return true;}}catch{return false;}
            return false;
        }
        public static IReadOnlyList<WhisperTrustedModel> Parse(string json)
        {
            if(string.IsNullOrEmpty(json)||json.Length>16384)throw new InvalidOperationException("MODEL_MANIFEST_INVALID");
            var dto=JsonConvert.DeserializeObject<Catalog>(json,new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error,MaxDepth=6});
            if(dto==null||dto.schema!="openempires-whisper-models@1"||dto.repository!="ggerganov/whisper.cpp"
                ||dto.revision==null||!Regex.IsMatch(dto.revision,"^[a-f0-9]{40}$")||dto.models==null||dto.models.Length<1||dto.models.Length>8)
                throw new InvalidOperationException("MODEL_MANIFEST_INVALID");
            var items=new List<WhisperTrustedModel>();var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in dto.models)
            {
                if(entry==null||entry.file==null||entry.file.Length>64||!Regex.IsMatch(entry.file,@"^ggml-[a-z0-9.]+\.bin$")||!names.Add(entry.file)
                    ||entry.bytes<1||entry.bytes>1600000000||entry.sha256==null||!Regex.IsMatch(entry.sha256,"^[a-f0-9]{64}$")
                    ||entry.language!="en"&&entry.language!="multilingual"||entry.role==null||entry.role.Length>256)
                    throw new InvalidOperationException("MODEL_MANIFEST_INVALID");
                items.Add(new WhisperTrustedModel(entry.file,entry.language,entry.bytes,entry.sha256,dto.revision));
            }
            return new ReadOnlyCollection<WhisperTrustedModel>(items);
        }
        [Serializable]private sealed class Catalog {public string schema,repository,revision,provenance;public Entry[] models;}
        [Serializable]private sealed class Entry {public string file,language,sha256,role;public long bytes;}
    }
}
