using System.Collections.Generic;

namespace OpenEmpires
{
    // Per-(localPlayer, aiPlayer) chat history for the LLM teammate controller. Bounded
    // to a small ring buffer so the prompt stays cheap. State is non-deterministic and
    // local to the typing client — nothing here ever crosses into sim state.
    public static class LlmConversationMemory
    {
        private const int MaxTurns = 20; // ~10 user/ai pairs

        private static readonly Dictionary<long, List<GeminiClient.Turn>> turns =
            new Dictionary<long, List<GeminiClient.Turn>>();

        private static long Key(int localPlayerId, int aiPlayerId)
            => ((long)localPlayerId << 32) | (uint)aiPlayerId;

        public static List<GeminiClient.Turn> GetHistory(int localPlayerId, int aiPlayerId)
        {
            if (turns.TryGetValue(Key(localPlayerId, aiPlayerId), out var list))
                return list;
            return null;
        }

        public static void Append(int localPlayerId, int aiPlayerId, bool isUser, string text)
        {
            var k = Key(localPlayerId, aiPlayerId);
            if (!turns.TryGetValue(k, out var list))
            {
                list = new List<GeminiClient.Turn>();
                turns[k] = list;
            }
            list.Add(new GeminiClient.Turn { IsUser = isUser, Text = text });
            while (list.Count > MaxTurns) list.RemoveAt(0);
        }

        public static void ClearAll() => turns.Clear();
    }
}
