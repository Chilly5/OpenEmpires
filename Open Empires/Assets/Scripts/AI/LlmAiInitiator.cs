using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // Fires AI-initiated chat lines (with optional structured intents) when interesting
    // events happen on an ally AI's side. Polls AIPlayerSystem state once per second;
    // detects state transitions (age-up, first enemy spotted, army assembled, base attacked);
    // on transitions, the OWNER client runs the Gemini call and pushes the resulting reply
    // (locally rendered) + intents (deterministic command path).
    //
    // Owner rule: lowest-pid human ally of the AI. Ensures exactly one client per AI runs
    // the LLM, regardless of how many humans are on the team.
    public class LlmAiInitiator : MonoBehaviour
    {
        [Tooltip("Cooldown between AI-initiated messages per AI.")]
        public float MinSecondsBetweenInitiations = 60f;

        [Tooltip("Seconds between state polls.")]
        public float PollInterval = 1f;

        [SerializeField] private bool logInitiator;

        public int LocalPlayerId { get; set; }

        private struct AiSnapshot
        {
            public bool Initialized;
            public int LastAge;
            public int LastKnownEnemyBaseCount;
            public int LastCombatStateHash;
            public int LastUnderAttackTick;
            public float LastInitiationRealtime;
        }
        private readonly Dictionary<int, AiSnapshot> snapshots = new Dictionary<int, AiSnapshot>();
        private float nextPollRealtime;
        private bool callInFlight;
        private bool warnedNoApiKey;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextPollRealtime) return;
            nextPollRealtime = now + PollInterval;
            if (callInFlight) return;
            Poll();
        }

        private void Poll()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.IsMatchOver) return;
            string apiKey = DotEnvLoader.Get("GEMINI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                if (!warnedNoApiKey)
                {
                    warnedNoApiKey = true;
                    Debug.LogWarning("[LlmAiInitiator] AI-initiated chat disabled: GEMINI_API_KEY not found.");
                }
                return;
            }

            foreach (int aiPid in sim.AiPlayerIds)
            {
                if (!IsOwnerOf(sim, aiPid)) continue;
                var ai = sim.GetAiPlayer(aiPid);
                if (ai == null) continue;

                var snap = snapshots.TryGetValue(aiPid, out var existing) ? existing : default;
                string trigger = DetectTrigger(sim, ai, ref snap);
                snapshots[aiPid] = snap;

                if (trigger != null)
                {
                    Initiate(sim, aiPid, trigger, apiKey);
                    return; // one initiation per poll
                }
            }
        }

        private string DetectTrigger(GameSimulation sim, AIPlayerSystem ai, ref AiSnapshot snap)
        {
            int age = sim.GetPlayerAge(ai.PlayerId);
            int enemies = ai.KnownEnemyBaseCount;
            int stateHash = ai.CombatStateName.GetHashCode();
            int underAttackTick = ai.LastEnemyAttackOnMeTick;

            // Initialize on first poll without firing; we want to detect transitions only.
            if (!snap.Initialized)
            {
                snap.LastAge = age;
                snap.LastKnownEnemyBaseCount = enemies;
                snap.LastCombatStateHash = stateHash;
                snap.LastUnderAttackTick = underAttackTick;
                snap.Initialized = true;
                return null;
            }

            // Per-AI cooldown so we don't spam.
            if (Time.realtimeSinceStartup - snap.LastInitiationRealtime < MinSecondsBetweenInitiations)
            {
                snap.LastAge = age;
                snap.LastKnownEnemyBaseCount = enemies;
                snap.LastCombatStateHash = stateHash;
                snap.LastUnderAttackTick = underAttackTick;
                return null;
            }

            string trigger = null;

            if (age > snap.LastAge)
            {
                trigger = $"You just reached Age {age}. What's your plan?";
            }
            else if (enemies > snap.LastKnownEnemyBaseCount && snap.LastKnownEnemyBaseCount == 0)
            {
                trigger = "You just spotted the first enemy base. Suggest a course of action.";
            }
            else if (stateHash != snap.LastCombatStateHash
                  && ai.CombatStateName == "Assembling"
                  && ai.ArmySize >= 8)
            {
                trigger = $"Your army of {ai.ArmySize} units is assembled at base. Coordinate with the human: push, hold, or wait?";
            }
            else if (underAttackTick > snap.LastUnderAttackTick + 600 // new attack wave
                  && underAttackTick > 0)
            {
                trigger = "Your base just came under fresh attack. Ask the human for help or commit defenders yourself.";
            }

            snap.LastAge = age;
            snap.LastKnownEnemyBaseCount = enemies;
            snap.LastCombatStateHash = stateHash;
            snap.LastUnderAttackTick = underAttackTick;
            if (trigger != null) snap.LastInitiationRealtime = Time.realtimeSinceStartup;
            return trigger;
        }

        private bool IsOwnerOf(GameSimulation sim, int aiPid)
        {
            int lowestHumanAlly = -1;
            for (int pid = 0; pid < sim.PlayerCount; pid++)
            {
                if (pid == aiPid) continue;
                if (IsAi(sim, pid)) continue;
                if (!sim.AreAllies(pid, aiPid)) continue;
                if (lowestHumanAlly < 0 || pid < lowestHumanAlly) lowestHumanAlly = pid;
            }
            return lowestHumanAlly == LocalPlayerId;
        }

        private static bool IsAi(GameSimulation sim, int pid)
        {
            foreach (int a in sim.AiPlayerIds)
                if (a == pid) return true;
            return false;
        }

        private void Initiate(GameSimulation sim, int aiPid, string trigger, string apiKey)
        {
            callInFlight = true;
            string aiName = $"AI Player {aiPid}";
            string systemPrompt = LlmIntentSchema.BuildSystemPrompt(aiName, aiPid);
            string stateLine = LlmStateExtractor.Build(sim, LocalPlayerId, aiPid);
            string userMessage = "[Game state] " + stateLine + "\n[AI internal event] " + trigger
                + "\nYou are SPEAKING FIRST to the human teammate. Be brief and concrete.";

            var history = LlmConversationMemory.GetHistory(LocalPlayerId, aiPid);

            StartCoroutine(GeminiClient.Send(apiKey, systemPrompt, history, userMessage,
                onSuccess: json => HandleReply(json, aiPid),
                onFailure: reason =>
                {
                    callInFlight = false;
                    if (logInitiator) Debug.LogWarning($"[LlmAiInitiator] Gemini failed: {reason}");
                }));
        }

        private void HandleReply(string json, int aiPid)
        {
            callInFlight = false;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var parsed = LlmIntentSchema.Parse(json, sim, aiPid, sim.CurrentTick);
            parsed = LlmIntentSchema.ReconcileReplyAndIntents(parsed, sim, aiPid, sim.CurrentTick);

            if (!string.IsNullOrEmpty(parsed.Reply))
            {
                LlmConversationMemory.Append(LocalPlayerId, aiPid, false, parsed.Reply);
                RenderAiChatLocally(aiPid, parsed.Reply);
            }

            if (parsed.Intents != null)
            {
                for (int i = 0; i < parsed.Intents.Count; i++)
                {
                    var intent = parsed.Intents[i];
                    // Issuer = aiPid: self-issued. TeamHelper.AreAllies(x,x) returns true,
                    // so ProcessAiIntentCommand will accept it.
                    sim.CommandBuffer.EnqueueCommand(new AiIntentCommand(
                        aiPid,
                        aiPid,
                        intent.Kind,
                        intent.ParamA, intent.ParamB, intent.ParamC, intent.ParamD,
                        intent.DurationTicks,
                        intent.TriggerType, intent.TriggerMagnitude));
                }
            }

            if (logInitiator)
                Debug.Log($"[LlmAiInitiator] AI player {aiPid} said: {parsed.Reply}");
        }

        private void RenderAiChatLocally(int aiPlayerId, string text)
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
                IsSystem = false,
                SenderPlayerId = aiPlayerId,
            });
        }
    }
}
