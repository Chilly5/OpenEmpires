using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Local, opt-in evidence only. No game authority, payloads, headers, context, entity IDs or coordinates.
    internal sealed class CommanderTurnObservationStore
    {
        internal const int MaximumTurns = 64;
        internal const int MaximumExportBytes = 131072;
        private static readonly HashSet<string> TimingKeys = new HashSet<string>(StringComparer.Ordinal)
        { "uiSubmissionMs", "contextMs", "packingMs", "transportMs", "providerMs", "parseMs", "admissionMs", "firstNativeActionMs" };
        private static readonly HashSet<string> SectionKeys = new HashSet<string>(StringComparer.Ordinal)
        { "contract", "context", "memory", "questionFacts", "pending", "utterance", "messageMargin", "repairTemplate" };
        private static readonly HashSet<string> StageKeys = new HashSet<string>(StringComparer.Ordinal)
        { "submitted", "context", "provider", "parsed", "admission", "awaiting-approval", "admitted", "rejected", "timed-out", "cancelled", "late-attempt" };
        private static readonly HashSet<string> OutcomeKeys = new HashSet<string>(StringComparer.Ordinal)
        { "Unknown", "Request", "DynamicPlan", "Clarify", "Unsupported", "Answer" };
        private static readonly HashSet<string> StatusKeys = new HashSet<string>(StringComparer.Ordinal)
        { "Unknown", "NotApplicable", "Accepted", "Waiting", "Working", "Blocked", "Completed", "Failed", "Cancelled", "AwaitingApproval", "Rejected" };
        private readonly object gate = new object();
        private readonly string path;
        private readonly string session = Guid.NewGuid().ToString("N");
        private readonly List<TurnRecord> turns = new List<TurnRecord>();
        private bool recording;
        private Action persisted;

        internal CommanderTurnObservationStore(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > MaximumExportBytes) return;
                JObject saved = JObject.Parse(File.ReadAllText(path));
                if ((string)saved["schema"] != "commander-local-observation-v1"
                    || !(saved["turns"] is JArray rows)) return;
                foreach (JObject row in rows.OfType<JObject>().Take(MaximumTurns))
                {
                    TurnRecord item = TurnRecord.Read(row);
                    if (item != null) turns.Add(item);
                }
            }
            catch (Exception) { turns.Clear(); } // Corrupt local evidence never enters a provider request.
        }

        internal bool Recording { get { lock (gate) return recording; } }
        internal void SetRecording(bool enabled) { lock (gate) recording = enabled; }
        internal void SetPersistedCallback(Action callback) { lock (gate) persisted = callback; }

        internal CommanderTurnObservation Begin(long requestId, long turn, int revision, string route)
            => Begin(requestId, turn, revision, 0, route);

        internal CommanderTurnObservation Begin(long requestId, long turn, int revision, int generation, string route)
        {
            if (requestId <= 0 || turn <= 0 || revision < 0 || generation < 0) return null;
            lock (gate)
            {
                if (!recording) return null;
                var record = new TurnRecord { Session = session, RequestId = requestId, Turn = turn,
                    Revision = revision, Generation = generation,
                    Route = route == "WebGateway" ? "WebGateway" : "WindowsDirect",
                    Stage = "submitted", Outcome = "Unknown", TaskStatus = "Unknown" };
                turns.Add(record);
                while (turns.Count > MaximumTurns) turns.RemoveAt(0);
                SaveLocked();
                return new CommanderTurnObservation(this, record);
            }
        }

        internal void ObserveTaskCard(long requestId, int generation, CommanderTaskStatus status)
        {
            lock (gate)
            {
                TurnRecord record = turns.LastOrDefault(item => item.Session == session
                    && item.RequestId == requestId && item.Generation == generation);
                if (record == null) return;
                string safe = status.ToString();
                if (!StatusKeys.Contains(safe) || record.TaskStatus == safe) return;
                record.TaskStatus = safe;
                SaveLocked();
            }
        }

        internal string ExportJson()
        {
            lock (gate) return ExportLocked();
        }

        private string ExportLocked() => new JObject { ["schema"] = "commander-local-observation-v1",
            ["turns"] = new JArray(turns.Select(item => item.Json())) }.ToString(Formatting.None);

        private void SaveLocked()
        {
            string json = ExportLocked();
            while (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumExportBytes && turns.Count > 0)
            { turns.RemoveAt(0); json = ExportLocked(); }
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, json);
                if (File.Exists(path))
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // IDBFS persists the completed virtual-FS replacement in the queued sync.
                    File.Delete(path);
                    File.Move(temporary, path);
