using System;
using System.Collections.Generic;
using System.IO;
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
        private const string ArmKey = "OpenEmpires.Commander.CaptureNextTestRun";
        private static readonly CommanderFinalFixResultCapture Instance = new CommanderFinalFixResultCapture();
        private readonly List<JObject> leaves = new List<JObject>();
        private string file;
        private string started;

        static CommanderFinalFixResultCapture()
        {
            TestRunnerApi.RegisterTestCallback(Instance);
        }

        [MenuItem("Tools/Commander/Arm next test result capture")]
        private static void ArmNextRun() => EditorPrefs.SetBool(ArmKey, true);

        public void RunStarted(ITestAdaptor testsToRun)
        {
            file = null;
            if (!EditorPrefs.GetBool(ArmKey, false)) return;
            leaves.Clear();
            started = DateTime.UtcNow.ToString("O");
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence");
            Directory.CreateDirectory(directory);
            file = Path.Combine(directory, "final-fix-leaves-"
                + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
            Save(false, null);
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
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
            if (file == null) return;
            if (result != null && file != null)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, Path.Combine("Docs", "CommanderPhase5C",
                        "evidence", Path.GetFileNameWithoutExtension(file) + ".xml"));
                }
                catch (Exception) { } // JSON leaves still retain the exact result states.
            }
            Save(true, result);
            EditorPrefs.SetBool(ArmKey, false);
            file = null;
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
