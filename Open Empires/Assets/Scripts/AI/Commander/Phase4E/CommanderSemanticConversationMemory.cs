using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public enum CommanderSemanticMemoryKind
    {
        AcceptedUnitCount,
        AcceptedStructure,
        Clarification
    }

    // Detached, bounded facts for semantic follow-ups. This type intentionally contains
    // no Unity objects, entity IDs, coordinates, workers, goals, or commands.
    public sealed class CommanderSemanticMemoryEntry
    {
        public long Sequence { get; }
        public CommanderSemanticMemoryKind Kind { get; }
        public string Unit { get; }
        public int TargetTotal { get; }
        public string Structure { get; }
        public int Count { get; }
        public string Anchor { get; }
        public string Relation { get; }
        public int? ClearGapTiles { get; }
        public string Message { get; }

        internal CommanderSemanticMemoryEntry(long sequence, CommanderSemanticMemoryKind kind,
            string unit = null, int targetTotal = 0, string structure = null, int count = 0,
            string anchor = null, string relation = null, int? clearGapTiles = null,
            string message = null)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(CommanderSemanticMemoryKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Sequence = sequence;
            Kind = kind;
            Unit = unit ?? string.Empty;
            TargetTotal = Math.Max(0, Math.Min(200, targetTotal));
            Structure = structure ?? string.Empty;
            Count = Math.Max(0, Math.Min(20, count));
            Anchor = anchor ?? string.Empty;
            Relation = relation ?? string.Empty;
            ClearGapTiles = clearGapTiles;
            Message = message ?? string.Empty;
        }

        internal CommanderSemanticMemoryEntry Copy() => new CommanderSemanticMemoryEntry(
            Sequence, Kind, Unit, TargetTotal, Structure, Count, Anchor, Relation,
            ClearGapTiles, Message);
    }

    public sealed class CommanderSemanticConversationMemory
    {
        public const int DefaultCapacity = 8;
        public const int MaximumCapacity = 32;
        public const int MaximumTextLength = 180;
        private readonly object sync = new object();
        private readonly List<CommanderSemanticMemoryEntry> entries =
            new List<CommanderSemanticMemoryEntry>();
        private long nextSequence = 1;

        public int Capacity { get; }

        public CommanderSemanticConversationMemory(int capacity = DefaultCapacity)
        {
            if (capacity < 1 || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public void RecordAcceptedUnitCount(int unitType, int targetTotal)
        {
            if (!CommanderIntentCatalog.IsSupportedUnit(unitType))
                throw new ArgumentException("The semantic unit type is not supported.", nameof(unitType));
            Add(CommanderSemanticMemoryKind.AcceptedUnitCount,
                unit: CommanderIntentCatalog.GetUnitDisplayName(unitType),
                targetTotal: targetTotal);
        }

        public void RecordAcceptedStructure(BuildingType structure, int count,
            CommanderSemanticAnchorSelector? anchor = null,
            CommanderSemanticPlacementRelation? relation = null,
            int? clearGapTiles = null)
        {
            Add(CommanderSemanticMemoryKind.AcceptedStructure,
                structure: structure.ToString(), count: count,
                anchor: anchor?.ToString(), relation: relation?.ToString(),
                clearGapTiles: clearGapTiles);
        }

        public void RecordClarification(string message)
        {
            string bounded = Bound(message);
            if (string.IsNullOrWhiteSpace(bounded)) return;
            Add(CommanderSemanticMemoryKind.Clarification, message: bounded);
        }

        public IReadOnlyList<CommanderSemanticMemoryEntry> Snapshot()
        {
            lock (sync)
            {
                var copy = new List<CommanderSemanticMemoryEntry>(entries.Count);
                for (int i = 0; i < entries.Count; i++) copy.Add(entries[i].Copy());
                return new ReadOnlyCollection<CommanderSemanticMemoryEntry>(copy);
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

        public string ToJson() => Serialize(Snapshot());

        internal static string Serialize(IReadOnlyList<CommanderSemanticMemoryEntry> values)
        {
            var array = new JArray();
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                {
                    CommanderSemanticMemoryEntry entry = values[i];
                    if (entry == null) continue;
                    var item = new JObject
                    {
                        ["kind"] = entry.Kind.ToString(),
                        ["unit"] = entry.Unit,
                        ["targetTotal"] = entry.TargetTotal,
                        ["structure"] = entry.Structure,
                        ["count"] = entry.Count,
                        ["anchor"] = entry.Anchor,
                        ["relation"] = entry.Relation,
                        ["clearGapTiles"] = entry.ClearGapTiles.HasValue
                            ? (JToken)entry.ClearGapTiles.Value : JValue.CreateNull(),
                        ["message"] = entry.Message
                    };
                    array.Add(item);
                }
            return array.ToString(Formatting.None);
        }

        private void Add(CommanderSemanticMemoryKind kind, string unit = null,
            int targetTotal = 0, string structure = null, int count = 0,
            string anchor = null, string relation = null, int? clearGapTiles = null,
            string message = null)
        {
            lock (sync)
            {
                entries.Add(new CommanderSemanticMemoryEntry(nextSequence++, kind,
                    unit, targetTotal, structure, count, anchor, relation,
                    clearGapTiles, message));
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
