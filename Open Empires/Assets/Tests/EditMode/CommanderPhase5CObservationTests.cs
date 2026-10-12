using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CObservationTests
    {
        [Test]
        public void LocalObservation_PersistsOnlyBoundedRedactedAttemptAndOutcome()
        {
            // A real production store and serializer, not the scenario-test recorder.
            string directory = Path.Combine(Path.GetTempPath(), "commander-observation-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "observations.json");
            try
            {
                var store = new CommanderTurnObservationStore(path);
                store.SetRecording(true);
                var turn = store.Begin(17L, 23L, 4, "WindowsDirect");
                Assert.That(turn, Is.Not.Null);
                var usage = CommanderProviderUsage.Read(new JObject { ["usage"] = new JObject {
                    ["prompt_tokens"] = 321, ["completion_tokens"] = 24, ["cost"] = .0007m } });
                var attempt = new CommanderProviderAttempt("initial", 900,
                    new System.Collections.Generic.Dictionary<string, int> { ["contract"] = 200 },
                    usage, 37, 200, "semantic-valid");
                turn.ObserveAttempt(0, attempt);
                turn.Mark("admitted", "Request", "Accepted");
                string json = store.ExportJson();
                Assert.That(File.Exists(path), Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo(json));
                var record = (JObject)((JArray)JObject.Parse(json)["turns"])[0];
                Assert.That((long)record["requestId"], Is.EqualTo(17));
                Assert.That((long)record["turn"], Is.EqualTo(23));
                Assert.That((int)record["revision"], Is.EqualTo(4));
                Assert.That((long)record["attempts"]?[0]?["usage"]?["promptTokens"], Is.EqualTo(321));
                Assert.That((decimal)record["attempts"]?[0]?["usage"]?["cost"], Is.EqualTo(.0007m));
                Assert.That((string)record["taskStatus"], Is.EqualTo("Accepted"));
                Assert.That(json, Does.Not.Contain("\"prompt\":")); // no raw prompt field/content
                Assert.That(json.Length, Is.LessThan(131072));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public async Task FailedChargedProviderAttempt_IsDurableAndKeepsOriginalTurnAfterLaterTurn()
        {
            string directory = Path.Combine(Path.GetTempPath(), "commander-attempt-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new CommanderTurnObservationStore(Path.Combine(directory, "observations.json"));
                store.SetRecording(true);
                var first = store.Begin(41, 51, 1, 9, "WindowsDirect");
                first.Mark("timed-out", "Unknown", "Failed");
                store.Begin(42, 52, 2, 9, "WindowsDirect");
                var request = new CommanderSemanticProviderRequest("Build a House.",
                    CommanderEconomyClarificationTests.Context(), null, null, null, null, false, first);
                var provider = new OpenRouterCommanderProvider("fixture-not-a-credential",
                    new ChargedFailureTransport());
                await provider.TranslateSemanticAsync(request, CancellationToken.None);
                JObject persisted = JObject.Parse(File.ReadAllText(Path.Combine(directory, "observations.json")));
                var turns = (JArray)persisted["turns"];
                Assert.That((long)turns[0]["requestId"], Is.EqualTo(41));
                Assert.That((string)turns[0]["stage"], Is.EqualTo("timed-out"));
                Assert.That((long)turns[0]["attempts"]?[0]?["usage"]?["promptTokens"], Is.EqualTo(37));
                Assert.That((decimal)turns[0]["attempts"]?[0]?["usage"]?["cost"], Is.EqualTo(.001m));
                Assert.That((string)turns[0]["attempts"]?[0]?["stage"], Is.EqualTo("provider-error"));
                Assert.That(((JArray)turns[1]["attempts"]).Count, Is.Zero);
                Assert.That(persisted.ToString(), Does.Not.Contain("fixture-not-a-credential"));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void BoundedRing_DropsEvictedHandleAndKeepsUnknownUsageNull()
        {
            string directory = Path.Combine(Path.GetTempPath(), "commander-ring-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new CommanderTurnObservationStore(Path.Combine(directory, "observations.json"));
                store.SetRecording(true);
                var evicted = store.Begin(1, 1, 1, "WindowsDirect");
                CommanderTurnObservationStore.CommanderTurnObservation last = null;
                for (int i = 2; i <= 70; i++) last = store.Begin(i, i, 1, "WindowsDirect");
                var unknown = new CommanderProviderAttempt("initial", 100,
                    new System.Collections.Generic.Dictionary<string, int>(),
                    CommanderProviderUsage.Unknown, 3, null, "timeout");
                evicted.ObserveAttempt(0, unknown);
                last.ObserveAttempt(0, unknown);
                var rows = (JArray)JObject.Parse(store.ExportJson())["turns"];
                Assert.That(rows.Count, Is.EqualTo(CommanderTurnObservationStore.MaximumTurns));
                Assert.That((long)rows[0]["requestId"], Is.EqualTo(7));
                Assert.That((long)rows[rows.Count - 1]["requestId"], Is.EqualTo(70));
                Assert.That(rows[rows.Count - 1]["attempts"]?[0]?["usage"]?["promptTokens"]?.Type,
                    Is.EqualTo(JTokenType.Null));
                Assert.That(rows[rows.Count - 1]["attempts"]?[0]?["usage"]?["cost"]?.Type,
                    Is.EqualTo(JTokenType.Null));
                Assert.That(System.Text.Encoding.UTF8.GetByteCount(store.ExportJson()),
                    Is.LessThanOrEqualTo(CommanderTurnObservationStore.MaximumExportBytes));
            }
            finally { Directory.Delete(directory, true); }
        }

        private sealed class ChargedFailureTransport : ICommanderHttpTransport
        {
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                System.Collections.Generic.IReadOnlyDictionary<string, string> headers, CancellationToken token)
                => Task.FromResult(new CommanderHttpResponse(200,
                    "{\"usage\":{\"prompt_tokens\":37,\"cost\":0.001},\"error\":{\"code\":402}}"));
        }
    }
}
