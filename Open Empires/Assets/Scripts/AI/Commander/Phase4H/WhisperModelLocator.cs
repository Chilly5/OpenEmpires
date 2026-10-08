using System;
using System.IO;
using UnityEngine;

namespace OpenEmpires
{
    public static class WhisperModelLocator
    {
        public const string DefaultModelName = "ggml-tiny.bin";
        public static string WritableModelRoot => Path.Combine(Application.persistentDataPath,"CommanderVoice","Models");
        public static string CachePath(string root,WhisperTrustedModel model) => Path.Combine(Path.GetFullPath(root),model.Sha256,model.FileName);
        public static bool TryResolveModelPath(string modelName,out string resolvedPath,string explicitPath=null)
        {
            resolvedPath=null;
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if(!WhisperModelCatalog.TryGet(modelName,out var model))return false;
            try
            {
                // An explicit missing choice is not permission to downgrade/fallback.
                if(!string.IsNullOrWhiteSpace(explicitPath))
                {
                    string chosen=Path.GetFullPath(explicitPath);
                    if(Path.GetExtension(chosen)!=".bin"||!File.Exists(chosen))return false;
                    resolvedPath=chosen;return true;
                }
                string persistent=CachePath(WritableModelRoot,model);
                if(File.Exists(persistent)){resolvedPath=persistent;return true;}
#if UNITY_EDITOR
                string developer=CachePath(Path.Combine(Path.GetDirectoryName(Application.dataPath),"LocalModels","Whisper"),model);
                if(File.Exists(developer)){resolvedPath=developer;return true;}
#endif
                // No native payload is required in shared StreamingAssets. Cheap
                // resolution is not verification; the worker verifies before load.
                return false;
            }
            catch{return false;}
#endif
        }
    }
}
