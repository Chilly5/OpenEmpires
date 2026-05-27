using UnityEngine;

namespace OpenEmpires
{
    // Orchestrates free-form chat between the local human and an ally AI teammate.
    //
    // Lives on the same GameObject as ChatUI. Only the typing client runs the LLM call;
    // structured intents extracted from the response are enqueued as deterministic
    // AiIntentCommands that replicate to every client through the existing command path.
    // The AI's free-form reply text renders locally on the typing client only — other
    // clients see the AI's behavior change but not the chat line. V2 can wire a
    // server-side relay if cross-client visibility is needed.
    public class LlmTeammateController : MonoBehaviour
    {
        [Tooltip("Throttle: minimum seconds between LLM calls per local player.")]
        public float MinSecondsBetweenCalls = 5f;

        [Tooltip("Log when reply↔intent reconciliation synthesizes intents or replies.")]
        [SerializeField] private bool logReconciliation;

        private float lastCallTimeRealtime = -100f;
        private bool callInFlight;

        // One-shot diagnostic flags so we don't spam logs/chat on every keystroke.
        private bool warnedNoApiKey;
        private bool warnedNoAi;
        private bool warnedSimMissing;

        // Returns true if this controller took ownership of the message (i.e. fired
        // an LLM call). Caller should skip the legacy keyword→ping path when true.
        public bool OnPlayerMessage(string text, int issuerPlayerId)
        {
            if (string.IsNullOrEmpty(text)) return false;

            string apiKey = DotEnvLoader.Get("GEMINI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                WarnOnce(ref warnedNoApiKey, "AI teammate unavailable: GEMINI_API_KEY not found in .env or env var.");
                return false;
            }

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.IsMatchOver)
            {
                WarnOnce(ref warnedSimMissing, "AI teammate unavailable: game simulation not active.");
                return false;
            }

            int aiPlayerId = FindAllyAi(sim, issuerPlayerId);
            if (aiPlayerId < 0)
            {
                WarnOnce(ref warnedNoAi, "AI teammate unavailable: no ally AI on your team.");
                return false;
            }

            // Throttle: excess messages fall through to the legacy keyword path so the
            // player still gets *some* response.
            float now = Time.realtimeSinceStartup;
            if (callInFlight || (now - lastCallTimeRealtime) < MinSecondsBetweenCalls) return false;
            lastCallTimeRealtime = now;
            callInFlight = true;

            string aiName = $"AI Player {aiPlayerId}";
            string systemPrompt = LlmIntentSchema.BuildSystemPrompt(aiName, aiPlayerId);
            string stateLine = LlmStateExtractor.Build(sim, issuerPlayerId, aiPlayerId);
            string userMessage = "[Game state] " + stateLine + "\n[Player says] " + text;

            var history = LlmConversationMemory.GetHistory(issuerPlayerId, aiPlayerId);
            LlmConversationMemory.Append(issuerPlayerId, aiPlayerId, true, text);

            int tickAtSend = sim.CurrentTick;
            StartCoroutine(GeminiClient.Send(apiKey, systemPrompt, history, userMessage,
                onSuccess: json => HandleLlmReply(json, issuerPlayerId, aiPlayerId, tickAtSend),
                onFailure: reason => HandleLlmFailure(reason, aiPlayerId)));
            return true;
        }

        private void HandleLlmReply(string json, int issuerPlayerId, int aiPlayerId, int tickAtSend)
        {
            callInFlight = false;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var parsed = LlmIntentSchema.Parse(json, sim, aiPlayerId, sim.CurrentTick);

            int intentsBefore = parsed.Intents?.Count ?? 0;
            string replyBefore = parsed.Reply ?? string.Empty;
            parsed = LlmIntentSchema.ReconcileReplyAndIntents(parsed, sim, aiPlayerId, sim.CurrentTick);
            if (logReconciliation)
            {
                int intentsAfter = parsed.Intents?.Count ?? 0;
                if (intentsAfter != intentsBefore || !ReferenceEquals(parsed.Reply, replyBefore))
                {
                    Debug.Log($"[LlmTeammate] Reconciled: intents {intentsBefore}→{intentsAfter}, reply='{parsed.Reply}'");
                }
            }

            if (!string.IsNullOrEmpty(parsed.Reply))
            {
                LlmConversationMemory.Append(issuerPlayerId, aiPlayerId, false, parsed.Reply);
                RenderAiChatLocally(aiPlayerId, parsed.Reply);
            }

            if (parsed.Intents != null)
            {
                for (int i = 0; i < parsed.Intents.Count; i++)
                {
                    var intent = parsed.Intents[i];
                    sim.CommandBuffer.EnqueueCommand(new AiIntentCommand(
                        aiPlayerId,
                        issuerPlayerId,
                        intent.Kind,
                        intent.ParamA, intent.ParamB, intent.ParamC, intent.ParamD,
                        intent.DurationTicks,
                        intent.TriggerType, intent.TriggerMagnitude));
                }
            }
        }

        private void HandleLlmFailure(string reason, int aiPlayerId)
        {
            callInFlight = false;
            Debug.LogWarning($"[LlmTeammate] Gemini call failed: {reason}");
            RenderAiChatLocally(aiPlayerId, "(AI didn't respond)", isSystem: true);
        }

        private void RenderAiChatLocally(int aiPlayerId, string text, bool isSystem = false)
        {
            string name = $"AI Player {aiPlayerId}";
            Color color = aiPlayerId >= 0 && aiPlayerId < GameSetup.PlayerColors.Length
                ? GameSetup.PlayerColors[aiPlayerId]
                : Color.white;
            ChatManager.AddMessage(new ChatMessage
            {
                SenderName = name,
                SenderColor = color,
                Text = text,
                Channel = ChatChannel.Team,
                IsSystem = isSystem,
                SenderPlayerId = aiPlayerId,
            });
        }

        // Logs once per session AND posts a one-time system chat line so the player
        // sees *why* the AI isn't responding instead of just silence.
        private void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            flag = true;
            Debug.LogWarning("[LlmTeammate] " + message);
            ChatManager.AddMessage(new ChatMessage
            {
                SenderName = "System",
                SenderColor = Color.gray,
                Text = message,
                Channel = ChatChannel.Team,
                IsSystem = true,
                SenderPlayerId = -1,
            });
        }

        // Lowest-playerId ally AI on the human's team. Deterministic ordering so two
        // human teammates pick the same target when chatting.
        private static int FindAllyAi(GameSimulation sim, int humanPlayerId)
        {
            int best = -1;
            foreach (var aiPid in sim.AiPlayerIds)
            {
                if (aiPid == humanPlayerId) continue;
                if (!sim.AreAllies(humanPlayerId, aiPid)) continue;
                if (best < 0 || aiPid < best) best = aiPid;
            }
            return best;
        }
    }
}
