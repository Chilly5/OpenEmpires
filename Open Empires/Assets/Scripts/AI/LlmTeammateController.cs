using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // Orchestrates free-form chat between the local human and an ally AI teammate using
    // native LLM function calling.
    //
    // Lives on the same GameObject as ChatUI. Only the typing client runs the LLM loop;
    // the action tools the model calls are collected as deterministic AiIntentCommands
    // that replicate to every client through the existing command path. The AI's spoken
    // reply renders locally on the typing client only — other clients see the AI's
    // behavior change but not the chat line.
    public class LlmTeammateController : MonoBehaviour
    {
        [Tooltip("Throttle: minimum seconds between LLM turns per local player.")]
        public float MinSecondsBetweenCalls = 5f;

        [Header("Multimodal context (owner-client input only; never affects the sim)")]
        [Tooltip("Send an off-screen top-down tactical snapshot (PNG) with each LLM turn.")]
        public bool SendTacticalSnapshot = true;

        [Tooltip("Square pixel size of the tactical snapshot.")]
        public int SnapshotResolution = 640;

        [Tooltip("Orthographic half-size (world units) framed by the snapshot.")]
        public float SnapshotWorldHalfSize = 40f;

        [Tooltip("Send the last few seconds of game audio (WAV) with each LLM turn.")]
        public bool SendGameAudio = true;

        [Tooltip("Seconds of recent game audio to send.")]
        public float AudioWindowSeconds = 8f;

        [Tooltip("Log the AI teammate's reasoning, tool calls, and results to the console.")]
        [SerializeField] private bool verboseLogging = true;

        [Tooltip("Also dump raw Gemini HTTP request/response bodies (very noisy).")]
        [SerializeField] private bool verboseHttpLogging;

        private void Awake() => ApplyDebugFlags();
        private void OnValidate() => ApplyDebugFlags();
        private void ApplyDebugFlags()
        {
            LlmDebug.Verbose = verboseLogging;
            LlmDebug.VerboseHttp = verboseHttpLogging;
        }

        private float lastCallTimeRealtime = -100f;
        private bool callInFlight;

        private bool warnedNoApiKey;
        private bool warnedNoAi;
        private bool warnedSimMissing;

        private float lastBusyNoticeRealtime = -100f;
        private const float BusyNoticeCooldown = 2f;

        // Returns true if this controller took ownership of the message (i.e. fired an
        // LLM turn). Caller should skip the legacy keyword→ping path when true.
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

            float now = Time.realtimeSinceStartup;
            if (callInFlight)
            {
                PostBusyNotice("AI is still thinking — message dropped.");
                return false;
            }
            float cooldownRemaining = MinSecondsBetweenCalls - (now - lastCallTimeRealtime);
            if (cooldownRemaining > 0f)
            {
                PostBusyNotice($"AI on cooldown ({cooldownRemaining:0.#}s) — message dropped.");
                return false;
            }
            lastCallTimeRealtime = now;
            callInFlight = true;

            string aiName = $"AI Player {aiPlayerId}";
            string systemPrompt = LlmTool.BuildSystemPrompt(aiName);
            string stateLine = LlmStateExtractor.Build(sim, issuerPlayerId, aiPlayerId);
            string userMessage = "[Game state] " + stateLine + "\n[Teammate says] " + text;

            var history = LlmConversationMemory.GetHistory(issuerPlayerId, aiPlayerId);
            LlmConversationMemory.Append(issuerPlayerId, aiPlayerId, true, text);
            var media = CaptureMedia(sim, aiPlayerId);
            var contents = LlmToolLoop.BuildInitialContents(history, userMessage, media);

            Debug.Log($"[LlmTeammate] → AI{aiPlayerId}: {text}");
            StartCoroutine(LlmToolLoop.Run(apiKey, systemPrompt, contents, sim, aiPlayerId, issuerPlayerId,
                onComplete: (intents, reply) => HandleComplete(intents, reply, issuerPlayerId, aiPlayerId),
                onFailure: reason => HandleFailure(reason, aiPlayerId)));
            return true;
        }

        // Captures the configured multimodal inputs (tactical snapshot + game audio) for one
        // LLM turn. Owner-client only and read-only on sim state. Returns null when both
        // inputs are disabled or unavailable, so the caller falls back to text-only.
        // Shared with LlmAiInitiator (same GameObject) so config lives in one place.
        public List<GeminiClient.Blob> CaptureMedia(GameSimulation sim, int aiPlayerId)
        {
            List<GeminiClient.Blob> media = null;

            if (SendTacticalSnapshot)
            {
                byte[] png = GameSnapshotCapture.Instance.CapturePng(
                    sim, aiPlayerId, SnapshotResolution, SnapshotWorldHalfSize);
                if (png != null && png.Length > 0)
                {
                    if (media == null) media = new List<GeminiClient.Blob>(2);
                    media.Add(new GeminiClient.Blob(png, "image/png"));
                }
            }

            if (SendGameAudio)
            {
                var audio = GameAudioCapture.Instance;
                byte[] wav = audio != null ? audio.EncodeRecentWav(AudioWindowSeconds) : null;
                if (wav != null && wav.Length > 0)
                {
                    if (media == null) media = new List<GeminiClient.Blob>(2);
                    media.Add(new GeminiClient.Blob(wav, "audio/wav"));
                }
            }

            if (media != null)
                LlmDebug.Log($"AI{aiPlayerId} multimodal: {media.Count} part(s) attached.");
            return media;
        }

        private void HandleComplete(List<LlmIntentSchema.ParsedIntent> intents, string reply,
            int issuerPlayerId, int aiPlayerId)
        {
            callInFlight = false;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int intentCount = intents?.Count ?? 0;
            Debug.Log($"[LlmTeammate] ← AI{aiPlayerId}: reply='{reply}' intents={intentCount}");

            if (!string.IsNullOrEmpty(reply))
            {
                string trimmed = reply.Length > LlmIntentSchema.MaxReplyChars
                    ? reply.Substring(0, LlmIntentSchema.MaxReplyChars)
                    : reply;
                LlmConversationMemory.Append(issuerPlayerId, aiPlayerId, false, trimmed);
                RenderAiChatLocally(aiPlayerId, trimmed);
            }

            if (intents != null)
            {
                for (int i = 0; i < intents.Count; i++)
                    EnqueueIntent(sim, aiPlayerId, issuerPlayerId, intents[i]);
            }

            if (string.IsNullOrEmpty(reply) && intentCount == 0)
                RenderAiChatLocally(aiPlayerId, "(AI had nothing to say)", isSystem: true);
        }

        private void HandleFailure(string reason, int aiPlayerId)
        {
            callInFlight = false;
            Debug.LogWarning($"[LlmTeammate] LLM turn failed: {reason}");
            RenderAiChatLocally(aiPlayerId, "(AI didn't respond)", isSystem: true);
        }

        // issuerPlayerId = the human who asked; must be a same-team ally of the target AI.
        private static void EnqueueIntent(GameSimulation sim, int aiPlayerId, int issuerPlayerId,
            LlmIntentSchema.ParsedIntent intent)
        {
            LlmDebug.Cmd($"queue AiIntent {intent.Kind} → AI{aiPlayerId} (issuer {issuerPlayerId}) "
                + $"A={intent.ParamA} B={intent.ParamB} C={intent.ParamC} D={intent.ParamD} "
                + $"dur={intent.DurationTicks} trig={intent.TriggerType}/{intent.TriggerMagnitude}");
            sim.CommandBuffer.EnqueueCommand(new AiIntentCommand(
                aiPlayerId, issuerPlayerId, intent.Kind,
                intent.ParamA, intent.ParamB, intent.ParamC, intent.ParamD,
                intent.DurationTicks, intent.TriggerType, intent.TriggerMagnitude));
        }

        private void RenderAiChatLocally(int aiPlayerId, string text, bool isSystem = false)
        {
            string name = ChatUI.FormatAiSpeakerName(aiPlayerId);
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

        private void PostBusyNotice(string message)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastBusyNoticeRealtime < BusyNoticeCooldown) return;
            lastBusyNoticeRealtime = now;
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
