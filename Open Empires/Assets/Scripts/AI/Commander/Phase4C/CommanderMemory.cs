using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed class CommanderMemory
    {
        public int Capacity { get; }
        public const int DefaultCapacity = 32;
        public const int MaximumCapacity = 128;
        public const int MaximumTextLength = 512;

        private readonly object sync = new object();
        private readonly List<MemoryEntry> entries = new List<MemoryEntry>();
        private long nextSequence = 1;

        public int Count { get { lock (sync) return entries.Count; } }

        public CommanderMemory(int capacity = DefaultCapacity)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public void RecordConversation(CommanderConversationRole role, string text)
        {
            if (role != CommanderConversationRole.Player
                && role != CommanderConversationRole.Commander)
                throw new ArgumentOutOfRangeException(nameof(role));
            Add(role == CommanderConversationRole.Player
                ? MemoryEntryKind.PlayerConversation : MemoryEntryKind.CommanderConversation,
                text);
        }

        public void RecordCavalryPreference() => Add(MemoryEntryKind.Preference,
            "Attack focus: cavalry.", CommanderCavalryPreference.Cavalry);

        public void RecordApprovedStrategy(string objective, int intentId, int planId, string outcome)
        {
            if (string.IsNullOrWhiteSpace(objective))
                throw new ArgumentException("An approved strategy objective is required.", nameof(objective));
            if (intentId < 1) throw new ArgumentOutOfRangeException(nameof(intentId));
            if (planId < 1) throw new ArgumentOutOfRangeException(nameof(planId));
            Add(MemoryEntryKind.ApprovedStrategy, outcome, null, "Accepted",
                intentId, planId, objective);
        }

        public void RecordDecision(string status, string outcome, int? intentId = null,
            int? planId = null, string objective = null)
        {
            if (string.IsNullOrWhiteSpace(status))
                throw new ArgumentException("A decision status is required.", nameof(status));
            if (intentId.HasValue && intentId.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(intentId));
            if (planId.HasValue && planId.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(planId));
            Add(MemoryEntryKind.Decision, outcome, null, status, intentId, planId, objective);
        }

        public void RecordExplanation(string text) => Add(MemoryEntryKind.Explanation, text);

        public IReadOnlyList<MemoryEntry> Snapshot()
        {
            lock (sync)
            {
                var copy = new List<MemoryEntry>(entries.Count);
                for (int i = 0; i < entries.Count; i++) copy.Add(entries[i].Copy());
                return new ReadOnlyCollection<MemoryEntry>(copy);
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                entries.Clear();
                nextSequence = 1;
            }
        }

        public string ToJson() => SerializeSnapshot(Snapshot());

        internal static string SerializeSnapshot(IReadOnlyList<MemoryEntry> values)
        {
            var array = new JArray();
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                {
                    MemoryEntry entry = values[i];
                    if (entry == null) continue;
                    array.Add(new JObject
                    {
                        ["sequence"] = entry.Sequence,
                        ["kind"] = entry.Kind.ToString(),
                        ["text"] = entry.Text,
                        ["cavalryPreference"] = entry.CavalryPreference.HasValue
                            ? (JToken)entry.CavalryPreference.Value.ToString() : JValue.CreateNull(),
                        ["status"] = entry.Status,
                        ["intentId"] = entry.IntentId.HasValue
                            ? (JToken)entry.IntentId.Value : JValue.CreateNull(),
                        ["planId"] = entry.PlanId.HasValue
                            ? (JToken)entry.PlanId.Value : JValue.CreateNull(),
                        ["objective"] = entry.Objective
                    });
                }
            return array.ToString(Formatting.None);
        }

        private void Add(MemoryEntryKind kind, string text,
            CommanderCavalryPreference? cavalryPreference = null, string status = null,
            int? intentId = null, int? planId = null, string objective = null)
        {
            string boundedText = Bound(text);
            string boundedStatus = Bound(status);
            string boundedObjective = Bound(objective);
            lock (sync)
            {
                entries.Add(new MemoryEntry(nextSequence++, kind, boundedText,
                    cavalryPreference, boundedStatus, intentId, planId, boundedObjective));
                while (entries.Count > Capacity) entries.RemoveAt(0);
            }
        }

        private static string Bound(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength
                ? value : value.Substring(0, MaximumTextLength);
        }
    }
}
