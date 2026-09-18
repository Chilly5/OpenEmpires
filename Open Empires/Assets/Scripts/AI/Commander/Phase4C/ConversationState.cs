using System.Collections.Generic;
using System;

namespace OpenEmpires
{
    public sealed class ConversationState
    {
        public int PlayerId { get; }
        public CommanderMemory Memory { get; }

        public ConversationState(int playerId, int capacity = CommanderMemory.DefaultCapacity)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
            Memory = new CommanderMemory(capacity);
        }

        public IReadOnlyList<MemoryEntry> Snapshot() => Memory.Snapshot();
        public bool TryGetCavalryPreference(out CommanderCavalryPreference preference)
        {
            IReadOnlyList<MemoryEntry> snapshot = Memory.Snapshot();
            for (int i = snapshot.Count - 1; i >= 0; i--)
                if (snapshot[i].Kind == MemoryEntryKind.Preference
                    && snapshot[i].CavalryPreference == CommanderCavalryPreference.Cavalry)
                {
                    preference = CommanderCavalryPreference.Cavalry;
                    return true;
                }
            preference = default;
            return false;
        }
        public void Reset() => Memory.Clear();
    }
}
