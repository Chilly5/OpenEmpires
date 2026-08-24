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

            string path = FindEnvFile();
            if (path == null)
            {
                Debug.Log("[DotEnv] No .env found; LLM teammate features disabled unless GEMINI_API_KEY is set in env vars.");
                return;
            }

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
                Debug.Log($"[DotEnv] Loaded .env from {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DotEnvLoader] Failed to read .env at {path}: {e.Message}");
            }
        }

        // Walks several candidate roots in priority order. The Unity-Editor-default
        // (Application.dataPath/..) is only the Unity project folder, which can be one
        // (or more) levels INSIDE the git repo when the Unity project lives in a subdir.
        private static string FindEnvFile()
        {
            string dataPath;
            try { dataPath = Application.dataPath; }
            catch { return null; }
            if (string.IsNullOrEmpty(dataPath)) return null;

            // Walk Unity-relative roots first, then the build executable directory.
            string[] candidates =
            {
                SafeCombine(dataPath, "..", ".env"),
                SafeCombine(dataPath, "..", "..", ".env"),
                SafeCombine(dataPath, "..", "..", "..", ".env"),
                SafeCombine(dataPath, ".env"),
                SafeCombine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && File.Exists(candidates[i])) return candidates[i];
            }
            return null;
        }

        private static string SafeCombine(params string[] parts)
        {
            try { return Path.GetFullPath(Path.Combine(parts)); }
            catch { return null; }
        }
    }
}
