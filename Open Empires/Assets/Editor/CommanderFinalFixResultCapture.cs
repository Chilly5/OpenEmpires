using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace OpenEmpires.Editor
{
    // Editor-only, default-inactive result retention for one explicitly armed test run.
    // It never starts tests, gameplay, or a provider request.
    [InitializeOnLoad]
    internal sealed class CommanderFinalFixResultCapture : ICallbacks
    {
        private static readonly string KeyRoot = "OpenEmpires.Commander.CaptureRun." + Application.dataPath + ".";
        private static readonly CommanderFinalFixResultCapture Instance = new CommanderFinalFixResultCapture();
        private readonly Func<string> currentRunGuid;
        private readonly string armKey, ownerKey, fileKey, outputDirectory;
        private readonly List<JObject> leaves = new List<JObject>();
        private string file;
        private string started;
        private string owner;

        static CommanderFinalFixResultCapture()
        {
            TestRunnerApi.RegisterTestCallback(Instance);
        }

        private CommanderFinalFixResultCapture() : this(CurrentRunGuid, KeyRoot,
            Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence")) { }

        internal CommanderFinalFixResultCapture(Func<string> runGuid, string keyRoot, string directory)
        {
            currentRunGuid = runGuid ?? throw new ArgumentNullException(nameof(runGuid));
            if (string.IsNullOrEmpty(keyRoot)) throw new ArgumentException(nameof(keyRoot));
            outputDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
            armKey = keyRoot + "Arm";
            ownerKey = keyRoot + "Owner";
            fileKey = keyRoot + "File";
        }

        [MenuItem("Tools/Commander/Arm next test result capture")]
        private static void ArmNextRun() => Instance.Arm();

        private void Arm() => SessionState.SetBool(armKey, true);

        public void RunStarted(ITestAdaptor testsToRun)
        {
            file = null;
            owner = null;
            string currentRun = currentRunGuid();
            string previousOwner = SessionState.GetString(ownerKey, "");
            string previousFile = SessionState.GetString(fileKey, "");
            if (previousOwner.Length != 0)
            {
                if (currentRun == previousOwner && LoadOwned(previousFile, previousOwner)) return;
                ClearOwned(); // The former file remains explicitly unfinished.
            }
            bool armed = SessionState.GetBool(armKey, false);
            SessionState.SetBool(armKey, false); // Consume before any file or callback work.
            if (!armed || string.IsNullOrEmpty(currentRun)) return;
            leaves.Clear();
            started = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(outputDirectory);
            file = Path.Combine(outputDirectory, "final-fix-leaves-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            owner = currentRun;
            SessionState.SetString(ownerKey, owner);
            SessionState.SetString(fileKey, file);
            Save(false, null);
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!OwnsCurrentRun()) return;
            if (result == null || result.HasChildren || file == null) return;
            leaves.Add(new JObject {
                ["fullName"] = result.FullName,
                ["state"] = result.ResultState,
                ["durationSeconds"] = result.Duration,
                ["message"] = result.Message == null ? null : result.Message.Substring(0,
                    Math.Min(500, result.Message.Length))
            });
            Save(false, null);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (!OwnsCurrentRun()) return;
            if (result != null)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, Path.Combine(outputDirectory,
                        Path.GetFileNameWithoutExtension(file) + ".xml"));
                }
                catch (Exception) { } // JSON leaves still retain the exact result states.
            }
            Save(result != null, result);
            ClearOwned();
            file = null;
            owner = null;
        }

        private bool OwnsCurrentRun()
        {
            string currentRun = currentRunGuid();
            if (string.IsNullOrEmpty(currentRun)) return false;
            if (file == null && !LoadOwned(SessionState.GetString(fileKey, ""),
                    SessionState.GetString(ownerKey, ""))) return false;
            return owner == currentRun;
        }

        private bool LoadOwned(string candidate, string run)
        {
            if (string.IsNullOrEmpty(run) || string.IsNullOrEmpty(candidate)
                || !File.Exists(candidate)) return false;
            try
            {
                JObject saved = JObject.Parse(File.ReadAllText(candidate));
                if ((string)saved["schema"] != "commander-focused-test-leaves-v1"
                    || (bool?)saved["finished"] != false) return false;
                leaves.Clear();
                if (saved["leaves"] is JArray rows)
                    foreach (JObject row in rows) leaves.Add((JObject)row.DeepClone());
                started = (string)saved["startedUtc"];
                file = candidate;
                owner = run;
                return true;
            }
            catch (Exception) { return false; }
        }

        private void ClearOwned()
        {
            SessionState.EraseString(ownerKey);
            SessionState.EraseString(fileKey);
        }

        private static string CurrentRunGuid()
        {
            // The installed Test Framework's persisted TestJobData has a stable guid
            // across its domain-reload resume. Fail closed if its shape changes.
            try
            {
                Type holder = typeof(TestRunnerApi).Assembly.GetType(
                    "UnityEditor.TestTools.TestRunner.TestRun.TestJobDataHolder");
                object instance = holder?.GetProperty("instance", BindingFlags.Static
                    | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                    ?.GetValue(null);
                object runs = holder?.GetField("TestRuns")?.GetValue(instance);
                string found = null;
                if (runs is System.Collections.IEnumerable entries)
                    foreach (object entry in entries)
                    {
                        Type type = entry.GetType();
                        if ((bool)type.GetField("isRunning").GetValue(entry))
                        {
                            string guid = (string)type.GetField("guid").GetValue(entry);
                            if (found != null || string.IsNullOrEmpty(guid)) return null;
                            found = guid;
                        }
                    }
                return found;
            }
            catch (Exception) { return null; }
        }

        private void Save(bool finished, ITestResultAdaptor result)
        {
            if (file == null) return;
            try
            {
                var json = new JObject { ["schema"] = "commander-focused-test-leaves-v1",
                    ["startedUtc"] = started, ["finished"] = finished,
                    ["runState"] = result?.ResultState,
                    ["passCount"] = result?.PassCount, ["failCount"] = result?.FailCount,
                    ["skipCount"] = result?.SkipCount, ["leaves"] = new JArray(leaves) };
                string temp = file + ".tmp";
                File.WriteAllText(temp, json.ToString(Formatting.None));
                if (File.Exists(file)) File.Replace(temp, file, null);
                else File.Move(temp, file);
            }
            catch (Exception) { } // Evidence hook may not affect test semantics.
        }
    }
}
