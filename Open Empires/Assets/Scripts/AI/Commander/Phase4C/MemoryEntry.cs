using System;

namespace OpenEmpires
{
    public enum MemoryEntryKind
    {
        PlayerConversation,
        CommanderConversation,
        Preference,
        ApprovedStrategy,
        Decision,
        Explanation
    }

    public enum CommanderCavalryPreference
    {
        Cavalry = 1
    }

    public sealed class MemoryEntry
    {
        public long Sequence { get; }
        public MemoryEntryKind Kind { get; }
        public string Text { get; }
        public CommanderCavalryPreference? CavalryPreference { get; }
        public string Status { get; }
        public int? IntentId { get; }
        public int? PlanId { get; }
        public string Objective { get; }

        internal MemoryEntry(long sequence, MemoryEntryKind kind, string text,
            CommanderCavalryPreference? cavalryPreference = null, string status = null,
            int? intentId = null, int? planId = null, string objective = null)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(MemoryEntryKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (cavalryPreference.HasValue
                && cavalryPreference.Value != CommanderCavalryPreference.Cavalry)
                throw new ArgumentOutOfRangeException(nameof(cavalryPreference));
            Sequence = sequence;
            Kind = kind;
            Text = text ?? string.Empty;
            CavalryPreference = cavalryPreference;
            Status = status ?? string.Empty;
            IntentId = intentId;
            PlanId = planId;
            Objective = objective ?? string.Empty;
        }

        internal MemoryEntry Copy() => new MemoryEntry(Sequence, Kind, Text,
            CavalryPreference, Status, IntentId, PlanId, Objective);
    }
}
