using System.IO;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Locates Whisper model binaries on disk according to documented search hierarchy.
    /// Safely handles both Editor and Windows Standalone builds without assuming Editor-only paths.
    /// </summary>
    public static class WhisperModelLocator
    {
        public const string DefaultModelName = "ggml-tiny.bin";

        public static bool TryResolveModelPath(string modelName, out string resolvedPath, string explicitPath = null)
        {
            resolvedPath = null;
            string targetName = string.IsNullOrWhiteSpace(modelName) ? DefaultModelName : modelName.Trim();

            // 1. Explicit configured path
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string explicitFullPath = Path.GetFullPath(explicitPath);
                if (File.Exists(explicitFullPath))
                {
                    resolvedPath = explicitFullPath;
                    return true;
                }
            }

            // 2. Packaged model location (StreamingAssets)
            try
            {
                string streamingPath = Path.Combine(Application.streamingAssetsPath, "Whisper", targetName);
                if (File.Exists(streamingPath))
                {
                    resolvedPath = streamingPath;
                    return true;
                }

                // If targetName contains a directory or full path
                if (File.Exists(Path.Combine(Application.streamingAssetsPath, targetName)))
                {
                    resolvedPath = Path.Combine(Application.streamingAssetsPath, targetName);
                    return true;
                }
            }
            catch
            {
                // StreamingAssets path evaluation guard
            }

            // 3. Persistent application model cache
            try
            {
                string persistentPath = Path.Combine(Application.persistentDataPath, "Whisper", targetName);
                if (File.Exists(persistentPath))
                {
                    resolvedPath = persistentPath;
                    return true;
                }
            }
            catch
            {
                // Persistent path evaluation guard
            }

            // 4. Development fallback under project root / Assets
            try
            {
                string devPath = Path.Combine(Application.dataPath, "StreamingAssets", "Whisper", targetName);
                if (File.Exists(devPath))
                {
                    resolvedPath = devPath;
                    return true;
                }
            }
            catch
            {
                // Dev path guard
            }

            return false;
        }
    }
}
