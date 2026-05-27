using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenEmpires
{
    // Loads a project-root .env file (KEY=VALUE per line, # comments). Lazy-init.
    // Falls back to process env vars. The .env is editor/dev-only — shipping builds
    // place dataPath inside the bundle and won't have a sibling .env file, so the
    // env-var fallback is the safety net.
    public static class DotEnvLoader
    {
        private static Dictionary<string, string> values;
        private static bool initialized;

        public static string Get(string key)
        {
            EnsureLoaded();
            if (values != null && values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            try
            {
                var envVar = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(envVar)) return envVar;
            }
            catch { }
            return null;
        }

        private static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;

            string root;
            try { root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
            catch { return; }
            string path = Path.Combine(root, ".env");
            if (!File.Exists(path)) return;

            values = new Dictionary<string, string>();
            try
            {
                foreach (var rawLine in File.ReadAllLines(path))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (v.Length >= 2 && ((v[0] == '"' && v[v.Length - 1] == '"') ||
                                          (v[0] == '\'' && v[v.Length - 1] == '\'')))
                        v = v.Substring(1, v.Length - 2);
                    values[k] = v;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DotEnvLoader] Failed to read .env: {e.Message}");
            }
        }
    }
}