#else
                    // Replace preserves the old complete file if the write fails.
                    File.Replace(temporary, path, null);
#endif
                }
                else File.Move(temporary, path);
                try { persisted?.Invoke(); } catch (Exception) { }
            }
            catch (Exception)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception) { }
                // Observation must never affect admission or gameplay.
            }
        }

        internal sealed class CommanderTurnObservation
        {
            private readonly CommanderTurnObservationStore owner;
            private readonly TurnRecord record;
            internal CommanderTurnObservation(CommanderTurnObservationStore owner, TurnRecord record)
            { this.owner = owner; this.record = record; }

            internal void ObserveAttempt(int index, CommanderProviderAttempt attempt)
            {
                if (index < 0 || index > 1 || attempt == null) return;
                lock (owner.gate)
                {
                    if (!owner.turns.Contains(record)) return; // Never reassign a late attempt to another turn.
                    while (record.Attempts.Count <= index) record.Attempts.Add(null);
                    record.Attempts[index] = AttemptRecord.From(attempt);
                    owner.SaveLocked();
                }
            }

            internal void Mark(string stage, string outcome, string taskStatus)
            {
                lock (owner.gate)
                {
                    if (!owner.turns.Contains(record)) return;
                    if (StageKeys.Contains(stage)) record.Stage = stage;
                    if (OutcomeKeys.Contains(outcome)) record.Outcome = outcome;
                    if (StatusKeys.Contains(taskStatus)) record.TaskStatus = taskStatus;
                    owner.SaveLocked();
                }
            }

            internal void Timing(string key, long milliseconds)
            {
                if (!TimingKeys.Contains(key) || milliseconds < 0) return;
                lock (owner.gate)
                {
                    if (!owner.turns.Contains(record)) return;
                    if (!record.Timings.ContainsKey(key)) record.Timings[key] = milliseconds;
                    owner.SaveLocked();
                }
            }

            internal void AddTiming(string key, long milliseconds)
            {
                if ((key != "transportMs" && key != "parseMs") || milliseconds < 0) return;
                lock (owner.gate)
                {
                    if (!owner.turns.Contains(record)) return;
                    record.Timings.TryGetValue(key, out long previous);
                    record.Timings[key] = previous > long.MaxValue - milliseconds
                        ? long.MaxValue : previous + milliseconds;
                    owner.SaveLocked();
                }
            }
        }

        internal sealed class TurnRecord
        {
            internal string Session;
            internal long RequestId, Turn;
            internal int Revision, Generation;
            internal string Route, Stage, Outcome, TaskStatus;
            internal readonly Dictionary<string, long> Timings = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly List<AttemptRecord> Attempts = new List<AttemptRecord>();

            internal JObject Json()
            {
                var timings = new JObject();
                foreach (string key in TimingKeys.OrderBy(x => x, StringComparer.Ordinal))
                    timings[key] = Timings.TryGetValue(key, out long value) ? new JValue(value) : JValue.CreateNull();
                return new JObject { ["session"] = Session, ["requestId"] = RequestId, ["turn"] = Turn,
                    ["revision"] = Revision, ["generation"] = Generation, ["route"] = Route,
                    ["stage"] = Stage, ["outcome"] = Outcome, ["taskStatus"] = TaskStatus,
                    ["timings"] = timings,
                    ["attempts"] = new JArray(Attempts.Where(x => x != null).Select(x => x.Json())) };
            }

            internal static TurnRecord Read(JObject row)
            {
                string session = (string)row["session"];
                if (session == null || session.Length != 32 || !Guid.TryParseExact(session, "N", out _)) return null;
                long? requestId = (long?)row["requestId"], turn = (long?)row["turn"];
                int? revision = (int?)row["revision"], generation = (int?)row["generation"];
                if (requestId <= 0 || turn <= 0 || revision < 0 || generation < 0) return null;
                var item = new TurnRecord { Session = session, RequestId = requestId.Value, Turn = turn.Value,
                    Revision = revision.Value, Generation = generation.Value,
                    Route = (string)row["route"] == "WebGateway" ? "WebGateway" : "WindowsDirect",
                    Stage = StageKeys.Contains((string)row["stage"]) ? (string)row["stage"] : "submitted",
                    Outcome = OutcomeKeys.Contains((string)row["outcome"]) ? (string)row["outcome"] : "Unknown",
                    TaskStatus = StatusKeys.Contains((string)row["taskStatus"]) ? (string)row["taskStatus"] : "Unknown" };
                if (row["timings"] is JObject timing)
                    foreach (string key in TimingKeys)
                        if ((long?)timing[key] is long value && value >= 0) item.Timings[key] = value;
                if (row["attempts"] is JArray attempts)
                    foreach (JObject attempt in attempts.OfType<JObject>().Take(2))
                        item.Attempts.Add(AttemptRecord.Read(attempt));
                return item;
            }
        }

        internal sealed class AttemptRecord
        {
            internal string Kind, Stage;
            internal int Estimate;
            internal long Elapsed;
            internal int? HttpStatus;
            internal readonly Dictionary<string, int> Sections = new Dictionary<string, int>(StringComparer.Ordinal);
            internal long? Prompt, Completion, Cached, Reasoning, Total;
            internal decimal? Cost;

            internal static AttemptRecord From(CommanderProviderAttempt attempt)
            {
                var item = new AttemptRecord { Kind = attempt.Kind == "repair" ? "repair" : "initial",
                    Stage = SafeStage(attempt.ResultStage), Estimate = Math.Max(0, attempt.InputEstimate),
                    Elapsed = Math.Max(0, attempt.ElapsedMilliseconds), HttpStatus = attempt.HttpStatusCode,
                    Prompt = attempt.Usage.PromptTokens, Completion = attempt.Usage.CompletionTokens,
                    Cached = attempt.Usage.CachedTokens, Reasoning = attempt.Usage.ReasoningTokens,
                    Total = attempt.Usage.TotalTokens, Cost = attempt.Usage.Cost };
                foreach (var section in attempt.SectionEstimates)
                    if (SectionKeys.Contains(section.Key) && section.Value >= 0) item.Sections[section.Key] = section.Value;
                return item;
            }

            internal JObject Json()
            {
                var sections = new JObject();
                foreach (var section in Sections.OrderBy(x => x.Key, StringComparer.Ordinal))
                    sections[section.Key] = section.Value;
                return new JObject { ["kind"] = Kind, ["estimate"] = Estimate,
                    ["sections"] = sections, ["elapsedMs"] = Elapsed, ["httpStatus"] = HttpStatus,
                    ["stage"] = Stage,
                    ["usage"] = new JObject { ["promptTokens"] = Prompt, ["completionTokens"] = Completion,
                        ["cachedTokens"] = Cached, ["reasoningTokens"] = Reasoning,
                        ["totalTokens"] = Total, ["cost"] = Cost } };
            }

            internal static AttemptRecord Read(JObject row)
            {
                var item = new AttemptRecord { Kind = (string)row["kind"] == "repair" ? "repair" : "initial",
                    Stage = SafeStage((string)row["stage"]), Estimate = Math.Max(0, (int?)row["estimate"] ?? 0),
                    Elapsed = Math.Max(0, (long?)row["elapsedMs"] ?? 0),
                    HttpStatus = (int?)row["httpStatus"] };
                if (row["sections"] is JObject sections)
                    foreach (string key in SectionKeys)
                        if ((int?)sections[key] is int value && value >= 0) item.Sections[key] = value;
                if (row["usage"] is JObject usage)
                {
                    item.Prompt = Nonnegative((long?)usage["promptTokens"]);
                    item.Completion = Nonnegative((long?)usage["completionTokens"]);
                    item.Cached = Nonnegative((long?)usage["cachedTokens"]);
                    item.Reasoning = Nonnegative((long?)usage["reasoningTokens"]);
                    item.Total = Nonnegative((long?)usage["totalTokens"]);
                    decimal? cost = (decimal?)usage["cost"];
                    item.Cost = cost >= 0 ? cost : null;
                }
                return item;
            }

            private static long? Nonnegative(long? value) => value >= 0 ? value : null;
            private static string SafeStage(string value)
                => !string.IsNullOrEmpty(value) && value.Length <= 40
                    && value.All(c => c >= 'a' && c <= 'z' || c == '-') ? value : "unknown";
        }
    }
}
