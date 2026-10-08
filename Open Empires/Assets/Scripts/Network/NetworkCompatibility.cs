using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OpenEmpires
{
    [Serializable]
    public sealed class NetworkCompatibilityProfile
    {
        public int protocol_revision;
        public string source_sha256;
        public string command_encoding;
        public string recovery_policy;
    }

    // Release compatibility, not authentication, anti-cheat or executable integrity.
    // Both platforms built from the same frozen inputs use this same identity.
    public static class NetworkCompatibility
    {
        public const int ProtocolRevision = 2;
        public const string CommandEncoding = "legacy-or-sourcekind-v1";
        public const string RecoveryPolicy = "bounded-full-history-v1";
        public const string AssetPath = "Assets/Resources/Network/LockstepCompatibility.json";
        public static NetworkCompatibilityProfile Current
        {
            get
            {
                var asset = Resources.Load<TextAsset>("Network/LockstepCompatibility");
                var profile = Parse(asset?.text);
#if UNITY_EDITOR
                // Never advertise a stale checked-in/generated identity from Editor.
                try { if (profile != null && profile.source_sha256 != ComputeSourceIdentity(System.IO.Path.GetDirectoryName(Application.dataPath))) return null; }
                catch { return null; }
#endif
                return profile;
            }
        }
        public static NetworkCompatibilityProfile Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 2048) return null;
            try
            {
                var value = JObject.Parse(json, new Newtonsoft.Json.Linq.JsonLoadSettings
                    { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                string[] fields = { "protocol_revision", "source_sha256", "command_encoding", "recovery_policy" };
                if (value.Properties().Count() != fields.Length || value.Properties().Any(p => !fields.Contains(p.Name))) return null;
                if (value["protocol_revision"]?.Type != JTokenType.Integer || fields.Skip(1).Any(f => value[f]?.Type != JTokenType.String)) return null;
                var profile = value.ToObject<NetworkCompatibilityProfile>();
                return Valid(profile) ? profile : null;
            }
            catch { return null; }
        }
        private static bool Valid(NetworkCompatibilityProfile profile) => profile != null
            && profile.protocol_revision == ProtocolRevision && profile.command_encoding == CommandEncoding
            && profile.recovery_policy == RecoveryPolicy && profile.source_sha256?.Length == 64
            && profile.source_sha256.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public static bool Matches(NetworkCompatibilityProfile expected, NetworkCompatibilityProfile received) =>
            Valid(expected) && Valid(received) && expected.source_sha256 == received.source_sha256;

#if UNITY_EDITOR
        // Deterministic bounded source/data inventory. Excludes editor/tests,
        // generated identity itself, models/media/builds and all credential stores.
        public static string ComputeSourceIdentity(string projectRoot)
        {
            string root = System.IO.Path.GetFullPath(projectRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            var inputs = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.Ordinal);
            string[] extensions = { ".cs", ".asmdef", ".asmref", ".asset", ".prefab", ".unity", ".json", ".jslib", ".js", ".mjs", ".wasm", ".dll", ".so", ".a" };
            var directories = new System.Collections.Generic.Stack<string>(); directories.Push(System.IO.Path.Combine(root, "Assets"));
            while (directories.Count > 0)
            {
                string directory = directories.Pop();
                if ((System.IO.File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Linked compatibility input directory is unsupported.");
                foreach (string child in System.IO.Directory.EnumerateDirectories(directory))
                {
                    string name = System.IO.Path.GetFileName(child);
                    if (name == "Editor" || name == "Tests" || name == "StreamingAssets"
                        || name.Equals("Secrets",StringComparison.OrdinalIgnoreCase) || name.Equals("Credentials",StringComparison.OrdinalIgnoreCase)) continue;
                    directories.Push(child);
                }
                foreach (string path in System.IO.Directory.EnumerateFiles(directory))
                {
                    string relative = path.Substring(root.Length).Replace('\\', '/');
                    if (relative == AssetPath || relative == AssetPath + ".meta") continue;
                    string name=System.IO.Path.GetFileNameWithoutExtension(path);
                    if(name.StartsWith(".env",StringComparison.OrdinalIgnoreCase) || name.Equals("credentials",StringComparison.OrdinalIgnoreCase)
                        || name.Equals("secrets",StringComparison.OrdinalIgnoreCase) || relative.EndsWith("/Editor.meta",StringComparison.Ordinal)
                        || relative.EndsWith("/Tests.meta",StringComparison.Ordinal) || relative.EndsWith("/StreamingAssets.meta",StringComparison.Ordinal)) continue;
                    if (relative.IndexOf('\n') >= 0 || relative.IndexOf('\r') >= 0) throw new InvalidOperationException("Invalid compatibility path.");
                    string ext = System.IO.Path.GetExtension(path);
                    if (!extensions.Contains(ext) && ext != ".meta") continue;
                    if ((System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Linked compatibility input file is unsupported.");
                    inputs.Add(relative, path);
                    if (inputs.Count > 20000) throw new InvalidOperationException("Compatibility inventory exceeds its bound.");
                }
            }
            foreach (string relative in new[] { "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt" })
                inputs.Add(relative, System.IO.Path.Combine(root, relative));
            foreach (string path in System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(root, "ProjectSettings"), "*.asset"))
                inputs.Add(path.Substring(root.Length).Replace('\\', '/'), path);
            using var digest = System.Security.Cryptography.SHA256.Create();
            using var canonical = new System.IO.MemoryStream();
            foreach (var item in inputs)
            {
                var info = new System.IO.FileInfo(item.Value);
                if (info.Length > 134217728) throw new InvalidOperationException("Compatibility source/data input exceeds its bound.");
                using var stream = System.IO.File.OpenRead(item.Value);
                string hash = BitConverter.ToString(digest.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                byte[] line = System.Text.Encoding.UTF8.GetBytes(item.Key + "\n" + hash + "\n");
                if (canonical.Length + line.Length > 4194304) throw new InvalidOperationException("Compatibility digest inventory exceeds its bound.");
                canonical.Write(line, 0, line.Length);
            }
            canonical.Position = 0;
            return BitConverter.ToString(digest.ComputeHash(canonical)).Replace("-", "").ToLowerInvariant();
        }
#endif
    }
}
