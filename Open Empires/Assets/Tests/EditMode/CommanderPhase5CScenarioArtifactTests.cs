using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CScenarioArtifactTests
    {
        [Test]
        public void PerTurnArtifact_IsAtomicBoundedAndRejectsUnsafeKeys()
        {
            Type writer = typeof(OpenEmpires.TestSupport.ScenarioEvidenceRecorder);
            MethodInfo write = writer.GetMethod("RecordPhase5CTurn", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(write, Is.Not.Null, "Task 6 extends the existing atomic recorder with a safe turn record.");
            string directory = Path.Combine(Path.GetTempPath(), "phase5c-artifact-" + Guid.NewGuid().ToString("N"));
            try
            {
                var safe = new JObject
                {
                    ["schemaVersion"] = 1, ["caseId"] = "mine-yes-original-request",
                    ["requestId"] = "request-1", ["utterance"] = "Build a gold mine near gold.",
                    ["actualPromptTokens"] = JValue.CreateNull(), ["status"] = "started"
                };
                string path = Path.Combine(directory, "mine-yes-original-request-turn-0.json");
                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
                write.Invoke(recorder, new object[] { safe });
                Assert.That(File.Exists(path), Is.True);
                Assert.That(JObject.Parse(File.ReadAllText(path))["actualPromptTokens"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
                var unsafeRecord = (JObject)safe.DeepClone();
                unsafeRecord["entityId"] = 42;
                Assert.That(() => write.Invoke(recorder, new object[] { unsafeRecord }),
                    Throws.TypeOf<TargetInvocationException>());
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PerTurnArtifact_AllowsDistinctObservationTurnWithNoExecutionTicket()
        {
            string directory = Path.Combine(Path.GetTempPath(), "phase5c-observation-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(directory, "causal-information-owned-task-facts.json");
                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
                recorder.RecordPhase5CTurn(new JObject
                {
                    ["schemaVersion"] = 1, ["caseId"] = "causal-information-owned-task-facts",
                    ["requestId"] = JValue.CreateNull(), ["turnCorrelationId"] = "ui-turn:1",
                    ["utterance"] = "Why is my troop production stuck?", ["status"] = "observed"
                });
                var saved = JObject.Parse(File.ReadAllText(path));
                Assert.That(saved["requestId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That((string)saved["turnCorrelationId"], Is.EqualTo("ui-turn:1"));
                Assert.That((string)saved["caseId"], Is.EqualTo("causal-information-owned-task-facts"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
