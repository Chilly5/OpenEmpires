using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // Opt-in Editor coroutine, paid, parse-only diagnosis. No UI, admission, or gameplay orders.
    [Category("CommanderLiveProvider")]
    public sealed class CommanderPhase5CLiveFoodDiagnosticTests
    {
        internal const string OptIn = "OpenEmpires.Phase5C.FoodDiagnostic.AllowOneRun";
        private const string RunId = "ab892215-c3f1-4b05-9c4d-9f617bb52a14";
        private const string Model = "openai/gpt-6-luna";
        private sealed class Study
        {
            public string schema = "commander-food-shape-diagnostic@1";
            public string model = Model;
            public string stage = "starting";
            public Case[] cases;
        }
        private sealed class Case
        {
            public string utterance, stage, outcome, schemaDiagnostic, safeTrace;
            public bool valid;
            public int nodeCount, initialHttp, repairHttp, httpStatus;
            public string[] nodeTypes, workerModes, workerCounts, workerDestinations;
            public string[] missingFields;
            public Attempt[] attempts;
            public List<Shape> envelopes = new List<Shape>();
        }
        private sealed class Attempt
        {
            public string kind, stage, promptTokens, completionTokens, cachedTokens, cost;
            public long elapsedMs;
            public int inputUtf8Estimate, httpStatus;
        }
        private sealed class Shape
        {
            public int responseBytes, contentCharacters, contentUtf8Bytes, nodeCount, unknownRootKeyCount;
            public string envelope, contentKind, jsonError, errorPath, outcome, nodesType;
            public bool hasOutcome, hasNodes, hasExecutionOrder, hasMessage, hasPending;
        }

        // Observes shape in memory, writes allow-listed metadata, and returns the exact
        // original response. It never logs/stores the provider body or modifies parsing.
        private sealed class ShapeProbeTransport : ICommanderHttpTransport
        {
            private readonly ICommanderHttpTransport inner;
            private readonly Action<Shape> observed;
            internal bool CaptureFailed { get; private set; }
            internal ShapeProbeTransport(ICommanderHttpTransport inner, Action<Shape> observed)
            { this.inner = inner; this.observed = observed; }
            public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                var response = await inner.PostJsonAsync(uri, json, headers, token).ConfigureAwait(false);
                try { observed(Inspect(response)); }
                catch (Exception) { CaptureFailed = true; } // Observation must never alter the provider response.
                return response;
            }
            private static Shape Inspect(CommanderHttpResponse response)
            {
                var shape = new Shape { envelope = "unreadable", contentKind = "none",
                    nodesType = "missing", responseBytes = Encoding.UTF8.GetByteCount(response?.Body ?? "") };
                if (response == null) { shape.envelope = "null-response"; return shape; }
                if (shape.responseBytes > 65536) { shape.envelope = "oversize"; return shape; }
                try
                {
                    var outer = JObject.Parse(response.Body);
                    shape.envelope = "json";
                    var content = outer["choices"]?[0]?["message"]?["content"];
                    string text = null;
                    if (content?.Type == JTokenType.String)
                    { shape.contentKind = "string"; text = (string)content; }
                    else if (content is JArray parts && parts.All(p => p is JObject
                        && (string)p["type"] == "text" && p["text"]?.Type == JTokenType.String))
                    { shape.contentKind = "text-parts"; text = string.Concat(parts.Select(p => (string)p["text"])); }
                    else shape.contentKind = content == null ? "missing" : "other";
                    if (text == null) return shape;
                    shape.contentCharacters = text.Length;
                    shape.contentUtf8Bytes = Encoding.UTF8.GetByteCount(text);
                    string candidate = text.Trim();
                    if (candidate.StartsWith("```", StringComparison.Ordinal)
                        && candidate.EndsWith("```", StringComparison.Ordinal))
                    {
                        int line = candidate.IndexOf('\n');
                        if (line >= 0 && (candidate.Substring(0, line).TrimEnd('\r') == "```json"
                            || candidate.Substring(0, line).TrimEnd('\r') == "```"))
                            candidate = candidate.Substring(line + 1, candidate.Length - line - 4).Trim();
                    }
                    JObject root;
                    using (var reader = new JsonTextReader(new StringReader(candidate))
                        { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                    {
                        root = JObject.Load(reader, new JsonLoadSettings
                            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                                CommentHandling = CommentHandling.Load });
                        if (reader.Read()) throw new JsonException();
                    }
                    shape.hasOutcome = root.Property("outcome") != null;
                    shape.hasNodes = root.Property("nodes") != null;
                    shape.hasExecutionOrder = root.Property("executionOrder") != null;
                    shape.hasMessage = root.Property("message") != null;
                    shape.hasPending = root.Property("pending") != null;
                    shape.unknownRootKeyCount = root.Properties().Count(p => p.Name != "outcome"
                        && p.Name != "nodes" && p.Name != "executionOrder" && p.Name != "message"
                        && p.Name != "pending" && p.Name != "missingFields" && p.Name != "missingSlots"
                        && p.Name != "version");
                    string outcome = root["outcome"]?.Type == JTokenType.String ? (string)root["outcome"] : null;
                    shape.outcome = new[] { "Request", "DynamicPlan", "Clarify", "Unsupported", "Answer" }
                        .Contains(outcome) ? outcome : outcome == null ? "missing-or-nonstring" : "unrecognized";
                    shape.nodesType = root["nodes"]?.Type.ToString() ?? "missing";
                    shape.nodeCount = root["nodes"] is JArray nodes ? nodes.Count : 0;
                }
                catch (JsonException error)
                {
                    shape.jsonError = error is JsonReaderException ? "JsonReaderException" :
                        error is JsonSerializationException ? "JsonSerializationException" : "JsonException";
                    string path = (error as JsonReaderException)?.Path;
                    // No arbitrary provider key/value or path is exported.
                    string leaf = path?.Split('.').LastOrDefault()?.Split('[')[0];
                    shape.errorPath = new[] { "outcome", "nodes", "executionOrder", "message", "pending",
                        "type", "count", "mode", "countMode", "workers", "destination",
                        "resource", "sourceKind" }.Contains(leaf) ? leaf : "other-or-none";
                }
                return shape;
            }
        }

        [UnityTest]
        public IEnumerator FixedFoodFiveEightAndWoodSix_ProbeProductionEnvelopeWithoutOrders()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(OptIn, false))
                Assert.Ignore("Paid production shape diagnostic requires explicit one-run opt-in.");
            UnityEditor.SessionState.SetBool(OptIn, false);
