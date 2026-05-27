using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenEmpires
{
    // Defines the strict JSON contract between the LLM and the sim. The LLM never names
    // raw map coordinates — it picks from a small keyword set ("enemy_base", "my_base",
    // "front_line"), which this parser resolves through deterministic sim reads. Every
    // numeric is clamped. Unknown intent kinds are rejected per-intent, not per-message.
    public static class LlmIntentSchema
    {
        public const int MaxIntentsPerTurn = 4;
        public const int MinDurationTicks = 150;   // 5s
        public const int MaxDurationTicks = 5400;  // 3min
        public const int MaxReplyChars = 200;

        public struct ParsedIntent
        {
            public AiIntentKind Kind;
            public int ParamA, ParamB, ParamC, ParamD;
            public int DurationTicks;
            public int TriggerType;       // 0=immediate, 1=delay, 2=on_age_up, 3=on_army_size, 4=on_enemy_attack
            public int TriggerMagnitude;  // delay-ticks / target-age / army-count
        }

        // Unit-class filter for AttackAt/DefendAt's ParamC.
        public const int UnitClassAll      = 0;
        public const int UnitClassArchers  = 1;
        public const int UnitClassHorsemen = 2;
        public const int UnitClassSpearmen = 3;

        public const int TriggerImmediate    = 0;
        public const int TriggerDelay        = 1;
        public const int TriggerOnAgeUp      = 2;
        public const int TriggerOnArmySize   = 3;
        public const int TriggerOnEnemyAttack = 4;

        public struct Result
        {
            public string Reply;
            public List<ParsedIntent> Intents;
        }

        public static string BuildSystemPrompt(string aiName, int aiPlayerId)
        {
            var sb = new StringBuilder(1024);
            sb.Append("You are ").Append(aiName).Append(", an RTS AI teammate playing alongside the human. ");
            sb.Append("Reply in 1-2 short sentences, in character, like a teammate over voice comms. ");
            sb.Append("Then optionally issue structured intents that change your in-game priorities. ");
            sb.Append("Output ONLY JSON matching this schema, no prose outside it:\n");
            sb.Append("{\n");
            sb.Append("  \"reply\": \"string, <=200 chars\",\n");
            sb.Append("  \"intents\": [ /* up to 4 items, omit if no action */\n");
            sb.Append("    {\n");
            sb.Append("      \"kind\": \"AttackAt\" | \"DefendAt\" | \"SetAggression\" | \"PrioritizeResource\" | \"FocusEconomy\" | \"FocusMilitary\" | \"PushAgeUp\" | \"Acknowledge\" | \"Decline\",\n");
            sb.Append("      \"target\": position keyword for AttackAt/DefendAt:\n");
            sb.Append("        - \"enemy_base\" | \"my_base\" | \"front_line\"\n");
            sb.Append("        - cardinal: \"north\" | \"south\" | \"east\" | \"west\"\n");
            sb.Append("        - building-relative: \"near_my_X\" or \"near_their_X\" where X in\n");
            sb.Append("          {tc, barracks, archery, stables, landmark, mill, mine, lumber_yard,\n");
            sb.Append("           tower, keep, blacksmith, market, monastery, siege_workshop, university, wonder}\n");
            sb.Append("        - resource: \"my_gold\" | \"their_gold\"\n");
            sb.Append("      \"army_subset\": \"all\" | \"archers\" | \"horsemen\" | \"spearmen\",   // for AttackAt/DefendAt; default \"all\"\n");
            sb.Append("      \"resource\": \"Food\" | \"Wood\" | \"Gold\" | \"Stone\",     // for PrioritizeResource\n");
            sb.Append("      \"weight\": int -5..10,                                  // for PrioritizeResource\n");
            sb.Append("      \"level\": int 0..100,                                   // for SetAggression/Focus*\n");
            sb.Append("      \"retreat\": int 10..80,                                 // for SetAggression\n");
            sb.Append("      \"age\": 2 | 3,                                          // for PushAgeUp\n");
            sb.Append("      \"duration_seconds\": int 5..180,\n");
            sb.Append("      \"trigger\": \"immediate\" | \"delay\" | \"on_age_up\" | \"on_army_size\" | \"on_enemy_attack\",\n");
            sb.Append("      \"trigger_value\": int  // delay in seconds (for delay), army size (for on_army_size), or target age (for on_age_up)\n");
            sb.Append("    }\n");
            sb.Append("  ]\n");
            sb.Append("}\n");
            sb.Append("Rules: keep reply natural and short. Only issue intents the human asked for or strongly implied. ");
            sb.Append("If the human is just chatting, use kind=Acknowledge with no other intents. ");
            sb.Append("Truthfulness: every action you mention in your reply MUST appear as a matching intent. ");
            sb.Append("Don't say \"attacking now\" without an AttackAt intent. Don't act silently — if you ");
            sb.Append("emit an intent, say what you're doing in the reply. ");
            sb.Append("When the human specifies WHEN to act (\"in 90 seconds\", \"when you have 15 units\", \"after you age up\"), ");
            sb.Append("set the matching trigger/trigger_value — never lie about timing. If they say \"with archers\" or ");
            sb.Append("\"send your knights\", set army_subset accordingly. Reference real map features from the game state ");
            sb.Append("when describing what you'll do (e.g. mention enemy positions you actually know about).");
            return sb.ToString();
        }

        // Reconciles the LLM's free-form reply with its structured intents so the chat
        // text becomes load-bearing instead of cosmetic. Two passes:
        //   A) reply commits to an action that has no matching intent → synthesize one
        //   B) intents are present but reply is empty or generic → fill in a templated line
        // Runs only on the typing client (same boundary as the LLM call). Synthesized
        // intents go through the existing deterministic AiIntentCommand path.
        public static Result ReconcileReplyAndIntents(Result parsed, GameSimulation sim, int aiPlayerId, int currentTick)
        {
            if (sim == null) return parsed;
            if (parsed.Intents == null) parsed.Intents = new List<ParsedIntent>();

            string reply = parsed.Reply ?? string.Empty;
            string trimmed = reply.TrimEnd();
            bool isQuestion = trimmed.Length > 0 && trimmed[trimmed.Length - 1] == '?';
            string lower = reply.ToLowerInvariant();
            string[] tokens = Tokenize(lower);

            // Pass A — synthesize missing intents from committal phrases.
            if (!isQuestion)
            {
                TrySynthesizePositional(tokens, AttackKeywords, AiIntentKind.AttackAt, "enemy_base",
                    60, parsed.Intents, sim, aiPlayerId);
                TrySynthesizePositional(tokens, DefendKeywords, AiIntentKind.DefendAt, "my_base",
                    60, parsed.Intents, sim, aiPlayerId);
                TrySynthesizeAggression(tokens, parsed.Intents);
                TrySynthesizeFocus(tokens, BoomKeywords, AiIntentKind.FocusEconomy, 60, 120, parsed.Intents);
                TrySynthesizeFocus(tokens, MilitaryKeywords, AiIntentKind.FocusMilitary, 70, 120, parsed.Intents);
                TrySynthesizeAgeUp(tokens, parsed.Intents, sim, aiPlayerId);
                TrySynthesizeResource(tokens, parsed.Intents);
            }

            // Pass B — synthesize a templated reply when the AI acted silently.
            if (parsed.Intents.Count > 0 && IsEmptyOrGeneric(reply))
            {
                for (int i = 0; i < parsed.Intents.Count; i++)
                {
                    var kind = parsed.Intents[i].Kind;
                    if (kind == AiIntentKind.Acknowledge || kind == AiIntentKind.Decline) continue;
                    parsed.Reply = TemplatedReplyFor(kind);
                    break;
                }
            }

            return parsed;
        }

        private static void TrySynthesizePositional(string[] tokens, string[] keywords, AiIntentKind kind,
            string positionKeyword, int durationSeconds, List<ParsedIntent> intents,
            GameSimulation sim, int aiPlayerId)
        {
            if (intents.Count >= MaxIntentsPerTurn) return;
            if (AlreadyHasKind(intents, kind)) return;
            if (!FindKeyword(tokens, keywords, out int wordIndex)) return;
            if (HasNegationBefore(tokens, wordIndex)) return;
            if (!TryResolvePosition(positionKeyword, sim, aiPlayerId, out int rawX, out int rawZ)) return;

            intents.Add(new ParsedIntent
            {
                Kind = kind,
                ParamA = rawX,
                ParamB = rawZ,
                DurationTicks = Mathf.Clamp(durationSeconds * 30, MinDurationTicks, MaxDurationTicks),
            });
        }

        private static void TrySynthesizeAggression(string[] tokens, List<ParsedIntent> intents)
        {
            if (intents.Count >= MaxIntentsPerTurn) return;
            if (AlreadyHasKind(intents, AiIntentKind.SetAggression)) return;
            if (!FindKeyword(tokens, RetreatKeywords, out int wordIndex)) return;
            if (HasNegationBefore(tokens, wordIndex)) return;

            // level=15 → high attackThreshold (~28) + high retreat% (~52). Cautious play.
            intents.Add(new ParsedIntent
            {
                Kind = AiIntentKind.SetAggression,
                ParamA = Mathf.Clamp(32 - (15 * 30 / 100), 2, 32),
                ParamB = Mathf.Clamp(60 - (15 * 50 / 100), 10, 80),
                DurationTicks = Mathf.Clamp(90 * 30, MinDurationTicks, MaxDurationTicks),
            });
        }

        private static void TrySynthesizeFocus(string[] tokens, string[] keywords, AiIntentKind kind,
            int strength, int durationSeconds, List<ParsedIntent> intents)
        {
            if (intents.Count >= MaxIntentsPerTurn) return;
            if (AlreadyHasKind(intents, kind)) return;
            if (!FindKeyword(tokens, keywords, out int wordIndex)) return;
            if (HasNegationBefore(tokens, wordIndex)) return;

            intents.Add(new ParsedIntent
            {
                Kind = kind,
                ParamA = Mathf.Clamp(strength, 0, 100),
                DurationTicks = Mathf.Clamp(durationSeconds * 30, MinDurationTicks, MaxDurationTicks),
            });
        }

        private static void TrySynthesizeAgeUp(string[] tokens, List<ParsedIntent> intents,
            GameSimulation sim, int aiPlayerId)
        {
            if (intents.Count >= MaxIntentsPerTurn) return;
            if (AlreadyHasKind(intents, AiIntentKind.PushAgeUp)) return;
            if (!FindKeyword(tokens, AgeUpKeywords, out int wordIndex)) return;
            if (HasNegationBefore(tokens, wordIndex)) return;

            int currentAge = sim.GetPlayerAge(aiPlayerId);
            int target = Mathf.Clamp(currentAge + 1, 2, 3);
            intents.Add(new ParsedIntent
            {
                Kind = AiIntentKind.PushAgeUp,
                ParamA = target,
                DurationTicks = Mathf.Clamp(90 * 30, MinDurationTicks, MaxDurationTicks),
            });
        }

        private static void TrySynthesizeResource(string[] tokens, List<ParsedIntent> intents)
        {
            if (intents.Count >= MaxIntentsPerTurn) return;
            if (AlreadyHasKind(intents, AiIntentKind.PrioritizeResource)) return;
            // Look for action verb adjacent to a resource word: e.g. "more wood", "need food",
            // "focus gold", "gather stone". The action verb must NOT be negated.
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                if (!IsResourceVerb(tokens[i])) continue;
                if (HasNegationBefore(tokens, i)) continue;
                int resType;
                if (!TryParseResourceToken(tokens[i + 1], out resType)) continue;
                intents.Add(new ParsedIntent
                {
                    Kind = AiIntentKind.PrioritizeResource,
                    ParamA = resType,
                    ParamB = 3, // moderate boost
                    DurationTicks = Mathf.Clamp(90 * 30, MinDurationTicks, MaxDurationTicks),
                });
                return;
            }
        }

        private static bool IsResourceVerb(string t)
            => t == "more" || t == "need" || t == "focus" || t == "gather" || t == "prioritize";

        private static bool TryParseResourceToken(string t, out int resType)
        {
            switch (t)
            {
                case "food":  resType = (int)ResourceType.Food;  return true;
                case "wood":  resType = (int)ResourceType.Wood;  return true;
                case "gold":  resType = (int)ResourceType.Gold;  return true;
                case "stone": resType = (int)ResourceType.Stone; return true;
            }
            resType = 0;
            return false;
        }

        private static bool AlreadyHasKind(List<ParsedIntent> intents, AiIntentKind kind)
        {
            for (int i = 0; i < intents.Count; i++)
                if (intents[i].Kind == kind) return true;
            return false;
        }

        private static bool IsEmptyOrGeneric(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply)) return true;
            string t = reply.Trim().ToLowerInvariant().TrimEnd('.', '!', ' ');
            return t == "got it" || t == "ok" || t == "okay" || t == "sure" || t == "yep" || t == "yes";
        }

        private static string TemplatedReplyFor(AiIntentKind kind)
        {
            switch (kind)
            {
                case AiIntentKind.AttackAt:           return "On my way to attack!";
                case AiIntentKind.DefendAt:           return "Defending now!";
                case AiIntentKind.SetAggression:      return "Adjusting my play.";
                case AiIntentKind.PrioritizeResource: return "Refocusing my villagers.";
                case AiIntentKind.FocusEconomy:       return "Booming up.";
                case AiIntentKind.FocusMilitary:      return "Going hard military.";
                case AiIntentKind.PushAgeUp:          return "Pushing for next age.";
            }
            return "Got it.";
        }

        // Whole-token prefix match for single-word entries; exact n-gram match for
        // multi-word entries. Returns the index of the first matching token.
        private static bool FindKeyword(string[] tokens, string[] keywords, out int wordIndex)
        {
            for (int t = 0; t < tokens.Length; t++)
            {
                for (int k = 0; k < keywords.Length; k++)
                {
                    string kw = keywords[k];
                    int space = kw.IndexOf(' ');
                    if (space < 0)
                    {
                        if (tokens[t].StartsWith(kw, StringComparison.Ordinal))
                        {
                            wordIndex = t;
                            return true;
                        }
                    }
                    else
                    {
                        // Multi-word: split kw on spaces and match consecutive tokens exactly.
                        if (MatchPhrase(tokens, t, kw))
                        {
                            wordIndex = t;
                            return true;
                        }
                    }
                }
            }
            wordIndex = -1;
            return false;
        }

        private static bool MatchPhrase(string[] tokens, int start, string phrase)
        {
            int j = 0;
            int t = start;
            int len = phrase.Length;
            while (j < len)
            {
                int next = phrase.IndexOf(' ', j);
                string part = next < 0 ? phrase.Substring(j) : phrase.Substring(j, next - j);
                if (t >= tokens.Length || tokens[t] != part) return false;
                t++;
                if (next < 0) return true;
                j = next + 1;
            }
            return true;
        }

        private static bool HasNegationBefore(string[] tokens, int wordIndex)
        {
            int start = Mathf.Max(0, wordIndex - 3);
            for (int i = start; i < wordIndex; i++)
                if (IsNegation(tokens[i])) return true;
            return false;
        }

        private static bool IsNegation(string t)
            => t == "not" || t == "no" || t == "never"
            || t == "dont" || t == "don't"
            || t == "wont" || t == "won't"
            || t == "cant" || t == "can't"
            || t == "shouldnt" || t == "shouldn't"
            || t == "wouldnt" || t == "wouldn't";

        // Tokenize on non-letter (apostrophe kept as part of word so "won't" stays one token).
        private static string[] Tokenize(string lower)
        {
            var list = new List<string>();
            int start = -1;
            for (int i = 0; i <= lower.Length; i++)
            {
                bool isWord = i < lower.Length && (char.IsLetter(lower[i]) || lower[i] == '\'');
                if (isWord && start < 0) start = i;
                else if (!isWord && start >= 0) { list.Add(lower.Substring(start, i - start)); start = -1; }
            }
            return list.ToArray();
        }

        // Keyword tables — prefix matches for single-word entries, exact phrase for multi-word.
        private static readonly string[] AttackKeywords    = { "attack", "push", "raid", "assault", "charge", "rush", "offens" };
        private static readonly string[] DefendKeywords    = { "defend", "hold", "protect", "guard" };
        private static readonly string[] RetreatKeywords   = { "retreat", "fall back", "play safe", "pull back", "back off", "play defensive" };
        private static readonly string[] BoomKeywords      = { "boom", "build economy", "focus eco", "go eco", "tech up", "macro" };
        private static readonly string[] MilitaryKeywords  = { "all-in", "all in", "full military", "go aggressive", "go agro", "go offensive" };
        private static readonly string[] AgeUpKeywords     = { "age up", "fast castle", "rush age", "next age", "feudal", "castle age", "imperial" };

        public static Result Parse(string json, GameSimulation sim, int aiPlayerId, int currentTick)
        {
            var result = new Result { Reply = string.Empty, Intents = new List<ParsedIntent>() };
            if (string.IsNullOrEmpty(json)) return result;

            RawShape raw;
            try { raw = JsonUtility.FromJson<RawShape>(json); }
            catch { raw = null; }
            if (raw == null) return result;

            if (!string.IsNullOrEmpty(raw.reply))
                result.Reply = raw.reply.Length > MaxReplyChars ? raw.reply.Substring(0, MaxReplyChars) : raw.reply;

            if (raw.intents == null) return result;
            for (int i = 0; i < raw.intents.Length && result.Intents.Count < MaxIntentsPerTurn; i++)
            {
                if (TryConvert(raw.intents[i], sim, aiPlayerId, out var parsed))
                    result.Intents.Add(parsed);
            }
            return result;
        }

        private static bool TryConvert(RawIntent r, GameSimulation sim, int aiPlayerId, out ParsedIntent parsed)
        {
            parsed = default;
            if (r == null || string.IsNullOrEmpty(r.kind)) return false;

            if (!TryParseKind(r.kind, out var kind)) return false;

            int duration = Mathf.Clamp(r.duration_seconds * 30, MinDurationTicks, MaxDurationTicks);
            parsed.Kind = kind;
            parsed.DurationTicks = duration;
            ParseTrigger(r, out parsed.TriggerType, out parsed.TriggerMagnitude);

            switch (kind)
            {
                case AiIntentKind.AttackAt:
                case AiIntentKind.DefendAt:
                {
                    if (!TryResolvePosition(r.target, sim, aiPlayerId, out int rawX, out int rawZ))
                        return false;
                    parsed.ParamA = rawX;
                    parsed.ParamB = rawZ;
                    parsed.ParamC = ParseUnitSubset(r.army_subset);
                    return true;
                }
                case AiIntentKind.SetAggression:
                {
                    int level = Mathf.Clamp(r.level, 0, 100);
                    // Map 0..100 → attackThreshold 32..2 (higher level = more aggressive = lower threshold)
                    parsed.ParamA = Mathf.Clamp(32 - (level * 30 / 100), 2, 32);
                    parsed.ParamB = r.retreat > 0
                        ? Mathf.Clamp(r.retreat, 10, 80)
                        : Mathf.Clamp(60 - (level * 50 / 100), 10, 80);
                    return true;
                }
                case AiIntentKind.PrioritizeResource:
                {
                    if (!TryParseResource(r.resource, out int resType)) return false;
                    parsed.ParamA = resType;
                    parsed.ParamB = Mathf.Clamp(r.weight, -5, 10);
                    return true;
                }
                case AiIntentKind.FocusEconomy:
                case AiIntentKind.FocusMilitary:
                    parsed.ParamA = Mathf.Clamp(r.level, 0, 100);
                    return true;
                case AiIntentKind.PushAgeUp:
                    if (r.age != 2 && r.age != 3) return false;
                    parsed.ParamA = r.age;
                    return true;
                case AiIntentKind.Acknowledge:
                case AiIntentKind.Decline:
                    return true;
            }
            return false;
        }

        private static bool TryParseKind(string s, out AiIntentKind kind)
        {
            switch (s)
            {
                case "AttackAt":           kind = AiIntentKind.AttackAt; return true;
                case "DefendAt":           kind = AiIntentKind.DefendAt; return true;
                case "SetAggression":      kind = AiIntentKind.SetAggression; return true;
                case "PrioritizeResource": kind = AiIntentKind.PrioritizeResource; return true;
                case "FocusEconomy":       kind = AiIntentKind.FocusEconomy; return true;
                case "FocusMilitary":      kind = AiIntentKind.FocusMilitary; return true;
                case "PushAgeUp":          kind = AiIntentKind.PushAgeUp; return true;
                case "Acknowledge":        kind = AiIntentKind.Acknowledge; return true;
                case "Decline":            kind = AiIntentKind.Decline; return true;
            }
            kind = default;
            return false;
        }

        private static bool TryParseResource(string s, out int resType)
        {
            switch (s)
            {
                case "Food":  resType = (int)ResourceType.Food; return true;
                case "Wood":  resType = (int)ResourceType.Wood; return true;
                case "Gold":  resType = (int)ResourceType.Gold; return true;
                case "Stone": resType = (int)ResourceType.Stone; return true;
            }
            resType = 0;
            return false;
        }

        private static int ParseUnitSubset(string s)
        {
            if (string.IsNullOrEmpty(s)) return UnitClassAll;
            switch (s.ToLowerInvariant())
            {
                case "archers":  return UnitClassArchers;
                case "horsemen": return UnitClassHorsemen;
                case "spearmen": return UnitClassSpearmen;
            }
            return UnitClassAll;
        }

        // Resolve a trigger spec from the raw intent. Magnitude is converted to:
        //   delay → ticks (clamped 0..15min)
        //   on_age_up → target age 2..3
        //   on_army_size → unit count 1..100
        //   on_enemy_attack → 0 (unused)
        private static void ParseTrigger(RawIntent r, out int triggerType, out int triggerMagnitude)
        {
            triggerType = TriggerImmediate;
            triggerMagnitude = 0;
            if (r == null || string.IsNullOrEmpty(r.trigger)) return;
            switch (r.trigger.ToLowerInvariant())
            {
                case "immediate":
                    triggerType = TriggerImmediate;
                    return;
                case "delay":
                    triggerType = TriggerDelay;
                    triggerMagnitude = Mathf.Clamp(r.trigger_value * 30, 0, 15 * 60 * 30); // 15 min cap
                    return;
                case "on_age_up":
                    triggerType = TriggerOnAgeUp;
                    triggerMagnitude = Mathf.Clamp(r.trigger_value, 2, 3);
                    return;
                case "on_army_size":
                    triggerType = TriggerOnArmySize;
                    triggerMagnitude = Mathf.Clamp(r.trigger_value, 1, 100);
                    return;
                case "on_enemy_attack":
                    triggerType = TriggerOnEnemyAttack;
                    return;
            }
        }

        // Position resolution mirrors ChatUI.ResolveChatPingPosition: deterministic ordering
        // by playerId, picks first non-self non-ally for enemy_base, own TC for my_base.
        // front_line is the midpoint between them.
        // Cardinal keywords resolve to map quadrant centers. Building-relative keywords
        // (near_my_X / near_their_X) pick the lowest-Id matching building.
        // Resource keywords (my_gold / their_gold) pick the gold node nearest the relevant TC.
        private static bool TryResolvePosition(string target, GameSimulation sim, int aiPlayerId,
            out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            if (string.IsNullOrEmpty(target) || sim == null) return false;

            FixedVector3? enemyTc = FindEnemyTc(sim, aiPlayerId);
            FixedVector3? ownTc = FindOwnTc(sim, aiPlayerId);

            // Anchors that don't depend on extra lookups
            switch (target)
            {
                case "enemy_base":
                    if (!enemyTc.HasValue) return false;
                    rawX = enemyTc.Value.x.Raw; rawZ = enemyTc.Value.z.Raw;
                    return true;
                case "my_base":
                    if (!ownTc.HasValue) return false;
                    rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw;
                    return true;
                case "front_line":
                    if (!enemyTc.HasValue || !ownTc.HasValue) return false;
                    rawX = (enemyTc.Value.x.Raw + ownTc.Value.x.Raw) / 2;
                    rawZ = (enemyTc.Value.z.Raw + ownTc.Value.z.Raw) / 2;
                    return true;
            }

            // Cardinal quadrants of the map
            if (target == "north" || target == "south" || target == "east" || target == "west")
            {
                if (sim.MapData == null) return false;
                float w = sim.MapData.Width;
                float h = sim.MapData.Height;
                int tileX, tileZ;
                switch (target)
                {
                    case "north": tileX = (int)(w * 0.5f); tileZ = (int)(h * 0.8f); break;
                    case "south": tileX = (int)(w * 0.5f); tileZ = (int)(h * 0.2f); break;
                    case "east":  tileX = (int)(w * 0.8f); tileZ = (int)(h * 0.5f); break;
                    default:      tileX = (int)(w * 0.2f); tileZ = (int)(h * 0.5f); break;
                }
                var pos = sim.MapData.TileToWorldFixed(tileX, tileZ);
                rawX = pos.x.Raw; rawZ = pos.z.Raw;
                return true;
            }

            // Building-relative: near_my_X / near_their_X
            const string myPrefix = "near_my_";
            const string theirPrefix = "near_their_";
            bool mine = target.StartsWith(myPrefix, StringComparison.Ordinal);
            bool theirs = !mine && target.StartsWith(theirPrefix, StringComparison.Ordinal);
            if (mine || theirs)
            {
                string suffix = target.Substring(mine ? myPrefix.Length : theirPrefix.Length);
                if (!TryParseBuildingTypeKeyword(suffix, out var btype)) return false;
                return TryResolveBuildingPosition(sim, aiPlayerId, btype, mine, out rawX, out rawZ);
            }

            // Gold nodes nearest to a base.
            if (target == "my_gold" || target == "their_gold")
            {
                FixedVector3? anchor = target == "my_gold" ? ownTc : enemyTc;
                if (!anchor.HasValue) return false;
                return TryResolveNearestResourceNode(sim, ResourceType.Gold, anchor.Value, out rawX, out rawZ);
            }

            return false;
        }

        private static bool TryParseBuildingTypeKeyword(string s, out BuildingType type)
        {
            switch (s)
            {
                case "tc":              type = BuildingType.TownCenter;    return true;
                case "barracks":        type = BuildingType.Barracks;      return true;
                case "archery":         type = BuildingType.ArcheryRange;  return true;
                case "stables":         type = BuildingType.Stables;       return true;
                case "landmark":        type = BuildingType.Landmark;      return true;
                case "mill":            type = BuildingType.Mill;          return true;
                case "mine":            type = BuildingType.Mine;          return true;
                case "lumber_yard":     type = BuildingType.LumberYard;    return true;
                case "tower":           type = BuildingType.Tower;         return true;
                case "keep":            type = BuildingType.Keep;          return true;
                case "blacksmith":      type = BuildingType.Blacksmith;    return true;
                case "market":          type = BuildingType.Market;        return true;
                case "monastery":       type = BuildingType.Monastery;     return true;
                case "siege_workshop":  type = BuildingType.SiegeWorkshop; return true;
                case "university":      type = BuildingType.University;    return true;
                case "wonder":          type = BuildingType.Wonder;        return true;
            }
            type = default;
            return false;
        }

        // Lowest-Id matching building owned by aiPlayerId (mine=true) or by any non-ally enemy.
        // BuildingRegistry returns buildings in deterministic creation order; this just picks
        // the first match, which is identical across all clients.
        private static bool TryResolveBuildingPosition(GameSimulation sim, int aiPlayerId,
            BuildingType type, bool mine, out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            int bestId = int.MaxValue;
            FixedVector3 bestPos = default;
            bool found = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.IsDestroyed) continue;
                if (b.Type != type) continue;
                bool isMine = b.PlayerId == aiPlayerId;
                bool isAlly = sim.AreAllies(b.PlayerId, aiPlayerId);
                if (mine)
                {
                    if (!isMine) continue;
                }
                else
                {
                    if (isMine || isAlly) continue;
                }
                if (b.Id < bestId)
                {
                    bestId = b.Id;
                    bestPos = b.SimPosition;
                    found = true;
                }
            }
            if (!found) return false;
            rawX = bestPos.x.Raw;
            rawZ = bestPos.z.Raw;
            return true;
        }

        private static bool TryResolveNearestResourceNode(GameSimulation sim, ResourceType type,
            FixedVector3 anchor, out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            var nodes = sim.MapData.GetAllResourceNodesSorted();
            int bestDistSq = int.MaxValue;
            FixedVector3 bestPos = default;
            bool found = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Type != type) continue;
                if (n.IsDepleted) continue;
                int dx = n.Position.x.Raw - anchor.x.Raw;
                int dz = n.Position.z.Raw - anchor.z.Raw;
                // Distance comparison uses raw squared deltas; overflow possible only on enormous maps.
                long distSq = (long)dx * dx + (long)dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = (int)Mathf.Min(int.MaxValue, distSq);
                    bestPos = n.Position;
                    found = true;
                }
            }
            if (!found) return false;
            rawX = bestPos.x.Raw;
            rawZ = bestPos.z.Raw;
            return true;
        }

        private static FixedVector3? FindOwnTc(GameSimulation sim, int aiPlayerId)
        {
            if (sim.FirstTownCenterIds.TryGetValue(aiPlayerId, out int tcId))
            {
                var tc = sim.BuildingRegistry.GetBuilding(tcId);
                if (tc != null && !tc.IsDestroyed) return tc.SimPosition;
            }
            return null;
        }

        private static FixedVector3? FindEnemyTc(GameSimulation sim, int aiPlayerId)
        {
            var keys = new List<int>(sim.FirstTownCenterIds.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                int pid = keys[i];
                if (pid == aiPlayerId) continue;
                if (sim.AreAllies(pid, aiPlayerId)) continue;
                var tc = sim.BuildingRegistry.GetBuilding(sim.FirstTownCenterIds[pid]);
                if (tc != null && !tc.IsDestroyed) return tc.SimPosition;
            }
            return null;
        }

        [Serializable]
        private class RawShape
        {
            public string reply;
            public RawIntent[] intents;
        }

        [Serializable]
        private class RawIntent
        {
            public string kind;
            public string target;
            public string resource;
            public string army_subset;
            public string trigger;
            public int weight;
            public int level;
            public int retreat;
            public int age;
            public int duration_seconds;
            public int trigger_value;
        }
    }
}
