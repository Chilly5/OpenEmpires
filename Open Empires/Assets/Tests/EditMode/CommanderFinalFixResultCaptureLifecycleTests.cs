using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5COffline")]
    public sealed class CommanderFinalFixResultCaptureLifecycleTests
    {
        [Test]
        public void Capture_IsolatedOwnedRunResumesAndUnarmedDifferentRunLeavesItIncomplete()
        {
            Type type = Type.GetType(
                "OpenEmpires.Editor.CommanderFinalFixResultCapture, OpenEmpires.Editor", true);
            var staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
            var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            string productionRoot = (string)type.GetField("KeyRoot", staticFlags).GetValue(null);
            string productionArm = productionRoot + "Arm";
            string productionOwner = productionRoot + "Owner";
            string productionFile = productionRoot + "File";
            bool armBefore = SessionState.GetBool(productionArm, false);
            string ownerBefore = SessionState.GetString(productionOwner, "");
            string pathBefore = SessionState.GetString(productionFile, "");
            object registered = type.GetField("Instance", staticFlags).GetValue(null);
            string activeFileBefore = (string)type.GetField("file", instanceFlags).GetValue(registered);

            string directory = Path.Combine(Path.GetTempPath(), "commander-capture-isolated-"
                + Guid.NewGuid().ToString("N"));
            string isolatedRoot = "OpenEmpires.Commander.CaptureIsolated."
                + Guid.NewGuid().ToString("N") + ".";
            string runGuid = "owned-focused-run";
            Func<string> identity = () => runGuid;
            var constructor = type.GetConstructor(instanceFlags, null,
                new[] { typeof(Func<string>), typeof(string), typeof(string) }, null);
            Assert.That(constructor, Is.Not.Null, "Capture needs an isolated Editor-only ownership scope.");
            Directory.CreateDirectory(directory);
            try
            {
                ICallbacks Create() => (ICallbacks)constructor.Invoke(
                    new object[] { identity, isolatedRoot, directory });
                string ActiveFile(ICallbacks callback) => (string)type.GetField(
                    "file", instanceFlags).GetValue(callback);

                ICallbacks first = Create();
                type.GetMethod("Arm", instanceFlags).Invoke(first, null);
                first.RunStarted(null);
                string ownedFile = ActiveFile(first);
                Assert.That(ownedFile, Is.Not.Null);
                Assert.That(File.Exists(ownedFile), Is.True);
                Assert.That(SessionState.GetBool(isolatedRoot + "Arm", false), Is.False,
                    "One-run arm is consumed at the first RunStarted.");
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));

                ICallbacks recreated = Create();
                recreated.RunStarted(null);
                Assert.That(ActiveFile(recreated), Is.EqualTo(ownedFile),
                    "The same framework run owns the original artifact after callback recreation.");
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));

                runGuid = "different-unarmed-run";
                ICallbacks next = Create();
                next.RunStarted(null);
                next.RunFinished(null);
                Assert.That(ActiveFile(next), Is.Null);
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1),
                    "A different unarmed run cannot create or steal an artifact.");
                Assert.That((bool)JObject.Parse(File.ReadAllText(ownedFile))["finished"], Is.False,
                    "The interrupted original run must remain explicitly incomplete.");
                Assert.That(SessionState.GetString(isolatedRoot + "Owner", ""), Is.Empty);
                Assert.That(SessionState.GetString(isolatedRoot + "File", ""), Is.Empty);

                Assert.That(SessionState.GetBool(productionArm, false), Is.EqualTo(armBefore));
                Assert.That(SessionState.GetString(productionOwner, ""), Is.EqualTo(ownerBefore));
                Assert.That(SessionState.GetString(productionFile, ""), Is.EqualTo(pathBefore));
                Assert.That((string)type.GetField("file", instanceFlags).GetValue(registered),
                    Is.EqualTo(activeFileBefore), "The real registered callback remains untouched.");
            }
            finally
            {
                SessionState.SetBool(isolatedRoot + "Arm", false);
                SessionState.EraseString(isolatedRoot + "Owner");
                SessionState.EraseString(isolatedRoot + "File");
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
