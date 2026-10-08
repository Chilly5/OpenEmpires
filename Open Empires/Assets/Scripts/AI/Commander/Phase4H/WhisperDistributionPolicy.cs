using System;
using System.Collections.Generic;
using System.IO;

namespace OpenEmpires
{
    public static class WhisperDistributionPolicy
    {
        public static void ValidateWebStreamingAssets(string root)
        {
            if(!Directory.Exists(root))return;
            var models=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var model in WhisperModelCatalog.Load())models.Add(model.FileName);
            var pending=new Stack<string>();pending.Push(Path.GetFullPath(root));int count=0;
            while(pending.Count>0)
            {
                string directory=pending.Pop();
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("WEB_STREAMING_LINK_UNSUPPORTED");
                foreach(var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if(++count>4096)throw new InvalidDataException("WEB_STREAMING_INVENTORY_LIMIT");
                    var attributes=File.GetAttributes(entry);
                    if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("WEB_STREAMING_LINK_UNSUPPORTED");
                    if((attributes&FileAttributes.Directory)!=0){pending.Push(entry);continue;}
                    string name=Path.GetFileName(entry);
                    if(models.Contains(name)||name.Equals("libwhisper.dll",StringComparison.OrdinalIgnoreCase)
                        ||Path.GetExtension(name).Equals(".bin",StringComparison.OrdinalIgnoreCase)
                          &&Path.GetFileName(directory).Equals("Whisper",StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("WEB_NATIVE_MODEL_PAYLOAD_FORBIDDEN");
                }
            }
        }
    }
}