#else
            Assert.Ignore("Paid production shape diagnostic is Editor-only.");
#endif
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence", "live-food-shape-3");
            string resultPath = Path.Combine(directory, "safe-result.json");
            Assert.That(File.Exists(resultPath), Is.False, "Preserve the first diagnostic and its paid attempts.");
            Directory.CreateDirectory(directory);
            var study = new Study { cases = new[] {
                new Case { utterance = "gather food with 5 villagers", stage = "pending" },
                new Case { utterance = "gather food with 8 villager", stage = "pending" },
                new Case { utterance = "gather wood with 6 villagers", stage = "pending" } } };
            void Save()
            {
                string temp = resultPath + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(study, Formatting.Indented));
                if (File.Exists(resultPath)) File.Replace(temp, resultPath, null);
                else File.Move(temp, resultPath);
            }
            Save();
            var journal = new EvidenceBudgetJournal(Path.Combine(directory, "paid-usage-ledger.json"), RunId, true);
            Case current = null;
            var probe = new ShapeProbeTransport(new CommanderHttpClientTransport(), shape =>
            { current?.envelopes.Add(shape); Save(); });
            var budget = new BudgetedSemanticTransport(probe, journal);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                for (int i = 0; i < 9; i++)
                {
                    var unit = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x + 6 + i, z + 7),
                        Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
                    unit.UnitType = 0; unit.IsVillager = true;
                    unit.MaxHealth = unit.CurrentHealth = 100; unit.State = UnitState.Idle;
                }
                manager = new CommanderGoalManager(simulation, 0);
                study.stage = "fixture-ready"; Save();
                foreach (Case item in study.cases)
                {
                    current = item;
                    item.stage = "submission"; Save();
                    var provider = new OpenRouterCommanderProvider(transport: budget,
                        providerTimeout: TimeSpan.FromSeconds(15));
                    int attemptsBefore = budget.InitialHttpAttempts + budget.RepairHttpAttempts;
                    using (var cancellation = new CancellationTokenSource())
                    using (budget.BeginSubmission())
                    {
                        var request = new CommanderSemanticProviderRequest(item.utterance,
                            new CommanderContextBuilder().Build(simulation, manager, item.utterance));
                        Task<CommanderSemanticResult> task = provider.TranslateSemanticAsync(request, cancellation.Token);
                        float start = Time.realtimeSinceStartup;
                        while (!task.IsCompleted && Time.realtimeSinceStartup - start < 20f) yield return null;
                        if (!task.IsCompleted) cancellation.Cancel();
                        if (!task.IsCompleted)
                        {
                            float drain = Time.realtimeSinceStartup;
                            while (!task.IsCompleted && Time.realtimeSinceStartup - drain < 2f) yield return null;
                        }
                        item.stage = !task.IsCompleted ? "owned-await-overrun" : task.IsCanceled ? "cancelled"
                            : task.IsFaulted ? "faulted" : "translated";
                        item.initialHttp = budget.InitialHttpAttempts;
                        item.repairHttp = budget.RepairHttpAttempts;
                        item.httpStatus = budget.LastHttpStatus;
                        item.safeTrace = provider.LastRequestTrace;
                        item.attempts = provider.LastSemanticAttempts.Select(a => new Attempt {
                            kind = a.Kind, stage = a.ResultStage, elapsedMs = a.ElapsedMilliseconds,
                            inputUtf8Estimate = a.InputEstimate, httpStatus = a.HttpStatusCode ?? 0,
                            promptTokens = a.Usage.PromptTokens?.ToString() ?? "unknown",
                            completionTokens = a.Usage.CompletionTokens?.ToString() ?? "unknown",
                            cachedTokens = a.Usage.CachedTokens?.ToString() ?? "unknown",
                            cost = a.Usage.Cost?.ToString() ?? "unknown" }).ToArray();
                        if (task.Status == TaskStatus.RanToCompletion)
                        {
                            var result = task.Result;
                            item.valid = result.IsValid; item.outcome = result.Outcome.ToString();
                            item.schemaDiagnostic = result.SchemaDiagnostic;
                            item.missingFields = result.PendingDraft?.MissingFields.Select(f => f.ToString()).ToArray();
                            item.nodeCount = result.Nodes.Count;
                            if (result.IsValid)
                            {
                                item.nodeTypes = result.Nodes.Select(n => n.Type.ToString()).ToArray();
                                item.workerModes = result.Nodes.Select(n => n.WorkerAllocation?.Mode.ToString() ?? "").ToArray();
                                item.workerCounts = result.Nodes.Select(n => n.WorkerAllocation?.Count?.ToString() ?? "").ToArray();
                                item.workerDestinations = result.Nodes.Select(n => n.WorkerAllocation?.Destination?.Resource.ToString() ?? "").ToArray();
                            }
                        }
                        Save(); // Persist every first result before any score assertion.
                        if (budget.InitialHttpAttempts + budget.RepairHttpAttempts == attemptsBefore
                            || item.httpStatus < 200 || item.httpStatus > 299 || !task.IsCompleted)
                        { study.stage = "stopped-on-http-or-owned-await"; Save(); break; }
                    }
                }
                Assert.That(manager.Goals, Is.Empty, "Diagnostic never admits a gameplay goal.");
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty, "Diagnostic never issues native orders.");
                Assert.That(probe.CaptureFailed, Is.False, "Envelope-shape observation failed; response was unchanged.");
                Assert.That(study.cases.All(c => c.stage == "translated" && c.valid
                    && c.outcome == "Request" && c.nodeCount == 1
                    && c.nodeTypes?.SingleOrDefault() == "AllocateWorkers"
                    && c.workerModes?.SingleOrDefault() == "SelectedCount"
                    && c.workerCounts?.SingleOrDefault() == (c.utterance.Contains(" 5 ") ? "5" : c.utterance.Contains(" 8 ") ? "8" : "6")
                    && c.workerDestinations?.SingleOrDefault() == (c.utterance.Contains("wood") ? "Wood" : "Food")),
                    Is.True, "See allow-listed safe-result.json and owned HTTP journal for first failing layer.");
                study.stage = "scored"; Save();
            }
            finally
            {
                try { Save(); } catch (IOException) { /* Preserve the original test failure. */ }
                manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
